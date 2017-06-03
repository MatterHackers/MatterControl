using System;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrinterCommunication;
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

				string itemName = "Row Item Fennec Fox";

				// Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				Assert.IsFalse(testRunner.WaitForName(itemName, 1), "Fennec Fox should not exist at test start");

				// Click Local Library Add Button
				testRunner.ClickByName("Library Add Button");

				// Get Library Item to Add
				testRunner.Delay(2);
				testRunner.Type(MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				Assert.IsTrue(testRunner.WaitForName(itemName, 2));

				return Task.FromResult(0);
			});
		}

		[Test]
		public async Task LocalLibraryAddButtonAddsMultipleItemsToLibrary()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				// Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				// Make sure both Items do not exist before the test begins
				Assert.IsFalse(testRunner.WaitForName("Row Item Fennec Fox", 1), "Fennec Fox part should not exist at test start");
				Assert.IsFalse(testRunner.WaitForName("Row Item Batman", 1), "Batman part should not exist at test start");

				// Click Local Library Add Button
				testRunner.ClickByName("Library Add Button");

				testRunner.Delay(2);

				// Type file paths for each file into File Open dialog
				testRunner.Type(string.Format("\"{0}\" \"{1}\"",
					MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl"),
					MatterControlUtilities.GetTestItemPath("Batman.stl")));

				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				Assert.IsTrue(testRunner.WaitForName("Row Item Fennec Fox", 2), "Fennec Fox part should exist after adding");
				Assert.IsTrue(testRunner.WaitForName("Row Item Batman", 2), "Batman part should exist after adding");

				return Task.FromResult(0);
			});
		}

		[Test]
		public async Task LocalLibraryAddButtonAddAMFToLibrary()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				// Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				// Make sure that Item does not exist before the test begins
				Assert.IsFalse(testRunner.WaitForName("Row Item Rook", 1), "Rook part should not exist at test start");

				// Add Library item
				testRunner.ClickByName("Library Add Button");
				testRunner.Delay(2);
				testRunner.Type(MatterControlUtilities.GetTestItemPath("Rook.amf"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				Assert.IsTrue(testRunner.WaitForName("Row Item Rook", 2), "Rook part should exist after adding");

				return Task.FromResult(0);
			}, overrideWidth: 1024, overrideHeight: 800);
		}

		[Test]
		public async Task LocalLibraryAddButtonAddZipToLibrary()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				// Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
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

				Assert.IsTrue(testRunner.WaitForName("Row Item Batman", 2), "Batman part should exist after adding");
				Assert.IsTrue(testRunner.WaitForName("Row Item 2013-01-25 Mouthpiece V2", 2), "Mouthpiece part should exist after adding");

				return Task.FromResult(0);
			});
		}
	
		[Test]
		public async Task RenameButtonRenameLocalLibraryItem()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				//Navigate To Local Library 
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				// Add Library item
				testRunner.ClickByName("Library Add Button", 5);
				testRunner.Delay(2);
				testRunner.Type(MatterControlUtilities.GetTestItemPath("Rook.amf"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				testRunner.ClickByName("Row Item Rook", 2);

				// Open and wait rename window
				testRunner.LibraryRenameSelectedItem();
				testRunner.WaitForName("Rename Button");

				testRunner.Delay(1);

				// Rename item
				testRunner.Type("Rook Renamed");
				testRunner.ClickByName("Rename Button");

				// Confirm
				Assert.IsTrue(testRunner.WaitForName("Row Item Rook Renamed", 5));
				Assert.IsFalse(testRunner.WaitForName("Row Item Rook", 2));

				return Task.FromResult(0);
			}, overrideWidth: 600);
		}

		[Test]
		public async Task RenameButtonRenamesLocalLibraryFolder()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();
				// Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				testRunner.Delay(.2);

				testRunner.NavigateToFolder("Local Library Row Item Collection");

				// Create New Folder
				testRunner.ClickByName("Create Folder From Library Button");
				testRunner.Delay(.5);

				testRunner.Type("New Folder");
				testRunner.Delay(.5);

				testRunner.ClickByName("Create Folder Button");
				testRunner.Delay(.2);

				// Confirm newly created folder exists
				Assert.IsTrue(testRunner.WaitForName("New Folder Row Item Collection", 1), "New folder should appear as GuiWidget");

				testRunner.ClickByName("New Folder Row Item Collection");
				testRunner.Delay(.2);

				testRunner.LibraryRenameSelectedItem();

				testRunner.Delay(.5);
				testRunner.Type("Renamed Library Folder");

				testRunner.ClickByName("Rename Button");
				testRunner.Delay(.2);

				// Make sure the renamed Library Folder exists
				Assert.IsTrue(testRunner.WaitForName("Renamed Library Folder Row Item Collection", 2), "Renamed folder should exist");

				return Task.FromResult(0);	
			});
		}

		[Test]
		public async Task RemoveButtonClickedRemovesSingleItem()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();
				
				//Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				// Add Library item
				testRunner.ClickByName("Library Add Button", 5);
				testRunner.Delay(2);
				testRunner.Type(MatterControlUtilities.GetTestItemPath("Rook.amf"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				// Select and remove item
				testRunner.ClickByName("Row Item Rook", 5);
				MatterControlUtilities.LibraryRemoveSelectedItem(testRunner);
				testRunner.Delay(1);

				//Make sure that Export Item Window exists after Export button is clicked
				Assert.IsFalse(testRunner.WaitForName("Row Item Rook", 1));

				return Task.FromResult(0);
			});
		}

		[Test]
		public async Task RemoveButtonClickedRemovesMultipleItems()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				// Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				testRunner.ClickByName("Library Add Button");

				testRunner.Delay(2);

				// Type file paths for each file into File Open dialog
				testRunner.Type(string.Format("\"{0}\" \"{1}\"",
					MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl"),
					MatterControlUtilities.GetTestItemPath("Batman.stl")));

				testRunner.Type("{Enter}");

				// Make sure row items exist before remove
				Assert.IsTrue(testRunner.WaitForName("Row Item Batman", 6), "Batman part should exist after add");
				Assert.IsTrue(testRunner.WaitForName("Row Item Fennec Fox", 2), "Fennec part should exist after add");

				testRunner.ClickByName("Row Item Fennec Fox", 1);

				Keyboard.SetKeyDownState(Keys.ControlKey, down: true);
				testRunner.ClickByName("Row Item Batman", 1);
				Keyboard.SetKeyDownState(Keys.ControlKey, down: false);

				// Remove items
				MatterControlUtilities.LibraryRemoveSelectedItem(testRunner);
				testRunner.Delay(1);

				// Make sure both selected items are removed
				Assert.IsFalse(testRunner.WaitForName("Row Item Batman", 1), "Batman part should *not* exist after remove");
				Assert.IsFalse(testRunner.WaitForName("Row Item Fennec Fox", 1), "Fennec part *not* exist after remove");

				return Task.FromResult(0);
			});
		}

		[Test]
		public async Task AddToQueueFromLibraryButtonAddsItemToQueue()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				//Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				testRunner.Delay(1);
				testRunner.ClickByName("Library Edit Button");
				testRunner.Delay(1);

				//Select Library Item
				string rowItemOne = "Row Item Calibration - Box";
				testRunner.ClickByName(rowItemOne);

				testRunner.Delay(1);

				int queueCountBeforeAdd = QueueData.Instance.ItemCount;

				//Add Library Item to the Print Queue
				MatterControlUtilities.LibraryAddSelectionToQueue(testRunner);

				testRunner.Delay(2);

				//Make sure that the Queue Count increases by one
				int queueCountAfterAdd = QueueData.Instance.ItemCount;

				Assert.IsTrue(queueCountAfterAdd == queueCountBeforeAdd + 1);

				//Navigate to Queue
				testRunner.ClickByName("Queue Tab");

				testRunner.Delay(1);

				//Make sure that the Print Item was added
				string queueItem = "Queue Item Calibration - Box";
				bool queueItemWasAdded = testRunner.WaitForName(queueItem, 2);
				Assert.IsTrue(queueItemWasAdded == true);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test]
		public async Task AddToQueueFromLibraryButtonAddsItemsToQueue()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				//Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				//Add an item to the library
				string libraryItemToAdd = MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl");
				testRunner.ClickByName("Library Add Button");

				testRunner.Delay(2);
				testRunner.Type(libraryItemToAdd);
				testRunner.Delay(2);
				testRunner.Type("{Enter}");

				testRunner.Delay(1);
				testRunner.ClickByName("Library Edit Button");
				testRunner.Delay(1);

				int queueCountBeforeAdd = QueueData.Instance.ItemCount;

				//Select both Library Items
				string rowItemOne = "Row Item Calibration - Box";
				testRunner.ClickByName(rowItemOne);

				string rowItemTwo = "Row Item Fennec Fox";
				testRunner.ClickByName(rowItemTwo);

				//Click the Add To Queue button
				testRunner.Delay(1);
				MatterControlUtilities.LibraryAddSelectionToQueue(testRunner);
				testRunner.Delay(2);

				//Make sure Queue Count increases by the correct amount
				int queueCountAfterAdd = QueueData.Instance.ItemCount;

				Assert.IsTrue(queueCountAfterAdd == queueCountBeforeAdd + 2);

				//Navigate to the Print Queue
				testRunner.ClickByName("Queue Tab");
				testRunner.Delay(1);

				//Test that both added print items exist
				string queueItemOne = "Queue Item Calibration - Box";
				string queueItemTwo = "Queue Item Fennec_Fox";
				bool queueItemOneWasAdded = testRunner.WaitForName(queueItemOne, 2);
				bool queueItemTwoWasAdded = testRunner.WaitForName(queueItemTwo, 2);

				Assert.IsTrue(queueItemOneWasAdded == true);
				Assert.IsTrue(queueItemTwoWasAdded == true);

				return Task.FromResult(0);	
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test]
		public async Task LibraryItemThumbnailClickedOpensPartPreview()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				// Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				Assert.IsFalse(testRunner.WaitForName("Part Preview Window", 1), "Preview Window should not exist before we click the view button");

				testRunner.ClickByName("Row Item Calibration - Box");
				testRunner.Delay(1);

				// Click Library Item View Button
				testRunner.ClickByName("Row Item Calibration - Box View Button");

				Assert.IsTrue(testRunner.WaitForName("Part Preview Window", 2), "Part Preview Window should be open after View button is clicked");
				testRunner.Delay(.2);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test]
		public async Task PrintLibraryItem()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button", 1);

				using (var emulator = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					// Navigate to Local Library
					testRunner.ClickByName("Library Tab");
					testRunner.NavigateToFolder("Local Library Row Item Collection");

					testRunner.ClickByName("Row Item Calibration - Box");

					int initialQueueCount = QueueData.Instance.ItemCount;

					// Click Library Item Print Button
					testRunner.ClickByName("Row Item Calibration - Box Print Button");
					testRunner.Delay(2);

					Assert.AreEqual(initialQueueCount + 1, QueueData.Instance.ItemCount, "Queue count should increment by one after clicking 'Print'");
					Assert.AreEqual("Calibration - Box", QueueData.Instance.PrintItems[0].Name, "Library item should be inserted at queue index 0");
					Assert.AreEqual("Calibration - Box", QueueData.Instance.SelectedPrintItem.Name, "Library item should be the selected item");
					Assert.AreEqual("Calibration - Box", PrinterConnectionAndCommunication.Instance.ActivePrintItem.Name, "PrinterConnectionCommunication item should be the expected item");

					testRunner.CancelPrint();
					testRunner.Delay(1);

					testRunner.NameExists("Start Print Button");
				}

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}
	}
}
