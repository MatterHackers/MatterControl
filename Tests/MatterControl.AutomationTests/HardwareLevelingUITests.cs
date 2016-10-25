using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class HardwareLevelingUITests
	{
		[Test, Apartment(ApartmentState.STA)]
		public async Task HasHardwareLevelingHidesLevelingSettings()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);
				//Add printer that has hardware leveling
				MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

				MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

				testRunner.ClickByName("Printer Tab", 1);
				testRunner.Wait(1);

				//Make sure Print Leveling tab is not visible 
				bool testPrintLeveling = testRunner.WaitForName("Print Leveling Tab", 3);
				Assert.IsTrue(testPrintLeveling == false);

				//Add printer that does not have hardware leveling
				MatterControlUtilities.AddAndSelectPrinter(testRunner, "3D Factory", "MendelMax 1.5");

				testRunner.ClickByName("Slice Settings Tab", 1);

				testRunner.ClickByName("Printer Tab", 1);

				//Make sure Print Leveling tab is visible
				bool printLevelingVisible = testRunner.WaitForName("Print Leveling Tab", 2);
				Assert.IsTrue(printLevelingVisible == true);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideHeight: 800);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task SoftwareLevelingRequiredCorrectWorkflow()
		{
			Process emulatorProcess = null;

			AutomationTest testToRun = (testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);

				// make a jump start printer
				emulatorProcess = MatterControlUtilities.LaunchAndConnectToPrinterEmulator(testRunner, false, "JumStart", "V1");

				// make sure it is showing the correct button
				Assert.IsTrue(!testRunner.WaitForName("Start Print Button", .5), "Start Print hidden");
				Assert.IsTrue(testRunner.WaitForName("Finish Setup Button", .5), "Finish Setup showing");

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

				Assert.IsTrue(testRunner.ClickByName("Done Button", .5), "Found Done button");

				// make sure the button has changed to start print
				Assert.IsTrue(testRunner.WaitForName("Start Print Button", .5), "Start Print showing");
				Assert.IsTrue(!testRunner.WaitForName("Finish Setup Button", .5), "Finish Setup hidden");

				// reset to defaults and make sure print leveling is cleared
				MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

				Assert.IsTrue(testRunner.ClickByName("Slice Settings Options Menu", 1), "Click Options");
				Assert.IsTrue(testRunner.ClickByName("Reset to defaults Menu Item", 1), "Select Reset to defaults");
				Assert.IsTrue(testRunner.ClickByName("Yes Button", .5), "Click yes to revert");
				testRunner.Wait(1);

				// make sure it is showing the correct button
				Assert.IsTrue(!testRunner.WaitForName("Start Print Button", .5), "Start Print hidden");
				Assert.IsTrue(testRunner.WaitForName("Finish Setup Button", .5), "Finish Setup showing");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);

			try
			{
				emulatorProcess?.Kill();
			}
			catch { }
		}
	}
}

