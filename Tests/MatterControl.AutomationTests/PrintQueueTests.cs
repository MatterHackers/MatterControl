/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), Category("MatterControl.Automation"), RunInApplicationDomain]
	public class PrintQueueTests
	{
		[Test, Apartment(ApartmentState.STA), Category("FixNeeded" /* Not Finished */)]
		public async Task ClickOnBuyButton()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				//Make sure image does not exist before we click the buy button
				testRunner.MatchLimit = 500000;
				bool imageExists = testRunner.ImageExists("MatterHackersStoreImage.png");
				Assert.IsTrue(imageExists == false, "Web page is not open");

				//Click Buy button and test that the MatterHackers store web page is open
				testRunner.ClickByName("Buy Materials Button", 5);
				bool imageExists2 = testRunner.ImageExists("MatterHackersStoreImage.png", 10);
				Assert.IsTrue(imageExists2 == true, "Web page is open");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task ClickingCreateButtonOpensPluginWindow()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();
				// Tests that clicking the create button opens create tools plugin window
				testRunner.CloseSignInAndPrinterSelect();

				//Make sure that plugin window does not exist
				bool pluginWindowExists1 = testRunner.WaitForName("Plugin Chooser Window", 0);
				Assert.IsTrue(pluginWindowExists1 == false, "Plugin window does not exist");

				testRunner.ClickByName("Design Tool Button", 5);

				//Test that the plugin window does exist after the create button is clicked
				SystemWindow containingWindow;
				GuiWidget pluginWindowExists = testRunner.GetWidgetByName("Plugin Chooser Window", out containingWindow, secondsToWait: 3);
				Assert.IsTrue(pluginWindowExists != null, "Plugin Chooser Window");
				pluginWindowExists.CloseOnIdle();
				testRunner.Wait(.5);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task ClickOnExportButton()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				// Tests that clicking the queue export button with a single item selected opens export item window
				testRunner.CloseSignInAndPrinterSelect();

				//Make sure that the export window does not exist
				bool exportWindowExists1 = testRunner.WaitForName("Export Item Window", 0);
				Assert.IsTrue(exportWindowExists1 == false, "Export window does not exist");

				testRunner.ClickByName("Queue Export Button", 5);
				SystemWindow containingWindow;
				GuiWidget exportWindow = testRunner.GetWidgetByName("Export Item Window", out containingWindow, 5);
				Assert.IsTrue(exportWindow != null, "Export window does exist");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test, Apartment(ApartmentState.STA), Category("FixNeeded") /* Test now works as expected but product does not implement expected functionality */]
		public async Task QueueExportIsDisabledIfEmpty()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				SystemWindow systemWindow;

				testRunner.ClickByName("Queue... Menu", 2);

				var exportButton = testRunner.GetWidgetByName(" Export to Zip Menu Item", out systemWindow, 5);
				Assert.IsNotNull(exportButton, "Export button should exist");
				Assert.IsTrue(exportButton.Enabled, "Export button should be enabled");

				testRunner.ClickByName(" Remove All Menu Item", 2);

				testRunner.Wait(1);

				testRunner.ClickByName("Queue... Menu", 2);
				AutomationRunner.WaitUntil(() => !exportButton.Enabled, 4);
				Assert.IsFalse(exportButton.Enabled, "Export button should be disabled after Queue Menu -> Remove All");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test, Apartment(ApartmentState.STA)] 
		public async Task QueueThumbnailWidgetOpensPartPreview()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				// Tests that clicking a queue item thumbnail opens a Part Preview window
				Assert.IsFalse(testRunner.NameExists("Part Preview Window"), "Part Preview Window should not exist");

				testRunner.ClickByName("Queue Item Thumbnail");

				SystemWindow containingWindow;
				Assert.IsNotNull(testRunner.GetWidgetByName("Part Preview Window", out containingWindow, 3), "Part Preview Window Exists");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		/// <summary>
		/// Tests that Queue Copy button increases the queue count by one and that a new queue item appears with the expected name
		/// </summary>
		/// <returns></returns>
		[Test, Apartment(ApartmentState.STA)]
		public async Task CopyButtonMakesACopyOfPartInTheQueue()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				int expectedQueueCount = QueueData.Instance.ItemCount + 1;

				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("Queue Item Batman", 3);
				testRunner.Wait(.2);

				testRunner.ClickByName("Queue Copy Button", 3);
				AutomationRunner.WaitUntil(() => QueueData.Instance.ItemCount == expectedQueueCount, 3);

				Assert.AreEqual(expectedQueueCount, QueueData.Instance.ItemCount, "Copy button increases queue count by one");
				Assert.IsTrue(testRunner.WaitForName("Queue Item Batman - copy", 2), "Copied Batman item exists with expected name");
				testRunner.Wait(.3);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task AddSingleItemToQueue()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				/*
				 * Tests that Queue Add button adds a single part to queue:
				 * 1. The Queue count is increased by 1
				 * 2. A QueueRowItem is created and added to the queue
				 */

				int queueCountBeforeAdd = QueueData.Instance.ItemCount;

				//Click Add Button and Add Part To Queue
				testRunner.ClickByName("Queue Add Button", 2);
				testRunner.Wait(2);

				string queueItemPath = MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl");

				testRunner.Type(queueItemPath);
				testRunner.Wait(1);
				testRunner.Type("{Enter}");

				//Make sure single part is added and queue count increases by one
				bool fennecFoxPartWasAdded = testRunner.WaitForName("Queue Item Fennec_Fox", 2);
				Assert.IsTrue(fennecFoxPartWasAdded == true);

				int queueCountAfterAdd = QueueData.Instance.ItemCount;

				Assert.IsTrue(queueCountBeforeAdd + 1 == queueCountAfterAdd);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task AddMultipleItemsToQueue()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				/*
				 * Tests that Add button can add multiple files to the print queue:
				 * 1. The Queue count is increased by 2
				 * 2. 2 QueueRowItems are created and added to the queue
				 */

				int queueCountBeforeAdd = QueueData.Instance.ItemCount;

				//Click Add Button and Add Part To Queue
				testRunner.ClickByName("Queue Add Button", 2);
				string pathToFirstQueueItem = MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl");
				testRunner.Wait(1);
				string pathToSecondQueueItem = MatterControlUtilities.GetTestItemPath("Batman.stl");
				string textForBothQueueItems = string.Format("\"{0}\" \"{1}\"", pathToFirstQueueItem, pathToSecondQueueItem);

				testRunner.Type(textForBothQueueItems);
				testRunner.Wait(2);
				testRunner.Type("{Enter}");
				testRunner.Wait(2);

				//Confirm that both items were added and  that the queue count increases by the appropriate number
				int queueCountAfterAdd = QueueData.Instance.ItemCount;

				Assert.IsTrue(QueueData.Instance.ItemCount == queueCountBeforeAdd + 2);

				bool firstQueueItemWasAdded = testRunner.WaitForName("Queue Item Fennec_Fox", 2);
				bool secondQueueItemWasAdded = testRunner.WaitForName("Queue Item Batman", 2);

				Assert.IsTrue(firstQueueItemWasAdded == true);
				Assert.IsTrue(secondQueueItemWasAdded == true);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		/// <summary>
		/// Tests that
		/// 1. Target item exists
		/// 2. QueueData.Instance.Count is correctly decremented after remove
		/// 3. Target item does not exist after remove
		/// </summary>
		[Test, Apartment(ApartmentState.STA)]
		public async Task RemoveButtonRemovesSingleItem()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.Wait(1);

				int expectedQueueCount = QueueData.Instance.ItemCount - 1;

				// Assert exists
				Assert.IsTrue(testRunner.NameExists("Queue Item 2013-01-25_Mouthpiece_v2"), "Target item should exist before Remove");

				// Remove target item
				testRunner.ClickByName("Queue Remove Button", 2);
				testRunner.Wait(1);

				// Assert removed
				Assert.AreEqual(expectedQueueCount, QueueData.Instance.ItemCount, "After Remove button click, Queue count should be 1 less");
				Assert.IsFalse(testRunner.WaitForName("Queue Item 2013-01-25_Mouthpiece_v2", 1), "Target item should not exist after Remove");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task RemoveLastItemInListChangesSelection()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.Wait(1);

				int expectedQueueCount = QueueData.Instance.ItemCount - 1;

				Assert.AreEqual(QueueData.Instance.SelectedIndex, 0);

				testRunner.ClickByName("Queue Item MatterControl - Coin", 2);

				Assert.AreEqual(QueueData.Instance.SelectedIndex, 3);

				// Remove target item
				testRunner.ClickByName("Queue Remove Button", 2);
				testRunner.Wait(.5);

				// after remove we select the next up the list
				Assert.AreEqual(QueueData.Instance.SelectedIndex, 0);

				// Assert removed
				Assert.AreEqual(expectedQueueCount, QueueData.Instance.ItemCount, "After Remove button click, Queue count should be 1 less");
				Assert.IsFalse(testRunner.WaitForName("Queue Item MatterControl - Coin", .5), "Target item should not exist after Remove");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task EditButtonTurnsOnOffEditMode()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();
				/*
				 *Tests that when the edit button is clicked we go into editmode (print queue items have checkboxes on them)  
				 *1. After Edit button is clicked print queue items have check boxes
				 *2. Selecting multiple queue itema and then clicking the Remove button removes the item 
				 *3. Selecting multiple queue items and then clicking the Remove button decreases the queue tab count by one
				 */

				bool checkboxExists = testRunner.WaitForName("Queue Item Checkbox", 2);

				Assert.IsTrue(checkboxExists == false);

				Assert.IsTrue(QueueData.Instance.ItemCount == 4);

				SystemWindow systemWindow;
				string itemName = "Queue Item 2013-01-25_Mouthpiece_v2";

				GuiWidget queueItem = testRunner.GetWidgetByName(itemName, out systemWindow, 3);
				SearchRegion queueItemRegion = testRunner.GetRegionByName(itemName, 3);

				{
					testRunner.ClickByName("Queue Edit Button", 2);

					SystemWindow containingWindow;
					GuiWidget foundWidget = testRunner.GetWidgetByName("Queue Item Checkbox", out containingWindow, 3, searchRegion: queueItemRegion);
					Assert.IsTrue(foundWidget != null, "We should have an actual checkbox");
				}

				{
					testRunner.ClickByName("Queue Done Button", 2);

					testRunner.Wait(.5);

					SystemWindow containingWindow;
					GuiWidget foundWidget = testRunner.GetWidgetByName("Queue Item Checkbox", out containingWindow, 1, searchRegion: queueItemRegion);
					Assert.IsTrue(foundWidget == null, "We should not have an actual checkbox");
				}

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items, overrideWidth: 600);
		}

		/// <summary>
		/// Tests that:
		/// 1. When in Edit mode, checkboxes appear
		/// 2. When not in Edit mode, no checkboxes appear
		/// </summary>
		[Test, Apartment(ApartmentState.STA)]
		public async Task DoneButtonTurnsOffEditMode()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				SystemWindow systemWindow;

				testRunner.CloseSignInAndPrinterSelect();

				SearchRegion searchRegion = testRunner.GetRegionByName("Queue Item 2013-01-25_Mouthpiece_v2", 3);

				// Enter Edit mode and confirm checkboxes exist
				testRunner.ClickByName("Queue Edit Button", 2);
				testRunner.Wait(.3);
				Assert.IsNotNull(
					testRunner.GetWidgetByName("Queue Item Checkbox", out systemWindow, 3, searchRegion), 
					"While in Edit mode, checkboxes should exist on queue items");

				// Exit Edit mode and confirm checkboxes are missing
				testRunner.ClickByName("Queue Done Button", 1);
				testRunner.Wait(.3);
				Assert.IsNull(
					testRunner.GetWidgetByName("Queue Item Checkbox", out systemWindow, 1, searchRegion), 
					"After exiting Edit mode, checkboxes should not exist on queue items");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task RemoveButtonRemovesMultipleItems()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();
				/*
				 *Tests that when one item is selected  
				 *1. Queue Item count equals three before the test starts 
				 *2. Selecting multiple queue items and then clicking the Remove button removes the item 
				 *3. Selecting multiple queue items and then clicking the Remove button decreases the queue tab count by one
				 */

				int queueItemCount = QueueData.Instance.ItemCount;

				testRunner.Wait(2);

				testRunner.ClickByName("Queue Edit Button", 2);

				testRunner.ClickByName("Queue Item Batman", 2);

				testRunner.ClickByName("Queue Remove Button", 2);

				testRunner.Wait(1);

				int queueItemCountAfterRemove = QueueData.Instance.ItemCount;

				Assert.IsTrue(queueItemCount - 2 == queueItemCountAfterRemove);

				bool queueItemExists = testRunner.WaitForName("Queue Item Batman", 2);
				bool secondQueueItemExists = testRunner.WaitForName("Queue Item 2013-01-25_Mouthpiece_v2", 2);

				Assert.IsTrue(queueItemExists == false);
				Assert.IsTrue(secondQueueItemExists == false);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task QueueSelectCheckBoxWorks()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();
				/*
				 *Tests that when one item is selected  
				 *1. Queue Item count equals three before the test starts 
				 *2. Selecting multiple queue items and then clicking the Remove button removes the item 
				 *3. Selecting multiple queue items and then clicking the Remove button decreases the queue tab count by one
				 */

				int queueItemCount = QueueData.Instance.ItemCount;

				bool queueItemExists = testRunner.WaitForName("Queue Item Batman", 2);
				bool secondQueueItemExists = testRunner.WaitForName("Queue Item 2013-01-25_Mouthpiece_v2", 2);

				SystemWindow systemWindow;
				GuiWidget rowItem = testRunner.GetWidgetByName("Queue Item Batman", out systemWindow, 3);

				SearchRegion rowItemRegion = testRunner.GetRegionByName("Queue Item Batman", 3);

				testRunner.ClickByName("Queue Edit Button", 3);

				GuiWidget foundWidget = testRunner.GetWidgetByName("Queue Item Checkbox", out systemWindow, 3, searchRegion: rowItemRegion);
				CheckBox checkBoxWidget = foundWidget as CheckBox;
				Assert.IsTrue(checkBoxWidget != null, "We should have an actual checkbox");
				Assert.IsTrue(checkBoxWidget.Checked == false, "currently not checked");

				testRunner.ClickByName("Queue Item Batman", 3);
				Assert.IsTrue(checkBoxWidget.Checked == true, "currently checked");

				testRunner.ClickByName("Queue Item Checkbox", 3, searchRegion: rowItemRegion);
				Assert.IsTrue(checkBoxWidget.Checked == false, "currently not checked");


				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		/// <summary>
		/// Confirms the Export to Zip feature compresses and exports to a zip file and that file imports without issue
		/// </summary>
		/// <returns></returns>
		[Test, Apartment(ApartmentState.STA)]
		public async Task ExportToZipImportFromZip()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				// Ensure output file does not exist
				string exportZipPath = MatterControlUtilities.GetTestItemPath("TestExportZip.zip");
				if (File.Exists(exportZipPath))
				{
					File.Delete(exportZipPath);
				}

				testRunner.CloseSignInAndPrinterSelect();

				Assert.AreEqual(4, QueueData.Instance.ItemCount, "Queue should initially have 4 items");

				// Invoke Queue -> Export to Zip dialog
				testRunner.ClickByName("Queue... Menu", 2);
				testRunner.Wait(.2);
				testRunner.ClickByName(" Export to Zip Menu Item", 2);
				testRunner.Wait(2);
				testRunner.Type(exportZipPath);
				testRunner.Wait(2);
				testRunner.Type("{Enter}");

				AutomationRunner.WaitUntil(() => File.Exists(exportZipPath), 3);
				Assert.IsTrue(File.Exists(exportZipPath), "Queue was exported to zip file, file exists on disk at expected path");

				// Import the exported zip file and confirm the Queue Count increases by 3 
				testRunner.ClickByName("Queue Add Button");
				testRunner.Wait(1);
				testRunner.Type(exportZipPath);
				testRunner.Wait(1);
				testRunner.Type("{Enter}");

				AutomationRunner.WaitUntil(() => QueueData.Instance.ItemCount == 8, 5);
				Assert.AreEqual(8, QueueData.Instance.ItemCount, "All parts imported successfully from exported zip");

				testRunner.Wait(.3);

				try
				{
					if (File.Exists(exportZipPath))
					{
						File.Delete(exportZipPath);
					}
				}
				catch { }

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task SendMenuClickedWithoutCloudPlugins()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				SystemWindow parentWindow;

				testRunner.CloseSignInAndPrinterSelect();

				Assert.IsTrue(QueueData.Instance.ItemCount > 0, "Queue is not empty at test startup");

				testRunner.ClickByName("More...  Menu", 2);
				testRunner.Wait(.2);

				testRunner.ClickByName("Send Menu Item", 2);
				testRunner.Wait(.2);

				// WaitFor Ok button and ensure parent window has expected title and named button
				testRunner.WaitForName("Ok Button", 2);
				var widget = testRunner.GetWidgetByName("Ok Button", out parentWindow);
				Assert.IsTrue(widget != null 
					&& parentWindow.Title == "MatterControl - Alert", "Send Disabled warning appears when no plugins exists to satisfy behavior");

				testRunner.Wait(.2);

				// Close dialog before exiting
				testRunner.ClickByName("Ok Button");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		/// <summary>
		/// Tests that when the Remove All menu item is clicked 
		///   1. Queue Item count is set to one
		///   2. All widgets that were previously in the queue are removed
		/// </summary>
		[Test, Apartment(ApartmentState.STA)]
		public async Task RemoveAllMenuItemClickedRemovesAll()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();
				Assert.AreEqual(4, QueueData.Instance.ItemCount, "Queue has expected 4 items, including default Coin");

				// Assert that widgets exists
				Assert.IsTrue(testRunner.WaitForName("Queue Item Batman"), "Batman part exists");
				Assert.IsTrue(testRunner.WaitForName("Queue Item Fennec_Fox"), "Fox part exists");
				Assert.IsTrue(testRunner.WaitForName("Queue Item 2013-01-25_Mouthpiece_v2"), "Mouthpiece part exists");

				// Act - remove all print queue items
				testRunner.RemoveAllFromQueue();

				AutomationRunner.WaitUntil(() => QueueData.Instance.ItemCount == 0, 5);

				// Assert that object model has been cleared
				Assert.AreEqual(0, QueueData.Instance.ItemCount, "Queue is empty after RemoveAll action");

				// Assert that widgets have been removed
				testRunner.Wait(.5);

				Assert.IsFalse(testRunner.NameExists("Queue Item Batman"), "Batman part removed");
				Assert.IsFalse(testRunner.NameExists("Queue Item Fennec_Fox"), "Fox part removed");
				Assert.IsFalse(testRunner.NameExists("Queue Item 2013-01-25_Mouthpiece_v2"), "Mouthpiece part removed");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test, Apartment(ApartmentState.STA), Category("FixNeeded" /* Not Finished */)]
		public async Task ClickCreatePartSheetButton()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				bool queueEmpty = true;
				int queueItemCount = QueueData.Instance.ItemCount;

				if (queueItemCount == 3)
				{
					queueEmpty = false;
				}

				Assert.IsTrue(queueEmpty == false);
				testRunner.ClickByName("Queue... Menu", 2);
				testRunner.Wait(.2);
				testRunner.ClickByName(" Create Part Sheet Menu Item", 2);
				testRunner.Wait(2);

				string pathToSavePartSheet = MatterControlUtilities.GetTestItemPath("CreatePartSheet");
				string validatePartSheetPath = Path.Combine("..", "..", "..", "TestData", "QueueItems", "CreatePartSheet.pdf");

				testRunner.Type(pathToSavePartSheet);
				testRunner.Wait(1);
				testRunner.Type("{Enter}");
				testRunner.Wait(1);
				testRunner.Wait(5);

				bool partSheetCreated = File.Exists(validatePartSheetPath);

				testRunner.Wait(2);
				Assert.IsTrue(partSheetCreated == true);

				if (File.Exists(validatePartSheetPath))
				{
					File.Delete(validatePartSheetPath);
				}

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		/// <summary>
		/// *Tests:
		/// *1. When the remove button on a queue item is clicked the queue tab count decreases by one 
		/// *2. When the remove button on a queue item is clicked the item is removed
		/// *3. When the View button on a queue item is clicked the part preview window is opened
		/// </summary>
		/// <returns></returns>
		[Test, Apartment(ApartmentState.STA)]
		public async Task ClickQueueRowItemViewAndRemove()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.Wait(2);

				Assert.AreEqual(4, QueueData.Instance.ItemCount, "Queue should initially have four items");
				Assert.IsTrue(testRunner.WaitForName("Queue Item Batman", 1));
				Assert.IsTrue(testRunner.WaitForName("Queue Item 2013-01-25_Mouthpiece_v2", 1));

				testRunner.ClickByName("Queue Item Batman", 1);
				testRunner.ClickByName("Queue Item Batman Remove");
				testRunner.Wait(2);

				Assert.AreEqual(3, QueueData.Instance.ItemCount, "Batman item removed");
				Assert.IsFalse(testRunner.NameExists("Queue Item Batman"), "Batman item removed");

				Assert.IsFalse(testRunner.NameExists("Queue Item 2013-01-25_Mouthpiece_v2 Part Preview"), "Mouthpiece Part Preview should not initially be visible");
				testRunner.ClickByName("Queue Item 2013-01-25_Mouthpiece_v2", 1);
				testRunner.Wait(2);
				testRunner.ClickByName("Queue Item 2013-01-25_Mouthpiece_v2 View", 1);

				Assert.IsTrue(testRunner.WaitForName("Queue Item 2013-01-25_Mouthpiece_v2 Part Preview", 2), "The Mouthpiece Part Preview should appear after the view button is clicked");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items, overrideWidth: 600);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task QueueAddButtonAddsAMF()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				int initialQueueCount = QueueData.Instance.ItemCount;

				// Click Add button 
				testRunner.ClickByName("Queue Add Button");
				testRunner.Wait(1);

				testRunner.Type(MatterControlUtilities.GetTestItemPath("Rook.amf"));
				testRunner.Wait(1);
				testRunner.Type("{Enter}");

				// Widget should exist
				Assert.IsTrue(testRunner.WaitForName("Queue Item Rook", 5), "Widget for added item should exist in control tree");

				// Queue count should increases by one 
				Assert.AreEqual(initialQueueCount + 1, QueueData.Instance.ItemCount, "After adding item, queue count should increase by one");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task QueueAddButtonAddsSTL()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				int initialQueueCount = QueueData.Instance.ItemCount;

				// Click Add button 
				testRunner.ClickByName("Queue Add Button");
				testRunner.Wait(1);

				testRunner.Type(MatterControlUtilities.GetTestItemPath("Batman.stl"));
				testRunner.Wait(1);
				testRunner.Type("{Enter}");

				// Widget should exist
				Assert.IsTrue(testRunner.WaitForName("Queue Item Batman", 5), "Widget for added item should exist in control tree");

				// Queue count should increases by one 
				Assert.AreEqual(initialQueueCount + 1, QueueData.Instance.ItemCount, "After adding item, queue count should increase by one");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task QueueAddButtonAddsGcodeFile()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				int initialQueueCount = QueueData.Instance.ItemCount;

				// Click Add button 
				testRunner.ClickByName("Queue Add Button");
				testRunner.Wait(1);

				testRunner.Type(MatterControlUtilities.GetTestItemPath("chichen-itza_pyramid.gcode"));
				testRunner.Wait(1);
				testRunner.Type("{Enter}");

				// Widget should exist
				Assert.IsTrue(testRunner.WaitForName("Queue Item chichen-itza_pyramid", 5), "Widget for added item should exist in control tree");

				// Queue count should increases by one 
				Assert.AreEqual(initialQueueCount + 1, QueueData.Instance.ItemCount, "After adding item, queue count should increase by one");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun);
		}
	}
}
