using System;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class LibraryDownloadsTests
	{
		[Test, Apartment(ApartmentState.STA)]
		public async Task DownloadsAddButtonAddsMultipleFiles()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);
				MatterControlUtilities.CreateDownloadsSubFolder();

				//Navigate to Downloads Library Provider
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Downloads Row Item Collection");
				testRunner.NavigateToFolder("-Temporary Row Item Collection");
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

				Assert.IsTrue(testRunner.WaitForName("Row Item Fennec Fox", 2), "Fennec Fox item exists");
				Assert.IsTrue(testRunner.WaitForName("Row Item Batman", 2), "Batman item exists");
				testRunner.Wait(1);

				return Task.FromResult(0);
			};

			// TODO: The standard assignment without a try/catch should be used and DeleteDownloadsSubFolder should be called from a TearDown method
			try
			{
				await MatterControlUtilities.RunTest(testToRun);

			}
			catch { }
			finally
			{
				MatterControlUtilities.DeleteDownloadsSubFolder();
			}
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task DownloadsAddButtonAddsAMFFiles()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);
				MatterControlUtilities.CreateDownloadsSubFolder();

				//Navigate to Downloads Library Provider
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Downloads Row Item Collection");
				testRunner.NavigateToFolder("-Temporary Row Item Collection");
				testRunner.ClickByName("Library Add Button");
				testRunner.Wait(2);

				//Add AMF part items to Downloads and then type paths into file dialog 
				testRunner.Wait(2);
				testRunner.Type(MatterControlUtilities.GetTestItemPath("Rook.amf"));
				testRunner.Wait(1);
				testRunner.Type("{Enter}");

				Assert.IsTrue(testRunner.WaitForName("Row Item Rook", 2), "Rook item exists");
				testRunner.Wait(1);

				return Task.FromResult(0);
			};

			try
			{
				await MatterControlUtilities.RunTest(testToRun);

			}
			catch { }
			finally
			{
				MatterControlUtilities.DeleteDownloadsSubFolder();
			}
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task DownloadsAddButtonAddsZipFiles()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);
				MatterControlUtilities.CreateDownloadsSubFolder();

				// Navigate to Downloads Library Provider
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Downloads Row Item Collection");
				testRunner.NavigateToFolder("-Temporary Row Item Collection");
				testRunner.ClickByName("Library Add Button");
				testRunner.Wait(2);

				// Add AMF part items to Downloads and then type paths into file dialogs 
				testRunner.Wait(2);
				testRunner.Type(MatterControlUtilities.GetTestItemPath("Test.zip"));
				testRunner.Wait(1);
				testRunner.Type("{Enter}");

				Assert.IsTrue(testRunner.WaitForName("Row Item Chinese Dragon", 2), "Chinese Dragon item exists");
				Assert.IsTrue(testRunner.WaitForName("Row Item chichen-itza pyramid", 2), "chichen-itza item exists");
				Assert.IsTrue(testRunner.WaitForName("Row Item Circle Calibration", 2), "Circle Calibration item exists");

				testRunner.Wait(1);

				return Task.FromResult(0);
			};

			try
			{
				MatterControlUtilities.RunTest(testToRun);
			}
			catch { }

			// Give MatterControl a moment to shutdown
			Thread.Sleep(2000);
			try
			{
				// Then attempt to clean up
				MatterControlUtilities.DeleteDownloadsSubFolder();
			}
			catch { }
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task RenameDownloadsPrintItem()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);
				MatterControlUtilities.CreateDownloadsSubFolder();

				//Navigate to Downloads Library Provider
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Downloads Row Item Collection");
				testRunner.NavigateToFolder("-Temporary Row Item Collection");
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
				Assert.IsTrue(testRunner.WaitForName("Row Item Batman Renamed", 2));

				return Task.FromResult(0);
			};

			try
			{
				await MatterControlUtilities.RunTest(testToRun);

			}
			catch { }
			finally
			{
				MatterControlUtilities.DeleteDownloadsSubFolder();
			}
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task CreateFolder()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);
				MatterControlUtilities.CreateDownloadsSubFolder();

				//Navigate to Downloads Library Provider
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Downloads Row Item Collection");
				testRunner.NavigateToFolder("-Temporary Row Item Collection");

				string newFolderName = "New Folder";

				testRunner.ClickByName("Create Folder From Library Button");
				testRunner.Wait(2);
				testRunner.Type(newFolderName);
				testRunner.ClickByName("Create Folder Button");

				testRunner.Wait(2);
				Assert.IsTrue(testRunner.WaitForName(newFolderName + " Row Item Collection", 2), $"{newFolderName} exists");

				return Task.FromResult(0);
			};

			try
			{
				await MatterControlUtilities.RunTest(testToRun);

			}
			catch { }
			finally
			{
				MatterControlUtilities.DeleteDownloadsSubFolder();
			}
		}
	}
}