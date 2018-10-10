using System;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), Apartment(ApartmentState.STA), RunInApplicationDomain]
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

		[Test]
		public async Task DownloadsAddButtonAddsMultipleFiles()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.AddAndSelectPrinter();

				// Navigate to Downloads Library Provider
				testRunner.NavigateToFolder("Downloads Row Item Collection");
				testRunner.NavigateToFolder("-Temporary Row Item Collection");

				// Add both files to the FileOpen dialog
				testRunner.InvokeLibraryAddDialog();
				testRunner.CompleteDialog(
					string.Format(
						"\"{0}\" \"{1}\"",
						MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl"),
						MatterControlUtilities.GetTestItemPath("Batman.stl")),
					5);

				Assert.IsTrue(testRunner.WaitForName("Row Item Fennec_Fox.stl", 2), "Fennec Fox item exists");
				Assert.IsTrue(testRunner.WaitForName("Row Item Batman.stl", 2), "Batman item exists");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task DownloadsAddButtonAddsAMFFiles()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.AddAndSelectPrinter();

				// Navigate to Downloads Library Provider
				testRunner.NavigateToFolder("Downloads Row Item Collection");
				testRunner.NavigateToFolder("-Temporary Row Item Collection");

				// Add AMF part items to Downloads and then type paths into file dialog
				testRunner.InvokeLibraryAddDialog();
				testRunner.CompleteDialog(MatterControlUtilities.GetTestItemPath("Rook.amf"), 4);

				Assert.IsTrue(testRunner.WaitForName("Row Item Rook.amf"), "Rook item exists");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task DownloadsAddButtonAddsZipFiles()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.AddAndSelectPrinter();

				// Navigate to Downloads Library Provider
				testRunner.NavigateToFolder("Downloads Row Item Collection");
				testRunner.NavigateToFolder("-Temporary Row Item Collection");

				testRunner.InvokeLibraryAddDialog();
				testRunner.CompleteDialog(MatterControlUtilities.GetTestItemPath("Test.zip"), 4);

				testRunner.DoubleClickByName("Test.zip Row Item Collection");

				testRunner.DoubleClickByName("TestCompress Row Item Collection");

				Assert.IsTrue(testRunner.WaitForName("Row Item Chinese Dragon.stl", 2), "Chinese Dragon item exists");
				Assert.IsTrue(testRunner.WaitForName("Row Item chichen-itza_pyramid.stl", 2), "chichen-itza item exists");
				Assert.IsTrue(testRunner.WaitForName("Row Item Circle Calibration.stl", 2), "Circle Calibration item exists");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task RenameDownloadsPrintItem()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.AddAndSelectPrinter();

				// Navigate to Downloads Library Provider
				testRunner.NavigateToFolder("Downloads Row Item Collection");
				testRunner.NavigateToFolder("-Temporary Row Item Collection");
				testRunner.InvokeLibraryAddDialog();

				testRunner.CompleteDialog(MatterControlUtilities.GetTestItemPath("Batman.stl"), 2);

				// Rename added item
				testRunner.ClickByName("Row Item Batman.stl");

				testRunner.LibraryRenameSelectedItem();

				testRunner.WaitForName("InputBoxPage Action Button");
				testRunner.Type("Batman Renamed");

				testRunner.ClickByName("InputBoxPage Action Button");

				Assert.IsTrue(testRunner.WaitForName("Row Item Batman Renamed.stl", 2));

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task CreateFolder()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.AddAndSelectPrinter();

				//Navigate to Downloads Library Provider
				testRunner.NavigateToFolder("Downloads Row Item Collection");
				testRunner.NavigateToFolder("-Temporary Row Item Collection");

				testRunner.CreateChildFolder("New Folder");

				return Task.CompletedTask;
			});
		}
	}
}