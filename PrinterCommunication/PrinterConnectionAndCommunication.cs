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

// This should split into Connection and Communication eventually and use PrinterIo for the sourc of data.

#define USE_FROSTED_SERIAL_PORT

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
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
using MatterHackers.SerialPortCommunication;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using MatterHackers.VectorMath;
using Microsoft.Win32.SafeHandles;

namespace MatterHackers.MatterControl.PrinterCommunication
{
    /// <summary>
    /// This is a class to pass temperatures to callbacks that expect them.
    /// A call back can try and cast to this ( TemperatureEventArgs tempArgs = e as TemperatureEventArgs)
    /// and then use the temperature if available.
    /// </summary>
    public class TemperatureEventArgs : EventArgs
    {
        double temperature;

        public TemperatureEventArgs(double temperature)
        {
            this.temperature = temperature;
        }

        public double Temperature
        {
            get { return temperature; }
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

        static PrinterConnectionAndCommunication globalInstance;
        string connectionFailureMessage = "Unknown Reason";

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

        FoundStringStartsWithCallbacks ReadLineStartCallBacks = new FoundStringStartsWithCallbacks();
        FoundStringContainsCallbacks ReadLineContainsCallBacks = new FoundStringContainsCallbacks();

        FoundStringStartsWithCallbacks WriteLineStartCallBacks = new FoundStringStartsWithCallbacks();
        FoundStringContainsCallbacks WriteLineContainsCallBacks = new FoundStringContainsCallbacks();

        bool printWasCanceled = false;
        int firstLineToResendIndex = 0;
        PrintTask activePrintTask;
        List<string> allCheckSumLinesSent = new List<string>();

        List<string> LinesToWriteQueue = new List<string>();

        Stopwatch timeSinceLastReadAnything = new Stopwatch();
        Stopwatch timeHaveBeenWaitingForOK = new Stopwatch();

        public enum CommunicationStates { Disconnected, AttemptingToConnect, FailedToConnect, Connected, PreparingToPrint, Printing, Paused, FinishedPrint, Disconnecting, ConnectionLost };
        CommunicationStates communicationState = CommunicationStates.Disconnected;

        bool ForceImmediateWrites = false;

        public CommunicationStates CommunicationState
        {
            get
            {
                return communicationState;
            }

            set
            {
                if (communicationState != value)
                {
                    // if it was printing
                    if (communicationState == CommunicationStates.Printing)
                    {
                        // and is changing to paused
                        if (value == CommunicationStates.Paused)
                        {
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
                            
                            OnPrintFinished(null);
                            timeSinceStartedPrint.Stop();
                        }
                        else
                        {
                            timeSinceStartedPrint.Stop();
                            timeSinceStartedPrint.Reset();
                        }
                    }
                    else if (communicationState == CommunicationStates.Paused) // was paused
                    {
                        // changing to printing
                        if (value == CommunicationStates.Printing)
                        {
                            timeSinceStartedPrint.Start();
                        }
                    }

                    communicationState = value;
                    OnCommunicationStateChanged(null);
                }
            }
        }

        bool stopTryingToConnect = false;

        double actualExtruderTemperature;
        double targetExtruderTemperature;
        double actualBedTemperature;
        double targetBedTemperature;
        string printJobDisplayName = null;
        GCodeFile loadedGCode = new GCodeFile();
		#if USE_FROSTED_SERIAL_PORT
		IFrostedSerialPort serialPort;
		#else
		SerialPort serialPort;
		#endif
        Thread readFromPrinterThread;
        Thread connectThread;

        private PrintItemWrapper activePrintItem;

        int lastRemainingSecondsReported = 0;
        int printerCommandQueueIndex = -1;

        Thread sendGCodeToPrinterThread;

        public bool DtrEnableOnConnect
        {
            get
            {
                if (ActivePrinter != null)
                {
                    //return !ActivePrinter.SuppressDtrOnConnect;
                }

                return true;
            }

            set
            {
                throw new NotImplementedException();
#if false
                if (ActivePrinter != null)
                {
                    bool enableDtrOnConnect = !ActivePrinter.SuppressDtrOnConnect;
                    if (enableDtrOnConnect != value)
                    {
                        ActivePrinter.SuppressDtrOnConnect = !value;
                        ActivePrinter.Commit();
                    }
                }
#endif
            }
        }

        Vector3 currentDestination;
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
                    case CommunicationStates.Paused:
                    case CommunicationStates.FinishedPrint:
                        return true;

                    default:
                        throw new NotImplementedException("Make sure very satus returns the correct connected state.");
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

            ReadLineStartCallBacks.AddCallBackToKey("ok T:", ReadTemperatures); // marlin
            ReadLineStartCallBacks.AddCallBackToKey("T:", ReadTemperatures); // repatier

            ReadLineStartCallBacks.AddCallBackToKey("C:", ReadTargetPositions);
            ReadLineStartCallBacks.AddCallBackToKey("X:", ReadTargetPositions);

            ReadLineContainsCallBacks.AddCallBackToKey("RS:", PrinterRequestsResend);
            ReadLineContainsCallBacks.AddCallBackToKey("Resend:", PrinterRequestsResend);
            ReadLineContainsCallBacks.AddCallBackToKey("FIRMWARE_NAME:", PrinterStatesFirmware);

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
                        return true;

                    default:
                        throw new NotImplementedException("Make sure very satus returns the correct connected state.");
                }
            }
        }

        public enum DetailedPrintingState { HomingAxis, HeatingBed, HeatingExtruder, Printing };
        DetailedPrintingState detailedPrintingState;

        public DetailedPrintingState PrintingState
        {
            get
            {
                return detailedPrintingState;
            }
        }

        public string PrintingStateString
        {
            get
            {
                switch (detailedPrintingState)
                {
                    case DetailedPrintingState.HomingAxis:
                        return "Homing Axis";

                    case DetailedPrintingState.HeatingBed:
                        return "Waiting for Bed to Heat to {0}°".FormatWith(TargetBedTemperature);

                    case DetailedPrintingState.HeatingExtruder:
                        return "Waiting for Extruder to Heat to {0}°".FormatWith(TargetExtruderTemperature);

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
						return LocalizedString.Get("Not Connected");
                    case CommunicationStates.Disconnecting:
						return LocalizedString.Get("Disconnecting");
					case CommunicationStates.AttemptingToConnect:
						string connectingMessageTxt = LocalizedString.Get ("Connecting");
						return "{0}...".FormatWith(connectingMessageTxt);
                    case CommunicationStates.ConnectionLost:
						return LocalizedString.Get("Connection Lost");
                    case CommunicationStates.FailedToConnect:
                        return "Unable to Connect";
                    case CommunicationStates.Connected:
						return LocalizedString.Get("Connected");
                    case CommunicationStates.PreparingToPrint:
						return LocalizedString.Get("Preparing To Print");
                    case CommunicationStates.Printing:
						return LocalizedString.Get("Printing");
                    case CommunicationStates.Paused:
						return LocalizedString.Get("Paused");
                    case CommunicationStates.FinishedPrint:
						return LocalizedString.Get("Finished Print");
                    default:
                        throw new NotImplementedException("Make sure very satus returns the correct connected state.");
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
                return loadedGCode.Count;
            }
        }

        public int TotalSecondsInPrint
        {
            get
            {
                if (loadedGCode.Count > 0)
                {
                    if (FeedRateRatio != 0)
                    {
                        return (int)(loadedGCode.Instruction(0).secondsToEndFromHere / FeedRateRatio);
                    }

                    return (int)(loadedGCode.Instruction(0).secondsToEndFromHere);
                }

                return 0;
            }
        }

        int backupAmount = 16;
        public int CurrentlyPrintingLayer
        {
            get
            {
                int currentIndex = printerCommandQueueIndex - backupAmount;
                if (currentIndex >= 0
                    && currentIndex < loadedGCode.Count)
                {
                    for(int zIndex = 0; zIndex < loadedGCode.NumChangesInZ; zIndex++)
                    {
                        if (currentIndex < loadedGCode.IndexOfChangeInZ[zIndex])
                        {
                            return zIndex-1;
                        }
                    }

                    return loadedGCode.NumChangesInZ - 1;
                }

                return -1;
            }
        }

        public double RatioIntoCurrentLayer
        {
            get
            {
                int currentIndex = printerCommandQueueIndex - backupAmount;
                if (currentIndex >= 0
                    && currentIndex < loadedGCode.Count)
                {
                    int currentLayer = CurrentlyPrintingLayer;
                    int startIndex = loadedGCode.IndexOfChangeInZ[currentLayer];
                    int endIndex = loadedGCode.Count - 1;
                    if (currentLayer < loadedGCode.NumChangesInZ - 2)
                    {
                        endIndex = loadedGCode.IndexOfChangeInZ[currentLayer + 1] - 1;
                    }

                    int deltaFromStart = Math.Max(0, currentIndex - startIndex);
                    return deltaFromStart / (double)(endIndex - startIndex);
                }

                return 0;
            }
        }

        Stopwatch timeSinceStartedPrint = new Stopwatch();
        public int SecondsRemaining
        {
            get
            {
                if (!timeSinceStartedPrint.IsRunning
                    && PrinterIsPrinting)
                {
                    timeSinceStartedPrint.Restart();
                }

                if (NumberOfLinesInCurrentPrint > 0)
                {
                    if (printerCommandQueueIndex >= 0
                        && printerCommandQueueIndex < loadedGCode.Count
                        && loadedGCode.Instruction(printerCommandQueueIndex).secondsToEndFromHere != 0)
                    {
                        if (FeedRateRatio != 0)
                        {
                            lastRemainingSecondsReported = (int)(loadedGCode.Instruction(printerCommandQueueIndex).secondsToEndFromHere / FeedRateRatio);
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
                if (PrintIsFinished && !PrinterIsPaused)
                {
                    return 100.0;
                }
                else if (NumberOfLinesInCurrentPrint > 0 && TotalSecondsInPrint > 0)
                {
                    return Math.Min(99.9, ((1 - ((double)SecondsRemaining / (double)TotalSecondsInPrint)) * 100));
                }
                else
                {
                    return 0.0;
                }
            }
        }

        public void PrintingCanContinue(object sender, EventArgs e)
        {
            timeHaveBeenWaitingForOK.Stop();
        }

        System.Diagnostics.Stopwatch temperatureRequestTimer = new System.Diagnostics.Stopwatch();
        public void OnIdle()
        {
            if (!temperatureRequestTimer.IsRunning)
            {
                temperatureRequestTimer.Start();
            }
            if (temperatureRequestTimer.ElapsedMilliseconds > 2000)
            {
                if (MonitorPrinterTemperature)
                {
                    SendLineToPrinterNow("M105");
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

        private void WriteNextLineFromQueue()
        {
            using (TimedLock.Lock(this, "WriteNextLineFromQueue"))
            {
                string lineToWrite = LinesToWriteQueue[0];

                lineToWrite = KeepTrackOfPostionAndDestination(lineToWrite);

                LinesToWriteQueue.RemoveAt(0); // remove the line first (in case we inject another command)
                WriteToPrinter(lineToWrite + "\r\n", lineToWrite);
                System.Threading.Thread.Sleep(1);
            }
        }

        public double TargetExtruderTemperature
        {
            get
            {
                return targetExtruderTemperature;
            }
            set
            {
                if (targetExtruderTemperature != value)
                {
                    targetExtruderTemperature = value;
                    OnExtruderTemperatureSet(new TemperatureEventArgs(TargetExtruderTemperature));
                    if (PrinterIsConnected)
                    {
                        SendLineToPrinterNow("M104 S{0}".FormatWith(targetExtruderTemperature));
                    }
                    
                }
            }
        }

        public double ActualExtruderTemperature
        {
            get
            {
                return actualExtruderTemperature;
            }
        }

        public void ExtruderTemperatureWasWritenToPrinter(object sender, EventArgs e)
        {
            FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;

            string[] splitOnS = foundStringEventArgs.LineToCheck.Split('S');
            if (splitOnS.Length == 2)
            {
                string temp = splitOnS[1];
                try
                {
                    double tempBeingSet = double.Parse(temp);
                    // we set the private variable so that we don't get the callbacks called and get in a loop of setting the temp
                    targetExtruderTemperature = tempBeingSet;
                    OnExtruderTemperatureSet(new TemperatureEventArgs(TargetExtruderTemperature));
                }
                catch
                {
                    Debug.WriteLine("Unable to Parse Extruder Temperature: {0}".FormatWith(temp));
                }
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
                    OnBedTemperatureSet(new TemperatureEventArgs(TargetBedTemperature));
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
                        OnBedTemperatureSet(new TemperatureEventArgs(TargetBedTemperature));
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
        }

        public void ReadTemperatures(object sender, EventArgs e)
        {
            FoundStringEventArgs foundStringEventArgs = e as FoundStringEventArgs;

            string temperatureString = foundStringEventArgs.LineToCheck;
            {
                int extruderTempLocationInString = temperatureString.IndexOf("T:");
                if (extruderTempLocationInString > -1)
                {
                    extruderTempLocationInString += 2;
                    int endOfExtruderTempInString = temperatureString.IndexOf(" ", extruderTempLocationInString);
                    if (endOfExtruderTempInString < 0)
                    {
                        endOfExtruderTempInString = temperatureString.Length;
                    }

                    string extruderTemp = temperatureString.Substring(extruderTempLocationInString, endOfExtruderTempInString - extruderTempLocationInString);
                    try
                    {
                        double readExtruderTemp = double.Parse(extruderTemp);
                        if (actualExtruderTemperature != readExtruderTemp)
                        {
                            actualExtruderTemperature = readExtruderTemp;
                            OnExtruderTemperatureRead(new TemperatureEventArgs(ActualExtruderTemperature));
                        }
                    }
                    catch
                    {
                        Debug.WriteLine("Unable to Parse Extruder Temperature: {0}".FormatWith(extruderTemp));
                    }
                }
            }
            {
                int bedTempLocationInString = temperatureString.IndexOf("B:");
                if (bedTempLocationInString > -1)
                {
                    bedTempLocationInString += 2;
                    int endOfbedTempInString = temperatureString.IndexOf(" ", bedTempLocationInString);
                    if (endOfbedTempInString < 0)
                    {
                        endOfbedTempInString = temperatureString.Length;
                    }

                    string bedTemp = temperatureString.Substring(bedTempLocationInString, endOfbedTempInString - bedTempLocationInString);
                    try
                    {
                        double readBedTemp = double.Parse(bedTemp);
                        if (actualBedTemperature != readBedTemp)
                        {
                            actualBedTemperature = readBedTemp;
                            OnBedTemperatureRead(new TemperatureEventArgs(ActualBedTemperature));
                        }
                    }
                    catch
                    {
                        Debug.WriteLine("Unable to Parse Bed Temperature: {0}".FormatWith(bedTemp));
                    }
                }
            }
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
                    connectThread.Join(); //Halt connection thread
                    Disable();
                    connectionFailureMessage = "Cancelled";
                    OnConnectionFailed(null);
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else if (CommunicationState == CommunicationStates.FailedToConnect)
            {
                connectThread.Join();
                return false;
            }
            else
            {
                connectThread.Join(); //Halt connection thread
                OnConnectionSucceeded(null);
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

        public int BaudRate
        {
            get
            {
                int baudRate = 0;
                if (this.ActivePrinter != null)
                {
                    try
                    {
                        baudRate = Convert.ToInt32(this.ActivePrinter.BaudRate);
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
            LinesToWriteQueue.Clear();
            //Attempt connecting to a specific printer
            CommunicationState = CommunicationStates.AttemptingToConnect;
            this.stopTryingToConnect = false;
            firmwareType = FirmwareTypes.Unknown;
            firmwareVersion = null;

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
				connectionFailureMessage = "Unavailable";
                OnConnectionFailed(null);
            }
        }

        void Connect_Thread()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            // Allow the user to set the appropriate properties.
            var portNames = SerialPort.GetPortNames();
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

        //Function is not mac-friendly
        bool SerialPortAlreadyOpen(string portName)
        {
            if (OsInformation.OperatingSystem == OSType.Mac)
			{
				return false;
			}
            else if (OsInformation.OperatingSystem == OSType.X11) 
			{
				return false;
			}
            else
            {
                int dwFlagsAndAttributes = 0x40000000;

                //Borrowed from Microsoft's Serial Port Open Method :)
                SafeFileHandle hFile = CreateFile(@"\\.\" + portName, -1073741824, 0, IntPtr.Zero, 3, dwFlagsAndAttributes, IntPtr.Zero);
                if (hFile.IsInvalid)
                {
                    return true;
                }

                hFile.Close();

                return false;
            }
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
            connectionFailureMessage = "Unknown Reason";

            if (PrinterIsConnected)
            {
                throw new Exception("You can only connect when not currently connected.");
            }

            CommunicationState = CommunicationStates.AttemptingToConnect;
            bool serialPortIsAvailable = SerialPortIsAvailable(serialPortName);
            bool serialPortIsAlreadyOpen = SerialPortAlreadyOpen(serialPortName);

            if (serialPortIsAvailable && !serialPortIsAlreadyOpen)
            {
                if (CommunicationState == CommunicationStates.AttemptingToConnect)
                {
                    try
                    {
						#if USE_FROSTED_SERIAL_PORT
						serialPort = FrostedSerialPort.CreateAndOpen(serialPortName, baudRate, DtrEnableOnConnect);
						#else
						serialPort = new SerialPort(serialPortName);
						serialPort.BaudRate = baudRate;
						if (DtrEnableOnConnect)
						{
						serialPort.DtrEnable = true;
						}

						// Set the read/write timeouts
						serialPort.ReadTimeout = 500;
						serialPort.WriteTimeout = 500;

						serialPort.Open();
						#endif

						readFromPrinterThread = new Thread(ReadFromPrinter);
                        readFromPrinterThread.Name = "Read From Printer";
                        readFromPrinterThread.IsBackground = true;
                        readFromPrinterThread.Start();

                        // let's check if the printer will talk to us
                        ReadPosition();
                        SendLineToPrinterNow("M105");
                        SendLineToPrinterNow("M115");
                    }
                    catch (System.ArgumentOutOfRangeException)
                    {
                        connectionFailureMessage = "Unsupported Baud Rate";
                        OnConnectionFailed(null);
                    }

                    catch (Exception)
                    {
                        OnConnectionFailed(null);
                    }
                }
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

                if (movementMode == PrinterMachineInstruction.MovementTypes.Relative)
                {
                    newDestination += currentDestination;
                }

                if (currentDestination != newDestination)
                {
                    currentDestination = newDestination;
                    DestinationChanged.CallEvents(this, null);
                }

                if (ActivePrinter.DoPrintLeveling)
                {
                    string inputLine = lineBeingSent;
                    lineBeingSent = PrintLevelingPlane.Instance.ApplyLeveling(currentDestination, movementMode, inputLine);
                }
            }

            PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(ActivePrinterProfile.Instance.ActivePrinter);
            if(levelingData != null)
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
                detailedPrintingState = DetailedPrintingState.HomingAxis;
            }
            else if (lineBeingSetToPrinter.StartsWith("M190"))
            {
                detailedPrintingState = DetailedPrintingState.HeatingBed;
            }
            else if (lineBeingSetToPrinter.StartsWith("M109"))
            {
                detailedPrintingState = DetailedPrintingState.HeatingExtruder;
            }
            else
            {
                detailedPrintingState = DetailedPrintingState.Printing;
            }
        }

        public void SendLinesToPrinterNow(string[] linesToWrite)
        {
            if (PrinterIsPrinting)
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
                if (PrinterIsPrinting)
                {
                    // insert the command into the printing queue at the head
                    if (printerCommandQueueIndex >= 0
                        && printerCommandQueueIndex < loadedGCode.Count - 1)
                    {
                        if (!loadedGCode.Instruction(printerCommandQueueIndex + 1).Line.Contains(lineToWrite))
                        {
                            loadedGCode.Insert(printerCommandQueueIndex + 1, new PrinterMachineInstruction(lineToWrite, loadedGCode.Instruction(printerCommandQueueIndex)));
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

        private void WriteChecksumLineToPrinter(string lineToWrite)
        {
            SetDetailedPrintingState(lineToWrite);

            lineToWrite = ApplyExtrusionMultiplier(lineToWrite);
            lineToWrite = ApplyFeedRateMultiplier(lineToWrite);
            lineToWrite = KeepTrackOfPostionAndDestination(lineToWrite);

            string lineWithCount = "N" + (allCheckSumLinesSent.Count + 1).ToString() + " " + lineToWrite;
            string lineWithChecksum = lineWithCount + "*" + GCodeFile.CalculateChecksum(lineWithCount);
            WriteToPrinter(lineWithChecksum + "\r\n", lineToWrite);
            allCheckSumLinesSent.Add(lineWithChecksum);
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
                        CommunicationUnconditionalToPrinter.CallEvents(this, currentEvent);

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
                        timeSinceLastReadAnything.Restart();
                        timeHaveBeenWaitingForOK.Restart();
                        serialPort.Write(lineToWrite);
                    }
                    catch (IOException)
                    {
                        OnConnectionFailed(null);
                    }
                    catch (TimeoutException)
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
                serialPort = FrostedSerialPort.Create(this.ActivePrinter.ComPort);
                serialPort.BaudRate = this.BaudRate;
                if (PrinterConnectionAndCommunication.Instance.DtrEnableOnConnect)
                {
                    serialPort.DtrEnable = true;
                }

                // Set the read/write timeouts
                serialPort.ReadTimeout = 500;
                serialPort.WriteTimeout = 500;
                serialPort.Open();
                
                serialPort.RtsEnable = true;
                serialPort.RtsEnable = false;
                try
                {
                    Thread.Sleep(1);
                }
                catch
                {
                }
                serialPort.RtsEnable = true;
                serialPort.Close();
            }
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
                TargetExtruderTemperature = 0;
                TargetBedTemperature = 0;
                FanSpeed0To255 = 0;
                ForceImmediateWrites = false;

                CommunicationState = CommunicationStates.Disconnecting;
                if (readFromPrinterThread != null)
                {
                    readFromPrinterThread.Join();
                }
                serialPort.Close();
                serialPort.Dispose();
                serialPort = null;
                CommunicationState = CommunicationStates.Disconnected;
                LinesToWriteQueue.Clear();
            }
            else
            {
                //Need to reset UI - even if manual disconnect                
                TargetExtruderTemperature = 0;
                TargetBedTemperature = 0;
                FanSpeed0To255 = 0;
            }
            OnEnabledChanged(null);
        }

        void SendCurrentGCodeFileToPrinter()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            while (PrinterIsPrinting && PrinterIsConnected)
            {
                WriteNextLineFromGCodeFile();
            }
        }

        Stopwatch timeSinceLastWrite = new Stopwatch();
        void WriteNextLineFromGCodeFile()
        {
            timeSinceLastWrite.Restart();
            if (PrinterIsConnected)
            {
                //bool forceResendInCaseOfOKError = false;
                // wait until the printer responds from the last command with an ok OR we waited to long
                while (PrinterIsPrinting && timeHaveBeenWaitingForOK.IsRunning)// && !forceResendInCaseOfOKError)
                {
#if false 
                    // this is a bunch of code to try and make sure the printer does not stop on transmission errors.
                    // It is not working and is currently disabled. It would be great to debug this and get it working. Lars.
                    // It has been more than 5 seconds since the printer responded anything 
                    // and it was not ok, and it's been more than 10 second since we sent the command
                    if (timeSinceLastReadAnything.Elapsed.Seconds > 5 && timeSinceLastWrite.Elapsed.Seconds > 10)
                    {
                        // we are still sending commands
                        if (printerCommandQueueIndex > 0 && printerCommandQueueIndex < loadedGCode.GCodeCommandQueue.Count)
                        {
                            // the last instruction was a move
                            PrinterMachineInstruction lastInstruction = loadedGCode.Instruction(printerCommandQueueIndex - 1);
                            if (firstLineToResendIndex == allCheckSumLinesSent.Count)
                            {
                                // Basically we got some response but it did not contain an OK.
                                // The theory is that we may have recieved a transmission error (like 'OP' rather than 'OK')
                                // and in that event we don't want the print to just stop and wait forever.
                                forceResendInCaseOfOKError = true;
                                firstLineToResendIndex--; // we are going to resend the last command
                            }
                        }
                    }

                    bool printerWantsResend = firstLineToResendIndex < allCheckSumLinesSent.Count;
                    if (printerWantsResend)
                    {
                        forceResendInCaseOfOKError = true;
                    }
#endif

                    // we are waiting for ok so wait some time
                    System.Threading.Thread.Sleep(1);
                }

                bool pauseRequested = false;
                using (TimedLock.Lock(this, "WriteNextLineFromGCodeFile"))
                {
                    if (PrinterIsPrinting && printerCommandQueueIndex < loadedGCode.Count)
                    {
                        if (firstLineToResendIndex < allCheckSumLinesSent.Count)
                        {
                            WriteToPrinter(allCheckSumLinesSent[firstLineToResendIndex++], null);
                        }
                        else
                        {
                            string lineToWrite = loadedGCode.Instruction(printerCommandQueueIndex).Line;
                            if (lineToWrite == "MH_PAUSE")
                            {
                                pauseRequested = true;
                            }
                            else
                            {
                                WriteChecksumLineToPrinter(lineToWrite);
                            }

                            printerCommandQueueIndex++;
                            firstLineToResendIndex++;
                        }
                    }
                    else if (printWasCanceled)
                    {
                        CommunicationState = CommunicationStates.Connected;
                        // never leave the extruder and the bed hot
                        ReleaseMotors();
                        TargetExtruderTemperature = 0;
                        TargetBedTemperature = 0;
                        printWasCanceled = false;
                    }
                    else
                    {
                        if (printerCommandQueueIndex == loadedGCode.Count)
                        {
                            CommunicationState = CommunicationStates.FinishedPrint;

                            printJobDisplayName = null;
                            
                            // never leave the extruder and the bed hot
                            ReleaseMotors();
                            TargetExtruderTemperature = 0;
                            TargetBedTemperature = 0;                            
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
        }

        public void RequestPause()
        {
            if (PrinterIsPrinting)
            {
                // Add the pause_gcode to the loadedGCode.GCodeCommandQueue
                string pauseGCode = ActiveSliceSettings.Instance.GetActiveValue("pause_gcode");
                if (pauseGCode.Trim() == "")
                {
                    DoPause();
                }
                else
                {
                    using (TimedLock.Lock(this, "RequestPause"))
                    {
                        double currentFeedRate = loadedGCode.Instruction(printerCommandQueueIndex).FeedRate;
                        int lastIndexAdded = InjectGCode(pauseGCode, printerCommandQueueIndex);

                        // inject a marker to tell when we are done with the inserted pause code
                        lastIndexAdded = InjectGCode("MH_PAUSE", lastIndexAdded);

                        // inject the resume_gcode to execute when we resume printing
                        string resumeGCode = ActiveSliceSettings.Instance.GetActiveValue("resume_gcode");
                        lastIndexAdded = InjectGCode(resumeGCode, lastIndexAdded);

                        lastIndexAdded = InjectGCode("G1 F{0}".FormatWith(currentFeedRate), lastIndexAdded);
                    }
                }
            }
        }

        void DoPause()
        {
            if (PrinterIsPrinting)
            {
                CommunicationState = CommunicationStates.Paused;
                if (sendGCodeToPrinterThread != null)
                {
                    sendGCodeToPrinterThread.Join();
                }
                sendGCodeToPrinterThread = null;
            }
        }

        private int InjectGCode(string codeToInject, int indexToStartInjection)
        {
            codeToInject = codeToInject.Replace("\\n", "\n");
            string[] lines = codeToInject.Split('\n');

            int linesAdded = 0;
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string[] splitOnSemicolon = lines[i].Split(';');
                string trimedLine = splitOnSemicolon[0].Trim().ToUpper();
                if (trimedLine != "")
                {
                    if (loadedGCode.Count > indexToStartInjection)
                    {
                        loadedGCode.Insert(indexToStartInjection, new PrinterMachineInstruction(trimedLine, loadedGCode.Instruction(indexToStartInjection)));
                    }
                    else
                    {
                        loadedGCode.Add(new PrinterMachineInstruction(trimedLine));
                    }
                    linesAdded++;
                }
            }

            return indexToStartInjection + linesAdded;
        }

        public void Resume()
        {
            if (PrinterIsPaused)
            {
                CommunicationState = CommunicationStates.Printing;
                sendGCodeToPrinterThread = new Thread(SendCurrentGCodeFileToPrinter);
                sendGCodeToPrinterThread.Name = "sendGCodeToPrinterThread - Resume";
                sendGCodeToPrinterThread.IsBackground = true;
                sendGCodeToPrinterThread.Start();
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
            printerCommandQueueIndex = 0;
            lastRemainingSecondsReported = 0;

            allCheckSumLinesSent.Clear();
            WriteChecksumLineToPrinter("M110 S1");
            firstLineToResendIndex = 1;
        }

        public void Stop()
        {
            switch (CommunicationState)
            {
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
                                InjectGCode(cancelGCode, printerCommandQueueIndex);
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
                        if (sendGCodeToPrinterThread != null)
                        {
                            sendGCodeToPrinterThread.Join();
                            sendGCodeToPrinterThread = null;
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

                case CommunicationStates.AttemptingToConnect:
                    CommunicationState = CommunicationStates.FailedToConnect;
                    connectThread.Join();
                    CommunicationState = CommunicationStates.Disconnecting;
                    if (readFromPrinterThread != null)
                    {
                        readFromPrinterThread.Join();
                    }
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

        public bool StartPrint(string gcodeFileContents, int startIndex = 0)
        {   
            gcodeFileContents = gcodeFileContents.Replace("\r\n", "\n");
            gcodeFileContents = gcodeFileContents.Replace('\r', '\n');
            string[] gcodeLines = gcodeFileContents.Split('\n');
            List<string> printableGCode = new List<string>();
            foreach (string line in gcodeLines)
            {
                string[] splitOnSemicolon = line.Split(';');
                string trimedLine = splitOnSemicolon[0].Trim().ToUpper();
                if (trimedLine.Length < 1)
                {
                    continue;
                }

                printableGCode.Add(trimedLine);
            }

            //Is there a reason this check doesn't happen earlier? (KP)
            if (!PrinterIsConnected || PrinterIsPrinting)
            {
                return false;
            }

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

            ExtrusionRatio = 1;
            FeedRateRatio = 1;

            CommunicationState = CommunicationStates.Printing;
            ClearQueuedGCode();
            loadedGCode = GCodeFile.ParseGCodeString(string.Join("\n", printableGCode.ToArray()));

            if (printableGCode.Count == 0)
            {
                return true;
            }

            sendGCodeToPrinterThread = new Thread(SendCurrentGCodeFileToPrinter);
            sendGCodeToPrinterThread.Name = "sendGCodeToPrinterThread - StartPrint";
            sendGCodeToPrinterThread.IsBackground = true;
            sendGCodeToPrinterThread.Start();

            return true;
        }

        string lineBeingRead = "";
        string lastLineRead = "";
        public void ReadFromPrinter()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            timeSinceLastReadAnything.Restart();
            // we want this while loop to be as fast as possible. Don't allow any significant work to happen in here
            while (CommunicationState == CommunicationStates.AttemptingToConnect
                || (PrinterIsConnected && serialPort.IsOpen && !Disconnecting))
            {
                try
                {
                    while (serialPort.BytesToRead > 0)
                    {
                        char nextChar = (char)serialPort.ReadChar();
                        using (TimedLock.Lock(this, "ReadFromPrinter"))
                        {
                            if (nextChar == '\r' || nextChar == '\n')
                            {
                                lastLineRead = lineBeingRead;
                                lineBeingRead = "";

                                // process this command
                                {
                                    StringEventArgs currentEvent = new StringEventArgs(lastLineRead);
                                    CommunicationUnconditionalFromPrinter.CallEvents(this, currentEvent);

                                    FoundStringEventArgs foundResponse = new FoundStringEventArgs(currentEvent.Data);
                                    ReadLineStartCallBacks.CheckForKeys(foundResponse);
                                    ReadLineContainsCallBacks.CheckForKeys(foundResponse);

                                    if (foundResponse.SendToDelegateFunctions)
                                    {
                                        ReadLine.CallEvents(this, currentEvent);
                                    }
                                }

                                if (CommunicationState == CommunicationStates.AttemptingToConnect)
                                {
                                    CommunicationState = CommunicationStates.Connected;
                                }
                            }
                            else
                            {
                                lineBeingRead += nextChar;
                            }
                            timeSinceLastReadAnything.Restart();
                        }
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
                catch (InvalidOperationException)
                {
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
    }
}
