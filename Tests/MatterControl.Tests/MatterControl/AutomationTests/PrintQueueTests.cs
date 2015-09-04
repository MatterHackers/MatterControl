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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.Agg.PlatformAbstract;
using System.IO;
using MatterHackers.MatterControl.CreatorPlugins;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.DataStorage;
using System.Diagnostics;

namespace MatterHackers.MatterControl.UI
{
	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class BuyButtonTests
	{
		[Test, RequiresSTA, RunInApplicationDomain, Ignore("Not Finished")]
		public void ClickOnBuyButton()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{

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

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, "MC_Three_Queue_Items");

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2); // make sure we ran all our tests
		}
	}

	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class CreateButtonTest
	{
		[Test, RequiresSTA, RunInApplicationDomain]
		//Test Works
		public void ClickOnCreateButton()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{

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

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, "MC_Three_Queue_Items");

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2); // make sure we ran all our tests

		}
	}


	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class ExportButtonTest
	{
		[Test, RequiresSTA, RunInApplicationDomain]
		//Test Works
		public void ClickOnExportButton()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{

					//Make sure that the export window does not exist
					bool exportWindowExists1 = testRunner.WaitForName( "Export Window Queue", 0);
					resultsHarness.AddTestResult(exportWindowExists1 == false, "Export window does not exist");

					testRunner.ClickByName("Export Queue Button", 5);
					SystemWindow containingWindow;
					GuiWidget exportWindow = testRunner.GetWidgetByName("Export Window Queue", out containingWindow, 5);
					resultsHarness.AddTestResult(exportWindow != null, "Export window does exist");
					exportWindow.CloseOnIdle();
					testRunner.Wait(.5);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, "MC_Three_Queue_Items");
			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2); // make sure we ran all our tests

		}
	}

	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class ExportButtonDisabledNoQueueItems
	{
		[Test, RequiresSTA, RunInApplicationDomain, Ignore("Not Finished")]
		public void ExportButtonIsDisabledWithNoItemsInQueue()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					
					//bool exportButtonExists = testRunner.NameExists("Export Queue Button");
					bool exportButtonExists = testRunner.WaitForName("Export Queue Button", 10);
					testRunner.Wait(5);
					resultsHarness.AddTestResult(exportButtonExists == false, "Export button is disabled");

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, "MC_Fresh_Installation");
			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 1); // make sure we ran all our tests
		}
	}

	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class QueueItemThumnailWidget
	{
		[Test, RequiresSTA, RunInApplicationDomain]
		public void QueueThumbnailWidgetOpensPartPreview()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					bool partPreviewWindowExists1 = testRunner.WaitForName("Part Preview Window Thumbnail", 0);
					resultsHarness.AddTestResult(partPreviewWindowExists1 == false, "Part Preview Window Does Not Exist");

					testRunner.ClickByName("Queue Item Thumbnail");

					SystemWindow containingWindow;
					GuiWidget partPreviewWindowExists = testRunner.GetWidgetByName("Part Preview Window Thumbnail", out containingWindow, 3);
					resultsHarness.AddTestResult(partPreviewWindowExists != null, "Part Preview Window Exists");
					partPreviewWindowExists.CloseOnIdle();
					testRunner.Wait(.5);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, "MC_Three_Queue_Items");
			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2); // make sure we ran all our tests
		}
	}

	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class ClickCopyButtonMakesACopyOfPrintItemInQueue
	{
		[Test, RequiresSTA, RunInApplicationDomain]
		public void CopyButtonMakesACopyOfPartInTheQueue()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{

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

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, "MC_Three_Queue_Items");
			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 2); // make sure we ran all our tests
		}
	}

	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class AddSingleItemToQueueAddsItem
	{
		[Test, RequiresSTA, RunInApplicationDomain]
		public void AddSingleItemToQueue()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{

					/*
					 * Tests that when the QueueData.Instance.AddItem function is called:
					 * 1. The Queue count is increased by 1
					 * 2. A QueueRowItem is created and added to the queue
					 * 3. That a copy of the part is saved to AppData\MatterControl\data\QueueItems folder
					 */
					//TODO: Eventually modify test so that we test adding queue items via file 


					bool queueDataCountEqualsZero = false;
					bool addedPartIncreasesQueueDataCount = false;
					bool queueItemAppDataDirectoryExists = false;
					bool queueItemAppDataDirectoryDoesNotExists = true;
					int currentQueueCount = QueueData.Instance.Count;
					string pathToAddedQueueItem = Path.Combine(MatterHackers.MatterControl.DataStorage.ApplicationDataStorage.ApplicationUserDataPath, "data", "QueueItems", "Batman.stl");
					string partToBeAdded = Path.Combine("..", "..", "..", "TestData", "TestParts", "Batman.stl");
					
					//Make Sure Queue Count = 0 
					if(currentQueueCount  == 0)
					{
						queueDataCountEqualsZero = true;
					}

					resultsHarness.AddTestResult(queueDataCountEqualsZero == true, "Queue count is zero before the test starts");
					testRunner.Wait(3);

					//Make sure queue item does not exist
					bool batmanSTLExists = testRunner.WaitForName("Queue Item " + "Batman", 2);
					resultsHarness.AddTestResult(batmanSTLExists == false);
					

					//Make sure that QueueItems directory does not exist
					if (!File.Exists(pathToAddedQueueItem))
					{
						queueItemAppDataDirectoryDoesNotExists = true;
					}

					resultsHarness.AddTestResult(queueItemAppDataDirectoryDoesNotExists == true, "Path to QueueItems directory does not exist before tests");

					QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileNameWithoutExtension(partToBeAdded), partToBeAdded)));
					
					resultsHarness.AddTestResult(testRunner.WaitForName("Queue Item " + "Batman", 2));

					int queueCountAfterAdd = QueueData.Instance.Count;

					if(queueCountAfterAdd == currentQueueCount + 1)
					{
						addedPartIncreasesQueueDataCount = true;
					}

					resultsHarness.AddTestResult(addedPartIncreasesQueueDataCount == true);
					testRunner.Wait(3);

					if(File.Exists(pathToAddedQueueItem))
					{
						queueItemAppDataDirectoryExists = true;
					}

					resultsHarness.AddTestResult(queueItemAppDataDirectoryExists == true);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, "MC_Fresh_Installation");
			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 6); // make sure we ran all our tests
		}
	}

	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
	public class RemoveAllMenuItemClicked
	{

		[Test, RequiresSTA, RunInApplicationDomain]
		public void RemoveAllMenuItemClickedRemovesAll()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
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

					resultsHarness.AddTestResult(queueEmpty = true);

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

					resultsHarness.AddTestResult(queueEmpty = true);

					bool batmanPartExists2 = testRunner.WaitForName("Queue Item " + "Batman", 1);
					bool foxPartExistst2 = testRunner.WaitForName("Queue Item " + "Fennec_Fox", 1);
					bool mouthpiecePartExists2 = testRunner.WaitForName("Queue Item " + "2013-01-25_Mouthpiece_v2", 1);

					resultsHarness.AddTestResult(batmanPartExists2 == false);
					resultsHarness.AddTestResult(mouthpiecePartExists2 == false);
					resultsHarness.AddTestResult(foxPartExistst2 == false);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, "MC_Three_Queue_Items");
			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 8); // make sure we ran all our tests
		}
	}
}













