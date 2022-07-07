using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.PrintHistory;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PrinterEmulator;
using MatterHackers.VectorMath;
using NUnit.Framework;
using TestInvoker;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), Parallelizable(ParallelScope.Children)]
	public class PrintingTests
	{
		[Test, ChildProcessTest, Category("Emulator")]
		public async Task CompletingPrintTurnsoffHeat()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button", 1);

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "One printer should be defined after add");

					testRunner.SelectSliceSettingsField(SettingsKey.end_gcode);

					testRunner.Type("^a");
					testRunner.Type("{BACKSPACE}");
					testRunner.Type("G28");

					testRunner.SelectSliceSettingsField(SettingsKey.start_gcode);

					var printer = testRunner.FirstPrinter();

					// TODO: Failure persisting GCode / MultilineTextField value
					//       Expected string length 3 but was 2.Strings differ at index 0.
					//       Shift-G is being swallowed by something.

					// Validate GCode fields persist values
					Assert.AreEqual(
						"G28",
						printer.Settings.GetValue(SettingsKey.end_gcode),
						"Failure persisting GCode/MultilineTextField value");

					testRunner.AddItemToBed();

					// Shorten the delay so the test runs in a reasonable time
					printer.Connection.TimeToHoldTemperature = 5;

					testRunner.StartPrint(printer);

					// Wait for print to finish
					testRunner.WaitForPrintFinished(printer);

					// Wait for expected temp
					testRunner.WaitFor(() => printer.Connection.GetActualHotendTemperature(0) <= 0, 10);
					Assert.Less(printer.Connection.GetActualHotendTemperature(0), 30);

					// Wait for expected temp
					testRunner.WaitFor(() => printer.Connection.ActualBedTemperature <= 10);
					Assert.Less(printer.Connection.ActualBedTemperature, 10);

					// Make sure we can run this whole thing again
					testRunner.StartPrint(printer);

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

		[Test, ChildProcessTest, Category("Emulator")]
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

					testRunner.OpenPrintPopupMenu()
						.ClickByName("SetupPrinter")
						.Complete9StepLeveling()
						.AddItemToBed();

					var printer = testRunner.FirstPrinter();

					var currentSettings = printer.Settings;
					currentSettings.SetValue(SettingsKey.pause_gcode, "");
					currentSettings.SetValue(SettingsKey.resume_gcode, "");

					testRunner.StartPrint(printer, pauseAtLayers: "2");

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

					testRunner.StartPrint(printer, pauseAtLayers: "2");

					testRunner.WaitForName("Yes Button", 20); // the yes button is 'Resume'

					// the user types in the pause layer 1 based and we are 0 based, so we should be on: user 2, printer 1.
					Assert.AreEqual(1, printer.Connection.CurrentlyPrintingLayer);
					// assert the leveling is working
					Assert.AreEqual(12.25, emulator.Destination.Z);

					// NOTE: System.Exception : WaitForWidgetEnabled Failed: Named GuiWidget not found [Print Progress Dial]
					//       Might be fixed in CancelPrint now.
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

					testRunner.StartPrint(printer, pauseAtLayers: "2");

					testRunner.WaitForName("Yes Button", 20); // the yes button is 'Resume'

					// the user types in the pause layer 1 based and we are 0 based, so we should be on: user 2, printer 1.
					Assert.AreEqual(1, printer.Connection.CurrentlyPrintingLayer);

					// assert the leveling is working
					Assert.AreEqual(5.25, emulator.Destination.Z);
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 230);
		}

		[Test, ChildProcessTest, Category("Emulator")]
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
				"MatterControl Printer Emulator",
				"Commands:",
				"    SLOW // make the emulator simulate actual printing speeds (default)",
				"    FAST // run as fast as possible",
				"    THROWERROR // generate a simulated error for testing",
				"Emulating:",
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

		[Test, ChildProcessTest, Category("Emulator")]
		public async Task PrinterRequestsResumeWorkingAsExpected()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "One printer should be defined after add");

					// print a part
					testRunner.AddItemToBed();

					testRunner.StartPrint(testRunner.FirstPrinter(), pauseAtLayers: "2;6");

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
			}, maxTimeToRun: 90 * 2); // Once timed out at 90.
		}

		[Test, ChildProcessTest, Category("Emulator")]
		public async Task PrinterDeletedWhilePrinting()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "One printer should exist after add");

					var printer = testRunner.FirstPrinter();

					// print a part
					testRunner.AddItemToBed();
					testRunner.StartPrint(printer, pauseAtLayers: "2");
					ProfileManager.DebugPrinterDelete = true;

					// Wait for pause dialog
					testRunner.ClickResumeButton(printer, true, 1);

					// Wait for done
					testRunner.WaitForPrintFinished(printer);
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 180);
		}

		[Test, ChildProcessTest, Category("Emulator")]
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

					Assert.IsTrue(printer.Connection.RecoveryIsEnabled);

					// print a part
					testRunner.AddItemToBed()
						.StartPrint(printer, pauseAtLayers: "2;4;6")
						.ClickResumeButton(printer, true, 1) // Resume
						.ClickResumeButton(printer, false, 3) // close the pause dialog pop-up do not resume
						.ClickByName("Disconnect from printer button")
						.ClickByName("Yes Button") // accept the disconnect
						//.ClickByName("Cancel Wizard Button") // click the close on the collect info dialog
						.ClickByName("Connect to printer button") // Reconnect
						.WaitFor(() => printer.Connection.CommunicationState == CommunicationStates.Connected);

					// Assert that recovery happens
					Assert.IsTrue(PrintRecovery.RecoveryAvailable(printer), "Recovery should be enabled after Disconnect while printing");

					// Recover the print
					testRunner.ClickButton("Yes Button", "Recover Print")
						.ClickResumeButton(printer, true, 5) // The first pause that we get after recovery should be layer 6.
						.WaitForPrintFinished(printer);
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 180);
		}

		[Test, ChildProcessTest, Category("Emulator")]
		public async Task TemperatureTowerWorks()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "One printer should exist after add");

					var printer = testRunner.FirstPrinter();

					bool foundTemp = false;
					printer.Connection.LineSent += (s, e) =>
					{
						if (e.Contains("M104 S222.2"))
						{
							foundTemp = true;
						}
					};

					// print a part
					testRunner.AddItemToBed()
						.AddItemToBed("Scripting Row Item Collection", "Row Item Set Temperature")
						.DragDropByName("MoveInZControl", "MoveInZControl", offsetDrag: new Point2D(0, 0), offsetDrop: new Point2D(0, 10))
						.ClickByName("Temperature Edit")
						.Type("222.2")
						.StartPrint(printer)
						.WaitFor(() => printer.Connection.CommunicationState == CommunicationStates.FinishedPrint, 60);
						// TODO: finish export test
						//.ExportPrintAndLoadGCode(printer, out string gcode);

					Assert.IsTrue(foundTemp);
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 180);
		}

		[Test, ChildProcessTest, Category("Emulator")]
		public async Task RecoveryT1NoProbe()
		{
			await ExtruderT1RecoveryTest("Airwolf 3D", "HD");
		}

		[Test, ChildProcessTest, Category("Emulator")]
		public async Task RecoveryT1WithProbe()
		{
			await ExtruderT1RecoveryTest("FlashForge", "Creator Dual");
		}

		public async Task ExtruderT1RecoveryTest(string make, string model)
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator(make, model))
				{
					Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "One printer should exist after add");

					var printer = testRunner.FirstPrinter();
					testRunner.ChangeSettings(
						new (string, string)[]
						{
							(SettingsKey.recover_is_enabled, "1"),
							(SettingsKey.extruder_count, "2"),
							(SettingsKey.has_hardware_leveling, "0"),
						}, printer);

					Assert.IsTrue(printer.Connection.RecoveryIsEnabled);

					// print a part
					testRunner.AddItemToBed()
						.ClickByName("ItemMaterialButton")
						.ClickByName("Material 2 Button")
						.StartPrint(printer, pauseAtLayers: "2;3;4;5");

					testRunner.ClickResumeButton(printer, true, 1); // Resume
																	// make sure we are printing with extruder 2 (T1)
					Assert.AreEqual(0, printer.Connection.GetTargetHotendTemperature(0));
					Assert.Greater(printer.Connection.GetTargetHotendTemperature(1), 0);

					testRunner.ClickResumeButton(printer, false, 2) // close the pause dialog pop-up do not resume
						.ClickByName("Disconnect from printer button")
						.ClickByName("Yes Button") // Are you sure?
						.ClickByName("Connect to printer button") // Reconnect
						.WaitFor(() => printer.Connection.CommunicationState == CommunicationStates.Connected);

					// Assert that recovery happens
					Assert.IsTrue(PrintRecovery.RecoveryAvailable(printer), "Recovery should be enabled after Disconnect while printing");

					// Recover the print
					testRunner.ClickButton("Yes Button", "Recover Print");

					// The first pause that we get after recovery should be layer 4 (index 3).
					testRunner.ClickResumeButton(printer, true, 3);
					// make sure we are printing with extruder 2 (T1)
					Assert.AreEqual(0, printer.Connection.GetTargetHotendTemperature(0));
					Assert.Greater(printer.Connection.GetTargetHotendTemperature(1), 0);

					testRunner.ClickResumeButton(printer, true, 4);

					testRunner.WaitForPrintFinished(printer);
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 180);
		}

		[Test, ChildProcessTest, Category("Emulator")]
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

					testRunner.AddItemToBed();

					testRunner.SwitchToControlsTab();

					var printer = testRunner.FirstPrinter();

					// Wait for printing to complete
					var printFinishedResetEvent = new AutoResetEvent(false);
					printer.Connection.PrintFinished += (s, e) =>
					{
						printFinishedResetEvent.Set();
					};

					testRunner.StartPrint(printer)
						.ScrollIntoView("Extrusion Multiplier NumberEdit")
						.ScrollIntoView("Feed Rate NumberEdit");

					testRunner.PausePrint();

					// Tuning values should default to 1 when missing
					ConfirmExpectedSpeeds(testRunner, 1, 1, "Initial case");

					testRunner.Delay()
						.ClickByName("Extrusion Multiplier NumberEdit")
						.Type(targetExtrusionRate.ToString())
						.ClickByName("Feed Rate NumberEdit")
						.Type(targetFeedRate.ToString())
						// Force focus away from the feed rate field, causing an persisted update
						.ClickByName("Extrusion Multiplier NumberEdit");

					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate, "After setting TextEdit values");

					// Wait for slicing to complete before setting target values
					testRunner.WaitFor(() => printer.Connection.DetailedPrintingState == DetailedPrintingState.Printing, 8);
					testRunner.Delay();

					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate, "While printing");

					testRunner.ResumePrint();

					// Wait up to 60 seconds for the print to finish
					printFinishedResetEvent.WaitOne(60 * 1000);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate, "After print finished");

					testRunner.WaitForPrintFinished(printer)
						.StartPrint(printer) // Restart the print
						.Delay(1);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate, "After print restarted");

					testRunner.CancelPrint()
						.Delay(1);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate, "After canceled print");
				}

				return Task.CompletedTask;
			}, overrideHeight: 900, maxTimeToRun: 120);
		}

		[Test, ChildProcessTest, Category("Emulator")]
		public async Task TuningAdjustmentControlsBoundToStreamValues()
		{
			double targetExtrusionRate = 1.5;
			double targetFeedRate = 2;

			double initialExtrusionRate = 0.6;
			double initialFeedRate = 0.7;

			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button");

				// Then validate that they are picked up
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "One printer should be defined after add");

					testRunner.AddItemToBed();

					testRunner.SwitchToControlsTab();

					var printer = testRunner.FirstPrinter();

					// Set custom adjustment values
					printer.Settings.SetValue(SettingsKey.feedrate_ratio, initialFeedRate.ToString());
					printer.Settings.SetValue(SettingsKey.extrusion_ratio, initialExtrusionRate.ToString());

					var printFinishedResetEvent = new AutoResetEvent(false);
					printer.Connection.PrintFinished += (s, e) => printFinishedResetEvent.Set();

					testRunner.StartPrint(printer);

					testRunner.PausePrint();

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

					testRunner.ResumePrint();

					// Wait for printing to complete
					printFinishedResetEvent.WaitOne();

					testRunner.WaitForPrintFinished(printer);

					// Values should match entered values
					testRunner.StartPrint(printer);
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

		[Test, ChildProcessTest, Category("Emulator")]
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

		[Test, ChildProcessTest, Category("Emulator")]
		public async Task CancelingPrintTurnsHeatAndFanOff()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button");

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					var resetEvent = new AutoResetEvent(false);

					Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "One printer should exist after add");

					testRunner.AddItemToBed();

					var printer = testRunner.FirstPrinter();

					testRunner.StartPrint(printer);

					int fanChangedCount = 0;
					emulator.FanSpeedChanged += (s, e) =>
					{
						fanChangedCount++;
					};

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
			SolidSlider slider;

			// Assert the UI has the expected values
			slider = testRunner.GetWidgetByName("Extrusion Multiplier Slider", out _) as SolidSlider;
			testRunner.WaitFor(() => targetExtrusionRate == slider.Value);

			Assert.AreEqual(targetExtrusionRate, slider.Value, $"Unexpected Extrusion Rate Slider Value - {scope}");

			slider = testRunner.GetWidgetByName("Feed Rate Slider", out _) as SolidSlider;
			testRunner.WaitFor(() => targetFeedRate == slider.Value);
			Assert.AreEqual(targetFeedRate, slider.Value, $"Unexpected Feed Rate Slider Value - {scope}");

			var printer = testRunner.FirstPrinter();

			// Assert the changes took effect on the model
			testRunner.WaitFor(() => targetExtrusionRate == printer.Connection.ExtrusionMultiplierStream.ExtrusionRatio);
			Assert.AreEqual(targetExtrusionRate, printer.Connection.ExtrusionMultiplierStream.ExtrusionRatio, $"Unexpected Extrusion Rate - {scope}");

			testRunner.WaitFor(() => targetFeedRate == printer.Connection.FeedRateMultiplierStream.FeedRateRatio);
			Assert.AreEqual(targetFeedRate, printer.Connection.FeedRateMultiplierStream.FeedRateRatio, $"Unexpected Feed Rate - {scope}");
		}
	}
}
