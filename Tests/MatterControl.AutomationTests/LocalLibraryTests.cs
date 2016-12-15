using System;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class LocalLibraryTests
	{
		[Test, Apartment(ApartmentState.STA)]
		public async Task LocalLibraryAddButtonAddSingleItemToLibrary()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				string itemName = "Row Item Fennec Fox";

				//Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				//Make sure that Item does not exist before the test begins
				bool rowItemExists = testRunner.WaitForName(itemName, 1);
				Assert.IsTrue(rowItemExists == false);

				//Click Local Library Add Button
				testRunner.ClickByName("Library Add Button");

				//Get Library Item to Add
				string rowItemPath = MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl");
				testRunner.Wait(2);
				testRunner.Type(rowItemPath);
				testRunner.Wait(1);
				testRunner.Type("{Enter}");

				bool rowItemWasAdded = testRunner.WaitForName(itemName, 2);
				Assert.IsTrue(rowItemWasAdded == true);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task LocalLibraryAddButtonAddsMultipleItemsToLibrary()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();
				//Names of Items to be added
				string firstItemName = "Row Item Fennec Fox";
				string secondItemName = "Row Item Batman";

				//Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				//Make sure both Items do not exist before the test begins
				bool firstItemExists = testRunner.WaitForName(firstItemName, 1);
				bool secondItemExists = testRunner.WaitForName(secondItemName, 1);
				Assert.IsTrue(firstItemExists == false);
				Assert.IsTrue(secondItemExists == false);

				//Click Local Library Add Button
				testRunner.ClickByName("Library Add Button");

				//Get Library Item to Add
				string firstRowItemPath = MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl");
				string secondRowItemPath = MatterControlUtilities.GetTestItemPath("Batman.stl");

				string textForBothRowItems = string.Format("\"{0}\" \"{1}\"", firstRowItemPath, secondRowItemPath);
				testRunner.Wait(2);
				testRunner.Type(textForBothRowItems);
				testRunner.Wait(1);
				testRunner.Type("{Enter}");

				bool firstRowItemWasAdded = testRunner.WaitForName(firstItemName, 2);
				bool secondRowItemWasAdded = testRunner.WaitForName(secondItemName, 2);
				Assert.IsTrue(firstRowItemWasAdded == true);
				Assert.IsTrue(secondRowItemWasAdded == true);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task LocalLibraryAddButtonAddAMFToLibrary()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				string itemName = "Row Item Rook";

				//Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				//Make sure that Item does not exist before the test begins
				bool rowItemExists = testRunner.WaitForName(itemName, 1);
				Assert.IsTrue(rowItemExists == false);

				//Click Local Library Add Button
				testRunner.ClickByName("Library Add Button");

				//Get Library Item to Add
				string rowItemPath = MatterControlUtilities.GetTestItemPath("Rook.amf");
				testRunner.Wait(2);
				testRunner.Type(rowItemPath);
				testRunner.Wait(1);
				testRunner.Type("{Enter}");

				bool rowItemWasAdded = testRunner.WaitForName(itemName, 2);
				Assert.IsTrue(rowItemWasAdded == true);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideWidth: 1024, overrideHeight: 800);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task LocalLibraryAddButtonAddZipToLibrary()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				//Items in Batman.zip
				string firstItemName = "Row Item Batman";
				string secondItemName = "Row Item 2013-01-25 Mouthpiece v2";

				//Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				//Make sure that Item does not exist before the test begins
				bool firstItemInZipExists = testRunner.WaitForName(firstItemName, 1);
				bool secondItemInZipExists = testRunner.WaitForName(secondItemName, 1);
				Assert.IsTrue(firstItemInZipExists == false);
				Assert.IsTrue(firstItemInZipExists == false);

				//Click Local Library Add Button
				testRunner.ClickByName("Library Add Button");

				//Get Library Item to Add
				string rowItemPath = MatterControlUtilities.GetTestItemPath("Batman.zip");
				testRunner.Wait(2);
				testRunner.Type(rowItemPath);
				testRunner.Wait(1);
				testRunner.Type("{Enter}");

				bool firstItemInZipWasAdded = testRunner.WaitForName(firstItemName, 2);
				bool secondItemInZipWasAdded = testRunner.WaitForName(secondItemName, 2);
				Assert.IsTrue(firstItemInZipWasAdded == true);
				Assert.IsTrue(secondItemInZipWasAdded == true);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}
	
		[Test, Apartment(ApartmentState.STA)]
		public async Task RenameButtonRenameLocalLibraryItem()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				//Navigate To Local Library 
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				testRunner.Wait(1);

				string rowItemToRename = "Row Item Calibration - Box";
				testRunner.ClickByName("Library Edit Button");
				testRunner.Wait(1);
				testRunner.ClickByName(rowItemToRename);
				MatterControlUtilities.LibraryRenameSelectedItem(testRunner);

				testRunner.Wait(2);

				testRunner.Type("Library Item Renamed");

				testRunner.ClickByName("Rename Button");

				string renamedRowItem = "Row Item Library Item Renamed";
				bool libraryItemWasRenamed = testRunner.WaitForName(renamedRowItem, 2);
				bool libraryItemBeforeRenameExists = testRunner.WaitForName(rowItemToRename, 2);

				Assert.IsTrue(libraryItemWasRenamed == true);
				Assert.IsTrue(libraryItemBeforeRenameExists == false);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideWidth: 600);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task RenameButtonRenamesLocalLibraryFolder()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();
				// Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				testRunner.Wait(.2);

				testRunner.NavigateToFolder("Local Library Row Item Collection");

				// Create New Folder
				testRunner.ClickByName("Create Folder From Library Button");
				testRunner.Wait(.5);

				testRunner.Type("New Folder");
				testRunner.Wait(.5);

				testRunner.ClickByName("Create Folder Button");
				testRunner.Wait(.2);

				// Confirm newly created folder exists
				Assert.IsTrue(testRunner.WaitForName("New Folder Row Item Collection", 1), "New folder should appear as GuiWidget");

				testRunner.ClickByName("Library Edit Button");
				testRunner.Wait(.2);

				testRunner.ClickByName("New Folder Row Item Collection");
				testRunner.Wait(.2);

				MatterControlUtilities.LibraryRenameSelectedItem(testRunner);
				testRunner.Wait(.5);
				testRunner.Type("Renamed Library Folder");

				testRunner.ClickByName("Rename Button");
				testRunner.Wait(.2);

				// Make sure the renamed Library Folder exists
				Assert.IsTrue(testRunner.WaitForName("Renamed Library Folder Row Item Collection", 2), "Renamed folder should exist");

				return Task.FromResult(0);	
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task ClickLibraryEditButtonOpensPartPreviewWindow()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();
				//Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				testRunner.Wait(1);

				string rowItem = "Row Item Calibration - Box";
				testRunner.ClickByName("Library Edit Button");
				testRunner.Wait(1);
				testRunner.ClickByName(rowItem);

				MatterControlUtilities.LibraryEditSelectedItem(testRunner);

				//Make sure that Export Item Window exists after Export button is clicked
				bool exportItemWindowExists = testRunner.WaitForName("Part Preview Window", 2);
				Assert.IsTrue(exportItemWindowExists == true);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task RemoveButtonClickedRemovesSingleItem()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();
				//Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				testRunner.Wait(1);

				string rowItem = "Row Item Calibration - Box";
				testRunner.ClickByName("Library Edit Button");
				testRunner.Wait(1);
				testRunner.ClickByName(rowItem);

				MatterControlUtilities.LibraryRemoveSelectedItem(testRunner);

				testRunner.Wait(1);

				//Make sure that Export Item Window exists after Export button is clicked
				bool rowItemExists = testRunner.WaitForName(rowItem, 1);
				Assert.IsTrue(rowItemExists == false);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task RemoveButtonClickedRemovesMultipleItems()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				// Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				testRunner.Wait(1);
				testRunner.ClickByName("Library Edit Button");
				testRunner.Wait(1);

				string rowItemPath = MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl");
				testRunner.ClickByName("Library Add Button");

				testRunner.Wait(2);
				testRunner.Type(rowItemPath);
				testRunner.Type("{Enter}");
				testRunner.Wait(1);

				string rowItemOne = "Row Item Calibration - Box";
				testRunner.ClickByName(rowItemOne, 1);

				string rowItemTwo = "Row Item Fennec Fox";
				testRunner.ClickByName(rowItemTwo, 1);

				testRunner.Wait(1);

				// Make sure row items exist before remove
				Assert.IsTrue(testRunner.WaitForName(rowItemOne, 2), "rowItemOne should exist before remove");
				Assert.IsTrue(testRunner.WaitForName(rowItemTwo, 2), "rowItemTwo should exist before remove");

				// Remove items
				MatterControlUtilities.LibraryRemoveSelectedItem(testRunner);
				testRunner.Wait(1);

				// Make sure both selected items are removed
				Assert.IsFalse(testRunner.WaitForName(rowItemOne, 2), "rowItemOne should *not* exist after remove");
				Assert.IsFalse(testRunner.WaitForName(rowItemTwo, 2), "rowItemTwo should *not* exist after remove");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task AddToQueueFromLibraryButtonAddsItemToQueue()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				//Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				testRunner.Wait(1);
				testRunner.ClickByName("Library Edit Button");
				testRunner.Wait(1);

				//Select Library Item
				string rowItemOne = "Row Item Calibration - Box";
				testRunner.ClickByName(rowItemOne);

				testRunner.Wait(1);

				int queueCountBeforeAdd = QueueData.Instance.ItemCount;

				//Add Library Item to the Print Queue
				MatterControlUtilities.LibraryAddSelectionToQueue(testRunner);

				testRunner.Wait(2);

				//Make sure that the Queue Count increases by one
				int queueCountAfterAdd = QueueData.Instance.ItemCount;

				Assert.IsTrue(queueCountAfterAdd == queueCountBeforeAdd + 1);

				//Navigate to Queue
				testRunner.ClickByName("Queue Tab");

				testRunner.Wait(1);

				//Make sure that the Print Item was added
				string queueItem = "Queue Item Calibration - Box";
				bool queueItemWasAdded = testRunner.WaitForName(queueItem, 2);
				Assert.IsTrue(queueItemWasAdded == true);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test, Apartment(ApartmentState.STA)]
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

				testRunner.Wait(2);
				testRunner.Type(libraryItemToAdd);
				testRunner.Wait(2);
				testRunner.Type("{Enter}");

				testRunner.Wait(1);
				testRunner.ClickByName("Library Edit Button");
				testRunner.Wait(1);

				int queueCountBeforeAdd = QueueData.Instance.ItemCount;

				//Select both Library Items
				string rowItemOne = "Row Item Calibration - Box";
				testRunner.ClickByName(rowItemOne);

				string rowItemTwo = "Row Item Fennec Fox";
				testRunner.ClickByName(rowItemTwo);

				//Click the Add To Queue button
				testRunner.Wait(1);
				MatterControlUtilities.LibraryAddSelectionToQueue(testRunner);
				testRunner.Wait(2);

				//Make sure Queue Count increases by the correct amount
				int queueCountAfterAdd = QueueData.Instance.ItemCount;

				Assert.IsTrue(queueCountAfterAdd == queueCountBeforeAdd + 2);

				//Navigate to the Print Queue
				testRunner.ClickByName("Queue Tab");
				testRunner.Wait(1);

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

		[Test, Apartment(ApartmentState.STA)]
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
				testRunner.Wait(1);

				// Click Library Item View Button
				testRunner.ClickByName("Row Item Calibration - Box View Button");

				Assert.IsTrue(testRunner.WaitForName("Part Preview Window", 2), "Part Preview Window should be open after View button is clicked");
				testRunner.Wait(.2);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}


		[Test, Apartment(ApartmentState.STA)]
		public async Task PrintLibraryItem()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.WaitForName("Cancel Wizard Button", 1);

				using (var emulatorProcess = testRunner.LaunchAndConnectToPrinterEmulator())
				{
					// Navigate to Local Library
					testRunner.ClickByName("Library Tab");
					testRunner.NavigateToFolder("Local Library Row Item Collection");

					testRunner.ClickByName("Row Item Calibration - Box");

					int initialQueueCount = QueueData.Instance.ItemCount;

					// Click Library Item Print Button
					testRunner.ClickByName("Row Item Calibration - Box Print Button");
					testRunner.Wait(.5);

					Assert.AreEqual(initialQueueCount + 1, QueueData.Instance.ItemCount, "Queue count should increment by one after clicking 'Print'");
					Assert.AreEqual("Calibration - Box", QueueData.Instance.PrintItems[0].Name, "Library item should be inserted at queue index 0");
					Assert.AreEqual("Calibration - Box", QueueData.Instance.SelectedPrintItem.Name, "Library item should be the selected item");
					Assert.AreEqual("Calibration - Box", PrinterConnectionAndCommunication.Instance.ActivePrintItem.Name, "PrinterConnectionCommunication item should be the expected item");

					testRunner.ClickByName("Cancel Print Button");
				}

				testRunner.WaitForName("Start Print Button", 5);

				return Task.FromResult(0);

			};

			await MatterControlUtilities.RunTest(testToRun);
		}
	}
}
