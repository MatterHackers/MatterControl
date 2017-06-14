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
				testRunner.ClickByName("Create Raft Checkbox");

				testRunner.ClickByName("Toggle Layer View Button");
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
					testRunner.ClickByName("Layer(s) To Pause: Edit");
					testRunner.Type("4;2;a;not;6");

					testRunner.AddDefaultFileToBedplate();

					testRunner.ClickByName("Toggle Layer View Button");

					testRunner.ClickByName("Generate Gcode Button");
					testRunner.ClickByName("Display Checkbox");
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
					testRunner.ClickByName("Layer(s) To Pause: Edit");
					testRunner.Type("2");

					testRunner.AddDefaultFileToBedplate();

					testRunner.ClickByName("Toggle Layer View Button");

					testRunner.ClickByName("Generate Gcode Button");
					testRunner.ClickByName("Display Checkbox");
					testRunner.ClickByName("Sync To Print Checkbox");

					testRunner.ClickByName("Start Print Button");

					// assert the leveling is working
					testRunner.WaitForName("Yes Button", 200);
					// close the pause dialog pop-up
					testRunner.ClickByName("Yes Button");

					testRunner.WaitForName("Resume Button", 30);
					testRunner.ClickByName("Cancel Print Button");

					testRunner.WaitForName("Start Print Button");
					Assert.IsTrue(testRunner.NameExists("Start Print Button"));

					int g28Count = 0;
					foreach(var line in PrinterOutputCache.Instance.PrinterLines)
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

			SystemWindow containingWindow;
			GuiWidget layerNumber = testRunner.GetWidgetByName("Current GCode Layer Edit", out containingWindow, 20);

			layerNumber.Invalidate();
			testRunner.Delay(() => layerNumber.Text == indexToWaitFor.ToString(), 2);

			Assert.IsTrue(layerNumber.Text == indexToWaitFor.ToString());
			testRunner.ClickByName("Resume Button");
			testRunner.Delay(.1);
		}

		[Test /* Test will fail if screen size is and "HeatBeforeHoming" falls below the fold */]
		public async Task ClearingCheckBoxClearsUserOverride()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				// Navigate to Local Library 
				testRunner.SwitchToAdvancedSliceSettings();

				testRunner.ClickByName("Printer Tab");
				testRunner.ClickByName("Features Tab");

				CheckAndUncheckSetting(testRunner, SettingsKey.heat_extruder_before_homing, "Heat Before Homing Checkbox", false);

				CheckAndUncheckSetting(testRunner, SettingsKey.has_fan, "Has Fan Checkbox", true);

				return Task.CompletedTask;
			}, overrideWidth: 1224, overrideHeight: 900);
		}

		[Test /* Test will fail if screen size is and "HeatBeforeHoming" falls below the fold */]
		public async Task SwitchingMaterialsCausesSettingsChangedEvents()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
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

		private static void CheckAndUncheckSetting(AutomationRunner testRunner, string settingToChange, string checkBoxName, bool expected)
		{
			// Assert that the checkbox is currently unchecked, and there is no user override
			Assert.IsTrue(ActiveSliceSettings.Instance.GetValue<bool>(settingToChange) == expected);
			Assert.IsTrue(ActiveSliceSettings.Instance.UserLayer.ContainsKey(settingToChange) == false);

			// Click the checkbox
			testRunner.ClickByName(checkBoxName);
			testRunner.Delay(2);

			// Assert the checkbox is checked and the user override is set
			Assert.IsTrue(ActiveSliceSettings.Instance.GetValue<bool>(settingToChange) != expected);
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
				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				// Navigate to Settings Tab and make sure Bed Temp Text box is visible 
				testRunner.SwitchToAdvancedSliceSettings();

				testRunner.ClickByName("Filament Tab");
				testRunner.ClickByName("Temperatures Tab");

				Assert.IsTrue(testRunner.WaitForName("Extruder Temperature Textbox")); 
				Assert.IsTrue(testRunner.WaitForName("Bed Temperature Textbox"));

				// Uncheck Has Heated Bed checkbox and make sure Bed Temp Textbox is not visible
				testRunner.ClickByName("Printer Tab");
				testRunner.ClickByName("Features Tab");
				testRunner.DragByName("Show Reset Connection Checkbox", 1, offset: new Agg.Point2D(-40, 0));
				testRunner.MoveToByName("Show Reset Connection Checkbox", 1, offset: new Agg.Point2D(0, 120));
				testRunner.Drop();
				testRunner.ClickByName("Has Heated Bed Checkbox");
				testRunner.Delay(.5);

				testRunner.ClickByName("Filament Tab");
				Assert.IsFalse(testRunner.WaitForName("Bed Temperature Textbox"), "Filament -> Bed Temp should not be visible after Heated Bed unchecked");

				// Make sure Bed Temperature Options are not visible in printer controls
				testRunner.SwitchToControlsTab();

				Assert.IsFalse(testRunner.WaitForName("Bed Temperature Controls Widget"), "Controls -> Bed Temp should not be visible after Heated Bed unchecked");

				return Task.CompletedTask;
			}, overrideWidth: 1300);
		}
	
		[Test]
		public async Task QualitySettingsStayAsOverrides()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				// Add Guest printers
				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");
				testRunner.SwitchToAdvancedSliceSettings();

				testRunner.ClickByName("Layer Height Textbox");
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
