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
using System.Diagnostics;
using System.IO;
using MatterControl.Common.Repository;
using MatterControl.Printing.Pipelines;
using MatterHackers.MatterControl;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using MatterHackers.VectorMath;

namespace MatterControl.Printing
{
	public class RemotePrinterConnection : IPrinterConnection
	{

		private IPrinterConnection remoteConnection;

		public RemotePrinterConnection(PrintHostConfig printer)
		{
			// - What do we do here, write the events and listeners on the MC side that actually
			//    exist on the print server side?
			//
			// - Add all of the events into the SignalR system so that non-MC clients can use them as well?
			//
			// We should think through why PrinterConnection needs to be what it is...
		}

		public event EventHandler AtxPowerStateChanged;

		public event EventHandler BedTargetTemperatureChanged;

		public event EventHandler BedTemperatureRead;

		public event EventHandler CommunicationStateChanged;

		public event EventHandler<ConnectFailedEventArgs> ConnectionFailed;

		public event EventHandler ConnectionSucceeded;

		public event EventHandler DestinationChanged;

		public event EventHandler DetailedPrintingStateChanged;

		public event EventHandler Disposed;

		public event EventHandler<DeviceErrorArgs> ErrorReported;

		public event EventHandler FanSpeedSet;

		public event EventHandler<PrintPauseEventArgs> FilamentRunout;

		public event EventHandler FirmwareVersionRead;

		public event EventHandler HomingPositionChanged;

		public event EventHandler<int> HotendTargetTemperatureChanged;

		public event EventHandler HotendTemperatureRead;

		public event EventHandler<string> LineReceived;

		public event EventHandler<string> LineSent;

		public event EventHandler<PrintPauseEventArgs> PauseOnLayer;

		public event EventHandler PrintCanceled;

		public event EventHandler<string> PrintFinished;

		public event EventHandler TemporarilyHoldingTemp;

		public int ActiveExtruderIndex => remoteConnection.ActiveExtruderIndex;

		public string ActivePrintName => remoteConnection.ActivePrintName;

		public PrintJob ActivePrintTask { get => remoteConnection.ActivePrintTask; set => remoteConnection.ActivePrintTask = value; }

		public double ActualBedTemperature => remoteConnection.ActualBedTemperature;

		public bool AllowLeveling { set => hubConnection.InvokeAsync("AllowLeveling", value).ConfigureAwait(false); }

		public bool AnyHeatIsOn => remoteConnection.AnyHeatIsOn;

		public bool AtxPowerEnabled { get => remoteConnection.AtxPowerEnabled; set => remoteConnection.AtxPowerEnabled = value; }

		public bool AutoReleaseMotors => remoteConnection.AutoReleaseMotors;

		public int BaudRate => remoteConnection.BaudRate;

		public bool CalibrationPrint => remoteConnection.CalibrationPrint;

		public string CancelGCode => remoteConnection.CancelGCode;

		public CommunicationStates CommunicationState { get => remoteConnection.CommunicationState; set => remoteConnection.CommunicationState = value; }

		public string ComPort => remoteConnection.ComPort;

		public string ConnectGCode => remoteConnection.ConnectGCode;

		public bool ContinueHoldingTemperature { get => remoteConnection.ContinueHoldingTemperature; set => remoteConnection.ContinueHoldingTemperature = value; }

		public Vector3 CurrentDestination => remoteConnection.CurrentDestination;

		public double CurrentExtruderDestination => remoteConnection.CurrentExtruderDestination;

		public int CurrentlyPrintingLayer => remoteConnection.CurrentlyPrintingLayer;

		public DetailedPrintingState DetailedPrintingState { get => remoteConnection.DetailedPrintingState; set => remoteConnection.DetailedPrintingState = value; }

		public string DeviceCode => remoteConnection.DeviceCode;

		public bool Disconnecting => remoteConnection.Disconnecting;

		public string DriverType => remoteConnection.DriverType;

		public bool EnableNetworkPrinting => remoteConnection.EnableNetworkPrinting;

		public int ExtruderCount => remoteConnection.ExtruderCount;

		public double FanSpeed0To255 { get => remoteConnection.FanSpeed0To255; set => remoteConnection.FanSpeed0To255 = value; }

		public double FeedRateRatio => remoteConnection.FeedRateRatio;

		public bool FilamentPositionSensorDetected => remoteConnection.FilamentPositionSensorDetected;

		public FirmwareTypes FirmwareType => remoteConnection.FirmwareType;

		public string FirmwareVersion => remoteConnection.FirmwareVersion;

		public Vector3 HomingPosition => remoteConnection.HomingPosition;

		public bool IsConnected => remoteConnection.IsConnected;

		public Vector3 LastReportedPosition => remoteConnection.LastReportedPosition;

		public bool MonitorPrinterTemperature { get => remoteConnection.MonitorPrinterTemperature; set => remoteConnection.MonitorPrinterTemperature = value; }

		public int NumQueuedCommands => remoteConnection.NumQueuedCommands;

		public bool Paused => remoteConnection.Paused;

		public double PercentComplete => remoteConnection.PercentComplete;

		public CommunicationStates PrePauseCommunicationState => remoteConnection.PrePauseCommunicationState;

		public PrintHostConfig Printer => remoteConnection.Printer;

		public bool Printing => remoteConnection.Printing;

		public bool PrintIsActive => remoteConnection.PrintIsActive;

		public bool PrintIsFinished => remoteConnection.PrintIsFinished;

		public string PrintJobName => remoteConnection.PrintJobName;

		public bool PrintWasCanceled { get => remoteConnection.PrintWasCanceled; set => remoteConnection.PrintWasCanceled = value; }

