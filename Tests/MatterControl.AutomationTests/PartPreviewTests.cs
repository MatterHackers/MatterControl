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
				testRunner.Wait(1);

				testRunner.ClickByName("Row Item Calibration - Box");
				testRunner.ClickByName("Row Item Calibration - Box View Button");
				testRunner.Wait(1);

				// Get View3DWidget
				View3DWidget view3D = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3) as View3DWidget;

				// Click Edit button to make edit controls visible
				testRunner.ClickByName("3D View Edit");
				testRunner.WaitForName("3D View Copy", 3);
				Assert.AreEqual(1, view3D.MeshGroups.Count, "Should have 1 part before copy");


				// Click Copy button and count MeshGroups 
				testRunner.ClickByName("3D View Copy");
				AutomationRunner.WaitUntil(() => view3D.MeshGroups.Count == 2, 3);
				Assert.AreEqual(2, view3D.MeshGroups.Count, "Should have 2 parts after copy");

				// Click Copy button a second time and count MeshGroups
				testRunner.ClickByName("3D View Copy");
				AutomationRunner.WaitUntil(() => view3D.MeshGroups.Count == 3, 3);
				Assert.AreEqual(3, view3D.MeshGroups.Count, "Should have 3 parts after 2nd copy");

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideWidth: 800);
		}

		[Test, Apartment(ApartmentState.STA)]
		public async Task GroupAndUngroup()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				SystemWindow systemWindow;

				//Navigate to Local Library 
				testRunner.ClickByName("Library Tab");
				testRunner.NavigateToFolder("Local Library Row Item Collection");
				testRunner.Wait(1);
				testRunner.ClickByName("Row Item Calibration - Box");
				MatterControlUtilities.LibraryEditSelectedItem(testRunner);

				//Get View3DWidget and count MeshGroups before Copy button is clicked
				GuiWidget partPreview = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3);
				View3DWidget view3D = partPreview as View3DWidget;

				string copyButtonName = "3D View Copy";

				int partCountBeforeCopy = view3D.MeshGroups.Count;
				Assert.IsTrue(partCountBeforeCopy == 1);

				for (int i = 0; i <= 4; i++)
				{
					testRunner.ClickByName(copyButtonName);
					testRunner.Wait(1);
				}

				//Get MeshGroupCount before Group is clicked
				System.Threading.Thread.Sleep(2000);
				int partsOnBedBeforeGroup = view3D.MeshGroups.Count;
				Assert.IsTrue(partsOnBedBeforeGroup == 6);

				//Click Group Button and get MeshGroup count after Group button is clicked
				testRunner.ClickByName("3D View Group");
				System.Threading.Thread.Sleep(2000);
				int partsOnBedAfterGroup = view3D.MeshGroups.Count;
				Assert.IsTrue(partsOnBedAfterGroup == 1);

				testRunner.ClickByName("3D View Ungroup");
				System.Threading.Thread.Sleep(2000);
				int partsOnBedAfterUngroup = view3D.MeshGroups.Count;
				Assert.IsTrue(partsOnBedAfterUngroup == 6);

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
				testRunner.Wait(1);
				testRunner.ClickByName("Row Item Calibration - Box");
				MatterControlUtilities.LibraryEditSelectedItem(testRunner);

				//Get View3DWidget and count MeshGroups before Copy button is clicked
				GuiWidget partPreview = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3);
				View3DWidget view3D = partPreview as View3DWidget;

				string copyButtonName = "3D View Copy";

				int partCountBeforeCopy = view3D.MeshGroups.Count;
				Assert.IsTrue(partCountBeforeCopy == 1);

				for (int i = 0; i <= 4; i++)
				{
					testRunner.ClickByName(copyButtonName);
					testRunner.Wait(.5);
				}

				//Get MeshGroupCount before Group is clicked
				System.Threading.Thread.Sleep(2000);
				int partsOnBedBeforeRemove = view3D.MeshGroups.Count;
				Assert.IsTrue(partsOnBedBeforeRemove == 6);

				//Check that MeshCount decreases by 1 
				testRunner.ClickByName("3D View Remove");
				System.Threading.Thread.Sleep(2000);
				int meshCountAfterRemove = view3D.MeshGroups.Count;
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

				// Get View3DWidget and count MeshGroups before Copy button is clicked
				View3DWidget view3D = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3) as View3DWidget;

				for (int i = 0; i <= 4; i++)
				{
					testRunner.ClickByName("3D View Copy");
					AutomationRunner.WaitUntil(() => view3D.MeshGroups.Count == i + 2, 2);
					Assert.AreEqual(i + 2, view3D.MeshGroups.Count);
				}

				testRunner.Wait(.2);

				for (int x = 0; x <= 4; x++)
				{
					int meshCountBeforeUndo = view3D.MeshGroups.Count;
					testRunner.ClickByName("3D View Undo");
					AutomationRunner.WaitUntil(() => view3D.MeshGroups.Count == meshCountBeforeUndo - 1, 2);
					Assert.AreEqual(meshCountBeforeUndo - 1, view3D.MeshGroups.Count);
				}

				testRunner.Wait(.2);

				for (int z = 0; z <= 4; z++)
				{
					int meshCountBeforeRedo = view3D.MeshGroups.Count;
					testRunner.ClickByName("3D View Redo");
					AutomationRunner.WaitUntil(() => view3D.MeshGroups.Count == meshCountBeforeRedo + 1, 2);
					Assert.AreEqual(meshCountBeforeRedo + 1, view3D.MeshGroups.Count);
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
				testRunner.Wait(1); // wait for window to finish opening
				Assert.AreEqual(1, view3D.MeshGroups.Count, "Should have 1 part before copy");

				for (int i = 0; i <= 4; i++)
				{
					testRunner.ClickByName("3D View Copy", 1);
					testRunner.Wait(.2);
				}

				Assert.AreEqual(6, view3D.MeshGroups.Count, "Should have 6 parts after batch copy");

				testRunner.ClickByName("3D View Remove", 1);
				AutomationRunner.WaitUntil(() => view3D.MeshGroups.Count == 5, 3);
				Assert.AreEqual(5, view3D.MeshGroups.Count, "Should have 5 parts after Remove");

				testRunner.ClickByName("3D View Undo");
				AutomationRunner.WaitUntil(() => view3D.MeshGroups.Count == 6, 3);
				Assert.AreEqual(6, view3D.MeshGroups.Count, "Should have 6 parts after Undo");

				testRunner.ClickByName("3D View Redo");
				AutomationRunner.WaitUntil(() => view3D.MeshGroups.Count == 5, 3);
				Assert.AreEqual(5, view3D.MeshGroups.Count, "Should have 5 parts after Redo");

				view3D.CloseOnIdle();
				testRunner.Wait(.1);

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
				testRunner.Wait(1);
				testRunner.ClickByName("Row Item Calibration - Box");
				MatterControlUtilities.LibraryEditSelectedItem(testRunner);

				SystemWindow systemWindow;
				GuiWidget partPreview = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3);
				View3DWidget view3D = partPreview as View3DWidget;

				for (int i = 0; i <= 2; i++)
				{
					testRunner.ClickByName("3D View Copy");
					testRunner.Wait(.5);
				}

				//Click Save As button to save changes to the part
				testRunner.ClickByName("Save As Menu");
				testRunner.Wait(1);
				testRunner.ClickByName("Save As Menu Item");
				testRunner.Wait(1);

				//Type in name of new part and then save to Print Queue
				testRunner.Type("Save As Print Queue");
				testRunner.NavigateToFolder("Print Queue Row Item Collection");
				testRunner.Wait(1);
				testRunner.ClickByName("Save As Save Button");

				view3D.CloseOnIdle();
				testRunner.Wait(.5);

				//Make sure there is a new Queue item with a name that matches the new part
				testRunner.Wait(1);
				testRunner.ClickByName("Queue Tab");
				testRunner.Wait(1);
				Assert.IsTrue(testRunner.WaitForName("Queue Item Save As Print Queue", 5));

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, overrideWidth: 600);
		}
	}
}
