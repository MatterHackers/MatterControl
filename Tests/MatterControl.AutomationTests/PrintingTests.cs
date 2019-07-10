using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterControl.Printing;
using MatterControl.Printing.Pipelines;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrintHistory;
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
					Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "One printer should be defined after add");

					testRunner.SelectSliceSettingsField(PrinterSettings.Layout.Printer, "end_gcode");

					testRunner.Type("^a");
					testRunner.Type("{BACKSPACE}");
					testRunner.Type("G28");

					testRunner.SelectSliceSettingsField(PrinterSettings.Layout.Printer, "start_gcode");

					var printer = testRunner.FirstPrinter();

					// Validate GCode fields persist values
					Assert.AreEqual(
						"G28",
						printer.Settings.GetValue(SettingsKey.end_gcode),
						"Failure persisting GCode/MultilineTextField value");

					testRunner.AddItemToBedplate();

					// Shorten the delay so the test runs in a reasonable time
					printer.Connection.TimeToHoldTemperature = 5;

					testRunner.StartPrint();

					// Wait for print to finish
					testRunner.WaitForPrintFinished(printer);

					// Wait for expected temp
					testRunner.WaitFor(() => printer.Connection.GetActualHotendTemperature(0) <= 0, 10);
					Assert.Less(printer.Connection.GetActualHotendTemperature(0), 30);

					// Wait for expected temp
					testRunner.WaitFor(() => printer.Connection.ActualBedTemperature <= 10);
					Assert.Less(printer.Connection.ActualBedTemperature, 10);

					// Make sure we can run this whole thing again
					testRunner.StartPrint();

					// Wait for print to finish
					testRunner.WaitForPrintFinished(printer);

					// Wait for expected temp
					testRunner.WaitFor(() => printer.Connection.GetActualHotendTemperature(0) <= 0, 10);
					Assert.Less(printer.Connection.GetActualHotendTemperature(0), 30);

					// Wait for expected temp
					testRunner.WaitFor(() => printer.Connection.ActualBedTemperature <= 10);
					Assert.Less(printer.Connection.ActualBedTemperature, 10);
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 95);
		}

		[Test, Category("Emulator")]
		public async Task PulseLevelingTest()
		{
			// Validates the Pulse profile requires leveling and it works as expected
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				AutomationRunner.TimeToMoveMouse = .2;
				testRunner.WaitForName("Cancel Wizard Button", 1);

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator("Pulse", "A-134"))
				{
					Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "One printer should be defined after add");

					testRunner.OpenPrintPopupMenu();

					testRunner.ClickByName("SetupPrinter");

					testRunner.Complete9StepLeveling();

					// print a part
					testRunner.AddItemToBedplate();

					var printer = testRunner.FirstPrinter();

					var currentSettings = printer.Settings;
					currentSettings.SetValue(SettingsKey.pause_gcode, "");
					currentSettings.SetValue(SettingsKey.resume_gcode, "");

					testRunner.StartPrint(pauseAtLayers: "2");

					testRunner.WaitForName("Yes Button", 20); // the yes button is 'Resume'

					// the user types in the pause layer 1 based and we are 0 based, so we should be on: user 2, printer 1.
					Assert.AreEqual(1, printer.Connection.CurrentlyPrintingLayer);

					// assert the leveling is working
					Assert.AreEqual(11.25, emulator.Destination.Z);

					testRunner.CancelPrint();

					// now run leveling again and make sure we get the same result
					testRunner.SwitchToControlsTab();
					testRunner.ClickByName("Printer Calibration Button");

					testRunner.ClickByName("Print Leveling Row");

					testRunner.Complete9StepLeveling(2);

					testRunner.StartPrint(pauseAtLayers: "2");

					testRunner.WaitForName("Yes Button", 20); // the yes button is 'Resume'

					// the user types in the pause layer 1 based and we are 0 based, so we should be on: user 2, printer 1.
					Assert.AreEqual(1, printer.Connection.CurrentlyPrintingLayer);
					// assert the leveling is working
					Assert.AreEqual(12.25, emulator.Destination.Z);

					testRunner.CancelPrint();

					// now modify the leveling data manually and assert that it is applied when printing
					testRunner.SwitchToControlsTab();

					testRunner.ClickByName("Printer Calibration Button");

					testRunner.ClickByName("Edit Leveling Data Button");
					for (int i = 0; i < 3; i++)
					{
						var name = $"z Position {i}";
						testRunner.ClickByName(name);
						testRunner.Type("^a"); // select all
						testRunner.Type("5");
					}

					testRunner.ClickByName("Save Leveling Button");

					testRunner.ClickByName("Cancel Wizard Button");

					testRunner.StartPrint(pauseAtLayers: "2");

					testRunner.WaitForName("Yes Button", 20); // the yes button is 'Resume'

					// the user types in the pause layer 1 based and we are 0 based, so we should be on: user 2, printer 1.
					Assert.AreEqual(1, printer.Connection.CurrentlyPrintingLayer);

					// assert the leveling is working
					Assert.AreEqual(5.25, emulator.Destination.Z);
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 130);
		}

		[Test, Category("Emulator")]
		public void ExpectedEmulatorResponses()
		{
			// TODO: Emulator behavior should emulate actual printer firmware and use configuration rather than M104/M109 sends to set extruder count
			//
			// Quirky emulator returns single extruder M105 responses until after the first M104, at which point it extends its extruder count to match
			string M105ResponseBeforeM104 = "ok T:27.0 / 0.0";
			string M105ResponseAfterM104 = "ok T0:27.0 / 0.0 T1:27.0 / 0.0";

			string[] test1 = new string[]
			{
				"N1 M110 N1 * 125",
				"ok",
				"N2 M114 * 37",
				"X:0.00 Y: 0.00 Z: 0.00 E: 0.00 Count X: 0.00 Y: 0.00 Z: 0.00",
				"ok",
				"N3 M105 * 36",
				M105ResponseBeforeM104,
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
				M105ResponseAfterM104,
				"N6 M105 * 45",
				"Error:checksum mismatch, Last Line: 5",
				"Resend: 6",
				"ok",
				"N6 M105 * 33",
				M105ResponseAfterM104,
				"N7 M105 * 32",
				M105ResponseAfterM104,
				"N8 M105 * 47",
				M105ResponseAfterM104,
				"N9 M105 * 46",
				M105ResponseAfterM104,
				"N10 M105 * 22",
				M105ResponseAfterM104,
				"N11 M105 * 23",
				M105ResponseAfterM104,
				"N12 M105 * 20",
				M105ResponseAfterM104,
				"N13 M105 * 21",
				M105ResponseAfterM104,
				"N14 M105 * 18",
				M105ResponseAfterM104,
				"N15 M105 * 19",
				M105ResponseAfterM104,
				"N16 M105 * 16",
				M105ResponseAfterM104,
				"N17 M105 * 40",
				"Error:checksum mismatch, Last Line: 16",
				"Resend: 17",
				"ok",
				"N17 M105 * 17",
				M105ResponseAfterM104,
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
				emulator.HasHeatedBed = false;

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
					Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "One printer should be defined after add");

					// print a part
					testRunner.AddItemToBedplate();

					testRunner.StartPrint(pauseAtLayers: "2;6");

					// turn on line error simulation
					emulator.SimulateLineErrors = true;

					// close the pause dialog pop-up (resume)
					testRunner.WaitForName("Yes Button", 20); // the yes button is 'Resume'
					testRunner.ClickByName("Yes Button");

					// simulate board reboot
					emulator.SimulateReboot();

					// close the pause dialog pop-up (resume)
					testRunner.Delay(3);
					testRunner.WaitForName("Yes Button", 20);
					testRunner.ClickByName("Yes Button");

					// Wait for done
					testRunner.WaitForPrintFinished(testRunner.FirstPrinter());
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 90);
		}

		[Test, Category("Emulator")]
		public async Task PrinterRecoveryTest()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "One printer should exist after add");

					var printer = testRunner.FirstPrinter();
					printer.Settings.SetValue(SettingsKey.recover_is_enabled, "1");
					printer.Settings.SetValue(SettingsKey.has_hardware_leveling, "0");

					// TODO: Delay needed to work around timing issue in MatterHackers/MCCentral#2415
					testRunner.Delay(1);

					Assert.IsTrue(printer.Connection.RecoveryIsEnabled);

					// print a part
					testRunner.AddItemToBedplate();
					testRunner.StartPrint(pauseAtLayers: "2;4;6");

					// Wait for pause dialog
					testRunner.WaitForName("Yes Button", 15); // the yes button is 'Resume'

					// validate the current layer
					Assert.AreEqual(1, printer.Connection.CurrentlyPrintingLayer);

					// Resume
					testRunner.ClickByName("Yes Button");

					// the printer is now paused
					// close the pause dialog pop-up do not resume
					ClickDialogButton(testRunner, printer, "No Button", 3);

					// Disconnect
					testRunner.ClickByName("Disconnect from printer button");

					// Reconnect
					testRunner.WaitForName("Connect to printer button", 10);
					testRunner.ClickByName("Connect to printer button");

					testRunner.WaitFor(() => printer.Connection.CommunicationState == CommunicationStates.Connected);

					// Assert that recovery happens
					Assert.IsTrue(PrintRecovery.RecoveryAvailable(printer), "Recovery should be enabled after Disconnect while printing");

					// Recover the print
					ClickDialogButton(testRunner, printer, "Yes Button", -1);

					// The first pause that we get after recovery should be layer 6.
					// wait for the pause and continue
					ClickDialogButton(testRunner, printer, "Yes Button", 5);

					// Wait for done
					testRunner.WaitForPrintFinished(printer);
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 180);
		}

		// TODO: convert to extension method
		private static void ClickDialogButton(AutomationRunner testRunner, PrinterConfig printer, string buttonName, int expectedLayer)
		{
			testRunner.WaitForName(buttonName, 90);
			Assert.AreEqual(expectedLayer, printer.Connection.CurrentlyPrintingLayer);

			testRunner.ClickByName(buttonName);
			testRunner.WaitFor(() => !testRunner.NameExists(buttonName), 1);
		}

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
					Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "One printer should be defined after add");

					testRunner.AddItemToBedplate();

					testRunner.SwitchToControlsTab();

					var printer = testRunner.FirstPrinter();

					// Wait for printing to complete
					var printFinishedResetEvent = new AutoResetEvent(false);
					printer.Connection.PrintFinished += (s, e) =>
					{
						printFinishedResetEvent.Set();
					};

					testRunner.StartPrint();

					var container = testRunner.GetWidgetByName("ManualPrinterControls.ControlsContainer", out _, 5);

					// Scroll the widget into view
					var scrollable = container.Parents<ManualPrinterControls>().First() as ScrollableWidget;
					var width = scrollable.Width;

					// Workaround needed to scroll to the bottom of the Controls panel
					// scrollable.ScrollPosition = new Vector2();
					scrollable.ScrollPosition = new Vector2(0, 30);

					// Workaround to force layout to fix problems with size of Tuning Widgets after setting ScrollPosition manually
					scrollable.Width = width - 1;
					scrollable.Width = width;

					// Tuning values should default to 1 when missing
					ConfirmExpectedSpeeds(testRunner, 1, 1, "Initial case");

					testRunner.Delay();
					testRunner.ClickByName("Extrusion Multiplier NumberEdit");
					testRunner.Type(targetExtrusionRate.ToString());

					testRunner.ClickByName("Feed Rate NumberEdit");
					testRunner.Type(targetFeedRate.ToString());

					// Force focus away from the feed rate field, causing an persisted update
					testRunner.ClickByName("Extrusion Multiplier NumberEdit");

					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate, "After setting TextEdit values");

					// Wait for slicing to complete before setting target values
					testRunner.WaitFor(() => printer.Connection.DetailedPrintingState == DetailedPrintingState.Printing, 8);
					testRunner.Delay();

					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate, "While printing");

					// Wait up to 60 seconds for the print to finish
					printFinishedResetEvent.WaitOne(60 * 1000);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate, "After print finished");

					testRunner.WaitForPrintFinished(printer);

					// Restart the print
					testRunner.StartPrint();
					testRunner.Delay(2);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate, "After print restarted");

					testRunner.CancelPrint();
					testRunner.Delay(1);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate, "After canceled print");
				}

				return Task.CompletedTask;
			}, overrideHeight: 900, maxTimeToRun: 120);
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
				FeedRateMultiplierStream.FeedRateRatio = initialFeedRate;
				ExtrusionMultiplierStream.ExtrusionRatio = initialExtrusionRate;

				// Then validate that they are picked up
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "One printer should be defined after add");

					testRunner.AddItemToBedplate();

					testRunner.SwitchToControlsTab();

					var printer = testRunner.FirstPrinter();

					var printFinishedResetEvent = new AutoResetEvent(false);
					printer.Connection.PrintFinished += (s, e) => printFinishedResetEvent.Set();

					testRunner.StartPrint();

					var container = testRunner.GetWidgetByName("ManualPrinterControls.ControlsContainer", out _, 5);

					// Scroll the widget into view
					var scrollable = container.Parents<ManualPrinterControls>().FirstOrDefault() as ScrollableWidget;
					var width = scrollable.Width;

					// Workaround needed to scroll to the bottom of the Controls panel
					// scrollable.ScrollPosition = new Vector2();
					scrollable.ScrollPosition = new Vector2(0, 30);

					// Workaround to force layout to fix problems with size of Tuning Widgets after setting ScrollPosition manually
					scrollable.Width = width - 1;
					scrollable.Width = width;

					// Tuning values should match
					ConfirmExpectedSpeeds(testRunner, initialExtrusionRate, initialFeedRate, "Initial case");

					testRunner.Delay();
					testRunner.ClickByName("Extrusion Multiplier NumberEdit");
					testRunner.Type(targetExtrusionRate.ToString());

					testRunner.ClickByName("Feed Rate NumberEdit");
					testRunner.Type(targetFeedRate.ToString());

					// Force focus away from the feed rate field, causing an persisted update
					testRunner.ClickByName("Extrusion Multiplier NumberEdit");

					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate, "After setting TextEdit values");

					// Wait for slicing to complete before setting target values
					testRunner.WaitFor(() => printer.Connection.DetailedPrintingState == DetailedPrintingState.Printing, 8);
					testRunner.Delay();

					// Values should remain after print completes
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate, "While printing");

					// Wait for printing to complete
					printFinishedResetEvent.WaitOne();

					testRunner.WaitForPrintFinished(printer);

					// Values should match entered values
					testRunner.StartPrint();
					testRunner.WaitFor(() => printer.Connection.CommunicationState == CommunicationStates.Printing, 15);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate, "While reprinting");

					testRunner.CancelPrint();
					testRunner.WaitFor(() => printer.Connection.CommunicationState == CommunicationStates.Connected, 15);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate, "After cancel");
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

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator(runSlow: true))
				{
					testRunner.NavigateToFolder("SD Card Row Item Collection");

					testRunner.ClickByName("Row Item Item 1.gcode");

					testRunner.ClickByName("Print Library Overflow Menu");
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

					testRunner.CloseMatterControl();

					testRunner.ClickByName("Yes Button");

					testRunner.Delay(2);
					Assert.AreEqual(0, tempChangedCount, "We should not change this while exiting an sd card print.");
					Assert.AreEqual(0, fanChangedCount, "We should not change this while exiting an sd card print.");
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 90);
		}

		[Test, Category("Emulator")]
		public async Task CancelingPrintTurnsHeatAndFanOff()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button");

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					var resetEvent = new AutoResetEvent(false);

					Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "One printer should exist after add");

					testRunner.AddItemToBedplate();

					testRunner.StartPrint();

					int fanChangedCount = 0;
					emulator.FanSpeedChanged += (s, e) =>
					{
						fanChangedCount++;
					};

					var printer = testRunner.FirstPrinter();

					emulator.WaitForLayer(printer.Settings, 2);
					emulator.RunSlow = true;

					// Click close but cancel
					testRunner.CloseMatterControl();
					testRunner.ClickByName("No Button");

					// Wait for close
					testRunner.WaitForWidgetDisappear("Yes Button", 4);
					testRunner.Delay(2);

					// Confirm abort
					Assert.IsFalse(AppContext.RootSystemWindow.HasBeenClosed, "Canceling Close dialog should *not* close MatterControl");

					// Close MatterControl and cancel print
					testRunner.CloseMatterControl();
					testRunner.ClickByName("Yes Button");

					// Wait for Disconnected CommunicationState which occurs after PrinterConnection.Disable()
					testRunner.WaitForCommunicationStateDisconnected(printer, maxSeconds: 30);

					// Wait for close
					testRunner.WaitForWidgetDisappear("Yes Button", 4);
					testRunner.Delay(2);

					// Confirm close
					Assert.IsTrue(AppContext.RootSystemWindow.HasBeenClosed, "Confirming Close dialog *should* close MatterControl");

					// Wait for M106 change
					testRunner.WaitFor(() => fanChangedCount > 0, 15, 500);

					// Assert expected temp targets and fan transitions
					Assert.AreEqual(0, (int) emulator.CurrentExtruder.TargetTemperature, "Unexpected target temperature - MC close should call Connection.Disable->TurnOffBedAndExtruders to shutdown heaters");
					Assert.AreEqual(0, (int) emulator.HeatedBed.TargetTemperature, "Unexpected target temperature - MC close should call Connection.Disable->TurnOffBedAndExtruders to shutdown heaters");
					Assert.AreEqual(1, fanChangedCount, "Unexpected fan speed change count - MC close should call Connection.Disable which shuts down fans via M106");
				}

				return Task.CompletedTask;
			}, overrideHeight: 900, maxTimeToRun: 90);
		}

		private static void ConfirmExpectedSpeeds(AutomationRunner testRunner, double targetExtrusionRate, double targetFeedRate, string scope)
		{
			SystemWindow systemWindow;
			SolidSlider slider;

			// Assert the UI has the expected values
			slider = testRunner.GetWidgetByName("Extrusion Multiplier Slider", out systemWindow, 5) as SolidSlider;
			testRunner.WaitFor(() => targetExtrusionRate == slider.Value);

			Assert.AreEqual(targetExtrusionRate, slider.Value, $"Unexpected Extrusion Rate Slider Value - {scope}");

			slider = testRunner.GetWidgetByName("Feed Rate Slider", out systemWindow, 5) as SolidSlider;
			testRunner.WaitFor(() => targetFeedRate == slider.Value);
			Assert.AreEqual(targetFeedRate, slider.Value, $"Unexpected Feed Rate Slider Value - {scope}");

			// Assert the changes took effect on the model
			testRunner.WaitFor(() => targetExtrusionRate == ExtrusionMultiplierStream.ExtrusionRatio);
			Assert.AreEqual(targetExtrusionRate, ExtrusionMultiplierStream.ExtrusionRatio, $"Unexpected Extrusion Rate - {scope}");

			testRunner.WaitFor(() => targetFeedRate == FeedRateMultiplierStream.FeedRateRatio);
			Assert.AreEqual(targetFeedRate, FeedRateMultiplierStream.FeedRateRatio, $"Unexpected Feed Rate - {scope}");
		}
	}
}