		public double RatioIntoCurrentLayerInstructions => remoteConnection.RatioIntoCurrentLayerInstructions;

		public double RatioIntoCurrentLayerSeconds => remoteConnection.RatioIntoCurrentLayerSeconds;

		public bool RecoveryIsEnabled => remoteConnection.RecoveryIsEnabled;

		public int SecondsPrinted => remoteConnection.SecondsPrinted;

		public int SecondsToEnd => remoteConnection.SecondsToEnd;

		public double SecondsToHoldTemperature => remoteConnection.SecondsToHoldTemperature;

		public bool SendWithChecksum => remoteConnection.SendWithChecksum;

		public IFrostedSerialPort serialPort => remoteConnection.serialPort;

		public double TargetBedTemperature { get => remoteConnection.TargetBedTemperature; set => remoteConnection.TargetBedTemperature = value; }

		public int TimeToHoldTemperature { get => remoteConnection.TimeToHoldTemperature; set => remoteConnection.TimeToHoldTemperature = value; }

		public int TotalLayersInPrint => remoteConnection.TotalLayersInPrint;

		public int TotalSecondsInPrint => remoteConnection.TotalSecondsInPrint;

		public bool WaitingForPositionRead => remoteConnection.WaitingForPositionRead;

		public void ArduinoDtrReset()
		{
			remoteConnection.ArduinoDtrReset();
		}

		public void Connect()
		{
			remoteConnection.Connect();
		}

		public void DeleteFileFromSdCard(string fileName)
		{
			remoteConnection.DeleteFileFromSdCard(fileName);
		}

		public void Disable()
		{
			remoteConnection.Disable();
		}

		public void Dispose()
		{
			remoteConnection.Dispose();
		}

		public double GetActualHotendTemperature(int hotendIndex0Based)
		{
			return remoteConnection.GetActualHotendTemperature(hotendIndex0Based);
		}

		public double GetTargetHotendTemperature(int hotendIndex0Based)
		{
			return remoteConnection.GetTargetHotendTemperature(hotendIndex0Based);
		}

		// TODO: Review Invalid - seems more low level than required. MatterControl should have simple Connect interface only
		public void HaltConnectionThread()
		{
			remoteConnection.HaltConnectionThread();
		}

		public void HomeAxis(PrinterAxis axis)
		{
			remoteConnection.HomeAxis(axis);
		}

		public void MacroCancel()
		{
			remoteConnection.MacroCancel();
		}

		public void MacroStart()
		{
			remoteConnection.MacroStart();
		}

		public void MoveAbsolute(PrinterAxis axis, double axisPositionMm, double feedRateMmPerMinute)
		{
			remoteConnection.MoveAbsolute(axis, axisPositionMm, feedRateMmPerMinute);
		}

		public void MoveAbsolute(Vector3 position, double feedRateMmPerMinute)
		{
			remoteConnection.MoveAbsolute(position, feedRateMmPerMinute);
		}

		public void MoveExtruderRelative(double moveAmountMm, double feedRateMmPerMinute, int extruderNumber = 0)
		{
			remoteConnection.MoveExtruderRelative(moveAmountMm, feedRateMmPerMinute, extruderNumber);
		}

		public void MoveRelative(PrinterAxis axis, double moveAmountMm, double feedRateMmPerMinute)
		{
			remoteConnection.MoveRelative(axis, moveAmountMm, feedRateMmPerMinute);
		}

		public void QueueLine(string lineToWrite, bool forceTopOfQueue = false)
		{
			remoteConnection.QueueLine(lineToWrite, forceTopOfQueue);
		}

		public void ReadPosition(PositionReadType positionReadType = PositionReadType.Other, bool forceToTopOfQueue = false)
		{
			remoteConnection.ReadPosition();
		}

		public void RebootBoard()
		{
			remoteConnection.RebootBoard();
		}

		public void ReleaseMotors(bool forceRelease = false)
		{
			remoteConnection.ReleaseMotors(forceRelease);
		}

		public void RequestPause()
		{
			remoteConnection.RequestPause();
		}

		public void ResetToReadyState()
		{
			remoteConnection.ResetToReadyState();
		}

		public void Resume()
		{
			remoteConnection.Resume();
		}

		public void SetMovementToAbsolute()
		{
			remoteConnection.SetMovementToAbsolute();
		}

		public void SetMovementToRelative()
		{
			remoteConnection.SetMovementToRelative();
		}

		public void SetTargetHotendTemperature(int hotendIndex0Based, double temperature, bool forceSend = false)
		{
			remoteConnection.SetTargetHotendTemperature(hotendIndex0Based, temperature, forceSend);
		}

		public void StartPrint(PrintJob printTask, bool calibrationPrint = false)
		{
			remoteConnection.StartPrint(printTask, calibrationPrint);
		}

		public void StartPrint(Stream gcodeStream, PrintJob printTask, bool calibrationPrint = false)
		{
			remoteConnection.StartPrint(gcodeStream, printTask, calibrationPrint);
		}

		public bool StartSdCardPrint(string m23FileName)
		{
			return remoteConnection.StartSdCardPrint(m23FileName);
		}

		public void Stop(bool markPrintCanceled = true)
		{
			remoteConnection.Stop();
		}

		public void SwitchToGCode(string gCodeFilePath)
		{
			remoteConnection.SwitchToGCode(gCodeFilePath);
		}

		public void TurnOffBedAndExtruders(TurnOff turnOffTime)
		{
			remoteConnection.TurnOffBedAndExtruders(turnOffTime);
		}
	}
}
