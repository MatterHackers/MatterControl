﻿using System;
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
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class PartPreviewTests
	{
		[Test, Apartment(ApartmentState.STA)]
		public async Task CopyButtonMakesCopyOfPart()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				SystemWindow systemWindow;

				testRunner.CloseSignInAndPrinterSelect();

				// Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");
				testRunner.Delay(1);

				testRunner.ClickByName("Row Item Calibration - Box");
				testRunner.ClickByName("Row Item Calibration - Box View Button");
				testRunner.Delay(1);

				// Get View3DWidget
				View3DWidget view3D = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3) as View3DWidget;

				// Click Edit button to make edit controls visible
				testRunner.WaitForName("3D View Copy", 3);
				Assert.AreEqual(1, view3D.Scene.Children.Count, "Should have 1 part before copy");

				testRunner.Select3DPart("Calibration - Box");

				// Click Copy button and count Scene.Children 
				testRunner.ClickByName("3D View Copy");
				testRunner.Delay(() => view3D.Scene.Children.Count == 2, 3);
				Assert.AreEqual(2, view3D.Scene.Children.Count, "Should have 2 parts after copy");

				// Click Copy button a second time and count Scene.Children
				testRunner.ClickByName("3D View Copy");
				testRunner.Delay(() => view3D.Scene.Children.Count == 3, 3);
				Assert.AreEqual(3, view3D.Scene.Children.Count, "Should have 3 parts after 2nd copy");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideWidth: 800, maxTimeToRun: 60);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task GroupAndUngroup()
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

				//Get View3DWidget and count Scene.Children before Copy button is clicked
				SystemWindow systemWindow;
				GuiWidget partPreview = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3);
				View3DWidget view3D = partPreview as View3DWidget;

				int partCountBeforeCopy = view3D.Scene.Children.Count();
				Assert.IsTrue(partCountBeforeCopy == 1);

				for (int i = 0; i <= 4; i++)
				{
					testRunner.ClickByName("3D View Copy");
					testRunner.Delay(() => view3D.Scene.Children.Count == i+2, 3);
					Assert.AreEqual(i + 2, view3D.Scene.Children.Count, $"Should have {i+2} parts after copy");
				}

				//Get MeshGroupCount before Group is clicked
				Assert.AreEqual(6, view3D.Scene.Children.Count());

				testRunner.Type("^a");

				testRunner.ClickByName("3D View Group");
				testRunner.Delay(() => view3D.Scene.Children.Count == 1, 3);
				Assert.AreEqual(1, view3D.Scene.Children.Count, $"Should have 1 parts after group");

				testRunner.ClickByName("3D View Ungroup");
				testRunner.Delay(() => view3D.Scene.Children.Count == 6, 3);
				Assert.AreEqual(6, view3D.Scene.Children.Count, $"Should have 6 parts after ungroup");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideWidth: 600);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task RemoveButtonRemovesParts()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				SystemWindow systemWindow;

				//Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");
				testRunner.Delay(1);
				testRunner.ClickByName("Row Item Calibration - Box");
				MatterControlUtilities.LibraryEditSelectedItem(testRunner);

				//Get View3DWidget and count Scene.Children before Copy button is clicked
				GuiWidget partPreview = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3);
				View3DWidget view3D = partPreview as View3DWidget;

				string copyButtonName = "3D View Copy";

				int partCountBeforeCopy = view3D.Scene.Children.Count();
				Assert.IsTrue(partCountBeforeCopy == 1);

				for (int i = 0; i <= 4; i++)
				{
					testRunner.ClickByName(copyButtonName);
					testRunner.Delay(.5);
				}

				//Get MeshGroupCount before Group is clicked
				System.Threading.Thread.Sleep(2000);
				int partsOnBedBeforeRemove = view3D.Scene.Children.Count();
				Assert.IsTrue(partsOnBedBeforeRemove == 6);

				//Check that MeshCount decreases by 1 
				testRunner.ClickByName("3D View Remove");
				System.Threading.Thread.Sleep(2000);
				int meshCountAfterRemove = view3D.Scene.Children.Count();
				Assert.IsTrue(meshCountAfterRemove == 5);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideWidth:600);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task UndoRedoCopy()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				SystemWindow systemWindow;

				// Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");
				testRunner.ClickByName("Row Item Calibration - Box");
				MatterControlUtilities.LibraryEditSelectedItem(testRunner);

				//Get View3DWidget and count Scene.Children before Copy button is clicked
				GuiWidget partPreview = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3);
				View3DWidget view3D = partPreview as View3DWidget;

				string copyButtonName = "3D View Copy";

				//Click Edit button to make edit controls visible
				testRunner.Delay(1);
				int partCountBeforeCopy = view3D.Scene.Children.Count();
				Assert.IsTrue(partCountBeforeCopy == 1);

				for (int i = 0; i <= 4; i++)
				{
					testRunner.ClickByName(copyButtonName);
					testRunner.Delay(() => view3D.Scene.Children.Count() == i + 2, 2);
					Assert.AreEqual(view3D.Scene.Children.Count(), i + 2);
				}

				testRunner.Delay(.2);

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

				for (int z = 0; z <= 4; z++)
				{
					int meshCountBeforeRedo = view3D.Scene.Children.Count();
					testRunner.ClickByName("3D View Redo");

					testRunner.Delay(
						() => meshCountBeforeRedo + 1 == view3D.Scene.Children.Count(),
						2);
					Assert.AreEqual(meshCountBeforeRedo + 1, view3D.Scene.Children.Count());
				}

				return Task.FromResult(0);	
			};

			await MatterControlUtilities.RunTest(testToRun, overrideWidth: 640);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task CopyRemoveUndoRedo()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				SystemWindow systemWindow;

				testRunner.CloseSignInAndPrinterSelect();

				// Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");
				testRunner.ClickByName("Row Item Calibration - Box", 1);
				MatterControlUtilities.LibraryEditSelectedItem(testRunner);

				// Get View3DWidget
				View3DWidget view3D = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3) as View3DWidget;

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

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideWidth: 800);
		}

		[Test, Apartment(ApartmentState.STA)]
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

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideWidth: 600);
		}
	}
}
