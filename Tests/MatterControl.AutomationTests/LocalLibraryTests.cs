using System;
using System.Threading;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class AddSingleItemToLocalLibrary
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void LocalLibraryAddButtonAddSingleItemToLibrary()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);

				string itemName = "Row Item " + "Fennec Fox";

				//Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");

				//Make sure that Item does not exist before the test begins
				bool rowItemExists = testRunner.WaitForName(itemName, 1);
				testRunner.AddTestResult(rowItemExists == false);

				//Click Local Library Add Button
				testRunner.ClickByName("Library Add Button");

				//Get Library Item to Add
				string rowItemPath = MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl");
				testRunner.Wait(2);
				testRunner.Type(rowItemPath);
				testRunner.Wait(1);
				testRunner.Type("{Enter}");

				bool rowItemWasAdded = testRunner.WaitForName(itemName, 2);
				testRunner.AddTestResult(rowItemWasAdded == true);

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class AddMultipleItemsToLocalLibrary
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void LocalLibraryAddButtonAddsMultipleItemsToLibrary()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);
				//Names of Items to be added
				string firstItemName = "Row Item " + "Fennec Fox";
				string secondItemName = "Row Item " + "Batman";

				//Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");

				//Make sure both Items do not exist before the test begins
				bool firstItemExists = testRunner.WaitForName(firstItemName, 1);
				bool secondItemExists = testRunner.WaitForName(secondItemName, 1);
				testRunner.AddTestResult(firstItemExists == false);
				testRunner.AddTestResult(secondItemExists == false);

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
				testRunner.AddTestResult(firstRowItemWasAdded == true);
				testRunner.AddTestResult(secondRowItemWasAdded == true);

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(4));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class AddAMFItemToLocalLibrary
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void LocalLibraryAddButtonAddAMFToLibrary()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);

				string itemName = "Row Item " + "Rook";

				//Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");

				//Make sure that Item does not exist before the test begins
				bool rowItemExists = testRunner.WaitForName(itemName, 1);
				testRunner.AddTestResult(rowItemExists == false);

				//Click Local Library Add Button
				testRunner.ClickByName("Library Add Button");

				//Get Library Item to Add
				string rowItemPath = MatterControlUtilities.GetTestItemPath("Rook.amf");
				testRunner.Wait(2);
				testRunner.Type(rowItemPath);
				testRunner.Wait(1);
				testRunner.Type("{Enter}");

				bool rowItemWasAdded = testRunner.WaitForName(itemName, 2);
				testRunner.AddTestResult(rowItemWasAdded == true);

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, overrideWidth: 1024, overrideHeight: 800, defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class AddZipFileToLocalLibrary
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void LocalLibraryAddButtonAddZipToLibrary()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);

				//Items in Batman.zip
				string firstItemName = "Row Item " + "Batman";
				string secondItemName = "Row Item " + "2013-01-25 Mouthpiece v2";

				//Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");

				//Make sure that Item does not exist before the test begins
				bool firstItemInZipExists = testRunner.WaitForName(firstItemName, 1);
				bool secondItemInZipExists = testRunner.WaitForName(secondItemName, 1);
				testRunner.AddTestResult(firstItemInZipExists == false);
				testRunner.AddTestResult(firstItemInZipExists == false);

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
				testRunner.AddTestResult(firstItemInZipWasAdded == true);
				testRunner.AddTestResult(secondItemInZipWasAdded == true);

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(4));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class RenameButtonRenamesLibraryRowItem
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void RenameButtonRenameLocalLibraryItem()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);

				//Navigate To Local Library 
				testRunner.ClickByName("Library Tab");
				MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");

				testRunner.Wait(1);

				string rowItemToRename = "Row Item " + "Calibration - Box";
				testRunner.ClickByName("Library Edit Button");
				testRunner.Wait(1);
				testRunner.ClickByName(rowItemToRename);
				MatterControlUtilities.LibraryRenameSelectedItem(testRunner);

				testRunner.Wait(2);

				testRunner.Type("Library Item Renamed");

				testRunner.ClickByName("Rename Button");

				string renamedRowItem = "Row Item " + "Library Item Renamed";
				bool libraryItemWasRenamed = testRunner.WaitForName(renamedRowItem, 2);
				bool libraryItemBeforeRenameExists = testRunner.WaitForName(rowItemToRename, 2);

				testRunner.AddTestResult(libraryItemWasRenamed == true);
				testRunner.AddTestResult(libraryItemBeforeRenameExists == false);

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, overrideWidth: 600, defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class UserCanSuccessfullyCreateAndRenameLibraryFolder
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void RenameButtonRenameLocalLibraryFolder()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);
				//Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");

				//Create New Folder
				testRunner.ClickByName("Create Folder From Library Button");
				testRunner.Wait(.5);
				testRunner.Type("New Folder");
				testRunner.Wait(.5);
				testRunner.ClickByName("Create Folder Button");

				//Check for Created Folder
				string newLibraryFolder = "New Folder Row Item Collection";
				bool newFolderWasCreated = testRunner.WaitForName(newLibraryFolder, 1);
				testRunner.AddTestResult(newFolderWasCreated == true);

				testRunner.ClickByName("Library Edit Button");
				testRunner.ClickByName("New Folder Row Item Collection");
				MatterControlUtilities.LibraryRenameSelectedItem(testRunner);
				testRunner.Wait(.5);
				testRunner.Type("Renamed Library Folder");
				testRunner.ClickByName("Rename Button");

				//Make sure that renamed Library Folder Exists
				bool renamedLibraryFolderExists = testRunner.WaitForName("Renamed Library Folder Row Item Collection", 2);
				testRunner.AddTestResult(renamedLibraryFolderExists == true);

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class LibraryEditButtonOpensUpPartPreviewWindow
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void ClickLibraryEditButtonOpensPartPreviewWindow()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);
				//Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");

				testRunner.Wait(1);

				string rowItem = "Row Item " + "Calibration - Box";
				testRunner.ClickByName("Library Edit Button");
				testRunner.Wait(1);
				testRunner.ClickByName(rowItem);

				MatterControlUtilities.LibraryEditSelectedItem(testRunner);

				//Make sure that Export Item Window exists after Export button is clicked
				bool exportItemWindowExists = testRunner.WaitForName("Part Preview Window", 2);
				testRunner.AddTestResult(exportItemWindowExists == true);

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(1));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class OneLibraryItemSelectedRemoveButtonRemovesItem
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void RemoveButtonClickedRemovesSingleItem()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);
				//Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");

				testRunner.Wait(1);

				string rowItem = "Row Item " + "Calibration - Box";
				testRunner.ClickByName("Library Edit Button");
				testRunner.Wait(1);
				testRunner.ClickByName(rowItem);

				MatterControlUtilities.LibraryRemoveSelectedItem(testRunner);

				testRunner.Wait(1);

				//Make sure that Export Item Window exists after Export button is clicked
				bool rowItemExists = testRunner.WaitForName(rowItem, 1);
				testRunner.AddTestResult(rowItemExists == false);

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(1));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class MultipleLibraryItemsSelectedRemoveButtonRemovesItem
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void RemoveButtonClickedRemovesMultipleItems()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);
				//Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");

				testRunner.Wait(1);
				testRunner.ClickByName("Library Edit Button");
				testRunner.Wait(1);

				string rowItemPath = MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl");
				testRunner.ClickByName("Library Add Button");

				testRunner.Wait(2);
				testRunner.Type(rowItemPath);
				testRunner.Type("{Enter}");

				testRunner.Wait(1);
				string rowItemOne = "Row Item " + "Calibration - Box";
				testRunner.ClickByName(rowItemOne, 1);

				string rowItemTwo = "Row Item " + "Fennec Fox";
				testRunner.ClickByName(rowItemTwo, 1);

				testRunner.Wait(1);

				//Make sure row items exist before remove
				bool rowItemOneExistsBeforeRemove = testRunner.WaitForName(rowItemOne, 2);
				bool rowItemTwoExistsBeforeRemove = testRunner.WaitForName(rowItemTwo, 2);
				testRunner.AddTestResult(rowItemOneExistsBeforeRemove == true);
				testRunner.AddTestResult(rowItemTwoExistsBeforeRemove == true);

				MatterControlUtilities.LibraryRemoveSelectedItem(testRunner);
				testRunner.Wait(1);

				//Make sure both selected items are removed
				bool rowItemOneExists = testRunner.WaitForName(rowItemOne, 2);
				bool rowItemTwoExists = testRunner.WaitForName(rowItemTwo, 2);
				testRunner.AddTestResult(rowItemOneExists == false);
				testRunner.AddTestResult(rowItemTwoExists == false);

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(4));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class AddToQueueButtonAddsSingleItemToQueue
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void AddToQueueFromLibraryButtonAddsItemToQueue()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);

				//Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");

				testRunner.Wait(1);
				testRunner.ClickByName("Library Edit Button");
				testRunner.Wait(1);

				//Select Library Item
				string rowItemOne = "Row Item " + "Calibration - Box";
				testRunner.ClickByName(rowItemOne);

				testRunner.Wait(1);

				int queueCountBeforeAdd = QueueData.Instance.Count;

				//Add Library Item to the Print Queue
				MatterControlUtilities.LibraryAddSelectionToQueue(testRunner);

				testRunner.Wait(2);

				//Make sure that the Queue Count increases by one
				int queueCountAfterAdd = QueueData.Instance.Count;

				testRunner.AddTestResult(queueCountAfterAdd == queueCountBeforeAdd + 1);

				//Navigate to Queue
				testRunner.ClickByName("Queue Tab");

				testRunner.Wait(1);

				//Make sure that the Print Item was added
				string queueItem = "Queue Item " + "Calibration - Box";
				bool queueItemWasAdded = testRunner.WaitForName(queueItem, 2);
				testRunner.AddTestResult(queueItemWasAdded == true);

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class AddToQueueButtonAddsMultipleItemsToQueue
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void AddToQueueFromLibraryButtonAddsItemsToQueue()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);

				//Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");

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

				int queueCountBeforeAdd = QueueData.Instance.Count;

				//Select both Library Items
				string rowItemOne = "Row Item " + "Calibration - Box";
				testRunner.ClickByName(rowItemOne);

				string rowItemTwo = "Row Item " + "Fennec Fox";
				testRunner.ClickByName(rowItemTwo);

				//Click the Add To Queue button
				testRunner.Wait(1);
				MatterControlUtilities.LibraryAddSelectionToQueue(testRunner);
				testRunner.Wait(2);

				//Make sure Queue Count increases by the correct amount
				int queueCountAfterAdd = QueueData.Instance.Count;

				testRunner.AddTestResult(queueCountAfterAdd == queueCountBeforeAdd + 2);

				//Navigate to the Print Queue
				testRunner.ClickByName("Queue Tab");
				testRunner.Wait(1);

				//Test that both added print items exist
				string queueItemOne = "Queue Item " + "Calibration - Box";
				string queueItemTwo = "Queue Item " + "Fennec_Fox";
				bool queueItemOneWasAdded = testRunner.WaitForName(queueItemOne, 2);
				bool queueItemTwoWasAdded = testRunner.WaitForName(queueItemTwo, 2);

				testRunner.AddTestResult(queueItemOneWasAdded == true);
				testRunner.AddTestResult(queueItemTwoWasAdded == true);

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(3));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class ClickLibraryTumbnailWidgetOpensPartPreview
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void LibraryItemThumbnailClickedOpensPartPreview()
		{
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				MatterControlUtilities.PrepForTestRun(testRunner);

				//Navigate to Local Library
				testRunner.ClickByName("Library Tab");
				MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");

				//Make sure Part Preview Window does not exists before we click the view button
				bool partPreviewExistsOne = testRunner.WaitForName("Part Preview Window", 1);
				testRunner.AddTestResult(partPreviewExistsOne == false);

				string libraryRowItemName = "Row Item " + "Calibration - Box";
				testRunner.ClickByName(libraryRowItemName);

				testRunner.Wait(1);

				//Click Library Item View Button
				string libraryItemViewButton = "Row Item " + "Calibration - Box" + " View Button";
				testRunner.ClickByName(libraryItemViewButton);

				//Make sure that Part Preview Window opens after View button is clicked
				bool partPreviewWindowExists = testRunner.WaitForName("Part Preview Window", 1.5);
				testRunner.AddTestResult(partPreviewWindowExists == true);

				MatterControlUtilities.CloseMatterControl(testRunner);
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, defaultTestImages: MatterControlUtilities.DefaultTestImages);
			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}
}
