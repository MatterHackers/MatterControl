/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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

using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Ignore("Not clear if these should be ported"), Category("MatterControl.UI.Automation"), Category("MatterControl.Automation"), RunInApplicationDomain]
	public class PrintQueueUncertain
	{
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

				return Task.CompletedTask;
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

					testRunner.Delay(.5);

					SystemWindow containingWindow;
					GuiWidget foundWidget = testRunner.GetWidgetByName("Queue Item Checkbox", out containingWindow, 1, searchRegion: queueItemRegion);
					Assert.IsTrue(foundWidget == null, "We should not have an actual checkbox");
				}

				return Task.CompletedTask;
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
				testRunner.Delay(.3);
				Assert.IsNotNull(
					testRunner.GetWidgetByName("Queue Item Checkbox", out systemWindow, 3, searchRegion),
					"While in Edit mode, checkboxes should exist on queue items");

				// Exit Edit mode and confirm checkboxes are missing
				testRunner.ClickByName("Queue Done Button", 1);
				testRunner.Delay(.3);
				Assert.IsNull(
					testRunner.GetWidgetByName("Queue Item Checkbox", out systemWindow, 1, searchRegion),
					"After exiting Edit mode, checkboxes should not exist on queue items");

				return Task.CompletedTask;
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

				testRunner.Delay(() => QueueData.Instance.ItemCount == 0, 5);

				// Assert that object model has been cleared
				Assert.AreEqual(0, QueueData.Instance.ItemCount, "Queue is empty after RemoveAll action");

				// Assert that widgets have been removed
				testRunner.Delay(.5);

				Assert.IsFalse(testRunner.NameExists("Queue Item Batman"), "Batman part removed");
				Assert.IsFalse(testRunner.NameExists("Queue Item Fennec_Fox"), "Fox part removed");
				Assert.IsFalse(testRunner.NameExists("Queue Item 2013-01-25_Mouthpiece_v2"), "Mouthpiece part removed");

				return Task.CompletedTask;
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

				testRunner.Delay(2);

				Assert.AreEqual(4, QueueData.Instance.ItemCount, "Queue should initially have four items");
				Assert.IsTrue(testRunner.WaitForName("Queue Item Batman", 1));
				Assert.IsTrue(testRunner.WaitForName("Queue Item 2013-01-25_Mouthpiece_v2", 1));

				testRunner.ClickByName("Queue Item Batman", 1);
				testRunner.ClickByName("Queue Item Batman Remove");
				testRunner.Delay(2);

				Assert.AreEqual(3, QueueData.Instance.ItemCount, "Batman item removed");
				Assert.IsFalse(testRunner.NameExists("Queue Item Batman"), "Batman item removed");

				Assert.IsFalse(testRunner.NameExists("Queue Item 2013-01-25_Mouthpiece_v2 Part Preview"), "Mouthpiece Part Preview should not initially be visible");
				testRunner.ClickByName("Queue Item 2013-01-25_Mouthpiece_v2", 1);
				testRunner.Delay(2);
				testRunner.ClickByName("Queue Item 2013-01-25_Mouthpiece_v2 View", 1);

				Assert.IsTrue(testRunner.WaitForName("Queue Item 2013-01-25_Mouthpiece_v2 Part Preview", 2), "The Mouthpiece Part Preview should appear after the view button is clicked");

				return Task.CompletedTask;
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items, overrideWidth: 600);
		}
	}
}
