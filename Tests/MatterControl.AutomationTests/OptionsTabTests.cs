using System;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class ShowTerminalButtonClickedOpensTerminal
	{
		[Test, Apartment(ApartmentState.STA)]
		public async Task ClickingShowTerminalButtonOpensTerminal()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();
				testRunner.ClickByName("SettingsAndControls", 5);
				testRunner.Delay(2);
				testRunner.ClickByName("Options Tab", 6);

				bool terminalWindowExists1 = testRunner.WaitForName("Gcode Terminal", 0);
				Assert.IsTrue(terminalWindowExists1 == false, "Terminal Window does not exist");

				testRunner.ClickByName("Show Terminal Button", 6);
				testRunner.Delay(1);

				SystemWindow containingWindow;
				GuiWidget terminalWindow = testRunner.GetWidgetByName("Gcode Terminal", out containingWindow, 3);
				Assert.IsTrue(terminalWindow != null, "Terminal Window exists after Show Terminal button is clicked");
				containingWindow.CloseOnIdle();
				testRunner.Delay(.5);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

	}
}
