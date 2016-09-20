using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;
using System;


namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class HardwareLevelingUITests
	{
		[Test, RequiresSTA, RunInApplicationDomain]
		public void HasHardwareLevelingHidesLevelingSettings()
		{
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);
					//Add printer that has hardware leveling
					MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

					testRunner.Wait(1);
					testRunner.ClickByName("SettingsAndControls", 1);
					testRunner.Wait(1);
					testRunner.ClickByName("Slice Settings Tab", 1);
					testRunner.ClickByName("User Level Dropdown", 1);
					testRunner.ClickByName("Advanced Menu Item", 1);
					testRunner.Wait(.5);
					testRunner.ClickByName("Printer Tab", 1);
					testRunner.Wait(1);

					//Make sure Print Leveling tab is not visible 
					bool testPrintLeveling = testRunner.WaitForName("Print Leveling Tab", 3);
					resultsHarness.AddTestResult(testPrintLeveling == false);

					//Add printer that does not have hardware leveling
					MatterControlUtilities.AddAndSelectPrinter(testRunner, "3D Factory", "MendelMax 1.5");
					testRunner.Wait(.2);
					testRunner.ClickByName("Slice Settings Tab",1);
					testRunner.ClickByName("Printer Tab",1);

					//Make sure Print Leveling tab is visible
					bool printLevelingVisible = testRunner.WaitForName("Print Leveling Tab", 2);
					resultsHarness.AddTestResult(printLevelingVisible == true);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, overrideHeight: 800);

			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}
}

