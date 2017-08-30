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
				testRunner.CloseSignInAndPrinterSelect();
				testRunner.AddTestAssetsToLibrary("Batman.stl");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task LocalLibraryAddButtonAddsMultipleItemsToLibrary()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();
				testRunner.AddTestAssetsToLibrary("Rook.amf", "Batman.stl");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task LocalLibraryAddButtonAddAMFToLibrary()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();
				testRunner.AddTestAssetsToLibrary("Rook.amf");

				return Task.CompletedTask;
			}, overrideWidth: 1024, overrideHeight: 800);
		}

		[Test]
		public async Task LocalLibraryAddButtonAddZipToLibrary()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				// Navigate to Local Library 
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				// Make sure that Item does not exist before the test begins
				Assert.IsFalse(testRunner.WaitForName("Row Item Batman", 1), "Batman part should not exist at test start");
				Assert.IsFalse(testRunner.WaitForName("Row Item 2013-01-25 Mouthpiece V2", 1), "Mouthpiece part should not exist at test start");

				// Add Library item
				testRunner.ClickByName("Library Add Button");
				testRunner.Delay(2);
				testRunner.Type(MatterControlUtilities.GetTestItemPath("Batman.zip"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				Assert.IsTrue(testRunner.WaitForName("Row Item Batman"), "Batman part should exist after adding");
				Assert.IsTrue(testRunner.WaitForName("Row Item 2013-01-25 Mouthpiece V2"), "Mouthpiece part should exist after adding");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task RenameButtonRenameLocalLibraryItem()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.AddTestAssetsToLibrary("Rook.amf");

				testRunner.ClickByName("Row Item Rook");

				// Open and wait rename window
				testRunner.LibraryRenameSelectedItem();
				testRunner.WaitForName("Rename Button");

				testRunner.Delay(1);

				// Rename item
				testRunner.Type("Rook Renamed");
				testRunner.ClickByName("Rename Button");

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
				testRunner.CloseSignInAndPrinterSelect();

				// Navigate to Local Library
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				// Create New Folder
				testRunner.ClickByName("Create Folder From Library Button");
				testRunner.Delay(.5);

				testRunner.Type("New Folder");
				testRunner.Delay(.5);

				testRunner.ClickByName("Create Folder Button");
				testRunner.Delay(.2);

				// Confirm newly created folder exists
				Assert.IsTrue(testRunner.WaitForName("New Folder Row Item Collection"), "New folder should appear as GuiWidget");

				testRunner.ClickByName("New Folder Row Item Collection");
				testRunner.Delay(.2);

				testRunner.LibraryRenameSelectedItem();

				testRunner.Delay(.5);
				testRunner.Type("Renamed Library Folder");

				testRunner.ClickByName("Rename Button");
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
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.AddTestAssetsToLibrary("Rook.amf");

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
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.AddTestAssetsToLibrary("Rook.amf", "Batman.stl");

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

		[Test]
		public async Task AddToQueueFromLibraryButtonAddsItemToQueue()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				int expectedCount = QueueData.Instance.ItemCount + 1;

				testRunner.CloseSignInAndPrinterSelect();

				testRunner.AddTestAssetsToLibrary("Rook.amf");

				// Select Library Item
				testRunner.ClickByName("Row Item Rook");

				// Add Library Item to the Print Queue
				MatterControlUtilities.LibraryAddSelectionToQueue(testRunner);
				testRunner.Delay(2);

				// Make sure that the Queue Count increases by one
				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue item count should increase by one after add");

				// Navigate to the PrintQueueContainer
				testRunner.NavigateToLibraryHome();
				testRunner.NavigateToFolder("Print Queue Row Item Collection");

				// Make sure that the item exists in the PrintQueueContainer
				Assert.IsTrue(testRunner.WaitForName("Row Item Rook"), "Rook item should exist in the Queue after Add");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task AddToQueueFromLibraryButtonAddsItemsToQueue()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				int expectedCount = QueueData.Instance.ItemCount + 2;

				testRunner.CloseSignInAndPrinterSelect();

				testRunner.AddTestAssetsToLibrary("Rook.amf", "Batman.stl");
				
				// Select both items
				testRunner.ClickByName("Row Item Rook");
				Keyboard.SetKeyDownState(Keys.ControlKey, down: true);
				testRunner.ClickByName("Row Item Batman");
				Keyboard.SetKeyDownState(Keys.ControlKey, down: false);

				// Click the Add To Queue button
				testRunner.Delay(1);
				MatterControlUtilities.LibraryAddSelectionToQueue(testRunner);

				// TODO: Somehow thumbnail generation is happening on the UI thread and bogging this down. Leave at 15 second for a short-term workaround
				testRunner.Delay(() => QueueData.Instance.ItemCount == expectedCount, 15, 500);

				// Make sure Queue Count increases by the correct amount
				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount);

				// Navigate to the PrintQueueContainer
				testRunner.NavigateToLibraryHome();
				testRunner.NavigateToFolder("Print Queue Row Item Collection");

				// Make sure that the items exist in the PrintQueueContainer
				Assert.IsTrue(testRunner.WaitForName("Row Item Rook"), "Rook item should exist in the Queue after Add");
				Assert.IsTrue(testRunner.WaitForName("Row Item Batman"), "Batman item should exist in the Queue after Add");

				return Task.CompletedTask;
			});
		}
	}
}
