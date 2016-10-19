using System;
using System.Threading;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PartPreviewWindow;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain]
	public class SqLiteLibraryProviderTests
	{
		[Test, Apartment(ApartmentState.STA), RunInApplicationDomain]
		public void LibraryQueueViewRefreshesOnAddItem()
		{
			// Run a copy of MatterControl
			Action<AutomationRunner> testToRun = (AutomationRunner testRunner) =>
			{
				{
					MatterControlUtilities.PrepForTestRun(testRunner);

					testRunner.ClickByName("Library Tab", 5);

					MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection");
					testRunner.Wait(1);
					testRunner.ClickByName("Row Item Calibration - Box");
					testRunner.ClickByName("Row Item Calibration - Box View Button");
					testRunner.Wait(1);

					SystemWindow systemWindow;
					GuiWidget partPreview = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3);
					View3DWidget view3D = partPreview as View3DWidget;

					testRunner.AddTestResult(testRunner.ClickByName("3D View Edit", 3));

					testRunner.AddTestResult(testRunner.ClickByName("3D View Copy", 3), "Click Copy");
					// wait for the copy to finish
					testRunner.Wait(.1);
					testRunner.AddTestResult(testRunner.ClickByName("3D View Remove", 3), "Click Delete");
					testRunner.AddTestResult(testRunner.ClickByName("Save As Menu", 3), "Click Save As Menu");
					testRunner.AddTestResult(testRunner.ClickByName("Save As Menu Item", 3), "Click Save As");

					testRunner.Wait(1);

					testRunner.Type("0Test Part");
					testRunner.AddTestResult(MatterControlUtilities.NavigateToFolder(testRunner, "Local Library Row Item Collection"));

					testRunner.AddTestResult(testRunner.ClickByName("Save As Save Button", 1));

					view3D.CloseOnIdle();
					testRunner.Wait(.5);

					// ensure that it is now in the library folder (that the folder updated)
					testRunner.AddTestResult(testRunner.WaitForName("Row Item " + "0Test Part", 5), "The part we added should be in the library");

					testRunner.Wait(.5);

					MatterControlUtilities.CloseMatterControl(testRunner); 
				}
			};

			AutomationRunner testHarness = MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items, defaultTestImages: MatterControlUtilities.DefaultTestImages);

			Assert.IsTrue(testHarness.AllTestsPassed(8));
		}
	}
}