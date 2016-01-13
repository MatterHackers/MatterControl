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

namespace MatterControl.MatterControl.UI
{
	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class AddMultipleFilesToDownloads
	{
		List<string> addedFiles = new List<string>();

		[Test, RequiresSTA, RunInApplicationDomain]
		public void DownloadsAddButtonAddsMultipleFiles()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{

					string itemName = "Row Item " + "Fennec Fox";
					string itemNameTwo = "Row Item " + "Batman";

					//Navigate to Downloads Library Provider
					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Downloads Row Item Collection");
					testRunner.ClickByName("Library Add Button");
					testRunner.Wait(3);

					//Get parts to add
					string rowItemPath = MatterControlUtilities.PathToQueueItemsFolder("Fennec_Fox.stl");
					string secondRowItemPath = MatterControlUtilities.PathToQueueItemsFolder("Batman.stl");

					//Get files to delete from Downloads once each test has completed and then add them to List
					string firstFileToDelete = Path.Combine(MatterControlUtilities.PathToDownloadsFolder, "Fennec_Fox.stl");
					string secondFileToDelete = Path.Combine(MatterControlUtilities.PathToDownloadsFolder, "Batman.stl");
					addedFiles.Add(firstFileToDelete);
					addedFiles.Add(secondFileToDelete);

					//Format text to add both items to Downloads and then type paths into file dialogues 
					string textForBothRowItems = String.Format("\"{0}\" \"{1}\"", rowItemPath, secondRowItemPath);
					testRunner.Wait(2);
					testRunner.Type(textForBothRowItems);
					testRunner.Wait(1);
					testRunner.Type("{Enter}");

					//Get test results 
					bool rowItemWasAdded = testRunner.WaitForName(itemName, 2);
					resultsHarness.AddTestResult(rowItemWasAdded == true);

					bool secondRowItemWasAdded = testRunner.WaitForName(itemNameTwo, 2);
					resultsHarness.AddTestResult(secondRowItemWasAdded == true);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};
			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);
			MatterControlUtilities.CleanUpDownloadsDirectoryAfterTest(addedFiles);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2); // make sure we ran all our tests
			
		}
	}

	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class AddAMFToDownloads
	{
		List<string> addedFiles = new List<string>();

		[Test, RequiresSTA, RunInApplicationDomain]
		public void DownloadsAddButtonAddsAMFFiles()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{

					string itemName = "Row Item " + "Rook";

					//Navigate to Downloads Library Provider
					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Downloads Row Item Collection");
					testRunner.ClickByName("Library Add Button");
					testRunner.Wait(3);

					//Get parts to add
					string rowItemPath = MatterControlUtilities.PathToQueueItemsFolder("Rook.amf");

					//Get files to delete from Downloads once each test has completed and then add them to List
					string firstFileToDelete = Path.Combine(MatterControlUtilities.PathToDownloadsFolder, "Rook.amf");
					addedFiles.Add(firstFileToDelete);

					//Add AMF part items to Downloads and then type paths into file dialogues 
					testRunner.Wait(2);
					testRunner.Type(rowItemPath);
					testRunner.Wait(1);
					testRunner.Type("{Enter}");

					//Get test results 
					bool rowItemWasAdded = testRunner.WaitForName(itemName, 2);
					resultsHarness.AddTestResult(rowItemWasAdded == true);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};
			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);
			MatterControlUtilities.CleanUpDownloadsDirectoryAfterTest(addedFiles);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 1); // make sure we ran all our tests

		}
	}

	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class AddZipFileToDownloads
	{
		List<string> addedFiles = new List<string>();

		[Test, RequiresSTA, RunInApplicationDomain]
		public void DownloadsAddButtonAddsZipFiles()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{

					string firstItemName = "Row Item " + "Chinese Dragon";
					string secondItemName = "Row Item " + "chichen-itza pyramid";
					string thirdItemName = "Row Item " + "Circle Calibration";

					//Navigate to Downloads Library Provider
					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Downloads Row Item Collection");
					testRunner.ClickByName("Library Add Button");
					testRunner.Wait(3);

					//Get parts to add
					string rowItemPath = MatterControlUtilities.PathToQueueItemsFolder("Test.zip");

					//Get files to delete from Downloads once each test has completed and then add them to List
					string firstFileToDelete = Path.Combine(MatterControlUtilities.PathToDownloadsFolder, "Circle Calibration.stl");
					string secondFileToDelete = Path.Combine(MatterControlUtilities.PathToDownloadsFolder, "Chinese Dragon.stl");
					string thirdFileToDelete = Path.Combine(MatterControlUtilities.PathToDownloadsFolder, "chichen-itza_pyramid.stl");
					addedFiles.Add(firstFileToDelete);
					addedFiles.Add(secondFileToDelete);
					addedFiles.Add(thirdFileToDelete);

					//Add AMF part items to Downloads and then type paths into file dialogues 
					testRunner.Wait(2);
					testRunner.Type(rowItemPath);
					testRunner.Wait(1);
					testRunner.Type("{Enter}");

					//Get test results 
					bool firstRowItemWasAdded = testRunner.WaitForName(firstItemName, 2);
					resultsHarness.AddTestResult(firstRowItemWasAdded == true);

					bool secondRowItemWasAdded = testRunner.WaitForName(secondItemName, 2);
					resultsHarness.AddTestResult(secondRowItemWasAdded == true);

					bool thirdRowItemWasAdded = testRunner.WaitForName(thirdItemName, 2);
					resultsHarness.AddTestResult(thirdRowItemWasAdded == true);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};
			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);
			MatterControlUtilities.CleanUpDownloadsDirectoryAfterTest(addedFiles);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 3); // make sure we ran all our tests

		}
	}

	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain, Ignore("Not Finished")]
	public class ExportItemsFromDownloads
	{

		List<string> addedFiles = new List<string>();

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
					string rowItemPath = MatterControlUtilities.PathToQueueItemsFolder("Batman.stl");

					//Get files to delete from Downloads once each test has completed and then add them to List
					string firstFileToDelete = Path.Combine(MatterControlUtilities.PathToDownloadsFolder, "Batman.stl");
					addedFiles.Add(firstFileToDelete);

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
			MatterControlUtilities.CleanUpDownloadsDirectoryAfterTest(addedFiles);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2); // make sure we ran all our tests

		}
	}
}
