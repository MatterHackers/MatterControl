using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;
using System;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class ShowTerminalButtonClickedOpensTerminal
	{
		[Test, RequiresSTA, RunInApplicationDomain]
		public void ClickingShowTerminalButtonOpensTerminal()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);
					testRunner.ClickByName("SettingsAndControls", 5);
					testRunner.Wait(2);
					testRunner.ClickByName("Options Tab", 6);

					bool terminalWindowExists1 = testRunner.WaitForName("Gcode Terminal", 0);
					resultsHarness.AddTestResult(terminalWindowExists1 == false, "Terminal Window does not exist");

					testRunner.ClickByName("Show Terminal Button", 6);
					testRunner.Wait(1);

					SystemWindow containingWindow;
					GuiWidget terminalWindow = testRunner.GetWidgetByName("Gcode Terminal", out containingWindow, 3);
					resultsHarness.AddTestResult(terminalWindow != null, "Terminal Window exists after Show Terminal button is clicked");
					containingWindow.CloseOnIdle();
					testRunner.Wait(.5);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class ConfigureNotificationSettingsButtonClickedOpensNotificationWindow
	{
		[Test, RequiresSTA, RunInApplicationDomain, Ignore("Not Finished")]
		//DOES NOT WORK
		public void ClickingConfigureNotificationSettingsButtonOpensWindow()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					testRunner.ClickByName("SettingsAndControls", 5);
					testRunner.ClickByName("Options Tab", 6);

					bool printNotificationsWindowExists1 = testRunner.WaitForName("Notification Options Window", 3);
					resultsHarness.AddTestResult(printNotificationsWindowExists1 == false, "Print Notification Window does not exist");

					testRunner.ClickByName("Configure Notification Settings Button", 6);
					bool printNotificationsWindowExists2 = testRunner.WaitForName("Notification Options Window", 3);
					resultsHarness.AddTestResult(printNotificationsWindowExists2 == true, "Print Notifications Window exists after Configure button is clicked");

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, "MC_Three_Queue_Items");

			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}
}
