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
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class PrintingTests
	{
		[Test, Category("Emulator")]
		public async Task CompletingPrintTurnsoffHeat()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button", 1);

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					testRunner.SwitchToAdvancedSliceSettings();

					testRunner.ClickByName("Printer Tab");
					testRunner.ClickByName("Custom G-Code Tab");
					testRunner.ClickByName("end_gcode Edit Field");
					testRunner.Type("^a");
					testRunner.Type("{BACKSPACE}");
					testRunner.Type("G28");

					testRunner.AddDefaultFileToBedplate();

					testRunner.ClickByName("Start Print Button");

					// Wait for print to finish
					testRunner.WaitForPrintFinished();

					// Wait for expected temp
					testRunner.Delay(() => PrinterConnection.Instance.GetActualExtruderTemperature(0) <= 0, 5);
					Assert.Less(PrinterConnection.Instance.GetActualExtruderTemperature(0), 30);

					// Wait for expected temp
					testRunner.Delay(() => PrinterConnection.Instance.ActualBedTemperature <= 10, 5);
					Assert.Less(PrinterConnection.Instance.ActualBedTemperature, 10);
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 200);
		}

		[Test, Category("Emulator")]
		public async Task PulseRequiresLevelingAndLevelingWorks()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button", 1);

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator("Pulse", "A-134"))
				{
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					// close the finish setup window
					testRunner.ClickByName("Cancel Button");

					testRunner.SwitchToAdvancedSliceSettings();

					testRunner.ClickByName("General Tab");
					testRunner.ClickByName("Single Print Tab");
					testRunner.ClickByName("Layer(s) To Pause: Edit");
					testRunner.Type("2");

					// switch to controls so we can see the heights
					testRunner.SwitchToControlsTab();

					// run the leveling wizard (only 4 next as there is no heated bed
					testRunner.ClickByName("Finish Setup Button");
					testRunner.ClickByName("Next Button");
					testRunner.ClickByName("Next Button");
					testRunner.ClickByName("Next Button");
					testRunner.ClickByName("Next Button");
					for (int i = 0; i < 3; i++)
					{
						testRunner.ClickByName("Move Z positive");
						testRunner.ClickByName("Next Button");
						testRunner.ClickByName("Next Button");
						testRunner.ClickByName("Next Button");
					}
					testRunner.ClickByName("Done Button");

					testRunner.Delay(1);

					// print a part
					testRunner.AddDefaultFileToBedplate();
					testRunner.ClickByName("Start Print Button");

					testRunner.Delay(() => emulator.ZPosition > 5, 3);

					// assert the leveling is working
					Assert.Greater(emulator.ZPosition, 5);

					testRunner.ClickByName("Cancel Print Button");
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 90);
		}

		[Test, Category("Emulator")]
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
			using (var emulator = new Emulator())
			{
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
		}

		[Test, Category("Emulator")]
		public async Task PrinterRequestsResumeWorkingAsExpected()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					testRunner.SwitchToAdvancedSliceSettings();

					testRunner.ClickByName("General Tab");
					testRunner.ClickByName("Single Print Tab");
					testRunner.ClickByName("Layer(s) To Pause: Edit");
					testRunner.Type("2;6");

					testRunner.ClickByName("Pin Settings Button");

					// print a part
					testRunner.AddDefaultFileToBedplate();
					testRunner.ClickByName("Start Print Button");

					// turn on line error simulation
					emulator.SimulateLineErrors = true;

					// close the pause dialog pop-up (resume)
					testRunner.WaitForName("No Button", 90);
					testRunner.ClickByName("No Button");

					// simulate board reboot
					emulator.SimulateReboot();

					// close the pause dialog pop-up (resume)
					testRunner.Delay(3);
					testRunner.WaitForName("No Button", 90);
					testRunner.ClickByName("No Button");

					// Wait for done
					testRunner.WaitForPrintFinished();
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 90);
		}

		private EventHandler unregisterEvents;

		[Test, Category("Emulator")]
		public async Task TuningAdjustmentsDefaultToOneAndPersists()
		{
			double targetExtrusionRate = 1.5;
			double targetFeedRate = 2;

			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button");

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					testRunner.AddDefaultFileToBedplate();
					
					testRunner.SwitchToControlsTab();

					// Wait for printing to complete
					var printFinishedResetEvent = new AutoResetEvent(false);
					PrinterConnection.Instance.PrintFinished.RegisterEvent((s, e) => printFinishedResetEvent.Set(), ref unregisterEvents);

					testRunner.ClickByName("Start Print Button");

					var container = testRunner.GetWidgetByName("ManualPrinterControls.ControlsContainer", out _, 5);

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
					testRunner.ClickByName("Extrusion Multiplier NumberEdit");

					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					// Wait for slicing to complete before setting target values
					testRunner.Delay(() => PrinterConnection.Instance.DetailedPrintingState == DetailedPrintingState.Printing, 8);
					testRunner.Delay();

					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					printFinishedResetEvent.WaitOne();

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					testRunner.WaitForPrintFinished();

					// Restart the print
					testRunner.ClickByName("Start Print Button");
					testRunner.Delay(2);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					testRunner.CancelPrint();
					testRunner.Delay(1);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);
				}

				return Task.CompletedTask;
			}, overrideHeight:900, maxTimeToRun: 120);
		}

		[Test, Category("Emulator")]
		public async Task TuningAdjustmentControlsBoundToStreamValues()
		{
			double targetExtrusionRate = 1.5;
			double targetFeedRate = 2;

			double initialExtrusionRate = 0.6;
			double initialFeedRate = 0.7;

			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button");

				// Set custom adjustment values
				FeedRateMultiplyerStream.FeedRateRatio = initialFeedRate;
				ExtrusionMultiplyerStream.ExtrusionRatio = initialExtrusionRate;

				// Then validate that they are picked up
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					testRunner.AddDefaultFileToBedplate();

					testRunner.SwitchToControlsTab();

					var printFinishedResetEvent = new AutoResetEvent(false);
					PrinterConnection.Instance.PrintFinished.RegisterEvent((s, e) => printFinishedResetEvent.Set(), ref unregisterEvents);

					testRunner.ClickByName("Start Print Button");

					var container = testRunner.GetWidgetByName("ManualPrinterControls.ControlsContainer", out _, 5);

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
					testRunner.SwitchToControlsTab();

					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					// Wait for slicing to complete before setting target values
					testRunner.Delay(() => PrinterConnection.Instance.DetailedPrintingState == DetailedPrintingState.Printing, 8);
					testRunner.Delay();

					// Values should remain after print completes
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					// Wait for printing to complete
					printFinishedResetEvent.WaitOne();

					testRunner.WaitForPrintFinished();

					// Values should match entered values
					testRunner.ClickByName("Start Print Button");
					testRunner.Delay(2);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					testRunner.CancelPrint();
					testRunner.Delay(1);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);
				}

				return Task.CompletedTask;
			}, overrideHeight: 900, maxTimeToRun: 120);
		}

		[Test, Category("Emulator")]
		public async Task CloseShouldNotStopSDPrint()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button");

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					testRunner.WaitForName("SD Card Row Item Collection");
					testRunner.NavigateToFolder("SD Card Row Item Collection");

					testRunner.ClickByName("Row Item Item 1.gcode");

					testRunner.ClickByName("Print Library Overflow Menu", delayBeforeReturn: 1);
					testRunner.ClickByName("Print Menu Item");

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

				return Task.CompletedTask;
			}, overrideHeight: 900, maxTimeToRun: 90);
		}

		[Test, Category("Emulator")]
		public async Task CancelingNormalPrintTurnsHeatAndFanOff()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button");

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					testRunner.AddDefaultFileToBedplate();
					testRunner.ClickByName("Start Print Button");
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
					// TODO: change this to checking that the fan speed is 0 - when the emulator tracks fan speed.
					Assert.AreEqual(1, fanChangedCount, "We expected to see fan change on quiting.");
				}

				return Task.CompletedTask;
			}, overrideHeight: 900, maxTimeToRun: 90);
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
