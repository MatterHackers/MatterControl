using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.Agg.PlatformAbstract;
using System.IO;
using MatterHackers.MatterControl.CreatorPlugins;
using MatterHackers.Agg.UI.Tests;


namespace MatterHackers.MatterControl.UI
{
	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
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

					testRunner.ClickByName("SettingsAndControls", 5);
					testRunner.Wait(2);
					testRunner.ClickByName("Options Tab", 6);

					bool terminalWindowExists1 = testRunner.WaitForName("Gcode Terminal", 0);
					resultsHarness.AddTestResult(terminalWindowExists1 == false, "Terminal Window does not exist");

					testRunner.ClickByName("Show Terminal Button", 6);
		
					SystemWindow containingWindow;
					GuiWidget terminalWindow = testRunner.GetWidgetByName("Gcode Terminal", out containingWindow, secondsToWait: 3);
					resultsHarness.AddTestResult(terminalWindow != null, "Terminal Window exists after Show Terminal button is clicked");
					containingWindow.CloseOnIdle();
					testRunner.Wait(.5);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

#if !__ANDROID__
			// Set the static data to point to the directory of MatterControl
			StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData(Path.Combine("..", "..", "..", "..", "StaticData"));
#endif
			bool showWindow;
			string testDBFolder = "MC_Three_Queue_Items";
			MatterControlUtilities.DataFolderState staticDataState = MatterControlUtilities.MakeNewStaticDataForTesting(testDBFolder);
			MatterControlApplication matterControlWindow = MatterControlApplication.CreateInstance(out showWindow);
			AutomationTesterHarness testHarness = AutomationTesterHarness.ShowWindowAndExectueTests(matterControlWindow, testToRun, 45);
			MatterControlUtilities.RestoreStaticDataAfterTesting(staticDataState, true);
			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2); // make sure we ran all our tests
		}
	}

	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
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

					testRunner.ClickByName("SettingsAndControls", secondsToWait: 5);
					testRunner.ClickByName("Options Tab", secondsToWait: 6);

					bool printNotificationsWindowExists1 = testRunner.WaitForName("Notification Options Window", secondsToWait: 3);
					resultsHarness.AddTestResult(printNotificationsWindowExists1 == false, "Print Notification Window does not exist");

					testRunner.ClickByName("Configure Notification Settings Button", secondsToWait: 6);
					bool printNotificationsWindowExists2 = testRunner.WaitForName("Notification Options Window", secondsToWait: 3);
					resultsHarness.AddTestResult(printNotificationsWindowExists2 == true, "Print Notifications Window exists after Configure button is clicked");

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

#if !__ANDROID__
			// Set the static data to point to the directory of MatterControl
			StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData(Path.Combine("..", "..", "..", "..", "StaticData"));
#endif
			bool showWindow;
			string testDBFolder = "MC_Three_Queue_Items";
			MatterControlUtilities.DataFolderState staticDataState = MatterControlUtilities.MakeNewStaticDataForTesting(testDBFolder);
			MatterControlApplication matterControlWindow = MatterControlApplication.CreateInstance(out showWindow);
			AutomationTesterHarness testHarness = AutomationTesterHarness.ShowWindowAndExectueTests(matterControlWindow, testToRun, 45);
			MatterControlUtilities.RestoreStaticDataAfterTesting(staticDataState, true);
			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2); // make sure we ran all our tests
		}
	}

}
