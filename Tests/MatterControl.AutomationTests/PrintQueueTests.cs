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

using System;
using System.Threading.Tasks;
using MatterHackers.MatterControl.PartPreviewWindow;
using NUnit.Framework;
using TestInvoker;

namespace MatterHackers.MatterControl.Tests.Automation
{
	// Most of these tests are disabled. Local Library and Queue needs to be added by InitializeLibrary() (MatterHackers.MatterControl.ApplicationController).
	[TestFixture, Category("MatterControl.UI.Automation"), Parallelizable(ParallelScope.Children)]
	public class PrintQueueTests
	{
		[Test, ChildProcessTest]
		public async Task AddOneItemToQueue()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
                // Expected = initial + 1
                throw new NotImplementedException("fix this");
				int expectedCount = 0;// QueueData.Instance.ItemCount + 1;

                testRunner.AddAndSelectPrinter();

				testRunner.ChangeToQueueContainer();

				// Click Add button and select files
				testRunner.InvokeLibraryAddDialog();

				// Open Fennec_Fox
				testRunner.CompleteDialog(MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl"));

				// Wait for expected outcome
				throw new NotImplementedException("fix this");
                //testRunner.WaitFor(() => QueueData.Instance.ItemCount == expectedCount);

				// Assert - one part  added and queue count increases by one
				throw new NotImplementedException("fix this");
                //Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by 1 when adding 1 item");

				Assert.IsTrue(testRunner.WaitForName("Row Item Fennec_Fox.stl"), "Named widget should exist after add(Fennec_Fox)");

				return Task.CompletedTask;
			});
		}

		[Test, ChildProcessTest]
		public async Task AddTwoItemsToQueue()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
                // Expected = initial + 2;
                throw new NotImplementedException("fix this");
                int expectedCount = 0;// QueueData.Instance.ItemCount + 2;

                testRunner.AddAndSelectPrinter();

				testRunner.ChangeToQueueContainer();

				// Click Add button and select files
				testRunner.InvokeLibraryAddDialog();

				// Open Fennec_Fox, Batman files
				testRunner.CompleteDialog(
					string.Format(
						"\"{0}\";\"{1}\"",
						MatterControlUtilities.GetTestItemPath("Fennec_Fox.stl"),
						MatterControlUtilities.GetTestItemPath("Batman.stl")),
					secondsToWait: 2);

                // Wait for expected outcome
                throw new NotImplementedException("fix this");
                //testRunner.WaitFor(() => QueueData.Instance.ItemCount == expectedCount);

                // Assert - two parts added and queue count increases by two
                throw new NotImplementedException("fix this");
                //Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by 2 when adding 2 items");
                Assert.IsTrue(testRunner.WaitForName("Row Item Fennec_Fox.stl"), "Named widget should exist after add(Fennec_Fox)");
				Assert.IsTrue(testRunner.WaitForName("Row Item Batman.stl"), "Named widget should exist after add(Batman)");

				return Task.CompletedTask;
			});
		}

		[Test, ChildProcessTest]
		public async Task RemoveButtonRemovesSingleItem()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				throw new NotImplementedException("fix this");
				int expectedCount = 0;// QueueData.Instance.ItemCount - 1;

				testRunner.AddAndSelectPrinter();

				testRunner.NavigateToFolder("Queue Row Item Collection");

				// Select both items
				testRunner.SelectListItems("Row Item 2013-01-25_Mouthpiece_v2.stl");

				// Remove item
				testRunner.LibraryRemoveSelectedItem();
				throw new NotImplementedException("fix this");
                //testRunner.WaitFor(() => QueueData.Instance.ItemCount == expectedCount, 500);

				throw new NotImplementedException("fix this");
                //Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should decrease by one after clicking Remove");

				// Make sure selected item was removed
				Assert.IsFalse(testRunner.WaitForName("Row Item 2013-01-25_Mouthpiece_v2.stl", .5), "Mouthpiece part should *not* exist after remove");

				return Task.CompletedTask;
			}, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test, ChildProcessTest]
		public async Task RemoveButtonRemovesMultipleItems()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				throw new NotImplementedException("fix this");
                int expectedCount = 0;// QueueData.Instance.ItemCount - 2;

				testRunner.AddAndSelectPrinter();

				testRunner.NavigateToFolder("Queue Row Item Collection");

				// Select both items
				testRunner.SelectListItems("Row Item Batman.stl", "Row Item 2013-01-25_Mouthpiece_v2.stl");

				// Remove items
				testRunner.LibraryRemoveSelectedItem();
				throw new NotImplementedException("fix this");
                //testRunner.WaitFor(() => QueueData.Instance.ItemCount == expectedCount, 500);

				throw new NotImplementedException("fix this");
                //Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should decrease by two after clicking Remove");

				// Make sure both selected items are removed
				Assert.IsFalse(testRunner.WaitForName("Row Item Batman.stl", .5), "Batman part should *not* exist after remove");
				Assert.IsFalse(testRunner.WaitForName("Row Item 2013-01-25_Mouthpiece_v2.stl", .5), "Mouthpiece part should *not* exist after remove");

				return Task.CompletedTask;
			}, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		
		[Test, ChildProcessTest]
		public async Task DragTo3DViewAddsItem()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenPartTab();

				testRunner.AddTestAssetsToLibrary(new[] { "Batman.stl" });

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				Assert.AreEqual(0, scene.Children.Count, "The scene should have zero items before drag/drop");

				testRunner.DragDropByName("Row Item Batman", "Object3DControlLayer");
				Assert.AreEqual(1, scene.Children.Count, "The scene should have one item after drag/drop");

				testRunner.Delay(.2);

				testRunner.DragDropByName("Row Item Batman", "Object3DControlLayer");
				Assert.AreEqual(2, scene.Children.Count, "The scene should have two items after drag/drop");

				return Task.CompletedTask;
			}, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items);
		}

		[Test, ChildProcessTest]
		public async Task AddAmfFile()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
                // Expected = initial + 1
                throw new NotImplementedException("fix this");

                int expectedCount = 0;// QueueData.Instance.ItemCount + 1;

				testRunner.AddAndSelectPrinter();

				testRunner.ChangeToQueueContainer();

				// Click Add button and select files
				testRunner.InvokeLibraryAddDialog();

				// Open Rook
				testRunner.CompleteDialog(
					MatterControlUtilities.GetTestItemPath("Rook.amf"));

				// Wait for expected outcome
				throw new NotImplementedException("fix this");
                //testRunner.WaitFor(() => QueueData.Instance.ItemCount == expectedCount);

				// Assert - one part  added and queue count increases by one
				throw new NotImplementedException("fix this");
                //Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by 1 when adding 1 item");
				Assert.IsTrue(testRunner.WaitForName("Row Item Rook.amf"), "Named widget should exist after add(Rook)");

				return Task.CompletedTask;
			});
		}

		[Test, ChildProcessTest]
		public async Task AddStlFile()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
                // Expected = initial + 1
                throw new NotImplementedException("fix this");

                int expectedCount = 0;// QueueData.Instance.ItemCount + 1;

				testRunner.AddAndSelectPrinter();

				testRunner.ChangeToQueueContainer();

				// Click Add button and select files
				testRunner.InvokeLibraryAddDialog();

				// Open Batman
				testRunner.CompleteDialog(
					MatterControlUtilities.GetTestItemPath("Batman.stl"));

				// Wait for expected outcome
				throw new NotImplementedException("fix this");
                //testRunner.WaitFor(() => QueueData.Instance.ItemCount == expectedCount);

                // Assert - one part  added and queue count increases by one
				throw new NotImplementedException("fix this");
                //Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by 1 when adding 1 item");
				Assert.IsTrue(testRunner.WaitForName("Row Item Batman.stl"), "Named widget should exist after add(Batman)");

				return Task.CompletedTask;
			});
		}

		[Test, ChildProcessTest]
		public async Task AddGCodeFile()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
                // Expected = initial + 1
                throw new NotImplementedException("fix this");

                int expectedCount = 0;// QueueData.Instance.ItemCount + 1;

				testRunner.AddAndSelectPrinter();

				testRunner.ChangeToQueueContainer();

				// Click Add button and select files
				testRunner.InvokeLibraryAddDialog();

				// Open chichen-itza_pyramid
				testRunner.CompleteDialog(
					MatterControlUtilities.GetTestItemPath("chichen-itza_pyramid.gcode"));

				// Wait for expected outcome
				throw new NotImplementedException("fix this");
                //testRunner.WaitFor(() => QueueData.Instance.ItemCount == expectedCount);

                // Assert - one part  added and queue count increases by one
                throw new NotImplementedException("fix this");

                //Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by 1 when adding 1 item");
                Assert.IsTrue(testRunner.WaitForName("Row Item chichen-itza_pyramid.gcode"), "Named widget should exist after add(chichen-itza)");

				return Task.CompletedTask;
			});
		}
	}
}
