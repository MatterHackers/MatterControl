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

					testRunner.SelectSliceSettingsField("Printer", "end_gcode");

					testRunner.Type("^a");
					testRunner.Type("{BACKSPACE}");
					testRunner.Type("G28");

					testRunner.AddItemToBedplate();

					testRunner.StartPrint();

					// Wait for print to finish
					testRunner.WaitForPrintFinished();

					// Wait for expected temp
					testRunner.WaitFor(() => ApplicationController.Instance.ActivePrinter.Connection.GetActualHotendTemperature(0) <= 0);
					Assert.Less(ApplicationController.Instance.ActivePrinter.Connection.GetActualHotendTemperature(0), 30);

					// Wait for expected temp
					testRunner.WaitFor(() => ApplicationController.Instance.ActivePrinter.Connection.ActualBedTemperature <= 10);
					Assert.Less(ApplicationController.Instance.ActivePrinter.Connection.ActualBedTemperature, 10);

					// Make sure we can run this whole thing again
					testRunner.StartPrint();

					// Wait for print to finish
					testRunner.WaitForPrintFinished();

					// Wait for expected temp
					testRunner.WaitFor(() => ApplicationController.Instance.ActivePrinter.Connection.GetActualHotendTemperature(0) <= 0);
					Assert.Less(ApplicationController.Instance.ActivePrinter.Connection.GetActualHotendTemperature(0), 30);

					// Wait for expected temp
					testRunner.WaitFor(() => ApplicationController.Instance.ActivePrinter.Connection.ActualBedTemperature <= 10);
					Assert.Less(ApplicationController.Instance.ActivePrinter.Connection.ActualBedTemperature, 10);
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 70);
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

					// Cancel in this case is on the Leveling Wizard, results in ReloadAll and for consistency across devices, requires we wait till it completes
					testRunner.WaitForReloadAll(() => testRunner.ClickByName("Cancel Button"));

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

					testRunner.WaitForReloadAll(() => testRunner.ClickByName("Done Button"));

					// print a part
					testRunner.AddItemToBedplate();
					testRunner.StartPrint();

					emulator.WaitForLayer(ActiveSliceSettings.Instance.printer.Settings, 2);

					testRunner.WaitFor(() => emulator.ZPosition > 5);

					// assert the leveling is working
					Assert.Greater(emulator.ZPosition, 5);

					testRunner.CancelPrint();
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
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					testRunner.OpenPrintPopupMenu();
					testRunner.ClickByName("Layer(s) To Pause Field");
					testRunner.Type("2;6");

					// print a part
					testRunner.AddItemToBedplate();

					testRunner.StartPrint();

					// turn on line error simulation
					emulator.SimulateLineErrors = true;

					// close the pause dialog pop-up (resume)
					testRunner.WaitForName("No Button", 90);// the no button is 'Resume'
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

		[Test, Category("Emulator")]
		public async Task PrinterRecoveryTest()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					ActiveSliceSettings.Instance.SetValue(SettingsKey.recover_is_enabled, "1");
					ActiveSliceSettings.Instance.SetValue(SettingsKey.has_hardware_leveling, "0");

					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					// TODO: Delay needed to work around timing issue in MatterHackers/MCCentral#2415
					testRunner.Delay(1);

					testRunner.OpenPrintPopupMenu();
					testRunner.ClickByName("Layer(s) To Pause Field");
					testRunner.Type("2;4;6");

					// print a part
					testRunner.AddItemToBedplate();
					testRunner.StartPrint();

					// Dismiss pause dialog
					testRunner.WaitForName("No Button", 90); // the no button is 'Resume'
					
					// validate the current layer
					Assert.AreEqual(2, ApplicationController.Instance.ActivePrinter.Connection.CurrentlyPrintingLayer);
					testRunner.ClickByName("No Button");

					// the printer is now paused
					// close the pause dialog pop-up do not resume
					ClickDialogButton(testRunner, "Yes Button", 4);

					// Disconnect 
					testRunner.ClickByName("Disconnect from printer button");

					// Reconnect
					testRunner.WaitForName("Connect to printer button", 10);
					testRunner.ClickByName("Connect to printer button");

					testRunner.WaitFor(() => ApplicationController.Instance.ActivePrinter.Connection.CommunicationState == CommunicationStates.Connected);

					// Assert that recovery happens

					// Recover the print
					ClickDialogButton(testRunner, "Yes Button", -1);

					// The first pause that we get after recovery should be layer 6.
					// wait for the pause and continue
					ClickDialogButton(testRunner, "No Button", 6);

					// Wait for done
					testRunner.WaitForPrintFinished();
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 180);
		}

		private static void ClickDialogButton(AutomationRunner testRunner, string buttonName, int expectedLayer)
		{
			testRunner.WaitForName(buttonName, 90);
			Assert.AreEqual(expectedLayer, ApplicationController.Instance.ActivePrinter.Connection.CurrentlyPrintingLayer);
			testRunner.ClickByName(buttonName);
			testRunner.WaitFor(() => !testRunner.NameExists(buttonName), 1);
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

					testRunner.AddItemToBedplate();

					testRunner.SwitchToControlsTab();

					// Wait for printing to complete
					var printFinishedResetEvent = new AutoResetEvent(false);
					ApplicationController.Instance.ActivePrinter.Connection.PrintFinished.RegisterEvent((s, e) => printFinishedResetEvent.Set(), ref unregisterEvents);

					testRunner.StartPrint();

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
					testRunner.WaitFor(() => ApplicationController.Instance.ActivePrinter.Connection.DetailedPrintingState == DetailedPrintingState.Printing, 8);
					testRunner.Delay();

					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					printFinishedResetEvent.WaitOne();

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					testRunner.WaitForPrintFinished();

					// Restart the print
					testRunner.StartPrint();
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

					testRunner.AddItemToBedplate();

					testRunner.SwitchToControlsTab();

					var printer = ApplicationController.Instance.ActivePrinter;

					var printFinishedResetEvent = new AutoResetEvent(false);
					printer.Connection.PrintFinished.RegisterEvent((s, e) => printFinishedResetEvent.Set(), ref unregisterEvents);

					testRunner.StartPrint();

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
					testRunner.WaitFor(() => printer.Connection.DetailedPrintingState == DetailedPrintingState.Printing, 8);
					testRunner.Delay();

					// Values should remain after print completes
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					// Wait for printing to complete
					printFinishedResetEvent.WaitOne();

					testRunner.WaitForPrintFinished();

					// Values should match entered values
					testRunner.StartPrint();
					testRunner.WaitFor(() => printer.Connection.CommunicationState == CommunicationStates.Printing, 15);

					// Values should match entered values
					ConfirmExpectedSpeeds(testRunner, targetExtrusionRate, targetFeedRate);

					testRunner.CancelPrint();
					testRunner.WaitFor(() => printer.Connection.CommunicationState == CommunicationStates.Connected, 15);

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

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator(runSlow: true))
				{
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					testRunner.WaitForName("SD Card Row Item Collection");
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
			}, overrideHeight: 900, maxTimeToRun: 90);
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

					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					testRunner.AddItemToBedplate();

					testRunner.StartPrint();
					
					int fanChangedCount = 0;
					emulator.FanSpeedChanged += (s, e) =>
					{
						fanChangedCount++;
					};

					var printer = ApplicationController.Instance.ActivePrinter;

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
					testRunner.WaitForCommunicationStateDisconnected(maxSeconds: 30);

					// Wait for close
					testRunner.WaitForWidgetDisappear("Yes Button", 4);
					testRunner.Delay(2);

					// Confirm close
					Assert.IsTrue(AppContext.RootSystemWindow.HasBeenClosed, "Confirming Close dialog *should* close MatterControl");

					// Wait for M106 change
					testRunner.WaitFor(() => fanChangedCount > 0, 15, 500);

					// Assert expected temp targets and fan transitions
					Assert.AreEqual(0, (int) emulator.Hotend.TargetTemperature, "Unexpected target temperature - MC close should call Connection.Disable->TurnOffBedAndExtruders to shutdown heaters");
					Assert.AreEqual(0, (int) emulator.HeatedBed.TargetTemperature, "Unexpected target temperature - MC close should call Connection.Disable->TurnOffBedAndExtruders to shutdown heaters");
					Assert.AreEqual(1, fanChangedCount, "Unexpected fan speed change count - MC close should call Connection.Disable which shuts down fans via M106");
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
			Assert.IsTrue(targetExtrusionRate == slider.Value);

			slider = testRunner.GetWidgetByName("Feed Rate Slider", out systemWindow, 5) as SolidSlider;
			Assert.IsTrue(targetFeedRate == slider.Value);

			testRunner.Delay(.2);

			// Assert the changes took effect on the model
			Assert.IsTrue(targetExtrusionRate == ExtrusionMultiplyerStream.ExtrusionRatio);
			Assert.IsTrue(targetFeedRate == FeedRateMultiplyerStream.FeedRateRatio);
		}
	}
}
