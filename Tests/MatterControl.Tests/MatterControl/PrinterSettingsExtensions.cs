using MatterControl.Printing;
using MatterHackers.MatterControl;

namespace MatterControl.Tests.MatterControl
{
	public static class PrinterSettingsExtensions
	{
		public static PrintHostConfig Shim(this PrinterConfig printer)
		{
			return ApplicationController.Instance.Shim(printer);
		}
	}
}
