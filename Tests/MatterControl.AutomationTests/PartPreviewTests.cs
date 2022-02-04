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
					.Require(() => scene.Children.Count == 2, "Should have 2 parts after copy");

				// Click Copy button a second time and count Scene.Children
				testRunner.ClickByName("Duplicate Button");
				testRunner.Require(() => scene.Children.Count == 3, "Should have 3 parts after 2nd copy");

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

				testRunner.Require(() => scene.Children.Count == 1, "Should have 1 part (the phil)");

				var tempFilaname = "Temp Test Save.mcx";
				var tempFullPath = Path.Combine(ApplicationDataStorage.Instance.MyDocumentsDirectory, tempFilaname);

				// delete the temp file if it exists in the Downloads folder
				void DeleteTempFile()
				{
					if (File.Exists(tempFullPath))
					{
						File.Delete(tempFullPath);
					}
				}

				DeleteTempFile();

				// Make sure the tab is named 'New Design'
				Assert.IsNotNull(systemWindow.GetVisibleWigetWithText("New Design"));

				// Click the save button
				testRunner.ClickByName("Save Button")
					// Cancle the save as
					.ClickByName("Cancel Wizard Button");

				// Make sure the tab is named 'New Design'
				Assert.IsNotNull(systemWindow.GetVisibleWigetWithText("New Design"));

				// Click the close tab button
				testRunner.ClickByName("Close Tab Button")
					// Select Cancel
					.ClickByName("Cancel Button");

				// Make sure the tab is named 'New Design'
				Assert.IsNotNull(systemWindow.GetVisibleWigetWithText("New Design"));

				// Click the close tab button
				testRunner.ClickByName("Close Tab Button")
					// Select 'Save'
					.ClickByName("Yes Button")
					// Cancel the 'Save As'
					.ClickByName("Cancel Wizard Button");

				// Make sure the window is still open and the tab is named 'New Design'
				Assert.IsNotNull(systemWindow.GetVisibleWigetWithText("New Design"));

				// Click the save button
				testRunner.ClickByName("Save Button")
					// Save a temp file to the downloads folder
					.DoubleClickByName("Computer Row Item Collection")
					.DoubleClickByName("Downloads Row Item Collection")
					.ClickByName("Design Name Edit Field")
					.Type(tempFilaname)
					.ClickByName("Accept Button");
				// Verify it is there
				Assert.IsTrue(File.Exists(tempFullPath));
				// And that the tab got the name
				Assert.IsNotNull(systemWindow.GetVisibleWigetWithText(tempFilaname));
				// and the tooltip is right
				Assert.IsTrue(systemWindow.GetVisibleWigetWithText(tempFilaname).ToolTipText == tempFullPath);
				// Add a part to the bed
				testRunner.AddItemToBed();
				// Click the close tab button (we have an edit so it should show the save request)
				testRunner.ClickByName("Close Tab Button")
					// Click the 'Cancel'
					.ClickByName("Cancel Button")
					// Click the 'Save' button
					.ClickByName("Save Button")
					// Click the close button (now we have no edit it should cancel without request)
					.ClickByName("Close Tab Button");

				// Verify the tab closes without requesting save
				testRunner.Require(() => systemWindow.GetVisibleWigetWithText(tempFilaname) == null, "The tab should have closed");

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
						.Require(() => scene.Children.Count == i, $"Should have {i} parts after copy");
				}

				// Get MeshGroupCount before Group is clicked
				Assert.AreEqual(6, scene.Children.Count, "Scene should have 6 parts after copy loop");

				// Duplicate button moved to new container - move focus back to View3DWidget so CTRL-A below is seen by expected control
				testRunner.Select3DPart("Calibration - Box.stl")
					// select all
					.Type("^a")
					.ClickByName("Group Button")
					.Require(() => scene.Children.Count == 1, $"Should have 1 parts after group");

				testRunner.ClickByName("Ungroup Button")
					.Require(() => scene.Children.Count == 6, $"Should have 6 parts after ungroup");

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
