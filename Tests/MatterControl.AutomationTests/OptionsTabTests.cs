using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class ShowTerminalButtonClickedOpensTerminal
	{
		[Test, Apartment(ApartmentState.STA)]
		public async Task ClickingShowTerminalButtonOpensTerminal()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();
				testRunner.ClickByName("Options Tab", 6);

				Assert.IsFalse(testRunner.WaitForName("Gcode Terminal", 0.5), "Terminal Window should not exist");

				testRunner.ClickByName("Show Terminal Button", 6);
				testRunner.Delay(1);

				SystemWindow containingWindow;
				GuiWidget terminalWindow = testRunner.GetWidgetByName("Gcode Terminal", out containingWindow, 3);
				Assert.IsNotNull(terminalWindow, "Terminal Window should exists after Show Terminal button is clicked");
				containingWindow.CloseOnIdle();
				testRunner.Delay(.5);

				return Task.CompletedTask;
			});
		}
	}
}
