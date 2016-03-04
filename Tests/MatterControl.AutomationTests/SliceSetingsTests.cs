using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.Agg.PlatformAbstract;
using System.IO;
using MatterHackers.MatterControl.CreatorPlugins;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.DataStorage;
using System.Diagnostics;

namespace MatterHackers.MatterControl.UI
{
	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class SliceSetingsTests
	{
		[Test, RequiresSTA, RunInApplicationDomain]
		public void RaftEnabledPassedToSliceEngine()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{


					MatterControlUtilities.SelectAndAddPrinter(testRunner, "Airwolf 3D", "HD", true);

					//Navigate to Local Library 
					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");
					testRunner.Wait(1);
					testRunner.ClickByName("Row Item Calibration - Box");
					testRunner.ClickByName("Row Item Calibration - Box Print Button");
					testRunner.Wait(1);

					testRunner.ClickByName("Layer View Tab");

					testRunner.ClickByName("Bread Crumb Button Home");
					testRunner.Wait(1);
					testRunner.ClickByName("SettingsAndControls");
					testRunner.Wait(1);
					testRunner.ClickByName("User Level Dropdown");
					testRunner.Wait(1);
					testRunner.ClickByName("Advanced Menu Item");
					testRunner.Wait(1);
					testRunner.ClickByName("Skirt and Raft Tab");
					testRunner.Wait(1);

					testRunner.ClickByName("Create Raft Checkbox");
					testRunner.Wait(1.5);
					testRunner.ClickByName("Save Slice Settings Button");
					testRunner.ClickByName("Generate Gcode Button");
					testRunner.Wait(5);

					//Call compare slice settings methode here
					resultsHarness.AddTestResult(MatterControlUtilities.CompareExpectedSliceSettingValueWithActualVaue("enableRaft", "True"));


					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 1); // make sure we ran all our tests
		}

		//Stress Test check & uncheck 1000x
		[Test, RequiresSTA, RunInApplicationDomain, Ignore("Not Finished")]
		public void HasHeatedBedCheckUncheck()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{

					MatterControlUtilities.SelectAndAddPrinter(testRunner, "Airwolf 3D", "HD", true);

					//Navigate to Local Library 
					testRunner.ClickByName("SettingsAndControls");
					testRunner.Wait(1);
					testRunner.ClickByName("User Level Dropdown");
					testRunner.Wait(1);
					testRunner.ClickByName("Advanced Menu Item");
					testRunner.Wait(1);
					testRunner.ClickByName("Printer Tab");
					testRunner.Wait(1);

					testRunner.ClickByName("Features Tab");
					testRunner.Wait(2);

					for (int i = 0; i <= 1000; i++)
					{
						testRunner.ClickByName("Has Heated Bed Checkbox");
						testRunner.Wait(.5);
					}

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 0); // make sure we ran all our tests
		}

		[Test, RequiresSTA, RunInApplicationDomain]
		public void HasHeatedBedCheckedHidesBedTemperatureOptions()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{

					MatterControlUtilities.SelectAndAddPrinter(testRunner, "Airwolf 3D", "HD", true);

					//Navigate to Settings Tab and make sure Bed Temp Text box is visible 
					testRunner.ClickByName("SettingsAndControls");
					testRunner.Wait(1);
					testRunner.ClickByName("User Level Dropdown");
					testRunner.Wait(1);
					testRunner.ClickByName("Advanced Menu Item");
					testRunner.Wait(1);
					testRunner.ClickByName("Filament Tab");
					testRunner.Wait(1);
					resultsHarness.AddTestResult(testRunner.WaitForName("Bed Temperature Textbox", 2));

					//Uncheck Has Heated Bed checkbox and make sure Bed Temp Textbox is not visible
					testRunner.ClickByName("Printer Tab");
					testRunner.Wait(1);
					testRunner.ClickByName("Features Tab");
					testRunner.Wait(1);
					testRunner.ClickByName("Has Heated Bed Checkbox");
					testRunner.Wait(.5);
					testRunner.ClickByName("Filament Tab");
					testRunner.Wait(1);
					bool bedTemperatureTextBoxVisible = testRunner.WaitForName("Bed Temperature Textbox", 2);
					resultsHarness.AddTestResult(bedTemperatureTextBoxVisible == false);

					//Make sure Bed Temperature Options are not visible in printer controls
					testRunner.ClickByName("Controls Tab");
					bool bedTemperatureControlsWidget = testRunner.WaitForName("Bed Temperature Controls Widget", 2);
					resultsHarness.AddTestResult(bedTemperatureTextBoxVisible == false);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 3); // make sure we ran all our tests
		}
	}
}
