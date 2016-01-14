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
		public void DownloadsExportButtonExportsGcode()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{

					MatterControlUtilities.SelectAndAddPrinter(testRunner, "Airwolf 3D", "HD");

					string firstItemName = "Row Item " + "Batman";

					//Navigate to Downloads Library Provider
					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Downloads Row Item Collection");
					testRunner.ClickByName("Library Add Button");
					testRunner.Wait(2);

					//Get parts to add
					string rowItemPath = MatterControlUtilities.GetTestItemPath("Batman.stl");

					//Get files to delete from Downloads once each test has completed and then add them to List
					string firstFileToDelete = Path.Combine(MatterControlUtilities.PathToDownloadsSubFolder, "Batman.stl");


					//Add STL part items to Downloads and then type paths into file dialogue
					testRunner.Wait(2);
					testRunner.Type(rowItemPath);
					testRunner.Wait(1);
					testRunner.Type("{Enter}");

					//Get test results 
					bool firstRowItemWasAdded = testRunner.WaitForName(firstItemName, 2);
					resultsHarness.AddTestResult(firstRowItemWasAdded == true);

					testRunner.ClickByName("Library Edit Button");
					testRunner.ClickByName(firstItemName);
					testRunner.ClickByName("Library Export Button");
					testRunner.Wait(2);

					//testRunner.WaitForName("Export Item Window", 2);
					testRunner.ClickByName("Export as GCode Button");
					testRunner.Wait(2);

					string gcodeExportPath = MatterControlUtilities.PathToExportGcodeFolder;
					testRunner.Type(gcodeExportPath);
					testRunner.Type("{Enter}");
					testRunner.Wait(2);

					bool gcodeExported = false;

					if (File.Exists(gcodeExportPath))
					{
						gcodeExported = true;
					}

					resultsHarness.AddTestResult(gcodeExported == true);


					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);
			//MatterControlUtilities.CleanUpDownloadsDirectoryAfterTest(addedFiles);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2); // make sure we ran all our tests

		}
	}
}
