/*
Copyright (c) 2019, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MatterControl.Common.Repository;
using MatterControl.Printing.Pipelines;
using MatterControl.Printing.PrintLeveling;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.SerialPortCommunication;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using MatterHackers.VectorMath;

namespace MatterControl.Printing
{
	/// <summary>
	/// This is the class that communicates with a RepRap printer over the serial port.
	/// It handles opening and closing the serial port and does quite a bit of gcode parsing.
	/// It should be refactored into better modules at some point.
	/// </summary>
	public class PrinterConnection : IDisposable
	{
		public static event EventHandler AnyCommunicationStateChanged;

		public event EventHandler Disposed;

		public event EventHandler TemporarilyHoldingTemp;

		public event EventHandler<DeviceErrorArgs> ErrorReported;

		public event EventHandler BedTemperatureRead;

		public event EventHandler CommunicationStateChanged;

		public event EventHandler DetailedPrintingStateChanged;

		public event EventHandler<ConnectFailedEventArgs> ConnectionFailed;

		public event EventHandler ConnectionSucceeded;

		public void OnPauseOnLayer(PrintPauseEventArgs printPauseEventArgs)
		{
			PauseOnLayer?.Invoke(this, printPauseEventArgs);
		}

		public event EventHandler DestinationChanged;

		public event EventHandler HomingPositionChanged;

		public event EventHandler HotendTemperatureRead;

		public event EventHandler<int> HotendTargetTemperatureChanged;

		public event EventHandler BedTargetTemperatureChanged;

		public event EventHandler FanSpeedSet;

		public event EventHandler FirmwareVersionRead;

		public void OnFilamentRunout(PrintPauseEventArgs printPauseEventArgs)
		{
			FilamentRunout?.Invoke(this, printPauseEventArgs);
		}

		public event EventHandler<string> PrintFinished;

		public event EventHandler PrintCanceled;

		public event EventHandler<PrintPauseEventArgs> PauseOnLayer;

		public event EventHandler<PrintPauseEventArgs> FilamentRunout;

		public event EventHandler<string> LineReceived;

		public event EventHandler<string> LineSent;

		public bool WaitingForPositionRead
		{
			get
			{
				// make sure the longest we will wait under any circumstance is 60 seconds
				if (waitingForPosition.ElapsedMilliseconds > 60000)
				{
					waitingForPosition.Reset();
					PositionReadType = PositionReadType.None;
				}

				return waitingForPosition.IsRunning || (PositionReadType != PositionReadType.None);
			}
		}

		public bool ContinueHoldingTemperature { get; set; }

		public double SecondsToHoldTemperature { get; private set; }

		public event EventHandler AtxPowerStateChanged;

		private bool atxPowerIsOn = false;

		internal const int MaxExtruders = 16;

		private const int MaxInvalidConnectionChars = 3;

		private readonly object locker = new object();

		private double actualBedTemperature;

		public int ActiveExtruderIndex
		{
			get => (toolChangeStream != null) ? toolChangeStream.RequestedTool : 0;
		}

		private readonly double[] actualHotendTemperature = new double[MaxExtruders];

		private readonly CheckSumLines allCheckSumLinesSent = new CheckSumLines();

		private CommunicationStates _communicationState = CommunicationStates.Disconnected;

		private PrinterMove currentDestination;

		private double currentSdBytes = 0;

		private double fanSpeed;

		private int currentLineIndexToSend = 0;

		private bool forceImmediateWrites = false;

		private string lastLineRead = "";

		public Stopwatch TimeHaveBeenHoldingTemperature { get; set; }

		private PrinterMove lastReportedPosition = PrinterMove.Unknown;

		private GCodeSwitcher gCodeFileSwitcher = null;
		private PauseHandlingStream pauseHandlingStream = null;
		private QueuedCommandsStream queuedCommandStream = null;
		private MaxLengthStream maxLengthStream;
		private ToolChangeStream toolChangeStream;
		private PrintLevelingStream printLevelingStream = null;
		private WaitForTempStream waitForTempStream = null;

		private GCodeStream totalGCodeStream = null;

		public CommunicationStates PrePauseCommunicationState { get; private set; } = CommunicationStates.Printing;

		private DetailedPrintingState _detailedPrintingState;

		private readonly ContainsStringLineActions readLineContainsCallBacks = new ContainsStringLineActions();

		private readonly StartsWithLineActions readLineStartCallBacks = new StartsWithLineActions();

		// we start out by setting it to a nothing file
		public IFrostedSerialPort serialPort { get; private set; }

		private double _targetBedTemperature;

		private readonly double[] targetHotendTemperature = new double[MaxExtruders];

		private readonly Stopwatch timeHaveBeenWaitingForOK = new Stopwatch();

		private readonly Stopwatch timeSinceLastReadAnything = new Stopwatch();

		private readonly Stopwatch timeSinceLastWrite = new Stopwatch();

		private readonly Stopwatch timeSinceRecievedOk = new Stopwatch();

		private readonly Stopwatch timePrinting = new Stopwatch();

		private readonly Stopwatch timeWaitingForSdProgress = new Stopwatch();

		private double totalSdBytes = 0;

		private PositionReadType PositionReadType { get; set; } = PositionReadType.None;

		private readonly Stopwatch waitingForPosition = new Stopwatch();

		private readonly ContainsStringLineActions writeLineContainsCallBacks = new ContainsStringLineActions();

		private readonly StartsWithLineActions writeLineStartCallBacks = new StartsWithLineActions();

		private double secondsSinceUpdateHistory = 0;
		private long lineSinceUpdateHistory = 0;

		// TODO: To be replace with DI container instance
		private static PrintJobRepository repository;

		static PrinterConnection()
		{
			var printTaskContext = new PrintServerContext();
			printTaskContext.Database.EnsureCreated();

			repository = new PrintJobRepository(printTaskContext);
		}

		public PrinterConnection(PrintHostConfig printer)
		{
			this.Printer = printer;

			MonitorPrinterTemperature = true;

			readLineStartCallBacks.Register("start", FoundStart);
			readLineStartCallBacks.Register("start", PrintingCanContinue);

			readLineStartCallBacks.Register("ok", SuppressEcho);
			readLineStartCallBacks.Register("wait", SuppressEcho);
			readLineStartCallBacks.Register("T:", SuppressEcho); // repetier

			readLineStartCallBacks.Register("ok", PrintingCanContinue);
			readLineStartCallBacks.Register("Done saving file", PrintingCanContinue);

			readLineStartCallBacks.Register("B:", ReadTemperatures); // smoothie
			readLineContainsCallBacks.Register("T0:", ReadTemperatures); // marlin
			readLineContainsCallBacks.Register("T:", ReadTemperatures); // repetier

			readLineStartCallBacks.Register("SD printing byte", ReadSdProgress); // repetier

			readLineStartCallBacks.Register("C:", ReadTargetPositions);
			readLineStartCallBacks.Register("ok C:", ReadTargetPositions); // smoothie is reporting the C: with an ok first.
			readLineStartCallBacks.Register("X:", ReadTargetPositions);
			readLineStartCallBacks.Register("ok X:", ReadTargetPositions);

			readLineStartCallBacks.Register("rs ", PrinterRequestsResend); // smoothie is lower case and no :
			readLineStartCallBacks.Register("RS:", PrinterRequestsResend);
			readLineContainsCallBacks.Register("Resend:", PrinterRequestsResend);

			readLineContainsCallBacks.Register("FIRMWARE_NAME:", PrinterStatesFirmware);

			// smoothie failures
			readLineContainsCallBacks.Register("T:inf", PrinterReportsError);
			readLineContainsCallBacks.Register("B:inf", PrinterReportsError);
			readLineContainsCallBacks.Register("ZProbe not triggered", PrinterReportsError);
			readLineStartCallBacks.Register("ERROR: Homing cycle failed", PrinterReportsError);

			// marlin failures
			readLineContainsCallBacks.Register("MINTEMP", PrinterReportsError);
			readLineContainsCallBacks.Register("MAXTEMP", PrinterReportsError);
			readLineContainsCallBacks.Register("M999", PrinterReportsError);
			readLineContainsCallBacks.Register("Error: Extruder switched off", PrinterReportsError);
			readLineContainsCallBacks.Register("Heater decoupled", PrinterReportsError);
			readLineContainsCallBacks.Register("cold extrusion prevented", PrinterReportsError);
			readLineContainsCallBacks.Register("Error:Thermal Runaway, system stopped!", PrinterReportsError);
			readLineContainsCallBacks.Register("Error:Heating failed", PrinterReportsError);
			readLineStartCallBacks.Register("temp sensor defect", PrinterReportsError);
			readLineStartCallBacks.Register("Error:Printer halted", PrinterReportsError);

			// repetier failures
			readLineContainsCallBacks.Register("dry run mode", PrinterReportsError);
			readLineStartCallBacks.Register("accelerometer send i2c error", PrinterReportsError);
			readLineStartCallBacks.Register("accelerometer i2c recv error", PrinterReportsError);

			// s3g failures
			readLineContainsCallBacks.Register("Bot is Shutdown due to Overheat", PrinterReportsError);

			writeLineStartCallBacks.Register("M80", AtxPowerUpWasWritenToPrinter);
			writeLineStartCallBacks.Register("M81", AtxPowerDownWasWritenToPrinter);
			writeLineStartCallBacks.Register("M104", HotendTemperatureWasWritenToPrinter);
			writeLineStartCallBacks.Register("M106", FanSpeedWasWritenToPrinter);
			writeLineStartCallBacks.Register("M107", FanOffWasWritenToPrinter);
			writeLineStartCallBacks.Register("M109", HotendTemperatureWasWritenToPrinter);
			writeLineStartCallBacks.Register("M140", BedTemperatureWasWritenToPrinter);
			writeLineStartCallBacks.Register("M190", BedTemperatureWasWritenToPrinter);

			Task.Run(() =>
			{
				this.OnIdle();
				Thread.Sleep(10);
			});

			printer.Settings.SettingChanged += (s, stringEvent) =>
			{
				var extruder = -1;
				switch (stringEvent.Data)
				{
					case SettingsKey.temperature:
						extruder = 0;
						break;

					case SettingsKey.temperature1:
						extruder = 1;
						break;

					case SettingsKey.temperature2:
						extruder = 2;
						break;

					case SettingsKey.temperature3:
						extruder = 3;
						break;
				}

				if (extruder > -1)
				{
					if (this.Printing
						&& (this.DetailedPrintingState == DetailedPrintingState.HeatingT0
							|| this.DetailedPrintingState == DetailedPrintingState.HeatingT1))
					{
					}
					else
					{
						double goalTemp = this.GetTargetHotendTemperature(extruder);
						if (goalTemp > 0)
						{
							var newGoal = printer.Settings.GetValue<double>(stringEvent.Data);
							this.SetTargetHotendTemperature(extruder, newGoal);
						}
					}
				}

				if (stringEvent.Data == SettingsKey.bed_temperature)
				{
					if (this.Printing
						&& this.DetailedPrintingState == DetailedPrintingState.HeatingBed)
					{
					}
					else
					{
						double goalTemp = this.TargetBedTemperature;
						if (goalTemp > 0)
						{
							var newGoal = printer.Settings.GetValue<double>(SettingsKey.bed_temperature);
							this.TargetBedTemperature = newGoal;
						}
					}
				}
			};
		}

		public double ActualBedTemperature => actualBedTemperature;

		// PrinterSettings/Options {{
		public int BaudRate
		{
			get
			{
				if (string.IsNullOrEmpty(Printer.Settings.GetValue(SettingsKey.baud_rate)))
				{
					return 250000;
				}

				return Printer.Settings.GetValue<int>(SettingsKey.baud_rate);
			}
		}

		public double FeedRateRatio => Printer.Settings.GetValue<double>(SettingsKey.feedrate_ratio);

		public string ConnectGCode => Printer.Settings.GetValue(SettingsKey.connect_gcode);

		public string CancelGCode => Printer.Settings.GetValue(SettingsKey.cancel_gcode);

		public int ExtruderCount => Printer.Settings.GetValue<int>(SettingsKey.extruder_count);

		public bool SendWithChecksum => Printer.Settings.GetValue<bool>(SettingsKey.send_with_checksum);

		public bool EnableNetworkPrinting => Printer.Settings.GetValue<bool>(SettingsKey.enable_network_printing);

		public bool AutoReleaseMotors => Printer.Settings.GetValue<bool>(SettingsKey.auto_release_motors);

		public bool RecoveryIsEnabled => Printer.Settings.GetValue<bool>(SettingsKey.recover_is_enabled);

		private readonly List<(Regex Regex, string Replacement)> readLineReplacements = new List<(Regex Regex, string Replacement)>();

		public void InitializeReadLineReplacements()
		{
			var readRegEx = Printer.Settings.GetValue(SettingsKey.read_regex);

			// Clear and rebuild the replacement list
			readLineReplacements.Clear();

			foreach (string regExLine in readRegEx.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries))
			{
				var matches = ProcessWriteRegexStream.GetQuotedParts.Matches(regExLine);
				if (matches.Count == 2)
				{
					var search = matches[0].Value.Substring(1, matches[0].Value.Length - 2);
					var replace = matches[1].Value.Substring(1, matches[1].Value.Length - 2);
					readLineReplacements.Add((new Regex(search, RegexOptions.Compiled), replace));
				}
			}
		}

		// PrinterSettings/Options }}

		private bool communicationPossible = false;

		public CommunicationStates CommunicationState
		{
			get => _communicationState;
			set
			{
				switch (value)
				{
					case CommunicationStates.AttemptingToConnect:
						// TODO: Investigate the validity of this claim/warning
						// #if DEBUG
						// if (serialPort == null)
						// {
						// throw new Exception("The serial port should be constructed prior to setting this or we can fail our connection on a write before it has a chance to be created.");
						// }
						// #endif
						break;

					case CommunicationStates.Connected:
						communicationPossible = true;
						QueueLine("M115");
						ReadPosition(PositionReadType.Other);
						break;

					case CommunicationStates.ConnectionLost:
					case CommunicationStates.Disconnected:
						if (communicationPossible)
						{
							TurnOffBedAndExtruders(TurnOff.Now);
							for (int hotendIndex = 0; hotendIndex < MaxExtruders; hotendIndex++)
							{
								actualHotendTemperature[hotendIndex] = 0;
								OnHotendTemperatureRead(new TemperatureEventArgs(hotendIndex, GetActualHotendTemperature(hotendIndex)));
							}

							actualBedTemperature = 0;
							OnBedTemperatureRead(new TemperatureEventArgs(0, ActualBedTemperature));
						}

						communicationPossible = false;

						break;
				}

				if (_communicationState != value)
				{
					// TODO: Not really an error, not really worth adding LogInfo eventing when these events need to be ripped out if we move out of process. In the short
					// term no harm is caused by reusing LogError and the message simply dumps to the printer terminal without any "Error" messaging accomanying it
					this.LogError($"Communication State: {value}", ErrorSource.Connection);

					switch (_communicationState)
					{
						// if it was printing
						case CommunicationStates.PrintingFromSd:
						case CommunicationStates.Printing:
							{
								// and is changing to paused
								if (value == CommunicationStates.Paused)
								{
									if (_communicationState == CommunicationStates.Printing)
									{
										PrePauseCommunicationState = CommunicationStates.Printing;
									}
									else
									{
										PrePauseCommunicationState = CommunicationStates.PrintingFromSd;
									}

									timePrinting.Stop();
								}
								else if (value == CommunicationStates.FinishedPrint)
								{
									if (ActivePrintTask != null)
									{
										ActivePrintTask.PrintEnd = DateTime.Now;
										ActivePrintTask.PercentDone = 100;
										ActivePrintTask.PrintComplete = true;

										repository.Update(ActivePrintTask);
									}

									// Set this early as we always want our functions to know the state we are in.
									_communicationState = value;
									timePrinting.Stop();

									if (!string.IsNullOrWhiteSpace(this.ActivePrintName))
									{
										PrintFinished?.Invoke(this, this.ActivePrintName);
									}
								}
								else
								{
									timePrinting.Stop();
									timePrinting.Reset();
								}
							}

							break;

						// was paused
						case CommunicationStates.Paused:
							{
								// changing to printing
								if (value == CommunicationStates.Printing)
								{
									timePrinting.Start();
								}
							}

							break;

						default:
							if (!timePrinting.IsRunning
								&& value == CommunicationStates.Printing)
							{
								// If we are just starting to print (we know we were not paused or it would have stopped above)
								timePrinting.Restart();
							}

							break;
					}

					_communicationState = value;
					CommunicationStateChanged?.Invoke(this, null);
					AnyCommunicationStateChanged?.Invoke(this, null);
				}
			}
		}

		public void SwitchToGCode(string gCodeFilePath)
		{
			gCodeFileSwitcher.SwitchTo(gCodeFilePath);
		}

		public string ComPort => Printer.Settings?.Helpers.ComPort();

		public string DriverType => (this.ComPort == "Emulator") ? "Emulator" : Printer.Settings?.GetValue(SettingsKey.driver_type);

		public bool AtxPowerEnabled
		{
			get => atxPowerIsOn;
			set
			{
				if (value)
				{
					QueueLine("M80");
				}
				else
				{
					QueueLine("M81");
				}
			}
		}

		public double CurrentExtruderDestination => currentDestination.extrusion;

		public Vector3 CurrentDestination => currentDestination.position;

		public int CurrentlyPrintingLayer
		{
			get
			{
				if (gCodeFileSwitcher != null)
				{
					return gCodeFileSwitcher?.GCodeFile?.GetLayerIndex(gCodeFileSwitcher.LineIndex) ?? -1;
				}

				return -1;
			}
		}

		public string DeviceCode { get; private set; }

		public bool Disconnecting => CommunicationState == CommunicationStates.Disconnecting;

		public double FanSpeed0To255
		{
			get => fanSpeed;
			set
			{
				fanSpeed = Math.Max(0, Math.Min(255, value));
				OnFanSpeedSet(null);
				if (this.IsConnected)
				{
					QueueLine($"M106 S{(int)(fanSpeed + .5)}");
				}
			}
		}

		public FirmwareTypes FirmwareType { get; private set; } = FirmwareTypes.Unknown;

		public string FirmwareVersion { get; private set; }

		public Vector3 LastReportedPosition => lastReportedPosition.position;

		public bool MonitorPrinterTemperature { get; set; }

		public double PercentComplete
		{
			get
			{
				if (CommunicationState == CommunicationStates.PrintingFromSd
					|| (CommunicationState == CommunicationStates.Paused && PrePauseCommunicationState == CommunicationStates.PrintingFromSd))
				{
					if (totalSdBytes > 0)
					{
						return currentSdBytes / totalSdBytes * 100;
					}

					return 0;
				}

				if (PrintIsFinished && !Paused)
				{
					return 100.0;
				}
				else if (NumberOfLinesInCurrentPrint > 0
					&& gCodeFileSwitcher?.GCodeFile != null)
				{
					return gCodeFileSwitcher.GCodeFile.PercentComplete(gCodeFileSwitcher.LineIndex);
				}
				else
				{
					return 0.0;
				}
			}
		}

		public bool IsConnected
		{
			get
			{
				switch (CommunicationState)
				{
					case CommunicationStates.Disconnected:
					case CommunicationStates.AttemptingToConnect:
					case CommunicationStates.ConnectionLost:
					case CommunicationStates.FailedToConnect:
						return false;

					case CommunicationStates.Disconnecting:
					case CommunicationStates.Connected:
					case CommunicationStates.PreparingToPrint:
					case CommunicationStates.Printing:
					case CommunicationStates.PrintingFromSd:
					case CommunicationStates.Paused:
					case CommunicationStates.FinishedPrint:
						return true;

					default:
						throw new NotImplementedException("Make sure every status returns the correct connected state.");
				}
			}
		}

		public bool Paused => CommunicationState == CommunicationStates.Paused;

		public bool Printing
		{
			get
			{
				switch (CommunicationState)
				{
					case CommunicationStates.Disconnected:
					case CommunicationStates.Disconnecting:
					case CommunicationStates.AttemptingToConnect:
					case CommunicationStates.ConnectionLost:
					case CommunicationStates.FailedToConnect:
					case CommunicationStates.Connected:
					case CommunicationStates.PreparingToPrint:
					case CommunicationStates.Paused:
					case CommunicationStates.FinishedPrint:
						return false;

					case CommunicationStates.Printing:
					case CommunicationStates.PrintingFromSd:
						return true;

					default:
						throw new NotImplementedException("Make sure every status returns the correct connected state.");
				}
			}
		}

		public DetailedPrintingState DetailedPrintingState
		{
			get => _detailedPrintingState;
			set
			{
				if (_detailedPrintingState != value)
				{
					_detailedPrintingState = value;
					DetailedPrintingStateChanged?.Invoke(this, null);
				}
			}
		}

		public bool PrintIsActive
		{
			get
			{
				switch (CommunicationState)
				{
					case CommunicationStates.Disconnected:
					case CommunicationStates.Disconnecting:
					case CommunicationStates.AttemptingToConnect:
					case CommunicationStates.ConnectionLost:
					case CommunicationStates.FailedToConnect:
					case CommunicationStates.Connected:
					case CommunicationStates.FinishedPrint:
						return false;

					case CommunicationStates.Printing:
					case CommunicationStates.PrintingFromSd:
					case CommunicationStates.PreparingToPrint:
					case CommunicationStates.Paused:
						return true;

					default:
						throw new NotImplementedException("Make sure every status returns the correct connected state.");
				}
			}
		}

		public bool PrintIsFinished => CommunicationState == CommunicationStates.FinishedPrint;

		public string PrintJobName { get; private set; } = null;

		public bool PrintWasCanceled { get; set; } = false;

		public double RatioIntoCurrentLayerSeconds
		{
			get
			{
				if (gCodeFileSwitcher?.GCodeFile == null
					|| !(gCodeFileSwitcher?.GCodeFile is GCodeMemoryFile))
				{
					return 0;
				}

				return gCodeFileSwitcher.GCodeFile.Ratio0to1IntoContainedLayerSeconds(gCodeFileSwitcher.LineIndex);
			}
		}

		public double RatioIntoCurrentLayerInstructions
		{
			get
			{
				if (gCodeFileSwitcher?.GCodeFile == null
					|| !(gCodeFileSwitcher?.GCodeFile is GCodeMemoryFile))
				{
					return 0;
				}

				return gCodeFileSwitcher.GCodeFile.Ratio0to1IntoContainedLayerInstruction(gCodeFileSwitcher.LineIndex);
			}
		}

		public int SecondsToEnd
		{
			get
			{
				if (gCodeFileSwitcher?.GCodeFile == null)
				{
					return 0;
				}

				return (int)gCodeFileSwitcher.GCodeFile.Instruction(gCodeFileSwitcher.LineIndex).SecondsToEndFromHere;
			}
		}

		public int SecondsPrinted
		{
			get
			{
				if (Printing || Paused || PrintIsFinished)
				{
					return (int)(timePrinting.ElapsedMilliseconds / 1000);
				}

				return 0;
			}
		}

		public double TargetBedTemperature
		{
			get => _targetBedTemperature;
			set
			{
				ContinueHoldingTemperature = false;
				if (_targetBedTemperature != value)
				{
					_targetBedTemperature = value;
					if (this.IsConnected)
					{
						QueueLine($"M140 S{_targetBedTemperature}");
					}

					BedTargetTemperatureChanged?.Invoke(this, null);
				}
			}
		}

		public int TotalLayersInPrint => gCodeFileSwitcher?.GCodeFile?.LayerCount ?? -1;

		private int NumberOfLinesInCurrentPrint => gCodeFileSwitcher?.GCodeFile?.LineCount ?? -1;

		public int TotalSecondsInPrint
		{
			get
			{
				if (gCodeFileSwitcher?.GCodeFile?.LineCount > 0)
				{
					if (this.FeedRateRatio != 0)
					{
						return (int)(gCodeFileSwitcher.GCodeFile.TotalSecondsInPrint / this.FeedRateRatio);
					}

					return (int)gCodeFileSwitcher.GCodeFile.TotalSecondsInPrint;
				}

				return 0;
			}
		}

		public PrintHostConfig Printer { get; }

		public void ReleaseAndReportFailedConnection(ConnectionFailure reason, string message = null)
		{
			// Shutdown the serial port
			if (serialPort != null)
			{
				// Close and dispose the serial port
				serialPort.Close();
				serialPort.Dispose();
				serialPort = null;
			}

			// Notify
			OnConnectionFailed(reason, message);
		}

		public void LogError(string message, ErrorSource source)
		{
			this.ErrorReported(this, new DeviceErrorArgs()
			{
				Message = message,
				Source = source
			});
		}

		public void BedTemperatureWasWritenToPrinter(string line)
		{
			string[] splitOnS = line.Split('S');
			if (splitOnS.Length == 2)
			{
				string temp = splitOnS[1];
				try
				{
					double tempBeingSet = double.Parse(temp);
					if (TargetBedTemperature != tempBeingSet)
					{
						// we set the private variable so that we don't get the callbacks called and get in a loop of setting the temp
						_targetBedTemperature = tempBeingSet;
						BedTargetTemperatureChanged?.Invoke(this, null);
					}
				}
				catch
				{
				}
			}
		}

		public void Connect()
		{
			// TODO: Consider adding any conditions that would results in a connection failure to this initial test
			// Start the process of requesting permission and exit if permission is not currently granted
			if (!this.EnableNetworkPrinting
				&& !FrostedSerialPort.EnsureDeviceAccess())
			{
				// TODO: Consider calling OnConnectionFailed as we do below to fire events that indicate connection failed
				CommunicationState = CommunicationStates.FailedToConnect;
				return;
			}

			// Attempt connecting to a specific printer
			this.FirmwareType = FirmwareTypes.Unknown;

			// On Android, there will never be more than one serial port available for us to connect to. Override the current .ComPort value to account for
			// this aspect to ensure the validation logic that verifies port availability/in use status can proceed without additional workarounds for Android
#if __ANDROID__
				string currentPortName = FrostedSerialPort.GetPortNames().FirstOrDefault();
				if (!string.IsNullOrEmpty(currentPortName))
				{
					// TODO: Ensure that this does *not* cause a write to the settings file and should be an in memory update only
					Printer.Settings?.Helpers.SetComPort(currentPortName);
				}
#endif

			if (SerialPortIsAvailable(this.ComPort))
			{
				// Create and start connection thread
				Task.Run(() =>
				{
					Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

					// Allow the user to set the appropriate properties.
					var portNames = FrostedSerialPort.GetPortNames();

					// Debug.WriteLine("Open ports: {0}".FormatWith(portNames.Length));
					if (portNames.Length > 0 || IsNetworkPrinting())
					{
						// AttemptToConnect {{
						{
							string serialPortName = this.ComPort;
							int baudRate = this.BaudRate;

							// make sure we don't have a left over print task
							ActivePrintTask = null;

							if (this.IsConnected)
							{
								this.OnConnectionFailed(ConnectionFailure.AlreadyConnected);
								return;
							}

							var portFactory = FrostedSerialPortFactory.GetAppropriateFactory(this.DriverType);

							bool serialPortIsAvailable = portFactory.SerialPortIsAvailable(serialPortName, Printer.Settings);
							bool serialPortIsAlreadyOpen = this.ComPort != "Emulator" &&
								portFactory.SerialPortAlreadyOpen(serialPortName);

							if (serialPortIsAvailable && !serialPortIsAlreadyOpen)
							{
								if (!this.IsConnected)
								{
									try
									{
										// AttemptingToConnect must come before we open the port, as TcipSerialPort does not return immediately and
										// we're actually doing the bulk of the connection time in CreateAndOpen
										CommunicationState = CommunicationStates.AttemptingToConnect;

										serialPort = portFactory.CreateAndOpen(serialPortName, Printer.Settings, baudRate, true);
#if __ANDROID__
										ToggleHighLowHigh(serialPort);
#endif
										// TODO: Review and reconsider the cases where this was required
										// wait a bit of time to let the firmware start up
										// Thread.Sleep(500);

										// We have to send a line because some printers (like old print-r-bots) do not send anything when connecting and there is no other way to know they are there.
										foreach (var line in ProcessWriteRegexStream.ProcessWriteRegEx("M105\n", this.Printer))
										{
											WriteRaw(line, line);
										}

										var sb = new StringBuilder();

										// Read character data until we see a newline or exceed the MAX_INVALID_CONNECTION_CHARS threshold
										while (true)
										{
											// Plugins required probing to fill read buffer
											var na = serialPort.BytesToRead;

											// Read, sanitize, store
											string response = serialPort.ReadExisting().Replace("\r\n", "\n").Replace('\r', '\n');
											sb.Append(response);

											bool hasNewline = response.Contains('\n');
											if (hasNewline || response.Contains('?'))
											{
												int invalidCharactersOnFirstLine = sb.ToString().Split('?').Length - 1;
												if (hasNewline
													&& invalidCharactersOnFirstLine <= MaxInvalidConnectionChars)
												{
													// Exit loop, continue with connect
													break;
												}
												else if (invalidCharactersOnFirstLine > MaxInvalidConnectionChars)
												{
													// Abort if we've exceeded the invalid char count

													// Force port shutdown and cleanup
													ReleaseAndReportFailedConnection(ConnectionFailure.MaximumErrorsReached);
													return;
												}
											}
										}

										// Place all consumed data back in the buffer to be processed by ReadFromPrinter

										// Setting connected before calling ReadThread.Start causes the misguided CheckOnPrinter logic to spin up new  ReadThreads
										/*
										// Switch to connected state when a newline is found and we haven't exceeded the invalid char count
										CommunicationState = CommunicationStates.Connected;
										*/

										CreateStreamProcessors();

										TurnOffBedAndExtruders(TurnOff.Now); // make sure our ui and the printer agree and that the printer is in a known state (not heating).
										haveReportedError = false;

										QueueLine(this.ConnectGCode);

										// Call instance event
										ConnectionSucceeded?.Invoke(this, null);

										Console.WriteLine("ReadFromPrinter thread created.");
										ReadThread.Start(this);

										CommunicationState = CommunicationStates.Connected;

										// We do not need to wait for the M105
										PrintingCanContinue(null);
									}
									catch (ArgumentOutOfRangeException ex)
									{
										OnConnectionFailed(ConnectionFailure.UnsupportedBaudRate, ex.Message, ex.GetType().ToString());
									}
									catch (IOException ex)
									{
										OnConnectionFailed(ConnectionFailure.IOException, ex.Message, ex.GetType().ToString());
									}
									catch (TimeoutException ex)
									{
										OnConnectionFailed(ConnectionFailure.ConnectionTimeout, ex.Message, ex.GetType().ToString());
									}
									catch (Exception ex)
									{
										OnConnectionFailed(ConnectionFailure.Unknown, ex.Message, ex.GetType().ToString());
									}
								}
							}
							else
							{
								if (serialPortIsAlreadyOpen)
								{
									OnConnectionFailed(ConnectionFailure.PortInUse);
								}
								else
								{
									OnConnectionFailed(ConnectionFailure.PortUnavailable);
								}
							}
						}

						// AttemptToConnect }}

						if (CommunicationState == CommunicationStates.FailedToConnect)
						{
							OnConnectionFailed(ConnectionFailure.FailedToConnect);
						}
					}
					else
					{
						OnConnectionFailed(ConnectionFailure.PortUnavailable);
					}
				});
			}
			else
			{
				OnConnectionFailed(ConnectionFailure.PortUnavailable);
			}
		}

		public void DeleteFileFromSdCard(string fileName)
		{
			// Register to detect the file deleted confirmation.
			// This should have worked without this by getting the normal 'ok' on the next line. But the ok is not on its own line.
			readLineStartCallBacks.Register("File deleted:", FileDeleteConfirmed);
			// and send the line to delete the file
			QueueLine($"M30 {fileName.ToLower()}");
		}

		/// <summary>
		/// Disable the currently active printer connection and job if it is being actively controlled by MC
		/// If we are observing an SD card print, do nothing.
		/// </summary>
		public void Disable()
		{
			if (this.CommunicationState == CommunicationStates.PrintingFromSd
				|| (this.Paused && this.PrePauseCommunicationState == CommunicationStates.PrintingFromSd))
			{
				// don't turn off anything if we are printing from sd
				return;
			}

			if (this.IsConnected)
			{
				// Make sure we send this without waiting for the printer to respond. We want to try and turn off the heaters.
				// It may be possible in the future to make this go into the printer queue for assured sending but it means
				// the program has to be smart about closing an able to wait until the printer has agreed that it shut off
				// the motors and heaters (a good idea and something for the future).
				forceImmediateWrites = true;
				ReleaseMotors();
				TurnOffBedAndExtruders(TurnOff.Now);
				FanSpeed0To255 = 0;
				forceImmediateWrites = false;

				CommunicationState = CommunicationStates.Disconnecting;
				currentReadThreadIndex++;
				if (serialPort != null)
				{
					serialPort.Close();
					serialPort.Dispose();
				}

				serialPort = null;
			}
			else
			{
				// Need to reset UI - even if manual disconnect
				TurnOffBedAndExtruders(TurnOff.Now);
				FanSpeed0To255 = 0;
			}

			CommunicationState = CommunicationStates.Disconnected;
		}

		public void HotendTemperatureWasWritenToPrinter(string line)
		{
			double tempBeingSet = 0;
			if (GCodeFile.GetFirstNumberAfter("S", line, ref tempBeingSet))
			{
				int extruderIndex = 0;
				if (GCodeFile.GetFirstNumberAfter("T", line, ref extruderIndex))
				{
					// we set the private variable so that we don't get the callbacks called and get in a loop of setting the temp
					int hotendIndex0Based = Math.Min(extruderIndex, MaxExtruders - 1);
					targetHotendTemperature[hotendIndex0Based] = tempBeingSet;
				}
				else
				{
					// we set the private variable so that we don't get the callbacks called and get in a loop of setting the temp
					targetHotendTemperature[ActiveExtruderIndex] = tempBeingSet;
				}

				HotendTargetTemperatureChanged?.Invoke(this, extruderIndex);
			}
		}

		public void FanOffWasWritenToPrinter(string line)
		{
			fanSpeed = 0;
			OnFanSpeedSet(null);
		}

		public void FanSpeedWasWritenToPrinter(string line)
		{
			string[] splitOnS = line.Split('S');
			if (splitOnS.Length != 2)
			{
				// when there is no explicit S value the assumption is 255
				splitOnS = "M106 S255".Split('S');
			}

			if (splitOnS.Length == 2)
			{
				string fanSpeedString = splitOnS[1];
				try
				{
					int fanSpeedBeingSet = int.Parse(fanSpeedString);
					if (FanSpeed0To255 != fanSpeedBeingSet)
					{
						fanSpeed = fanSpeedBeingSet;
						OnFanSpeedSet(null);
					}
				}
				catch (Exception)
				{
				}
			}
		}

		public void FoundStart(string line)
		{
		}

		public double GetActualHotendTemperature(int hotendIndex0Based)
		{
			hotendIndex0Based = Math.Min(hotendIndex0Based, MaxExtruders - 1);
			return actualHotendTemperature[hotendIndex0Based];
		}

		public double GetTargetHotendTemperature(int hotendIndex0Based)
		{
			hotendIndex0Based = Math.Min(hotendIndex0Based, MaxExtruders - 1);
			return targetHotendTemperature[hotendIndex0Based];
		}

		public void HaltConnectionThread()
		{
			// TODO: stopTryingToConnect is not longer used by anyone. Likely we need to wire up setting CancellationToken from this context
			// this.stopTryingToConnect = true;
		}

		public void HomeAxis(PrinterAxis axis)
		{
			string command = "G28";

			// If we are homing everything we don't need to add any details
			if (!axis.HasFlag(PrinterAxis.XYZ))
			{
				if ((axis & PrinterAxis.X) == PrinterAxis.X)
				{
					command += " X0";
				}

				if ((axis & PrinterAxis.Y) == PrinterAxis.Y)
				{
					command += " Y0";
				}

				if ((axis & PrinterAxis.Z) == PrinterAxis.Z)
				{
					command += " Z0";
				}
			}

			QueueLine(command);
		}

		public void MoveAbsolute(PrinterAxis axis, double axisPositionMm, double feedRateMmPerMinute)
		{
			SetMovementToAbsolute();
			QueueLine(string.Format("G1 {0}{1:0.###} F{2}", axis, axisPositionMm, feedRateMmPerMinute));
		}

		public void MoveAbsolute(Vector3 position, double feedRateMmPerMinute)
		{
			SetMovementToAbsolute();
			QueueLine(string.Format("G1 X{0:0.###}Y{1:0.###}Z{2:0.###} F{3}", position.X, position.Y, position.Z, feedRateMmPerMinute));
		}

		public void MoveExtruderRelative(double moveAmountMm, double feedRateMmPerMinute, int extruderNumber = 0)
		{
			if (moveAmountMm != 0)
			{
				// TODO: Long term we need to track the active extruder and make requiresToolChange be driven by the extruder you're actually on
				bool requiresToolChange = extruderNumber != ActiveExtruderIndex;

				SetMovementToRelative();

				if (requiresToolChange)
				{
					var currentExtruderIndex = ActiveExtruderIndex;
					// Set to extrude to use
					QueueLine($"T{extruderNumber}");
					QueueLine($"G1 E{moveAmountMm:0.####} F{feedRateMmPerMinute}");
					// Reset back to previous extruder
					QueueLine($"T{currentExtruderIndex}");
				}
				else
				{
					QueueLine($"G1 E{moveAmountMm:0.####} F{feedRateMmPerMinute}");
				}

				SetMovementToAbsolute();
			}
		}

		public void MoveRelative(PrinterAxis axis, double moveAmountMm, double feedRateMmPerMinute)
		{
			if (moveAmountMm != 0)
			{
				SetMovementToRelative();
				QueueLine(string.Format("G1 {0}{1:0.###} F{2}", axis, moveAmountMm, feedRateMmPerMinute));
				SetMovementToAbsolute();
			}
		}

		public void OnConnectionFailed(ConnectionFailure reason, string message = null, string exceptionType = null)
		{
			communicationPossible = false;

			ConnectionFailed?.Invoke(this, new ConnectFailedEventArgs(reason)
			{
				Message = message,
				ExceptionType = exceptionType
			});

			CommunicationState = CommunicationStates.Disconnected;
		}

		private void OnIdle()
		{
			if (this.IsConnected && ReadThread.NumRunning == 0)
			{
				ReadThread.Start(this);
			}
		}

		public void PrinterRequestsResend(string line)
		{
			if (!string.IsNullOrEmpty(line))
			{
				// marlin and repetier send a : before the number and then and ok
				if (!GCodeFile.GetFirstNumberAfter(":", line, ref currentLineIndexToSend))
				{
					if (currentLineIndexToSend == allCheckSumLinesSent.Count)
					{
						// asking for the next line don't do anything, continue with sending next instruction
						return;
					}

					// smoothie sends an N before the number and no ok
					if (GCodeFile.GetFirstNumberAfter("N", line, ref currentLineIndexToSend))
					{
						// clear waiting for ok because smoothie will not send it
						PrintingCanContinue(line);
					}
				}

				if (currentLineIndexToSend == allCheckSumLinesSent.Count)
				{
					// asking for the next line don't do anything, continue with sending next instruction
					return;
				}

				if (currentLineIndexToSend >= allCheckSumLinesSent.Count
					|| currentLineIndexToSend == 1)
				{
					QueueLine("M110 N1");
					allCheckSumLinesSent.SetStartingIndex(1);
					waitingForPosition.Reset();
					PositionReadType = PositionReadType.None;
				}
			}
		}

		private bool haveReportedError = false;

		public void PrinterReportsError(string line)
		{
			if (!haveReportedError)
			{
				haveReportedError = true;

				if (line != null)
				{
					this.LogError(line, ErrorSource.Firmware);
				}

				// pause the printer
				RequestPause();
			}
		}

		public void PrinterStatesFirmware(string line)
		{
			string firmwareName = "";
			if (GCodeFile.GetFirstStringAfter("FIRMWARE_NAME:", line, " ", ref firmwareName))
			{
				firmwareName = firmwareName.ToLower();
				if (firmwareName.Contains("repetier"))
				{
					this.FirmwareType = FirmwareTypes.Repetier;
				}
				else if (firmwareName.Contains("marlin"))
				{
					this.FirmwareType = FirmwareTypes.Marlin;
				}
				else if (firmwareName.Contains("sprinter"))
				{
					this.FirmwareType = FirmwareTypes.Sprinter;
				}
			}

			string firmwareVersionReported = "";
			if (GCodeFile.GetFirstStringAfter("MACHINE_TYPE:", line, " EXTRUDER_COUNT", ref firmwareVersionReported))
			{
				char splitChar = '^';
				if (firmwareVersionReported.Contains(splitChar))
				{
					string[] split = firmwareVersionReported.Split(splitChar);
					if (split.Length == 2)
					{
						DeviceCode = split[0];
						firmwareVersionReported = split[1];
					}
				}

				// Firmware version was detected and is different
				if (firmwareVersionReported != "" && FirmwareVersion != firmwareVersionReported)
				{
					FirmwareVersion = firmwareVersionReported;
					OnFirmwareVersionRead(null);
				}
			}
		}

		// this is to make it misbehave
		// int okCount = 1;
		public void PrintingCanContinue(string line)
		{
			// if ((okCount++ % 67) != 0)
			{
				timeHaveBeenWaitingForOK.Stop();
			}
		}

		public void ArduinoDtrReset()
		{
			// TODO: Ideally we would shutdown the printer connection when this method is called and we're connected. The
			// current approach results in unpredictable behavior if the caller fails to close the connection
			if (serialPort == null && this.Printer.Settings != null)
			{
				IFrostedSerialPort resetSerialPort = FrostedSerialPortFactory.GetAppropriateFactory(this.DriverType).Create(this.ComPort, Printer.Settings);
				resetSerialPort.Open();

				Thread.Sleep(500);

				ToggleHighLowHigh(resetSerialPort);

				resetSerialPort.Close();
			}
		}

		private string dataLastRead = string.Empty;

		public void ReadFromPrinter(ReadThread readThreadHolder)
		{
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
			timeSinceLastReadAnything.Restart();
			// we want this while loop to be as fast as possible. Don't allow any significant work to happen in here
			while (CommunicationState == CommunicationStates.AttemptingToConnect
				|| (this.IsConnected && serialPort != null && serialPort.IsOpen && !Disconnecting && readThreadHolder.IsCurrentThread()))
			{
				if ((this.IsConnected
					|| this.CommunicationState == CommunicationStates.AttemptingToConnect)
					&& CommunicationState != CommunicationStates.PrintingFromSd)
				{
					TryWriteNextLineFromGCodeFile();
				}

				try
				{
					while (serialPort != null
						&& serialPort.BytesToRead > 0
						&& readThreadHolder.IsCurrentThread())
					{
						lock (locker)
						{
							string allDataRead = serialPort.ReadExisting();
							allDataRead = allDataRead.Replace("\r\n", "\n");
							allDataRead = allDataRead.Replace('\r', '\n');
							dataLastRead += allDataRead;

							do
							{
								int returnPosition = dataLastRead.IndexOf('\n');
								if (returnPosition < 0)
								{
									// there is no return keep getting characters
									break;
								}

								if (dataLastRead.Length > 0)
								{
									if (lastLineRead.StartsWith("ok"))
									{
										timeSinceRecievedOk.Restart();
									}

									lastLineRead = dataLastRead.Substring(0, returnPosition);
									var (firstLine, extraLines) = ProcessReadRegEx(lastLineRead);
									lastLineRead = firstLine;
									dataLastRead += extraLines;
									dataLastRead = dataLastRead.Substring(returnPosition + 1);

									// process this command
									{
										readLineStartCallBacks.ProcessLine(lastLineRead);
										readLineContainsCallBacks.ProcessLine(lastLineRead);

										LineReceived?.Invoke(this, lastLineRead);
									}
								}
							}
							while (true);
						}

						timeSinceLastReadAnything.Restart();
					}

					if (Printing)
					{
						Thread.Sleep(0);
					}
					else
					{
						Thread.Sleep(1);
					}
				}
				catch (TimeoutException)
				{
				}
				catch (IOException ex)
				{
					OnConnectionFailed(ConnectionFailure.IOException, ex.Message);
				}
				catch (InvalidOperationException ex)
				{
					// this happens when the serial port closes after we check and before we read it.
					OnConnectionFailed(ConnectionFailure.InvalidOperationException, ex.Message);
				}
				catch (UnauthorizedAccessException ex)
				{
					OnConnectionFailed(ConnectionFailure.UnauthorizedAccessException, ex.Message);
				}
				catch (Exception)
				{
				}
			}

			Console.WriteLine("Exiting ReadFromPrinter method: " + CommunicationState.ToString());
		}

		public void ReadPosition(PositionReadType positionReadType = PositionReadType.Other, bool forceToTopOfQueue = false)
		{
			var nextIssue = queuedCommandStream.Peek();
			if (nextIssue == null
				|| nextIssue != "M114")
			{
				QueueLine("M114", forceToTopOfQueue);
				PositionReadType = positionReadType;
			}
		}

		public void ReadSdProgress(string line)
		{
			if (line != null)
			{
				string sdProgressString = line.Substring("Sd printing byte ".Length);

				string[] values = sdProgressString.Split('/');
				currentSdBytes = long.Parse(values[0]);
				totalSdBytes = long.Parse(values[1]);
			}

			// We read it so we are no longer waiting
			timeWaitingForSdProgress.Stop();
		}

		public void ReadTargetPositions(string line)
		{
			GCodeFile.GetFirstNumberAfter("X:", line, ref lastReportedPosition.position.X);
			GCodeFile.GetFirstNumberAfter("Y:", line, ref lastReportedPosition.position.Y);
			GCodeFile.GetFirstNumberAfter("Z:", line, ref lastReportedPosition.position.Z);
			GCodeFile.GetFirstNumberAfter("E:", line, ref lastReportedPosition.extrusion);

			currentDestination = lastReportedPosition;
			DestinationChanged?.Invoke(this, null);
			if (totalGCodeStream != null)
			{
				totalGCodeStream.SetPrinterPosition(currentDestination);
			}

			if (PositionReadType.HasFlag(PositionReadType.HomeX))
			{
				HomingPosition = new Vector3(lastReportedPosition.position.X, HomingPosition.Y, HomingPosition.Z);
			}

			if (PositionReadType.HasFlag(PositionReadType.HomeY))
			{
				HomingPosition = new Vector3(HomingPosition.X, lastReportedPosition.position.Y, HomingPosition.Z);
			}

			if (PositionReadType.HasFlag(PositionReadType.HomeZ))
			{
				HomingPosition = new Vector3(HomingPosition.X, HomingPosition.Y, lastReportedPosition.position.Z);
			}

			waitingForPosition.Reset();
			PositionReadType = PositionReadType.None;
		}

		public static void ParseTemperatureString(string temperatureString,
			double[] actualHotendTemperature,
			Action<TemperatureEventArgs> hotendTemperatureChange,
			ref double actualBedTemperature,
			Action<TemperatureEventArgs> bedTemperatureChanged)
		{
			{
				double readHotendTemp = 0;
				if (GCodeFile.GetFirstNumberAfter("T:", temperatureString, ref readHotendTemp))
				{
					if (actualHotendTemperature[0] != readHotendTemp)
					{
						actualHotendTemperature[0] = readHotendTemp;
						hotendTemperatureChange?.Invoke(new TemperatureEventArgs(0, readHotendTemp));
					}
				}

				for (int hotendIndex = 0; hotendIndex < MaxExtruders; hotendIndex++)
				{
					if (GCodeFile.GetFirstNumberAfter($"T{hotendIndex}:", temperatureString, ref readHotendTemp))
					{
						if (actualHotendTemperature[hotendIndex] != readHotendTemp)
						{
							actualHotendTemperature[hotendIndex] = readHotendTemp;
							hotendTemperatureChange?.Invoke(new TemperatureEventArgs(hotendIndex, readHotendTemp));
						}
					}
					else
					{
						continue;
					}
				}
			}

			{
				double readBedTemp = 0;
				if (GCodeFile.GetFirstNumberAfter("B:", temperatureString, ref readBedTemp))
				{
					if (actualBedTemperature != readBedTemp)
					{
						actualBedTemperature = readBedTemp;
						bedTemperatureChanged?.Invoke(new TemperatureEventArgs(0, readBedTemp));
					}
				}
			}
		}

		public void ReadTemperatures(string line)
		{
			ParseTemperatureString(
				line,
				actualHotendTemperature,
				OnHotendTemperatureRead,
				ref actualBedTemperature,
				OnBedTemperatureRead);
		}

		public void RebootBoard()
		{
			try
			{
				if (Printer.Settings.PrinterSelected)
				{
					// first make sure we are not printing if possible (cancel slicing)
					if (serialPort != null) // we still have a serial port
					{
						Stop(false);
						ClearQueuedGCode();

						CommunicationState = CommunicationStates.Disconnecting;
						currentReadThreadIndex++;
						ToggleHighLowHigh(serialPort);
						if (serialPort != null)
						{
							serialPort.Close();
							serialPort.Dispose();
						}

						serialPort = null;
						// make sure we clear out the stream processors
						CreateStreamProcessors();
						CommunicationState = CommunicationStates.Disconnected;

						// We were connected to a printer so try to reconnect
						Connect();
					}
					else
					{
						// We reset the board while attempting to connect, so now we don't have a serial port.
						// Create one and do the DTR to reset
						var factory = FrostedSerialPortFactory.GetAppropriateFactory(this.DriverType);
						var resetSerialPort = factory.Create(this.ComPort, Printer.Settings);
						resetSerialPort.Open();

						Thread.Sleep(500);

						ToggleHighLowHigh(resetSerialPort);

						resetSerialPort.Close();

						// let the process know we canceled not ended normally.
						CommunicationState = CommunicationStates.Disconnected;
					}
				}
			}
			catch (Exception)
			{
			}
		}

		private void ToggleHighLowHigh(IFrostedSerialPort serialPort)
		{
			serialPort.RtsEnable = true;
			serialPort.DtrEnable = true;
			Thread.Sleep(100);
			serialPort.RtsEnable = false;
			serialPort.DtrEnable = false;
			Thread.Sleep(100);
			serialPort.RtsEnable = true;
			serialPort.DtrEnable = true;
		}

		public void ReleaseMotors(bool forceRelease = false)
		{
			if (forceRelease
				|| this.AutoReleaseMotors)
			{
				QueueLine("M84");
			}
		}

		public void RequestPause()
		{
			if (Printing)
			{
				if (CommunicationState == CommunicationStates.PrintingFromSd)
				{
					CommunicationState = CommunicationStates.Paused;
					QueueLine("M25"); // : Pause SD print
					return;
				}

				pauseHandlingStream.DoPause(PauseHandlingStream.PauseReason.UserRequested);
			}
		}

		public void ResetToReadyState()
		{
			if (CommunicationState == CommunicationStates.FinishedPrint)
			{
				CommunicationState = CommunicationStates.Connected;
			}
			else
			{
				throw new Exception("You should only reset after a print has finished.");
			}
		}

		public void Resume()
		{
			if (Paused)
			{
				if (PrePauseCommunicationState == CommunicationStates.PrintingFromSd)
				{
					CommunicationState = CommunicationStates.PrintingFromSd;

					QueueLine("M24"); // Start/resume SD print
				}
				else
				{
					pauseHandlingStream.Resume();
					CommunicationState = CommunicationStates.Printing;
				}
			}
		}

		public void QueueLine(string lineToWrite, bool forceTopOfQueue = false)
		{
			lock (locker)
			{
				if (lineToWrite.Contains("\\n"))
				{
					lineToWrite = lineToWrite.Replace("\\n", "\n");
				}

				// Check line for line breaks, split and process separate if necessary
				if (lineToWrite.Contains("\n"))
				{
					string[] linesToWrite = lineToWrite.Split(new string[] { "\n" }, StringSplitOptions.None);
					for (int i = 0; i < linesToWrite.Length; i++)
					{
						string line = linesToWrite[i].Trim();
						if (line.Length > 0)
						{
							QueueLine(line);
						}
					}

					return;
				}

				if (CommunicationState == CommunicationStates.PrintingFromSd
					|| forceImmediateWrites)
				{
					lineToWrite = lineToWrite.Split(';')[0].Trim();
					if (lineToWrite.Length > 0)
					{
						// sometimes we need to send code without buffering (like when we are closing the program).
						WriteRaw(lineToWrite + "\n", lineToWrite);
					}
				}
				else
				{
					if (lineToWrite.Trim().Length > 0)
					{
						// insert the command into the printing queue at the head
						queuedCommandStream?.Add(lineToWrite, forceTopOfQueue);
					}
				}
			}
		}

		private (string firstLine, string extraLines) ProcessReadRegEx(string lineBeingRead)
		{
			var addedLines = new List<string>();
			foreach (var item in readLineReplacements)
			{
				var splitReplacement = item.Replacement.Split(',');
				if (splitReplacement.Length > 0)
				{
					if (item.Regex.IsMatch(lineBeingRead))
					{
						// replace on the first replacement group only
						var replacedString = item.Regex.Replace(lineBeingRead, splitReplacement[0]);
						lineBeingRead = replacedString;
						// add in the other replacement groups
						for (int j = 1; j < splitReplacement.Length; j++)
						{
							addedLines.Add(splitReplacement[j]);
						}

						break;
					}
				}
			}

			string extraLines = "";
			foreach (var line in addedLines)
			{
				extraLines += line + "\n";
			}

			return (lineBeingRead, extraLines);
		}

		// Check is serial port is in the list of available serial ports
		public bool SerialPortIsAvailable(string portName)
		{
			if (IsNetworkPrinting())
			{
				return true;
			}

			try
			{
				string[] portNames = FrostedSerialPort.GetPortNames();
				return portNames.Any(x => string.Compare(x, portName, true) == 0);
			}
			catch (Exception)
			{
				return false;
			}
		}

		public void SetMovementToAbsolute()
		{
			QueueLine("G90");
		}

		public void SetMovementToRelative()
		{
			QueueLine("G91");
		}

		public void SetTargetHotendTemperature(int hotendIndex0Based, double temperature, bool forceSend = false)
		{
			hotendIndex0Based = Math.Min(hotendIndex0Based, MaxExtruders - 1);

			if (targetHotendTemperature[hotendIndex0Based] != temperature
				|| forceSend)
			{
				ContinueHoldingTemperature = false;
				targetHotendTemperature[hotendIndex0Based] = temperature;
				if (this.IsConnected)
				{
					QueueLine(string.Format("M104 T{0} S{1}", hotendIndex0Based, targetHotendTemperature[hotendIndex0Based]));
				}

				HotendTargetTemperatureChanged?.Invoke(this, hotendIndex0Based);
			}
		}

		public bool CalibrationPrint { get; private set; }

		private CancellationTokenSource printingCancellation;

		public void StartPrint(PrintJob printTask, bool calibrationPrint = false)
		{
			using (var gcodeStream = File.OpenRead(printTask.GCodeFile))
			{
				this.StartPrint(gcodeStream, printTask, calibrationPrint);
			}
		}

		// TODO: Review - restoring for test support where we run in process with emulator. Envisioned out-of-proc use won't hit this interface
		public void StartPrint(Stream gcodeStream, PrintJob printTask, bool calibrationPrint = false)
		{
			if (printTask.Id == 0)
			{
				repository.Add(printTask);
			}

			if (!this.IsConnected || Printing)
			{
				return;
			}

			this.CalibrationPrint = calibrationPrint;

			printingCancellation = new CancellationTokenSource();

			haveReportedError = false;
			PrintWasCanceled = false;

			waitingForPosition.Reset();
			PositionReadType = PositionReadType.None;

			ClearQueuedGCode();

			// LoadGCodeToPrint
			CreateStreamProcessors(gcodeStream);

			CommunicationState = CommunicationStates.Printing;

			if (ActivePrintTask == null
				&& !CalibrationPrint)
			{
				// TODO: Fix printerItemID int requirement
				ActivePrintTask = printTask;

				repository.Update(ActivePrintTask);

				Task.Run(() => this.SyncProgressToDB(printingCancellation.Token));
			}
		}

		public bool StartSdCardPrint(string m23FileName)
		{
			if (!this.IsConnected
				|| Printing
				|| string.IsNullOrEmpty(m23FileName))
			{
				return false;
			}

			currentSdBytes = 0;

			ClearQueuedGCode();
			CommunicationState = CommunicationStates.PrintingFromSd;

			QueueLine($"M23 {m23FileName.ToLower()}"); // Select SD File
			QueueLine("M24"); // Start/resume SD print

			readLineStartCallBacks.Register("Done printing file", DonePrintingSdFile);

			return true;
		}

		public void Stop(bool markPrintCanceled = true)
		{
			switch (CommunicationState)
			{
				case CommunicationStates.PrintingFromSd:
					CancelSDCardPrint();
					break;

				case CommunicationStates.Printing:
					CancelPrint(markPrintCanceled);
					break;

				case CommunicationStates.Paused:
					if (PrePauseCommunicationState == CommunicationStates.PrintingFromSd)
					{
						CancelSDCardPrint();
						CommunicationState = CommunicationStates.Connected;
					}
					else
					{
						CancelPrint(markPrintCanceled);
						// We have to continue printing the end gcode, so we set this to Printing.
						CommunicationState = CommunicationStates.Printing;
					}

					break;

				case CommunicationStates.AttemptingToConnect:
					CommunicationState = CommunicationStates.FailedToConnect;
					// connectThread.Join(JoinThreadTimeoutMs);

					CommunicationState = CommunicationStates.Disconnecting;
					currentReadThreadIndex++;
					if (serialPort != null)
					{
						serialPort.Close();
						serialPort.Dispose();
						serialPort = null;
					}

					CommunicationState = CommunicationStates.Disconnected;
					break;

				case CommunicationStates.PreparingToPrint:
					CommunicationState = CommunicationStates.Connected;
					break;
			}
		}

		private void CancelPrint(bool markPrintCanceled)
		{
			lock (locker)
			{
				// Flag as canceled, wait briefly for listening threads to catch up
				printingCancellation.Cancel();
				Thread.Sleep(15);

				// get rid of all the gcode we have left to print
				ClearQueuedGCode();

				if (!string.IsNullOrEmpty(this.CancelGCode))
				{
					// add any gcode we want to print while canceling
					QueueLine(this.CancelGCode);
				}

				// let the process know we canceled not ended normally.
				this.PrintWasCanceled = true;

				// TODO: Reimplement tracking
				/*
				if (markPrintCanceled
					&& ActivePrintTask != null)
				{
					TimeSpan printTimeSpan = DateTime.Now.Subtract(ActivePrintTask.PrintStart);

					ActivePrintTask.PrintEnd = DateTime.Now;
					ActivePrintTask.PrintComplete = false;
					ActivePrintTask.PrintingGCodeFileName = "";
					ActivePrintTask.Commit();
				}

				// no matter what we no longer have a print task
				ActivePrintTask = null;
				*/
			}
		}

		private void CancelSDCardPrint()
		{
			lock (locker)
			{
				// get rid of all the gcode we have left to print
				ClearQueuedGCode();
				// let the process know we canceled not ended normally.
				CommunicationState = CommunicationStates.Connected;
				QueueLine("M25"); // : Pause SD print
				QueueLine("M26"); // : Set SD position
								  // never leave the extruder and the bed hot
				DonePrintingSdFile(null);
			}
		}

		public void SuppressEcho(string line)
		{
			// AllowListenerNotification = false;
		}

		private void ClearQueuedGCode()
		{
			gCodeFileSwitcher?.GCodeFile?.Clear();
		}

		private void DonePrintingSdFile(string line)
		{
			readLineStartCallBacks.Unregister("Done printing file", DonePrintingSdFile);
			CommunicationState = CommunicationStates.FinishedPrint;

			this.PrintJobName = null;

			// never leave the extruder and the bed hot
			TurnOffBedAndExtruders(TurnOff.Now);

			ReleaseMotors();
		}

		private void FileDeleteConfirmed(string line)
		{
			readLineStartCallBacks.Unregister("File deleted:", FileDeleteConfirmed);
			PrintingCanContinue(line);
		}

		private void KeepTrackOfAbsolutePositionAndDestination(string lineBeingSent)
		{
			if (lineBeingSent.StartsWith("G0 ")
				|| lineBeingSent.StartsWith("G1 ")
				|| lineBeingSent.StartsWith("G2 ")
				|| lineBeingSent.StartsWith("G3 "))
			{
				PrinterMove newDestination = currentDestination;

				GCodeFile.GetFirstNumberAfter("X", lineBeingSent, ref newDestination.position.X);
				GCodeFile.GetFirstNumberAfter("Y", lineBeingSent, ref newDestination.position.Y);
				GCodeFile.GetFirstNumberAfter("Z", lineBeingSent, ref newDestination.position.Z);

				GCodeFile.GetFirstNumberAfter("E", lineBeingSent, ref newDestination.extrusion);
				GCodeFile.GetFirstNumberAfter("F", lineBeingSent, ref newDestination.feedRate);

				if (currentDestination.position != newDestination.position
					|| currentDestination.extrusion != newDestination.extrusion)
				{
					currentDestination = newDestination;
					DestinationChanged?.Invoke(this, null);
				}
			}
		}

		private void CreateStreamProcessors(Stream gcodeStream = null)
		{
			secondsSinceUpdateHistory = 0;
			lineSinceUpdateHistory = 0;

			totalGCodeStream?.Dispose();
			totalGCodeStream = null;
			GCodeStream accumulatedStream;
			if (gcodeStream != null)
			{
				gCodeFileSwitcher = new GCodeSwitcher(gcodeStream, Printer);

				if (this.RecoveryIsEnabled
					&& ActivePrintTask != null) // We are resuming a failed print (do lots of interesting stuff).
				{
					accumulatedStream = new SendProgressStream(new PrintRecoveryStream(gCodeFileSwitcher, Printer, ActivePrintTask.PercentDone), Printer);
					// And increment the recovery count
					ActivePrintTask.RecoveryCount++;

					repository.Update(ActivePrintTask);
				}
				else
				{
					accumulatedStream = new SendProgressStream(gCodeFileSwitcher, Printer);
				}

				accumulatedStream = pauseHandlingStream = new PauseHandlingStream(Printer, accumulatedStream);
			}
			else
			{
				gCodeFileSwitcher = null;
				accumulatedStream = new NotPrintingStream(Printer);
			}

			accumulatedStream = queuedCommandStream = new QueuedCommandsStream(Printer, accumulatedStream);

			// ensure that our read-line replacements are updated at the same time we build our write line replacements
			InitializeReadLineReplacements();

			accumulatedStream = new RelativeToAbsoluteStream(Printer, accumulatedStream);

			if (ExtruderCount > 1)
			{
				accumulatedStream = toolChangeStream = new ToolChangeStream(Printer, accumulatedStream, queuedCommandStream, gCodeFileSwitcher);
				accumulatedStream = new ToolSpeedMultiplierStream(Printer, accumulatedStream);
			}

			accumulatedStream = new BabyStepsStream(Printer, accumulatedStream);

			bool enableLineSplitting = gcodeStream != null && Printer.Settings.GetValue<bool>(SettingsKey.enable_line_splitting);
			accumulatedStream = maxLengthStream = new MaxLengthStream(Printer, accumulatedStream, enableLineSplitting ? 1 : 2000);

			if (!LevelingValidation.NeedsToBeRun(Printer))
			{
				accumulatedStream = printLevelingStream = new PrintLevelingStream(Printer, accumulatedStream);
			}

			accumulatedStream = waitForTempStream = new WaitForTempStream(Printer, accumulatedStream);
			accumulatedStream = new ExtrusionMultiplierStream(Printer, accumulatedStream);
			accumulatedStream = new FeedRateMultiplierStream(Printer, accumulatedStream);
			accumulatedStream = new RequestTemperaturesStream(Printer, accumulatedStream);

			if (Printer.Settings.GetValue<bool>(SettingsKey.emulate_endstops))
			{
				var softwareEndstopsExStream12 = new SoftwareEndstopsStream(Printer, accumulatedStream);
				accumulatedStream = softwareEndstopsExStream12;
			}

			accumulatedStream = new RemoveNOPsStream(Printer, accumulatedStream);

			processWriteRegexStream = new ProcessWriteRegexStream(Printer, accumulatedStream, queuedCommandStream);
			accumulatedStream = processWriteRegexStream;

			totalGCodeStream = accumulatedStream;

			// Force a reset of the printer checksum state (but allow it to be write regexed)
			var transformedCommand = ProcessWriteRegexStream.ProcessWriteRegEx("M110 N1", this.Printer);
			if (transformedCommand != null)
			{
				foreach (var line in transformedCommand)
				{
					WriteChecksumLine(line);
				}
			}

			// Get the current position of the printer any time we reset our streams
			ReadPosition(PositionReadType.Other);
		}

		public GCodeStream TotalGCodeStream => totalGCodeStream;

		private void SyncProgressToDB(CancellationToken cancellationToken)
		{
			// var timer = Stopwatch.StartNew();

			while (!cancellationToken.IsCancellationRequested
				&& this.CommunicationState != CommunicationStates.FinishedPrint
				&& this.CommunicationState != CommunicationStates.Connected)
			{
				double secondsSinceStartedPrint = timePrinting.Elapsed.TotalSeconds;

				if (timePrinting.Elapsed.TotalSeconds > 0
					&& gCodeFileSwitcher != null
					&& (secondsSinceUpdateHistory > secondsSinceStartedPrint
					|| secondsSinceUpdateHistory + 1 < secondsSinceStartedPrint
					|| lineSinceUpdateHistory + 20 < gCodeFileSwitcher.LineIndex))
				{
					double currentDone = gCodeFileSwitcher.GCodeFile.PercentComplete(gCodeFileSwitcher.LineIndex);

					// Only update the amount done if it is greater than what is recorded.
					// We don't want to mess up the resume before we actually resume it.
					if (ActivePrintTask != null
						&& ActivePrintTask.PercentDone < currentDone)
					{
						ActivePrintTask.PercentDone = currentDone;
						repository.Update(ActivePrintTask);
					}

					secondsSinceUpdateHistory = secondsSinceStartedPrint;
					lineSinceUpdateHistory = gCodeFileSwitcher.LineIndex;
				}

				Thread.Sleep(5);
			}

			// Console.WriteLine("Syncing print to db stopped");
		}

		private void AtxPowerUpWasWritenToPrinter(string line)
		{
			OnAtxPowerStateChanged(true);
		}

		private void AtxPowerDownWasWritenToPrinter(string line)
		{
			OnAtxPowerStateChanged(false);
		}

		private void OnBedTemperatureRead(EventArgs e)
		{
			BedTemperatureRead?.Invoke(this, e);
		}

		private void OnHotendTemperatureRead(EventArgs e)
		{
			HotendTemperatureRead?.Invoke(this, e);
		}

		private void OnFanSpeedSet(EventArgs e)
		{
			FanSpeedSet?.Invoke(this, e);
		}

		private void OnFirmwareVersionRead(EventArgs e)
		{
			FirmwareVersionRead?.Invoke(this, e);
		}

		private bool IsNetworkPrinting()
		{
			return this.EnableNetworkPrinting;
		}

		private void OnAtxPowerStateChanged(bool enableAtxPower)
		{
			atxPowerIsOn = enableAtxPower;
			AtxPowerStateChanged?.Invoke(this, null);
		}

		private void SetDetailedPrintingState(string lineBeingSetToPrinter)
		{
			if (lineBeingSetToPrinter.StartsWith("G28"))
			{
				// don't time the homing operation
				timePrinting.Stop();
				DetailedPrintingState = DetailedPrintingState.HomingAxis;
			}
			else if (waitForTempStream?.HeatingBed ?? false)
			{
				// don't time the heating bed operation
				timePrinting.Stop();
				DetailedPrintingState = DetailedPrintingState.HeatingBed;
			}
			else if (waitForTempStream?.HeatingT0 ?? false)
			{
				// don't time the heating extruder operation
				timePrinting.Stop();
				DetailedPrintingState = DetailedPrintingState.HeatingT0;
			}
			else if (waitForTempStream?.HeatingT1 ?? false)
			{
				// don't time the heating extruder operation
				timePrinting.Stop();
				DetailedPrintingState = DetailedPrintingState.HeatingT1;
			}
			else
			{
				// make sure we time all of the printing that we are doing
				if (this.Printing && !this.Paused)
				{
					timePrinting.Start();
				}

				DetailedPrintingState = DetailedPrintingState.Printing;
			}
		}

		private string currentSentLine;

		private int ExpectedWaitSeconds(string lastInstruction)
		{
			var timeMultiple = noOkResendCount + 1;
			if (lastInstruction.StartsWith("G0 ")
				|| lastInstruction.Contains("G1 "))
			{
				// for moves we wait only as much as 2 seconds
				return 2 * timeMultiple;
			}
			else if (lastInstruction.StartsWith("M109 ")
				|| lastInstruction.StartsWith("M190 "))
			{
				// heat and wait will allow a long wait time for ok
				return 60;
			}
			else if (lastInstruction.StartsWith("G28"))
			{
				return 30;
			}

			// any other move we allow up to 10 seconds for response
			return 10 * timeMultiple;
		}

		private void TryWriteNextLineFromGCodeFile()
		{
			if (totalGCodeStream == null)
			{
				// don't try to write until we are initialized
				return;
			}

			// wait until the printer responds from the last command with an OK OR we waited too long
			if (timeHaveBeenWaitingForOK.IsRunning)
			{
				lock (locker)
				{
					// we are still sending commands
					if (currentSentLine != null)
					{
						// This code is to try and make sure the printer does not stop on transmission errors.
						// If it has been more than 10 seconds since the printer responded anything
						// and it was not ok, and it's been more than 30 second since we sent the command.
						if ((timeSinceLastReadAnything.Elapsed.TotalSeconds > 10 && timeSinceLastWrite.Elapsed.TotalSeconds > 30)
							|| timeHaveBeenWaitingForOK.Elapsed.TotalSeconds > ExpectedWaitSeconds(currentSentLine))
						{
							// Basically we got some response but it did not contain an OK.
							// The theory is that we may have received a transmission error (like 'OP' rather than 'OK')
							// and in that event we don't want the print to just stop and wait forever.
							currentLineIndexToSend = Math.Max(0, currentLineIndexToSend--); // we are going to resend the last command
							noOkResendCount++;
						}
						else
						{
							// we are waiting for the ok so let's wait
							return;
						}
					}
				}
			}
			else
			{
				noOkResendCount = 0;
			}

			lock (locker)
			{
				if (currentLineIndexToSend < allCheckSumLinesSent.Count)
				{
					WriteRaw(allCheckSumLinesSent[currentLineIndexToSend++] + "\n", "resend");
				}
				else
				{
					int waitTimeInMs = 60000; // 60 seconds
					if (waitingForPosition.IsRunning
						&& waitingForPosition.ElapsedMilliseconds < waitTimeInMs
						&& this.IsConnected)
					{
						// we are waiting for a position response don't print more
						return;
					}

					currentSentLine = totalGCodeStream.ReadLine();

					if (currentSentLine != null)
					{
						if (currentSentLine.EndsWith("; NO_PROCESSING"))
						{
							// make sure our processing pipe knows the translated position after a NO_PROCESSING
							ReadPosition(PositionReadType.Other, true);
						}

						if (currentSentLine.Contains("M114")
							&& this.IsConnected)
						{
							waitingForPosition.Restart();
						}

						currentSentLine = currentSentLine.Trim();

						// Check if there is anything in front of the ;.
						if (currentSentLine.Split(';')[0].Trim().Length > 0)
						{
							if (currentSentLine.Length > 0)
							{
								WriteChecksumLine(currentSentLine);

								currentLineIndexToSend++;
							}
						}
					}
					else if (this.PrintWasCanceled)
					{
						CommunicationState = CommunicationStates.Connected;
						// never leave the extruder and the bed hot
						ReleaseMotors();
						TurnOffBedAndExtruders(TurnOff.AfterDelay);
						this.PrintWasCanceled = false;
						// and finally notify anyone that wants to know
						PrintCanceled?.Invoke(this, null);
					}
					else if (CommunicationState == CommunicationStates.Printing) // we finished printing normally
					{
						CommunicationState = CommunicationStates.FinishedPrint;

						this.PrintJobName = null;

						// get us back to the no printing setting (this will clear the queued commands)
						CreateStreamProcessors();

						// never leave the extruder and the bed hot
						ReleaseMotors();
						if (SecondsPrinted < GCodeMemoryFile.LeaveHeatersOnTime)
						{
							// The user may still be sitting at the machine, leave it heated for a period of time
							TurnOffBedAndExtruders(TurnOff.AfterDelay);
						}
						else
						{
							// Turn off the heaters on long prints as the user is less likely to be around and interacting
							TurnOffBedAndExtruders(TurnOff.Now);
						}
					}
				}
			}
		}

		public int TimeToHoldTemperature { get; set; } = 600;

		public bool AnyHeatIsOn
		{
			get
			{
				bool anyHeatIsOn = false;
				// check if any temps are set
				for (int i = 0; i < this.ExtruderCount; i++)
				{
					if (GetTargetHotendTemperature(i) > 0)
					{
						anyHeatIsOn = true;
						break;
					}
				}

				anyHeatIsOn |= TargetBedTemperature > 0;
				return anyHeatIsOn;
			}
		}

		public int NumQueuedCommands
		{
			get
			{
				if (queuedCommandStream != null)
				{
					return queuedCommandStream.Count;
				}

				return 0;
			}
		}

		public bool AllowLeveling
		{
			set
			{
				if (printLevelingStream != null)
				{
					printLevelingStream.AllowLeveling = value;
				}
				else if (value)
				{
					// we are requesting it turned back on, re-build the leveling stream
					CreateStreamProcessors();
				}
			}
		}

		/// <summary>
		/// Gets a value indicating whether the Pause Handling Stream has seen a change in the position sensor.
		/// It is important that this is not persisted, it is meant to function correctly if the user
		/// plugs in or removes a filament position sensor.
		/// </summary>
		public bool FilamentPositionSensorDetected { get; internal set; }

		public Vector3 HomingPosition
		{
			get => _homingPosition;

			private set
			{
				if (value != _homingPosition)
				{
					_homingPosition = value;
					HomingPositionChanged?.Invoke(this, null);
				}
			}
		}

		public PrintJob ActivePrintTask { get; set; }

		public string ActivePrintName => this.ActivePrintTask?.PrintName ?? "";

		public void TurnOffBedAndExtruders(TurnOff turnOffTime)
		{
			if (turnOffTime == TurnOff.Now)
			{
				for (int i = 0; i < this.ExtruderCount; i++)
				{
					SetTargetHotendTemperature(i, 0, true);
				}

				TargetBedTemperature = 0;
			}
			else
			{
				bool currentlyWaiting = ContinueHoldingTemperature && TimeHaveBeenHoldingTemperature.IsRunning && TimeHaveBeenHoldingTemperature.Elapsed.TotalSeconds < TimeToHoldTemperature;
				SecondsToHoldTemperature = TimeToHoldTemperature;
				ContinueHoldingTemperature = true;
				TimeHaveBeenHoldingTemperature = Stopwatch.StartNew();
				if (!currentlyWaiting)
				{
					TemporarilyHoldingTemp?.Invoke(this, null);
					// wait secondsToWait and turn off the heaters
					Task.Run(() =>
					{
						while (TimeHaveBeenHoldingTemperature.Elapsed.TotalSeconds < TimeToHoldTemperature
							&& ContinueHoldingTemperature)
						{
							if (CommunicationState == CommunicationStates.PreparingToPrint
								|| Printing)
							{
								ContinueHoldingTemperature = false;
							}

							if (!AnyHeatIsOn)
							{
								ContinueHoldingTemperature = false;
							}

							SecondsToHoldTemperature = ContinueHoldingTemperature ? Math.Max(0, TimeToHoldTemperature - TimeHaveBeenHoldingTemperature.Elapsed.TotalSeconds) : 0;
							Thread.Sleep(100);
						}

						// times up turn off heaters
						if (ContinueHoldingTemperature
							&& !Printing
							&& !Paused)
						{
							for (int i = 0; i < this.ExtruderCount; i++)
							{
								SetTargetHotendTemperature(i, 0, true);
							}

							TargetBedTemperature = 0;
						}
					});
				}
			}
		}

		// this is to make it misbehave, chaos monkey, bad checksum
		// int checkSumCount = 1;

		private void WriteChecksumLine(string lineToWrite)
		{
			bool sendLineWithChecksum = !lineToWrite.Contains("WRITE_RAW");

			// remove the comment if any
			lineToWrite = RemoveCommentIfAny(lineToWrite);

			KeepTrackOfAbsolutePositionAndDestination(lineToWrite);

			if (this.SendWithChecksum && sendLineWithChecksum)
			{
				// always send the reset line number without a checksum so that it is accepted
				string lineWithCount;
				if (lineToWrite.StartsWith("M110"))
				{
					lineWithCount = $"N1 {lineToWrite}";
					GCodeFile.GetFirstNumberAfter("N", lineToWrite, ref currentLineIndexToSend);
					allCheckSumLinesSent.SetStartingIndex(currentLineIndexToSend);
					currentLineIndexToSend++;
				}
				else
				{
					lineWithCount = $"N{allCheckSumLinesSent.Count} {lineToWrite}";
					if (lineToWrite.StartsWith("M999"))
					{
						allCheckSumLinesSent.SetStartingIndex(1);
					}
				}

				string lineWithChecksum = lineWithCount + "*" + GCodeFile.CalculateChecksum(lineWithCount).ToString();

				allCheckSumLinesSent.Add(lineWithChecksum);

				// if ((checkSumCount++ % 11) == 0)
				// lineWithChecksum = lineWithCount + "*" + (GCodeFile.CalculateChecksum(lineWithCount) + checkSumCount).ToString();

				WriteRaw(lineWithChecksum + "\n", lineToWrite);
			}
			else
			{
				WriteRaw(lineToWrite + "\n", lineToWrite);
			}

			SetDetailedPrintingState(lineToWrite);
		}

		private static string RemoveCommentIfAny(string lineToWrite)
		{
			int commentIndex = lineToWrite.IndexOf(';');
			if (commentIndex > 0) // there is content in front of the ;
			{
				lineToWrite = lineToWrite.Substring(0, commentIndex).Trim();
			}

			return lineToWrite;
		}

		private void WriteRaw(string lineToWrite, string lineWithoutChecksum)
		{
			if (this.IsConnected || CommunicationState == CommunicationStates.AttemptingToConnect)
			{
				if (serialPort != null && serialPort.IsOpen)
				{
					if (lineWithoutChecksum.StartsWith("G92"))
					{
						// read out the position and store right now
						GCodeFile.GetFirstNumberAfter("X", lineWithoutChecksum, ref currentDestination.position.X);
						GCodeFile.GetFirstNumberAfter("Y", lineWithoutChecksum, ref currentDestination.position.Y);
						GCodeFile.GetFirstNumberAfter("Z", lineWithoutChecksum, ref currentDestination.position.X);
						GCodeFile.GetFirstNumberAfter("E", lineWithoutChecksum, ref currentDestination.extrusion);

						// The printer position has changed, make sure all the streams know
						if (totalGCodeStream != null)
						{
							totalGCodeStream.SetPrinterPosition(currentDestination);
						}
					}

					// If we get a home command, ask the printer where it is after sending it.
					if (lineWithoutChecksum.StartsWith("G28") // is a home
						|| lineWithoutChecksum.StartsWith("G29") // is a bed level
						|| lineWithoutChecksum.StartsWith("G30") // is a bed level
						|| (lineWithoutChecksum.StartsWith("T") && !lineWithoutChecksum.StartsWith("T:"))) // is a switch extruder (verify this is the right time to ask this)
					{
						PositionReadType readType = PositionReadType.Other;
						if (lineWithoutChecksum.StartsWith("G28"))
						{
							if (lineWithoutChecksum.Contains("X"))
							{
								readType |= PositionReadType.HomeX;
							}

							if (lineWithoutChecksum.Contains("Y"))
							{
								readType |= PositionReadType.HomeY;
							}

							if (lineWithoutChecksum.Contains("Z"))
							{
								readType |= PositionReadType.HomeZ;
							}

							if (lineWithoutChecksum == "G28")
							{
								readType = PositionReadType.HomeAll;
							}
						}

						ReadPosition(readType, true);
					}

					// write data to communication
					{
						if (lineWithoutChecksum != null)
						{
							writeLineStartCallBacks.ProcessLine(lineWithoutChecksum);
							writeLineContainsCallBacks.ProcessLine(lineWithoutChecksum);

							LineSent?.Invoke(this, lineToWrite);
						}
					}

					try
					{
						// only send the line if there is something to send (other than the \n)
						if (lineToWrite.Trim().Length > 0)
						{
							lock (locker)
							{
								serialPort.Write(lineToWrite);
								timeSinceLastWrite.Restart();
								timeHaveBeenWaitingForOK.Restart();
							}
						}

						// Debug.Write("w: " + lineToWrite);
					}
					catch (IOException ex)
					{
						if (CommunicationState == CommunicationStates.AttemptingToConnect)
						{
							// Handle hardware disconnects by relaying the failure reason and shutting down open resources
							ReleaseAndReportFailedConnection(ConnectionFailure.ConnectionLost, ex.Message);
						}
					}
					catch (TimeoutException ex) // known ok
					{
						// This writes on the next line, and there may have been another write attempt before it is printer. Write indented to attempt to show its association.
						this.LogError("        Error writing to printer:" + ex.Message, ErrorSource.Connection);
					}
					catch (UnauthorizedAccessException ex)
					{
						ReleaseAndReportFailedConnection(ConnectionFailure.UnauthorizedAccessException, ex.Message);
					}
					catch (Exception)
					{
					}
				}
				else
				{
					OnConnectionFailed(ConnectionFailure.WriteFailed);
				}
			}
		}

		public void MacroStart()
		{
			queuedCommandStream?.Reset();
		}

		public void MacroCancel()
		{
			maxLengthStream?.Cancel();
			waitForTempStream?.Cancel();
			queuedCommandStream?.Cancel();
		}

		public void Dispose()
		{
			Disposed?.Invoke(this, null);
		}

		private int currentReadThreadIndex = 0;
		private Vector3 _homingPosition = Vector3.NegativeInfinity;
		private int noOkResendCount;
		private ProcessWriteRegexStream processWriteRegexStream;

		public class ReadThread
		{
			private readonly int creationIndex;

			private static int numRunning = 0;
			private readonly PrinterConnection printerConnection;

			public static int NumRunning => numRunning;

			private ReadThread(PrinterConnection printerConnection)
			{
				this.printerConnection = printerConnection;
				numRunning++;
				printerConnection.currentReadThreadIndex++;
				creationIndex = printerConnection.currentReadThreadIndex;

				Task.Run(() =>
				{
					try
					{
						printerConnection.ReadFromPrinter(this);
					}
					catch
					{
					}

					printerConnection.LogError("Read Thread Has Exited", ErrorSource.Connection);
					numRunning--;
				});
			}

			internal static void Start(PrinterConnection printerConnection)
			{
				new ReadThread(printerConnection);
			}

			internal bool IsCurrentThread()
			{
				return printerConnection.currentReadThreadIndex == creationIndex;
			}
		}

		private class CheckSumLines
		{
			private static readonly int RingBufferCount = 64;

			private int addedCount = 0;
			private readonly string[] ringBuffer = new string[RingBufferCount];

			public int Count => addedCount;

			public string this[int index]
			{
				get => ringBuffer[index % RingBufferCount];
				set
				{
					ringBuffer[index % RingBufferCount] = value;
				}
			}

			internal void Add(string lineWithChecksum)
			{
				this[addedCount++] = lineWithChecksum;
			}

			internal void SetStartingIndex(int startingIndex)
			{
				addedCount = startingIndex;
			}
		}
	}
}
