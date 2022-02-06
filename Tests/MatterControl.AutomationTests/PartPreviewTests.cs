/*
Copyright (c) 2022, Lars Brubaker
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

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class PartPreviewTests
	{
		[Test]
		public async Task CopyButtonMakesCopyOfPart()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenPartTab();

				testRunner.AddItemToBed();

				// Get View3DWidget
				var view3D = testRunner.GetWidgetByName("View3DWidget", out _, 3) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				testRunner.WaitForName("Calibration - Box.stl");
				Assert.AreEqual(1, scene.Children.Count, "Should have 1 part before copy");

				// Select scene object
				testRunner.Select3DPart("Calibration - Box.stl")
					// Click Copy button and count Scene.Children
					.ClickByName("Duplicate Button")
					.Assert(() => scene.Children.Count == 2, "Should have 2 parts after copy");

				// Click Copy button a second time and count Scene.Children
				testRunner.ClickByName("Duplicate Button");
				testRunner.Assert(() => scene.Children.Count == 3, "Should have 3 parts after 2nd copy");

				return Task.CompletedTask;
			}, overrideWidth: 1300, maxTimeToRun: 60);
		}

		[Test]
		public async Task AddMultiplePartsMultipleTimes()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenPartTab();

				var parts = new[]
				{
					"Row Item Cone",
					"Row Item Sphere",
					"Row Item Torus"
				};
				testRunner.AddPrimitivePartsToBed(parts, multiSelect: true);

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _, 3) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				testRunner.WaitForName("Selection");
				Assert.AreEqual(1, scene.Children.Count, $"Should have 1 scene item after first AddToBed");

				testRunner.ClickByName("Print Library Overflow Menu");
				testRunner.ClickByName("Add to Bed Menu Item");
				testRunner.WaitForName("Selection");
				Assert.AreEqual(parts.Length + 1, scene.Children.Count, $"Should have {parts.Length + 1} scene items after second AddToBed");

				return Task.CompletedTask;
			}, overrideWidth: 1300, maxTimeToRun: 60);
		}

		[Test]
		public async Task DesignTabFileOpperations()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenPartTab(false);

				// Get View3DWidget
				var view3D = testRunner.GetWidgetByName("View3DWidget", out SystemWindow systemWindow, 3) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				var tempFilaname = "Temp Test Save.mcx";
				var tempFullPath = Path.Combine(ApplicationDataStorage.Instance.DownloadsDirectory, tempFilaname);

				// delete the temp file if it exists in the Downloads folder
				void DeleteTempFile()
				{
					if (File.Exists(tempFullPath))
					{
						File.Delete(tempFullPath);
					}
				}

				DeleteTempFile();

				testRunner.Assert(() => scene.Children.Count == 1, "Should have 1 part (the phil)")
					// Make sure the tab is named 'New Design'
					.Assert(() => systemWindow.GetVisibleWigetWithText("New Design") != null, "Must have New Design")
					// add a new part to the bed
					.AddItemToBed()
					// Click the save button
					.ClickByName("Save")
					// Cancle the save as
					.ClickByName("Cancel Wizard Button")
					// Make sure the tab is named 'New Design'
					.Assert(() => systemWindow.GetVisibleWigetWithText("New Design") != null, "Have a new design tab")
					// Click the close tab button
					.ClickByName("Close Tab Button")
					// Select Cancel
					.ClickByName("Cancel Button")
					// Make sure the tab is named 'New Design'
					.Assert(() => systemWindow.GetVisibleWigetWithText("New Design") != null, "Still have design tab")
					// Click the close tab button
					.ClickByName("Close Tab Button")
					// Select 'Save'
					.ClickByName("Yes Button")
					// Cancel the 'Save As'
					.ClickByName("Cancel Wizard Button")
					// Make sure the window is still open and the tab is named 'New Design'
					.Assert(() => systemWindow.GetVisibleWigetWithText("New Design") != null, "still have desin tab")
					// Click the save button
					.ClickByName("Save")
					// Save a temp file to the downloads folder
					.DoubleClickByName("Computer Row Item Collection")
					.DoubleClickByName("Downloads Row Item Collection")
					.ClickByName("Design Name Edit Field")
					.Type(tempFilaname)
					.ClickByName("Accept Button")
					// Verify it is there
					.Assert(() => File.Exists(tempFullPath), "Must save the file")
					// And that the tab got the name
					.Assert(() => systemWindow.GetVisibleWigetWithText(tempFilaname) != null, "Tab was renamed")
					// and the tooltip is right
					.Assert(() => systemWindow.GetVisibleWigetWithText(tempFilaname).ToolTipText == tempFullPath, "Correct tool tip name")
					// Add a part to the bed
					.AddItemToBed()
					// Click the close tab button (we have an edit so it should show the save request)
					.ClickByName("Close Tab Button")
					// Click the 'Cancel'
					.ClickByName("Cancel Button")
					// Click the 'Save' button
					.ClickByName("Save")
					// Click the close button (now we have no edit it should cancel without request)
					.ClickByName("Close Tab Button");

				// Verify the tab closes without requesting save
				testRunner.Assert(() => systemWindow.GetVisibleWigetWithText(tempFilaname) == null, "The tab should have closed");

				// delete the temp file if it exists in the Downloads folder
				DeleteTempFile();

				return Task.CompletedTask;
			}, maxTimeToRun: 60);
		}

		[Test]
		public async Task GroupAndUngroup()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenPartTab();

				testRunner.AddItemToBed();

				// Get View3DWidget and count Scene.Children before Copy button is clicked
				View3DWidget view3D = testRunner.GetWidgetByName("View3DWidget", out _, 3) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				// Assert expected start count
				Assert.AreEqual(1, scene.Children.Count, "Should have one part before copy");

				// Select scene object
				testRunner.Select3DPart("Calibration - Box.stl");

				for (int i = 2; i <= 6; i++)
				{
					testRunner.ClickByName("Duplicate Button")
						.Assert(() => scene.Children.Count == i, $"Should have {i} parts after copy");
				}

				// Get MeshGroupCount before Group is clicked
				Assert.AreEqual(6, scene.Children.Count, "Scene should have 6 parts after copy loop");

				// Duplicate button moved to new container - move focus back to View3DWidget so CTRL-A below is seen by expected control
				testRunner.Select3DPart("Calibration - Box.stl")
					// select all
					.Type("^a")
					.ClickByName("Group Button")
					.Assert(() => scene.Children.Count == 1, $"Should have 1 parts after group");

				testRunner.ClickByName("Ungroup Button")
					.Assert(() => scene.Children.Count == 6, $"Should have 6 parts after ungroup");

				return Task.CompletedTask;
			}, overrideWidth: 1300);
		}

		[Test]
		public async Task RemoveButtonRemovesParts()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenPartTab();

				testRunner.AddItemToBed();

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				testRunner.Select3DPart("Calibration - Box.stl");

				Assert.AreEqual(1, scene.Children.Count, "There should be 1 part on the bed after AddDefaultFileToBedplate()");

				// Add 5 items
				for (int i = 0; i <= 4; i++)
				{
					testRunner.ClickByName("Duplicate Button")
						.Delay(.5);
				}

				Assert.AreEqual(6, scene.Children.Count, "There should be 6 parts on the bed after the copy loop");

				// Remove an item
				testRunner.ClickByName("Remove Button");

				// Confirm
				Assert.AreEqual(5, scene.Children.Count, "There should be 5 parts on the bed after remove");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task SaveAsToQueue()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();

				testRunner.AddItemToBed();

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;

				testRunner.Select3DPart("Calibration - Box.stl");

				int expectedCount = QueueData.Instance.ItemCount + 1;

				testRunner.SaveBedplateToFolder("Test PartA", "Print Queue Row Item Collection")
					.NavigateToLibraryHome()
					.NavigateToFolder("Print Queue Row Item Collection");

				testRunner.Delay(200);
				Assert.IsTrue(testRunner.WaitForName("Row Item Test PartA"), "The part we added should be in the library");
				Assert.AreEqual(expectedCount, QueueData.Instance.ItemCount, "Queue count should increase by one after Save operation");

				return Task.CompletedTask;
			});
		}

		[Test]
		public async Task SaveAsToLocalLibrary()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter();

				testRunner.AddItemToBed();

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;

				testRunner.Select3DPart("Calibration - Box.stl");

				int expectedCount = QueueData.Instance.ItemCount + 1;

				testRunner.SaveBedplateToFolder("Test PartB", "Local Library Row Item Collection")
					.NavigateToLibraryHome()
					.NavigateToFolder("Local Library Row Item Collection");

				Assert.IsTrue(testRunner.WaitForName("Row Item Test PartB"), "The part we added should be in the library");

				return Task.CompletedTask;
			});
		}
	}

	public static class WidgetExtensions
    {
		/// <summary>
		/// Search the widget stack for a widget that is both visible on screen and has it's text set to the visibleText string
		/// </summary>
		/// <param name="widget">The root widget to search</param>
		/// <param name="">the name to search for</param>
		/// <returns></returns>
		public static GuiWidget GetVisibleWigetWithText(this GuiWidget widget, string visibleText)
        {
			if (widget.ActuallyVisibleOnScreen())
			{
				if (widget.Text == visibleText)
				{
					return widget;
				}

				foreach(var child in widget.Children)
                {
					var childWithText = GetVisibleWigetWithText(child, visibleText);
					if (childWithText != null)
                    {
						return childWithText;
                    }
                }
			}

			return null;
        }
    }
}
