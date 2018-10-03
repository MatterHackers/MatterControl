using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.SlicerConfiguration;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class HardwareLevelingUITests
	{
		[Test]
		public async Task HasHardwareLevelingHidesLevelingSettings()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForFirstDraw();

				// Add printer that has hardware leveling
				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				testRunner.SwitchToPrinterSettings();
				testRunner.ClickByName("Features Tab");
				testRunner.ClickByName("Slice Settings Overflow Menu");
				testRunner.ClickByName("Expand All Menu Item");
				Assert.IsFalse(testRunner.WaitForName("print_leveling_solution Row", .5), "Print leveling should not exist for an Airwolf HD");

				// Add printer that does not have hardware leveling
				testRunner.AddAndSelectPrinter("3D Factory", "MendelMax 1.5");

				testRunner.SwitchToPrinterSettings();
				testRunner.ClickByName("Features Tab");
				testRunner.ClickByName("Slice Settings Overflow Menu");
				testRunner.ClickByName("Expand All Menu Item");
				Assert.IsTrue(testRunner.WaitForName("print_leveling_solution Row"), "Print leveling should exist for a 3D Factory MendelMax");

				return Task.CompletedTask;
			}, overrideHeight: 800);
		}

		[Test, Category("Emulator")]
		public async Task SoftwareLevelingRequiredCorrectWorkflow()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				// make a jump start printer
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator("JumpStart", "V1", runSlow: false))
				{
					// make sure it is showing the correct button
					Assert.IsFalse(testRunner.WaitForName("PrintPopupMenu", .5), "Start Print should not be visible if PrintLeveling is required");
					Assert.IsTrue(testRunner.WaitForName("Finish Setup Button"), "Finish Setup should be visible if PrintLeveling is required");

					// Helper methods
					bool headerExists(string headerText)
					{
						var header = testRunner.GetWidgetByName("HeaderRow", out _);
						var textWidget = header.Children<TextWidget>().FirstOrDefault();

						return textWidget?.Text.StartsWith(headerText) ?? false;
					}

					void waitForPage(string headerText)
					{
						testRunner.WaitFor(() => headerExists(headerText));
						Assert.IsTrue(headerExists(headerText), "Expected page not found: " + headerText);
					}

					void waitForPageAndAdvance(string headerText)
					{
						waitForPage(headerText);
						testRunner.ClickByName("Next Button");
					}

					// do print leveling
					waitForPageAndAdvance("Initial Printer Setup");

					waitForPageAndAdvance("Print Leveling Overview");

					waitForPageAndAdvance("Select Material");

					waitForPageAndAdvance("Homing The Printer");

					waitForPageAndAdvance("Waiting For Printer To Heat");

					for (int i = 0; i < 3; i++)
					{
						var section = (i * 3) + 1;

						waitForPage($"Step {section} of 9");
						testRunner.ClickByName("Move Z positive");

						waitForPage($"Step {section} of 9");
						testRunner.ClickByName("Next Button");

						waitForPage($"Step {section + 1} of 9");
						testRunner.ClickByName("Next Button");

						waitForPage($"Step {section + 2} of 9");
						testRunner.ClickByName("Next Button");
					}

					testRunner.ClickByName("Done Button");

					// make sure the button has changed to start print
					Assert.IsTrue(testRunner.WaitForName("PrintPopupMenu"), "Start Print should be visible after leveling the printer");
					Assert.IsFalse(testRunner.WaitForName("Finish Setup Button", .5), "Finish Setup should not be visible after leveling the printer");

					// reset to defaults and make sure print leveling is cleared
					testRunner.SwitchToSliceSettings();

					testRunner.ClickByName("Printer Overflow Menu");
					testRunner.ClickByName("Reset to Defaults Menu Item");
					testRunner.ClickByName("Yes Button");

					// make sure it is showing the correct button
					Assert.IsTrue(!testRunner.WaitForName("PrintPopupMenu"), "Start Print should be visible after reset to Defaults");
					Assert.IsTrue(testRunner.WaitForName("Finish Setup Button"), "Finish Setup should not be visible after reset to Defaults");
				}

				return Task.CompletedTask;
			});
		}
	}
}

