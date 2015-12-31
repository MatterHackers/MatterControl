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
}
