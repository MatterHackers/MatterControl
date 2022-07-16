using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;
using NUnit.Framework;
using TestInvoker;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), Parallelizable(ParallelScope.Children)]
	public class SliceSetingsTests
	{
		[Test, ChildProcessTest]
		public async Task RaftEnabledPassedToSliceEngine()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForFirstDraw()
					.AddAndSelectPrinter()
					.AddTestAssetsToLibrary(new[] { "Rook.amf" })
					.AddItemToBed("", "Row Item Rook")
					.SwitchToSliceSettings()
					.SelectSliceSettingsField(SettingsKey.create_raft)
					.WaitForReloadAll(() => testRunner.StartSlicing())
					.WaitFor(() => MatterControlUtilities.CompareExpectedSliceSettingValueWithActualVaue("enableRaft", "True"), 10);

				// Call compare slice settings method here
				Assert.IsTrue(MatterControlUtilities.CompareExpectedSliceSettingValueWithActualVaue("enableRaft", "True"));

				return Task.CompletedTask;
			}, overrideWidth: 1224, overrideHeight: 800);
		}

		[Test, ChildProcessTest]
		public async Task RelativeRetractionExecutesCorrectly()
		{
			// NOTE: This test once timed out at 120, but took 38.4s when run on its own.
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button");

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator("Other", "Other"))
				{
					var printer = testRunner.FirstPrinter();

					var farthestE = 0.0;
					printer.Connection.LineReceived += (e, line) =>
					{
						// make sure the extrusion never goes back very far
						Assert.Greater(printer.Connection.CurrentExtruderDestination, farthestE - 10);
						farthestE = Math.Max(farthestE, printer.Connection.CurrentExtruderDestination);
					};

					testRunner.AddItemToBed()
						.StartPrint(printer)
						.WaitFor(() => printer.Connection.Printing, 60) // wait for the print to start
						.WaitFor(() => !printer.Connection.Printing, 60); // wait for the print to finish
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 120);
		}

		[Test, ChildProcessTest, Category("Emulator")]
		public async Task PauseOnLayerTest()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button");

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					var printer = testRunner.FirstPrinter();

					testRunner.AddItemToBed()
						.StartPrint(printer, pauseAtLayers: "4;2;a;not;6")
						.WaitForLayerAndResume(printer, 2)
						.WaitForLayerAndResume(printer, 4)
						.WaitForLayerAndResume(printer, 6)
						.WaitForPrintFinished(printer);
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 120);
		}

		[Test, ChildProcessTest, Category("Emulator")]
		public async Task OemSettingsChangeOfferedToUserTest()
		{
			await MatterControlUtilities.RunTest(async (testRunner) =>
			{
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					var printer = testRunner.FirstPrinter();

					var expectedWarningName = ValidationErrors.SettingsUpdateAvailable + " Row";

					// open the print menu and prove no oem message
					testRunner.OpenPrintPopupMenu();
					Assert.IsFalse(testRunner.NameExists(expectedWarningName, 1));
					// close the menu
					testRunner.ClickByName("PartPreviewContent")
						.WaitFor(() => !testRunner.NamedWidgetExists("Start Print Button"))
						// test again in case we have not downloaded the profile the first time
						.OpenPrintPopupMenu();
					Assert.IsFalse(testRunner.NameExists(expectedWarningName, 1));
					// close the menu
					testRunner.ClickByName("PartPreviewContent")
						.WaitFor(() => !testRunner.NamedWidgetExists("Start Print Button"));

					Assert.AreEqual(0, (await ProfileManager.GetChangedOemSettings(printer)).Count());

					// change some oem settings
					printer.Settings.SetValue(SettingsKey.layer_height, ".213", printer.Settings.OemLayer);
					printer.Settings.SetValue(SettingsKey.first_layer_height, ".213", printer.Settings.OemLayer);

					Assert.AreEqual(2, (await ProfileManager.GetChangedOemSettings(printer)).Count());

					// open menu again and check that warning is now visible
					testRunner.OpenPrintPopupMenu()
						.ClickByName(ValidationErrors.SettingsUpdateAvailable + " Button")
						.ClickByName(SettingsKey.layer_height + " Update");

					Assert.AreEqual(1, (await ProfileManager.GetChangedOemSettings(printer)).Count());

					testRunner.ClickByName("Cancel Wizard Button");

					testRunner.OpenPrintPopupMenu();
					Assert.IsTrue(testRunner.NameExists(expectedWarningName, 1));

					// close the menu
					testRunner.ClickByName("PartPreviewContent")
						.WaitFor(() => !testRunner.NamedWidgetExists("Start Print Button"))
						// open the menu button
						.ClickByName("Printer Overflow Menu")
						.ClickByName("Update Settings... Menu Item")
						.ClickByName(SettingsKey.first_layer_height + " Update");

					// accept the last option
					Assert.AreEqual(0, (await ProfileManager.GetChangedOemSettings(printer)).Count());
				}
			}, maxTimeToRun: 120);
		}

		[Test, ChildProcessTest, Category("Emulator")]
		public async Task MenuStaysOpenOnRebuildSettings()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					var printer = testRunner.FirstPrinter();

					// open the print menu and prove no oem message
					testRunner.OpenPrintPopupMenu();
					var supportWidegtName = SettingsKey.create_per_layer_support.SettingWidgetName();
					Assert.IsTrue(testRunner.NameExists(supportWidegtName, 1), "support option is visible");
					// toggle supports
					var supportButton = testRunner.GetWidgetByName(supportWidegtName, out _) as ICheckbox;
					for (int i = 0; i < 3; i++)
					{
						testRunner.ClickByName(supportWidegtName)
							.WaitFor(() => supportButton.Checked)
							.ClickByName(supportWidegtName)
							.WaitFor(() => !supportButton.Checked);
					}
					Assert.IsTrue(testRunner.NameExists(supportWidegtName, 1), "Print menu should still be open after toggle supports");
				}
				return Task.CompletedTask;
			}, maxTimeToRun: 120);
		}

		[Test, ChildProcessTest, Category("Emulator")]
		public async Task SettingsStayOpenOnRebuildSettings()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator(pinSettingsOpen: false))
				{
					var printer = testRunner.FirstPrinter();

					testRunner.OpenSettingsSidebar(false);
					for (int i = 0; i < 3; i++)
					{
						testRunner.Delay()
							.ClickByName("Slice Settings Overflow Menu")
							.Delay()
							.ClickByName("Advanced Menu Item")
							.Delay()
							.ClickByName("Slice Settings Overflow Menu")
							.Delay()
							.ClickByName("Simple Menu Item");
					}
				}
		
				return Task.CompletedTask;
			}, maxTimeToRun: 120);
		}

		[Test, ChildProcessTest, Category("Emulator")]
		public async Task CancelWorksAsExpected()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "One printer should exist after add");

					var printer = testRunner.FirstPrinter();
					printer.Settings.SetValue(SettingsKey.cancel_gcode, "G28 ; Cancel GCode");

					testRunner.AddItemToBed()
						.StartPrint(printer, pauseAtLayers: "2")
						// Wait for the Ok button
						.WaitForName("Yes Button", 30);
					emulator.RunSlow = true;
					testRunner.ClickByName("Yes Button")
						// Cancel the Printing task
						.ClickByName("Stop Task Button")
						// Wait for and assert that printing has been canceled
						.WaitFor(() => printer.Connection.CommunicationState == PrinterCommunication.CommunicationStates.Connected);
					Assert.AreEqual(printer.Connection.CommunicationState, PrinterCommunication.CommunicationStates.Connected);

					// Assert that two G28s were output to the terminal
					int g28Count = printer.Connection.TerminalLog.AllLines().Where(line => line.Contains("G28")).Count();
					Assert.AreEqual(2, g28Count, "The terminal log should contain one G28 from Start-GCode and one G28 from Cancel-GCode");
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 120);
		}

		[Test /* Test will fail if screen size is and "HeatBeforeHoming" falls below the fold */, ChildProcessTest]
		public async Task ClearingCheckBoxClearsUserOverride()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForFirstDraw()
					.AddAndSelectPrinter("Airwolf 3D", "HD")
					// Navigate to Local Library
					.SwitchToPrinterSettings()
					.ClickByName("Features SliceSettingsTab");

				var printer = testRunner.FirstPrinter();

				CheckAndUncheckSetting(testRunner, printer, SettingsKey.heat_extruder_before_homing, false);

				CheckAndUncheckSetting(testRunner, printer, SettingsKey.has_fan, true);

				return Task.CompletedTask;
			}, overrideWidth: 1224, overrideHeight: 900, maxTimeToRun: 600);
		}

		[Test, ChildProcessTest]
		public async Task DualExtrusionShowsCorrectHotendData()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					testRunner.ClickByName("Features SliceSettingsTab");

					// only 1 hotend and 1 extruder
					Assert.IsTrue(testRunner.NameExists("Hotend 0"));
					Assert.IsTrue(testRunner.NameExists("Bed TemperatureWidget"));
					Assert.IsFalse(testRunner.NameExists("Hotend 1", .1));

					testRunner.ClickByName("Hotend 0");

					// assert the temp is set when we first open (it comes from the material)
					ThemedNumberEdit tempWidget = testRunner.GetWidgetByName("Temperature Input", out _) as ThemedNumberEdit;
					Assert.AreEqual(240, (int)tempWidget.Value);

					// change material
					var dropDownLists = testRunner.GetWidgetsByName("Hotend Preset Selector");
					Assert.AreEqual(1, dropDownLists.Count, "There is one. The slice settings and the pop out.");
					DropDownList materialSelector = dropDownLists[0].Widget as DropDownList;
					Assert.AreEqual("", materialSelector.SelectedValue);

					testRunner.ClickByName("Hotend Preset Selector")
						.ClickByName("HIPS Menu");

					// check the extruder count
					var extrudeButtons = testRunner.GetWidgetsByName("Extrude Button");
					Assert.AreEqual(1, extrudeButtons.Count, "There should be just one.");

					int hipsGoalTemp = 220;
					testRunner.Delay();

					// assert the temp changed to a new temp
					Assert.AreEqual(hipsGoalTemp,(int) tempWidget.Value, "The goal temp should match the material temp");
					// and the printer heat is off
					Assert.AreEqual(0, (int) emulator.CurrentExtruder.TargetTemperature, "The printer should report the heaters are off");

					// turn on the heater
					testRunner.ClickByName("Toggle Heater")
						.Delay(1);

					// assert the printer is heating
					Assert.AreEqual(hipsGoalTemp, (int)emulator.CurrentExtruder.TargetTemperature, "The printer should report the expected goal temp");

					// turn off the heater
					testRunner.ClickByName("Toggle Heater")
						.Delay(1);

					// assert the printer is off
					Assert.AreEqual(0, (int)emulator.CurrentExtruder.TargetTemperature, "The printer should report the heaters are off");

					// type in a temp when the heating is off
					testRunner.ClickByName("Temperature Input")
						.Type("110")
						.Type("{Enter}")
						.Delay();

					// assert the printer is off
					Assert.AreEqual(0, (int)emulator.CurrentExtruder.TargetTemperature);

					// and the heat toggle is showing on
					ICheckbox heatToggle = testRunner.GetWidgetByName("Toggle Heater", out _) as ICheckbox;
					Assert.IsFalse(heatToggle.Checked);

					// turn it on
					testRunner.ClickByName("Toggle Heater");
					Assert.AreEqual(110, (int)emulator.CurrentExtruder.TargetTemperature);

					// adjust when on
					testRunner.ClickByName("Temperature Input")
						.Type("104")
						.Type("{Enter}")
						.Delay();
					Assert.AreEqual(104, (int)emulator.CurrentExtruder.TargetTemperature);

					// type in 0 and have the heater turn off
					testRunner.ClickByName("Temperature Input")
						.Type("^a")
						.Type("0")
						.Type("{Enter}")
						.Delay()
						// type in 60 and have the heater turn on
						.ClickByName("Temperature Input")
						.Type("^a")
						.Type("60")
						.Type("{Enter}")
						.Delay()
						.ClickByName("Toggle Heater");
					Assert.AreEqual(60, (int)emulator.CurrentExtruder.TargetTemperature);

					// click the remove override and have it change to default temp
					// NOTE: Got test failure twice: The printer should report the expected goal temp
					//                               Expected: 220
					//                               But was:  60
					//       Even though WaitFor was used. Maybe the emulator is just delayed sometimes.
					//       Adding Math.Round anyway. And more waiting.
					testRunner.ClickByName("Restore temperature")
						.WaitFor(() => hipsGoalTemp == (int)Math.Round(emulator.CurrentExtruder.TargetTemperature), maxSeconds: 10);
					Assert.AreEqual(hipsGoalTemp, (int)Math.Round(emulator.CurrentExtruder.TargetTemperature), "The printer should report the expected goal temp");

					// type in 60 and have the heater turn on
					testRunner.ClickByName("Temperature Input")
						.Type("^a")
						.Type("60")
						.Type("{Enter}")
						.Delay();
					Assert.AreEqual(60, (int)emulator.CurrentExtruder.TargetTemperature);

					// type in 0 and have the heater turn off
					testRunner.ClickByName("Temperature Input")
						.Type("^a")
						.Type("0")
						.Type("{Enter}")
						.Delay();

					// assert the printer is not heating
					Assert.AreEqual(0, (int)emulator.CurrentExtruder.TargetTemperature);
					// and the on toggle is showing off
					Assert.IsFalse(heatToggle.Checked);

					// test that the load filament button works and closes correctly
					testRunner.ClickByName("Temperature Input")
						.Type("^a")
						.Type("104")
						.Type("{Enter}")
						.Delay()
						.ClickByName("Load Filament Button")
						.ClickByName("Load Filament");
					Assert.AreEqual(104, (int)emulator.CurrentExtruder.TargetTemperature);
					testRunner.Delay()
						.ClickByName("Cancel Wizard Button")
						.Delay();
					Assert.AreEqual(0, (int)emulator.CurrentExtruder.TargetTemperature);

					testRunner.ClickByName("Hotend 0")
						.ClickByName("Load Filament Button")
						.ClickByName("Load Filament")
						.Delay();
					Assert.AreEqual(104, (int)emulator.CurrentExtruder.TargetTemperature);
					var systemWindow = testRunner.GetWidgetByName("Cancel Wizard Button", out SystemWindow containingWindow);
					// close the window through windows (alt-f4)
					testRunner.Type("%{F4}");
					Assert.AreEqual(0, (int)emulator.CurrentExtruder.TargetTemperature);

					// Switch back to the general tab
					testRunner.ClickByName("General SliceSettingsTab")
						.SelectSliceSettingsField(SettingsKey.extruder_count)
						.Type("2")
						.Type("{Enter}");

					// there are now 2 hotends and 2 extruders
					Assert.IsTrue(testRunner.NameExists("Hotend 0"));
					Assert.IsTrue(testRunner.NameExists("Hotend 1"));

					var printer = testRunner.FirstPrinter();

					SetCheckBoxSetting(testRunner, printer, SettingsKey.extruders_share_temperature, true);

					// there is one hotend and 2 extruders
					Assert.IsTrue(testRunner.NameExists("Hotend 0"));
					Assert.IsFalse(testRunner.NameExists("Hotend 1", .1));

					testRunner.ClickByName("Hotend 0");

					extrudeButtons = testRunner.GetWidgetsByName("Extrude Button");
					Assert.AreEqual(2, extrudeButtons.Count, "Now there should be two.");
				}

				return Task.CompletedTask;
			}, maxTimeToRun: 120);
		}

		[Test, ChildProcessTest]
		public void SliceSettingsOrganizerSupportsKeyLookup()
		{
			StaticData.RootPath = MatterControlUtilities.StaticDataPath;

			var organizer = PrinterSettings.Layout;

			var userLevel = organizer.AllSliceSettings;
			Assert.IsNotNull(userLevel);

			// Confirm expected keys
			Assert.IsTrue(userLevel.ContainsKey("bed_temperature"));
			Assert.IsTrue(organizer.AllSliceSettings.ContainsKey("bed_temperature"));
			Assert.IsTrue(organizer.AllPrinterSettings.ContainsKey("extruder_count"));

			// Confirm non-existent key
			Assert.IsFalse(userLevel.ContainsKey("non_existing_setting"));
			Assert.IsFalse(organizer.AllSliceSettings.ContainsKey("non_existing_setting"));
		}

		[Test, ChildProcessTest]
		public async Task SwitchingMaterialsCausesSettingsChangedEvents()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();

				var printer = testRunner.FirstPrinter();

				printer.Settings.OemLayer[SettingsKey.layer_height] = ".2";

				int layerHeightChangedCount = 0;

				PrinterSettings.AnyPrinterSettingChanged += (s, stringEvent) =>
				{
					if (stringEvent != null)
					{
						if (stringEvent.Data == SettingsKey.layer_height)
						{
							layerHeightChangedCount++;
						}
					}
				};

				// Navigate to Local Library
				testRunner.SwitchToSliceSettings();

				// Navigate to General Tab -> Layers / Surface Tab
				testRunner.SelectSliceSettingsField(SettingsKey.layer_height);
				Assert.AreEqual(0, layerHeightChangedCount, "No change to layer height yet.");

				var theme = ApplicationController.Instance.Theme;
				var indicator = testRunner.GetWidgetByName("Layer Thickness OverrideIndicator", out _);

				Assert.AreEqual(Color.Transparent, indicator.BackgroundColor);

				testRunner.ClickByName("Quality")
					.ClickByName("Fine Menu")
					.Delay(.5);
				Assert.AreEqual(1, layerHeightChangedCount, "Changed to fine.");
				Assert.AreEqual(theme.PresetColors.QualityPreset, indicator.BackgroundColor);

				testRunner.ClickByName("Quality")
					.ClickByName("Standard Menu")
					.Delay(.5);
				Assert.AreEqual(2, layerHeightChangedCount, "Changed to standard.");
				Assert.AreEqual(theme.PresetColors.QualityPreset, indicator.BackgroundColor);

				testRunner.ClickByName("Quality")
					.ClickByName("- none - Menu Item")
					.Delay(.5);
				Assert.AreEqual(Color.Transparent, indicator.BackgroundColor);
				Assert.AreEqual(3, layerHeightChangedCount, "Changed to - none -.");

				testRunner.ClickByName("Quality")
					.ClickByName("Standard Menu")
					.Delay(.5);
				Assert.AreEqual(4, layerHeightChangedCount, "Changed to standard.");
				Assert.AreEqual(theme.PresetColors.QualityPreset, indicator.BackgroundColor);

				// TODO: delete one of the settings
				// asserts that the deleted setting has been removed from the list

				return Task.CompletedTask;
			}, maxTimeToRun: 1000);
		}

		[Test, ChildProcessTest]
		public async Task DeleteProfileWorksForGuest()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForFirstDraw();

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

		private static void SetCheckBoxSetting(AutomationRunner testRunner, PrinterConfig printer, string settingToChange, bool valueToSet)
		{
			var settingsData = PrinterSettings.SettingsData[settingToChange];
			string checkBoxName = $"{settingsData.PresentationName} Field";

			Assert.IsTrue(printer.Settings.GetValue<bool>(settingToChange) != valueToSet);

			//testRunner.ScrollIntoView(checkBoxName);
			//testRunner.ClickByName(checkBoxName);
			testRunner.SelectSliceSettingsField(settingToChange)
				// give some time for the ui to update if necessary
				.Delay(2);

			Assert.IsTrue(printer.Settings.GetValue<bool>(settingToChange) == valueToSet);
		}

		private static void CheckAndUncheckSetting(AutomationRunner testRunner, PrinterConfig printer, string settingToChange, bool expected)
		{
			// Assert that the checkbox is currently unchecked, and there is no user override
			Assert.IsFalse(printer.Settings.UserLayer.ContainsKey(settingToChange));

			// Click the checkbox
			SetCheckBoxSetting(testRunner, printer, settingToChange, !expected);

			// Assert the checkbox is checked and the user override is set
			Assert.IsTrue(printer.Settings.UserLayer.ContainsKey(settingToChange));

			// make sure the setting is still open in case of a reload all
			testRunner.NavigateToSliceSettingsField(settingToChange)
				// Click the cancel user override button
				.ClickByName("Restore " + settingToChange)
				.Delay(2);

			// Assert the checkbox is unchecked and there is no user override
			Assert.IsTrue(printer.Settings.GetValue<bool>(settingToChange) == expected);
			Assert.IsFalse(printer.Settings.UserLayer.ContainsKey(settingToChange));
		}

		[Test, ChildProcessTest]
		public async Task HasHeatedBedCheckedHidesBedTemperatureOptions()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForFirstDraw()
					.AddAndSelectPrinter("Airwolf 3D", "HD")
					// Navigate to Settings Tab and make sure Bed Temp Text box is visible
					.SwitchToSliceSettings()
					.ClickByName("Slice Settings Overflow Menu")
					.ClickByName("Advanced Menu Item")
					.Delay()
					.SelectSliceSettingsField(SettingsKey.bed_temperature)
					.SelectSliceSettingsField(SettingsKey.temperature)
					// Uncheck Has Heated Bed checkbox and make sure Bed Temp Textbox is not visible
					.SwitchToPrinterSettings()
					//.SelectSliceSettingsField(SettingsKey.has_heated_bed) // NOTE: Happened once: System.Exception : ClickByName Failed: Named GuiWidget not found [Hardware SliceSettingsTab]
					//.Delay(1.0) // Wait for reload reliably:
					.WaitForReloadAll(() => testRunner.SelectSliceSettingsField(SettingsKey.has_heated_bed))
					.SwitchToSliceSettings()
					.NavigateToSliceSettingsField(SettingsKey.temperature);

				Assert.IsFalse(testRunner.WaitForName("Bed Temperature Textbox", .5), "Filament -> Bed Temp should not be visible after Heated Bed unchecked");

				// Make sure Bed Temperature Options are not visible in printer controls
				testRunner.SwitchToControlsTab();

				Assert.IsFalse(testRunner.WaitForName("Bed Temperature Controls Widget", .5), "Controls -> Bed Temp should not be visible after Heated Bed unchecked");

				return Task.CompletedTask;
			});
		}

		[Test, ChildProcessTest]
		public async Task QualitySettingsStayAsOverrides()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.WaitForFirstDraw();

				// Add Guest printers
				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD")
					.SwitchToSliceSettings();

				var printer = testRunner.FirstPrinter();

				testRunner.SelectSliceSettingsField(SettingsKey.layer_height)
					.Type(".5")
					// Force lose focus
					.SelectSliceSettingsField(SettingsKey.first_layer_height)
					.WaitFor(() => printer.Settings.GetValue<double>(SettingsKey.layer_height) == 0.5);
				
				Assert.AreEqual(printer.Settings.GetValue<double>(SettingsKey.layer_height).ToString(), "0.5", "Layer height is what we set it to");

				testRunner.ClickByName("Quality")
					.ClickByName("Fine Menu");

				testRunner.WaitFor(() => printer.Settings.GetValue<double>(SettingsKey.layer_height) == 0.1);
				Assert.AreEqual(printer.Settings.GetValue<double>(SettingsKey.layer_height).ToString(), "0.1", "Layer height is the fine override");

				// Close Airwolf
				testRunner.CloseFirstPrinterTab();

				// Assert printer counts
				Assert.AreEqual(1, ProfileManager.Instance.ActiveProfiles.Count(), "ProfileManager should have 1 profile after Airwolf close");
				Assert.AreEqual(0, ApplicationController.Instance.ActivePrinters.Count(), "Zero printers should be active after Airwolf close");

				testRunner.AddAndSelectPrinter("BCN3D", "Sigma");

				// Assert printer counts
				Assert.AreEqual(2, ProfileManager.Instance.ActiveProfiles.Count(), "ProfileManager has 2 profiles");
				Assert.AreEqual(1, ApplicationController.Instance.ActivePrinters.Count(), "One printer should be active after BCN add");

				// Close BCN
				testRunner.CloseFirstPrinterTab()
					// Reopen Airwolf
					.SwitchToHardwareTab()
					.DoubleClickByName("Airwolf 3D HD Node")
					.Delay(0.2);

				printer = testRunner.FirstPrinter();

				testRunner.WaitFor(() => printer.Settings.GetValue<double>(SettingsKey.layer_height) == 0.1);
				Assert.AreEqual(printer.Settings.GetValue<double>(SettingsKey.layer_height).ToString(), "0.1", "Layer height is the fine override");

				// Switch to Slice Settings Tab
				testRunner.ClickByName("Slice Settings Tab")
					.ClickByName("Quality")
					.ClickByName("- none - Menu Item")
					.WaitFor(() => printer.Settings.GetValue<double>(SettingsKey.layer_height) == 0.5);

				Assert.AreEqual(printer.Settings.GetValue<double>(SettingsKey.layer_height).ToString(), "0.5", "Layer height is what we set it to");

				return Task.CompletedTask;
			}, maxTimeToRun: 120);
		}

		[Test, ChildProcessTest]
		public void CopyFromTest()
		{
			StaticData.RootPath = MatterControlUtilities.StaticDataPath;
			MatterControlUtilities.OverrideAppDataLocation(MatterControlUtilities.RootPath);

			var settings = new PrinterSettings();
			settings.ID = "12345-print";
			settings.SetValue(SettingsKey.auto_connect, "1");
			settings.SetValue(SettingsKey.com_port, "some_com_port");
			settings.SetValue(SettingsKey.cancel_gcode, "hello world");

			settings.Macros.Add(new GCodeMacro() { Name = "Macro1", GCode = "G28 ; Home Printer" });

			settings.StagedUserSettings["retract_restart_extra_time_to_apply"] = "4";
			settings.StagedUserSettings["retract_restart_extra"] = "0.3";
			settings.StagedUserSettings["bridge_fan_speed"] = "50";

			var sha1 = settings.ComputeSHA1();

			var clone = new PrinterSettings();
			clone.CopyFrom(settings);

			Assert.AreEqual(settings.ToJson(), clone.ToJson(), "Cloned settings via CopyFrom should equal source");
			Assert.AreEqual(sha1, clone.ComputeSHA1(), "Cloned settings via CopyFrom should equal source");
		}

		private void CloseAllPrinterTabs(AutomationRunner testRunner)
		{
			// Close all printer tabs
			var mainViewWidget = testRunner.GetWidgetByName("PartPreviewContent", out _) as MainViewWidget;
			foreach (var tab in mainViewWidget.TabControl.AllTabs.Where(t => t.TabContent is PrinterTabPage).ToList())
			{
				if (tab is GuiWidget widget)
				{
					var closeWidget = widget.Descendants<ImageWidget>().First();
					closeWidget.InvokeClick();
				}
			}
		}
	}
}
