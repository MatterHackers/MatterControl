using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PartPreviewWindow;
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
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.AddDefaultFileToBedplate();

				// Get View3DWidget
				View3DWidget view3D = testRunner.GetWidgetByName("View3DWidget", out _, 3) as View3DWidget;

				testRunner.WaitForName("Calibration - Box.stl");
				Assert.AreEqual(1, view3D.Scene.Children.Count, "Should have 1 part before copy");

				// Select scene object
				testRunner.Select3DPart("Calibration - Box.stl");

				// Click Copy button and count Scene.Children 
				testRunner.ClickByName("3D View Copy");
				testRunner.Delay(() => view3D.Scene.Children.Count == 2, 3);
				Assert.AreEqual(2, view3D.Scene.Children.Count, "Should have 2 parts after copy");

				// Click Copy button a second time and count Scene.Children
				testRunner.ClickByName("3D View Copy");
				testRunner.Delay(() => view3D.Scene.Children.Count > 2, 3);
				Assert.AreEqual(3, view3D.Scene.Children.Count, "Should have 3 parts after 2nd copy");

				return Task.CompletedTask;
			}, overrideWidth: 1300, maxTimeToRun: 60);
		}

		[Test]
		public async Task GroupAndUngroup()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.AddDefaultFileToBedplate();

				// Get View3DWidget and count Scene.Children before Copy button is clicked
				View3DWidget view3D = testRunner.GetWidgetByName("View3DWidget", out _, 3) as View3DWidget;

				// Assert expected start count
				Assert.AreEqual(1, view3D.Scene.Children.Count, "Should have one part before copy");

				// Select scene object
				testRunner.Select3DPart("Calibration - Box.stl");

				for (int i = 2; i <= 6; i++)
				{
					testRunner.ClickByName("3D View Copy");
					testRunner.Delay(() => view3D.Scene.Children.Count == i, 3);
					Assert.AreEqual(i, view3D.Scene.Children.Count, $"Should have {i} parts after copy");
				}

				// Get MeshGroupCount before Group is clicked
				Assert.AreEqual(6, view3D.Scene.Children.Count, "Scene should have 6 parts after copy loop");

				testRunner.Type("^a");

				testRunner.ClickByName("3D View Group");
				testRunner.Delay(() => view3D.Scene.Children.Count == 1, 3);
				Assert.AreEqual(1, view3D.Scene.Children.Count, $"Should have 1 parts after group");

				testRunner.ClickByName("3D View Ungroup");
				testRunner.Delay(() => view3D.Scene.Children.Count == 6, 3);
				Assert.AreEqual(6, view3D.Scene.Children.Count, $"Should have 6 parts after ungroup");

				return Task.CompletedTask;
			}, overrideWidth: 1300);
		}

		[Test]
		public async Task RemoveButtonRemovesParts()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.AddDefaultFileToBedplate();
				
				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;

				testRunner.Select3DPart("Calibration - Box.stl");

				Assert.AreEqual(1, view3D.Scene.Children.Count, "There should be 1 part on the bed after AddDefaultFileToBedplate()");

				// Add 5 items
				for (int i = 0; i <= 4; i++)
				{
					testRunner.ClickByName("3D View Copy");
					testRunner.Delay(.5);
				}

				Assert.AreEqual(6, view3D.Scene.Children.Count, "There should be 6 parts on the bed after the copy loop");

				// Remove an item
				testRunner.ClickByName("3D View Remove");

				// Confirm
				Assert.AreEqual(5, view3D.Scene.Children.Count, "There should be 5 parts on the bed after remove");

				return Task.CompletedTask;
			}, overrideWidth:1300);
		}

		[Test]
		public async Task UndoRedoCopy()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.AddDefaultFileToBedplate();

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;

				testRunner.Select3DPart("Calibration - Box.stl");

				Assert.AreEqual(1, view3D.Scene.Children.Count, "There should be 1 part on the bed after AddDefaultFileToBedplate()");

				// Add 5 items
				for (int i = 0; i <= 4; i++)
				{
					testRunner.ClickByName("3D View Copy");
					testRunner.Delay(.5);
				}

				Assert.AreEqual(6, view3D.Scene.Children.Count, "There should be 6 parts on the bed after the copy loop");

				// Perform and validate 5 undos
				for (int x = 0; x <= 4; x++)
				{
					int meshCountBeforeUndo = view3D.Scene.Children.Count();
					testRunner.ClickByName("3D View Undo");

					testRunner.Delay(
						() => view3D.Scene.Children.Count() == meshCountBeforeUndo-1, 
						2);
					Assert.AreEqual(view3D.Scene.Children.Count(), meshCountBeforeUndo - 1);
				}

				testRunner.Delay(.2);

				// Perform and validate 5 redoes
				for (int z = 0; z <= 4; z++)
				{
					int meshCountBeforeRedo = view3D.Scene.Children.Count();
					testRunner.ClickByName("3D View Redo");

					testRunner.Delay(
						() => meshCountBeforeRedo + 1 == view3D.Scene.Children.Count(),
						2);
					Assert.AreEqual(meshCountBeforeRedo + 1, view3D.Scene.Children.Count());
				}

				return Task.CompletedTask;	
			}, overrideWidth: 1300);
		}

		[Test]
		public async Task CopyRemoveUndoRedo()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.AddDefaultFileToBedplate();

				// Get View3DWidget
				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;

				testRunner.Select3DPart("Calibration - Box.stl");

				// Click Edit button to make edit controls visible
				testRunner.WaitForName("3D View Copy", 3);
				testRunner.Delay(1); // wait for window to finish opening
				Assert.AreEqual(1, view3D.Scene.Children.Count, "Should have 1 part before copy");

				for (int i = 0; i <= 4; i++)
				{
					testRunner.ClickByName("3D View Copy", 1);
					testRunner.Delay(.2);
				}

				Assert.AreEqual(6, view3D.Scene.Children.Count, "Should have 6 parts after batch copy");

				testRunner.ClickByName("3D View Remove", 1);
				testRunner.Delay(() => view3D.Scene.Children.Count == 5, 3);
				Assert.AreEqual(5, view3D.Scene.Children.Count, "Should have 5 parts after Remove");

				testRunner.ClickByName("3D View Undo");
				testRunner.Delay(() => view3D.Scene.Children.Count == 6, 3);
				Assert.AreEqual(6, view3D.Scene.Children.Count, "Should have 6 parts after Undo");

				testRunner.ClickByName("3D View Redo");
				testRunner.Delay(() => view3D.Scene.Children.Count == 5, 3);
				Assert.AreEqual(5, view3D.Scene.Children.Count, "Should have 5 parts after Redo");

				view3D.CloseOnIdle();
				testRunner.Delay(.1);

				return Task.CompletedTask;
			}, overrideWidth: 1300);
		}

		[Test]
		public async Task SaveAsToQueue()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				//Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");
				testRunner.Delay(1);
				testRunner.ClickByName("Row Item Calibration - Box");
				MatterControlUtilities.LibraryEditSelectedItem(testRunner);

				SystemWindow systemWindow;
				GuiWidget partPreview = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3);
				View3DWidget view3D = partPreview as View3DWidget;

				for (int i = 0; i <= 2; i++)
				{
					testRunner.ClickByName("3D View Copy");
					testRunner.Delay(.5);
				}

				//Click Save As button to save changes to the part
				testRunner.ClickByName("Save As Menu");
				testRunner.Delay(1);
				testRunner.ClickByName("Save As Menu Item");
				testRunner.Delay(1);

				//Type in name of new part and then save to Print Queue
				testRunner.Type("Save As Print Queue");
				testRunner.NavigateToFolder("Print Queue Row Item Collection");
				testRunner.Delay(1);
				testRunner.ClickByName("Save As Save Button");

				view3D.CloseOnIdle();
				testRunner.Delay(.5);

				//Make sure there is a new Queue item with a name that matches the new part
				testRunner.Delay(1);
				testRunner.ClickByName("Queue Tab");
				testRunner.Delay(1);
				Assert.IsTrue(testRunner.WaitForName("Queue Item Save As Print Queue", 5));

				return Task.CompletedTask;
			};

			await MatterControlUtilities.RunTest(testToRun, overrideWidth: 600);
		}
	}
}
