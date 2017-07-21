/*
Copyright (c) 2014, Lars Brubaker
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

using Gaming.Game;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.SerialPortCommunication;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using MatterHackers.VectorMath;
using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.PrinterCommunication
{
	public static class ExtensionMethods
	{
		private static TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

		public static string GetFriendlyName(this PrintItemWrapper printItemWrapper)
		{
			if (printItemWrapper?.Name == null)
			{
				return "";
			}

			return textInfo?.ToTitleCase(printItemWrapper.Name.Replace('_', ' '));
		}
	}

	/// <summary>
	/// This is the class that communicates with a RepRap printer over the serial port.
	/// It handles opening and closing the serial port and does quite a bit of gcode parsing.
	/// It should be refactored into better modules at some point.
	/// </summary>
	public class PrinterConnectionAndCommunication
	{
		public RootedObjectEventHandler ActivePrintItemChanged = new RootedObjectEventHandler();

		public RootedObjectEventHandler BedTemperatureRead = new RootedObjectEventHandler();

		public RootedObjectEventHandler BedTemperatureSet = new RootedObjectEventHandler();

		public RootedObjectEventHandler CommunicationStateChanged = new RootedObjectEventHandler();

		public RootedObjectEventHandler CommunicationUnconditionalFromPrinter = new RootedObjectEventHandler();

		public RootedObjectEventHandler CommunicationUnconditionalToPrinter = new RootedObjectEventHandler();

		public RootedObjectEventHandler ConnectionFailed = new RootedObjectEventHandler();

		public RootedObjectEventHandler ConnectionSucceeded = new RootedObjectEventHandler();

		public RootedObjectEventHandler DestinationChanged = new RootedObjectEventHandler();

		public RootedObjectEventHandler EnableChanged = new RootedObjectEventHandler();

		public RootedObjectEventHandler ExtruderTemperatureRead = new RootedObjectEventHandler();

		public RootedObjectEventHandler ExtruderTemperatureSet = new RootedObjectEventHandler();

		public RootedObjectEventHandler FanSpeedSet = new RootedObjectEventHandler();

		public RootedObjectEventHandler FirmwareVersionRead = new RootedObjectEventHandler();

		public RootedObjectEventHandler PositionRead = new RootedObjectEventHandler();

		public RootedObjectEventHandler PrintFinished = new RootedObjectEventHandler();

		public RootedObjectEventHandler PauseOnLayer = new RootedObjectEventHandler();

		public RootedObjectEventHandler FilamentRunout = new RootedObjectEventHandler();

		public RootedObjectEventHandler PrintingStateChanged = new RootedObjectEventHandler();

		public RootedObjectEventHandler ReadLine = new RootedObjectEventHandler();

		public RootedObjectEventHandler WroteLine = new RootedObjectEventHandler();

		public bool WatingForPositionRead
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

		public RootedObjectEventHandler AtxPowerStateChanged = new RootedObjectEventHandler();

		private bool atxPowerIsOn = false;

		internal const int MAX_EXTRUDERS = 16;

		private const int MAX_INVALID_CONNECTION_CHARS = 3;

		private static PrinterConnectionAndCommunication globalInstance;

		private object locker = new object();

		private readonly int JoinThreadTimeoutMs = 5000;

		private PrintItemWrapper activePrintItem;

		private PrintTask activePrintTask;

		private double actualBedTemperature;

		private int currentlyActiveExtruderIndex = 0;

		private double[] actualExtruderTemperature = new double[MAX_EXTRUDERS];

		private CheckSumLines allCheckSumLinesSent = new CheckSumLines();

		private int backupAmount = 0;

		private CommunicationStates communicationState = CommunicationStates.Disconnected;

		private string connectionFailureMessage = "Unknown Reason";

		private Thread connectThread;

		private PrinterMove currentDestination;

		public double CurrentExtruderDestination { get { return currentDestination.extrusion; } }

		public double CurrentFeedRate { get { return currentDestination.feedRate; } }

		private double currentSdBytes = 0;

		private string doNotAskAgainMessage = "Don't remind me again".Localize();

		private PrinterMachineInstruction.MovementTypes extruderMode = PrinterMachineInstruction.MovementTypes.Absolute;

		private int fanSpeed;

		private bool firmwareUriGcodeSend = false;

		private int currentLineIndexToSend = 0;

		private bool ForceImmediateWrites = false;

		private string gcodeWarningMessage = "The file you are attempting to print is a GCode file.\n\nIt is recommended that you only print Gcode files known to match your printer's configuration.\n\nAre you sure you want to print this GCode file?".Localize();

		private string itemNotFoundMessage = "Item not found".Localize();

		private string lastLineRead = "";

		private PrinterMove lastReportedPosition;

		private DataViewGraph sendTimeAfterOkGraph;

		private GCodeFile loadedGCode = new GCodeFileLoaded();

		private GCodeFileStream gCodeFileStream0 = null;
		private PauseHandlingStream pauseHandlingStream1 = null;
		private QueuedCommandsStream queuedCommandStream2 = null;
		private RelativeToAbsoluteStream relativeToAbsoluteStream3 = null;
		private PrintLevelingStream printLevelingStream4 = null;
		private WaitForTempStream waitForTempStream5 = null;
		private BabyStepsStream babyStepsStream6 = null;
		private ExtrusionMultiplyerStream extrusionMultiplyerStream7 = null;
		private FeedRateMultiplyerStream feedrateMultiplyerStream8 = null;
		private RequestTemperaturesStream requestTemperaturesStream9 = null;
		private ProcessWriteRegexStream processWriteRegExStream10 = null;

		private GCodeStream totalGCodeStream = null;

		private PrinterMachineInstruction.MovementTypes movementMode = PrinterMachineInstruction.MovementTypes.Absolute;

		public CommunicationStates PrePauseCommunicationState { get; private set; } = CommunicationStates.Printing;

		private DetailedPrintingState printingStatePrivate;

		private FoundStringContainsCallbacks ReadLineContainsCallBacks = new FoundStringContainsCallbacks();

		private FoundStringStartsWithCallbacks ReadLineStartCallBacks = new FoundStringStartsWithCallbacks();

		private string removeFromQueueMessage = "Cannot find this file\nWould you like to remove it from the queue?".Localize();

		// we start out by setting it to a nothing file
		private IFrostedSerialPort serialPort;

		private bool stopTryingToConnect = false;

		private double targetBedTemperature;

		private double[] targetExtruderTemperature = new double[MAX_EXTRUDERS];

		private Stopwatch timeHaveBeenWaitingForOK = new Stopwatch();

		private Stopwatch timeSinceLastReadAnything = new Stopwatch();

		private Stopwatch timeSinceLastWrite = new Stopwatch();

		private Stopwatch timeSinceRecievedOk = new Stopwatch();

		private Stopwatch timeSinceStartedPrint = new Stopwatch();

		private Stopwatch timeWaitingForSdProgress = new Stopwatch();

		private double totalSdBytes = 0;

		private bool PositionReadQueued { get; set; } = false;
		private Stopwatch waitingForPosition = new Stopwatch();

		private FoundStringContainsCallbacks WriteLineContainsCallBacks = new FoundStringContainsCallbacks();

		private FoundStringStartsWithCallbacks WriteLineStartCallBacks = new FoundStringStartsWithCallbacks();

		private double secondsSinceUpdateHistory = 0;

		private EventHandler unregisterEvents;

		private double feedRateRatio = 1;

		private PrinterConnectionAndCommunication()
		{
			MonitorPrinterTemperature = true;

			StringComparer stringComparer = StringComparer.OrdinalIgnoreCase;
			ReadLineStartCallBacks.AddCallbackToKey("start", FoundStart);
			ReadLineStartCallBacks.AddCallbackToKey("start", PrintingCanContinue);

			ReadLineStartCallBacks.AddCallbackToKey("ok", SuppressEcho);
			ReadLineStartCallBacks.AddCallbackToKey("wait", SuppressEcho);
			ReadLineStartCallBacks.AddCallbackToKey("T:", SuppressEcho); // repetier

			ReadLineStartCallBacks.AddCallbackToKey("ok", PrintingCanContinue);
			ReadLineStartCallBacks.AddCallbackToKey("Done saving file", PrintingCanContinue);

			ReadLineStartCallBacks.AddCallbackToKey("ok T0:", ReadTemperatures); // marlin
			ReadLineStartCallBacks.AddCallbackToKey("B:", ReadTemperatures); // smoothie

			ReadLineStartCallBacks.AddCallbackToKey("SD printing byte", ReadSdProgress); // repetier

			ReadLineStartCallBacks.AddCallbackToKey("C:", ReadTargetPositions);
			ReadLineStartCallBacks.AddCallbackToKey("ok C:", ReadTargetPositions); // smoothie is reporting the C: with an ok first.
			ReadLineStartCallBacks.AddCallbackToKey("X:", ReadTargetPositions);

			ReadLineContainsCallBacks.AddCallbackToKey("T:", ReadTemperatures);

			ReadLineStartCallBacks.AddCallbackToKey("rs ", PrinterRequestsResend); // smoothie is lower case and no :
			ReadLineStartCallBacks.AddCallbackToKey("RS:", PrinterRequestsResend);
			ReadLineContainsCallBacks.AddCallbackToKey("Resend:", PrinterRequestsResend);

			ReadLineContainsCallBacks.AddCallbackToKey("FIRMWARE_NAME:", PrinterStatesFirmware);
			ReadLineStartCallBacks.AddCallbackToKey("EXTENSIONS:", PrinterStatesExtensions);

			#region hardware failure callbacks

			// smoothie temperature failures
			ReadLineContainsCallBacks.AddCallbackToKey("T:inf", PrinterReportsError);
			ReadLineContainsCallBacks.AddCallbackToKey("B:inf", PrinterReportsError);

			// marlin temperature failures
			ReadLineContainsCallBacks.AddCallbackToKey("MINTEMP", PrinterReportsError);
			ReadLineContainsCallBacks.AddCallbackToKey("MAXTEMP", PrinterReportsError);
			ReadLineContainsCallBacks.AddCallbackToKey("M999", PrinterReportsError);
			ReadLineContainsCallBacks.AddCallbackToKey("Error: Extruder switched off", PrinterReportsError);
			ReadLineContainsCallBacks.AddCallbackToKey("Heater decoupled", PrinterReportsError);
			ReadLineContainsCallBacks.AddCallbackToKey("cold extrusion prevented", PrinterReportsError);
			ReadLineContainsCallBacks.AddCallbackToKey("Error:Thermal Runaway, system stopped!", PrinterReportsError);
			ReadLineContainsCallBacks.AddCallbackToKey("Error:Heating failed", PrinterReportsError);

			// repetier temperature failures
			ReadLineContainsCallBacks.AddCallbackToKey("dry run mode", PrinterReportsError);
			ReadLineStartCallBacks.AddCallbackToKey("accelerometer send i2c error", PrinterReportsError);
			ReadLineStartCallBacks.AddCallbackToKey("accelerometer i2c recv error", PrinterReportsError);

			// s3g temperature failures
			ReadLineContainsCallBacks.AddCallbackToKey("Bot is Shutdown due to Overheat", PrinterReportsError);

			#endregion hardware failure callbacks

			WriteLineStartCallBacks.AddCallbackToKey("G90", MovementWasSetToAbsoluteMode);
			WriteLineStartCallBacks.AddCallbackToKey("G91", MovementWasSetToRelativeMode);
			WriteLineStartCallBacks.AddCallbackToKey("M80", AtxPowerUpWasWritenToPrinter);
			WriteLineStartCallBacks.AddCallbackToKey("M81", AtxPowerDownWasWritenToPrinter);
			WriteLineStartCallBacks.AddCallbackToKey("M82", ExtruderWasSetToAbsoluteMode);
			WriteLineStartCallBacks.AddCallbackToKey("M83", ExtruderWasSetToRelativeMode);
			WriteLineStartCallBacks.AddCallbackToKey("M104", ExtruderTemperatureWasWritenToPrinter);
			WriteLineStartCallBacks.AddCallbackToKey("M106", FanSpeedWasWritenToPrinter);
			WriteLineStartCallBacks.AddCallbackToKey("M107", FanOffWasWritenToPrinter);
			WriteLineStartCallBacks.AddCallbackToKey("M109", ExtruderTemperatureWasWritenToPrinter);
			WriteLineStartCallBacks.AddCallbackToKey("M140", BedTemperatureWasWritenToPrinter);
			WriteLineStartCallBacks.AddCallbackToKey("M190", BedTemperatureWasWritenToPrinter);
			WriteLineStartCallBacks.AddCallbackToKey("T", ExtruderIndexSet);

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				var eventArgs = e as StringEventArgs;
				if (eventArgs?.Data == SettingsKey.feedrate_ratio)
				{
					feedRateRatio = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.feedrate_ratio);
				}
			}, ref unregisterEvents);
		}

		private void ExtruderIndexSet(object sender, EventArgs e)
		{
			FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;

			double extruderBeingSet = 0;
			if (GCodeFile.GetFirstNumberAfter("T", foundStringEventArgs.LineToCheck, ref extruderBeingSet))
			{
				currentlyActiveExtruderIndex = (int)extruderBeingSet;
			}
		}

		[Flags]
		public enum Axis { X = 1, Y = 2, Z = 4, E = 8, XYZ = (X | Y | Z) }

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

		public static PrinterConnectionAndCommunication Instance
		{
			get
			{
				if (globalInstance == null)
				{
					globalInstance = new PrinterConnectionAndCommunication();
				}
				return globalInstance;
			}
		}

		public PrintItemWrapper ActivePrintItem
		{
			get
			{
				return this.activePrintItem;
			}
			set
			{
				if (!PrinterIsPrinting)
				{
					if (this.activePrintItem != value)
					{
						this.activePrintItem = value;
						if (CommunicationState == CommunicationStates.FinishedPrint)
						{
							CommunicationState = CommunicationStates.Connected;
						}
						OnActivePrintItemChanged(null);
					}
				}
				else
				{
					throw new Exception("Cannot change active print while printing");
				}
			}
		}

		public double ActualBedTemperature
		{
			get
			{
				return actualBedTemperature;
			}
		}

		public int BaudRate
		{
			get
			{
				int baudRate = 250000;
				if (this.ActivePrinter != null)
				{
					try
					{
						if (!string.IsNullOrEmpty(ActiveSliceSettings.Instance.GetValue(SettingsKey.baud_rate)))
						{
							baudRate = Convert.ToInt32(ActiveSliceSettings.Instance.GetValue(SettingsKey.baud_rate));
						}
					}
					catch
					{
					}
				}
				return baudRate;
			}
		}

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
#if DEBUG
						if (serialPort == null)
						{
							throw new Exception("The serial port should be constructed prior to setting this or we can fail our connection on a write before it has a chance to be created.");
						}
#endif
						break;

					case CommunicationStates.Connected:
						SendLineToPrinterNow("M115");
						ReadPosition();
						break;

					case CommunicationStates.ConnectionLost:
					case CommunicationStates.Disconnected:
						TurnOffBedAndExtruders();
						for (int extruderIndex = 0; extruderIndex < MAX_EXTRUDERS; extruderIndex++)
						{
							actualExtruderTemperature[extruderIndex] = 0;
							OnExtruderTemperatureRead(new TemperatureEventArgs(extruderIndex, GetActualExtruderTemperature(extruderIndex)));
						}

						actualBedTemperature = 0;
						OnBedTemperatureRead(new TemperatureEventArgs(0, ActualBedTemperature));
						break;
				}

				if (communicationState != value)
				{
					CommunicationUnconditionalToPrinter.CallEvents(this, new StringEventArgs("Communication State: {0}\n".FormatWith(value.ToString())));

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
										TimeSpan printTimeSpan = DateTime.Now.Subtract(activePrintTask.PrintStart);

										activePrintTask.PrintEnd = DateTime.Now;
										activePrintTask.PercentDone = 100;
										activePrintTask.PrintComplete = true;
										activePrintTask.Commit();
									}

									// Set this early as we always want our functions to know the state we are in.
									communicationState = value;
									timeSinceStartedPrint.Stop();
									OnPrintFinished(null);
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

		public string ComPort => ActiveSliceSettings.Instance?.Helpers.ComPort();

		public string DriverType => ActiveSliceSettings.Instance?.GetValue("driver_type");

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
					SendLineToPrinterNow("M80");
				}
				else
				{
					SendLineToPrinterNow("M81");
				}
			}
		}

		public string ConnectionFailureMessage { get { return connectionFailureMessage; } }

		public Vector3 CurrentDestination { get { return currentDestination.position; } }

		public int CurrentlyPrintingLayer
		{
			get
			{
				if (gCodeFileStream0 != null)
				{
					int instructionIndex = gCodeFileStream0.LineIndex - backupAmount;
					return loadedGCode.GetLayerIndex(instructionIndex);
				}

				return 0;
			}
		}

		public string DeviceCode { get; private set; }

		public bool Disconnecting
		{
			get
			{
				return CommunicationState == CommunicationStates.Disconnecting;
			}
		}

		public int FanSpeed0To255
		{
			get { return fanSpeed; }
			set
			{
				fanSpeed = Math.Max(0, Math.Min(255, value));
				OnFanSpeedSet(null);
				if (PrinterIsConnected)
				{
					SendLineToPrinterNow("M106 S{0}".FormatWith(fanSpeed));
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
					&& loadedGCode != null
					&& gCodeFileStream0 != null)
				{
					return loadedGCode.PercentComplete(gCodeFileStream0.LineIndex);
				}
				else
				{
					return 0.0;
				}
			}
		}

		public string PrinterConnectionStatusVerbose
		{
			get
			{
				switch (CommunicationState)
				{
					case CommunicationStates.Disconnected:
						return "Not Connected".Localize();

					case CommunicationStates.Disconnecting:
						return "Disconnecting".Localize();

					case CommunicationStates.AttemptingToConnect:
						return "Connecting".Localize() + "...";

					case CommunicationStates.ConnectionLost:
						return "Connection Lost".Localize();

					case CommunicationStates.FailedToConnect:
						return "Unable to Connect".Localize();

					case CommunicationStates.Connected:
						return "Connected".Localize();

					case CommunicationStates.PreparingToPrint:
						return "Preparing To Print".Localize();

					case CommunicationStates.Printing:
						return "Printing".Localize();

					case CommunicationStates.PrintingFromSd:
						return "Printing From SD Card".Localize();

					case CommunicationStates.Paused:
						return "Paused".Localize();

					case CommunicationStates.FinishedPrint:
						return "Finished Print".Localize();

					default:
						throw new NotImplementedException("Make sure every status returns the correct connected state.");
				}
			}
		}

		public bool PrinterIsConnected
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

		public bool PrinterIsPaused
		{
			get
			{
				return CommunicationState == CommunicationStates.Paused;
			}
		}

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

		public DetailedPrintingState PrintingState
		{
			get
			{
				return printingStatePrivate;
			}

			set
			{
				if (printingStatePrivate != value)
				{
					printingStatePrivate = value;
					PrintingStateChanged.CallEvents(this, null);
				}
			}
		}

		public string PrintingStateString
		{
			get
			{
				switch (PrintingState)
				{
					case DetailedPrintingState.HomingAxis:
						return "Homing Axis".Localize();

					case DetailedPrintingState.HeatingBed:
						return "Waiting for Bed to Heat to".Localize() + $" {TargetBedTemperature}°";

					case DetailedPrintingState.HeatingExtruder:
						return "Waiting for Extruder to Heat to".Localize() + $" {GetTargetExtruderTemperature(0)}°";

					case DetailedPrintingState.Printing:
						return "Currently Printing".Localize() + ":";

					default:
						return "";
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

		public bool PrintIsFinished
		{
			get
			{
				return CommunicationState == CommunicationStates.FinishedPrint;
			}
		}

		public string PrintJobName { get; private set; } = null;

		public bool PrintWasCanceled { get; set; } = false;

		public double RatioIntoCurrentLayer
		{
			get
			{
				if (gCodeFileStream0 == null)
				{
					return 0;
				}

				int instructionIndex = gCodeFileStream0.LineIndex - backupAmount;
				return loadedGCode.Ratio0to1IntoContainedLayer(instructionIndex);
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
			get
			{
				return targetBedTemperature;
			}
			set
			{
				if (targetBedTemperature != value)
				{
					targetBedTemperature = value;
					OnBedTemperatureSet(new TemperatureEventArgs(0, TargetBedTemperature));
					if (PrinterIsConnected)
					{
						SendLineToPrinterNow("M140 S{0}".FormatWith(targetBedTemperature));
					}
				}
			}
		}

		public int TotalLayersInPrint
		{
			get
			{
				try
				{
					int layerCount = loadedGCode.NumChangesInZ;
					return layerCount;
				}
				catch (Exception)
				{
					return -1;
				}
			}
		}

		public int TotalSecondsInPrint
		{
			get
			{
				if (loadedGCode.LineCount > 0)
				{
					if (feedRateRatio != 0)
					{
						return (int)(loadedGCode.TotalSecondsInPrint / feedRateRatio);
					}

					return (int)(loadedGCode.TotalSecondsInPrint);
				}

				return 0;
			}
		}

		// TODO: Consider having callers use the source rather than this proxy? Maybe better to change after arriving on a final type and location for printer settings
		public PrinterSettings ActivePrinter => ActiveSliceSettings.Instance;

		private int NumberOfLinesInCurrentPrint
		{
			get
			{
				return loadedGCode.LineCount;
			}
		}

		/// <summary>
		/// Abort an ongoing attempt to establish communication with a printer due to the specified problem. This is a specialized
		/// version of the functionality that's previously been in .Disable but focused specifically on the task of aborting an
		/// ongoing connection. Ideally we should unify all abort invocations to use this implementation rather than the mix
		/// of occasional OnConnectionFailed calls, .Disable and .stopTryingToConnect
		/// </summary>
		/// <param name="abortReason">The concise message which will be used to describe the connection failure</param>
		/// <param name="shutdownReadLoop">Shutdown/join the readFromPrinterThread</param>
		public void AbortConnectionAttempt(string abortReason, bool shutdownReadLoop = true)
		{
			// Set .Disconnecting to allow the read loop to exit gracefully before a forced thread join (and extended timeout)
			CommunicationState = CommunicationStates.Disconnecting;

			// Shutdown the connectionAttempt thread
			if (connectThread != null)
			{
				connectThread.Join(JoinThreadTimeoutMs); //Halt connection thread
			}

			// Shutdown the readFromPrinter thread
			if (shutdownReadLoop)
			{
				ReadThread.Join();
			}

			// Shutdown the serial port
			if (serialPort != null)
			{
				// Close and dispose the serial port
				serialPort.Close();
				serialPort.Dispose();
				serialPort = null;
			}

			// Set the final communication state
			CommunicationState = CommunicationStates.Disconnected;

			// Set the connection failure message and call OnConnectionFailed
			connectionFailureMessage = abortReason;

			// Notify
			OnConnectionFailed(null);
		}

		public void BedTemperatureWasWritenToPrinter(object sender, EventArgs e)
		{
			FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;

			string[] splitOnS = foundStringEventArgs.LineToCheck.Split('S');
			if (splitOnS.Length == 2)
			{
				string temp = splitOnS[1];
				try
				{
					double tempBeingSet = double.Parse(temp);
					if (TargetBedTemperature != tempBeingSet)
					{
						// we set the private variable so that we don't get the callbacks called and get in a loop of setting the temp
						targetBedTemperature = tempBeingSet;
						OnBedTemperatureSet(new TemperatureEventArgs(0, TargetBedTemperature));
					}
				}
				catch (Exception)
				{
				}
			}
		}

		public void ConnectToActivePrinter(bool showHelpIfNoPort = false)
		{
			if (ActivePrinter != null)
			{
				// Start the process of requesting permission and exit if permission is not currently granted
				if (!ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.enable_network_printing)
					&& !FrostedSerialPort.EnsureDeviceAccess())
				{
					CommunicationState = CommunicationStates.FailedToConnect;
					return;
				}

				PrinterOutputCache.Instance.Clear();
				//Attempt connecting to a specific printer
				this.stopTryingToConnect = false;
				this.FirmwareType = FirmwareTypes.Unknown;
				firmwareUriGcodeSend = false;

				// On Android, there will never be more than one serial port available for us to connect to. Override the current .ComPort value to account for
				// this aspect to ensure the validation logic that verifies port availability/in use status can proceed without additional workarounds for Android
#if __ANDROID__
				string currentPortName = FrostedSerialPort.GetPortNames().FirstOrDefault();
				if (!string.IsNullOrEmpty(currentPortName))
				{
					// TODO: Ensure that this does *not* cause a write to the settings file and should be an in memory update only
					ActiveSliceSettings.Instance?.Helpers.SetComPort(currentPortName);
				}
#endif

				if (SerialPortIsAvailable(this.ComPort))
				{
					//Create a timed callback to determine whether connection succeeded
					Timer connectionTimer = new Timer(new TimerCallback(ConnectionCallbackTimer));
					connectionTimer.Change(100, 0);

					//Create and start connection thread
					connectThread = new Thread(Connect_Thread);
					connectThread.Name = "Connect To Printer";
					connectThread.IsBackground = true;
					connectThread.Start();
				}
				else
				{
					Debug.WriteLine("Connection failed: {0}".FormatWith(this.ComPort));

					connectionFailureMessage = string.Format(
										"{0} is not available".Localize(),
										this.ComPort);

					OnConnectionFailed(null);

#if !__ANDROID__
					// Only pop up the com port helper if the USER actually CLICKED the connect button.
					if (showHelpIfNoPort)
					{
						WizardWindow.ShowComPortSetup();
					}
#endif
				}
			}
		}

		public void DeleteFileFromSdCard(string fileName)
		{
			// Register to detect the file deleted confirmation.
			// This should have worked without this by getting the normal 'ok' on the next line. But the ok is not on its own line.
			ReadLineStartCallBacks.AddCallbackToKey("File deleted:", FileDeleteConfirmed);
			// and send the line to delete the file
			SendLineToPrinterNow("M30 {0}".FormatWith(fileName.ToLower()));
		}

		public void Disable()
		{
			if (PrinterIsConnected)
			{
				// Make sure we send this without waiting for the printer to respond. We want to try and turn off the heaters.
				// It may be possible in the future to make this go into the printer queue for assured sending but it means
				// the program has to be smart about closing an able to wait until the printer has agreed that it shut off
				// the motors and heaters (a good idea and something for the future).
				ForceImmediateWrites = true;
				ReleaseMotors();
				TurnOffBedAndExtruders();
				FanSpeed0To255 = 0;
				ForceImmediateWrites = false;

				CommunicationState = CommunicationStates.Disconnecting;
				ReadThread.Join();
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
				TurnOffBedAndExtruders();
				FanSpeed0To255 = 0;
			}
			OnEnabledChanged(null);
		}

		public void ExtruderTemperatureWasWritenToPrinter(object sender, EventArgs e)
		{
			FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;

			double tempBeingSet = 0;
			if (GCodeFile.GetFirstNumberAfter("S", foundStringEventArgs.LineToCheck, ref tempBeingSet))
			{
				double exturderIndex = 0;
				if (GCodeFile.GetFirstNumberAfter("T", foundStringEventArgs.LineToCheck, ref exturderIndex))
				{
					// we set the private variable so that we don't get the callbacks called and get in a loop of setting the temp
					int extruderIndex0Based = Math.Min((int)exturderIndex, MAX_EXTRUDERS - 1);
					targetExtruderTemperature[extruderIndex0Based] = tempBeingSet;
				}
				else
				{
					// we set the private variable so that we don't get the callbacks called and get in a loop of setting the temp
					targetExtruderTemperature[currentlyActiveExtruderIndex] = tempBeingSet;
				}
				OnExtruderTemperatureSet(new TemperatureEventArgs((int)exturderIndex, tempBeingSet));
			}
		}

		public void FanOffWasWritenToPrinter(object sender, EventArgs e)
		{
			fanSpeed = 0;
			OnFanSpeedSet(null);
		}

		public void FanSpeedWasWritenToPrinter(object sender, EventArgs e)
		{
			FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;

			string[] splitOnS = foundStringEventArgs.LineToCheck.Split('S');
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

		public void FoundStart(object sender, EventArgs e)
		{
			FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;
			foundStringEventArgs.SendToDelegateFunctions = false;
		}

		public double GetActualExtruderTemperature(int extruderIndex0Based)
		{
			extruderIndex0Based = Math.Min(extruderIndex0Based, MAX_EXTRUDERS - 1);
			return actualExtruderTemperature[extruderIndex0Based];
		}

		public double GetTargetExtruderTemperature(int extruderIndex0Based)
		{
			extruderIndex0Based = Math.Min(extruderIndex0Based, MAX_EXTRUDERS - 1);
			return targetExtruderTemperature[extruderIndex0Based];
		}

		public void HaltConnectionThread()
		{
			this.stopTryingToConnect = true;
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

			SendLineToPrinterNow(command);
			ReadPosition();
		}

		public void MoveAbsolute(Axis axis, double axisPositionMm, double feedRateMmPerMinute)
		{
			SetMovementToAbsolute();
			SendLineToPrinterNow("G1 {0}{1:0.###} F{2}".FormatWith(axis, axisPositionMm, feedRateMmPerMinute));
		}

		public void MoveAbsolute(Vector3 position, double feedRateMmPerMinute)
		{
			SetMovementToAbsolute();
			SendLineToPrinterNow("G1 X{0:0.###}Y{1:0.###}Z{2:0.###} F{3}".FormatWith(position.x, position.y, position.z, feedRateMmPerMinute));
		}

		public void MoveExtruderRelative(double moveAmountMm, double feedRateMmPerMinute, int extruderNumber = 0)
		{
			if (moveAmountMm != 0)
			{
				// TODO: Long term we need to track the active extruder and make requiresToolChange be driven by the extruder you're actually on
				bool requiresToolChange = extruderNumber != 0;

				SetMovementToRelative();

				if (requiresToolChange)
				{
					SendLineToPrinterNow("T{0}".FormatWith(extruderNumber)); //Set active extruder
				}

				SendLineToPrinterNow("G1 E{0:0.###} F{1}".FormatWith(moveAmountMm, feedRateMmPerMinute));

				if (requiresToolChange)
				{
					SendLineToPrinterNow("T0"); //Reset back to extruder one
				}

				SetMovementToAbsolute();
			}
		}

		public void MoveRelative(Axis axis, double moveAmountMm, double feedRateMmPerMinute)
		{
			if (moveAmountMm != 0)
			{
				SetMovementToRelative();
				SendLineToPrinterNow("G1 {0}{1:0.###} F{2}".FormatWith(axis, moveAmountMm, feedRateMmPerMinute));
				SetMovementToAbsolute();
			}
		}

		public void OnCommunicationStateChanged(EventArgs e)
		{
			CommunicationStateChanged.CallEvents(this, e);
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
						File.WriteAllLines(pathToPrintOutputFile, PrinterOutputCache.Instance.PrinterLines);
					});
				}
			}
#endif
		}

		public void OnConnectionFailed(EventArgs e)
		{
			ConnectionFailed.CallEvents(this, e);

			CommunicationState = CommunicationStates.FailedToConnect;
			OnEnabledChanged(e);
		}

		public void OnIdle()
		{
			if (PrinterIsConnected && ReadThread.NumRunning == 0)
			{
				ReadThread.Start();
			}
		}

		public void OnPrintFinished(EventArgs e)
		{
			PrintFinished.CallEvents(this, new PrintItemWrapperEventArgs(this.ActivePrintItem));

			// TODO: Shouldn't this logic be in the UI layer where the controls are owned and hooked in via PrintFinished?
			bool oneOrMoreValuesReset = false;
			foreach (var keyValue in ActiveSliceSettings.Instance.BaseLayer)
			{
				string currentValue = ActiveSliceSettings.Instance.GetValue(keyValue.Key);

				bool valueIsClear = currentValue == "0" | currentValue == "";
				SliceSettingData data = SliceSettingsOrganizer.Instance.GetSettingsData(keyValue.Key);
				if (data?.ResetAtEndOfPrint == true && !valueIsClear)
				{
					oneOrMoreValuesReset = true;
					ActiveSliceSettings.Instance.ClearValue(keyValue.Key);
				}
			}

			if (oneOrMoreValuesReset)
			{
				ApplicationController.Instance.ReloadAdvancedControlsPanel();
			}
		}

		public void PrintActivePart(bool overrideAllowGCode = false)
		{
			try
			{
				// If leveling is required or is currently on
				if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_required_to_print)
					|| ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled))
				{
					PrintLevelingData levelingData = ActiveSliceSettings.Instance.Helpers.GetPrintLevelingData();
					if (levelingData?.HasBeenRunAndEnabled() != true)
					{
						LevelWizardBase.ShowPrintLevelWizard();
						return;
					}
				}

				if (ActivePrintItem != null)
				{
					string pathAndFile = ActivePrintItem.FileLocation;
					if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_sd_card_reader)
						&& pathAndFile == QueueData.SdCardFileName)
					{
						StartSdCardPrint();
					}
					else if (ActiveSliceSettings.Instance.IsValid())
					{
						if (File.Exists(pathAndFile))
						{
							// clear the output cache prior to starting a print
							PrinterOutputCache.Instance.Clear();

							string hideGCodeWarning = ApplicationSettings.Instance.get(ApplicationSettingsKey.HideGCodeWarning);

							if (Path.GetExtension(pathAndFile).ToUpper() == ".GCODE"
								&& hideGCodeWarning == null
								&& !overrideAllowGCode)
							{
								CheckBox hideGCodeWarningCheckBox = new CheckBox(doNotAskAgainMessage);
								hideGCodeWarningCheckBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
								hideGCodeWarningCheckBox.Margin = new BorderDouble(top: 6, left: 6);
								hideGCodeWarningCheckBox.HAnchor = Agg.UI.HAnchor.ParentLeft;
								hideGCodeWarningCheckBox.Click += (sender, e) =>
								{
									if (hideGCodeWarningCheckBox.Checked)
									{
										ApplicationSettings.Instance.set(ApplicationSettingsKey.HideGCodeWarning, "true");
									}
									else
									{
										ApplicationSettings.Instance.set(ApplicationSettingsKey.HideGCodeWarning, null);
									}
								};

								UiThread.RunOnIdle(() => StyledMessageBox.ShowMessageBox(onConfirmPrint, gcodeWarningMessage, "Warning - GCode file".Localize(), new GuiWidget[] { new VerticalSpacer(), hideGCodeWarningCheckBox }, StyledMessageBox.MessageType.YES_NO));
							}
							else
							{
								CommunicationState = PrinterConnectionAndCommunication.CommunicationStates.PreparingToPrint;
								PrintItemWrapper partToPrint = ActivePrintItem;
								SlicingQueue.Instance.QueuePartForSlicing(partToPrint);
								partToPrint.SlicingDone += partToPrint_SliceDone;
							}
						}
						else
						{
							string message = String.Format(removeFromQueueMessage, pathAndFile);
							StyledMessageBox.ShowMessageBox(onRemoveMessageConfirm, message, itemNotFoundMessage, StyledMessageBox.MessageType.YES_NO, "Remove".Localize(), "Cancel".Localize());
						}
					}
				}
			}
			catch (Exception)
			{
			}
		}

		public void PrintActivePartIfPossible(bool overrideAllowGCode = false)
		{
			if (CommunicationState == CommunicationStates.Connected || CommunicationState == CommunicationStates.FinishedPrint)
			{
				PrintActivePart(overrideAllowGCode);
			}
		}

		public void PrinterRequestsResend(object sender, EventArgs e)
		{
			FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;

			if (foundStringEventArgs != null
				&& !string.IsNullOrEmpty(foundStringEventArgs.LineToCheck))
			{
				string line = foundStringEventArgs.LineToCheck;
				// marlin and repetier send a : before the number and then and ok
				if (!GCodeFile.GetFirstNumberAfter(":", line, ref currentLineIndexToSend))
				{
					if (currentLineIndexToSend == allCheckSumLinesSent.Count)
					{
						// asking for the next line don't do anything, conitue with sending next instruction
						return;
					}
					// smoothie sends an N before the number and no ok
					if (GCodeFile.GetFirstNumberAfter("N", line, ref currentLineIndexToSend))
					{
						// clear waiting for ok because smoothie will not send it
						PrintingCanContinue(null, null);
					}
				}

				if (currentLineIndexToSend == allCheckSumLinesSent.Count)
				{
					// asking for the next line don't do anything, conitue with sending next instruction
					return;
				}

				if (currentLineIndexToSend >= allCheckSumLinesSent.Count
					|| currentLineIndexToSend == 1)
				{
					SendLineToPrinterNow("M110 N1");
					allCheckSumLinesSent.SetStartingIndex(1);
					waitingForPosition.Reset();
					PositionReadQueued = false;
				}
			}
		}

		private bool haveReportedError = false;

		public void PrinterReportsError(object sender, EventArgs e)
		{
			if (!haveReportedError)
			{
				haveReportedError = true;
				FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;
				if (foundStringEventArgs != null)
				{
					string message = "Your printer is reporting a hardware Error. This may prevent your printer from functioning properly.".Localize()
						+ "\n"
						+ "\n"
						+ "Error Reported".Localize() + ":"
						+ $" \"{foundStringEventArgs.LineToCheck}\".";
					UiThread.RunOnIdle(() =>
					StyledMessageBox.ShowMessageBox(null, message, "Printer Hardware Error".Localize())
					);
				}
			}
		}

		public void PrinterStatesExtensions(object sender, EventArgs e)
		{
			FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;
			if (foundStringEventArgs != null)
			{
				if (foundStringEventArgs.LineToCheck.Contains("URI_GCODE_SEND"))
				{
					firmwareUriGcodeSend = true;
				}
			}
		}

		public void PrinterStatesFirmware(object sender, EventArgs e)
		{
			FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;

			string firmwareName = "";
			if (GCodeFile.GetFirstStringAfter("FIRMWARE_NAME:", foundStringEventArgs.LineToCheck, " ", ref firmwareName))
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
			if (GCodeFile.GetFirstStringAfter("MACHINE_TYPE:", foundStringEventArgs.LineToCheck, " EXTRUDER_COUNT", ref firmwareVersionReported))
			{
				char splitChar = '^';
				if (firmwareVersionReported.Contains(splitChar))
				{
					string[] split = firmwareVersionReported.Split(splitChar);
					if (split.Count() == 2)
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
		public void PrintingCanContinue(object sender, EventArgs e)
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
			if (serialPort == null && this.ActivePrinter != null)
			{
				IFrostedSerialPort resetSerialPort = FrostedSerialPortFactory.GetAppropriateFactory(this.DriverType).Create(this.ComPort);
				resetSerialPort.Open();

				Thread.Sleep(500);

				ToggleHighLowHigh(resetSerialPort);

				resetSerialPort.Close();
			}
		}

		public void ReadFromPrinter(ReadThread readThreadHolder)
		{
			string dataLastRead = string.Empty;

			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
			timeSinceLastReadAnything.Restart();
			// we want this while loop to be as fast as possible. Don't allow any significant work to happen in here
			while (CommunicationState == CommunicationStates.AttemptingToConnect
				|| (PrinterIsConnected && serialPort != null && serialPort.IsOpen && !Disconnecting && readThreadHolder.IsCurrentThread()))
			{
				if ((PrinterIsConnected
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
							//Debug.Write("r: " + allDataRead);
							allDataRead = allDataRead.Replace("\r\n", "\n");
							allDataRead = allDataRead.Replace('\r', '\n');
							dataLastRead += allDataRead;
							do
							{
								int returnPosition = dataLastRead.IndexOf('\n');

								// Abort if we're AttemptingToConnect, no newline was found in the accumulator string and there's too many non-ascii chars
								if (this.communicationState == CommunicationStates.AttemptingToConnect && returnPosition < 0)
								{
									int totalInvalid = dataLastRead.Count(c => c == '?');
									if (totalInvalid > MAX_INVALID_CONNECTION_CHARS)
									{
										AbortConnectionAttempt("Invalid printer response".Localize(), false);
									}
								}

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

									lastLineRead = ProcessReadRegEx(lastLineRead);

									dataLastRead = dataLastRead.Substring(returnPosition + 1);

									// process this command
									{
										StringEventArgs currentEvent = new StringEventArgs(lastLineRead);
										if (PrinterIsPrinting)
										{
											CommunicationUnconditionalFromPrinter.CallEvents(this, new StringEventArgs("{0} [{1:0.000}]\n".FormatWith(lastLineRead, timeSinceStartedPrint.Elapsed.TotalSeconds)));
										}
										else
										{
											CommunicationUnconditionalFromPrinter.CallEvents(this, currentEvent);
										}

										FoundStringEventArgs foundResponse = new FoundStringEventArgs(currentEvent.Data);

										ReadLineStartCallBacks.CheckForKeys(foundResponse);
										ReadLineContainsCallBacks.CheckForKeys(foundResponse);

										if (foundResponse.SendToDelegateFunctions)
										{
											ReadLine.CallEvents(this, currentEvent);
										}
									}

									// If we've encountered a newline character and we're still in .AttemptingToConnect
									if (CommunicationState == CommunicationStates.AttemptingToConnect)
									{
										// TODO: This is an initial proof of concept for validating the printer response after DTR. More work is
										// needed to test this technique across existing hardware and/or edge cases where this simple approach
										// (initial line having more than 3 non-ASCII characters) may not be adequate or appropriate.
										// TODO: Revise the INVALID char count to an agreed upon threshold
										string[] segments = lastLineRead.Split('?');
										if (segments.Length <= MAX_INVALID_CONNECTION_CHARS)
										{
											CommunicationState = CommunicationStates.Connected;
											TurnOffBedAndExtruders(); // make sure our ui and the printer agree and that the printer is in a known state (not heating).
											haveReportedError = false;
											// now send any command that initialize this printer
											ClearQueuedGCode();
											string connectGCode = ActiveSliceSettings.Instance.GetValue(SettingsKey.connect_gcode);
											SendLineToPrinterNow(connectGCode);

											// and call back anyone who would like to know we connected
											UiThread.RunOnIdle(() => ConnectionSucceeded.CallEvents(this, null));

											// run the print leveling wizard if we need to for this printer
											if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_required_to_print)
												|| ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled))
											{
												PrintLevelingData levelingData = ActiveSliceSettings.Instance.Helpers.GetPrintLevelingData();
												if (levelingData?.HasBeenRunAndEnabled() != true)
												{
													UiThread.RunOnIdle(LevelWizardBase.ShowPrintLevelWizard);
												}
											}
										}
										else
										{
											// Force port shutdown and cleanup
											AbortConnectionAttempt("Invalid printer response".Localize(), false);
										}
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
					PrinterOutputCache.Instance.WriteLine("Exception:" + e2.Message);
					OnConnectionFailed(null);
				}
				catch (InvalidOperationException ex)
				{
					PrinterOutputCache.Instance.WriteLine("Exception:" + ex.Message);
					// this happens when the serial port closes after we check and before we read it.
					OnConnectionFailed(null);
				}
				catch (UnauthorizedAccessException e3)
				{
					PrinterOutputCache.Instance.WriteLine("Exception:" + e3.Message);
					OnConnectionFailed(null);
				}
				catch (Exception)
				{
				}
			}
		}

		public void ReadPosition(bool forceToTopOfQueue = false)
		{
			SendLineToPrinterNow("M114", forceToTopOfQueue);
			PositionReadQueued = true;
		}

		public void ReadSdProgress(object sender, EventArgs e)
		{
			FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;
			if (foundStringEventArgs != null)
			{
				string sdProgressString = foundStringEventArgs.LineToCheck.Substring("Sd printing byte ".Length);

				string[] values = sdProgressString.Split('/');
				currentSdBytes = long.Parse(values[0]);
				totalSdBytes = long.Parse(values[1]);
			}

			// We read it so we are no longer waiting
			timeWaitingForSdProgress.Stop();
		}

		public void ReadTargetPositions(object sender, EventArgs e)
		{
			FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;

			string lineToParse = foundStringEventArgs.LineToCheck;
			GCodeFile.GetFirstNumberAfter("X:", lineToParse, ref lastReportedPosition.position.x);
			GCodeFile.GetFirstNumberAfter("Y:", lineToParse, ref lastReportedPosition.position.y);
			GCodeFile.GetFirstNumberAfter("Z:", lineToParse, ref lastReportedPosition.position.z);
			GCodeFile.GetFirstNumberAfter("E:", lineToParse, ref lastReportedPosition.extrusion);

			//if (currentDestination != positionRead)
			{
				currentDestination = lastReportedPosition;
				DestinationChanged.CallEvents(this, null);
				if (totalGCodeStream != null)
				{
					totalGCodeStream.SetPrinterPosition(currentDestination);
				}
			}

			PositionRead.CallEvents(this, null);

			waitingForPosition.Reset();
			PositionReadQueued = false;
		}

		public void ReadTemperatures(object sender, EventArgs e)
		{
			FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;

			string temperatureString = foundStringEventArgs.LineToCheck;
			{
				double readExtruderTemp = 0;
				if (GCodeFile.GetFirstNumberAfter("T:", temperatureString, ref readExtruderTemp))
				{
					if (actualExtruderTemperature[0] != readExtruderTemp)
					{
						actualExtruderTemperature[0] = readExtruderTemp;
						OnExtruderTemperatureRead(new TemperatureEventArgs(0, GetActualExtruderTemperature(0)));
					}
				}

				for (int extruderIndex = 0; extruderIndex < MAX_EXTRUDERS; extruderIndex++)
				{
					string multiExtruderCheck = "T{0}:".FormatWith(extruderIndex);
					if (GCodeFile.GetFirstNumberAfter(multiExtruderCheck, temperatureString, ref readExtruderTemp))
					{
						if (actualExtruderTemperature[extruderIndex] != readExtruderTemp)
						{
							actualExtruderTemperature[extruderIndex] = readExtruderTemp;
							OnExtruderTemperatureRead(new TemperatureEventArgs(extruderIndex, GetActualExtruderTemperature(extruderIndex)));
						}
					}
					else
					{
						break;
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
						OnBedTemperatureRead(new TemperatureEventArgs(0, ActualBedTemperature));
					}
				}
			}
		}

		public void RebootBoard()
		{
			try
			{
				if (ActiveSliceSettings.Instance.PrinterSelected)
				{
					// first make sure we are not printing if possible (cancel slicing)
					if (serialPort != null) // we still have a serial port
					{
						Stop(false);
						ClearQueuedGCode();

						CommunicationState = CommunicationStates.Disconnecting;
						ReadThread.Join();
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
						UiThread.RunOnIdle(() =>
						{
							//HaltConnectionThread();
							ConnectToActivePrinter();
						}, 2);
					}
					else
					{
						// We reset the board while attempting to connect, so now we don't have a serial port.
						// Create one and do the DTR to reset
						var resetSerialPort = FrostedSerialPortFactory.GetAppropriateFactory(this.DriverType).Create(this.ComPort);
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

		public void ReleaseMotors()
		{
			SendLineToPrinterNow("M84");
		}

		public void RequestPause()
		{
			if (PrinterIsPrinting)
			{
				if (CommunicationState == CommunicationStates.PrintingFromSd)
				{
					CommunicationState = CommunicationStates.Paused;
					SendLineToPrinterNow("M25"); // : Pause SD print
					return;
				}

				pauseHandlingStream1.DoPause(PauseHandlingStream.PauseReason.UserRequested);
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

					SendLineToPrinterNow("M24"); // Start/resume SD print
				}
				else
				{
					pauseHandlingStream1.Resume();
					CommunicationState = CommunicationStates.Printing;
				}
			}
		}

		public void SendLinesToPrinterNow(string[] linesToWrite)
		{
			if (PrinterIsPrinting && CommunicationState != CommunicationStates.PrintingFromSd)
			{
				for (int i = linesToWrite.Length - 1; i >= 0; i--)
				{
					string line = linesToWrite[i].Trim();
					if (line.Length > 0)
					{
						SendLineToPrinterNow(line);
					}
				}
			}
			else
			{
				for (int i = 0; i < linesToWrite.Length; i++)
				{
					string line = linesToWrite[i].Trim();
					if (line.Length > 0)
					{
						SendLineToPrinterNow(line);
					}
				}
			}
		}

		public void SendLineToPrinterNow(string lineToWrite, bool forceTopOfQueue = false)
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
					SendLinesToPrinterNow(linesToWrite);
					return;
				}

				if (CommunicationState == CommunicationStates.PrintingFromSd
					|| ForceImmediateWrites)
				{
					lineToWrite = lineToWrite.Split(';')[0].Trim();
					if (lineToWrite.Trim().Length > 0)
					{
						// sometimes we need to send code without buffering (like when we are closing the program).
						WriteRawToPrinter(lineToWrite + "\n", lineToWrite);
					}
				}
				else
				{
					if (lineToWrite.Trim().Length > 0)
					{
						// insert the command into the printing queue at the head
						InjectGCode(lineToWrite, forceTopOfQueue);
					}
				}
			}
		}

		#region ProcessRead
		Regex getQuotedParts = new Regex(@"([""'])(\\?.)*?\1", RegexOptions.Compiled);
		string read_regex = "";
		private List<RegReplace> ReadLineReplacements = new List<RegReplace>();

		private string ProcessReadRegEx(string lineBeingRead)
		{
			if (read_regex != ActiveSliceSettings.Instance.GetValue(SettingsKey.read_regex))
			{
				ReadLineReplacements.Clear();
				string splitString = "\\n";
				read_regex = ActiveSliceSettings.Instance.GetValue(SettingsKey.read_regex);
				foreach (string regExLine in read_regex.Split(splitString.ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
				{
					var matches = getQuotedParts.Matches(regExLine);
					if (matches.Count == 2)
					{
						var search = matches[0].Value.Substring(1, matches[0].Value.Length - 2);
						var replace = matches[1].Value.Substring(1, matches[1].Value.Length - 2);
						ReadLineReplacements.Add(new RegReplace()
						{
							Regex = new Regex(search, RegexOptions.Compiled),
							Replacement = replace
						});
					}
				}
			}

			foreach(var item in ReadLineReplacements)
			{
				lineBeingRead = item.Regex.Replace(lineBeingRead, item.Replacement);
			}

			return lineBeingRead;
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
			SendLineToPrinterNow("G90");
		}

		public void SetMovementToRelative()
		{
			SendLineToPrinterNow("G91");
		}

		public void SetTargetExtruderTemperature(int extruderIndex0Based, double temperature, bool forceSend = false)
		{
			extruderIndex0Based = Math.Min(extruderIndex0Based, MAX_EXTRUDERS - 1);

			if (targetExtruderTemperature[extruderIndex0Based] != temperature
				|| forceSend)
			{
				targetExtruderTemperature[extruderIndex0Based] = temperature;
				OnExtruderTemperatureSet(new TemperatureEventArgs(extruderIndex0Based, temperature));
				if (PrinterIsConnected)
				{
					SendLineToPrinterNow("M104 T{0} S{1}".FormatWith(extruderIndex0Based, targetExtruderTemperature[extruderIndex0Based]));
				}
			}
		}

		public async void StartPrint(string gcodeFilename, PrintTask printTaskToUse = null)
		{
			if (!PrinterIsConnected || PrinterIsPrinting)
			{
				return;
			}

			haveReportedError = false;
			PrintWasCanceled = false;

			waitingForPosition.Reset();
			PositionReadQueued = false;

			ClearQueuedGCode();
			activePrintTask = printTaskToUse;

			await Task.Run(() =>
			{
				LoadGCodeToPrint(gcodeFilename);
			});
			DoneLoadingGCodeToPrint();
		}

		public bool StartSdCardPrint()
		{
			if (!PrinterIsConnected
				|| PrinterIsPrinting
				|| ActivePrintItem.PrintItem.FileLocation != QueueData.SdCardFileName)
			{
				return false;
			}

			currentSdBytes = 0;

			ClearQueuedGCode();
			CommunicationState = CommunicationStates.PrintingFromSd;

			SendLineToPrinterNow("M23 {0}".FormatWith(ActivePrintItem.PrintItem.Name.ToLower())); // Select SD File
			SendLineToPrinterNow("M24"); // Start/resume SD print

			ReadLineStartCallBacks.AddCallbackToKey("Done printing file", DonePrintingSdFile);

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
					{
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
					}
					break;

				case CommunicationStates.AttemptingToConnect:
					CommunicationState = CommunicationStates.FailedToConnect;
					connectThread.Join(JoinThreadTimeoutMs);
					CommunicationState = CommunicationStates.Disconnecting;
					ReadThread.Join();
					if (serialPort != null)
					{
						serialPort.Close();
						serialPort.Dispose();
						serialPort = null;
					}
					CommunicationState = CommunicationStates.Disconnected;
					break;

				case CommunicationStates.PreparingToPrint:
					SlicingQueue.Instance.CancelCurrentSlicing();
					CommunicationState = CommunicationStates.Connected;
					break;
			}
		}

		private void CancelPrint(bool markPrintCanceled)
		{
			lock (locker)
			{
				// get rid of all the gcode we have left to print
				ClearQueuedGCode();
				string cancelGCode = ActiveSliceSettings.Instance.GetValue(SettingsKey.cancel_gcode);
				if (cancelGCode.Trim() != "")
				{
					// add any gcode we want to print while canceling
					InjectGCode(cancelGCode);
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
				SendLineToPrinterNow("M25"); // : Pause SD print
				SendLineToPrinterNow("M26"); // : Set SD position
											 // never leave the extruder and the bed hot
				DonePrintingSdFile(this, null);
			}
		}

		public void SuppressEcho(object sender, EventArgs e)
		{
			FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;
			foundStringEventArgs.SendToDelegateFunctions = false;
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		internal static extern SafeFileHandle CreateFile(string lpFileName, int dwDesiredAccess, int dwShareMode, IntPtr securityAttrs, int dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

		private void AttemptToConnect(string serialPortName, int baudRate)
		{
			// make sure we don't have a left over print task
			activePrintTask = null;

			connectionFailureMessage = "Unknown Reason".Localize();

			if (PrinterIsConnected)
			{
#if DEBUG
				throw new Exception("You can only connect when not currently connected.".Localize());
#else
				return;
#endif
			}

			var portFactory = FrostedSerialPortFactory.GetAppropriateFactory(this.DriverType);

			bool serialPortIsAvailable = SerialPortIsAvailable(serialPortName);
			bool serialPortIsAlreadyOpen = portFactory.SerialPortAlreadyOpen(serialPortName);

			if (serialPortIsAvailable && !serialPortIsAlreadyOpen)
			{
				if (!PrinterIsConnected)
				{
					try
					{
						serialPort = portFactory.CreateAndOpen(serialPortName, baudRate, true);
#if __ANDROID__
						ToggleHighLowHigh(serialPort);
#endif
						// wait a bit of time to let the firmware start up
						Thread.Sleep(500);
						CommunicationState = CommunicationStates.AttemptingToConnect;

						ReadThread.Join();

						Console.WriteLine("ReadFromPrinter thread created.");
						ReadThread.Start();

						CreateStreamProcessors(null, false);

						// We have to send a line because some printers (like old print-r-bots) do not send anything when connecting and there is no other way to know they are there.
						SendLineToPrinterNow("M110 N1");
						ClearQueuedGCode();
						// We do not need to wait for the M105
						PrintingCanContinue(null, null);
					}
					catch (System.ArgumentOutOfRangeException e)
					{
						PrinterOutputCache.Instance.WriteLine("Exception:" + e.Message);
						connectionFailureMessage = "Unsupported Baud Rate".Localize();
						OnConnectionFailed(null);
					}
					catch (Exception ex)
					{
						PrinterOutputCache.Instance.WriteLine("Exception:" + ex.Message);
						OnConnectionFailed(null);
					}
				}
			}
			else
			{
				// If the serial port isn't available (i.e. the specified port name wasn't found in GetPortNames()) or the serial
				// port is already opened in another instance or process, then report the connection problem back to the user
				connectionFailureMessage = (serialPortIsAlreadyOpen ?
					string.Format("{0} in use", this.ComPort) :
					"Port not found".Localize());

				OnConnectionFailed(null);
			}
		}

		private void ClearQueuedGCode()
		{
			loadedGCode.Clear();
			WriteChecksumLineToPrinter("M110 N1");
		}

		private void Connect_Thread()
		{
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
			// Allow the user to set the appropriate properties.
			var portNames = FrostedSerialPort.GetPortNames();
			//Debug.WriteLine("Open ports: {0}".FormatWith(portNames.Length));
			if (portNames.Length > 0 || IsNetworkPrinting())
			{
				AttemptToConnect(this.ComPort, this.BaudRate);
				if (CommunicationState == CommunicationStates.FailedToConnect)
				{
					OnConnectionFailed(null);
				}
			}
			else
			{
				OnConnectionFailed(null);
			}
		}

		private void ConnectionCallbackTimer(object state)
		{
			Timer t = (Timer)state;
			if (!ContinueConnectionThread())
			{
				t.Dispose();
			}
			else
			{
				t.Change(100, 0);
			}
		}

		private bool ContinueConnectionThread()
		{
			if (CommunicationState == CommunicationStates.AttemptingToConnect)
			{
				if (this.stopTryingToConnect)
				{
					connectThread.Join(JoinThreadTimeoutMs); //Halt connection thread
					Disable();
					connectionFailureMessage = "Canceled".Localize();
					OnConnectionFailed(null);
					return false;
				}
				else
				{
					return true;
				}
			}
			else
			{
				// If we're no longer in the .AttemptingToConnect state, shutdown the connection thread and fire the
				// OnConnectonSuccess event if we're connected and not Disconnecting
				connectThread.Join(JoinThreadTimeoutMs);

				return false;
			}
		}

		private void DonePrintingSdFile(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				ReadLineStartCallBacks.RemoveCallbackFromKey("Done printing file", DonePrintingSdFile);
			});
			CommunicationState = CommunicationStates.FinishedPrint;

			this.PrintJobName = null;

			// never leave the extruder and the bed hot
			TurnOffBedAndExtruders();

			ReleaseMotors();
		}

		private void ExtruderWasSetToAbsoluteMode(object sender, EventArgs e)
		{
			extruderMode = PrinterMachineInstruction.MovementTypes.Absolute;
		}

		private void ExtruderWasSetToRelativeMode(object sender, EventArgs e)
		{
			extruderMode = PrinterMachineInstruction.MovementTypes.Relative;
		}

		private void FileDeleteConfirmed(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				ReadLineStartCallBacks.RemoveCallbackFromKey("File deleted:", FileDeleteConfirmed);
			});
			PrintingCanContinue(this, null);
		}

		private void InjectGCode(string codeToInject, bool forceTopOfQueue = false)
		{
			codeToInject = codeToInject.Replace("\\n", "\n");
			string[] lines = codeToInject.Split('\n');

			for (int i = 0; i < lines.Length; i++)
			{
				queuedCommandStream2?.Add(lines[i], forceTopOfQueue);
			}
		}

		private void KeepTrackOfAbsolutePostionAndDestination(string lineBeingSent)
		{
			if (lineBeingSent.StartsWith("G0 ")
				|| lineBeingSent.StartsWith("G1 ")
				|| lineBeingSent.StartsWith("G2 ")
				|| lineBeingSent.StartsWith("G3 "))
			{
				PrinterMove newDestination = currentDestination;
				if (movementMode == PrinterMachineInstruction.MovementTypes.Relative)
				{
					newDestination.position = Vector3.Zero;
				}

				GCodeFile.GetFirstNumberAfter("X", lineBeingSent, ref newDestination.position.x);
				GCodeFile.GetFirstNumberAfter("Y", lineBeingSent, ref newDestination.position.y);
				GCodeFile.GetFirstNumberAfter("Z", lineBeingSent, ref newDestination.position.z);

				GCodeFile.GetFirstNumberAfter("E", lineBeingSent, ref newDestination.extrusion);
				GCodeFile.GetFirstNumberAfter("F", lineBeingSent, ref newDestination.feedRate);

				if (movementMode == PrinterMachineInstruction.MovementTypes.Relative)
				{
					newDestination.position += currentDestination.position;
				}

				if (currentDestination.position != newDestination.position)
				{
					currentDestination = newDestination;
					DestinationChanged.CallEvents(this, null);
				}
			}
		}

		private void CreateStreamProcessors(string gcodeFilename, bool recoveryEnabled)
		{
			totalGCodeStream?.Dispose();

			GCodeStream firstStream = null;
			if (gcodeFilename != null)
			{
				loadedGCode = GCodeFile.Load(gcodeFilename);
				gCodeFileStream0 = new GCodeFileStream(loadedGCode);

				if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.recover_is_enabled)
					&& activePrintTask != null) // We are resuming a failed print (do lots of interesting stuff).
				{
					pauseHandlingStream1 = new PauseHandlingStream(new PrintRecoveryStream(gCodeFileStream0, activePrintTask.PercentDone));
					// And increment the recovery count
					activePrintTask.RecoveryCount++;
					activePrintTask.Commit();
				}
				else
				{
					pauseHandlingStream1 = new PauseHandlingStream(gCodeFileStream0);
				}

				firstStream = pauseHandlingStream1;
			}
			else
			{
				firstStream = new NotPrintingStream();
			}

			queuedCommandStream2 = new QueuedCommandsStream(firstStream);
			relativeToAbsoluteStream3 = new RelativeToAbsoluteStream(queuedCommandStream2);
			printLevelingStream4 = new PrintLevelingStream(relativeToAbsoluteStream3, true);
			waitForTempStream5 = new WaitForTempStream(printLevelingStream4);
			babyStepsStream6 = new BabyStepsStream(waitForTempStream5);
			if (activePrintTask != null)
			{
				// make sure we are in the position we were when we stopped printing
				babyStepsStream6.Offset = new Vector3(activePrintTask.PrintingOffsetX, activePrintTask.PrintingOffsetY, activePrintTask.PrintingOffsetZ);
			}
			extrusionMultiplyerStream7 = new ExtrusionMultiplyerStream(babyStepsStream6);
			feedrateMultiplyerStream8 = new FeedRateMultiplyerStream(extrusionMultiplyerStream7);
			requestTemperaturesStream9 = new RequestTemperaturesStream(feedrateMultiplyerStream8);
			processWriteRegExStream10 = new ProcessWriteRegexStream(requestTemperaturesStream9, queuedCommandStream2);
			totalGCodeStream = processWriteRegExStream10;

			// Get the current position of the printer any time we reset our streams
			ReadPosition();
		}

		private void LoadGCodeToPrint(string gcodeFilename)
		{
			CreateStreamProcessors(gcodeFilename, ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.recover_is_enabled));
		}

		private void DoneLoadingGCodeToPrint()
		{
			switch (communicationState)
			{
				case CommunicationStates.Connected:
					// This can happen if the printer is reset during the slicing of the part.
					break;

				case CommunicationStates.PreparingToPrint:
					if (ActivePrintItem.PrintItem.Id == 0)
					{
						ActivePrintItem.PrintItem.Commit();
					}

					if (activePrintTask == null)
					{
						// TODO: Fix printerItemID int requirement
						activePrintTask = new PrintTask();
						activePrintTask.PrintStart = DateTime.Now;
						activePrintTask.PrinterId = this.ActivePrinter.ID.GetHashCode();
						activePrintTask.PrintName = ActivePrintItem.PrintItem.Name;
						activePrintTask.PrintItemId = ActivePrintItem.PrintItem.Id;
						activePrintTask.PrintingGCodeFileName = ActivePrintItem.GetGCodePathAndFileName();
						activePrintTask.PrintComplete = false;

						activePrintTask.Commit();
					}

					CommunicationState = CommunicationStates.Printing;
					break;

				default:
#if DEBUG
					throw new Exception("We are not preparing to print so we should not be starting to print");
					//#else
					CommunicationState = CommunicationStates.Connected;
#endif
					break;
			}
		}

		private void MovementWasSetToAbsoluteMode(object sender, EventArgs e)
		{
			movementMode = PrinterMachineInstruction.MovementTypes.Absolute;
		}

		private void MovementWasSetToRelativeMode(object sender, EventArgs e)
		{
			movementMode = PrinterMachineInstruction.MovementTypes.Relative;
		}

		private void AtxPowerUpWasWritenToPrinter(object sender, EventArgs e)
		{
			OnAtxPowerStateChanged(true);
		}

		private void AtxPowerDownWasWritenToPrinter(object sender, EventArgs e)
		{
			OnAtxPowerStateChanged(false);
		}

		private void OnActivePrintItemChanged(EventArgs e)
		{
			ActivePrintItemChanged.CallEvents(this, e);
		}

		private void OnBedTemperatureRead(EventArgs e)
		{
			BedTemperatureRead.CallEvents(this, e);
		}

		private void OnBedTemperatureSet(EventArgs e)
		{
			BedTemperatureSet.CallEvents(this, e);
		}

		private void onConfirmPrint(bool messageBoxResponse)
		{
			if (messageBoxResponse)
			{
				CommunicationState = PrinterConnectionAndCommunication.CommunicationStates.PreparingToPrint;
				PrintItemWrapper partToPrint = ActivePrintItem;
				SlicingQueue.Instance.QueuePartForSlicing(partToPrint);
				partToPrint.SlicingDone += partToPrint_SliceDone;
			}
		}

		private void OnEnabledChanged(EventArgs e)
		{
			EnableChanged.CallEvents(this, e);
		}

		private void OnExtruderTemperatureRead(EventArgs e)
		{
			ExtruderTemperatureRead.CallEvents(this, e);
		}

		private void OnExtruderTemperatureSet(EventArgs e)
		{
			ExtruderTemperatureSet.CallEvents(this, e);
		}

		private void OnFanSpeedSet(EventArgs e)
		{
			FanSpeedSet.CallEvents(this, e);
		}

		private void OnFirmwareVersionRead(EventArgs e)
		{
			FirmwareVersionRead.CallEvents(this, e);
		}

		private void onRemoveMessageConfirm(bool messageBoxResponse)
		{
			if (messageBoxResponse)
			{
				QueueData.Instance.RemoveAt(QueueData.Instance.SelectedIndex);
			}
		}

		private bool IsNetworkPrinting()
		{
			return ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.enable_network_printing);
		}

		private void OnAtxPowerStateChanged(bool enableAtxPower)
		{
			atxPowerIsOn = enableAtxPower;
			AtxPowerStateChanged.CallEvents(this, null);
		}

		private void partToPrint_SliceDone(object sender, EventArgs e)
		{
			PrintItemWrapper partToPrint = sender as PrintItemWrapper;
			if (partToPrint != null)
			{
				partToPrint.SlicingDone -= partToPrint_SliceDone;
				string gcodePathAndFileName = partToPrint.GetGCodePathAndFileName();
				if (gcodePathAndFileName != "")
				{
					bool originalIsGCode = Path.GetExtension(partToPrint.FileLocation).ToUpper() == ".GCODE";
					if (File.Exists(gcodePathAndFileName))
					{
						// read the last few k of the file and see if it says "filament used". We use this marker to tell if the file finished writing
						if (originalIsGCode)
						{
							StartPrint(gcodePathAndFileName);
							return;
						}
						else
						{
							int bufferSize = 32000;
							using (Stream fileStream = new FileStream(gcodePathAndFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
							{
								byte[] buffer = new byte[bufferSize];
								fileStream.Seek(Math.Max(0, fileStream.Length - bufferSize), SeekOrigin.Begin);
								int numBytesRead = fileStream.Read(buffer, 0, bufferSize);
								fileStream.Close();

								string fileEnd = System.Text.Encoding.UTF8.GetString(buffer);
								if (fileEnd.Contains("filament used"))
								{
									if (firmwareUriGcodeSend)
									{
										currentSdBytes = 0;

										ClearQueuedGCode();

										SendLineToPrinterNow("M23 {0}".FormatWith(gcodePathAndFileName)); // Send the SD File
										SendLineToPrinterNow("M24"); // Start/resume SD print

										CommunicationState = CommunicationStates.PrintingFromSd;

										ReadLineStartCallBacks.AddCallbackToKey("Done printing file", DonePrintingSdFile);
									}
									else
									{
										StartPrint(gcodePathAndFileName);
									}
									return;
								}
							}
						}
					}

					CommunicationState = CommunicationStates.Connected;
				}
			}
		}

		private void SetDetailedPrintingState(string lineBeingSetToPrinter)
		{
			if (lineBeingSetToPrinter.StartsWith("G28"))
			{
				PrintingState = DetailedPrintingState.HomingAxis;
			}
			else if (waitForTempStream5?.HeatingBed ?? false)
			{
				PrintingState = DetailedPrintingState.HeatingBed;
			}
			else if (waitForTempStream5?.HeatingExtruder ?? false)
			{
				PrintingState = DetailedPrintingState.HeatingExtruder;
			}
			else
			{
				PrintingState = DetailedPrintingState.Printing;
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
							currentLineIndexToSend--; // we are going to resend the last command
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
					WriteRawToPrinter(allCheckSumLinesSent[currentLineIndexToSend++] + "\n", "resend");
				}
				else
				{
					int waitTimeInMs = 60000; // 60 seconds
					if (waitingForPosition.IsRunning
						&& waitingForPosition.ElapsedMilliseconds < waitTimeInMs
						&& PrinterIsConnected)
					{
						// we are waiting for a position response don't print more
						return;
					}

					previousSentLine = this.currentSentLine;
					currentSentLine = totalGCodeStream.ReadLine();

					if (currentSentLine != null)
					{
						if (currentSentLine.Contains("M114")
							&& PrinterIsConnected)
						{
							waitingForPosition.Restart();
						}

						double secondsSinceStartedPrint = timeSinceStartedPrint.Elapsed.TotalSeconds;
						if (secondsSinceUpdateHistory > secondsSinceStartedPrint
							|| secondsSinceUpdateHistory + 1 < secondsSinceStartedPrint)
						{
							double currentDone = loadedGCode.PercentComplete(gCodeFileStream0.LineIndex);
							// Only update the amount done if it is greater than what is recorded.
							// We don't want to mess up the resume before we actually resume it.
							if (activePrintTask != null
								&& babyStepsStream6 != null
								&& activePrintTask.PercentDone < currentDone)
							{
								activePrintTask.PercentDone = currentDone;
								activePrintTask.PrintingOffsetX = (float)babyStepsStream6.Offset.x;
								activePrintTask.PrintingOffsetY = (float)babyStepsStream6.Offset.y;
								activePrintTask.PrintingOffsetZ = (float)babyStepsStream6.Offset.z;
								try
								{
									Task.Run(() => activePrintTask.Commit());
								}
								catch
								{
									// Can't write for some reason, continue with the write.
								}
							}
							secondsSinceUpdateHistory = secondsSinceStartedPrint;
						}

						// Check if there is anything in front of the ;.
						currentSentLine = currentSentLine.Trim();
						if (currentSentLine.Split(';')[0].Trim().Length > 0)
						{
							if (currentSentLine.Length > 0)
							{
								WriteChecksumLineToPrinter(currentSentLine);

								currentLineIndexToSend++;
							}
						}
					}
					else if (this.PrintWasCanceled)
					{
						CommunicationState = CommunicationStates.Connected;
						// never leave the extruder and the bed hot
						ReleaseMotors();
						TurnOffBedAndExtruders();
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
						TurnOffBedAndExtruders();
					}
				}
			}
		}

		private void TurnOffBedAndExtruders()
		{
			for (int i = 0; i < ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count); i++)
			{
				SetTargetExtruderTemperature(i, 0, true);
			}
			TargetBedTemperature = 0;
		}

		// this is to make it misbehave, chaos monkey, bad checksum
		//int checkSumCount = 1;
		private void WriteChecksumLineToPrinter(string lineToWrite)
		{
			SetDetailedPrintingState(lineToWrite);

			// remove the comment if any
			lineToWrite = RemoveCommentIfAny(lineToWrite);

			KeepTrackOfAbsolutePostionAndDestination(lineToWrite);

			// always send the reset line number without a checksum so that it is accepted
			string lineWithCount;
			if (lineToWrite.StartsWith("M110"))
			{
				lineWithCount = $"N1 {lineToWrite}";
				GCodeFile.GetFirstNumberAfter("N", lineToWrite, ref currentLineIndexToSend);
				allCheckSumLinesSent.SetStartingIndex(currentLineIndexToSend);
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

			WriteRawToPrinter(lineWithChecksum + "\n", lineToWrite);
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

		private void WriteRawToPrinter(string lineToWrite, string lineWithoutChecksum)
		{
			if (PrinterIsConnected || CommunicationState == CommunicationStates.AttemptingToConnect)
			{
				if (serialPort != null && serialPort.IsOpen)
				{
					FoundStringEventArgs foundStringEvent = new FoundStringEventArgs(lineWithoutChecksum);

					// If we get a home command, ask the printer where it is after sending it.
					if (lineWithoutChecksum.StartsWith("G28") // is a home
						|| lineWithoutChecksum.StartsWith("G29") // is a bed level
						|| lineWithoutChecksum.StartsWith("G30") // is a bed level
						|| lineWithoutChecksum.StartsWith("G92") // is a reset of printer position
						|| (lineWithoutChecksum.StartsWith("T") && !lineWithoutChecksum.StartsWith("T:"))) // is a switch extruder (verify this is the right time to ask this)
					{
						ReadPosition(true);
					}

					// write data to communication
					{
						StringEventArgs currentEvent = new StringEventArgs(lineToWrite);
						if (PrinterIsPrinting)
						{
							string lineWidthoutCR = lineToWrite.TrimEnd();
							CommunicationUnconditionalToPrinter.CallEvents(this, new StringEventArgs("{0} [{1:0.000}]\n".FormatWith(lineWidthoutCR, timeSinceStartedPrint.Elapsed.TotalSeconds)));
						}
						else
						{
							CommunicationUnconditionalToPrinter.CallEvents(this, currentEvent);
						}

						if (lineWithoutChecksum != null)
						{ 
							WriteLineStartCallBacks.CheckForKeys(foundStringEvent);
							WriteLineContainsCallBacks.CheckForKeys(foundStringEvent);

							if (foundStringEvent.SendToDelegateFunctions)
							{
								WroteLine.CallEvents(this, currentEvent);
							}
						}
					}

					try
					{
						lock (locker)
						{
							serialPort.Write(lineToWrite);
							if (false) // this is for debugging. Eventually it could be hooked up to a user config option so it can be turned on in the field.
							{
								timeSinceRecievedOk.Stop();
								if (!haveHookedDrawing)
								{
									sendTimeAfterOkGraph = new DataViewGraph(150, 150, 0, 30);
									MatterControlApplication.Instance.AddChild(sendTimeAfterOkGraph);
									haveHookedDrawing = true;
								}
								sendTimeAfterOkGraph.AddData("ok->send", timeSinceRecievedOk.ElapsedMilliseconds);
							}
							timeSinceLastWrite.Restart();
							timeHaveBeenWaitingForOK.Restart();
						}
						//Debug.Write("w: " + lineToWrite);
					}
					catch (IOException ex)
					{
						PrinterOutputCache.Instance.WriteLine("Exception:" + ex.Message);

						if (CommunicationState == CommunicationStates.AttemptingToConnect)
						{
							// Handle hardware disconnects by relaying the failure reason and shutting down open resources
							AbortConnectionAttempt("Connection Lost - " + ex.Message);
						}
					}
					catch (TimeoutException e2) // known ok
					{
						// This writes on the next line, and there may have been another write attempt before it is printer. Write indented to attempt to show its association.
						PrinterOutputCache.Instance.WriteLine("        Error writing command:" + e2.Message);
					}
					catch (UnauthorizedAccessException e3)
					{
						PrinterOutputCache.Instance.WriteLine("Exception:" + e3.Message);
						AbortConnectionAttempt(e3.Message);
					}
					catch (Exception)
					{
					}
				}
				else
				{
					OnConnectionFailed(null);
				}
			}
		}

		public void MacroStart()
		{
			queuedCommandStream2?.Reset();
		}

		public void MacroCancel()
		{
			babyStepsStream6?.CancelMoves();
			waitForTempStream5?.Cancel();
			queuedCommandStream2?.Cancel();
		}

		public void MacroContinue()
		{
			queuedCommandStream2?.Continue();
		}

		private bool haveHookedDrawing = false;

		public class ReadThread
		{
			private static int currentReadThreadIndex = 0;
			private int creationIndex;

			private static int numRunning = 0;

			public static int NumRunning
			{
				get
				{
					return numRunning;
				}
			}

			private ReadThread()
			{
				numRunning++;
				currentReadThreadIndex++;
				creationIndex = currentReadThreadIndex;

				Task.Run(() =>
				{
					try
					{
						PrinterConnectionAndCommunication.Instance.ReadFromPrinter(this);
					}
					catch
					{
					}

					PrinterConnectionAndCommunication.Instance.CommunicationUnconditionalToPrinter.CallEvents(this, new StringEventArgs("Read Thread Has Exited.\n"));
					numRunning--;
				});
			}

			internal static void Join()
			{
				currentReadThreadIndex++;
			}

			internal static void Start()
			{
				new ReadThread();
			}

			internal bool IsCurrentThread()
			{
				return currentReadThreadIndex == creationIndex;
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

	public class PrintItemWrapperEventArgs : EventArgs
	{
		public PrintItemWrapperEventArgs(PrintItemWrapper printItemWrapper)
		{
			this.PrintItemWrapper = printItemWrapper;
		}

		public PrintItemWrapper PrintItemWrapper { get; }
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