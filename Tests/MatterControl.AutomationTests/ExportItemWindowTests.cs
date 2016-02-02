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
using System.Collections.Generic;
using MatterHackers.MatterControl.UI;
namespace MatterControl.AutomationTests
{
	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain, Ignore("Not Finished")]
	public class ExportItemsFromDownloads
	{

		[Test, RequiresSTA, RunInApplicationDomain]
		public void ExportAsGcode()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{

					MatterControlUtilities.SelectAndAddPrinter(testRunner, "Airwolf 3D", "HD", true);

					string firstItemName = "Row Item Batman";
					//Navigate to Downloads Library Provider
					testRunner.ClickByName("Queue Tab");
					testRunner.ClickByName("Queue Add Button", 2);

					//Get parts to add
					string rowItemPath = MatterControlUtilities.GetTestItemPath("Batman.stl");

					//Add STL part items to Downloads and then type paths into file dialogue
					testRunner.Wait(1);
					testRunner.Type(MatterControlUtilities.GetTestItemPath("Batman.stl"));
					testRunner.Wait(1);
					testRunner.Type("{Enter}");

					//Get test results 
					resultsHarness.AddTestResult(testRunner.WaitForName(firstItemName, 2) == true);

					testRunner.ClickByName("Queue Edit Button");
					testRunner.ClickByName(firstItemName);
					testRunner.ClickByName("Queue Export Button");
					testRunner.Wait(2);

					testRunner.WaitForName("Export Item Window", 2);
					testRunner.ClickByName("Export as GCode Button", 2);
					testRunner.Wait(2);

					string gcodeExportPath = MatterControlUtilities.PathToExportGcodeFolder;
					testRunner.Type(gcodeExportPath);
					testRunner.Type("{Enter}");
					testRunner.Wait(2);

					Console.WriteLine(gcodeExportPath);

					resultsHarness.AddTestResult(File.Exists(gcodeExportPath) == true);
					Debugger.Break();

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2);

		}
	}
}
