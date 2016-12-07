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
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class SliceSetingsTests
	{
		[Test, Apartment(ApartmentState.STA)]
		public async Task RaftEnabledPassedToSliceEngine()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

				// Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");
				testRunner.Wait(1);

				testRunner.ClickByName("Row Item Calibration - Box");
				testRunner.Wait(.2);

				// Add the Calibration box to the queue and selects it
				testRunner.ClickByName("Row Item Calibration - Box Print Button");
				testRunner.Wait(1);

				testRunner.ClickByName("Layer View Tab");
				testRunner.Wait(.2);

				testRunner.ClickByName("Bread Crumb Button Home", 1);
				testRunner.Wait(.2);

				MatterControlUtilities.SwitchToAdvancedSettings(testRunner);
				testRunner.Wait(.2);

				testRunner.ClickByName("Raft / Priming Tab", 1);
				testRunner.Wait(.2);

				testRunner.ClickByName("Create Raft Checkbox", 1);
				testRunner.Wait(1.5);

				testRunner.ClickByName("Generate Gcode Button", 1);
				testRunner.Wait(2);

				testRunner.WaitUntil(() => MatterControlUtilities.CompareExpectedSliceSettingValueWithActualVaue("enableRaft", "True"), 10);

				//Call compare slice settings method here
				Assert.IsTrue(MatterControlUtilities.CompareExpectedSliceSettingValueWithActualVaue("enableRaft", "True"));

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideWidth: 1224, overrideHeight: 800);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task PauseOnLayerDoesPauseOnPrint()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				using (var emulatorProcess = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					Assert.IsTrue(ProfileManager.Instance.ActiveProfile != null);

					MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

					testRunner.ClickByName("General Tab", 1);
					testRunner.ClickByName("Single Print Tab", 1);
					testRunner.ClickByName("Layer(s) To Pause: Edit");
					testRunner.Type("4;2;a;not;6");

					testRunner.ClickByName("Layer View Tab");

					testRunner.ClickByName("Generate Gcode Button", 1);
					testRunner.ClickByName("Display Checkbox", 10);
					testRunner.ClickByName("Sync To Print Checkbox", 1);

					testRunner.ClickByName("Start Print Button", 1);

					WaitForLayerAndResume(testRunner, 2);
					WaitForLayerAndResume(testRunner, 4);
					WaitForLayerAndResume(testRunner, 6);

					testRunner.WaitForName("Done Button", 30);
					testRunner.WaitForName("Print Again Button", 1);

					return Task.FromResult(0);
				}
			};

			await MatterControlUtilities.RunTest(testToRun, maxTimeToRun: 90);
		}

		private static void WaitForLayerAndResume(AutomationRunner testRunner, int indexToWaitFor)
		{
			testRunner.WaitForName("Resume Button", 30);

			SystemWindow containingWindow;
			GuiWidget layerNumber = testRunner.GetWidgetByName("Current GCode Layer Edit", out containingWindow, 20);

			layerNumber.Invalidate();
			testRunner.WaitUntil(() => layerNumber.Text == indexToWaitFor.ToString(), 2);

			Assert.IsTrue(layerNumber.Text == indexToWaitFor.ToString());
			testRunner.ClickByName("Resume Button", 1);
			testRunner.Wait(.1);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task ClearingCheckBoxClearsUserOverride()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

				//Navigate to Local Library 
				MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

				testRunner.ClickByName("Printer Tab", 1);
				testRunner.ClickByName("Features Tab", 1);

				CheckAndUncheckSetting(testRunner, SettingsKey.heat_extruder_before_homing, "Heat Before Homing Checkbox", false);

				CheckAndUncheckSetting(testRunner, SettingsKey.has_fan, "Has Fan Checkbox", true);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideWidth: 1224, overrideHeight: 900);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task DeleteProfileWorksForGuest()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				// assert no profiles
				Assert.IsTrue(ProfileManager.Instance.ActiveProfiles.Count() == 0);

				MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

				// assert one profile
				Assert.IsTrue(ProfileManager.Instance.ActiveProfiles.Count() == 1);

				MatterControlUtilities.DeleteSelectedPrinter(testRunner);

				// assert no profiles
				Assert.IsTrue(ProfileManager.Instance.ActiveProfiles.Count() == 0);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideWidth: 1224, overrideHeight: 900);
		}

		private static void CheckAndUncheckSetting(AutomationRunner testRunner, string settingToChange, string checkBoxName, bool expected)
		{
			// Assert that the checkbox is currently unchecked, and there is no user override
			Assert.IsTrue(ActiveSliceSettings.Instance.GetValue<bool>(settingToChange) == expected);
			Assert.IsTrue(ActiveSliceSettings.Instance.UserLayer.ContainsKey(settingToChange) == false);

			// Click the checkbox
			testRunner.ClickByName(checkBoxName, 1);
			testRunner.Wait(2);

			// Assert the checkbox is checked and the user override is set
			Assert.IsTrue(ActiveSliceSettings.Instance.GetValue<bool>(settingToChange) != expected);
			Assert.IsTrue(ActiveSliceSettings.Instance.UserLayer.ContainsKey(settingToChange) == true);

			// Click the cancel user override button
			testRunner.ClickByName("Restore " + settingToChange, 1);
			testRunner.Wait(2);

			// Assert the checkbox is unchecked and there is no user override
			Assert.IsTrue(ActiveSliceSettings.Instance.GetValue<bool>(settingToChange) == expected);
			Assert.IsTrue(ActiveSliceSettings.Instance.UserLayer.ContainsKey(settingToChange) == false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task HasHeatedBedCheckedHidesBedTemperatureOptions()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

				//Navigate to Settings Tab and make sure Bed Temp Text box is visible 
				MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

				testRunner.ClickByName("Filament Tab", 1);
				testRunner.ClickByName("Temperatures Tab", 1);
				Assert.IsTrue(testRunner.WaitForName("Extruder Temperature Textbox", 2)); 
				Assert.IsTrue(testRunner.WaitForName("Bed Temperature Textbox", 2));

				//Uncheck Has Heated Bed checkbox and make sure Bed Temp Textbox is not visible
				testRunner.ClickByName("Printer Tab", 1);
				testRunner.ClickByName("Features Tab", 1);
				testRunner.DragByName("Show Reset Connection Checkbox", 1, offset: new Agg.Point2D(-40, 0));
				testRunner.MoveToByName("Show Reset Connection Checkbox", 1, offset: new Agg.Point2D(0, 120));
				testRunner.Drop();
				testRunner.ClickByName("Has Heated Bed Checkbox", 1);
				testRunner.Wait(.5);
				testRunner.ClickByName("Filament Tab", 1);
				bool bedTemperatureTextBoxVisible = testRunner.WaitForName("Bed Temperature Textbox", 2);
				Assert.IsTrue(bedTemperatureTextBoxVisible == false);

				//Make sure Bed Temperature Options are not visible in printer controls
				testRunner.ClickByName("Controls Tab");
				bool bedTemperatureControlsWidget = testRunner.WaitForName("Bed Temperature Controls Widget", 2);
				Assert.IsTrue(bedTemperatureTextBoxVisible == false);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideWidth: 550);
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class QualitySettingsStayAsOverrides
	{
		[Test, Apartment(ApartmentState.STA)]
		public async Task SettingsStayAsOverrides()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				// Add Guest printers
				MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");
				MatterControlUtilities.SwitchToAdvancedSettings(testRunner);


				testRunner.ClickByName("Layer Height Textbox", 2);
				testRunner.Type(".5\n");
				testRunner.Wait(.5);
				Assert.AreEqual(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.layer_height), .5, "Layer height is what we set it to");
				testRunner.ClickByName("Quality", 2);
				testRunner.ClickByName("Fine Menu", 2);

				testRunner.Wait(.5);
				Assert.AreEqual(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.layer_height), .1, "Layer height is the fine override");

				MatterControlUtilities.AddAndSelectPrinter(testRunner, "BCN", "Sigma");

				// Check Guest printer count 
				Assert.AreEqual(2, ProfileManager.Instance.ActiveProfiles.Count(), "ProfileManager has 2 Profiles");

				// Check if Guest printer names exists in dropdown
				testRunner.ClickByName("Printers... Menu", 2);
				testRunner.ClickByName("Airwolf 3D HD Menu Item", 5);

				testRunner.Wait(1);
				Assert.AreEqual(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.layer_height), .1, "Layer height is the fine override");

				testRunner.ClickByName("Quality", 2);
				testRunner.ClickByName("- none - Menu Item", 2, delayBeforeReturn: .5);
				Assert.AreEqual(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.layer_height), .5, "Layer height is what we set it to");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, maxTimeToRun: 120);
		}
	}
}
