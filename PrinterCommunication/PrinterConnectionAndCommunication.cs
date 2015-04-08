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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.SerialPortCommunication;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using MatterHackers.VectorMath;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;

namespace MatterHackers.MatterControl.PrinterCommunication
{
    /// <summary>
    /// This is a class to pass temperatures to callbacks that expect them.
    /// A call back can try and cast to this ( TemperatureEventArgs tempArgs = e as TemperatureEventArgs)
    /// and then use the temperature if available.
    /// </summary>
    public class TemperatureEventArgs : EventArgs
    {
        int index0Based;
        double temperature;

        public int Index0Based
        {
            get { return index0Based; }
        }

        public double Temperature
        {
            get { return temperature; }
        }

        public TemperatureEventArgs(int index0Based, double temperature)
        {
            this.index0Based = index0Based;
            this.temperature = temperature;
        }
    }

    public class PrintItemWrapperEventArgs : EventArgs
    {
        PrintItemWrapper printItemWrapper;

        public PrintItemWrapperEventArgs(PrintItemWrapper printItemWrapper)
        {
            this.printItemWrapper = printItemWrapper;
        }

        public PrintItemWrapper PrintItemWrapper
        {
            get { return printItemWrapper; }
        }
    }

    /// <summary>
    /// This is the class that comunicates with a RepRap printer over the serial port.
    /// It handles opening and closing the serial port and does quite a bit of gcode parsing.
    /// It should be refactoried into better moduals at some point.
    /// </summary>
    public class PrinterConnectionAndCommunication
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(string lpFileName, int dwDesiredAccess, int dwShareMode, IntPtr securityAttrs, int dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

        public enum FirmwareTypes { Unknown, Repetier, Marlin, Sprinter };
        FirmwareTypes firmwareType = FirmwareTypes.Unknown;
        
        public FirmwareTypes FirmwareType
        {
            get { return firmwareType; }
        }

        string firmwareVersion;
        public string FirmwareVersion
        {
            get { return firmwareVersion; }
        }

        string deviceCode;
        public string DeviceCode
        {
            get { return deviceCode; }
        }

        static PrinterConnectionAndCommunication globalInstance;
        string connectionFailureMessage = "Unknown Reason";

		Stopwatch waitingForPosition = new Stopwatch();

        public string ConnectionFailureMessage { get { return connectionFailureMessage; } }

        public RootedObjectEventHandler ActivePrintItemChanged = new RootedObjectEventHandler();
        public RootedObjectEventHandler BedTemperatureRead = new RootedObjectEventHandler();
        public RootedObjectEventHandler BedTemperatureSet = new RootedObjectEventHandler();
        public RootedObjectEventHandler CommunicationUnconditionalFromPrinter = new RootedObjectEventHandler();
        public RootedObjectEventHandler CommunicationUnconditionalToPrinter = new RootedObjectEventHandler();
        public RootedObjectEventHandler ConnectionFailed = new RootedObjectEventHandler();
        public RootedObjectEventHandler CommunicationStateChanged = new RootedObjectEventHandler();
        public RootedObjectEventHandler ConnectionSucceeded = new RootedObjectEventHandler();
        public RootedObjectEventHandler DestinationChanged = new RootedObjectEventHandler();
        public RootedObjectEventHandler EnableChanged = new RootedObjectEventHandler();
        public RootedObjectEventHandler ExtruderTemperatureRead = new RootedObjectEventHandler();
        public RootedObjectEventHandler ExtruderTemperatureSet = new RootedObjectEventHandler();
        public RootedObjectEventHandler FanSpeedSet = new RootedObjectEventHandler();
        public RootedObjectEventHandler FirmwareVersionRead = new RootedObjectEventHandler();
        public RootedObjectEventHandler PrintFinished = new RootedObjectEventHandler();
        public RootedObjectEventHandler PositionRead = new RootedObjectEventHandler();
        public RootedObjectEventHandler ReadLine = new RootedObjectEventHandler();
        public RootedObjectEventHandler WroteLine = new RootedObjectEventHandler();
        public RootedObjectEventHandler PrintingStateChanged = new RootedObjectEventHandler();
        public RootedObjectEventHandler PowerStateChanged = new RootedObjectEventHandler();

        FoundStringStartsWithCallbacks ReadLineStartCallBacks = new FoundStringStartsWithCallbacks();
        FoundStringContainsCallbacks ReadLineContainsCallBacks = new FoundStringContainsCallbacks();

        FoundStringStartsWithCallbacks WriteLineStartCallBacks = new FoundStringStartsWithCallbacks();
        FoundStringContainsCallbacks WriteLineContainsCallBacks = new FoundStringContainsCallbacks();

        bool thePowerIsOn = false;
        bool printWasCanceled = false;
		public bool PrintWasCanceled { get { return printWasCanceled; } }
        int firstLineToResendIndex = 0;
        PrintTask activePrintTask;

		class CheckSumLines
		{
			static readonly int RingBufferCount = 16;

			int addedCount = 0;
			string[] ringBuffer = new string[RingBufferCount];

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

			internal void Clear()
			{
				addedCount = 0;
			}

			public int Count { get { return addedCount; } }
		}

		CheckSumLines allCheckSumLinesSent = new CheckSumLines();

        List<string> LinesToWriteQueue = new List<string>();

        Stopwatch timeSinceLastReadAnything = new Stopwatch();
        Stopwatch timeHaveBeenWaitingForOK = new Stopwatch();

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

		CommunicationStates communicationState = CommunicationStates.Disconnected;
		
		CommunicationStates prePauseCommunicationState = CommunicationStates.Printing;

        bool ForceImmediateWrites = false;

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
				}

                if (communicationState != value)
                {
                    switch (communicationState)
                    {
                        // if it was printing
						case CommunicationStates.PrintingFromSd:
                        case CommunicationStates.Printing:
                            {
                                // and is changing to paused
                                if (value == CommunicationStates.Paused)
                                {
									if(communicationState == CommunicationStates.Printing)
									{
										prePauseCommunicationState = CommunicationStates.Printing;
									}
									else
									{
										prePauseCommunicationState = CommunicationStates.PrintingFromSd;
									}
                                    timeSinceStartedPrint.Stop();
                                }
                                else if (value == CommunicationStates.FinishedPrint)
                                {
                                    if (activePrintTask != null)
                                    {
                                        TimeSpan printTimeSpan = DateTime.Now.Subtract(activePrintTask.PrintStart);

                                        activePrintTask.PrintEnd = DateTime.Now;
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
								// If we are just satrting to print (we know we were not pasued or it would have stoped above)
								timeSinceStartedPrint.Restart();
							}
							break;
                    }

                    communicationState = value;
                    OnCommunicationStateChanged(null);
                }
            }
        }

        bool stopTryingToConnect = false;

        const int MAX_EXTRUDERS = 16;
        double[] actualExtruderTemperature = new double[MAX_EXTRUDERS];
        double[] targetExtruderTemperature = new double[MAX_EXTRUDERS];
        double actualBedTemperature;
        double targetBedTemperature;
        string printJobDisplayName = null;
        GCodeFile loadedGCode = new GCodeFileLoaded(); // we start out by setting it to a nothing file
        IFrostedSerialPort serialPort;
		readonly int JoinThreadTimeoutMs = 5000;

		internal class ReadThreadHolder
		{
			readonly int JoinThreadTimeoutMs = 5000;

			static int currentReadThreadIndex = 0;
			int creationIndex;

			internal static void Start(DoWorkEventHandler readFromPrinterFunction)
			{
				new ReadThreadHolder(readFromPrinterFunction);
			}

			ReadThreadHolder(DoWorkEventHandler readFromPrinterFunction)
			{
				currentReadThreadIndex++;
				creationIndex = currentReadThreadIndex;

				BackgroundWorker readFromPrinterWorker = new BackgroundWorker();
				readFromPrinterWorker.DoWork += readFromPrinterFunction;

				readFromPrinterWorker.RunWorkerAsync(this);
			}

			internal bool IsCurrentThread()
			{
				return currentReadThreadIndex == creationIndex;
			}

			internal static void Join()
			{
				currentReadThreadIndex++;
			}
		}

        Thread connectThread;

        private PrintItemWrapper activePrintItem;

        int lastRemainingSecondsReported = 0;
        int printerCommandQueueLineIndex = -1;

        Vector3 currentDestination;
		double currentFeedRate;
		double currentExtruderDestination;
        Vector3 lastReportedPosition;

        public Vector3 CurrentDestination { get { return currentDestination; } }
        public Vector3 LastReportedPosition { get { return lastReportedPosition; } }

        PrinterMachineInstruction.MovementTypes extruderMode = PrinterMachineInstruction.MovementTypes.Absolute;
        PrinterMachineInstruction.MovementTypes movementMode = PrinterMachineInstruction.MovementTypes.Absolute;

