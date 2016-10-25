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
				MatterControlUtilities.PrepForTestRun(testRunner);
				testRunner.ClickByName("SettingsAndControls", 5);
				testRunner.Wait(2);
				testRunner.ClickByName("Options Tab", 6);

				bool terminalWindowExists1 = testRunner.WaitForName("Gcode Terminal", 0);
				Assert.IsTrue(terminalWindowExists1 == false, "Terminal Window does not exist");

				testRunner.ClickByName("Show Terminal Button", 6);
				testRunner.Wait(1);

				SystemWindow containingWindow;
				GuiWidget terminalWindow = testRunner.GetWidgetByName("Gcode Terminal", out containingWindow, 3);
				Assert.IsTrue(terminalWindow != null, "Terminal Window exists after Show Terminal button is clicked");
				containingWindow.CloseOnIdle();
				testRunner.Wait(.5);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test, Apartment(ApartmentState.STA), Category("FixNeeded" /* Not Finished */)]
		public async Task ConfigureNotificationSettingsButtonOpensNotificationWindow()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);

				testRunner.ClickByName("SettingsAndControls", 5);
				testRunner.ClickByName("Options Tab", 6);

				bool printNotificationsWindowExists1 = testRunner.WaitForName("Notification Options Window", 3);
				Assert.IsTrue(printNotificationsWindowExists1 == false, "Print Notification Window does not exist");

				testRunner.ClickByName("Configure Notification Settings Button", 6);
				bool printNotificationsWindowExists2 = testRunner.WaitForName("Notification Options Window", 3);
				Assert.IsTrue(printNotificationsWindowExists2 == true, "Print Notifications Window exists after Configure button is clicked");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, "MC_Three_Queue_Items");
		}
	}
}
