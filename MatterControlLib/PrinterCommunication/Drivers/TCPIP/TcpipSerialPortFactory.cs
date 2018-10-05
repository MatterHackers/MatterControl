using System.Net;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.SerialPortCommunication.FrostedSerial;

namespace TcpipDriver
{
	public class TcpipSerialPortFactory : FrostedSerialPortFactory
	{
		public override bool SerialPortAlreadyOpen(string portName) => false;

		protected override string GetDriverType() => "TCPIP";

		public override IFrostedSerialPort Create(string serialPortName)
		{
			return new TcpipSerialPort(ApplicationController.Instance.ActivePrinter, serialPortName);
		}

		public override bool SerialPortIsAvailable(string serialPortName)
		{
			var printer = ApplicationController.Instance.ActivePrinter;

			return int.TryParse(printer.Settings.GetValue(SettingsKey.ip_port), out _)
				&& IPAddress.TryParse(printer.Settings.GetValue(SettingsKey.ip_address), out _);
		}
	}
}
