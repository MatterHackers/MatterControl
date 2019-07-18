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
	public interface IPrinterConnection
	{
		int ActiveExtruderIndex { get; }

		string ActivePrintName { get; }

		PrintJob ActivePrintTask { get; set; }

		double ActualBedTemperature { get; }

		bool AllowLeveling { set; }

		bool AnyHeatIsOn { get; }

		bool AtxPowerEnabled { get; set; }

		bool AutoReleaseMotors { get; }

		int BaudRate { get; }

		bool CalibrationPrint { get; }

		string CancelGCode { get; }

		CommunicationStates CommunicationState { get; set; }

		string ComPort { get; }

		string ConnectGCode { get; }

		bool ContinueHoldingTemperature { get; set; }

		Vector3 CurrentDestination { get; }

		double CurrentExtruderDestination { get; }

		int CurrentlyPrintingLayer { get; }

		DetailedPrintingState DetailedPrintingState { get; set; }

		string DeviceCode { get; }

		bool Disconnecting { get; }

		string DriverType { get; }

		bool EnableNetworkPrinting { get; }

		int ExtruderCount { get; }

		double FanSpeed0To255 { get; set; }

		double FeedRateRatio { get; }

		bool FilamentPositionSensorDetected { get; }

		FirmwareTypes FirmwareType { get; }

		string FirmwareVersion { get; }

		Vector3 HomingPosition { get; }

		bool IsConnected { get; }

		Vector3 LastReportedPosition { get; }

		bool MonitorPrinterTemperature { get; set; }

		int NumQueuedCommands { get; }

		bool Paused { get; }

		double PercentComplete { get; }

		CommunicationStates PrePauseCommunicationState { get; }

		PrintHostConfig Printer { get; }

		bool Printing { get; }

		bool PrintIsActive { get; }

		bool PrintIsFinished { get; }

		string PrintJobName { get; }

		bool PrintWasCanceled { get; set; }

		double RatioIntoCurrentLayerInstructions { get; }

		double RatioIntoCurrentLayerSeconds { get; }

		bool RecoveryIsEnabled { get; }

		int SecondsPrinted { get; }

		int SecondsToEnd { get; }

		double SecondsToHoldTemperature { get; }

		bool SendWithChecksum { get; }

		IFrostedSerialPort serialPort { get; }

		double TargetBedTemperature { get; set; }

		Stopwatch TimeHaveBeenHoldingTemperature { get; set; }

		int TimeToHoldTemperature { get; set; }

		GCodeStream TotalGCodeStream { get; }

		int TotalLayersInPrint { get; }

		int TotalSecondsInPrint { get; }

		bool WaitingForPositionRead { get; }

		event EventHandler AtxPowerStateChanged;

		event EventHandler BedTargetTemperatureChanged;

		event EventHandler BedTemperatureRead;

		event EventHandler CommunicationStateChanged;

		event EventHandler<ConnectFailedEventArgs> ConnectionFailed;

		event EventHandler ConnectionSucceeded;

		event EventHandler DestinationChanged;

		event EventHandler DetailedPrintingStateChanged;

		event EventHandler Disposed;

		event EventHandler<DeviceErrorArgs> ErrorReported;

		event EventHandler FanSpeedSet;

		event EventHandler<PrintPauseEventArgs> FilamentRunout;

		event EventHandler FirmwareVersionRead;

		event EventHandler HomingPositionChanged;

		event EventHandler<int> HotendTargetTemperatureChanged;

		event EventHandler HotendTemperatureRead;

		event EventHandler<string> LineReceived;

		event EventHandler<string> LineSent;

		event EventHandler<PrintPauseEventArgs> PauseOnLayer;

		event EventHandler PrintCanceled;

		event EventHandler<string> PrintFinished;

		event EventHandler TemporarilyHoldingTemp;

		void ArduinoDtrReset();

		void BedTemperatureWasWritenToPrinter(string line);

		void Connect();

		void DeleteFileFromSdCard(string fileName);

		void Disable();

		void Dispose();

		void FanOffWasWritenToPrinter(string line);

		void FanSpeedWasWritenToPrinter(string line);

		void FoundStart(string line);

		double GetActualHotendTemperature(int hotendIndex0Based);

		double GetTargetHotendTemperature(int hotendIndex0Based);

		void HaltConnectionThread();

		void HomeAxis(PrinterAxis axis);

		void HotendTemperatureWasWritenToPrinter(string line);

		void InitializeReadLineReplacements();

		void LogError(string message, ErrorSource source);

		void MacroCancel();

		void MacroStart();

		void MoveAbsolute(PrinterAxis axis, double axisPositionMm, double feedRateMmPerMinute);

		void MoveAbsolute(Vector3 position, double feedRateMmPerMinute);

		void MoveExtruderRelative(double moveAmountMm, double feedRateMmPerMinute, int extruderNumber = 0);

		void MoveRelative(PrinterAxis axis, double moveAmountMm, double feedRateMmPerMinute);

		void OnConnectionFailed(ConnectionFailure reason, string message = null, string exceptionType = null);

		void OnFilamentRunout(PrintPauseEventArgs printPauseEventArgs);

		void OnPauseOnLayer(PrintPauseEventArgs printPauseEventArgs);

		void PrinterReportsError(string line);

		void PrinterRequestsResend(string line);

		void QueueLine(string lineToWrite, bool forceTopOfQueue = false);

		void ReadPosition(PositionReadType positionReadType = PositionReadType.Other, bool forceToTopOfQueue = false);

		void ReadTargetPositions(string line);

		void ReadTemperatures(string line);

		void RebootBoard();

		void ReleaseAndReportFailedConnection(ConnectionFailure reason, string message = null);

		void ReleaseMotors(bool forceRelease = false);

		void RequestPause();

		void ResetToReadyState();

		void Resume();

		bool SerialPortIsAvailable(string portName);

		void SetMovementToAbsolute();

		void SetMovementToRelative();

		void SetTargetHotendTemperature(int hotendIndex0Based, double temperature, bool forceSend = false);

		void StartPrint(PrintJob printTask, bool calibrationPrint = false);

		void StartPrint(Stream gcodeStream, PrintJob printTask, bool calibrationPrint = false);

		bool StartSdCardPrint(string m23FileName);

		void Stop(bool markPrintCanceled = true);

		void SuppressEcho(string line);

		void SwitchToGCode(string gCodeFilePath);

		void TurnOffBedAndExtruders(TurnOff turnOffTime);
	}
}