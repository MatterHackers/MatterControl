using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PrinterEmulator;
using MatterHackers.VectorMath;
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

					testRunner.Delay(5);

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

					testRunner.Delay(1);

					// print a part
					testRunner.ClickByName("Start Print Button", 1);

					// assert the leveling is working
					testRunner.WaitForName("Yes Button", 200);
					// close the pause dialog pop-up
					testRunner.ClickByName("Yes Button");

					Assert.Greater(emulator.ZPosition, 5);

					testRunner.ClickByName("Cancel Print Button", 1);
				}

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, maxTimeToRun: 300);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void ExpectedEmulatorResponses()
		{
			string[] test1 = new string[]
			{
				"N1 M110 N1 * 125",
				"ok",
				"N2 M114 * 37",
				"X:0.00 Y: 0.00 Z: 0.00 E: 0.00 Count X: 0.00 Y: 0.00 Z: 0.00",
				"ok",
				"N3 M105 * 36",
				"ok T:27.0 / 0.0",
				"N1 M110 N1*125",
				"ok",
				"N2 M115 * 36",
				"FIRMWARE_NAME:Marlin V1; Sprinter/grbl mashup for gen6 FIRMWARE_URL:https://github.com/MarlinFirmware/Marlin PROTOCOL_VERSION:1.0 MACHINE_TYPE:Framelis v1 EXTRUDER_COUNT:1 UUID:155f84b5-d4d7-46f4-9432-667e6876f37a",
				"ok",
				"N3 M104 T0 S0 * 34",
				"ok",
				"N4 M104 T1 S0 * 36",
				"ok",
				"N5 M105 * 34",
				"ok T:27.0 / 0.0",
				"N6 M105 * 45",
				"Error:checksum mismatch, Last Line: 5",
				"Resend: 6",
				"ok",
				"N6 M105 * 33",
				"ok T:27.0 / 0.0",
				"N7 M105 * 32",
				"ok T:27.0 / 0.0",
				"N8 M105 * 47",
				"ok T:27.0 / 0.0",
				"N9 M105 * 46",
				"ok T:27.0 / 0.0",
				"N10 M105 * 22",
				"ok T:27.0 / 0.0",
				"N11 M105 * 23",
				"ok T:27.0 / 0.0",
				"N12 M105 * 20",
				"ok T:27.0 / 0.0",
				"N13 M105 * 21",
				"ok T:27.0 / 0.0",
				"N14 M105 * 18",
				"ok T:27.0 / 0.0",
				"N15 M105 * 19",
				"ok T:27.0 / 0.0",
				"N16 M105 * 16",
				"ok T:27.0 / 0.0",
				"N17 M105 * 40",
				"Error:checksum mismatch, Last Line: 16",
				"Resend: 17",
				"ok",
				"N17 M105 * 17",
				"ok T:27.0 / 0.0",
			};

			string[] test2 = new string[]
			{
				"N1 M110 N1*125",
				"ok",
				"N1 M110 N1*125",
				"ok",
				"N1 M110 N1*125",
				"ok",
				"N2 M114*37",
				"X:0.00 Y: 0.00 Z: 0.00 E: 0.00 Count X: 0.00 Y: 0.00 Z: 0.00",
				 "ok",
			};

			SimulatePrint(test1);
			SimulatePrint(test2);
		}

		private static void SimulatePrint(string[] sendRecieveLog)
		{
			Emulator emulator = new Emulator();
			int lineIndex = 0;
			while (lineIndex < sendRecieveLog.Length)
			{
				var sentCommand = sendRecieveLog[lineIndex];
				string response = emulator.GetCorrectResponse(sentCommand);
				lineIndex++;
				var lines = response.Split('\n');
				for (int i = 0; i < lines.Length; i++)
				{
					if (!string.IsNullOrEmpty(lines[i]))
					{
						Assert.AreEqual(sendRecieveLog[lineIndex], lines[i]);
						lineIndex++;
					}
				}
			}
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task PrinterRequestsResumeWorkingAsExpected()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button", 1);

				using (var emulatorDisposable = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					var emulator = emulatorDisposable as Emulator;
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

					testRunner.ClickByName("General Tab", 1);
					testRunner.ClickByName("Single Print Tab", 1);
					testRunner.ClickByName("Layer(s) To Pause: Edit");
					testRunner.Type("2;6");

					// switch to controls so we can see the heights
					testRunner.ClickByName("Controls Tab");

					// print a part
					testRunner.ClickByName("Start Print Button", 1);

					// turn on line error simulation
					emulator.SimulateLineErrors = true;

					// close the pause dialog pop-up (resume)
					testRunner.ClickByName("No Button", 200);

					// simulate board reboot
					emulator.SimulateRebot();

					// close the pause dialog pop-up (resume)
					testRunner.ClickByName("No Button", 200);

					// Wait for done
					testRunner.WaitForName("Done Button", 60);
					testRunner.WaitForName("Print Again Button", 1);
				}

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, maxTimeToRun: 300);
		}

		private EventHandler unregisterEvents;

		[Test, Apartment(ApartmentState.STA)]
		public async Task TuningAdjustmentsDefaultToOneAndPersists()
		{
			double targetExtrusionRate = 1.5;
			double targetFeedRate = 2;

			AutomationTest testToRun = (testRunner) =>
			{
				SystemWindow systemWindow;

				testRunner.WaitForName("Cancel Wizard Button", 1);

				using (var emulatorDisposable = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					testRunner.SwitchToSettingsAndControls();

					testRunner.ClickByName("Controls Tab", 1);

					testRunner.ClickByName("Start Print Button", 1);

					var container = testRunner.GetWidgetByName("ManualPrinterControls.ControlsContainer", out systemWindow, 5);

					// Scroll the widget into view
					var scrollable = container.Parents<ManualPrinterControls>().First().Children<ScrollableWidget>().First();
					var width = scrollable.Width;

					// Workaround needed to scroll to the bottom of the Controls panel
					//scrollable.ScrollPosition = new Vector2();
					scrollable.ScrollPosition = new Vector2(0, 30);

					// Workaround to force layout to fix problems with size of Tuning Widgets after setting ScrollPosition manually
					scrollable.Width = width - 1;
					scrollable.Width = width;

					// Tuning values should default to 1 when missing
					ConfirmExpectedSpeeds(testRunner, 1, 1);

					testRunner.Delay();
					testRunner.ClickByName("Extrusion Multiplier NumberEdit");
					testRunner.Type(targetExtrusionRate.ToString());

					testRunner.ClickByName("Feed Rate NumberEdit");
					testRunner.Type(targetFeedRate.ToString());

					// Force focus away from the feed rate field, causing an persisted update
					testRunner.ClickByName("Controls Tab", 1);
					testRunner.Delay();

					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					// Wait for slicing to complete before setting target values
					testRunner.Delay(() => PrinterConnectionAndCommunication.Instance.PrintingState == PrinterConnectionAndCommunication.DetailedPrintingState.Printing, 8);
					testRunner.Delay();

					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					// Wait for printing to complete
					var resetEvent = new AutoResetEvent(false);
					PrinterConnectionAndCommunication.Instance.PrintFinished.RegisterEvent((s, e) => resetEvent.Set(), ref unregisterEvents);
					resetEvent.WaitOne();

					testRunner.WaitForName("Done Button", 30);
					testRunner.WaitForName("Print Again Button", 1);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					// Restart the print
					testRunner.ClickByName("Print Again Button", 1);
					testRunner.Delay(2);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					testRunner.CancelPrint();
					testRunner.Delay(1);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);
				}

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideHeight:900, maxTimeToRun: 990);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task TuningAdjustmentControlsBoundToStreamValues()
		{

			double targetExtrusionRate = 1.5;
			double targetFeedRate = 2;

			double initialExtrusionRate = 0.6;
			double initialFeedRate = 0.7;

			AutomationTest testToRun = (testRunner) =>
			{
				SystemWindow systemWindow;

				testRunner.WaitForName("Cancel Wizard Button", 1);

				// Set custom adjustment values
				FeedRateMultiplyerStream.FeedRateRatio = initialFeedRate;
				ExtrusionMultiplyerStream.ExtrusionRatio = initialExtrusionRate;

				// Then validate that they are picked up
				using (var emulatorDisposable = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					testRunner.SwitchToSettingsAndControls();

					testRunner.ClickByName("Controls Tab", 1);

					testRunner.ClickByName("Start Print Button", 1);

					var container = testRunner.GetWidgetByName("ManualPrinterControls.ControlsContainer", out systemWindow, 5);

					// Scroll the widget into view
					var scrollable = container.Parents<ManualPrinterControls>().First().Children<ScrollableWidget>().First();
					var width = scrollable.Width;

					// Workaround needed to scroll to the bottom of the Controls panel
					//scrollable.ScrollPosition = new Vector2();
					scrollable.ScrollPosition = new Vector2(0, 30);

					// Workaround to force layout to fix problems with size of Tuning Widgets after setting ScrollPosition manually
					scrollable.Width = width - 1;
					scrollable.Width = width;

					// Tuning values should match 
					ConfirmExpectedSpeeds(testRunner, initialExtrusionRate, initialFeedRate);

					testRunner.Delay();
					testRunner.ClickByName("Extrusion Multiplier NumberEdit");
					testRunner.Type(targetExtrusionRate.ToString());

					testRunner.ClickByName("Feed Rate NumberEdit");
					testRunner.Type(targetFeedRate.ToString());

					// Force focus away from the feed rate field, causing an persisted update
					testRunner.ClickByName("Controls Tab", 1);
					testRunner.Delay();

					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					// Wait for slicing to complete before setting target values
					testRunner.Delay(() => PrinterConnectionAndCommunication.Instance.PrintingState == PrinterConnectionAndCommunication.DetailedPrintingState.Printing, 8);
					testRunner.Delay();

					// Values should remain after print completes
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					// Wait for printing to complete
					var resetEvent = new AutoResetEvent(false);
					PrinterConnectionAndCommunication.Instance.PrintFinished.RegisterEvent((s, e) => resetEvent.Set(), ref unregisterEvents);
					resetEvent.WaitOne();

					testRunner.WaitForName("Done Button", 30);
					testRunner.WaitForName("Print Again Button", 1);

					// Values should match entered values
					testRunner.ClickByName("Print Again Button", 1);
					testRunner.Delay(2);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					testRunner.CancelPrint();
					testRunner.Delay(1);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);
				}

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideHeight: 900, maxTimeToRun: 990);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task CancelingSdCardPrintLeavesHeatAndFanOn()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button", 1);

				using (var emulatorDisposable = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Emulator emulator = (Emulator)emulatorDisposable;

					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					testRunner.ClickByName("Queue... Menu", 2);
					testRunner.ClickByName(" Remove All Menu Item", 2);
					testRunner.ClickByName("Queue... Menu", 2);
					testRunner.ClickByName(" Load Files Menu Item", 2);
					testRunner.Delay(2);

					testRunner.ClickByName("Start Print Button", 1);
					testRunner.Delay(2);

					int tempChangedCount = 0;
					int fanChangedCount = 0;
					emulator.ExtruderTemperatureChanged += (s, e) =>
					{
						tempChangedCount++;
					};
					emulator.FanSpeedChanged += (s, e) =>
					{
						fanChangedCount++;
					};

					testRunner.CloseMatterControlViaMenu();

					testRunner.ClickByName("Yes Button");

					testRunner.Delay(2);
					Assert.AreEqual(0, tempChangedCount, "We should not change this while exiting an sd card print.");
					Assert.AreEqual(0, fanChangedCount, "We should not change this while exiting an sd card print.");
				}

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideHeight: 900, maxTimeToRun: 990);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task CancelingNormalPrintTurnsHeatAndFanOff()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button", 1);

				using (var emulatorDisposable = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Emulator emulator = (Emulator)emulatorDisposable;

					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					testRunner.ClickByName("Start Print Button", 1);
					testRunner.Delay(5);

					int fanChangedCount = 0;
					emulator.FanSpeedChanged += (s, e) =>
					{
						fanChangedCount++;
					};
					testRunner.CloseMatterControlViaMenu();

					testRunner.ClickByName("Yes Button");

					testRunner.Delay(5);
					Assert.AreEqual(0, emulator.ExtruderGoalTemperature, "We need to set the temp to 0.");
					// TODO: chagne this to checking that the fan speed is 0 - when the emulator tracks fan speed.
					Assert.AreEqual(1, fanChangedCount, "We expected to see fan chage on quiting.");
				}

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideHeight: 900, maxTimeToRun: 990);
		}

		private static void ConfirmExpectedSpeeds(AutomationRunner testRunner, double targetExtrusionRate, double targetFeedRate)
		{
			SystemWindow systemWindow;
			SolidSlider slider;

			// Assert the UI has the expected values
			slider = testRunner.GetWidgetByName("Extrusion Multiplier Slider", out systemWindow, 5) as SolidSlider;
			Assert.AreEqual(targetExtrusionRate, slider.Value);

			slider = testRunner.GetWidgetByName("Feed Rate Slider", out systemWindow, 5) as SolidSlider;
			Assert.AreEqual(targetFeedRate, slider.Value);

			testRunner.Delay(.2);

			// Assert the changes took effect on the model
			Assert.AreEqual(targetExtrusionRate, ExtrusionMultiplyerStream.ExtrusionRatio);
			Assert.AreEqual(targetFeedRate, FeedRateMultiplyerStream.FeedRateRatio);
		}
	}
}
