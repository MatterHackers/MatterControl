using MatterHackers.Plugins.X3GDriver;
using MatterHackers.SerialPortCommunication.FrostedSerial;

namespace MatterHackers.MatterControl.Plugins.X3GDriver
{
	public class X3GFrostedSerialPortFactory : FrostedSerialPortFactory
	{
		override protected string GetDriverType() => "X3G";

		public override IFrostedSerialPort Create(string serialPortName)
		{
			return new X3GSerialPortWrapper(serialPortName);
		}
	}
}