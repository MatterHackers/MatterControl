using System;
using System.Threading;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation")]
	public class LibraryDownloadsTests
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void DownloadsAddButtonAddsMultipleFiles()
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
					MatterControlUtilities.NavigateToFolder(testRunner, "-Temporary Row Item Collection");
					testRunner.ClickByName("Library Add Button");
					testRunner.Wait(3);

					testRunner.Wait(2);

					// Add both files to the FileOpen dialog
					testRunner.Type(
						string.Format(
							"\"{0}\" \"{1}\"",
							MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl"),
							MatterControlUtilities.GetTestItemPath("Batman.stl")));

					testRunner.Wait(1);
					testRunner.Type("{Enter}");

					resultsHarness.AddTestResult(testRunner.WaitForName("Row Item Fennec Fox", 2), "Fennec Fox item exists");
					resultsHarness.AddTestResult(testRunner.WaitForName("Row Item Batman", 2), "Batman item exists");
					testRunner.Wait(1);

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
				MatterControlUtilities.DeleteDownloadsSubFolder();
			}

			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}

		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void DownloadsAddButtonAddsAMFFiles()
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
					MatterControlUtilities.NavigateToFolder(testRunner, "-Temporary Row Item Collection");
					testRunner.ClickByName("Library Add Button");
					testRunner.Wait(2);

					//Add AMF part items to Downloads and then type paths into file dialogues 
					testRunner.Wait(2);
					testRunner.Type(MatterControlUtilities.GetTestItemPath("Rook.amf"));
					testRunner.Wait(1);
					testRunner.Type("{Enter}");

					resultsHarness.AddTestResult(testRunner.WaitForName("Row Item Rook", 2), "Rook item exists");
					testRunner.Wait(1);

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
				MatterControlUtilities.DeleteDownloadsSubFolder();
			}

			Assert.IsTrue(testHarness.AllTestsPassed(1));
		}

		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void DownloadsAddButtonAddsZipFiles()
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
					MatterControlUtilities.NavigateToFolder(testRunner, "-Temporary Row Item Collection");
					testRunner.ClickByName("Library Add Button");
					testRunner.Wait(2);

					//Add AMF part items to Downloads and then type paths into file dialogues 
					testRunner.Wait(2);
					testRunner.Type(MatterControlUtilities.GetTestItemPath("Test.zip"));
					testRunner.Wait(1);
					testRunner.Type("{Enter}");


					resultsHarness.AddTestResult(testRunner.WaitForName("Row Item Chinese Dragon", 2), "Chinese Dragon item exists");
					resultsHarness.AddTestResult(testRunner.WaitForName("Row Item chichen-itza pyramid", 2), "chichen-itza item exists");
					resultsHarness.AddTestResult(testRunner.WaitForName("Row Item Circle Calibration", 2), "Circle Calibration item exists");

					testRunner.Wait(1);

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
				MatterControlUtilities.DeleteDownloadsSubFolder();
			}

			Assert.IsTrue(testHarness.AllTestsPassed(3));
		}

		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
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
					MatterControlUtilities.NavigateToFolder(testRunner, "-Temporary Row Item Collection");
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
				MatterControlUtilities.DeleteDownloadsSubFolder();
			}

			Assert.IsTrue(testHarness.AllTestsPassed(1));
		}

		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
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
					MatterControlUtilities.NavigateToFolder(testRunner, "-Temporary Row Item Collection");

					string newFolderName = "New Folder";

					testRunner.ClickByName("Create Folder From Library Button");
					testRunner.Wait(2);
					testRunner.Type(newFolderName);
					testRunner.ClickByName("Create Folder Button");

					testRunner.Wait(2);
					resultsHarness.AddTestResult(testRunner.WaitForName(newFolderName + " Row Item Collection", 2), $"{newFolderName} exists");

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
				MatterControlUtilities.DeleteDownloadsSubFolder();
			}

			Assert.IsTrue(testHarness.AllTestsPassed(1));
		}
	}
}