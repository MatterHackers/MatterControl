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
		[SetUp]
		public void Setup()
		{
			MatterControlUtilities.CreateDownloadsSubFolder();
		}

		[TearDown]
		public void TearDown()
		{
			MatterControlUtilities.DeleteDownloadsSubFolder();
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task DownloadsAddButtonAddsMultipleFiles()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				// Navigate to Downloads Library Provider
				testRunner.NavigateToFolder("Downloads Row Item Collection");
				testRunner.NavigateToFolder("-Temporary Row Item Collection");

				// Add both files to the FileOpen dialog
				testRunner.ClickByName("Library Add Button");
				testRunner.CompleteDialog(
					string.Format(
						"\"{0}\" \"{1}\"",
						MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl"),
						MatterControlUtilities.GetTestItemPath("Batman.stl")),
					5);

				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				Assert.IsTrue(testRunner.WaitForName("Row Item Fennec Fox", 2), "Fennec Fox item exists");
				Assert.IsTrue(testRunner.WaitForName("Row Item Batman", 2), "Batman item exists");

				return Task.CompletedTask;
			});
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task DownloadsAddButtonAddsAMFFiles()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				// Navigate to Downloads Library Provider
				testRunner.NavigateToFolder("Downloads Row Item Collection");
				testRunner.NavigateToFolder("-Temporary Row Item Collection");

				// Add AMF part items to Downloads and then type paths into file dialog 
				testRunner.ClickByName("Library Add Button");
				testRunner.CompleteDialog(MatterControlUtilities.GetTestItemPath("Rook.amf"), 4);
				testRunner.Type("{Enter}");

				Assert.IsTrue(testRunner.WaitForName("Row Item Rook", 2), "Rook item exists");
				testRunner.Delay(1);

				return Task.CompletedTask;
			});
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task DownloadsAddButtonAddsZipFiles()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				// Navigate to Downloads Library Provider
				testRunner.NavigateToFolder("Downloads Row Item Collection");
				testRunner.NavigateToFolder("-Temporary Row Item Collection");

				testRunner.ClickByName("Library Add Button");
				testRunner.CompleteDialog(MatterControlUtilities.GetTestItemPath("Test.zip"), 4);
				testRunner.Type("{Enter}");

				Assert.IsTrue(testRunner.WaitForName("Row Item Chinese Dragon", 2), "Chinese Dragon item exists");
				Assert.IsTrue(testRunner.WaitForName("Row Item chichen-itza pyramid", 2), "chichen-itza item exists");
				Assert.IsTrue(testRunner.WaitForName("Row Item Circle Calibration", 2), "Circle Calibration item exists");

				return Task.CompletedTask;
			});
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task RenameDownloadsPrintItem()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				// Navigate to Downloads Library Provider
				testRunner.NavigateToFolder("Downloads Row Item Collection");
				testRunner.NavigateToFolder("-Temporary Row Item Collection");
				testRunner.ClickByName("Library Add Button");

				testRunner.CompleteDialog(MatterControlUtilities.GetTestItemPath("Batman.stl"), 2);
				testRunner.Type("{Enter}");

				// Rename added item
				testRunner.ClickByName("Library Edit Button");
				testRunner.ClickByName("Row Item Batman");

				testRunner.LibraryRenameSelectedItem();
				testRunner.CompleteDialog("Batman Renamed", .5);

				testRunner.ClickByName("InputBoxPage Action Button");
				Assert.IsTrue(testRunner.WaitForName("Row Item Batman Renamed", 2));

				return Task.CompletedTask;
			});
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task CreateFolder()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				//Navigate to Downloads Library Provider
				testRunner.NavigateToFolder("Downloads Row Item Collection");
				testRunner.NavigateToFolder("-Temporary Row Item Collection");

				string newFolderName = "New Folder";

				testRunner.ClickByName("Create Folder From Library Button");
				testRunner.WaitForName("InputBoxPage Action Button");
				testRunner.Type(newFolderName);
				testRunner.ClickByName("InputBoxPage Action Button");

				Assert.IsTrue(testRunner.WaitForName(newFolderName + " Row Item Collection"), $"{newFolderName} exists");

				return Task.CompletedTask;
			});
		}
	}
}