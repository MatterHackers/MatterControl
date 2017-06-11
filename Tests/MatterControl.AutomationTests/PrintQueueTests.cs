﻿/*
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
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), Category("MatterControl.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class PrintQueueTests
	{
		[Test]
		public async Task AddOneItemToQueue()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				// Expected = initial + 1
				int expectedCount = QueueData.Instance.ItemCount + 1;

				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ChangeToQueueContainer();

				// Click Add button and select files
				testRunner.ClickByName("Library Add Button", 2);
				testRunner.WaitForName("Automation Dialog TextEdit", 3);
				testRunner.Type(MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				// Wait up to 3 seconds for expected outcome
				testRunner.Delay(() => QueueData.Instance.ItemCount == expectedCount, 3);

				// Assert - one part  added and queue count increases by one
				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by 1 when adding 1 item");
				Assert.IsTrue(testRunner.WaitForName("Row Item Fennec_Fox.stl", 2), "Named widget should exist after add(Fennec_Fox)");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task AddTwoItemsToQueue()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				// Expected = initial + 2;
				int expectedCount = QueueData.Instance.ItemCount + 2;

				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ChangeToQueueContainer();

				// Click Add button and select files
				testRunner.ClickByName("Library Add Button", 2);
				testRunner.WaitForName("Automation Dialog TextEdit", 3);
				testRunner.Type(string.Format(
					"\"{0}\" \"{1}\"",
					MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl"),
					MatterControlUtilities.GetTestItemPath("Batman.stl")));

				testRunner.Delay(2);
				testRunner.Type("{Enter}");

				// Wait up to 3 seconds for expected outcome
				testRunner.Delay(() => QueueData.Instance.ItemCount == expectedCount, 3);

				// Assert - two parts added and queue count increases by two
				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by 2 when adding 2 items");
				Assert.IsTrue(testRunner.WaitForName("Row Item Fennec_Fox.stl", 2), "Named widget should exist after add(Fennec_Fox)");
				Assert.IsTrue(testRunner.WaitForName("Row Item Batman.stl", 2), "Named widget should exist after add(Batman)");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task RemoveButtonRemovesSingleItem()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				int expectedCount = QueueData.Instance.ItemCount - 1;

				testRunner.CloseSignInAndPrinterSelect();

				testRunner.NavigateToFolder("Print Queue Row Item Collection");

				// Select both items
				testRunner.SelectListItems("Row Item 2013-01-25_Mouthpiece_v2");

				// Remove item
				testRunner.LibraryRemoveSelectedItem();
				testRunner.Delay(() => QueueData.Instance.ItemCount == expectedCount, 5, 500);

				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should decrease by one after clicking Remove");

				// Make sure selected item was removed
				Assert.IsFalse(testRunner.WaitForName("Row Item 2013-01-25_Mouthpiece_v2", 1), "Mouthpiece part should *not* exist after remove");

				return Task.CompletedTask;
			}, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test]
		public async Task RemoveButtonRemovesMultipleItems()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				int expectedCount = QueueData.Instance.ItemCount - 2;

				testRunner.CloseSignInAndPrinterSelect();

				testRunner.NavigateToFolder("Print Queue Row Item Collection");

				// Select both items
				testRunner.SelectListItems("Row Item Batman", "Row Item 2013-01-25_Mouthpiece_v2");

				// Remove items
				testRunner.LibraryRemoveSelectedItem();
				testRunner.Delay(() => QueueData.Instance.ItemCount == expectedCount, 5, 500);

				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should decrease by two after clicking Remove");

				// Make sure both selected items are removed
				Assert.IsFalse(testRunner.WaitForName("Row Item Batman", 1), "Batman part should *not* exist after remove");
				Assert.IsFalse(testRunner.WaitForName("Row Item 2013-01-25_Mouthpiece_v2", 1), "Mouthpiece part should *not* exist after remove");

				return Task.CompletedTask;
			}, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test]
		public async Task DragTo3DViewAddsItem()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.AddTestAssetsToLibrary("Batman.stl");

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;

				Assert.AreEqual(0, view3D.Scene.Children.Count, "The scene should have zero items before drag/drop");

				testRunner.DragDropByName("Row Item Batman", "centerPartPreviewAndControls");
				Assert.AreEqual(1, view3D.Scene.Children.Count, "The scene should have one item after drag/drop");

				testRunner.DragDropByName("Row Item Batman", "centerPartPreviewAndControls");
				Assert.AreEqual(2, view3D.Scene.Children.Count, "The scene should have two items after drag/drop");

				return Task.CompletedTask;
			}, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test]
		public async Task AddAmfFile()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				// Expected = initial + 1
				int expectedCount = QueueData.Instance.ItemCount + 1;

				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ChangeToQueueContainer();

				// Click Add button and select files
				testRunner.ClickByName("Library Add Button", 2);
				testRunner.WaitForName("Automation Dialog TextEdit", 3);

				testRunner.Type(MatterControlUtilities.GetTestItemPath("Rook.amf"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				// Wait up to 3 seconds for expected outcome
				testRunner.Delay(() => QueueData.Instance.ItemCount == expectedCount, 3);

				// Assert - one part  added and queue count increases by one
				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by 1 when adding 1 item");
				Assert.IsTrue(testRunner.WaitForName("Row Item Rook.amf", 2), "Named widget should exist after add(Rook)");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task AddStlFile()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				// Expected = initial + 1
				int expectedCount = QueueData.Instance.ItemCount + 1;

				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ChangeToQueueContainer();

				// Click Add button and select files
				testRunner.ClickByName("Library Add Button", 2);
				testRunner.WaitForName("Automation Dialog TextEdit", 3);

				testRunner.Type(MatterControlUtilities.GetTestItemPath("Batman.stl"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				// Wait up to 3 seconds for expected outcome
				testRunner.Delay(() => QueueData.Instance.ItemCount == expectedCount, 3);

				// Assert - one part  added and queue count increases by one
				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by 1 when adding 1 item");
				Assert.IsTrue(testRunner.WaitForName("Row Item Batman.stl", 2), "Named widget should exist after add(Batman)");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task AddGCodeFile()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				// Expected = initial + 1
				int expectedCount = QueueData.Instance.ItemCount + 1;

				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ChangeToQueueContainer();

				// Click Add button and select files
				testRunner.ClickByName("Library Add Button", 2);
				testRunner.WaitForName("Automation Dialog TextEdit", 3);

				testRunner.Type(MatterControlUtilities.GetTestItemPath("chichen-itza_pyramid.gcode"));
				testRunner.Delay(1);
				testRunner.Type("{Enter}");

				// Wait up to 3 seconds for expected outcome
				testRunner.Delay(() => QueueData.Instance.ItemCount == expectedCount, 3);

				// Assert - one part  added and queue count increases by one
				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by 1 when adding 1 item");
				Assert.IsTrue(testRunner.WaitForName("Row Item chichen-itza_pyramid.gcode", 2), "Named widget should exist after add(chichen-itza)");

				return Task.CompletedTask;
			});
		}
	}
}
