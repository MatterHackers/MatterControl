/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.SerialPortCommunication;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterCommunication
{
	public enum TurnOff { Now, AfterDelay }

	public enum CommunicationStates
	{
		Disconnected,
		AttemptingToConnect,
		FailedToConnect,
		Connected,
		PreparingToPrint,
		Printing,
		PrintingFromSd,
		Paused,
		FinishedPrint,
		Disconnecting,
		ConnectionLost
	};

	public enum DetailedPrintingState { HomingAxis, HeatingBed, HeatingExtruder, Printing };

	public enum FirmwareTypes { Unknown, Repetier, Marlin, Sprinter };

	/// <summary>
	/// This is the class that communicates with a RepRap printer over the serial port.
	/// It handles opening and closing the serial port and does quite a bit of gcode parsing.
	/// It should be refactored into better modules at some point.
	/// </summary>
	public class PrinterConnection : IDisposable
	{
		public event EventHandler Disposed;

		public event EventHandler TemporarilyHoldingTemp;
		public event EventHandler<string> ErrorReported;

		public event EventHandler BedTemperatureRead;

		public event EventHandler CommunicationStateChanged;

		public event EventHandler ConnectionFailed;

		public event EventHandler ConnectionSucceeded;

		public void OnPauseOnLayer(NamedItemEventArgs namedItemEventArgs)
		{
			PauseOnLayer?.Invoke(this, namedItemEventArgs);
		}

		public event EventHandler DestinationChanged;

		public event EventHandler EnableChanged;

		public event EventHandler HotendTemperatureRead;

		public event EventHandler FanSpeedSet;

		public event EventHandler FirmwareVersionRead;

		public void OnFilamentRunout(NamedItemEventArgs namedItemEventArgs)
		{
			FilamentRunout?.Invoke(this, namedItemEventArgs);
		}

		public event EventHandler PrintFinished;

		public event EventHandler PauseOnLayer;

		public event EventHandler FilamentRunout;

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
					PositionReadQueued = false;
				}

				return waitingForPosition.IsRunning || PositionReadQueued;
			}
		}

		public bool ContinuHoldingTemperature { get; set; }
		public double SecondsToHoldTemperature { get; private set; }

		public TerminalLog TerminalLog { get; }

		public EventHandler AtxPowerStateChanged;

		private bool atxPowerIsOn = false;

		internal const int MAX_EXTRUDERS = 16;

		private const int MAX_INVALID_CONNECTION_CHARS = 3;

		private object locker = new object();

		public PrintTask activePrintTask;

		private double actualBedTemperature;

		public int ActiveExtruderIndex { get; private set; }

		private double[] actualHotendTemperature = new double[MAX_EXTRUDERS];

		private CheckSumLines allCheckSumLinesSent = new CheckSumLines();

		private CommunicationStates communicationState = CommunicationStates.Disconnected;

		private PrinterMove currentDestination;

		public double CurrentFeedRate { get { return currentDestination.feedRate; } }

		private double currentSdBytes = 0;

		private double fanSpeed;

		private int currentLineIndexToSend = 0;

		private bool forceImmediateWrites = false;

		private string lastLineRead = "";

		public Stopwatch TimeHaveBeenHoldingTemperature { get; set; }

		private PrinterMove lastReportedPosition;

		private GCodeSwitcher gCodeFileSwitcher0 = null;
		private SendProgressStream sendProgressStream1 = null;
		private PauseHandlingStream pauseHandlingStream2 = null;
		private QueuedCommandsStream queuedCommandStream3 = null;
		private RelativeToAbsoluteStream relativeToAbsoluteStream4 = null;
		private BabyStepsStream babyStepsStream5 = null;
		private PrintLevelingStream printLevelingStream6 = null;
		private WaitForTempStream waitForTempStream7 = null;
		private ExtrusionMultiplyerStream extrusionMultiplyerStream8 = null;
		private FeedRateMultiplyerStream feedrateMultiplyerStream9 = null;
		private RequestTemperaturesStream requestTemperaturesStream10 = null;
		private ProcessWriteRegexStream processWriteRegExStream11 = null;

		private GCodeStream totalGCodeStream = null;

		private PrinterMachineInstruction.MovementTypes movementMode = PrinterMachineInstruction.MovementTypes.Absolute;

		public CommunicationStates PrePauseCommunicationState { get; private set; } = CommunicationStates.Printing;

		private DetailedPrintingState _printingStatePrivate;

		private ContainsStringLineActions ReadLineContainsCallBacks = new ContainsStringLineActions();

		private StartsWithLineActions ReadLineStartCallBacks = new StartsWithLineActions();

		// we start out by setting it to a nothing file
		private IFrostedSerialPort serialPort;

		private double _targetBedTemperature;

		private double[] targetHotendTemperature = new double[MAX_EXTRUDERS];

		private Stopwatch timeHaveBeenWaitingForOK = new Stopwatch();

		private Stopwatch timeSinceLastReadAnything = new Stopwatch();

		private Stopwatch timeSinceLastWrite = new Stopwatch();

		private Stopwatch timeSinceRecievedOk = new Stopwatch();

		private Stopwatch timeSinceStartedPrint = new Stopwatch();

		private Stopwatch timeWaitingForSdProgress = new Stopwatch();

		private double totalSdBytes = 0;

		private bool PositionReadQueued { get; set; } = false;
		private Stopwatch waitingForPosition = new Stopwatch();

		private ContainsStringLineActions WriteLineContainsCallBacks = new ContainsStringLineActions();

		private StartsWithLineActions WriteLineStartCallBacks = new StartsWithLineActions();

		private double secondsSinceUpdateHistory = 0;
		private long lineSinceUpdateHistory = 0;

		public PrinterConnection(PrinterConfig printer)
		{
			this.Printer = printer;

			TerminalLog = new TerminalLog(this);

			MonitorPrinterTemperature = true;

			ReadLineStartCallBacks.Register("start", FoundStart);
			ReadLineStartCallBacks.Register("start", PrintingCanContinue);

			ReadLineStartCallBacks.Register("ok", SuppressEcho);
			ReadLineStartCallBacks.Register("wait", SuppressEcho);
			ReadLineStartCallBacks.Register("T:", SuppressEcho); // repetier

			ReadLineStartCallBacks.Register("ok", PrintingCanContinue);
			ReadLineStartCallBacks.Register("Done saving file", PrintingCanContinue);

			ReadLineStartCallBacks.Register("B:", ReadTemperatures); // smoothie
			ReadLineContainsCallBacks.Register("T0:", ReadTemperatures); // marlin
			ReadLineContainsCallBacks.Register("T:", ReadTemperatures); // repetier

			ReadLineStartCallBacks.Register("SD printing byte", ReadSdProgress); // repetier

			ReadLineStartCallBacks.Register("C:", ReadTargetPositions);
			ReadLineStartCallBacks.Register("ok C:", ReadTargetPositions); // smoothie is reporting the C: with an ok first.
			ReadLineStartCallBacks.Register("X:", ReadTargetPositions);
			ReadLineStartCallBacks.Register("ok X:", ReadTargetPositions); //

			ReadLineStartCallBacks.Register("rs ", PrinterRequestsResend); // smoothie is lower case and no :
			ReadLineStartCallBacks.Register("RS:", PrinterRequestsResend);
			ReadLineContainsCallBacks.Register("Resend:", PrinterRequestsResend);

			ReadLineContainsCallBacks.Register("FIRMWARE_NAME:", PrinterStatesFirmware);

			#region hardware failure callbacks

			// smoothie temperature failures
			ReadLineContainsCallBacks.Register("T:inf", PrinterReportsError);
			ReadLineContainsCallBacks.Register("B:inf", PrinterReportsError);

			// marlin temperature failures
			ReadLineContainsCallBacks.Register("MINTEMP", PrinterReportsError);
			ReadLineContainsCallBacks.Register("MAXTEMP", PrinterReportsError);
			ReadLineContainsCallBacks.Register("M999", PrinterReportsError);
			ReadLineContainsCallBacks.Register("Error: Extruder switched off", PrinterReportsError);
			ReadLineContainsCallBacks.Register("Heater decoupled", PrinterReportsError);
			ReadLineContainsCallBacks.Register("cold extrusion prevented", PrinterReportsError);
			ReadLineContainsCallBacks.Register("Error:Thermal Runaway, system stopped!", PrinterReportsError);
			ReadLineContainsCallBacks.Register("Error:Heating failed", PrinterReportsError);
			ReadLineStartCallBacks.Register("temp sensor defect", PrinterReportsError);
			ReadLineStartCallBacks.Register("Error:Printer halted", PrinterReportsError);

			// repetier temperature failures
			ReadLineContainsCallBacks.Register("dry run mode", PrinterReportsError);
			ReadLineStartCallBacks.Register("accelerometer send i2c error", PrinterReportsError);
			ReadLineStartCallBacks.Register("accelerometer i2c recv error", PrinterReportsError);

			// s3g temperature failures
			ReadLineContainsCallBacks.Register("Bot is Shutdown due to Overheat", PrinterReportsError);

			#endregion hardware failure callbacks

			WriteLineStartCallBacks.Register("G90", MovementWasSetToAbsoluteMode);
			WriteLineStartCallBacks.Register("M80", AtxPowerUpWasWritenToPrinter);
			WriteLineStartCallBacks.Register("M81", AtxPowerDownWasWritenToPrinter);
			WriteLineStartCallBacks.Register("M104", HotendTemperatureWasWritenToPrinter);
			WriteLineStartCallBacks.Register("M106", FanSpeedWasWritenToPrinter);
			WriteLineStartCallBacks.Register("M107", FanOffWasWritenToPrinter);
			WriteLineStartCallBacks.Register("M109", HotendTemperatureWasWritenToPrinter);
			WriteLineStartCallBacks.Register("M140", BedTemperatureWasWritenToPrinter);
			WriteLineStartCallBacks.Register("M190", BedTemperatureWasWritenToPrinter);
			WriteLineStartCallBacks.Register("T", ExtruderIndexSet);

			Task.Run(() =>
			{
				this.OnIdle();
				Thread.Sleep(10);
			});
		}

		private void ExtruderIndexSet(string line)
		{
			double extruderBeingSet = 0;
			if (GCodeFile.GetFirstNumberAfter("T", line, ref extruderBeingSet))
			{
				ActiveExtruderIndex = (int)extruderBeingSet;
			}
		}

		[Flags]
		public enum Axis { X = 1, Y = 2, Z = 4, E = 8, XYZ = (X | Y | Z) }

		public double ActualBedTemperature
		{
			get
			{
				return actualBedTemperature;
			}
		}

		// PrinterSettings/Options {{

		public int BaudRate { get; set; } = 250000;

		public double FeedRateRatio { get; set; } = 1;

		public string ConnectGCode { get; set; } = "";

		public string CancelGCode { get; set; } = "";

		public int ExtruderCount { get; set; }

		public bool SendWithChecksum { get; set; }

		public bool EnableNetworkPrinting { get; set; }

		public bool AutoReleaseMotors { get; set; }

		public bool RecoveryIsEnabled { get; set; }
		public string LastPrintedItemName { get; private set; } = "";
		public string PrintingItemName { get; set; } = "";

		private List<(Regex Regex, string Replacement)> readLineReplacements = new List<(Regex Regex, string Replacement)>();

		private string _readLineReplacementString = "";
		public string ReadLineReplacementString
		{
			get => _readLineReplacementString;
			set
			{
				if (value != _readLineReplacementString)
				{
					_readLineReplacementString = value;

					// Clear and rebuild the replacement list
					readLineReplacements.Clear();

					foreach (string regExLine in _readLineReplacementString.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries))
					{
						var matches = getQuotedParts.Matches(regExLine);
						if (matches.Count == 2)
						{
							var search = matches[0].Value.Substring(1, matches[0].Value.Length - 2);
							var replace = matches[1].Value.Substring(1, matches[1].Value.Length - 2);
							readLineReplacements.Add((new Regex(search, RegexOptions.Compiled), replace));
						}
					}
				}
			}
		}

		// PrinterSettings/Options }}

		private bool communicationPossible = false;

		public CommunicationStates CommunicationState
		{
			get
			{
				return communicationState;
			}

			set
			{
				switch (value)
				{
					case CommunicationStates.AttemptingToConnect:
						PrintingItemName = "";
#if DEBUG
						if (serialPort == null)
						{
							throw new Exception("The serial port should be constructed prior to setting this or we can fail our connection on a write before it has a chance to be created.");
						}
#endif
						break;

					case CommunicationStates.Connected:
						communicationPossible = true;
						QueueLine("M115");
						ReadPosition();
						PrintingItemName = "";
						break;

					case CommunicationStates.ConnectionLost:
					case CommunicationStates.Disconnected:
						PrintingItemName = "";
						if (communicationPossible)
						{
							TurnOffBedAndExtruders(TurnOff.Now);
							for (int hotendIndex = 0; hotendIndex < MAX_EXTRUDERS; hotendIndex++)
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

				if (communicationState != value)
				{
					LineSent?.Invoke(this, string.Format("Communication State: {0}\n", value));

					switch (communicationState)
					{
						// if it was printing
						case CommunicationStates.PrintingFromSd:
						case CommunicationStates.Printing:
							{
								// and is changing to paused
								if (value == CommunicationStates.Paused)
								{
									if (communicationState == CommunicationStates.Printing)
									{
										PrePauseCommunicationState = CommunicationStates.Printing;
									}
									else
									{
										PrePauseCommunicationState = CommunicationStates.PrintingFromSd;
									}
									timeSinceStartedPrint.Stop();
								}
								else if (value == CommunicationStates.FinishedPrint)
								{
									if (activePrintTask != null)
									{
										activePrintTask.PrintEnd = DateTime.Now;
										activePrintTask.PercentDone = 100;
										activePrintTask.PrintComplete = true;
										activePrintTask.Commit();
									}

									LastPrintedItemName = PrintingItemName;
									PrintingItemName = "";

									// Set this early as we always want our functions to know the state we are in.
									communicationState = value;
									timeSinceStartedPrint.Stop();
									PrintFinished?.Invoke(this, new NamedItemEventArgs(Printer.Bed.EditContext.SourceItem.Name));
								}
								else
								{
									timeSinceStartedPrint.Stop();
									timeSinceStartedPrint.Reset();
								}
							}
							break;

						// was paused
						case CommunicationStates.Paused:
							{
								// changing to printing
								if (value == CommunicationStates.Printing)
								{
									timeSinceStartedPrint.Start();
								}
							}
							break;

						default:
							if (!timeSinceStartedPrint.IsRunning
								&& value == CommunicationStates.Printing)
							{
								// If we are just starting to print (we know we were not paused or it would have stopped above)
								timeSinceStartedPrint.Restart();
							}
							break;
					}

					communicationState = value;
					OnCommunicationStateChanged(null);
				}
			}
		}

		public void SwitchToGCode(string gCodeFilePath)
		{
			gCodeFileSwitcher0.SwitchTo(gCodeFilePath);
		}

		public string ComPort => Printer.Settings?.Helpers.ComPort();

		public string DriverType => (this.ComPort == "Emulator") ? "Emulator" : Printer.Settings?.GetValue("driver_type");

		public bool AtxPowerEnabled
		{
			get
			{
				return atxPowerIsOn;
			}
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

		public double CurrentExtruderDestination { get { return currentDestination.extrusion; } }

		public Vector3 CurrentDestination => currentDestination.position;

		public int CurrentlyPrintingLayer
		{
			get
			{
				if (gCodeFileSwitcher0 != null)
				{
					return gCodeFileSwitcher0?.GCodeFile?.GetLayerIndex(gCodeFileSwitcher0.LineIndex) ?? -1;
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
					QueueLine("M106 S{0}".FormatWith((int)(fanSpeed + .5)));
				}
			}
		}

		public FirmwareTypes FirmwareType { get; private set; } = FirmwareTypes.Unknown;

		public string FirmwareVersion { get; private set; }

		public Vector3 LastReportedPosition { get { return lastReportedPosition.position; } }

		public bool MonitorPrinterTemperature { get; set; }

		public double PercentComplete
		{
			get
			{
				if (CommunicationState == CommunicationStates.PrintingFromSd
					|| (communicationState == CommunicationStates.Paused && PrePauseCommunicationState == CommunicationStates.PrintingFromSd))
				{
					if (totalSdBytes > 0)
					{
						return currentSdBytes / totalSdBytes * 100;
					}

					return 0;
				}

				if (PrintIsFinished && !PrinterIsPaused)
				{
					return 100.0;
				}
				else if (NumberOfLinesInCurrentPrint > 0
					&& gCodeFileSwitcher0?.GCodeFile != null)
				{
					return gCodeFileSwitcher0.GCodeFile.PercentComplete(gCodeFileSwitcher0.LineIndex);
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

		public bool PrinterIsPaused => CommunicationState == CommunicationStates.Paused;

		public bool PrinterIsPrinting
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
			get => _printingStatePrivate;
			set
			{
				if (_printingStatePrivate != value)
				{
					_printingStatePrivate = value;
					OnCommunicationStateChanged(null);
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

		public double RatioIntoCurrentLayer
		{
			get
			{
				if (gCodeFileSwitcher0?.GCodeFile == null)
				{
					return 0;
				}

				return gCodeFileSwitcher0.GCodeFile.Ratio0to1IntoContainedLayer(gCodeFileSwitcher0.LineIndex);
			}
		}

		public int SecondsPrinted
		{
			get
			{
				if (PrinterIsPrinting || PrinterIsPaused || PrintIsFinished)
				{
					return (int)(timeSinceStartedPrint.ElapsedMilliseconds / 1000);
				}

				return 0;
			}
		}

		public double TargetBedTemperature
		{
			get => _targetBedTemperature;
			set
			{
				ContinuHoldingTemperature = false;
				if (_targetBedTemperature != value)
				{
					_targetBedTemperature = value;
					if (this.IsConnected)
					{
						QueueLine("M140 S{0}".FormatWith(_targetBedTemperature));
					}
				}
			}
		}

		public int TotalLayersInPrint => gCodeFileSwitcher0?.GCodeFile?.LayerCount ?? -1;

		private int NumberOfLinesInCurrentPrint => gCodeFileSwitcher0?.GCodeFile?.LineCount ?? -1;

		public int TotalSecondsInPrint
		{
			get
			{
				if (gCodeFileSwitcher0?.GCodeFile?.LineCount > 0)
				{
					if (this.FeedRateRatio != 0)
					{
						return (int)(gCodeFileSwitcher0.GCodeFile.TotalSecondsInPrint / this.FeedRateRatio);
					}

					return (int)(gCodeFileSwitcher0.GCodeFile.TotalSecondsInPrint);
				}

				return 0;
			}
		}

		public PrinterConfig Printer { get; }

		public void ReleaseAndReportFailedConnection(ConnectionFailure reason, string details = null)
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
			OnConnectionFailed(reason, details);
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

			TerminalLog.Clear();
			//Attempt connecting to a specific printer
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
				//Create and start connection thread
				Task.Run(() =>
				{
					Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

					// Allow the user to set the appropriate properties.
					var portNames = FrostedSerialPort.GetPortNames();

					//Debug.WriteLine("Open ports: {0}".FormatWith(portNames.Length));
					if (portNames.Length > 0 || IsNetworkPrinting())
					{
						// AttemptToConnect {{
						{
							string serialPortName = this.ComPort;
							int baudRate = this.BaudRate;

							// make sure we don't have a left over print task
							activePrintTask = null;

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
										serialPort = portFactory.CreateAndOpen(serialPortName, Printer.Settings, baudRate, true);
#if __ANDROID__
										ToggleHighLowHigh(serialPort);
#endif
										// TODO: Review and reconsider the cases where this was required
										// wait a bit of time to let the firmware start up
										//Thread.Sleep(500);

										CommunicationState = CommunicationStates.AttemptingToConnect;

										// We have to send a line because some printers (like old print-r-bots) do not send anything when connecting and there is no other way to know they are there.

										serialPort.Write("M105\n");

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
													&& invalidCharactersOnFirstLine <= MAX_INVALID_CONNECTION_CHARS)
												{
													// Exit loop, continue with connect
													break;
												}
												else if (invalidCharactersOnFirstLine > MAX_INVALID_CONNECTION_CHARS)
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

										CreateStreamProcessors(null, false);

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
									catch (ArgumentOutOfRangeException e)
									{
										TerminalLog.WriteLine("Exception:" + e.Message);

										OnConnectionFailed(ConnectionFailure.UnsupportedBaudRate);
									}
									catch (IOException e)
									{
										TerminalLog.WriteLine("Exception:" + e.Message);
										OnConnectionFailed(ConnectionFailure.IOException);
										if (AggContext.OperatingSystem == OSType.X11 && e.Message == "Permission denied")
										{
											UiThread.RunOnIdle(() =>
											{
												string message = @"In order for MatterControl to access the serial ports on Linux, you will need to give your user account the appropriate permissions. Run these commands in a terminal to add yourself to the correct group.
													
Ubuntu/Debian
--------------

```
# sudo gpasswd -a $USER dialout
```

Arch
----

```
# sudo gpasswd -a $USER uucp
# sudo gpasswd -a $USER lock
```

You will then need to logout and log back in to the computer for the changes to take effect. ";
												StyledMessageBox.ShowMessageBox(message, "Permission Denied".Localize());
											});
										}
									}
									catch (Exception ex)
									{
										TerminalLog.WriteLine("Exception:" + ex.Message);
										OnConnectionFailed(ConnectionFailure.Unknown);
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
				OnConnectionFailed(
					ConnectionFailure.PortUnavailable,
					$"{this.ComPort} is not available");
			}
		}

		public void DeleteFileFromSdCard(string fileName)
		{
			// Register to detect the file deleted confirmation.
			// This should have worked without this by getting the normal 'ok' on the next line. But the ok is not on its own line.
			ReadLineStartCallBacks.Register("File deleted:", FileDeleteConfirmed);
			// and send the line to delete the file
			QueueLine("M30 {0}".FormatWith(fileName.ToLower()));
		}

		/// <summary>
		/// Disable the currently active printer connection and job if it is being actively controlled by MC
		/// If we are observing an SD card print, do nothing.
		/// </summary>
		public void Disable()
		{
			if(this.CommunicationState == CommunicationStates.PrintingFromSd
				|| (this.PrinterIsPaused && this.PrePauseCommunicationState == CommunicationStates.PrintingFromSd))
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
				CommunicationState = CommunicationStates.Disconnected;
			}
			else
			{
				//Need to reset UI - even if manual disconnect
				TurnOffBedAndExtruders(TurnOff.Now);
				FanSpeed0To255 = 0;
			}
			OnEnabledChanged(null);
		}

		public void HotendTemperatureWasWritenToPrinter(string line)
		{
			double tempBeingSet = 0;
			if (GCodeFile.GetFirstNumberAfter("S", line, ref tempBeingSet))
			{
				double exturderIndex = 0;
				if (GCodeFile.GetFirstNumberAfter("T", line, ref exturderIndex))
				{
					// we set the private variable so that we don't get the callbacks called and get in a loop of setting the temp
					int hotendIndex0Based = Math.Min((int)exturderIndex, MAX_EXTRUDERS - 1);
					targetHotendTemperature[hotendIndex0Based] = tempBeingSet;
				}
				else
				{
					// we set the private variable so that we don't get the callbacks called and get in a loop of setting the temp
					targetHotendTemperature[ActiveExtruderIndex] = tempBeingSet;
				}
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
			hotendIndex0Based = Math.Min(hotendIndex0Based, MAX_EXTRUDERS - 1);
			return actualHotendTemperature[hotendIndex0Based];
		}

		public double GetTargetHotendTemperature(int hotendIndex0Based)
		{
			hotendIndex0Based = Math.Min(hotendIndex0Based, MAX_EXTRUDERS - 1);
			return targetHotendTemperature[hotendIndex0Based];
		}

		public void HaltConnectionThread()
		{
			// TODO: stopTryingToConnect is not longer used by anyone. Likely we need to wire up setting CancellationToken from this context
			//this.stopTryingToConnect = true;
		}

		public void HomeAxis(Axis axis)
		{
			string command = "G28";

			// If we are homing everything we don't need to add any details
			if (!axis.HasFlag(Axis.XYZ))
			{
				if ((axis & Axis.X) == Axis.X)
				{
					command += " X0";
				}
				if ((axis & Axis.Y) == Axis.Y)
				{
					command += " Y0";
				}
				if ((axis & Axis.Z) == Axis.Z)
				{
					command += " Z0";
				}
			}

			QueueLine(command);
			ReadPosition();
		}

		public void MoveAbsolute(Axis axis, double axisPositionMm, double feedRateMmPerMinute)
		{
			SetMovementToAbsolute();
			QueueLine("G1 {0}{1:0.###} F{2}".FormatWith(axis, axisPositionMm, feedRateMmPerMinute));
		}

		public void MoveAbsolute(Vector3 position, double feedRateMmPerMinute)
		{
			SetMovementToAbsolute();
			QueueLine("G1 X{0:0.###}Y{1:0.###}Z{2:0.###} F{3}".FormatWith(position.X, position.Y, position.Z, feedRateMmPerMinute));
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

		public void MoveRelative(Axis axis, double moveAmountMm, double feedRateMmPerMinute)
		{
			if (moveAmountMm != 0)
			{
				SetMovementToRelative();
				QueueLine("G1 {0}{1:0.###} F{2}".FormatWith(axis, moveAmountMm, feedRateMmPerMinute));
				SetMovementToAbsolute();
			}
		}

		public void OnCommunicationStateChanged(EventArgs e)
		{
			// Call instance event
			CommunicationStateChanged?.Invoke(this, e);
#if __ANDROID__

			//Path to the printer output file
			string pathToPrintOutputFile = Path.Combine(ApplicationDataStorage.Instance.PublicDataStoragePath, "print_output.txt");

			if (CommunicationState == CommunicationStates.FinishedPrint)
			{
				//Only write to the text file if file exists
				if (File.Exists(pathToPrintOutputFile))
				{
					Task.Run(() =>
					{
						File.WriteAllLines(pathToPrintOutputFile, this.TerminalLog.PrinterLines);
					});
				}
			}
#endif
		}

		public void OnConnectionFailed(ConnectionFailure reason, string failureDetails = null)
		{
			communicationPossible = false;

			var eventArgs = new ConnectFailedEventArgs(reason);
			ConnectionFailed?.Invoke(this, eventArgs);

			CommunicationState = CommunicationStates.Disconnected;
			OnEnabledChanged(eventArgs);
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
					PositionReadQueued = false;
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
					ErrorReported?.Invoke(this, line);
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

				//Firmware version was detected and is different
				if (firmwareVersionReported != "" && FirmwareVersion != firmwareVersionReported)
				{
					FirmwareVersion = firmwareVersionReported;
					OnFirmwareVersionRead(null);
				}
			}
		}

		// this is to make it misbehave
		//int okCount = 1;
		public void PrintingCanContinue(string line)
		{
			//if ((okCount++ % 67) != 0)
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
					|| this.communicationState == CommunicationStates.AttemptingToConnect)
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
									var processingData = ProcessReadRegEx(lastLineRead);
									lastLineRead = processingData.firstLine;
									dataLastRead += processingData.extraLines;
									dataLastRead = dataLastRead.Substring(returnPosition + 1);

									// process this command
									{
										ReadLineStartCallBacks.ProcessLine(lastLineRead);
										ReadLineContainsCallBacks.ProcessLine(lastLineRead);

										if (this.PrinterIsPrinting
											&& this.AppendElapsedTime)
										{
											lastLineRead = string.Format("{0} [{1:0.000}]\n", lastLineRead, timeSinceStartedPrint.Elapsed.TotalSeconds);
										}

										LineReceived?.Invoke(this, lastLineRead);
									}
								}
							} while (true);
						}
						timeSinceLastReadAnything.Restart();
					}

					if (PrinterIsPrinting)
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
				catch (IOException e2)
				{
					TerminalLog.WriteLine("Exception:" + e2.Message);
					OnConnectionFailed(ConnectionFailure.IOException);
				}
				catch (InvalidOperationException ex)
				{
					TerminalLog.WriteLine("Exception:" + ex.Message);
					// this happens when the serial port closes after we check and before we read it.
					OnConnectionFailed(ConnectionFailure.InvalidOperationException);
				}
				catch (UnauthorizedAccessException e3)
				{
					TerminalLog.WriteLine("Exception:" + e3.Message);
					OnConnectionFailed(ConnectionFailure.UnauthorizedAccessException);
				}
				catch (Exception)
				{
				}
			}

			Console.WriteLine("Exiting ReadFromPrinter method: " + communicationState.ToString());
		}

		public void ReadPosition(bool forceToTopOfQueue = false)
		{
			QueueLine("M114", forceToTopOfQueue);
			PositionReadQueued = true;
		}

		public void ReadSdProgress(string line)
		{
			if (line!= null)
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

			waitingForPosition.Reset();
			PositionReadQueued = false;
		}

		public static void ParseTemperatureString(string temperatureString,
			double[] actualHotendTemperature, Action<TemperatureEventArgs> hotendTemperatureChange,
			ref double actualBedTemperature, Action<TemperatureEventArgs> bedTemperatureChanged)
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

				for (int hotendIndex = 0; hotendIndex < MAX_EXTRUDERS; hotendIndex++)
				{
					string multiExtruderCheck = "T{0}:".FormatWith(hotendIndex);
					if (GCodeFile.GetFirstNumberAfter(multiExtruderCheck, temperatureString, ref readHotendTemp))
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
						CreateStreamProcessors(null, false);
						CommunicationState = CommunicationStates.Disconnected;

						// We were connected to a printer so try to reconnect
						Connect();
					}
					else
					{
						// We reset the board while attempting to connect, so now we don't have a serial port.
						// Create one and do the DTR to reset
						var resetSerialPort = FrostedSerialPortFactory.GetAppropriateFactory(this.DriverType).Create(this.ComPort, Printer.Settings);
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
			if (PrinterIsPrinting)
			{
				if (CommunicationState == CommunicationStates.PrintingFromSd)
				{
					CommunicationState = CommunicationStates.Paused;
					QueueLine("M25"); // : Pause SD print
					return;
				}

				pauseHandlingStream2.DoPause(PauseHandlingStream.PauseReason.UserRequested);
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
			if (PrinterIsPaused)
			{
				if (PrePauseCommunicationState == CommunicationStates.PrintingFromSd)
				{
					CommunicationState = CommunicationStates.PrintingFromSd;

					QueueLine("M24"); // Start/resume SD print
				}
				else
				{
					pauseHandlingStream2.Resume();
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

				//Check line for line breaks, split and process separate if necessary
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
						queuedCommandStream3?.Add(lineToWrite, forceTopOfQueue);
					}
				}
			}
		}

		#region ProcessRead
		private static Regex getQuotedParts = new Regex(@"([""'])(\\?.)*?\1", RegexOptions.Compiled);

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
			foreach(var line in addedLines)
			{
				extraLines += line + "\n";
			}

			return (lineBeingRead, extraLines);

		}
		#endregion // ProcessRead

		public bool SerialPortIsAvailable(string portName)
		//Check is serial port is in the list of available serial ports
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
			hotendIndex0Based = Math.Min(hotendIndex0Based, MAX_EXTRUDERS - 1);

			if (targetHotendTemperature[hotendIndex0Based] != temperature
				|| forceSend)
			{
				ContinuHoldingTemperature = false;
				targetHotendTemperature[hotendIndex0Based] = temperature;
				if (this.IsConnected)
				{
					QueueLine("M104 T{0} S{1}".FormatWith(hotendIndex0Based, targetHotendTemperature[hotendIndex0Based]));
					if (ActiveExtruderIndex != hotendIndex0Based)
					{
						// For smoothie, switch back to the extrude we were using before the temp change (smoothie switches to the specified extruder, marlin repetier do not)
						QueueLine("T{0}".FormatWith(ActiveExtruderIndex));
					}
				}
			}
		}

		private CancellationTokenSource printingCancellation;

		public async Task StartPrint(string gcodeFilename, PrintTask printTaskToUse = null)
		{
			if (!this.IsConnected || PrinterIsPrinting)
			{
				return;
			}

			printingCancellation = new CancellationTokenSource();

			haveReportedError = false;
			PrintWasCanceled = false;

			waitingForPosition.Reset();
			PositionReadQueued = false;

			ClearQueuedGCode();
			activePrintTask = printTaskToUse;

			await Task.Run(() =>
			{
				// LoadGCodeToPrint
				CreateStreamProcessors(gcodeFilename, this.RecoveryIsEnabled);
			});

			// DoneLoadingGCodeToPrint
			switch (communicationState)
			{
				case CommunicationStates.Connected:
					// This can happen if the printer is reset during the slicing of the part.
					break;

				case CommunicationStates.PreparingToPrint:
					{
						string filePath = this.Printer.Bed.EditContext.SourceFilePath;
						string fileName = Path.GetFileName(filePath);

						var activePrintItem = new PrintItemWrapper(new PrintItem(fileName, filePath));

						if (activePrintItem.PrintItem.Id == 0)
						{
							activePrintItem.PrintItem.Commit();
						}

						if (activePrintTask == null)
						{
							// TODO: Fix printerItemID int requirement
							activePrintTask = new PrintTask
							{
								PrintStart = DateTime.Now,
								PrinterId = this.Printer.Settings.ID.GetHashCode(),
								PrintName = activePrintItem.PrintItem.Name,
								PrintItemId = activePrintItem.PrintItem.Id,
								PrintingGCodeFileName = gcodeFilename,
								PrintComplete = false
							};

							activePrintTask.Commit();

							Task.Run(() => this.SyncProgressToDB(printingCancellation.Token)).ConfigureAwait(false);
						}
					}

					CommunicationState = CommunicationStates.Printing;
					break;

				default:
#if DEBUG
					throw new Exception("We are not preparing to print so we should not be starting to print");
#endif
					break;
			}
		}

		public bool StartSdCardPrint(string m23FileName)
		{
			if (!this.IsConnected
				|| PrinterIsPrinting
				|| string.IsNullOrEmpty(m23FileName))
			{
				return false;
			}

			currentSdBytes = 0;

			ClearQueuedGCode();
			CommunicationState = CommunicationStates.PrintingFromSd;

			QueueLine($"M23 {m23FileName.ToLower()}"); // Select SD File
			QueueLine("M24"); // Start/resume SD print

			ReadLineStartCallBacks.Register("Done printing file", DonePrintingSdFile);

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
					//connectThread.Join(JoinThreadTimeoutMs);

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
				if (markPrintCanceled
					&& activePrintTask != null)
				{
					TimeSpan printTimeSpan = DateTime.Now.Subtract(activePrintTask.PrintStart);

					activePrintTask.PrintEnd = DateTime.Now;
					activePrintTask.PrintComplete = false;
					activePrintTask.PrintingGCodeFileName = "";
					activePrintTask.Commit();
				}

				// no matter what we no longer have a print task
				activePrintTask = null;
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
			//AllowListenerNotification = false;
		}

		private void ClearQueuedGCode()
		{
			gCodeFileSwitcher0?.GCodeFile?.Clear();
		}

		private void DonePrintingSdFile(string line)
		{
			ReadLineStartCallBacks.Unregister("Done printing file", DonePrintingSdFile);
			CommunicationState = CommunicationStates.FinishedPrint;

			this.PrintJobName = null;

			// never leave the extruder and the bed hot
			TurnOffBedAndExtruders(TurnOff.Now);

			ReleaseMotors();
		}

		private void FileDeleteConfirmed(string line)
		{
			ReadLineStartCallBacks.Unregister("File deleted:", FileDeleteConfirmed);
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

		private void CreateStreamProcessors(string gcodeFilename, bool recoveryEnabled)
		{
			secondsSinceUpdateHistory = 0;
			lineSinceUpdateHistory = 0;

			totalGCodeStream?.Dispose();

			GCodeStream firstStreamToRead = null;
			if (gcodeFilename != null)
			{
				gCodeFileSwitcher0 = new GCodeSwitcher(gcodeFilename, Printer);

				if (this.RecoveryIsEnabled
					&& activePrintTask != null) // We are resuming a failed print (do lots of interesting stuff).
				{
					sendProgressStream1 = new SendProgressStream(new PrintRecoveryStream(gCodeFileSwitcher0, Printer, activePrintTask.PercentDone), Printer);
					// And increment the recovery count
					activePrintTask.RecoveryCount++;
					activePrintTask.Commit();
				}
				else
				{
					sendProgressStream1 = new SendProgressStream(gCodeFileSwitcher0, Printer);
				}

				pauseHandlingStream2 = new PauseHandlingStream(Printer, sendProgressStream1);
				firstStreamToRead = pauseHandlingStream2;
			}
			else
			{
				gCodeFileSwitcher0 = null;
				firstStreamToRead = new NotPrintingStream(Printer);
			}

			queuedCommandStream3 = new QueuedCommandsStream(Printer, firstStreamToRead);
			relativeToAbsoluteStream4 = new RelativeToAbsoluteStream(Printer, queuedCommandStream3);
			bool enableLineSpliting = gcodeFilename != null && Printer.Settings.GetValue<bool>(SettingsKey.enable_line_splitting);
			babyStepsStream5 = new BabyStepsStream(Printer, relativeToAbsoluteStream4, enableLineSpliting ? 1 : 2000);
			if (activePrintTask != null)
			{
				// make sure we are in the position we were when we stopped printing
				babyStepsStream5.Offset = new Vector3(activePrintTask.PrintingOffsetX, activePrintTask.PrintingOffsetY, activePrintTask.PrintingOffsetZ);
			}
			printLevelingStream6 = new PrintLevelingStream(Printer, babyStepsStream5, true);
			waitForTempStream7 = new WaitForTempStream(Printer, printLevelingStream6);
			extrusionMultiplyerStream8 = new ExtrusionMultiplyerStream(Printer, waitForTempStream7);
			feedrateMultiplyerStream9 = new FeedRateMultiplyerStream(Printer, extrusionMultiplyerStream8);
			requestTemperaturesStream10 = new RequestTemperaturesStream(Printer, feedrateMultiplyerStream9);
			processWriteRegExStream11 = new ProcessWriteRegexStream(Printer, requestTemperaturesStream10, queuedCommandStream3);
			totalGCodeStream = processWriteRegExStream11;

			// Force a reset of the printer checksum state (but allow it to be write regexed)
			var transformedCommand = processWriteRegExStream11?.ProcessWriteRegEx("M110 N1");
			if (transformedCommand != null)
			{
				foreach (var line in transformedCommand)
				{
					WriteChecksumLine(line);
				}
			}

			// Get the current position of the printer any time we reset our streams
			ReadPosition();
		}

		private void SyncProgressToDB(CancellationToken cancellationToken)
		{
			//var timer = Stopwatch.StartNew();

			while (!cancellationToken.IsCancellationRequested
				&& this.CommunicationState != CommunicationStates.FinishedPrint
				&& this.communicationState != CommunicationStates.Connected)
			{
				double secondsSinceStartedPrint = timeSinceStartedPrint.Elapsed.TotalSeconds;

				if (timeSinceStartedPrint.Elapsed.TotalSeconds > 0
					&& gCodeFileSwitcher0 != null
					&& (secondsSinceUpdateHistory > secondsSinceStartedPrint
					|| secondsSinceUpdateHistory + 1 < secondsSinceStartedPrint
					|| lineSinceUpdateHistory + 20 < gCodeFileSwitcher0.LineIndex))
				{
					double currentDone = gCodeFileSwitcher0.GCodeFile.PercentComplete(gCodeFileSwitcher0.LineIndex);
					// Only update the amount done if it is greater than what is recorded.
					// We don't want to mess up the resume before we actually resume it.
					if (activePrintTask != null
					    && babyStepsStream5 != null
						&& activePrintTask.PercentDone < currentDone)
					{
						activePrintTask.PercentDone = currentDone;
						activePrintTask.PrintingOffsetX = (float)babyStepsStream5.Offset.X;
						activePrintTask.PrintingOffsetY = (float)babyStepsStream5.Offset.Y;
						activePrintTask.PrintingOffsetZ = (float)babyStepsStream5.Offset.Z;
						activePrintTask?.Commit();

						// Interval looks to be ~10ms
						//Console.WriteLine("DB write: {0}ms", timer.ElapsedMilliseconds);
						//timer.Restart();
					}
					secondsSinceUpdateHistory = secondsSinceStartedPrint;
					lineSinceUpdateHistory = gCodeFileSwitcher0.LineIndex;
				}

				Thread.Sleep(5);
			}

			// Console.WriteLine("Syncing print to db stopped");
		}

		private void MovementWasSetToAbsoluteMode(string line)
		{
			movementMode = PrinterMachineInstruction.MovementTypes.Absolute;
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

		private void OnEnabledChanged(EventArgs e)
		{
			EnableChanged?.Invoke(this, e);
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
				timeSinceStartedPrint.Stop();
				DetailedPrintingState = DetailedPrintingState.HomingAxis;
			}
			else if (waitForTempStream7?.HeatingBed ?? false)
			{
				// don't time the heating bed operation
				timeSinceStartedPrint.Stop();
				DetailedPrintingState = DetailedPrintingState.HeatingBed;
			}
			else if (waitForTempStream7?.HeatingExtruder ?? false)
			{
				// don't time the heating extruder operation
				timeSinceStartedPrint.Stop();
				DetailedPrintingState = DetailedPrintingState.HeatingExtruder;
			}
			else
			{
				// make sure we time all of the printing that we are doing
				if (this.PrinterIsPrinting && !this.PrinterIsPaused)
				{
					timeSinceStartedPrint.Start();
				}
				DetailedPrintingState = DetailedPrintingState.Printing;
			}
		}

		private string currentSentLine;
		private string previousSentLine;

		private int ExpectedWaitSeconds(string lastInstruction)
		{
			if (lastInstruction.Contains("G0 ") || lastInstruction.Contains("G1 "))
			{
				// for moves we wait only as much as 2 seconds
				return 2;
			}

			return 10;
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
						}
						else
						{
							// we are waiting for the ok so let's wait
							return;
						}
					}
				}
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

					previousSentLine = this.currentSentLine;
					currentSentLine = totalGCodeStream.ReadLine();

					if (currentSentLine != null)
					{
						if (currentSentLine.EndsWith("; NO_PROCESSING"))
						{
							// make sure our processing pipe knows the translated position after a NO_PROCESSING
							ReadPosition(true);
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
					}
					else if (communicationState == CommunicationStates.Printing)// we finished printing normally
					{
						CommunicationState = CommunicationStates.FinishedPrint;

						this.PrintJobName = null;

						// get us back to the no printing setting (this will clear the queued commands)
						CreateStreamProcessors(null, false);

						// never leave the extruder and the bed hot
						ReleaseMotors();
						TurnOffBedAndExtruders(TurnOff.Now);
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
				if(queuedCommandStream3 != null)
				{
					return queuedCommandStream3.Count;
				}

				return 0;
			}
		}

		public bool AppendElapsedTime { get; set; }
		public bool AllowLeveling
		{
			get => printLevelingStream6.AllowLeveling;
			set => printLevelingStream6.AllowLeveling = value;
		}

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
				bool currentlyWaiting = ContinuHoldingTemperature && TimeHaveBeenHoldingTemperature.IsRunning && TimeHaveBeenHoldingTemperature.Elapsed.TotalSeconds < TimeToHoldTemperature;
				SecondsToHoldTemperature = TimeToHoldTemperature;
				ContinuHoldingTemperature = true;
				TimeHaveBeenHoldingTemperature = Stopwatch.StartNew();
				if (!currentlyWaiting)
				{
					TemporarilyHoldingTemp?.Invoke(this, null);
					// wait secondsToWait and turn off the heaters
					Task.Run(() =>
					{
						while (TimeHaveBeenHoldingTemperature.Elapsed.TotalSeconds < TimeToHoldTemperature
							&& ContinuHoldingTemperature)
						{
							if (CommunicationState == CommunicationStates.PreparingToPrint
								|| PrinterIsPrinting)
							{
								ContinuHoldingTemperature = false;
							}

							if (!AnyHeatIsOn)
							{
								ContinuHoldingTemperature = false;
							}

							SecondsToHoldTemperature = ContinuHoldingTemperature ? Math.Max(0, TimeToHoldTemperature - TimeHaveBeenHoldingTemperature.Elapsed.TotalSeconds) : 0;
							Thread.Sleep(100);
						}

						// times up turn off heaters
						if (ContinuHoldingTemperature
							&& !PrinterIsPrinting
							&& !PrinterIsPaused)
						{
							UiThread.RunOnIdle(() =>
							{
								for (int i = 0; i < this.ExtruderCount; i++)
								{
									SetTargetHotendTemperature(i, 0, true);
								}
								TargetBedTemperature = 0;
							});
						}
					});
				}
			}
		}

		// this is to make it misbehave, chaos monkey, bad checksum
		//int checkSumCount = 1;
		private void WriteChecksumLine(string lineToWrite)
		{
			bool sendLineWithChecksum = true;
			sendLineWithChecksum = !lineToWrite.Contains("WRITE_RAW");

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

				//if ((checkSumCount++ % 11) == 0)
				//lineWithChecksum = lineWithCount + "*" + (GCodeFile.CalculateChecksum(lineWithCount) + checkSumCount).ToString();

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
					// If we get a home command, ask the printer where it is after sending it.
					if (lineWithoutChecksum.Contains("G28") // is a home
						|| lineWithoutChecksum.Contains("G29") // is a bed level
						|| lineWithoutChecksum.Contains("G30") // is a bed level
						|| lineWithoutChecksum.Contains("G92") // is a reset of printer position
						|| (lineWithoutChecksum.StartsWith("T") && !lineWithoutChecksum.StartsWith("T:"))) // is a switch extruder (verify this is the right time to ask this)
					{
						ReadPosition(true);
					}

					// write data to communication
					{
						if (lineWithoutChecksum != null)
						{
							WriteLineStartCallBacks.ProcessLine(lineWithoutChecksum);
							WriteLineContainsCallBacks.ProcessLine(lineWithoutChecksum);

							var terminalLine = lineToWrite;
							if (PrinterIsPrinting)
							{
								string lineWithoutCR = terminalLine.TrimEnd();
								terminalLine = string.Format("{0} [{1:0.000}]\n", lineWithoutCR, timeSinceStartedPrint.Elapsed.TotalSeconds);
							}

							LineSent?.Invoke(this, terminalLine);
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
						//Debug.Write("w: " + lineToWrite);
					}
					catch (IOException ex)
					{
						TerminalLog.WriteLine("Exception:" + ex.Message);

						if (CommunicationState == CommunicationStates.AttemptingToConnect)
						{
							// Handle hardware disconnects by relaying the failure reason and shutting down open resources
							ReleaseAndReportFailedConnection(ConnectionFailure.ConnectionLost, ex.Message);
						}
					}
					catch (TimeoutException e2) // known ok
					{
						// This writes on the next line, and there may have been another write attempt before it is printer. Write indented to attempt to show its association.
						TerminalLog.WriteLine("        Error writing command:" + e2.Message);
					}
					catch (UnauthorizedAccessException e3)
					{
						TerminalLog.WriteLine("Exception:" + e3.Message);
						ReleaseAndReportFailedConnection(ConnectionFailure.UnauthorizedAccessException, e3.Message);
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
			queuedCommandStream3?.Reset();
		}

		public void MacroCancel()
		{
			babyStepsStream5?.CancelMoves();
			waitForTempStream7?.Cancel();
			queuedCommandStream3?.Cancel();
		}

		public void Dispose()
		{
			Disposed?.Invoke(this, null);
		}

		internal int currentReadThreadIndex = 0;

		public class ReadThread
		{
			private int creationIndex;

			private static int numRunning = 0;
			private PrinterConnection printerConnection;

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

					// TODO: Consider if passing non-printer messages through LineSent is acceptable or if a dedicated event would add clarity
					printerConnection?.LineSent?.Invoke(this, "Read Thread Has Exited.\n");
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
			private string[] ringBuffer = new string[RingBufferCount];

			public int Count { get { return addedCount; } }

			public string this[int index]
			{
				get
				{
					return ringBuffer[index % RingBufferCount];
				}

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

	public class ConnectFailedEventArgs : EventArgs
	{
		public ConnectionFailure Reason { get; }

		public ConnectFailedEventArgs(ConnectionFailure reason)
		{
			this.Reason = reason;
		}
	}

	public enum ConnectionFailure
	{
		Unknown,
		AlreadyConnected,
		MaximumErrorsReached,
		PortNotFound,
		PortInUse,
		WriteFailed,
		UnsupportedBaudRate,
		PortUnavailable,
		Aborted,
		FailedToConnect,
		IOException,
		InvalidOperationException,
		UnauthorizedAccessException,
		ConnectionLost,
		UsbDisconnected
	}

	public class NamedItemEventArgs : EventArgs
	{
		public NamedItemEventArgs(string name)
		{
			this.ItemName = name;
		}

		public string ItemName { get; }
	}

	/// <summary>
	/// This is a class to pass temperatures to callbacks that expect them.
	/// </summary>
	public class TemperatureEventArgs : EventArgs
	{
		public TemperatureEventArgs(int index0Based, double temperature)
		{
			this.Index0Based = index0Based;
			this.Temperature = temperature;
		}

		public int Index0Based { get; }
		public double Temperature { get; }
	}
}