        double extrusionRatio = 1;
        double currentActualExtrusionPosition = 0;
        double gcodeRequestedExtrusionPosition = 0;
        double previousGcodeRequestedExtrusionPosition = 0;
        public RootedObjectEventHandler ExtrusionRatioChanged = new RootedObjectEventHandler();
        public double ExtrusionRatio
        {
            get { return extrusionRatio; }
            set
            {
                if (value != extrusionRatio)
                {
                    extrusionRatio = value;
                    ExtrusionRatioChanged.CallEvents(this, null);
                }
            }
        }
        double feedRateRatio = 1;
        public RootedObjectEventHandler FeedRateRatioChanged = new RootedObjectEventHandler();
        public double FeedRateRatio
        {
            get { return feedRateRatio; }
            set
            {
                if (value != feedRateRatio)
                {
                    feedRateRatio = value;
                    FeedRateRatioChanged.CallEvents(this, null);
                }
            }
        }

        private Printer ActivePrinter
        {
            get
            {
                return ActivePrinterProfile.Instance.ActivePrinter;
            }
            set
            {
                ActivePrinterProfile.Instance.ActivePrinter = value;
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
                if (!PrinterConnectionAndCommunication.Instance.PrinterIsPrinting)
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

        public bool Disconnecting
        {
            get
            {
                return CommunicationState == CommunicationStates.Disconnecting;
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
                        throw new NotImplementedException("Make sure every satus returns the correct connected state.");
                }
            }
        }

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

        PrinterConnectionAndCommunication()
        {
            MonitorPrinterTemperature = true;

            StringComparer stringComparer = StringComparer.OrdinalIgnoreCase;
            ReadLineStartCallBacks.AddCallBackToKey("start", FoundStart);
            ReadLineStartCallBacks.AddCallBackToKey("start", PrintingCanContinue);

            ReadLineStartCallBacks.AddCallBackToKey("ok", SuppressEcho);
            ReadLineStartCallBacks.AddCallBackToKey("wait", SuppressEcho);
            ReadLineStartCallBacks.AddCallBackToKey("T:", SuppressEcho); // repatier

            ReadLineStartCallBacks.AddCallBackToKey("ok", PrintingCanContinue);
            ReadLineStartCallBacks.AddCallBackToKey("Done saving file", PrintingCanContinue);

            ReadLineStartCallBacks.AddCallBackToKey("ok T:", ReadTemperatures); // marlin
            ReadLineStartCallBacks.AddCallBackToKey("ok T0:", ReadTemperatures); // marlin
            ReadLineStartCallBacks.AddCallBackToKey("T:", ReadTemperatures); // repatier
            ReadLineStartCallBacks.AddCallBackToKey("B:", ReadTemperatures); // smoothie

            ReadLineStartCallBacks.AddCallBackToKey("SD printing byte", ReadSdProgress); // repatier

            ReadLineStartCallBacks.AddCallBackToKey("C:", ReadTargetPositions);
            ReadLineStartCallBacks.AddCallBackToKey("ok C:", ReadTargetPositions); // smoothie is reporting the C: with an ok first.
            ReadLineStartCallBacks.AddCallBackToKey("X:", ReadTargetPositions);

            ReadLineContainsCallBacks.AddCallBackToKey("RS:", PrinterRequestsResend);
            ReadLineContainsCallBacks.AddCallBackToKey("Resend:", PrinterRequestsResend);

			ReadLineContainsCallBacks.AddCallBackToKey("FIRMWARE_NAME:", PrinterStatesFirmware);
			ReadLineStartCallBacks.AddCallBackToKey("EXTENSIONS:", PrinterStatesExtensions);

            WriteLineStartCallBacks.AddCallBackToKey("M104", ExtruderTemperatureWasWritenToPrinter);
            WriteLineStartCallBacks.AddCallBackToKey("M109", ExtruderTemperatureWasWritenToPrinter);
            WriteLineStartCallBacks.AddCallBackToKey("M140", BedTemperatureWasWritenToPrinter);
            WriteLineStartCallBacks.AddCallBackToKey("M190", BedTemperatureWasWritenToPrinter);

            WriteLineStartCallBacks.AddCallBackToKey("M106", FanSpeedWasWritenToPrinter);
            WriteLineStartCallBacks.AddCallBackToKey("M107", FanOffWasWritenToPrinter);

            WriteLineStartCallBacks.AddCallBackToKey("M82", ExtruderWasSetToAbsoluteMode);
            WriteLineStartCallBacks.AddCallBackToKey("M83", ExtruderWasSetToRelativeMode);

            WriteLineStartCallBacks.AddCallBackToKey("G90", MovementWasSetToAbsoluteMode);
            WriteLineStartCallBacks.AddCallBackToKey("G91", MovementWasSetToRelativeMode);
        }

        public bool MonitorPrinterTemperature
        {
            get;
            set;
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

        public enum DetailedPrintingState { HomingAxis, HeatingBed, HeatingExtruder, Printing };
        DetailedPrintingState printingStatePrivate;

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
                        return "Homing Axis";

                    case DetailedPrintingState.HeatingBed:
                        return "Waiting for Bed to Heat to {0}°".FormatWith(TargetBedTemperature);

                    case DetailedPrintingState.HeatingExtruder:
                        return "Waiting for Extruder to Heat to {0}°".FormatWith(GetTargetExtruderTemperature(0));

                    case DetailedPrintingState.Printing:
                        return "Currently Printing:";

                    default:
                        return "";
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
                        string connectingMessageTxt = "Connecting".Localize();
                        return "{0}...".FormatWith(connectingMessageTxt);
                    case CommunicationStates.ConnectionLost:
                        return "Connection Lost".Localize();
                    case CommunicationStates.FailedToConnect:
                        return "Unable to Connect";
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
                        throw new NotImplementedException("Make sure every satus returns the correct connected state.");
                }
            }
        }

        public string PrintJobName
        {
            get
            {
                return printJobDisplayName;
            }
        }

        public bool PrintIsFinished
        {
            get
            {
                return CommunicationState == CommunicationStates.FinishedPrint;
            }
        }

        public bool PrinterIsPaused
        {
            get
            {
                return CommunicationState == CommunicationStates.Paused;
            }
        }        

        int NumberOfLinesInCurrentPrint
        {
            get
            {
				return loadedGCode.LineCount;
            }
        }

        public int TotalSecondsInPrint
        {
            get
            {
				if (loadedGCode.LineCount > 0)
                {
                    if (FeedRateRatio != 0)
                    {
						return (int)(loadedGCode.TotalSecondsInPrint / FeedRateRatio);
                    }

					return (int)(loadedGCode.TotalSecondsInPrint);
                }

                return 0;
            }
        }

        int backupAmount = 16;
        public int CurrentlyPrintingLayer
        {
            get
            {
                int instructionIndex = printerCommandQueueLineIndex - backupAmount;
				return loadedGCode.GetLayerIndex(instructionIndex);
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
                catch
                {
                    return -1;
                }
            }
        }

        public double RatioIntoCurrentLayer
        {
            get
            {
				int instructionIndex = printerCommandQueueLineIndex - backupAmount;
				return loadedGCode.Ratio0to1IntoContainedLayer(instructionIndex);
            }
        }

        Stopwatch timeSinceStartedPrint = new Stopwatch();
        public int SecondsRemaining
        {
            get
            {
                if (NumberOfLinesInCurrentPrint > 0)
                {
                    if (printerCommandQueueLineIndex >= 0
						&& printerCommandQueueLineIndex < loadedGCode.LineCount
                        && loadedGCode.Instruction(printerCommandQueueLineIndex).secondsToEndFromHere != 0)
                    {
                        if (FeedRateRatio != 0)
                        {
                            lastRemainingSecondsReported = (int)(loadedGCode.Instruction(printerCommandQueueLineIndex).secondsToEndFromHere / FeedRateRatio);
                        }
                    }

                    return lastRemainingSecondsReported;
                }

                return 0;
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

        public double PercentComplete
        {
            get
            {
                if (CommunicationState == CommunicationStates.PrintingFromSd
					|| (communicationState == CommunicationStates.Paused && prePauseCommunicationState == CommunicationStates.PrintingFromSd))
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
					&& loadedGCode != null)
                {
					return loadedGCode.PercentComplete(printerCommandQueueLineIndex);
                }
                else
                {
                    return 0.0;
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

        Stopwatch timeWaitingForTemperature = new Stopwatch();
        Stopwatch timeWaitingForSdProgress = new Stopwatch();
        System.Diagnostics.Stopwatch temperatureRequestTimer = new System.Diagnostics.Stopwatch();
        public void OnIdle()
        {
            if (!temperatureRequestTimer.IsRunning)
            {
                temperatureRequestTimer.Start();
            }
            if (temperatureRequestTimer.ElapsedMilliseconds > 2000)
            {
                if (MonitorPrinterTemperature
                    && (!timeWaitingForTemperature.IsRunning || timeWaitingForTemperature.Elapsed.TotalSeconds > 60))
                {
                    timeWaitingForTemperature.Restart();
                    SendLineToPrinterNow("M105");
                }
                
                if (CommunicationState == CommunicationStates.PrintingFromSd
                    && (!timeWaitingForSdProgress.IsRunning || timeWaitingForSdProgress.Elapsed.TotalSeconds > 10))
                {
                    timeWaitingForSdProgress.Restart();
                    SendLineToPrinterNow("M27"); // : Report SD print status
                }

                temperatureRequestTimer.Restart();
            }

            bool waited30SeconsdForOk = timeHaveBeenWaitingForOK.Elapsed.Seconds > 30; // waited for more than 30 seconds
            bool noResponseFor5Seconds = timeSinceLastReadAnything.Elapsed.Seconds > 5;
            bool waitedToLongForOK = waited30SeconsdForOk && noResponseFor5Seconds;
            while (LinesToWriteQueue.Count > 0 &&
                (!timeHaveBeenWaitingForOK.IsRunning || waitedToLongForOK))
            {
                WriteNextLineFromQueue();
            }
        }

        string doNotShowAgainMessage = "Do not show this message again".Localize();
        string gcodeWarningMessage = "The file you are attempting to print is a GCode file.\n\nIt is recommendended that you only print Gcode files known to match your printer's configuration.\n\nAre you sure you want to print this GCode file?".Localize();
        string removeFromQueueMessage = "Cannot find this file\nWould you like to remove it from the queue?".Localize();
        string itemNotFoundMessage = "Item not found".Localize();

        event EventHandler unregisterEvents;

        public void PrintActivePartIfPossible()
        {
            if (CommunicationState == CommunicationStates.Connected || CommunicationState == CommunicationStates.FinishedPrint)
            {
                PrintActivePart();
            }
        }

        public void PrintActivePart()
        {
            PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(ActivePrinterProfile.Instance.ActivePrinter);
            if (levelingData.needsPrintLeveling
                && levelingData.sampledPosition0.z == 0
                && levelingData.sampledPosition1.z == 0
                && levelingData.sampledPosition2.z == 0)
            {
                LevelWizardBase.ShowPrintLevelWizard(LevelWizardBase.RuningState.InitialStartupCalibration);
                return;
            }
            
            string pathAndFile = PrinterConnectionAndCommunication.Instance.ActivePrintItem.FileLocation;
            if (ActiveSliceSettings.Instance.HasSdCardReader()
                && pathAndFile == QueueData.SdCardFileName)
            {
                PrinterConnectionAndCommunication.Instance.StartSdCardPrint();
            }
            else if (ActiveSliceSettings.Instance.IsValid())
            {
                if (File.Exists(pathAndFile))
                {
                    // clear the output cache prior to starting a print
                    PrinterOutputCache.Instance.Clear();

                    string hideGCodeWarning = ApplicationSettings.Instance.get("HideGCodeWarning");

                    if (Path.GetExtension(pathAndFile).ToUpper() == ".GCODE" && hideGCodeWarning == null)
                    {
                        CheckBox hideGCodeWarningCheckBox = new CheckBox(doNotShowAgainMessage);
                        hideGCodeWarningCheckBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                        hideGCodeWarningCheckBox.Margin = new BorderDouble(top: 6, left: 6);
                        hideGCodeWarningCheckBox.HAnchor = Agg.UI.HAnchor.ParentLeft;
                        hideGCodeWarningCheckBox.Click += (sender, e) =>
                        {
                            if (hideGCodeWarningCheckBox.Checked)
                            {
                                ApplicationSettings.Instance.set("HideGCodeWarning", "true");
                            }
                            else
                            {
                                ApplicationSettings.Instance.set("HideGCodeWarning", null);
                            }
                        };
                        StyledMessageBox.ShowMessageBox(onConfirmPrint, gcodeWarningMessage, "Warning - GCode file".Localize(), new GuiWidget[] { new VerticalSpacer(), hideGCodeWarningCheckBox }, StyledMessageBox.MessageType.YES_NO);
                    }
                    else
                    {
                        PrinterConnectionAndCommunication.Instance.CommunicationState = PrinterConnectionAndCommunication.CommunicationStates.PreparingToPrint;
                        PrintItemWrapper partToPrint = PrinterConnectionAndCommunication.Instance.ActivePrintItem;
                        SlicingQueue.Instance.QueuePartForSlicing(partToPrint);
                        partToPrint.SlicingDone.RegisterEvent(partToPrint_SliceDone, ref unregisterEvents);
                    }
                }
                else
                {
                    string message = String.Format(removeFromQueueMessage, pathAndFile);
                    StyledMessageBox.ShowMessageBox(onRemoveMessageConfirm, message, itemNotFoundMessage, StyledMessageBox.MessageType.YES_NO);
                }
            }
        }

        void onConfirmPrint(bool messageBoxResponse)
        {
            if (messageBoxResponse)
            {
                PrinterConnectionAndCommunication.Instance.CommunicationState = PrinterConnectionAndCommunication.CommunicationStates.PreparingToPrint;
                PrintItemWrapper partToPrint = PrinterConnectionAndCommunication.Instance.ActivePrintItem;
                SlicingQueue.Instance.QueuePartForSlicing(partToPrint);
                partToPrint.SlicingDone.RegisterEvent(partToPrint_SliceDone, ref unregisterEvents);
            }
        }

        
        void onRemoveMessageConfirm(bool messageBoxResponse)
        {
            if (messageBoxResponse)
            {
                QueueData.Instance.RemoveAt(QueueData.Instance.SelectedIndex);
            }
        }

        void partToPrint_SliceDone(object sender, EventArgs e)
        {
            PrintItemWrapper partToPrint = sender as PrintItemWrapper;
            if (partToPrint != null)
            {
                partToPrint.SlicingDone.UnregisterEvent(partToPrint_SliceDone, ref unregisterEvents);
                string gcodePathAndFileName = partToPrint.GetGCodePathAndFileName();
                if (gcodePathAndFileName != "")
                {
					bool originalIsGCode = Path.GetExtension(partToPrint.FileLocation).ToUpper() == ".GCODE";
					if (File.Exists(gcodePathAndFileName))
					{
						// read the last few k of the file nad see if it says "filament used". We use this marker to tell if the file finished writing
						if (originalIsGCode)
						{
							PrinterConnectionAndCommunication.Instance.StartPrint(gcodePathAndFileName);
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

										ReadLineStartCallBacks.AddCallBackToKey("Done printing file", DonePrintingSdFile);
									}
									else
									{
										PrinterConnectionAndCommunication.Instance.StartPrint(gcodePathAndFileName);
									}
									return;
								}
							}
						}
					}

					PrinterConnectionAndCommunication.Instance.CommunicationState = PrinterConnectionAndCommunication.CommunicationStates.Connected;
                }
            }
        }

        private void WriteNextLineFromQueue()
        {
            string lineToWrite = LinesToWriteQueue[0];

            using (TimedLock.Lock(this, "WriteNextLineFromQueue"))
            {
                lineToWrite = KeepTrackOfPostionAndDestination(lineToWrite);
                lineToWrite = RunPrintLevelingTranslations(lineToWrite);

                LinesToWriteQueue.RemoveAt(0); // remove the line first (in case we inject another command)
                WriteToPrinter(lineToWrite + "\r\n", lineToWrite);
            }
            System.Threading.Thread.Sleep(1);
        }

        public double GetTargetExtruderTemperature(int extruderIndex0Based)
        {
            extruderIndex0Based = Math.Min(extruderIndex0Based, MAX_EXTRUDERS - 1);

            return targetExtruderTemperature[extruderIndex0Based];
        }

        public void SetTargetExtruderTemperature(int extruderIndex0Based, double temperature)
        {
            extruderIndex0Based = Math.Min(extruderIndex0Based, MAX_EXTRUDERS - 1);

            if (targetExtruderTemperature[extruderIndex0Based] != temperature)
            {
                targetExtruderTemperature[extruderIndex0Based] = temperature;
                OnExtruderTemperatureSet(new TemperatureEventArgs(extruderIndex0Based, temperature));
                if (PrinterIsConnected)
                {
                    SendLineToPrinterNow("M104 T{0} S{1}".FormatWith(extruderIndex0Based, targetExtruderTemperature[extruderIndex0Based]));
                }
            }
        }

        public double GetActualExtruderTemperature(int extruderIndex0Based)
        {
            extruderIndex0Based = Math.Min(extruderIndex0Based, MAX_EXTRUDERS - 1);

            return actualExtruderTemperature[extruderIndex0Based];
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
					SetTargetExtruderTemperature(extruderIndex0Based, tempBeingSet);
                }
                else
                {
                    // we set the private variable so that we don't get the callbacks called and get in a loop of setting the temp
					SetTargetExtruderTemperature(0, tempBeingSet);
				}
                OnExtruderTemperatureSet(e);
            }
        }

        int fanSpeed;
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

        public double ActualBedTemperature
        {
            get
            {
                return actualBedTemperature;
            }
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
                catch
                {
                    Debug.WriteLine("Unable to Parse Bed Temperature: {0}".FormatWith(temp));
                }
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
                catch
                {
                    Debug.WriteLine("Unable to Parse Fan Speed: {0}".FormatWith(fanSpeed));
                }
            }
        }

        public void ReadTargetPositions(object sender, EventArgs e)
        {
            FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;

            string lineToParse = foundStringEventArgs.LineToCheck;
            Vector3 positionRead = Vector3.Zero;
            GCodeFile.GetFirstNumberAfter("X:", lineToParse, ref positionRead.x);
            GCodeFile.GetFirstNumberAfter("Y:", lineToParse, ref positionRead.y);
            GCodeFile.GetFirstNumberAfter("Z:", lineToParse, ref positionRead.z);

            // The first position read is the target position.
            lastReportedPosition = positionRead;

#if false
            // The second position (if available) is the actual current position of the extruder.
            int xPosition = lineToParse.IndexOf('X');
            int secondXPosition = lineToParse.IndexOf("Count", xPosition);
            if (secondXPosition != -1)
            {
                Vector3 currentPositionRead = Vector3.Zero;
                GCodeFile.GetFirstNumberAfter("X:", lineToParse, ref currentPositionRead.x, secondXPosition - 1);
                GCodeFile.GetFirstNumberAfter("Y:", lineToParse, ref currentPositionRead.y, secondXPosition - 1);
                GCodeFile.GetFirstNumberAfter("Z:", lineToParse, ref currentPositionRead.z, secondXPosition - 1);

                lastReportedPosition = currentPositionRead;
            }
#endif

            if (currentDestination != positionRead)
            {
                currentDestination = positionRead;
                DestinationChanged.CallEvents(this, null);
            }

            PositionRead.CallEvents(this, null);

			waitingForPosition.Stop();
			waitingForPosition.Reset();
        }

        double currentSdBytes = 0;
        double totalSdBytes = 0;
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

            // We read them so we are no longer waiting
            timeWaitingForTemperature.Stop();
        }

        void MovementWasSetToAbsoluteMode(object sender, EventArgs e)
        {
            movementMode = PrinterMachineInstruction.MovementTypes.Absolute;
        }

        void MovementWasSetToRelativeMode(object sender, EventArgs e)
        {
            movementMode = PrinterMachineInstruction.MovementTypes.Relative;
        }

        void ExtruderWasSetToAbsoluteMode(object sender, EventArgs e)
        {
            extruderMode = PrinterMachineInstruction.MovementTypes.Absolute;
        }

        void ExtruderWasSetToRelativeMode(object sender, EventArgs e)
        {
            extruderMode = PrinterMachineInstruction.MovementTypes.Relative;
        }

        public void PrinterRequestsResend(object sender, EventArgs e)
        {
            FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;

            string[] splitOnColon = foundStringEventArgs.LineToCheck.Split(':');

            firstLineToResendIndex = int.Parse(splitOnColon[1]) - 1;
        }

		bool firmwareUriGcodeSend = false;
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
                    firmwareType = FirmwareTypes.Repetier;
                }
                else if (firmwareName.Contains("marlin"))
                {
                    firmwareType = FirmwareTypes.Marlin;
                }
                else if (firmwareName.Contains("sprinter"))
                {
                    firmwareType = FirmwareTypes.Sprinter;
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
                        deviceCode = split[0];
                        firmwareVersionReported = split[1];
                    }
                }
                
                //Firmware version was detected and is different
                if (firmwareVersionReported != "" && firmwareVersion != firmwareVersionReported)
                {                    
                    firmwareVersion = firmwareVersionReported;
                    OnFirmwareVersionRead(null);
                }
            }
        }

        public void FoundStart(object sender, EventArgs e)
        {
            FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;
            foundStringEventArgs.SendToDelegateFunctions = false;
        }

        public void SuppressEcho(object sender, EventArgs e)
        {
            FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;
            foundStringEventArgs.SendToDelegateFunctions = false;
        }

        void ConnectionCallbackTimer(object state)
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

        bool ContinueConnectionThread()
        {
            if (CommunicationState == CommunicationStates.AttemptingToConnect)
            {
                if (this.stopTryingToConnect)
                {
                    connectThread.Join(JoinThreadTimeoutMs); //Halt connection thread
                    Disable();
                    connectionFailureMessage = LocalizedString.Get("Cancelled");
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

                if(PrinterIsConnected && CommunicationState != CommunicationStates.Disconnecting)
                {
                    OnConnectionSucceeded(null);
                }

                return false;
            }
        }

        public void HaltConnectionThread()
        {
            this.stopTryingToConnect = true;
        }

        public void ConnectToActivePrinter()
        {
            if (PrinterConnectionAndCommunication.Instance.ActivePrinter != null)
            {
                // Start the process of requesting permission and exit if permission is not currently granted
				if (!FrostedSerialPort.EnsureDeviceAccess())
                {
                    CommunicationState = CommunicationStates.FailedToConnect;
                    return;
                }

                ConnectToPrinter(PrinterConnectionAndCommunication.Instance.ActivePrinter);
            }
        }

        public string ComPort
        {
            get
            {
                string comPort = null;
                if (this.ActivePrinter != null)
                {
                    comPort = this.ActivePrinter.ComPort;
                    
                }
                return comPort;
            }
        }

        public bool HasPower {
            get {
                bool hasPower = false;
                if (this.ActivePrinter != null) {
                    hasPower = this.ActivePrinter.HasPower;

                }
                return hasPower;
            }
        }

        public bool PowerisOn 
        {
            get 
            {
                return thePowerIsOn;
            }
            set {
                thePowerIsOn = value;
                PowerStateChanged.CallEvents(this, null);
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
						if (this.ActivePrinter.BaudRate != null)
						{
							baudRate = Convert.ToInt32(this.ActivePrinter.BaudRate);
						}
                    }
                    catch
                    {
                        Console.WriteLine("Unable to convert BaudRate to integer");
                    }
                }
                return baudRate;
            }
        }

        private void ConnectToPrinter(Printer printerRecord)
        {
            PrinterOutputCache.Instance.Clear();
            LinesToWriteQueue.Clear();
            //Attempt connecting to a specific printer
            this.stopTryingToConnect = false;
            firmwareType = FirmwareTypes.Unknown;
            firmwareVersion = null;
			firmwareUriGcodeSend = false;

            // On Android, there will never be more than one serial port available for us to connect to. Override the current .ComPort value to account for
            // this aspect to ensure the validation logic that verifies port availablity/in use status can proceed without additional workarounds for Android
#if __ANDROID__
            string currentPortName = FrostedSerialPort.GetPortNames().FirstOrDefault();

            if(!string.IsNullOrEmpty(currentPortName))
            {
                this.ActivePrinter.ComPort = currentPortName;
            }
#endif

            if (SerialPortIsAvailable(this.ActivePrinter.ComPort))
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
                Debug.WriteLine("Connection failed: {0}".FormatWith(this.ActivePrinter.ComPort));

                connectionFailureMessage = string.Format(
                                    "{0} is not available".Localize(),
                                    this.ActivePrinter.ComPort);

                OnConnectionFailed(null);
            }
        }

        void Connect_Thread()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            // Allow the user to set the appropriate properties.
			var portNames = FrostedSerialPort.GetPortNames();
            //Debug.WriteLine("Open ports: {0}".FormatWith(portNames.Length));
            if (portNames.Length > 0)
            {
                //Debug.WriteLine("Connecting to: {0} {1}".FormatWith(this.ActivePrinter.ComPort, this.BaudRate));
                AttemptToConnect(this.ActivePrinter.ComPort, this.BaudRate);
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

        public void OnConnectionSucceeded(EventArgs e)
        {
            CommunicationState = CommunicationStates.Connected;

            ConnectionSucceeded.CallEvents(this, e);

            OnEnabledChanged(e);
        }

        public void OnCommunicationStateChanged(EventArgs e)
        {
            CommunicationStateChanged.CallEvents(this, e);
        }

        public void OnConnectionFailed(EventArgs e)
        {
            ConnectionFailed.CallEvents(this, e);

            CommunicationState = CommunicationStates.FailedToConnect;
            OnEnabledChanged(e);
        }

        public bool SerialPortIsAvailable(string portName)
        //Check is serial port is in the list of available serial ports
        {
            try
            {
				string[] portNames = FrostedSerialPort.GetPortNames();
                return portNames.Any(x => string.Compare(x, portName, true) == 0);
            }
            catch
            {
                return false;
            }
        }

        void AttemptToConnect(string serialPortName, int baudRate)
        {
            connectionFailureMessage = LocalizedString.Get("Unknown Reason");

            if (PrinterIsConnected)
            {
                throw new Exception(LocalizedString.Get("You can only connect when not currently connected."));
            }

            bool serialPortIsAvailable = SerialPortIsAvailable(serialPortName);
            bool serialPortIsAlreadyOpen = FrostedSerialPortFactory.Instance.SerialPortAlreadyOpen(serialPortName);

            if (serialPortIsAvailable && !serialPortIsAlreadyOpen)
            {
                if (!PrinterIsConnected)
                {
                    try
                    {
						serialPort = FrostedSerialPortFactory.Instance.CreateAndOpen(serialPortName, baudRate, true);
						// wait a bit of time to let the firmware start up
						Thread.Sleep(500);
						CommunicationState = CommunicationStates.AttemptingToConnect;
						
						ReadThreadHolder.Join();

						Console.WriteLine("ReadFromPrinter thread created.");
						ReadThreadHolder.Start(ReadFromPrinter);
					
						// We have to send a line because some printers (like old printrbots) do not send anything when connecting and there is no other way to know they are there.
						SendLineToPrinterNow("M105");
						SendLineToPrinterNow("M115");
					}
                    catch (System.ArgumentOutOfRangeException)
                    {
                        connectionFailureMessage = LocalizedString.Get("Unsupported Baud Rate");
                        OnConnectionFailed(null);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("An unexpected exception occurred: " + ex.Message);
                        OnConnectionFailed(null);
                    }
                }
            }
            else
            {
                // If the serial port isn't avaiable (i.e. the specified port name wasn't found in GetPortNames()) or the serial
                // port is already opened in another instance or process, then report the connection problem back to the user
                connectionFailureMessage = (serialPortIsAlreadyOpen ? 
                    string.Format("{0} in use", PrinterConnectionAndCommunication.Instance.ActivePrinter.ComPort) :
                    LocalizedString.Get("Port not found"));

                OnConnectionFailed(null);
            }
        }

        public void OnPrintFinished(EventArgs e)
        {
            PrintFinished.CallEvents(this, new PrintItemWrapperEventArgs(this.ActivePrintItem));
        }

        void OnFirmwareVersionRead(EventArgs e)
        {
            FirmwareVersionRead.CallEvents(this, e);
        }

        void OnPowerStateChanged(EventArgs e) {
            PowerStateChanged.CallEvents(this, e);
        }
        
        void OnExtruderTemperatureRead(EventArgs e)
        {
            ExtruderTemperatureRead.CallEvents(this, e);
        }

        void OnBedTemperatureRead(EventArgs e)
        {
            BedTemperatureRead.CallEvents(this, e);
        }

        void OnExtruderTemperatureSet(EventArgs e)
        {
            ExtruderTemperatureSet.CallEvents(this, e);
        }

        void OnBedTemperatureSet(EventArgs e)
        {
            BedTemperatureSet.CallEvents(this, e);
        }

        void OnFanSpeedSet(EventArgs e)
        {
            FanSpeedSet.CallEvents(this, e);
        }

        void OnActivePrintItemChanged(EventArgs e)
        {
            ActivePrintItemChanged.CallEvents(this, e);
        }

        void OnEnabledChanged(EventArgs e)
        {
            EnableChanged.CallEvents(this, e);
        }

        string KeepTrackOfPostionAndDestination(string lineBeingSent)
        {
            if (lineBeingSent.StartsWith("G0 ") || lineBeingSent.StartsWith("G1 "))
            {
                Vector3 newDestination = currentDestination;
                if (movementMode == PrinterMachineInstruction.MovementTypes.Relative)
                {
                    newDestination = Vector3.Zero;
                }

                GCodeFile.GetFirstNumberAfter("X", lineBeingSent, ref newDestination.x);
                GCodeFile.GetFirstNumberAfter("Y", lineBeingSent, ref newDestination.y);
                GCodeFile.GetFirstNumberAfter("Z", lineBeingSent, ref newDestination.z);

				GCodeFile.GetFirstNumberAfter("E", lineBeingSent, ref currentExtruderDestination);
				GCodeFile.GetFirstNumberAfter("F", lineBeingSent, ref currentFeedRate);

                if (movementMode == PrinterMachineInstruction.MovementTypes.Relative)
                {
                    newDestination += currentDestination;
                }

                if (currentDestination != newDestination)
                {
                    currentDestination = newDestination;
                    DestinationChanged.CallEvents(this, null);
                }
            }

            return lineBeingSent;
        }

        string RunPrintLevelingTranslations(string lineBeingSent)
        {
            if (ActivePrinter != null 
                && ActivePrinter.DoPrintLeveling 
                && (lineBeingSent.StartsWith("G0 ") || lineBeingSent.StartsWith("G1 ")))
            {
                string inputLine = lineBeingSent;
                lineBeingSent = PrintLevelingPlane.Instance.ApplyLeveling(currentDestination, movementMode, inputLine);
            }

            PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(ActivePrinterProfile.Instance.ActivePrinter);
            if (levelingData != null)
            {
                List<string> linesToWrite = null;
                switch (levelingData.levelingSystem)
                {
                    case PrintLevelingData.LevelingSystem.Probe2Points:
                        linesToWrite = LevelWizard2Point.ProcessCommand(lineBeingSent);
                        break;

                    case PrintLevelingData.LevelingSystem.Probe3Points:
                        linesToWrite = LevelWizard3Point.ProcessCommand(lineBeingSent);
                        break;
                }

                lineBeingSent = linesToWrite[0];
                linesToWrite.RemoveAt(0);

                SendLinesToPrinterNow(linesToWrite.ToArray());
            }

            return lineBeingSent;
        }

        string ApplyExtrusionMultiplier(string lineBeingSent)
        {
            lineBeingSent = lineBeingSent.ToUpper().Trim();
            if (lineBeingSent.StartsWith("G0") || lineBeingSent.StartsWith("G1"))
            {
                if (GCodeFile.GetFirstNumberAfter("E", lineBeingSent, ref gcodeRequestedExtrusionPosition))
                {
                    double delta = gcodeRequestedExtrusionPosition - previousGcodeRequestedExtrusionPosition;
                    if (extruderMode == PrinterMachineInstruction.MovementTypes.Relative)
                    {
                        delta = gcodeRequestedExtrusionPosition;
                    }
                    double newActualExtruderPosition = currentActualExtrusionPosition + delta * ExtrusionRatio;
                    lineBeingSent = GCodeFile.ReplaceNumberAfter('E', lineBeingSent, newActualExtruderPosition);
                    previousGcodeRequestedExtrusionPosition = gcodeRequestedExtrusionPosition;
                    currentActualExtrusionPosition = newActualExtruderPosition;
                }
            }
            else if (lineBeingSent.StartsWith("G92"))
            {
                if (GCodeFile.GetFirstNumberAfter("E", lineBeingSent, ref gcodeRequestedExtrusionPosition))
                {
                    previousGcodeRequestedExtrusionPosition = gcodeRequestedExtrusionPosition;
                    currentActualExtrusionPosition = gcodeRequestedExtrusionPosition;
                }
            }

            return lineBeingSent;
        }

        string ApplyFeedRateMultiplier(string lineBeingSent)
        {
            if (FeedRateRatio != 1)
            {
                lineBeingSent = lineBeingSent.ToUpper().Trim();
                if (lineBeingSent.StartsWith("G0") || lineBeingSent.StartsWith("G1"))
                {
                    double feedRate = 0;
                    if (GCodeFile.GetFirstNumberAfter("F", lineBeingSent, ref feedRate))
                    {
                        lineBeingSent = GCodeFile.ReplaceNumberAfter('F', lineBeingSent, feedRate * FeedRateRatio);
                    }
                }
            }
            return lineBeingSent;
        }

        void SetDetailedPrintingState(string lineBeingSetToPrinter)
        {
            if (lineBeingSetToPrinter.StartsWith("G28"))
            {
                PrintingState = DetailedPrintingState.HomingAxis;
            }
            else if (lineBeingSetToPrinter.StartsWith("M190"))
            {
                PrintingState = DetailedPrintingState.HeatingBed;
            }
            else if (lineBeingSetToPrinter.StartsWith("M109"))
            {
                PrintingState = DetailedPrintingState.HeatingExtruder;
            }
            else
            {
                PrintingState = DetailedPrintingState.Printing;
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

        public void SendLineToPrinterNow(string lineToWrite)
        {
            using (TimedLock.Lock(this, "QueueLineToPrinter"))
            {
                //Check line for linebreaks, split and process separate if necessary
                if (lineToWrite.Contains("\n"))
                {
                    string[] linesToWrite = lineToWrite.Split(new string[] { "\n" }, StringSplitOptions.None);
                    SendLinesToPrinterNow(linesToWrite);
                    return;
                }

                lineToWrite = lineToWrite.Split(';')[0].Trim();
                if (lineToWrite.Trim().Length > 0)
                {
                    if (PrinterIsPrinting && CommunicationState != CommunicationStates.PrintingFromSd)
                    {
                        // insert the command into the printing queue at the head
                        if (printerCommandQueueLineIndex >= 0
							&& printerCommandQueueLineIndex < loadedGCode.LineCount - 1)
                        {
                            if (!loadedGCode.Instruction(printerCommandQueueLineIndex + 1).Line.Contains(lineToWrite))
                            {
                                loadedGCode.Insert(printerCommandQueueLineIndex + 1, new PrinterMachineInstruction(lineToWrite, loadedGCode.Instruction(printerCommandQueueLineIndex)));
                            }
                        }
                    }
                    else
                    {
                        // sometimes we need to send code without buffering (like when we are closing the program).
                        if (ForceImmediateWrites)
                        {
                            WriteToPrinter(lineToWrite + "\r\n", lineToWrite);
                        }
                        else
                        {
                            // try not to write the exact same command twice (like M105)
                            if (LinesToWriteQueue.Count == 0 || LinesToWriteQueue[LinesToWriteQueue.Count - 1] != lineToWrite)
                            {
                                LinesToWriteQueue.Add(lineToWrite);
                            }
                        }
                    }
                }
            }
        }

        // this is to make it misbehave
        //int checkSumCount = 1;
        private void WriteChecksumLineToPrinter(string lineToWrite)
        {
            SetDetailedPrintingState(lineToWrite);

            lineToWrite = ApplyExtrusionMultiplier(lineToWrite);
            lineToWrite = ApplyFeedRateMultiplier(lineToWrite);
            lineToWrite = KeepTrackOfPostionAndDestination(lineToWrite);
            lineToWrite = RunPrintLevelingTranslations(lineToWrite);

            string lineWithCount = "N" + (allCheckSumLinesSent.Count + 1).ToString() + " " + lineToWrite;
            string lineWithChecksum = lineWithCount + "*" + GCodeFile.CalculateChecksum(lineWithCount).ToString();
            allCheckSumLinesSent.Add(lineWithChecksum);
            //if ((checkSumCount++ % 71) == 0)
            {
                //lineWithChecksum = lineWithCount + "*" + (GCodeFile.CalculateChecksum(lineWithCount) + checkSumCount).ToString();
                //WriteToPrinter(lineWithChecksum + "\r\n", lineToWrite);
            }
            //else
            {
                WriteToPrinter(lineWithChecksum + "\r\n", lineToWrite);
            }
        }

        void WriteToPrinter(string lineToWrite, string lineWithoutChecksum)
        {
            if (PrinterIsConnected || CommunicationState == CommunicationStates.AttemptingToConnect)
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    FoundStringEventArgs foundStringEvent = new FoundStringEventArgs(lineWithoutChecksum);

                    // write data to communication
                    {
						StringEventArgs currentEvent = new StringEventArgs(lineToWrite);
						if (PrinterIsPrinting)
						{
							string lineWidthoutCR = lineToWrite.Substring(0, lineToWrite.Length - 2);
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
                        timeSinceLastWrite.Restart();
                        timeHaveBeenWaitingForOK.Restart();
                        serialPort.Write(lineToWrite);
                        //Debug.Write("w: " + lineToWrite);
                    }
                    catch (IOException ex)
                    {
                        Trace.WriteLine("Error writing to printer: " + ex.Message);

                        // Handle hardware disconnects by relaying the failure reason and shutting down open resources
                        AbortConnectionAttempt("Connection Lost - " + ex.Message);
                    }
                    catch (TimeoutException ex)
                    {
                    }
                }
                else
                {
                    OnConnectionFailed(null);
                }
            }
        }

        public void PulseRtsLow()
        {
            if (serialPort == null && this.ActivePrinter != null)
            {
				serialPort = FrostedSerialPortFactory.Instance.Create(this.ActivePrinter.ComPort);
                serialPort.BaudRate = this.BaudRate;

                // Set the read/write timeouts
                serialPort.ReadTimeout = 500;
                serialPort.WriteTimeout = 500;
                serialPort.Open();
                
                serialPort.RtsEnable = true;
                serialPort.RtsEnable = false;
                try
                {
                    Thread.Sleep(100);
                }
                catch
                {
                }
                serialPort.RtsEnable = true;
                serialPort.Close();
            }
        }

		public void RebootBoard()
		{
			if (this.ActivePrinter != null
				&&  serialPort != null)
			{
				bool wasConnected = PrinterIsConnected;
				// first make sure we are not printing if possible (cancel slicing)
				IFrostedSerialPort currentSerialPort = serialPort;
				Stop();
				serialPort = currentSerialPort;

				serialPort.RtsEnable = true;
				serialPort.DtrEnable = true;
				Thread.Sleep(100);
				serialPort.RtsEnable = false;
				serialPort.DtrEnable = false;
				Thread.Sleep(100);
				serialPort.RtsEnable = true;
				serialPort.DtrEnable = true;

				ClearQueuedGCode();

				if (wasConnected)
				{
					// let the process know we canceled not ended normaly.
					CommunicationState = CommunicationStates.Connected;
				}
			}
		}

		/// <summary>
        /// Abort an ongoing attempt to establish communcation with a printer due to the specified problem. This is a specialized
        /// version of the functionality that's previously been in .Disable but focused specifically on the task of aborting an 
        /// ongoing connection. Ideally we should unify all abort invocations to use this implementation rather than the mix
        /// of occasional OnConnectionFailed calls, .Disable and .stopTryingToConnect
        /// </summary>
        /// <param name="abortReason">The concise message which will be used to describe the connection failure</param>
        /// <param name="shutdownReadLoop">Shutdown/join the readFromPrinterThread</param>
        public void AbortConnectionAttempt(string abortReason, bool shutdownReadLoop = true)
        {
            // Set .Disconnecting to allow the readloop to exit gracefully before a forced thread join (and extended timeout)
            CommunicationState = CommunicationStates.Disconnecting;

            // Shudown the connectionAttempt thread
            if (connectThread != null)
            {
                connectThread.Join(JoinThreadTimeoutMs); //Halt connection thread
            }

            // Shutdown the readFromPrinter thread
            if (shutdownReadLoop)
            {
				ReadThreadHolder.Join();
            }

            // Shudown the serial port
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

        public void Disable()
        {
            if (PrinterIsConnected)
            {
                // Make sure we send this without waiting for the printer to respond. We want to try and turn off the heaters.
                // It may be possible in the future to make this go into the printer queue for assured sending but it means
                // the program has to be smart about closing an able to wait until the printer has agreed that it shut off
                // the motors and heaters (a good idea ane something for the future).
                ForceImmediateWrites = true;
                ReleaseMotors();
                TurnOffBedAndExtruders();
                FanSpeed0To255 = 0;
                ForceImmediateWrites = false;

                CommunicationState = CommunicationStates.Disconnecting;
				ReadThreadHolder.Join();
				if (serialPort != null)
				{
					serialPort.Close();
					serialPort.Dispose();
				}
                serialPort = null;
                CommunicationState = CommunicationStates.Disconnected;
                LinesToWriteQueue.Clear();
            }
            else
            {
                //Need to reset UI - even if manual disconnect                
                TurnOffBedAndExtruders();
                FanSpeed0To255 = 0;
            }
            OnEnabledChanged(null);
        }

		double MaxTimeToMoveForSentInstructions()
		{
			double maxTime = 0;
			for(int i = Math.Max(0, printerCommandQueueLineIndex - backupAmount); i < printerCommandQueueLineIndex; i++)
			{
				maxTime = Math.Max(maxTime, loadedGCode.Instruction(i).secondsThisLine);
			}

			return maxTime * 1.5;
		}

        Stopwatch timeSinceLastWrite = new Stopwatch();
        void TryWriteNextLineFromGCodeFile()
        {
            bool forceContinueInCaseOfNoResponse = false;
            // wait until the printer responds from the last command with an ok OR we waited too long
            if(timeHaveBeenWaitingForOK.IsRunning
                && !forceContinueInCaseOfNoResponse)
            {
                using (TimedLock.Lock(this, "WriteNextLineFromGCodeFile1"))
                {
                    // we are still sending commands
					if (printerCommandQueueLineIndex > 0 && printerCommandQueueLineIndex < loadedGCode.LineCount - 1)
                    {
                        // the last instruction was a move
                        PrinterMachineInstruction lastInstruction = loadedGCode.Instruction(printerCommandQueueLineIndex - 1);
						double epectedSecondsToWait = Math.Max(5, MaxTimeToMoveForSentInstructions());
						bool wasMoveAndNoOK = (lastInstruction.Line.Contains("G0 ") || lastInstruction.Line.Contains("G1 ")) 
							&& timeHaveBeenWaitingForOK.Elapsed.TotalSeconds > epectedSecondsToWait;
                        {
                            // This code is to try and make sure the printer does not stop on transmission errors.
                            // If it has been more than 10 seconds since the printer responded anything 
                            // and it was not ok, and it's been more than 30 second since we sent the command.
                            if ((timeSinceLastReadAnything.Elapsed.TotalSeconds > 10 && timeSinceLastWrite.Elapsed.TotalSeconds > 30)
                                || wasMoveAndNoOK)
                            {
                                //if (firstLineToResendIndex == allCheckSumLinesSent.Count)
                                {
                                    // Basically we got some response but it did not contain an OK.
                                    // The theory is that we may have recieved a transmission error (like 'OP' rather than 'OK')
                                    // and in that event we don't want the print to just stop and wait forever.
                                    forceContinueInCaseOfNoResponse = true;
                                    firstLineToResendIndex--; // we are going to resend the last command
                                }
                            }
                            else
                            {
                                // we are waiting for the ok so let's wait
                                return;
                            }
                        }
                    }
                }
            }

            bool pauseRequested = false;
            using (TimedLock.Lock(this, "WriteNextLineFromGCodeFile2"))
            {
				if (printerCommandQueueLineIndex < loadedGCode.LineCount)
                {
                    if (firstLineToResendIndex < allCheckSumLinesSent.Count)
                    {
                        WriteToPrinter(allCheckSumLinesSent[firstLineToResendIndex++] + "\n", "resend");
                    }
                    else
                    {
						int waitTimeInMs = 60000; // 60 seconds
						if (waitingForPosition.IsRunning && waitingForPosition.ElapsedMilliseconds < waitTimeInMs)
						{
							// we are waiting for a postion response don't print more
							return;
						}

						string lineToWrite = loadedGCode.Instruction(printerCommandQueueLineIndex).Line;
                        string[] splitOnSemicolon = lineToWrite.Split(';');
                        string trimedLine = splitOnSemicolon[0].Trim().ToUpper();

						if (lineToWrite.Contains("M114") )
						{
							waitingForPosition.Restart();
						}
						
                        if (trimedLine.Length > 0)
                        {
                            if (lineToWrite == "MH_PAUSE")
                            {
                                pauseRequested = true;
                            }
                            else if (lineToWrite == "M226" || lineToWrite == "@pause")
                            {
                                RequestPause(printerCommandQueueLineIndex + 1);
                            }
                            else
                            {
                                WriteChecksumLineToPrinter(lineToWrite);
                            }

                            firstLineToResendIndex++;
                        }
                        printerCommandQueueLineIndex++;
                    }
                }
                else if (printWasCanceled)
                {
                    CommunicationState = CommunicationStates.Connected;
                    // never leave the extruder and the bed hot
                    ReleaseMotors();
                    TurnOffBedAndExtruders();
                    printWasCanceled = false;
                }
                else
                {
					if (printerCommandQueueLineIndex == loadedGCode.LineCount)
                    {
                        CommunicationState = CommunicationStates.FinishedPrint;

                        printJobDisplayName = null;

                        // never leave the extruder and the bed hot
                        ReleaseMotors();
                        TurnOffBedAndExtruders();
                    }
                    else if (!PrinterIsPaused)
                    {
                        CommunicationState = CommunicationStates.Connected;
                    }
                }
            }

            if (pauseRequested)
            {
                DoPause();
            }
        }

        public void RequestPause(int injectionStartIndex = 0)
        {
            if (injectionStartIndex == 0)
            {
                injectionStartIndex = printerCommandQueueLineIndex;
            }

            if (PrinterIsPrinting)
            {
                if (CommunicationState == CommunicationStates.PrintingFromSd)
                {
                    CommunicationState = CommunicationStates.Paused;
                    SendLineToPrinterNow("M25"); // : Pause SD print
                    return;
                }

                // Add the pause_gcode to the loadedGCode.GCodeCommandQueue
                string pauseGCode = ActiveSliceSettings.Instance.GetActiveValue("pause_gcode");
                if (pauseGCode.Trim() == "")
                {
					// inject the resume_gcode to execute when we resume printing
					string resumeGCode = ActiveSliceSettings.Instance.GetActiveValue("resume_gcode");
					int lastIndexAdded = InjectGCode(resumeGCode, injectionStartIndex);
					
					// put in the code to return to return to our pre-pause postion 
					lastIndexAdded = InjectGCode("G0 X{0:0.000} Y{1:0.000} Z{2:0.000} F{3}".FormatWith(currentDestination.x, currentDestination.y, currentDestination.z, currentFeedRate), injectionStartIndex);
                    DoPause();
                }
                else
                {
                    using (TimedLock.Lock(this, "RequestPause"))
                    {
                        int lastIndexAdded = InjectGCode(pauseGCode, injectionStartIndex);

                        // inject a marker to tell when we are done with the inserted pause code
                        lastIndexAdded = InjectGCode("MH_PAUSE", lastIndexAdded);

                        // inject the resume_gcode to execute when we resume printing
                        string resumeGCode = ActiveSliceSettings.Instance.GetActiveValue("resume_gcode");
                        lastIndexAdded = InjectGCode(resumeGCode, lastIndexAdded);

						// put in the code to return to return to our pre-pause postion 
						lastIndexAdded = InjectGCode("G0 X{0:0.000} Y{1:0.000} Z{2:0.000} F{3}".FormatWith(currentDestination.x, currentDestination.y, currentDestination.z, currentFeedRate), lastIndexAdded);
                    }
                }
            }
        }

        void DoPause()
        {
            if (PrinterIsPrinting)
            {
                CommunicationState = CommunicationStates.Paused;
            }
        }

        private int InjectGCode(string codeToInject, int lineIndexToStartInjection)
        {
            codeToInject = GCodeProcessing.ReplaceMacroValues(codeToInject);

            codeToInject = codeToInject.Replace("\\n", "\n");
            string[] lines = codeToInject.Split('\n');

            int linesAdded = 0;
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string[] splitOnSemicolon = lines[i].Split(';');
                string trimedLine = splitOnSemicolon[0].Trim().ToUpper();
                if (trimedLine != "")
                {
					trimedLine = ReplacePrinterMacros(trimedLine);

					if (loadedGCode.LineCount > lineIndexToStartInjection)
                    {
                        loadedGCode.Insert(lineIndexToStartInjection, new PrinterMachineInstruction(trimedLine, loadedGCode.Instruction(lineIndexToStartInjection)));
                    }
                    else
                    {
                        loadedGCode.Add(new PrinterMachineInstruction(trimedLine));
                    }
                    linesAdded++;
                }
            }

            return lineIndexToStartInjection + linesAdded;
        }

		private string ReplacePrinterMacros(string trimedLine)
		{
			if (trimedLine.StartsWith("@"))
			{
				switch (trimedLine)
				{
					case "@RESTORE_XYZ_POSITION":
						return "G0 X{0:0.000} Y{1:0.000} Z{2:0.000} F{3}".FormatWith(currentDestination.x, currentDestination.y, currentDestination.z, currentFeedRate);

					case "@RESTORE_E_POSITION":
						return "G92 E{0:0.00000}".FormatWith(currentExtruderDestination);
				}
			}

			return trimedLine;
		}

        public void Resume()
        {
            if (PrinterIsPaused)
            {
				if (prePauseCommunicationState == CommunicationStates.PrintingFromSd)
                {
                    CommunicationState = CommunicationStates.PrintingFromSd;

                    SendLineToPrinterNow("M24"); // Start/resume SD print
                }
                else
                {
                    CommunicationState = CommunicationStates.Printing;
                }
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

        void ClearQueuedGCode()
        {
            loadedGCode.Clear();
            printerCommandQueueLineIndex = 0;
            lastRemainingSecondsReported = 0;

            allCheckSumLinesSent.Clear();
            WriteChecksumLineToPrinter("M110 S1");
            firstLineToResendIndex = 1;
        }

        public void Stop()
        {
            switch (CommunicationState)
            {
                case CommunicationStates.PrintingFromSd:
                    using (TimedLock.Lock(this, "CancelingPrint"))
                    {
                        // get rid of all the gcode we have left to print
                        ClearQueuedGCode();
                        // let the process know we canceled not ended normaly.
                        CommunicationState = CommunicationStates.Connected;
                        SendLineToPrinterNow("M25"); // : Pause SD print
                        SendLineToPrinterNow("M26"); // : Set SD position
                        // never leave the extruder and the bed hot
                        DonePrintingSdFile(this, null);
                    }
                    break;

                case CommunicationStates.Printing:
                    {
                        using (TimedLock.Lock(this, "CancelingPrint"))
                        {
                            // get rid of all the gcode we have left to print
                            ClearQueuedGCode();
                            string cancelGCode = ActiveSliceSettings.Instance.GetActiveValue("cancel_gcode");
                            if (cancelGCode.Trim() != "")
                            {
                                // add any gcode we want to print while canceling
                                InjectGCode(cancelGCode, printerCommandQueueLineIndex);
                            }
                            // let the process know we canceled not ended normaly.
                            printWasCanceled = true;
                        }
                        if (activePrintTask != null)
                        {
                            TimeSpan printTimeSpan = DateTime.Now.Subtract(activePrintTask.PrintStart);

                            activePrintTask.PrintEnd = DateTime.Now;
                            activePrintTask.PrintComplete = false;
                            activePrintTask.Commit();
                        }
                    }
                    
                    break;

                case CommunicationStates.Paused:
                    {
                        CommunicationState = CommunicationStates.Connected;
                        if (activePrintTask != null)
                        {
                            TimeSpan printTimeSpan = DateTime.Now.Subtract(activePrintTask.PrintStart);

                            activePrintTask.PrintEnd = DateTime.Now;
                            activePrintTask.PrintComplete = false;
                            activePrintTask.Commit();
                        }
                    }                    
                    break;

                case CommunicationStates.AttemptingToConnect:
                    CommunicationState = CommunicationStates.FailedToConnect;
                    connectThread.Join(JoinThreadTimeoutMs);
                    CommunicationState = CommunicationStates.Disconnecting;
					ReadThreadHolder.Join();
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

            ReadLineStartCallBacks.AddCallBackToKey("Done printing file", DonePrintingSdFile);

            return true;
        }

        void TurnOffBedAndExtruders()
        {
            SetTargetExtruderTemperature(0, 0);
            SetTargetExtruderTemperature(1, 0);
            TargetBedTemperature = 0;
        }

        void DonePrintingSdFile(object sender, EventArgs e)
        {
            UiThread.RunOnIdle((state) =>
            {
                ReadLineStartCallBacks.RemoveCallBackFromKey("Done printing file", DonePrintingSdFile);
            });
            CommunicationState = CommunicationStates.FinishedPrint;

            printJobDisplayName = null;

            // never leave the extruder and the bed hot
            TurnOffBedAndExtruders();

            ReleaseMotors();
        }

        public void StartPrint(string gcodeFilename)
        {
            if (!PrinterIsConnected || PrinterIsPrinting)
            {
                return;
            }

			printWasCanceled = false;
            ExtrusionRatio = 1;
            FeedRateRatio = 1;
			waitingForPosition.Stop();
			waitingForPosition.Reset();

            LinesToWriteQueue.Clear();
            ClearQueuedGCode();

			BackgroundWorker loadGCodeWorker = new BackgroundWorker();
			loadGCodeWorker.DoWork += new DoWorkEventHandler(loadGCodeWorker_DoWork);
			loadGCodeWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(loadGCodeWorker_RunWorkerCompleted);
			loadGCodeWorker.RunWorkerAsync(gcodeFilename);
		}

		void loadGCodeWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			string gcodeFilename = e.Argument as string;
			loadedGCode = GCodeFile.Load(gcodeFilename);
		}

		void loadGCodeWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			switch (communicationState)
			{
				case CommunicationStates.Connected:
					// This can happen if the printer is reset during the silcing of the part.
					break;

				case CommunicationStates.PreparingToPrint:
					if (ActivePrintItem.PrintItem.Id == 0)
					{
						ActivePrintItem.PrintItem.Commit();
					}

					activePrintTask = new PrintTask();
					activePrintTask.PrintStart = DateTime.Now;
					activePrintTask.PrinterId = ActivePrinterProfile.Instance.ActivePrinter.Id;
					activePrintTask.PrintName = ActivePrintItem.PrintItem.Name;
					activePrintTask.PrintItemId = ActivePrintItem.PrintItem.Id;
					activePrintTask.PrintComplete = false;
					activePrintTask.Commit();

					CommunicationState = CommunicationStates.Printing;
					break;

				default:
#if DEBUG
					throw new Exception("We are not preparing to print so we should not be starting to print");
#else
					CommunicationState = CommunicationStates.Connected;
					break;
#endif
			}
		}

        const int MAX_INVALID_CONNECTION_CHARS = 3;
        string dataLastRead = "";
        string lastLineRead = "";
        public void ReadFromPrinter(object sender, DoWorkEventArgs e)
        {
			ReadThreadHolder readThreadHolder = e.Argument as ReadThreadHolder;

			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            timeSinceLastReadAnything.Restart();
            // we want this while loop to be as fast as possible. Don't allow any significant work to happen in here
            while (CommunicationState == CommunicationStates.AttemptingToConnect
                || (PrinterIsConnected && serialPort.IsOpen && !Disconnecting && readThreadHolder.IsCurrentThread()))
            {
                if(PrinterIsPrinting 
                    && PrinterIsConnected
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
                        using (TimedLock.Lock(this, "ReadFromPrinter"))
                        {
                            string allDataRead = serialPort.ReadExisting();
                            //Debug.Write("r: " + allDataRead);
                            //Console.Write(indata);
                            dataLastRead += allDataRead.Replace('\r', '\n');
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
                                    lastLineRead = dataLastRead.Substring(0, returnPosition);
                                    dataLastRead = dataLastRead.Substring(returnPosition+1);

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
                                        // (initial line having more than 3 non-ascii characters) may not be adequate or appropriate. 
                                        // TODO: Revise the INVALID char count to an agreed upon threshold
                                        string[] segments = lastLineRead.Split('?');
                                        if (segments.Length <= MAX_INVALID_CONNECTION_CHARS)
                                        {
                                            CommunicationState = CommunicationStates.Connected;
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

                    Thread.Sleep(1);
                }
                catch (TimeoutException)
                {
                }
                catch (IOException)
                {
                    OnConnectionFailed(null);
                }
                catch (InvalidOperationException ex)
                {
                    Debug.WriteLine(ex.Message);
                    // this happens when the serial port closes after we check and before we read it.
                }
                catch (UnauthorizedAccessException)
                {
                    OnConnectionFailed(null);
                }
            }
        }

        public void ReleaseMotors()
        {
            SendLineToPrinterNow("M84");
        }

        [Flags]
        public enum Axis { X = 1, Y = 2, Z = 4, E = 8, XYZ = (X | Y | Z) }
        public void HomeAxis(Axis axis)
        {
            string command = "G28";
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

            SendLineToPrinterNow(command);
            ReadPosition();
        }

        public void SetMovementToAbsolute()
        {
            PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("G90");
        }

        public void SetMovementToRelative()
        {
            PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("G91");
        }

        public void MoveRelative(Axis axis, double moveAmountMm, double feedRateMmPerMinute)
        {
            if (moveAmountMm != 0)
            {
                SetMovementToRelative();
                PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("G1 F{0}".FormatWith(feedRateMmPerMinute));
                PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("G1 {0}{1}".FormatWith(axis, moveAmountMm));
                SetMovementToAbsolute();
            }
        }

        public void MoveExtruderRelative(double moveAmountMm, double feedRateMmPerMinute, int extruderNumber=0)
        {
            if (moveAmountMm != 0)
            {
                SetMovementToRelative();
                PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("T{0}".FormatWith(extruderNumber)); //Set active extruder
                PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("G1 F{0}".FormatWith(feedRateMmPerMinute));
                PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("G1 E{0}".FormatWith(moveAmountMm));
                PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("T0".FormatWith(extruderNumber)); //Reset back to extruder one
                SetMovementToAbsolute();
            }
        }

        public void MoveAbsolute(Axis axis, double axisPositionMm, double feedRateMmPerMinute)
        {
            SetMovementToAbsolute();
            PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("G1 F{0}".FormatWith(feedRateMmPerMinute));
            PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("G1 {0}{1}".FormatWith(axis, axisPositionMm));
        }

        public void MoveAbsolute(Vector3 position, double feedRateMmPerMinute)
        {
            SetMovementToAbsolute();
            PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("G1 F{0}".FormatWith(feedRateMmPerMinute));
            PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("G1 X{0}Y{1}Z{2}".FormatWith(position.x, position.y, position.z));
        }

        public void ReadPosition()
        {
            SendLineToPrinterNow("M114");
        }

        public void DeleteFileFromSdCard(string fileName)
        {
            // Register to detect the file deleted confirmation.
            // This should have worked without this by getting the normal 'ok' on the next line. But the ok is not on its own line.
            ReadLineStartCallBacks.AddCallBackToKey("File deleted:", FileDeleteConfirmed);
            // and send the line to delete the file
            SendLineToPrinterNow("M30 {0}".FormatWith(fileName.ToLower()));
        }

        void FileDeleteConfirmed(object sender, EventArgs e)
        {
            UiThread.RunOnIdle((state) =>
            {
                ReadLineStartCallBacks.RemoveCallBackFromKey("File deleted:", FileDeleteConfirmed);
            });
            PrintingCanContinue(this, null);
        }
    }
}
