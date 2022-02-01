using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
				testRunner.OpenEmptyPartTab();

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
					.WaitFor(() => scene.Children.Count == 2);
				Assert.AreEqual(2, scene.Children.Count, "Should have 2 parts after copy");

				// Click Copy button a second time and count Scene.Children
				testRunner.ClickByName("Duplicate Button");
				testRunner.WaitFor(() => scene.Children.Count > 2);
				Assert.AreEqual(3, scene.Children.Count, "Should have 3 parts after 2nd copy");

				return Task.CompletedTask;
			}, overrideWidth: 1300, maxTimeToRun: 60);
		}

		[Test]
		public async Task GroupAndUngroup()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenEmptyPartTab();

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
						.WaitFor(() => scene.Children.Count == i);
					Assert.AreEqual(i, scene.Children.Count, $"Should have {i} parts after copy");
				}

				// Get MeshGroupCount before Group is clicked
				Assert.AreEqual(6, scene.Children.Count, "Scene should have 6 parts after copy loop");

				// Duplicate button moved to new container - move focus back to View3DWidget so CTRL-A below is seen by expected control
				testRunner.Select3DPart("Calibration - Box.stl")
					// select all
					.Type("^a")
					.ClickByName("Group Button")
					.WaitFor(() => scene.Children.Count == 1);
				Assert.AreEqual(1, scene.Children.Count, $"Should have 1 parts after group");

				testRunner.ClickByName("Ungroup Button")
					.WaitFor(() => scene.Children.Count == 6);
				Assert.AreEqual(6, scene.Children.Count, $"Should have 6 parts after ungroup");

				return Task.CompletedTask;
			}, overrideWidth: 1300);
		}

		[Test]
		public async Task RemoveButtonRemovesParts()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenEmptyPartTab();

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

		[Test]
		public async Task AddMultiplePartsMultipleTimes()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenEmptyPartTab();

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
	}
}
