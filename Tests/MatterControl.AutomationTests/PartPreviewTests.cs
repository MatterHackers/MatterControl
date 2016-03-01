using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.PolygonMesh;
using MatterHackers.Agg.UI;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.MatterControl.PartPreviewWindow;
using System.IO;
using MatterHackers.MatterControl.CreatorPlugins;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.DataStorage;
using System.Diagnostics;


namespace MatterHackers.MatterControl.UI
{
	[TestFixture, Category("MatterControl.UI"), RunInApplicationDomain]
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

					SystemWindow systemWindow;

					//Navigate to Local Library 
					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");
					testRunner.Wait(1);
					testRunner.ClickByName("Row Item Calibration - Box");
					testRunner.ClickByName("Row Item Calibration - Box Print Button");
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
					System.Threading.Thread.Sleep(4000);
					int partCountAfterCopy = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(partCountAfterCopy == 2);
					testRunner.Wait(1);

					//Click Copy button a second time and count MeshGroups again
					testRunner.ClickByName(copyButtonName);
					System.Threading.Thread.Sleep(4000);
					int partCountAfterSecondCopy = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(partCountAfterSecondCopy == 3);


					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 3); // make sure we ran all our tests
		}

		[Test, RequiresSTA, RunInApplicationDomain]
		public void GroupAndUngroup()
		{
			// Run a copy of MatterControl
			Action<AutomationTesterHarness> testToRun = (AutomationTesterHarness resultsHarness) =>
			{
				AutomationRunner testRunner = new AutomationRunner(MatterControlUtilities.DefaultTestImages);
				{

					SystemWindow systemWindow;

					//Navigate to Local Library 
					testRunner.ClickByName("Library Tab");
					MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");
					testRunner.Wait(1);
					testRunner.ClickByName("Row Item Calibration - Box");
					testRunner.ClickByName("Row Item Calibration - Box Print Button");
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
					System.Threading.Thread.Sleep(4000);
					int partsOnBedBeforeGroup = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(partsOnBedBeforeGroup == 6);

					//Click Group Button and get MeshGroup count after Group button is clicked
					testRunner.ClickByName("3D View Group");
					System.Threading.Thread.Sleep(4000);
					int partsOnBedAfterGroup = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(partsOnBedAfterGroup == 1);

					testRunner.ClickByName("3D View Ungroup");
					System.Threading.Thread.Sleep(4000);
					int partsOnBedAfterUngroup = view3D.MeshGroups.Count();
					resultsHarness.AddTestResult(partsOnBedAfterUngroup == 6);

					MatterControlUtilities.CloseMatterControl(testRunner);
				}
			};

			AutomationTesterHarness testHarness = MatterControlUtilities.RunTest(testToRun);

			Assert.IsTrue(testHarness.AllTestsPassed);
			Assert.IsTrue(testHarness.TestCount == 4); // make sure we ran all our tests
		}
	}
}
