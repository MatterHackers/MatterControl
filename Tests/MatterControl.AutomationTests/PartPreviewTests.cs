using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PartPreviewWindow;
using NUnit.Framework;
using System;
using System.Linq;


namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class PartPreviewTests
	{
		[Test, RequiresSTA, RunInApplicationDomain]
		public void CopyButtonClickedMakesCopyOfPartOnBed()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					SystemWindow systemWindow;

					//Navigate to Local Library 
					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");
					testRunner.Wait(1);
					testRunner.ClickByName("Row Item Calibration - Box");
					testRunner.ClickByName("Row Item Calibration - Box View Button");
					testRunner.Wait(1);

					//Get View3DWidget and count MeshGroups before Copy button is clicked
					GuiWidget partPreview = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3);
					View3DWidget view3D = partPreview as View3DWidget;

					string copyButtonName = "3D View Copy";
					
					//Click Edit button to make edit controls visible
					testRunner.ClickByName("3D View Edit");
					testRunner.Wait(1);
					int partCountBeforeCopy = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(partCountBeforeCopy == 1);
					
					
					//Click Copy button and count MeshGroups 
					testRunner.ClickByName(copyButtonName);
					System.Threading.Thread.Sleep(500);
					int partCountAfterCopy = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(partCountAfterCopy == 2);
					testRunner.Wait(1);

					//Click Copy button a second time and count MeshGroups again
					testRunner.ClickByName(copyButtonName);
					System.Threading.Thread.Sleep(500);
					int partCountAfterSecondCopy = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(partCountAfterSecondCopy == 3);
					view3D.CloseOnIdle();
					testRunner.Wait(.5);


					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed(3));
		}

		[Test, RequiresSTA, RunInApplicationDomain]
		public void GroupAndUngroup()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					SystemWindow systemWindow;

					//Navigate to Local Library 
					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");
					testRunner.Wait(1);
					testRunner.ClickByName("Row Item Calibration - Box");
					MatterControlUtilities.LibraryEditSelectedItem(testRunner);
					testRunner.Wait(1);

					//Get View3DWidget and count MeshGroups before Copy button is clicked
					GuiWidget partPreview = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3);
					View3DWidget view3D = partPreview as View3DWidget;

					string copyButtonName = "3D View Copy";

					//Click Edit button to make edit controls visible
					testRunner.ClickByName("3D View Edit");
					testRunner.Wait(1);
					int partCountBeforeCopy = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(partCountBeforeCopy == 1);

					for (int i = 0; i <= 4; i++)
						{
							testRunner.ClickByName(copyButtonName);
							testRunner.Wait(1);
						}
					
					//Get MeshGroupCount before Group is clicked
					System.Threading.Thread.Sleep(2000);
					int partsOnBedBeforeGroup = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(partsOnBedBeforeGroup == 6);

					//Click Group Button and get MeshGroup count after Group button is clicked
					testRunner.ClickByName("3D View Group");
					System.Threading.Thread.Sleep(2000);
					int partsOnBedAfterGroup = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(partsOnBedAfterGroup == 1);

					testRunner.ClickByName("3D View Ungroup");
					System.Threading.Thread.Sleep(2000);
					int partsOnBedAfterUngroup = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(partsOnBedAfterUngroup == 6);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed(4));
		}

		[Test, RequiresSTA, RunInApplicationDomain]
		public void RemoveButtonRemovesParts()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					SystemWindow systemWindow;

					//Navigate to Local Library 
					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");
					testRunner.Wait(1);
					testRunner.ClickByName("Row Item Calibration - Box");
					MatterControlUtilities.LibraryEditSelectedItem(testRunner);
					testRunner.Wait(1);

					//Get View3DWidget and count MeshGroups before Copy button is clicked
					GuiWidget partPreview = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3);
					View3DWidget view3D = partPreview as View3DWidget;

					string copyButtonName = "3D View Copy";

					//Click Edit button to make edit controls visible
					testRunner.ClickByName("3D View Edit");
					testRunner.Wait(1);
					int partCountBeforeCopy = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(partCountBeforeCopy == 1);

					for (int i = 0; i <= 4; i++)
					{
						testRunner.ClickByName(copyButtonName);
						testRunner.Wait(1);
					}

					//Get MeshGroupCount before Group is clicked
					System.Threading.Thread.Sleep(2000);
					int partsOnBedBeforeRemove= view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(partsOnBedBeforeRemove == 6);

					//Check that MeshCount decreases by 1 
					testRunner.ClickByName("3D View Remove");
					System.Threading.Thread.Sleep(2000);
					int meshCountAfterRemove = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(meshCountAfterRemove == 5);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed(3));
		}

		[Test, RequiresSTA, RunInApplicationDomain, Ignore("Not Finished")]
		public void UndoRedoCopy()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					SystemWindow systemWindow;

					//Navigate to Local Library 
					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");
					testRunner.Wait(1);
					testRunner.ClickByName("Row Item Calibration - Box");
					MatterControlUtilities.LibraryEditSelectedItem(testRunner);
					testRunner.Wait(1);

					//Get View3DWidget and count MeshGroups before Copy button is clicked
					GuiWidget partPreview = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3);
					View3DWidget view3D = partPreview as View3DWidget;

					string copyButtonName = "3D View Copy";

					//Click Edit button to make edit controls visible
					testRunner.ClickByName("3D View Edit");
					testRunner.Wait(1);
					int partCountBeforeCopy = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(partCountBeforeCopy == 1);

					for (int i = 0; i <= 4; i++)
					{
						testRunner.ClickByName(copyButtonName);
						testRunner.Wait(1);
					}

					testRunner.Wait(1);

					for(int x = 0; x <= 4; x++)
					{

						int meshCountBeforeUndo = view3D.MeshGroups.Count();
						testRunner.ClickByName("3D View Undo");
						System.Threading.Thread.Sleep(2000);
						int meshCountAfterUndo = view3D.MeshGroups.Count();
						resultsHarness.AddTestResult(meshCountAfterUndo == meshCountBeforeUndo - 1);
						
					}

					testRunner.Wait(1);

					for(int z = 0; z <= 4; z++)
					{
						int meshCountBeforeRedo = view3D.MeshGroups.Count();
						testRunner.ClickByName("3D View Redo");
						System.Threading.Thread.Sleep(2000);
						int meshCountAfterRedo = view3D.MeshGroups.Count();
						resultsHarness.AddTestResult(meshCountAfterRedo == meshCountBeforeRedo + 1);

					}



					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed(11));
		}

		[Test, RequiresSTA, RunInApplicationDomain]
		public void UndoRedoDelete()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					SystemWindow systemWindow;

					//Navigate to Local Library 
					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");
					testRunner.ClickByName("Row Item Calibration - Box", 1);
					MatterControlUtilities.LibraryEditSelectedItem(testRunner);

					//Get View3DWidget and count MeshGroups before Copy button is clicked
					GuiWidget partPreview = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3);
					View3DWidget view3D = partPreview as View3DWidget;

					string copyButtonName = "3D View Copy";

					//Click Edit button to make edit controls visible
					testRunner.ClickByName("3D View Edit", 1);
					int partCountBeforeCopy = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(partCountBeforeCopy == 1);
					testRunner.Wait(.5);

					for (int i = 0; i <= 4; i++)
					{
						testRunner.ClickByName(copyButtonName, 1);
						testRunner.Wait(.2);
						int meshCount = view3D.MeshGroups.Count();
						resultsHarness.AddTestResult(meshCount == partCountBeforeCopy + i + 1);
					}

					int meshCountAfterCopy = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(meshCountAfterCopy == 6);
					testRunner.ClickByName("3D View Remove", 1);
					testRunner.Wait(.1);
					int meshCountAfterRemove = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(meshCountAfterRemove == 5);

					testRunner.ClickByName("3D View Undo");
					System.Threading.Thread.Sleep(2000);
					int meshCountAfterUndo = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(meshCountAfterUndo == 6);

					testRunner.ClickByName("3D View Redo");
					System.Threading.Thread.Sleep(2000);
					int meshCountAfterRedo = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(meshCountAfterRedo == 5);

					partPreview.CloseOnIdle();
					testRunner.Wait(.1);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun, overrideWidth: 800);

			Assert.IsTrue(testHarness.AllTestsPassed(10));
		}

		[Test, RequiresSTA, RunInApplicationDomain]
		public void SaveAsToQueue()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					//Navigate to Local Library 
					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");
					testRunner.Wait(1);
					testRunner.ClickByName("Row Item Calibration - Box");
					MatterControlUtilities.LibraryEditSelectedItem(testRunner);
					testRunner.Wait(1);

					//Click Edit button to make edit controls visible
					testRunner.ClickByName("3D View Edit");
					testRunner.Wait(1);

					SystemWindow systemWindow;
					GuiWidget partPreview = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3);
					View3DWidget view3D = partPreview as View3DWidget;

					for (int i = 0; i <= 2; i++)
					{
						testRunner.ClickByName("3D View Copy");
						testRunner.Wait(1);
					}

					//Click Save As button to save changes to the part
					testRunner.ClickByName("Save As Menu");
					testRunner.Wait(1);
					testRunner.ClickByName("Save As Menu Item");
					testRunner.Wait(1);

					//Type in name of new part and then save to Print Queue
					testRunner.Type("Save As Print Queue");
					MatterControlUtilities.NavigateToFolder(testRunner,"Print Queue Row Item Collection");
					testRunner.Wait(1);
					testRunner.ClickByName("Save As Save Button");

					view3D.CloseOnIdle();
					testRunner.Wait(.5);

					//Make sure there is a new Queue item with a name that matches the new part
					testRunner.Wait(1);
					testRunner.ClickByName("Queue Tab");
					testRunner.Wait(1);
					resultsHarness.AddTestResult(testRunner.WaitForName("Queue Item Save As Print Queue", 5));

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed(1));
		}
	}
}
