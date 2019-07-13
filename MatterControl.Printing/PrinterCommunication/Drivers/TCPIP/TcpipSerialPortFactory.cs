using System.Net;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.SerialPortCommunication.FrostedSerial;

namespace TcpipDriver
{
	public class TcpipSerialPortFactory : FrostedSerialPortFactory
	{
		public override bool SerialPortAlreadyOpen(string portName) => false;

		protected override string GetDriverType() => "TCPIP";

		public override IFrostedSerialPort Create(string serialPortName, PrinterSettings settings)
		{
			return new TcpipSerialPort(settings);
		}

		public override bool SerialPortIsAvailable(string serialPortName, PrinterSettings settings)
		{
			return int.TryParse(settings.GetValue(SettingsKey.ip_port), out _)
				&& IPAddress.TryParse(settings.GetValue(SettingsKey.ip_address), out _);
		}
	}
}
