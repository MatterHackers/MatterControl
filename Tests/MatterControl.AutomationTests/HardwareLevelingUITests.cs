using System;
using System.Diagnostics;
using System.Threading;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class HardwareLevelingUITests
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void HasHardwareLevelingHidesLevelingSettings()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);
				//Add printer that has hardware leveling
				MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

				MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

				testRunner.ClickByName("Printer Tab", 1);
				testRunner.Wait(1);

				//Make sure Print Leveling tab is not visible 
				bool testPrintLeveling = testRunner.WaitForName("Print Leveling Tab", 3);
				testRunner.AddTestResult(testPrintLeveling == false);

				//Add printer that does not have hardware leveling
				MatterControlUtilities.AddAndSelectPrinter(testRunner, "3D Factory", "MendelMax 1.5");

				testRunner.ClickByName("Slice Settings Tab", 1);

				testRunner.ClickByName("Printer Tab", 1);

				//Make sure Print Leveling tab is visible
				bool printLevelingVisible = testRunner.WaitForName("Print Leveling Tab", 2);
				testRunner.AddTestResult(printLevelingVisible == true);

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, overrideHeight: 800, defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}

		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void SoftwareLevelingRequiredCorrectWorkflow()
		{
			Process emulatorProcess = null;

			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);

				// make a jump start printer
				emulatorProcess = MatterControlUtilities.LaunchAndConnectToPrinterEmulator(testRunner, false, "JumStart", "V1");

				// make sure it is showing the correct button
				testRunner.AddTestResult(!testRunner.WaitForName("Start Print Button", .5), "Start Print hidden");
				testRunner.AddTestResult(testRunner.WaitForName("Finish Setup Button", .5), "Finish Setup showing");

				// do print leveling
				testRunner.ClickByName("Next Button", .5);
				testRunner.ClickByName("Next Button", .5);
				testRunner.ClickByName("Next Button", .5);
				for (int i = 0; i < 3; i++)
				{
					testRunner.ClickByName("Move Z positive", .5);
					testRunner.ClickByName("Next Button", .5);
					testRunner.ClickByName("Next Button", .5);
					testRunner.ClickByName("Next Button", .5);
				}

				testRunner.AddTestResult(testRunner.ClickByName("Done Button", .5), "Found Done button");

				// make sure the button has changed to start print
				testRunner.AddTestResult(testRunner.WaitForName("Start Print Button", .5), "Start Print showing");
				testRunner.AddTestResult(!testRunner.WaitForName("Finish Setup Button", .5), "Finish Setup hidden");

				// reset to defaults and make sure print leveling is cleared
				MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

				testRunner.AddTestResult(testRunner.ClickByName("Slice Settings Options Menu", 1), "Click Options");
				testRunner.AddTestResult(testRunner.ClickByName("Reset to defaults Menu Item", 1), "Select Reset to defaults");
				testRunner.AddTestResult(testRunner.ClickByName("Yes Button", .5), "Click yes to revert");
				testRunner.Wait(1);

				// make sure it is showing the correct button
				testRunner.AddTestResult(!testRunner.WaitForName("Start Print Button", .5), "Start Print hidden");
				testRunner.AddTestResult(testRunner.WaitForName("Finish Setup Button", .5), "Finish Setup showing");

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, defaultTestImages: MatterControlUtilities.DefaultTestImages);

			Assert.IsTrue(testHarness.AllTestsPassed(1));

			try
			{
				emulatorProcess?.Kill();
			}
			catch { }
		}
	}
}

