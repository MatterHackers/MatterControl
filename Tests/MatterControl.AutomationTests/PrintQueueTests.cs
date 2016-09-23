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
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), Category("MatterControl.Automation"), RunInApplicationDomain]
	public class BuyButtonTests
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain, Ignore("Not Finished")]
		public void ClickOnBuyButton()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					//Make sure image does not exist before we click the buy button
					testRunner.MatchLimit = 500000;
					bool imageExists = testRunner.ImageExists("MatterHackersStoreImage.png");
					resultsHarness.AddTestResult(imageExists == false, "Web page is not open");

					//Click Buy button and test that the MatterHackers store web page is open
					testRunner.ClickByName("Buy Materials Button", 5);
					bool imageExists2 = testRunner.ImageExists("MatterHackersStoreImage.png", 10);
					resultsHarness.AddTestResult(imageExists2 == true, "Web page is open");

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);

			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class ClickingCreateButtonOpensPluginWindow
    {
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		//Test Works
		public void ClickCreateButton()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);
					// Tests that clicking the create button opens create tools plugin window
					MatterControlUtilities.PrepForTestRun(testRunner);

					//Make sure that plugin window does not exist
					bool pluginWindowExists1 = testRunner.WaitForName("Plugin Chooser Window", 0);
					resultsHarness.AddTestResult(pluginWindowExists1 == false, "Plugin window does not exist");

					testRunner.ClickByName("Design Tool Button", 5);

					//Test that the plugin window does exist after the create button is clicked
					SystemWindow containingWindow;
					GuiWidget pluginWindowExists = testRunner.GetWidgetByName("Plugin Chooser Window", out containingWindow, secondsToWait: 3);
					resultsHarness.AddTestResult(pluginWindowExists != null, "Plugin Chooser Window");
					pluginWindowExists.CloseOnIdle();
					testRunner.Wait(.5);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class ExportButtonTest
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		//Test Works
		public void ClickOnExportButton()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					// Tests that clicking the queue export button with a single item selected opens export item window
					MatterControlUtilities.PrepForTestRun(testRunner);

					//Make sure that the export window does not exist
					bool exportWindowExists1 = testRunner.WaitForName( "Export Item Window", 0);
					resultsHarness.AddTestResult(exportWindowExists1 == false, "Export window does not exist");

					testRunner.ClickByName("Queue Export Button", 5);
					SystemWindow containingWindow;
					GuiWidget exportWindow = testRunner.GetWidgetByName("Export Item Window", out containingWindow, 5);
					resultsHarness.AddTestResult(exportWindow != null, "Export window does exist");
					if (exportWindow != null)
					{
						exportWindow.CloseOnIdle();
						testRunner.Wait(.5);
					}

					MatterControlUtilities.CloseMatterControl(testRunner);

				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Ignore("Not Finished")]
	public class ExportButtonDisabledNoQueueItems
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void ExportButtonIsDisabledWithNoItemsInQueue()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					//bool exportButtonExists = testRunner.NameExists("Export Queue Button");
					bool exportButtonExists = testRunner.WaitForName("Export Queue Button", 10);
					testRunner.Wait(5);
					resultsHarness.AddTestResult(exportButtonExists == false, "Export button is disabled");

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);
			Assert.IsTrue(testHarness.AllTestsPassed(1));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class QueueItemThumnailWidget
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain] 
		public void QueueThumbnailWidgetOpensPartPreview()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);
					
					// Tests that clicking a queue item thumbnail opens a Part Preview window

					bool partPreviewWindowExists1 = testRunner.WaitForName("Part Preview Window Thumbnail", 0);
					resultsHarness.AddTestResult(partPreviewWindowExists1 == false, "Part Preview Window Does Not Exist");

					testRunner.ClickByName("Queue Item Thumbnail");

					SystemWindow containingWindow;
					GuiWidget partPreviewWindowExists = testRunner.GetWidgetByName("Part Preview Window", out containingWindow, 3);
					resultsHarness.AddTestResult(partPreviewWindowExists != null, "Part Preview Window Exists");
					partPreviewWindowExists.CloseOnIdle();
					testRunner.Wait(.5);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class ClickCopyButtonMakesACopyOfPrintItemInQueue
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void CopyButtonMakesACopyOfPartInTheQueue()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);
					/* Tests that when the Queue Copy button is clicked:
					 * 1. The Queue Tab Count is increased by one
					 * 2. A Queue Row item is created and added to the queue with the correct name
					 */

					int queueCountBeforeCopyButtonIsClicked = QueueData.Instance.Count;
					bool copyIncreasesQueueDataCount = false;
					testRunner.ClickByName("Queue Item " + "Batman", 3);
					testRunner.ClickByName("Queue Copy Button", 3);

					testRunner.Wait(1);

					int currentQueueCount = QueueData.Instance.Count;
					if (currentQueueCount == queueCountBeforeCopyButtonIsClicked + 1)
					{
						copyIncreasesQueueDataCount = true;
					}

					resultsHarness.AddTestResult(copyIncreasesQueueDataCount == true, "Copy button clicked increases queue tab count by one");

					bool batmanQueueItemCopyExists = testRunner.WaitForName("Queue Item " + "Batman" + " - copy", 2);

					resultsHarness.AddTestResult(batmanQueueItemCopyExists == true);
					
					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class AddSingleItemToQueueAddsItem
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void AddSingleItemToQueue()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					/*
					 * Tests that Queue Add button adds a single part to queue:
					 * 1. The Queue count is increased by 1
					 * 2. A QueueRowItem is created and added to the queue
					 */

					int queueCountBeforeAdd = QueueData.Instance.Count;

					//Click Add Button and Add Part To Queue
					testRunner.ClickByName("Queue Add Button", 2);
					testRunner.Wait(2);

					string queueItemPath = MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl");

					testRunner.Type(queueItemPath);
					testRunner.Wait(1);
					testRunner.Type("{Enter}");


					//Make sure single part is added and queue count increases by one
					bool fennecFoxPartWasAdded = testRunner.WaitForName("Queue Item " + "Fennec_Fox", 2);
					resultsHarness.AddTestResult(fennecFoxPartWasAdded == true);

					int queueCountAfterAdd = QueueData.Instance.Count;

					resultsHarness.AddTestResult(queueCountBeforeAdd +1 == queueCountAfterAdd);

					MatterControlUtilities.CloseMatterControl(testRunner);

				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);
			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class AddButtonAddsMuiltipleItemsToQueue
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void AddMuiltipleItemsToQueue()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					/*
					 * Tests that Add button can add multiple files to the print queue:
					 * 1. The Queue count is increased by 2
					 * 2. 2 QueueRowItems are created and added to the queue
					 */

					int queueCountBeforeAdd = QueueData.Instance.Count;

					//Click Add Button and Add Part To Queue
					testRunner.ClickByName("Queue Add Button", 2);
					string pathToFirstQueueItem = MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl");
					testRunner.Wait(1);
					string pathToSecondQueueItem = MatterControlUtilities.GetTestItemPath("Batman.stl");
					string textForBothQueueItems = String.Format("\"{0}\" \"{1}\"", pathToFirstQueueItem, pathToSecondQueueItem);

					testRunner.Type(textForBothQueueItems);
					testRunner.Wait(2);
					testRunner.Type("{Enter}");
					testRunner.Wait(2);

					//Confirm that both items were added and  that the queue count increases by the appropriate number
					int queueCountAfterAdd = QueueData.Instance.Count;

					resultsHarness.AddTestResult(QueueData.Instance.Count == queueCountBeforeAdd + 2);

					bool firstQueueItemWasAdded = testRunner.WaitForName("Queue Item " + "Fennec_Fox", 2);
					bool secondQueueItemWasAdded = testRunner.WaitForName("Queue Item " + "Batman", 2);

					resultsHarness.AddTestResult(firstQueueItemWasAdded == true);
					resultsHarness.AddTestResult(secondQueueItemWasAdded == true);

					MatterControlUtilities.CloseMatterControl(testRunner);

				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);
			Assert.IsTrue(testHarness.AllTestsPassed(3));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class RemoveButtonClickedRemovesSingleItem
	{

		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void RemoveButtonRemovesSingleItem()
		{
			//Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);
					/*
					 *Tests that when one item is selected  
					 *1. Queue Item count equals three before the test starts 
					 *2. Selecting single queue item and then clicking the Remove button removes the item 
					 *3. Selecting single queue items and then clicking the Remove button decreases the queue tab count by one
					 */

					int queueItemCount = QueueData.Instance.Count;

					testRunner.ClickByName("Queue Remove Button", 2);

					testRunner.Wait(1);

					int queueItemCountAfterRemove = QueueData.Instance.Count;

					resultsHarness.AddTestResult(queueItemCount -1 == queueItemCountAfterRemove);

					bool queueItemExists = testRunner.WaitForName("Queue Item " + "2013-01-25_Mouthpiece_v2", 2);

					resultsHarness.AddTestResult(queueItemExists == false);


					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class EditButtonClickedTurnsOnOffEditMode
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void EditButtonTurnsOnOffEditMode()
		{
			//Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);
					/*
					 *Tests that when the edit button is clicked we go into editmode (print queue items have checkboxes on them)  
					 *1. After Edit button is clicked print queue items have check boxes
					 *2. Selecting multiple queue itema and then clicking the Remove button removes the item 
					 *3. Selecting multiple queue items and then clicking the Remove button decreases the queue tab count by one
					 */

					bool checkboxExists = testRunner.WaitForName("Queue Item Checkbox", 2);

					resultsHarness.AddTestResult(checkboxExists == false);

					resultsHarness.AddTestResult(QueueData.Instance.Count == 4);

					SystemWindow systemWindow;
					string itemName = "Queue Item " + "2013-01-25_Mouthpiece_v2";

					GuiWidget queueItem = testRunner.GetWidgetByName(itemName, out systemWindow, 3);
					SearchRegion queueItemRegion = testRunner.GetRegionByName(itemName, 3);

					{
						testRunner.ClickByName("Queue Edit Button", 2);

						SystemWindow containingWindow;
						GuiWidget foundWidget = testRunner.GetWidgetByName("Queue Item Checkbox", out containingWindow, 3, searchRegion: queueItemRegion);
						resultsHarness.AddTestResult(foundWidget != null, "We should have an actual checkbox");
					}

					{
						testRunner.ClickByName("Queue Done Button", 2);

						testRunner.Wait(.5);

						SystemWindow containingWindow;
						GuiWidget foundWidget = testRunner.GetWidgetByName("Queue Item Checkbox", out containingWindow, 1, searchRegion: queueItemRegion);
						resultsHarness.AddTestResult(foundWidget == null, "We should not have an actual checkbox");
					}

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items, overrideWidth: 600);
			Assert.IsTrue(testHarness.AllTestsPassed(4));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class DoneButtonClickedTurnsOffEditMode
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void DoneButtonTurnsOffEditMode()
		{
			//Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);
					/*
					 *Tests that when one item is selected  
					 *1. Queue Item count equals three before the test starts 
					 *2. Selecting multiple queue itema and then clicking the Remove button removes the item 
					 *3. Selecting multiple queue items and then clicking the Remove button decreases the queue tab count by one
					 */

					int queueItemCount = QueueData.Instance.Count;

					string itemName = "Queue Item " + "2013-01-25_Mouthpiece_v2";
					SystemWindow systemWindow;
					GuiWidget queueItem = testRunner.GetWidgetByName(itemName, out systemWindow, 3);
					SearchRegion queueItemRegion = testRunner.GetRegionByName(itemName, 3);

					testRunner.ClickByName("Queue Edit Button", 2);

					GuiWidget foundWidget = testRunner.GetWidgetByName("Queue Item Checkbox", out systemWindow, 3, searchRegion: queueItemRegion);
					resultsHarness.AddTestResult(foundWidget != null, "We should have an actual checkbox");

					testRunner.ClickByName("Queue Done Button", 1);

					foundWidget = testRunner.GetWidgetByName("Queue Item Checkbox", out systemWindow, 1, searchRegion: queueItemRegion);
					resultsHarness.AddTestResult(foundWidget != null, "Checkbox is gone");

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class RemoveButtonClickedRemovesMultipleItems
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void RemoveButtonRemovesMultipleItems()
		{
			//Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);
					/*
					 *Tests that when one item is selected  
					 *1. Queue Item count equals three before the test starts 
					 *2. Selecting multiple queue itema and then clicking the Remove button removes the item 
					 *3. Selecting multiple queue items and then clicking the Remove button decreases the queue tab count by one
					 */

					int queueItemCount = QueueData.Instance.Count;

					testRunner.Wait(2);

					testRunner.ClickByName("Queue Edit Button", 2);

					testRunner.ClickByName("Queue Item " + "Batman", 2);

					testRunner.ClickByName("Queue Remove Button", 2);

					testRunner.Wait(1);

					int queueItemCountAfterRemove = QueueData.Instance.Count;

					resultsHarness.AddTestResult(queueItemCount - 2 == queueItemCountAfterRemove);

					bool queueItemExists = testRunner.WaitForName("Queue Item " + "Batman", 2);
					bool secondQueueItemExists = testRunner.WaitForName("Queue Item " + "2013-01-25_Mouthpiece_v2", 2);

					resultsHarness.AddTestResult(queueItemExists == false);
					resultsHarness.AddTestResult(secondQueueItemExists == false);


					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
			Assert.IsTrue(testHarness.AllTestsPassed(3));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class ExportToZipMenuItemClickedExportsQueueToZip
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void ExportToZipMenuItemClicked()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);
					/*
					 *Tests Export to Zip menu item is clicked the queue is compressed and exported to location on disk
					 *1. Check that there are items in the queue 
					 *2. Export Queue and make sure file exists on disk
					 */

					bool queueEmpty = true;
					int queueItemCountBeforeRemoveAllClicked = QueueData.Instance.Count;

					if (queueItemCountBeforeRemoveAllClicked > 0)
					{
						queueEmpty = false;
					}

					resultsHarness.AddTestResult(queueEmpty == false);

					testRunner.ClickByName("Queue... Menu", 2);

					testRunner.ClickByName(" Export to Zip Menu Item", 2);

					testRunner.Wait(2);

					//Type in Absolute Path to Save 
					string exportZipPath = MatterControlUtilities.GetTestItemPath("TestExportZip.zip");

					// Ensure file does not exist before save
					if(File.Exists(exportZipPath))
					{
						File.Delete(exportZipPath);
					}

					testRunner.Type(exportZipPath);

					testRunner.Wait(2);

					testRunner.Type("{Enter}");

					testRunner.Wait(1);

					bool queueWasExportedToZip = File.Exists(exportZipPath);

					testRunner.Wait(2);

					resultsHarness.AddTestResult(queueWasExportedToZip == true);

					//Add the exprted zip file to the Queue and confirm that the Queue Count increases by 3 
					testRunner.ClickByName("Queue Add Button");
					testRunner.Wait(1);
					testRunner.Type(exportZipPath);
					testRunner.Wait(1);
					testRunner.Type("{Enter}");

					int queueCountAfterZipIsAdded = QueueData.Instance.Count;
					bool allItemsInZipWereAddedToTheQueue = false;

					if(queueCountAfterZipIsAdded == queueItemCountBeforeRemoveAllClicked * 2)
					{
						allItemsInZipWereAddedToTheQueue = true;
					}

					resultsHarness.AddTestResult(allItemsInZipWereAddedToTheQueue == true);

					if (File.Exists(exportZipPath))
					{
						File.Delete(exportZipPath);
					}

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
			Assert.IsTrue(testHarness.AllTestsPassed(3));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class SendMenuItemClickedWhileNotLoggedIn
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void SendMenuItemCLickedNoSignIn()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);
					/*
					 *Tests Export to Zip menu item is clicked the queue is compressed and exported to location on disk
					 *1. Check that there are items in the queue 
					 *2. Export Queue and make sure file exists on disk
					 */

					int queueItemCountBeforeRemoveAllClicked = QueueData.Instance.Count;

					resultsHarness.AddTestResult(queueItemCountBeforeRemoveAllClicked > 0);

					testRunner.ClickByName("More...  Menu", 2);

					testRunner.ClickByName("Send Menu Item", 2);

					bool signInPromptWindowOpens = testRunner.WaitForName("Ok Button", 2);

					resultsHarness.AddTestResult(signInPromptWindowOpens == true);

					testRunner.ClickByName("Ok Button");

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
			Assert.IsTrue(testHarness.AllTestsPassed(2));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class RemoveAllMenuItemClicked
	{

		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void RemoveAllMenuItemClickedRemovesAll()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);
					/*
					 *Tests that when the Remove All menu item is clicked 
					 *1. Queue Item count is set to zero
					 *2. All queue row items that were previously in the queue are removed
					 */

					bool queueEmpty = true;
					int queueItemCountBeforeRemoveAllClicked = QueueData.Instance.Count;

					if(queueItemCountBeforeRemoveAllClicked != 0)
					{
						queueEmpty = false;
					}

					resultsHarness.AddTestResult(queueEmpty == true);

					bool batmanPartExists1 = testRunner.WaitForName("Queue Item " + "Batman", 1);
					bool foxPartExistst1 = testRunner.WaitForName("Queue Item " + "Fennec_Fox", 1);
					bool mouthpiecePartExists1 = testRunner.WaitForName("Queue Item " + "2013-01-25_Mouthpiece_v2", 1);

					resultsHarness.AddTestResult(batmanPartExists1 == true);
					resultsHarness.AddTestResult(mouthpiecePartExists1 == true);
					resultsHarness.AddTestResult(foxPartExistst1 == true);

				
					testRunner.ClickByName("Queue... Menu", 2);

					testRunner.ClickByName(" Remove All Menu Item", 2);

					testRunner.Wait(2);

					int queueItemCountAfterRemoveAll = QueueData.Instance.Count; 

					if(queueItemCountAfterRemoveAll == 0)
					{
						queueEmpty = true; 
					}

					resultsHarness.AddTestResult(queueEmpty == true);

					bool batmanPartExists2 = testRunner.WaitForName("Queue Item " + "Batman", 1);
					bool foxPartExistst2 = testRunner.WaitForName("Queue Item " + "Fennec_Fox", 1);
					bool mouthpiecePartExists2 = testRunner.WaitForName("Queue Item " + "2013-01-25_Mouthpiece_v2", 1);

					resultsHarness.AddTestResult(batmanPartExists2 == false);
					resultsHarness.AddTestResult(mouthpiecePartExists2 == false);
					resultsHarness.AddTestResult(foxPartExistst2 == false);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
			Assert.IsTrue(testHarness.AllTestsPassed(8));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Ignore("Not Finished")]
	public class CreatePartSheetMenuItemClickedCreatesPartSheet
	{

		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void ClickCreatePartSheetButton()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);
					/*
					 *Tests that when the Remove All menu item is clicked 
					 *1. Queue Item count is set to zero
					 *2. All queue row items that were previously in the queue are removed
					 */

					bool queueEmpty = true;
					int queueItemCount = QueueData.Instance.Count;

					if (queueItemCount == 3)
					{
						queueEmpty = false;
					}

					resultsHarness.AddTestResult(queueEmpty == false);
					testRunner.ClickByName("Queue... Menu", 2);
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
					resultsHarness.AddTestResult(partSheetCreated == true);


					if (File.Exists(validatePartSheetPath))
					{

						File.Delete(validatePartSheetPath);

					}

					MatterControlUtilities.CloseMatterControl(testRunner);

				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
			Assert.IsTrue(testHarness.AllTestsPassed(5));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class QueueRowItemRemoveViewButtons
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void ClickQueueRoWItemViewAndRemove()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					/*
					 *Tests:
					 *1. When the remove button on a queue item is clicked the queue tab count decreases by one 
					 *2. When the remove button on a queue item is clicked the item is removed
					 *3. When the View button on a queue item is clicked the part preview window is opened
					 */


					testRunner.Wait(2);
					int currentQueueItemCount = QueueData.Instance.Count;

					resultsHarness.AddTestResult(testRunner.WaitForName("Queue Item " + "Batman", 1));
					resultsHarness.AddTestResult(testRunner.WaitForName("Queue Item " + "2013-01-25_Mouthpiece_v2", 1));

					testRunner.ClickByName("Queue Item " + "Batman", 1);
					testRunner.ClickByName("Queue Item " + "Batman" + " Remove");
					testRunner.Wait(2);

					int queueItemCountAfterRemove = QueueData.Instance.Count;
					
					resultsHarness.AddTestResult(currentQueueItemCount - 1 == queueItemCountAfterRemove);

					bool batmanQueueItemExists = testRunner.WaitForName("Queue Item " + "Batman", 1);
					resultsHarness.AddTestResult(batmanQueueItemExists == false);

					bool partPreviewWindowExists1 = testRunner.WaitForName("Queue Item " + "2013-01-25_Mouthpiece_v2" + " Part Preview", 1);
					resultsHarness.AddTestResult(partPreviewWindowExists1 == false);
					testRunner.ClickByName("Queue Item " + "2013-01-25_Mouthpiece_v2", 1);
					testRunner.Wait(2);
					testRunner.ClickByName("Queue Item " + "2013-01-25_Mouthpiece_v2" + " View", 1);

					bool partPreviewWindowExists2 = testRunner.WaitForName("Queue Item " + "2013-01-25_Mouthpiece_v2" + " Part Preview", 2);
					resultsHarness.AddTestResult(partPreviewWindowExists2 == true);

						
					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};
			
			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items, overrideWidth: 600);
			Assert.IsTrue(testHarness.AllTestsPassed(6));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class QueueAddButtonAddsAMFFileToQueue
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void QueueAddButtonAddsAMF()
		{
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					/* Tests that when the Queue Copy button is clicked:
					* 1. QueueCount = Zero
					* 2. Add button can add single .amf file to the queue 
					* 3. Queue count inceases by one 
					*/
					int queueCountBeforeTest = QueueData.Instance.Count;

					bool queueCountEqualsZero = false;

					if (queueCountBeforeTest == 0)
					{
						queueCountEqualsZero = true;
					}

					//Make sure queue count equals zero before test begins
					resultsHarness.AddTestResult(queueCountEqualsZero == true);

					//Click Add button 
					testRunner.ClickByName("Queue Add Button", 2);
					testRunner.Wait(1);

					string pathToType = MatterControlUtilities.GetTestItemPath("Rook.amf");

					testRunner.Type(pathToType);
					testRunner.Wait(1);
					testRunner.Type("{Enter}");


					//Make sure Queue Count increases by one 
					int queueCountAfterAMFIsAdded = QueueData.Instance.Count;
					bool oneItemAddedToQueue = false;

					if (queueCountAfterAMFIsAdded == 1)
					{
						oneItemAddedToQueue = true;
					}

					resultsHarness.AddTestResult(oneItemAddedToQueue == true);

					//Make sure amf queue item is added 
					bool firstQueueItemExists = testRunner.WaitForName("Queue Item " + "Rook", 1);
					resultsHarness.AddTestResult(firstQueueItemExists == true);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);
			Assert.IsTrue(testHarness.AllTestsPassed(3));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class QueueAddButtonAddsSTLFileToQueue
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void QueueAddButtonAddsSTL()
		{
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					/* Tests that when the Queue Copy button is clicked:
					* 1. QueueCount = Zero
					* 2. Add button can add single .stl file to the queue 
					* 3. Queue count inceases by one 
					*/
					int queueCountBeforeTest = QueueData.Instance.Count;
					bool queueCountEqualsZero = false;

					if (queueCountBeforeTest == 0)
					{
						queueCountEqualsZero = true;
					}

					//Make sure queue count equals zero before test begins
					resultsHarness.AddTestResult(queueCountEqualsZero == true);

					//Click Add button 
					testRunner.ClickByName("Queue Add Button", 2);
					testRunner.Wait(1);

					string pathToType = MatterControlUtilities.GetTestItemPath("Batman.stl");

					testRunner.Type(pathToType);
					testRunner.Wait(1);
					testRunner.Type("{Enter}");

					int queueCountAfterSTLIsAdded = QueueData.Instance.Count;
					bool oneItemAddedToQueue = false;

					if (queueCountAfterSTLIsAdded == 1)
					{
						oneItemAddedToQueue = true;
					}

					resultsHarness.AddTestResult(oneItemAddedToQueue == true);

					//stl queue item is added to the queue
					bool firstQueueItemExists = testRunner.WaitForName("Queue Item " + "Batman", 1);
					resultsHarness.AddTestResult(firstQueueItemExists == true);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);
			Assert.IsTrue(testHarness.AllTestsPassed(3));
		}
	}

	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class QueueAddButtonAddsGcodeFileToQueue
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void QueueAddButtonAddsGcodeFile()
		{

			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					/* Tests that when the Queue Copy button is clicked:
					* 1. QueueCount = Zero
					* 2. Add button can add single .gcode file to the queue 
					* 3. Queue count inceases by one 
					*/
					int queueCountBeforeTest = QueueData.Instance.Count;
					bool queueCountEqualsZero = false;

					if (queueCountBeforeTest == 0)
					{
						queueCountEqualsZero = true;
					}

					//Make sure queue count equals zero before test begins
					resultsHarness.AddTestResult(queueCountEqualsZero == true);

					//Click Add button 
					testRunner.ClickByName("Queue Add Button", 2);
					testRunner.Wait(1);

					string pathToType = MatterControlUtilities.GetTestItemPath("chichen-itza_pyramid.gcode");

					testRunner.Type(pathToType);
					testRunner.Wait(1);
					testRunner.Type("{Enter}");

					int queueCountAfterGcodeIsAdded = QueueData.Instance.Count;
					bool oneItemAddedToQueue = false;

					if (queueCountAfterGcodeIsAdded == 1)
					{
						oneItemAddedToQueue = true;
					}

					resultsHarness.AddTestResult(oneItemAddedToQueue == true);

					//stl queue item is added to the queue
					bool firstQueueItemExists = testRunner.WaitForName("Queue Item " + "chichen-itza_pyramid", 1);
					resultsHarness.AddTestResult(firstQueueItemExists == true);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);
			Assert.IsTrue(testHarness.AllTestsPassed(3));
		}
	}
}













