using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void RaftEnabledPassedToSliceEngine()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);

				MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

				//Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");
				testRunner.Wait(1);
				testRunner.ClickByName("Row Item Calibration - Box");
				testRunner.ClickByName("Row Item Calibration - Box Print Button");
				testRunner.Wait(1);

				testRunner.ClickByName("Layer View Tab");

				testRunner.ClickByName("Bread Crumb Button Home", 1);

				MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

				testRunner.ClickByName("Raft / Priming Tab", 1);

				testRunner.ClickByName("Create Raft Checkbox", 1);
				testRunner.Wait(1.5);
				testRunner.ClickByName("Generate Gcode Button", 1);
				testRunner.Wait(5);

				//Call compare slice settings method here
				testRunner.AddTestResult(MatterControlUtilities.CompareExpectedSliceSettingValueWithActualVaue("enableRaft", "True"));

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, overrideWidth: 1224, overrideHeight: 800, defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(1);
		}

		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void PauseOnLayerDoesPauseOnPrint()
		{
			Process emulatorProcess = null;

			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner, MatterControlUtilities.PrepAction.CloseSignInAndPrinterSelect);

				emulatorProcess = MatterControlUtilities.LaunchAndConnectToPrinterEmulator(testRunner);

				testRunner.AddTestResult(ProfileManager.Instance.ActiveProfile != null);

				MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

				testRunner.AddTestResult(testRunner.ClickByName("General Tab", 1));
				testRunner.AddTestResult(testRunner.ClickByName("Single Print Tab", 1));
				testRunner.AddTestResult(testRunner.ClickByName("Layer(s) To Pause:" + " Edit"));
				testRunner.Type("4;2;a;not;6");

				testRunner.AddTestResult(testRunner.ClickByName("Layer View Tab"));

				testRunner.AddTestResult(testRunner.ClickByName("Generate Gcode Button", 1));
				testRunner.AddTestResult(testRunner.ClickByName("Display Checkbox", 10));
				testRunner.AddTestResult(testRunner.ClickByName("Sync To Print Checkbox", 1));

				testRunner.AddTestResult(testRunner.ClickByName("Start Print Button", 1));

				WaitForLayerAndResume(testRunner, 2);
				WaitForLayerAndResume(testRunner, 4);
				WaitForLayerAndResume(testRunner, 6);

				testRunner.AddTestResult(testRunner.WaitForName("Done Button", 30));
				testRunner.AddTestResult(testRunner.WaitForName("Print Again Button", 1));

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, maxTimeToRun: 200);
			Assert.IsTrue(testHarness.AllTestsPassed(17));

			try
			{
				emulatorProcess?.Kill();
			}
			catch { }
		}

		private static void WaitForLayerAndResume(AutomationRunner testRunner, int indexToWaitFor)
		{
			testRunner.WaitForName("Resume Button", 30);

			SystemWindow containingWindow;
			GuiWidget layerNumber = testRunner.GetWidgetByName("Current GCode Layer Edit", out containingWindow, 20);

			layerNumber.Invalidate();
			testRunner.WaitUntil(() => layerNumber.Text == indexToWaitFor.ToString(), 2);

			testRunner.AddTestResult(layerNumber.Text == indexToWaitFor.ToString());
			testRunner.AddTestResult(testRunner.ClickByName("Resume Button", 1));
			testRunner.Wait(.1);
		}

		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void ClearingCheckBoxClearsUserOverride()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);

				MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

				//Navigate to Local Library 
				MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

				testRunner.AddTestResult(testRunner.ClickByName("Printer Tab", 1), "Switch to Printers tab");
				testRunner.AddTestResult(testRunner.ClickByName("Features Tab", 1), "Switch to Features tab");

				CheckAndUncheckSetting(testRunner, SettingsKey.heat_extruder_before_homing, "Heat Before Homing Checkbox", false);

				CheckAndUncheckSetting(testRunner, SettingsKey.has_fan, "Has Fan Checkbox", true);

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner  testHarness = MatterControlUtilities.RunTest(testToRun, overrideWidth: 1224, overrideHeight: 900, defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(18));
		}

		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void DeleteProfileWorksForGuest()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);

				// assert no profiles
				testRunner.AddTestResult(ProfileManager.Instance.ActiveProfiles.Count() == 0);

				MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

				// assert one profile
				testRunner.AddTestResult(ProfileManager.Instance.ActiveProfiles.Count() == 1);

				MatterControlUtilities.DeleteSelectedPrinter(testRunner);

				// assert no profiles
				testRunner.AddTestResult(ProfileManager.Instance.ActiveProfiles.Count() == 0);

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner  testHarness = MatterControlUtilities.RunTest(testToRun, overrideWidth: 1224, overrideHeight: 900, defaultTestImages: MatterControlUtilities.DefaultTestImages);

			Assert.IsTrue(testHarness.AllTestsPassed(3));
		}

		private static void CheckAndUncheckSetting(AutomationRunner testRunner, string settingToChange, string checkBoxName, bool expected)
		{
			// Assert that the checkbox is currently unchecked, and there is no user override
			testRunner.AddTestResult(ActiveSliceSettings.Instance.GetValue<bool>(settingToChange) == expected);
			testRunner.AddTestResult(ActiveSliceSettings.Instance.UserLayer.ContainsKey(settingToChange) == false);

			// Click the checkbox
			testRunner.AddTestResult(testRunner.ClickByName(checkBoxName, 1));
			testRunner.Wait(2);

			// Assert the checkbox is checked and the user override is set
			testRunner.AddTestResult(ActiveSliceSettings.Instance.GetValue<bool>(settingToChange) != expected);
			testRunner.AddTestResult(ActiveSliceSettings.Instance.UserLayer.ContainsKey(settingToChange) == true);

			// Click the cancel user override button
			testRunner.AddTestResult(testRunner.ClickByName("Restore " + settingToChange, 1));
			testRunner.Wait(2);

			// Assert the checkbox is unchecked and there is no user override
			testRunner.AddTestResult(ActiveSliceSettings.Instance.GetValue<bool>(settingToChange) == expected);
			testRunner.AddTestResult(ActiveSliceSettings.Instance.UserLayer.ContainsKey(settingToChange) == false);
		}

		//Stress Test check & uncheck 1000x
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain, Category("FixNeeded" /* Not Finished */)]
		public void HasHeatedBedCheckUncheck()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);

				MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

				//Navigate to Local Library 
				MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

				testRunner.AddTestResult(testRunner.ClickByName("Printer Tab"));
				testRunner.Wait(1);

				testRunner.AddTestResult(testRunner.ClickByName("Features Tab"));
				testRunner.Wait(2);

				for (int i = 0; i <= 1000; i++)
				{
					testRunner.AddTestResult(testRunner.ClickByName("Has Heated Bed Checkbox"));
					testRunner.Wait(.5);
				}

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, defaultTestImages: MatterControlUtilities.DefaultTestImages);

			Assert.IsTrue(testHarness.AllTestsPassed(1008));
		}

		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void HasHeatedBedCheckedHidesBedTemperatureOptions()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);

				MatterControlUtilities.AddAndSelectPrinter(testRunner, "Airwolf 3D", "HD");

				//Navigate to Settings Tab and make sure Bed Temp Text box is visible 
				MatterControlUtilities.SwitchToAdvancedSettings(testRunner);

				testRunner.ClickByName("Filament Tab", 1);
				testRunner.ClickByName("Temperatures Tab", 1);
				testRunner.AddTestResult(testRunner.WaitForName("Bed Temperature Textbox", 2));

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
				testRunner.AddTestResult(bedTemperatureTextBoxVisible == false);

				//Make sure Bed Temperature Options are not visible in printer controls
				testRunner.ClickByName("Controls Tab");
				bool bedTemperatureControlsWidget = testRunner.WaitForName("Bed Temperature Controls Widget", 2);
				testRunner.AddTestResult(bedTemperatureTextBoxVisible == false);

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, overrideWidth: 550, defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(3));
		}
	}
}
