using System;
using System.Threading;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class ShowTerminalButtonClickedOpensTerminal
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void ClickingShowTerminalButtonOpensTerminal()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);
				testRunner.ClickByName("SettingsAndControls", 5);
				testRunner.Wait(2);
				testRunner.ClickByName("Options Tab", 6);

				bool terminalWindowExists1 = testRunner.WaitForName("Gcode Terminal", 0);
				testRunner.AddTestResult(terminalWindowExists1 == false, "Terminal Window does not exist");

				testRunner.ClickByName("Show Terminal Button", 6);
				testRunner.Wait(1);

				SystemWindow containingWindow;
				GuiWidget terminalWindow = testRunner.GetWidgetByName("Gcode Terminal", out containingWindow, 3);
				testRunner.AddTestResult(terminalWindow != null, "Terminal Window exists after Show Terminal button is clicked");
				containingWindow.CloseOnIdle();
				testRunner.Wait(.5);

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class ConfigureNotificationSettingsButtonClickedOpensNotificationWindow
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain, Category("FixNeeded" /* Not Finished */)]
		//DOES NOT WORK
		public void ClickingConfigureNotificationSettingsButtonOpensWindow()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);

				testRunner.ClickByName("SettingsAndControls", 5);
				testRunner.ClickByName("Options Tab", 6);

				bool printNotificationsWindowExists1 = testRunner.WaitForName("Notification Options Window", 3);
				testRunner.AddTestResult(printNotificationsWindowExists1 == false, "Print Notification Window does not exist");

				testRunner.ClickByName("Configure Notification Settings Button", 6);
				bool printNotificationsWindowExists2 = testRunner.WaitForName("Notification Options Window", 3);
				testRunner.AddTestResult(printNotificationsWindowExists2 == true, "Print Notifications Window exists after Configure button is clicked");

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, "MC_Three_Queue_Items", defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}
}
