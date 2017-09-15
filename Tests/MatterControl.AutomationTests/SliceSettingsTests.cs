using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
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

				testRunner.ClickByName("Row Item Rook");
				testRunner.AddSelectedItemToBedplate();

				testRunner.SwitchToAdvancedSliceSettings();
				testRunner.ClickByName("Raft / Priming Tab");
				testRunner.ClickByName("Create Raft Field");

				testRunner.ClickByName("Generate Gcode Button");
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

					testRunner.SwitchToAdvancedSliceSettings();

					testRunner.ClickByName("General Tab");
					testRunner.ClickByName("Single Print Tab");
					testRunner.ClickByName("Layer(s) To Pause Field");
					testRunner.Type("4;2;a;not;6");

					testRunner.AddDefaultFileToBedplate();

					testRunner.ClickByName("Generate Gcode Button");

					testRunner.WaitForName("Current GCode Layer Edit");

					testRunner.ClickByName("View3D Overflow Menu");
					testRunner.ClickByName("Sync To Print Checkbox");

					testRunner.ClickByName("Start Print Button");

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
					ActiveSliceSettings.Instance.SetValue(SettingsKey.cancel_gcode, "G28 ; Cancel GCode");

					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					testRunner.SwitchToAdvancedSliceSettings();

					testRunner.ClickByName("General Tab");
					testRunner.ClickByName("Single Print Tab");
					testRunner.ClickByName("Layer(s) To Pause Field");
					testRunner.Type("2");

					testRunner.AddDefaultFileToBedplate();

					testRunner.ClickByName("Generate Gcode Button");
					testRunner.ClickByName("View3D Overflow Menu");
					testRunner.ClickByName("Sync To Print Checkbox");

					testRunner.ClickByName("Start Print Button");

					// assert the leveling is working
					testRunner.WaitForName("Yes Button", 200);
					// close the pause dialog pop-up
					testRunner.ClickByName("Yes Button");

					testRunner.WaitForName("Resume Button", 30);
					testRunner.ClickByName("Cancel Print Button");

					Assert.IsTrue(testRunner.NameExists("Start Print Button"));

					int g28Count = 0;
					foreach(var line in PrinterConnection.Instance.TerminalLog.PrinterLines)
					{
						if(line.Contains("G28"))
						{
							g28Count++;
						}
					}

					Assert.AreEqual(2, g28Count, "There should be the start come and the cancel print home");
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 120);
		}

		private static void WaitForLayerAndResume(AutomationRunner testRunner, int indexToWaitFor)
		{
			// assert the leveling is working
			testRunner.WaitForName("Yes Button", 30);
			// close the pause dialog pop-up
			testRunner.ClickByName("Yes Button");

			var printer = ApplicationController.Instance.Printer;

			testRunner.Delay(() => printer.Bed.ActiveLayerIndex + 1 == indexToWaitFor, 30, 500);

			Assert.AreEqual(indexToWaitFor, printer.Bed.ActiveLayerIndex + 1);
			testRunner.ClickByName("Resume Button");
			testRunner.Delay(.1);
		}

		[Test /* Test will fail if screen size is and "HeatBeforeHoming" falls below the fold */]
		public async Task ClearingCheckBoxClearsUserOverride()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				// Navigate to Local Library 
				testRunner.SwitchToAdvancedSliceSettings();

				testRunner.ClickByName("Printer Tab");
				testRunner.ClickByName("Features Tab");

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
				testRunner.CloseSignInAndPrinterSelect();

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					// Navigate to Local Library 
					testRunner.SwitchToAdvancedSliceSettings();

					testRunner.ClickByName("Printer Tab");
					testRunner.ClickByName("Features Tab");

					// only 1 hotend and 1 extruder
					Assert.IsTrue(testRunner.NameExists("Hotend 0"));
					Assert.IsFalse(testRunner.NameExists("Hotend 1", .1));

					testRunner.ClickByName("Hotend 0");

					// assert the temp is set when we first open (it comes from the material)
					EditableNumberDisplay tempWidget = testRunner.GetWidgetByName("Temperature Input", out _) as EditableNumberDisplay;
					Assert.AreEqual(240, tempWidget.Value);

					// change material
					var dropDownLists = testRunner.GetWidgetsByName("Material DropDown List");
					Assert.AreEqual(2, dropDownLists.Count, "There are two. The slice settings and the pop out.");
					DropDownList materialSelector = dropDownLists[0].widget as DropDownList;
					Assert.AreEqual("", materialSelector.SelectedValue);
					// BUG: the offest should not be required
					testRunner.ClickByName("Material DropDown List", offset: new Point2D(-20, -25));
					testRunner.ClickByName("HIPS Menu");

					// check the extruder count
					var extrudeButtons = testRunner.GetWidgetsByName("Extrude Button");
					Assert.AreEqual(1, extrudeButtons.Count, "There should be just one.");

					int hipsGoalTemp = 220;
					// assert the temp changed to a new temp
					Assert.AreEqual(hipsGoalTemp, tempWidget.Value, "The temp should have changed to ABS");
					// and the printer heat is off
					Assert.AreEqual(0, emulator.ExtruderGoalTemperature);

					// turn on the heater
					testRunner.ClickByName("Toggle Heater");
					testRunner.Delay();

					// assert the printer is heating
					Assert.AreEqual(hipsGoalTemp, emulator.ExtruderGoalTemperature);

					// turn off the heater
					testRunner.ClickByName("Toggle Heater");
					testRunner.Delay();

					// assert the printer is off
					Assert.AreEqual(0, emulator.ExtruderGoalTemperature);

					// type in a temp when the heating is off
					testRunner.ClickByName("Temperature Input");
					testRunner.Type("110");
					testRunner.Type("{Enter}");
					testRunner.Delay();

					// assert the printer is off
					Assert.AreEqual(0, emulator.ExtruderGoalTemperature);

					// and the heat toggle is showing on
					CheckBox heatToggle = testRunner.GetWidgetByName("Toggle Heater", out _) as CheckBox;
					Assert.IsFalse(heatToggle.Checked);

					// turn it on
					testRunner.ClickByName("Toggle Heater");
					Assert.AreEqual(110, emulator.ExtruderGoalTemperature);

					// adjust when on
					testRunner.ClickByName("Temperature Input");
					testRunner.Type("104");
					testRunner.Type("{Enter}");
					testRunner.Delay();
					Assert.AreEqual(104, emulator.ExtruderGoalTemperature);

					// type in 0 and have the heater turn off
					testRunner.ClickByName("Temperature Input");
					testRunner.Type("0");
					testRunner.Type("{Enter}");
					testRunner.Delay();

					// assert the printer is not heating
					Assert.AreEqual(0, emulator.ExtruderGoalTemperature);
					// and the on toggle is showing off
					Assert.IsFalse(heatToggle.Checked);

					testRunner.ClickByName(SliceSettingsOrganizer.Instance.GetSettingsData(SettingsKey.extruder_count).PresentationName + " Edit");
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
			}, maxTimeToRun: 200, overrideWidth: 1224, overrideHeight: 900);
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
				testRunner.SwitchToAdvancedSliceSettings();

				testRunner.ClickByName("General Tab");
				testRunner.ClickByName("Layers / Surface Tab");

				Assert.AreEqual(0, layerHeightChangedCount, "No change to layer height yet.");
				testRunner.ClickByName("Quality");
				testRunner.ClickByName("Fine Menu", delayBeforeReturn: .5);
				Assert.AreEqual(1, layerHeightChangedCount, "Changed to fine.");
				testRunner.ClickByName("Quality");
				testRunner.ClickByName("Standard Menu", delayBeforeReturn: .5);
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
				testRunner.SwitchToAdvancedSliceSettings();

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
				testRunner.SwitchToAdvancedSliceSettings();

				testRunner.ClickByName("Layer Thickness Field");
				testRunner.Type(".5\n");
				testRunner.Delay(.5);
				Assert.AreEqual(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.layer_height), .5, "Layer height is what we set it to");
				testRunner.ClickByName("Quality");
				testRunner.ClickByName("Fine Menu");

				testRunner.Delay(.5);
				Assert.AreEqual(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.layer_height), .1, "Layer height is the fine override");

				testRunner.AddAndSelectPrinter("BCN", "Sigma");

				// Check Guest printer count 
				Assert.AreEqual(2, ProfileManager.Instance.ActiveProfiles.Count(), "ProfileManager has 2 Profiles");

				// Check if Guest printer names exists in dropdown
				testRunner.OpenPrintersDropdown();
				testRunner.ClickByName("Airwolf 3D HD Menu Item");

				testRunner.Delay(1);
				Assert.AreEqual(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.layer_height), .1, "Layer height is the fine override");

				// Switch to Slice Settings Tab
				testRunner.ClickByName("Slice Settings Tab");

				testRunner.ClickByName("Quality");
				testRunner.ClickByName("- none - Menu Item", delayBeforeReturn: .5);
				Assert.AreEqual(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.layer_height), .5, "Layer height is what we set it to");

				return Task.CompletedTask;
			}, maxTimeToRun: 120);
		}
	}
}
