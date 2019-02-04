namespace MatterHackers.SerialPortCommunication.FrostedSerial
{
	public interface IFrostedSerialPortFactory
	{
		bool IsWindows { get; }

		bool SerialPortAlreadyOpen(string portName);

		IFrostedSerialPort Create(string serialPortName);
		IFrostedSerialPort CreateAndOpen(string serialPortName, int baudRate, bool DtrEnableOnConnect);
	}
}
