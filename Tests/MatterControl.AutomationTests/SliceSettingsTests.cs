using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class SliceSetingsTests
	{
		[Test]
		public async Task RaftEnabledPassedToSliceEngine()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				testRunner.AddTestAssetsToLibrary("Rook.amf");

				testRunner.AddItemToBedplate("", "Row Item Rook");

				testRunner.SwitchToSliceSettings();
				testRunner.ClickByName("Raft / Skirt / Brim Tab");
				testRunner.ClickByName("Create Raft Field");

				testRunner.StartSlicing();
				testRunner.Delay(() => MatterControlUtilities.CompareExpectedSliceSettingValueWithActualVaue("enableRaft", "True"), 10);

				// Call compare slice settings method here
				Assert.IsTrue(MatterControlUtilities.CompareExpectedSliceSettingValueWithActualVaue("enableRaft", "True"));

				return Task.CompletedTask;
			}, overrideWidth: 1224, overrideHeight: 800);
		}

		[Test, Category("Emulator")]
		public async Task PauseOnLayerDoesPauseOnPrint()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button");

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					testRunner.OpenPrintPopupMenu();
					testRunner.ClickByName("Layer(s) To Pause Field");
					testRunner.Type("4;2;a;not;6");

					testRunner.AddItemToBedplate();

					testRunner.OpenGCode3DOverflowMenu();
					testRunner.ClickByName("Sync To Print Menu Item");

					testRunner.StartPrint();

					WaitForLayerAndResume(testRunner, 2);
					WaitForLayerAndResume(testRunner, 4);
					WaitForLayerAndResume(testRunner, 6);

					testRunner.WaitForPrintFinished();
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 120);
		}

		[Test, Category("Emulator")]
		public async Task CancelWorksAsExpected()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					var printer = ApplicationController.Instance.ActivePrinter;

					printer.Settings.SetValue(SettingsKey.cancel_gcode, "G28 ; Cancel GCode");

					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					testRunner.OpenPrintPopupMenu();

					testRunner.ClickByName("Layer(s) To Pause Field");
					testRunner.Type("2");

					testRunner.AddItemToBedplate();

					// Turn on Sync-to-print
					testRunner.OpenGCode3DOverflowMenu();
					testRunner.ClickByName("Sync To Print Menu Item");

					testRunner.StartPrint();

					// Wait for the Ok button
					testRunner.WaitForName("Yes Button", 30);
					testRunner.ClickByName("Yes Button");

					// Cancel the Printing task
					testRunner.ClickByName("Stop Task Button");

					// Wait for and assert that printing has been canceled
					testRunner.Delay(() => printer.Connection.CommunicationState == PrinterCommunication.CommunicationStates.Connected);
					Assert.IsTrue(printer.Connection.CommunicationState == PrinterCommunication.CommunicationStates.Connected);

					// Assert that two G28s were output to the terminal
					int g28Count = printer.Connection.TerminalLog.PrinterLines.Where(line => line.Contains("G28")).Count();
					Assert.AreEqual(2, g28Count, "The terminal log should contain one G28 from Start-GCode and one G28 from Cancel-GCode");
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 120);
		}

		private static void WaitForLayerAndResume(AutomationRunner testRunner, int indexToWaitFor)
		{
			testRunner.WaitForName("No Button", 30);
			
			var printer = ApplicationController.Instance.ActivePrinter;

			testRunner.Delay(() => printer.Bed.ActiveLayerIndex + 1 == indexToWaitFor, 30, 500);
			Assert.AreEqual(indexToWaitFor, printer.Bed.ActiveLayerIndex + 1);

			testRunner.ClickByName("No Button");
		}

		[Test /* Test will fail if screen size is and "HeatBeforeHoming" falls below the fold */]
		public async Task ClearingCheckBoxClearsUserOverride()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				// Navigate to Local Library 
				testRunner.SwitchToSliceSettings();

				testRunner.ClickByName("Printer Tab");
				testRunner.ClickByName("Features Tab");

				// Find any sibling toggle switch and scroll the parent to the bottom
				var widget = testRunner.GetWidgetByName("has_fan Row", out _);
				var scrollable = widget.Parents<ScrollableWidget>().First();
				scrollable.ScrollPosition = new Vector2(0, -100);

				CheckAndUncheckSetting(testRunner, SettingsKey.heat_extruder_before_homing, false);

				CheckAndUncheckSetting(testRunner, SettingsKey.has_fan, true);

				return Task.CompletedTask;
			}, overrideWidth: 1224, overrideHeight: 900);
		}

		[Test]
		public async Task DualExtrusionShowsCorrectHotEndData()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					// Navigate to Local Library 
					testRunner.SwitchToSliceSettings();

					testRunner.ClickByName("Printer Tab");
					testRunner.ClickByName("Features Tab");

					// only 1 hotend and 1 extruder
					Assert.IsTrue(testRunner.NameExists("Hotend 0"));
					Assert.IsFalse(testRunner.NameExists("Hotend 1", .1));

					testRunner.ClickByName("Hotend 0");

					// assert the temp is set when we first open (it comes from the material)
					EditableNumberDisplay tempWidget = testRunner.GetWidgetByName("Temperature Input", out _) as EditableNumberDisplay;
					Assert.AreEqual(240, (int)tempWidget.Value);

					// change material
					var dropDownLists = testRunner.GetWidgetsByName("Hotend Preset Selector");
					Assert.AreEqual(1, dropDownLists.Count, "There is one. The slice settings and the pop out.");
					DropDownList materialSelector = dropDownLists[0].Widget as DropDownList;
					Assert.AreEqual("", materialSelector.SelectedValue);

					testRunner.ClickByName("Hotend Preset Selector");
					testRunner.ClickByName("HIPS Menu");

					// check the extruder count
					var extrudeButtons = testRunner.GetWidgetsByName("Extrude Button");
					Assert.AreEqual(1, extrudeButtons.Count, "There should be just one.");

					int hipsGoalTemp = 220;

					// assert the temp changed to a new temp
					Assert.AreEqual(hipsGoalTemp,(int) tempWidget.Value, "The goal temp should match the material temp");
					// and the printer heat is off
					Assert.AreEqual(0, (int) emulator.ExtruderGoalTemperature, "The printer should report the heaters are off");

					// turn on the heater
					testRunner.ClickByName("Toggle Heater");
					testRunner.Delay(2);

					// assert the printer is heating
					Assert.AreEqual(hipsGoalTemp, (int)emulator.ExtruderGoalTemperature, "The printer should report the expected goal temp");

					// turn off the heater
					testRunner.ClickByName("Toggle Heater");
					testRunner.Delay(2);

					// assert the printer is off
					Assert.AreEqual(0, (int)emulator.ExtruderGoalTemperature, "The printer should report the heaters are off");

					// type in a temp when the heating is off
					testRunner.ClickByName("Temperature Input");
					testRunner.Type("110");
					testRunner.Type("{Enter}");
					testRunner.Delay();

					// assert the printer is off
					Assert.AreEqual(0, (int)emulator.ExtruderGoalTemperature);

					// and the heat toggle is showing on
					CheckBox heatToggle = testRunner.GetWidgetByName("Toggle Heater", out _) as CheckBox;
					Assert.IsFalse(heatToggle.Checked);

					// turn it on
					testRunner.ClickByName("Toggle Heater");
					Assert.AreEqual(110, (int)emulator.ExtruderGoalTemperature);

					// adjust when on
					testRunner.ClickByName("Temperature Input");
					testRunner.Type("104");
					testRunner.Type("{Enter}");
					testRunner.Delay();
					Assert.AreEqual(104, (int)emulator.ExtruderGoalTemperature);

					// type in 0 and have the heater turn off
					testRunner.ClickByName("Temperature Input");
					testRunner.Type("0");
					testRunner.Type("{Enter}");
					testRunner.Delay();

					// assert the printer is not heating
					Assert.AreEqual(0, (int)emulator.ExtruderGoalTemperature);
					// and the on toggle is showing off
					Assert.IsFalse(heatToggle.Checked);

					testRunner.ClickByName("Extruder Tab");

					testRunner.ClickByName(SliceSettingsOrganizer.Instance.GetSettingsData(SettingsKey.extruder_count).PresentationName + " Field");
					testRunner.Type("2");
					testRunner.Type("{Enter}");

					// there are now 2 hotends and 2 extruders
					Assert.IsTrue(testRunner.NameExists("Hotend 0"));
					Assert.IsTrue(testRunner.NameExists("Hotend 1"));

					SetCheckBoxSetting(testRunner, SettingsKey.extruders_share_temperature, true);

					// there is one hotend and 2 extruders
					Assert.IsTrue(testRunner.NameExists("Hotend 0"));
					Assert.IsFalse(testRunner.NameExists("Hotend 1", .1));

					testRunner.ClickByName("Hotend 0");

					extrudeButtons = testRunner.GetWidgetsByName("Extrude Button");
					Assert.AreEqual(2, extrudeButtons.Count, "Now there should be two.");
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 666, overrideWidth: 1224, overrideHeight: 900);
		}

		[Test /* Test will fail if screen size is and "HeatBeforeHoming" falls below the fold */]
		public async Task SwitchingMaterialsCausesSettingsChangedEvents()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				EventHandler unregisterEvents = null;
				int layerHeightChangedCount = 0;

				ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
				{
					var stringEvent = e as StringEventArgs;
					if (stringEvent != null)
					{
						if (stringEvent.Data == SettingsKey.layer_height)
						{
							layerHeightChangedCount++;
						}
					}
				}, ref unregisterEvents);

				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				// Navigate to Local Library 
				testRunner.SwitchToSliceSettings();

				testRunner.ClickByName("General Tab");
				testRunner.ClickByName("Layers / Surface Tab");
				Assert.AreEqual(0, layerHeightChangedCount, "No change to layer height yet.");

				testRunner.ClickByName("Quality");
				testRunner.ClickByName("Fine Menu");
				testRunner.Delay(.5);
				Assert.AreEqual(1, layerHeightChangedCount, "Changed to fine.");

				testRunner.ClickByName("Quality");
				testRunner.ClickByName("Standard Menu");
				testRunner.Delay(.5);
				Assert.AreEqual(2, layerHeightChangedCount, "Changed to standard.");

				return Task.CompletedTask;
			}, overrideWidth: 1224, overrideHeight: 900);
		}

		[Test]
		public async Task DeleteProfileWorksForGuest()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				// assert no profiles
				Assert.AreEqual(0, ProfileManager.Instance.ActiveProfiles.Count());

				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				// assert one profile
				Assert.AreEqual(1, ProfileManager.Instance.ActiveProfiles.Count(), "One profile should exist after add");

				MatterControlUtilities.DeleteSelectedPrinter(testRunner);

				// assert no profiles
				Assert.AreEqual(0, ProfileManager.Instance.ActiveProfiles.Count(), "No profiles should exist after delete");

				return Task.CompletedTask;
			}, overrideWidth: 1224, overrideHeight: 900);
		}

		private static void SetCheckBoxSetting(AutomationRunner testRunner, string settingToChange, bool valueToSet)
		{
			var settingsData = SliceSettingsOrganizer.Instance.GetSettingsData(settingToChange);
			string checkBoxName = $"{settingsData.PresentationName} Field";

			Assert.IsTrue(ActiveSliceSettings.Instance.GetValue<bool>(settingToChange) != valueToSet);

			testRunner.ClickByName(checkBoxName);
			// give some time for the ui to update if necessary
			testRunner.Delay(2);

			Assert.IsTrue(ActiveSliceSettings.Instance.GetValue<bool>(settingToChange) == valueToSet);
		}

		private static void CheckAndUncheckSetting(AutomationRunner testRunner, string settingToChange, bool expected)
		{
			// Assert that the checkbox is currently unchecked, and there is no user override
			Assert.IsTrue(ActiveSliceSettings.Instance.UserLayer.ContainsKey(settingToChange) == false);

			// Click the checkbox
			SetCheckBoxSetting(testRunner, settingToChange, !expected);

			// Assert the checkbox is checked and the user override is set
			Assert.IsTrue(ActiveSliceSettings.Instance.UserLayer.ContainsKey(settingToChange) == true);

			// Click the cancel user override button
			testRunner.ClickByName("Restore " + settingToChange);
			testRunner.Delay(2);

			// Assert the checkbox is unchecked and there is no user override
			Assert.IsTrue(ActiveSliceSettings.Instance.GetValue<bool>(settingToChange) == expected);
			Assert.IsTrue(ActiveSliceSettings.Instance.UserLayer.ContainsKey(settingToChange) == false);
		}

		[Test]
		public async Task HasHeatedBedCheckedHidesBedTemperatureOptions()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				// Navigate to Settings Tab and make sure Bed Temp Text box is visible 
				testRunner.SwitchToSliceSettings();

				testRunner.ClickByName("Filament Tab");
				testRunner.ClickByName("Temperatures Tab");

				testRunner.ClickByName("Extruder Temperature Field"); 
				testRunner.ClickByName("Bed Temperature Field");

				// Uncheck Has Heated Bed checkbox and make sure Bed Temp Textbox is not visible
				testRunner.ClickByName("Printer Tab");
				testRunner.ClickByName("Features Tab");

				// Scroll the 'Has Heated Bed' field into view
				testRunner.DragByName("Show Reset Connection Field", 1, offset: new Agg.Point2D(-40, 0));
				testRunner.MoveToByName("Show Reset Connection Field", 1, offset: new Agg.Point2D(0, 120));
				testRunner.Drop();

				testRunner.ClickByName("Has Heated Bed Field");
				testRunner.Delay(.5);

				testRunner.ClickByName("Filament Tab");
				Assert.IsFalse(testRunner.WaitForName("Bed Temperature Textbox", .5), "Filament -> Bed Temp should not be visible after Heated Bed unchecked");

				// Make sure Bed Temperature Options are not visible in printer controls
				testRunner.SwitchToControlsTab();

				Assert.IsFalse(testRunner.WaitForName("Bed Temperature Controls Widget", .5), "Controls -> Bed Temp should not be visible after Heated Bed unchecked");

				return Task.CompletedTask;
			}, overrideWidth: 1300);
		}

		[Test]
		public async Task QualitySettingsStayAsOverrides()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				// Add Guest printers
				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");
				testRunner.SwitchToSliceSettings();

				testRunner.ClickByName("Layer Thickness Field");
				testRunner.Type(".5\n");
				testRunner.Delay(.5);
				Assert.AreEqual(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.layer_height).ToString(), "0.5", "Layer height is what we set it to");
				testRunner.ClickByName("Quality");
				testRunner.ClickByName("Fine Menu");

				testRunner.Delay(.5);
				Assert.AreEqual(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.layer_height).ToString(), "0.1", "Layer height is the fine override");

				testRunner.AddAndSelectPrinter("BCN", "Sigma");

				// Check Guest printer count 
				Assert.AreEqual(2, ProfileManager.Instance.ActiveProfiles.Count(), "ProfileManager has 2 Profiles");

				// Check if Guest printer names exists in dropdown
				testRunner.OpenPrintersDropdown();
				testRunner.ClickByName("Airwolf 3D HD Menu Item");

				testRunner.Delay(1);
				Assert.AreEqual(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.layer_height).ToString(), "0.1", "Layer height is the fine override");

				// Switch to Slice Settings Tab
				testRunner.ClickByName("Slice Settings Tab");

				testRunner.ClickByName("Quality");
				testRunner.ClickByName("- none - Menu Item");
				testRunner.Delay(.5);
				Assert.AreEqual(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.layer_height).ToString(), "0.5", "Layer height is what we set it to");

				return Task.CompletedTask;
			}, maxTimeToRun: 120);
		}
	}
}
