using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PrinterEmulator;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class PrintingTests
	{
		[Test, Apartment(ApartmentState.STA)]
		public async Task CompletingPrintTurnsoffHeat()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button", 1);

				using (var emulatorDisposable = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

					testRunner.ClickByName("Printer Tab", 1);
					testRunner.ClickByName("Custom G-Code Tab", 1);
					testRunner.ClickByName("end_gcode Edit Field");
					testRunner.Type("^a");
					testRunner.Type("{BACKSPACE}");
					testRunner.Type("G28");

					testRunner.ClickByName("Start Print Button", 1);

					testRunner.WaitForName("Done Button", 120);
					Assert.True(testRunner.NameExists("Done Button"), "The print has completed");
					testRunner.WaitForName("Print Again Button", 1);

					testRunner.Wait(5);

					Assert.Less(PrinterConnectionAndCommunication.Instance.GetActualExtruderTemperature(0), 30);
					Assert.Less(PrinterConnectionAndCommunication.Instance.ActualBedTemperature, 10);
				}

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, maxTimeToRun: 200);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task PulseRequiresLevelingAndLevelingWorks()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button", 1);

				using (var emulatorDisposable = testRunner.LaunchAndConnectToPrinterEmulator("Pulse", "A-134"))
				{
					var emulator = emulatorDisposable as Emulator;
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					// close the finish setup window
					testRunner.ClickByName("Cancel Button");

					MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

					testRunner.ClickByName("General Tab", 1);
					testRunner.ClickByName("Single Print Tab", 1);
					testRunner.ClickByName("Layer(s) To Pause: Edit");
					testRunner.Type("2");

					// switch to controls so we can see the heights
					testRunner.ClickByName("Controls Tab");

					// run the leveling wizard
					testRunner.ClickByName("Finish Setup Button");
					testRunner.ClickByName("Next Button");
					testRunner.ClickByName("Next Button");
					testRunner.ClickByName("Next Button");
					testRunner.ClickByName("Next Button");
					for (int i = 0; i < 3; i++)
					{
						testRunner.ClickByName("Move Z positive", .5);
						testRunner.ClickByName("Next Button", .5);
						testRunner.ClickByName("Next Button", .5);
						testRunner.ClickByName("Next Button", .5);
					}
					testRunner.ClickByName("Done Button");

					testRunner.Wait(1);

					// print a part
					testRunner.ClickByName("Start Print Button", 1);
					// assert the leveling is working
					testRunner.WaitForName("Resume Button", 200);

					Assert.Greater(emulator.ZPosition, 5);

					testRunner.ClickByName("Cancel Print Button", 1);
				}

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, maxTimeToRun: 300);
		}
	}
}
