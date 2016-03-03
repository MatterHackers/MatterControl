using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.PolygonMesh;
using MatterHackers.Agg.UI;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.MatterControl.PartPreviewWindow;
using System.IO;
using MatterHackers.MatterControl.CreatorPlugins;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.DataStorage;
using System.Diagnostics;

namespace MatterHackers.MatterControl.UI
{
	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class SlicingTests
	{

		[Test, RequiresSTA, RunInApplicationDomain]
		public void Slicing()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					
					//Navigate to Local Library 
					MatterControlUtilities.SelectAndAddPrinter(testRunner, "Airwolf 3D", "HD", true);

					testRunner.ClickByName("Library Tab");
					
					MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");
					testRunner.Wait(1);
					testRunner.ClickByName("Row Item Calibration - Box");
					testRunner.ClickByName("Row Item Calibration - Box Print Button");
					testRunner.Wait(1);
					testRunner.ClickByName("Layer View Tab");
					testRunner.Wait(1);
					testRunner.ClickByName("SettingsAndControls");
					testRunner.Wait(1);
					testRunner.ClickByName("Settings Tab");
					testRunner.Wait(1.1);
					testRunner.ClickByName("Skirt and Raft Tab");
					testRunner.Wait(1);
					testRunner.ClickByName("Create Raft Checkbox");
					testRunner.Wait(1);
					testRunner.ClickByName("Generate Gcode Button");
					testRunner.Wait(1.5);




					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 3); // make sure we ran all our tests
		}

	}
}
