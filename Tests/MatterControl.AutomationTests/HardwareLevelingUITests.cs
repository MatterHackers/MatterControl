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
				testRunner.CloseSignInAndPrinterSelect();

				//Add printer that has hardware leveling
				MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

				testRunner.SwitchToAdvancedSliceSettings();

				testRunner.ClickByName("Printer Tab", 1);
				testRunner.Delay(1);

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
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				// make a jump start printer
				using (var emulatorProcess = testRunner.LaunchAndConnectToPrinterEmulator("JumpStart", "V1", runSlow: false))
				{
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

					testRunner.ClickByName("Done Button", 1);

					// make sure the button has changed to start print
					Assert.IsTrue(testRunner.WaitForName("Start Print Button", 5), "Start Print showing");
					Assert.IsTrue(!testRunner.WaitForName("Finish Setup Button", 1), "Finish Setup hidden");

					// reset to defaults and make sure print leveling is cleared
					testRunner.SwitchToAdvancedSliceSettings();

					testRunner.ClickByName("Slice Settings Options Menu", 1);
					testRunner.ClickByName("Reset to Defaults Menu Item", 1);
					testRunner.ClickByName("Yes Button", .5);
					testRunner.Delay(1);

					// make sure it is showing the correct button
					Assert.IsTrue(!testRunner.WaitForName("Start Print Button", 1), "Start Print hidden");
					Assert.IsTrue(testRunner.WaitForName("Finish Setup Button", 1), "Finish Setup showing");
				}

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}
	}
}

