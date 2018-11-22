using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class LocalLibraryTests
	{
		[Test]
		public async Task LocalLibraryAddButtonAddSingleItemToLibrary()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();
				testRunner.AddTestAssetsToLibrary(new[] { "Batman.stl" });

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task LocalLibraryAddButtonAddsMultipleItemsToLibrary()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();
				testRunner.AddTestAssetsToLibrary(new[] { "Rook.amf", "Batman.stl" });

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task LocalLibraryAddButtonAddAMFToLibrary()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();
				testRunner.AddTestAssetsToLibrary(new[] { "Rook.amf" });

				return Task.CompletedTask;
			}, overrideWidth: 1024, overrideHeight: 800);
		}

		[Test]
		public async Task ParentFolderRefreshedOnPathPop()
		{
			// Expected: When descending into a child folder and moving items into the parent, popping the path to the parent should refresh and show the moved content
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.AddAndSelectPrinter();

				// Navigate to Local Library
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				string folderID = testRunner.CreateChildFolder("New Folder");

				testRunner.DoubleClickByName(folderID);

				// Add Library item
				testRunner.InvokeLibraryAddDialog();
				testRunner.Delay(2);
				testRunner.Type(MatterControlUtilities.GetTestItemPath("Batman.stl"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				string newFileID = "Row Item Batman";

				testRunner.ClickByName(newFileID);

				testRunner.LibraryMoveSelectedItem();

				testRunner.NavigateToFolder("Local Library Row Item Collection");

				// Click Move
				testRunner.ClickByName("Accept Button");

				// Wait for closed window/closed row
				testRunner.WaitForWidgetDisappear("Move Item Window", 5);
				testRunner.WaitForWidgetDisappear("Row Item Batman", 2);

				// Return to the Local Library folder
				testRunner.ClickByName("Library Up Button");

				// Assert that the expected item appears in the parent after popping the path
				testRunner.ClickByName(newFileID);

				return Task.CompletedTask;
			});
		}


		[Test]
		public async Task LocalLibraryAddButtonAddZipToLibrary()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();

				// Navigate to Local Library
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				// Make sure that Item does not exist before the test begins
				Assert.IsFalse(testRunner.WaitForName("Row Item Batman", 1), "Batman part should not exist at test start");
				Assert.IsFalse(testRunner.WaitForName("Row Item 2013-01-25 Mouthpiece V2", 1), "Mouthpiece part should not exist at test start");

				// Add Library item
				testRunner.InvokeLibraryAddDialog();
				testRunner.Delay(2);
				testRunner.Type(MatterControlUtilities.GetTestItemPath("Batman.zip"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				testRunner.WaitForName("Batman Row Item Collection");

				testRunner.DoubleClickByName("Batman Row Item Collection");

				Assert.IsTrue(testRunner.WaitForName("Row Item Batman.stl"), "Batman part should exist after adding");
				Assert.IsTrue(testRunner.WaitForName("Row Item 2013-01-25_Mouthpiece_v2.stl"), "Mouthpiece part should exist after adding");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task RenameButtonRenameLocalLibraryItem()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();

				testRunner.AddTestAssetsToLibrary(new[] { "Rook.amf" });

				testRunner.ClickByName("Row Item Rook");

				// Open and wait rename window
				testRunner.LibraryRenameSelectedItem();
				testRunner.WaitForName("InputBoxPage Action Button");

				testRunner.Delay(1);

				// Rename item
				testRunner.Type("Rook Renamed");
				testRunner.ClickByName("InputBoxPage Action Button");

				// Confirm
				Assert.IsTrue(testRunner.WaitForName("Row Item Rook Renamed"));
				Assert.IsFalse(testRunner.WaitForName("Row Item Rook", 1));

				return Task.CompletedTask;
			}, overrideWidth: 600);
		}

		[Test]
		public async Task RenameButtonRenamesLocalLibraryFolder()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();

				// Navigate to Local Library
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				// Create New Folder
				string folderID = testRunner.CreateChildFolder("New Folder");

				testRunner.ClickByName(folderID);
				testRunner.Delay(.2);

				testRunner.LibraryRenameSelectedItem();

				testRunner.Delay(.5);
				testRunner.Type("Renamed Library Folder");

				testRunner.ClickByName("InputBoxPage Action Button");
				testRunner.Delay(.2);

				// Make sure the renamed Library Folder exists
				Assert.IsTrue(testRunner.WaitForName("Renamed Library Folder Row Item Collection"), "Renamed folder should exist");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task RemoveButtonClickedRemovesSingleItem()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();

				testRunner.AddTestAssetsToLibrary(new[] { "Rook.amf" });

				// Select and remove item
				testRunner.ClickByName("Row Item Rook");
				testRunner.LibraryRemoveSelectedItem();

				// Make sure that the item has been removed
				Assert.IsFalse(testRunner.WaitForName("Row Item Rook", .5));

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task RemoveButtonClickedRemovesMultipleItems()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();

				testRunner.AddTestAssetsToLibrary(new[] { "Rook.amf", "Batman.stl" });

				// Select both items
				testRunner.SelectListItems("Row Item Rook", "Row Item Batman");

				// Remove items
				testRunner.LibraryRemoveSelectedItem();
				testRunner.Delay(1);

				// Make sure both selected items are removed
				Assert.IsFalse(testRunner.WaitForName("Row Item Rook", 1), "Rook part should *not* exist after remove");
				Assert.IsFalse(testRunner.WaitForName("Row Item Batman", 1), "Batman part *not* exist after remove");

				return Task.CompletedTask;
			});
		}
	}
}
