using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;
using System;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class AddMultipleFilesToDownloads
	{
		[Test, RequiresSTA, RunInApplicationDomain]
		public void DownloadsAddButtonAddsMultipleFiles()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{

				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);
					MatterControlUtilities.CreateDownloadsSubFolder();

					//Navigate to Downloads Library Provider
					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Downloads Row Item Collection");
					MatterControlUtilities.NavigateToFolder(testRunner, "Temporary Row Item Collection");
					testRunner.ClickByName("Library Add Button");
					testRunner.Wait(3);

					string firstRowItemPath = MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl");
					string secondRowItemPath = MatterControlUtilities.GetTestItemPath("Batman.stl");
					string textForBothRowItems = String.Format("\"{0}\" \"{1}\"", firstRowItemPath, secondRowItemPath);
					testRunner.Wait(2);
					testRunner.Type(textForBothRowItems);
					testRunner.Wait(1);
					testRunner.Type("{Enter}");

					resultsHarness.AddTestResult(testRunner.WaitForName("Row Item Fennec Fox", 2) == true);
					resultsHarness.AddTestResult(testRunner.WaitForName("Row Item Batman", 2) == true);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = null;

			try
			{
				testHarness = MatterControlUtilities.RunTest(testToRun);

			}
			catch { }
			finally
			{
				MatterControlUtilities.CleanupDownloadsDirectory(MatterControlUtilities.PathToDownloadsSubFolder);
			}

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2); // make sure we ran all our tests
		}
	}


	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class AddAMFToDownloads
	{

		[Test, RequiresSTA, RunInApplicationDomain]
		public void DownloadsAddButtonAddsAMFFiles()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					MatterControlUtilities.CreateDownloadsSubFolder();

					//Navigate to Downloads Library Provider
					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Downloads Row Item Collection");
					MatterControlUtilities.NavigateToFolder(testRunner, "Temporary Row Item Collection");
					testRunner.ClickByName("Library Add Button");
					testRunner.Wait(2);

					//Add AMF part items to Downloads and then type paths into file dialogues 
					testRunner.Wait(2);
					testRunner.Type(MatterControlUtilities.GetTestItemPath("Rook.amf"));
					testRunner.Wait(1);
					testRunner.Type("{Enter}");

					resultsHarness.AddTestResult(testRunner.WaitForName("Row Item Rook", 2) == true);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = null;

			try
			{
				testHarness = MatterControlUtilities.RunTest(testToRun);

			}
			catch { }
			finally
			{
				MatterControlUtilities.CleanupDownloadsDirectory(MatterControlUtilities.PathToDownloadsSubFolder);
			}

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 1);

		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class AddZipFileToDownloads
	{

		[Test, RequiresSTA, RunInApplicationDomain]
		public void DownloadsAddButtonAddsZipFiles()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);
					MatterControlUtilities.CreateDownloadsSubFolder();
					
					//Navigate to Downloads Library Provider
					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Downloads Row Item Collection");
					MatterControlUtilities.NavigateToFolder(testRunner, "Temporary Row Item Collection");
					testRunner.ClickByName("Library Add Button");
					testRunner.Wait(2);

					//Add AMF part items to Downloads and then type paths into file dialogues 
					testRunner.Wait(2);
					testRunner.Type(MatterControlUtilities.GetTestItemPath("Test.zip"));
					testRunner.Wait(1);
					testRunner.Type("{Enter}");


					resultsHarness.AddTestResult(testRunner.WaitForName("Row Item Chinese Dragon", 2) == true);
					resultsHarness.AddTestResult(testRunner.WaitForName("Row Item chichen-itza pyramid", 2) == true);
					resultsHarness.AddTestResult(testRunner.WaitForName("Row Item Circle Calibration", 2) == true);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};
			AutomationTesterHarness testHarness = null;

			try
			{
				testHarness = MatterControlUtilities.RunTest(testToRun);

			}
			catch { }
			finally
			{
				MatterControlUtilities.CleanupDownloadsDirectory(MatterControlUtilities.PathToDownloadsSubFolder);
			}

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 3);
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class RenameDownloadsItem
	{

		[Test, RequiresSTA, RunInApplicationDomain]
		public void RenameDownloadsPrintItem()
		{

			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);
					MatterControlUtilities.CreateDownloadsSubFolder();


					//Navigate to Downloads Library Provider
					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Downloads Row Item Collection");
					MatterControlUtilities.NavigateToFolder(testRunner, "Temporary Row Item Collection");
					testRunner.ClickByName("Library Add Button");
					testRunner.Wait(2);

					testRunner.Type(MatterControlUtilities.GetTestItemPath("Batman.stl"));
					testRunner.Wait(1);
					testRunner.Type("{Enter}");

					//Rename added item
					testRunner.ClickByName("Library Edit Button", .5);
					testRunner.ClickByName("Row Item Batman");
					MatterControlUtilities.LibraryRenameSelectedItem(testRunner);
					testRunner.Wait(.5);
					testRunner.Type("Batman Renamed");
					testRunner.ClickByName("Rename Button");
					resultsHarness.AddTestResult(testRunner.WaitForName("Row Item Batman Renamed", 2) == true);
					
					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};
			AutomationTesterHarness testHarness = null;

			try
			{
				testHarness = MatterControlUtilities.RunTest(testToRun);

			}
			catch { }
			finally
			{
				MatterControlUtilities.CleanupDownloadsDirectory(MatterControlUtilities.PathToDownloadsSubFolder);
			}

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 1);
		}
	}


		[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
		public class CreateSubFolderLibraryDownloads
		{

			[Test, RequiresSTA, RunInApplicationDomain]
			public void CreateFolder()
			{

				Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
				{
					AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
					{
						MatterControlUtilities.PrepForTestRun(testRunner);

						MatterControlUtilities.CreateDownloadsSubFolder();

						//Navigate to Downloads Library Provider
						testRunner.ClickByName("Library Tab");
						MatterControlUtilities.NavigateToFolder(testRunner, "Downloads Row Item Collection");
						MatterControlUtilities.NavigateToFolder(testRunner, "Temporary Row Item Collection");
						testRunner.ClickByName("Create Folder From Library Button");
						testRunner.Wait(2);
						testRunner.Type("New Folder");
						testRunner.ClickByName("Create Folder Button");

						testRunner.Wait(2);
						resultsHarness.AddTestResult(testRunner.WaitForName("New Folder Row Item Collection", 2) == true);

						MatterControlUtilities.CloseMatterControl(testRunner);
					}
				};
				AutomationTesterHarness testHarness = null;

				try
				{
					testHarness = MatterControlUtilities.RunTest(testToRun);

				}
				catch { }
				finally
				{
					MatterControlUtilities.CleanupDownloadsDirectory(MatterControlUtilities.PathToDownloadsSubFolder);
				}

				Assert.IsTrue(testHarness.AllTestsPassed);
				Assert.IsTrue(testHarness.TestCount == 1);
			}
		}
	
}

	

