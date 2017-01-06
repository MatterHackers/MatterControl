using System;
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
	public class SqLiteLibraryProviderTests
	{
		[Test, Apartment(ApartmentState.STA)]
		public async Task LibraryQueueViewRefreshesOnAddItem()
		{
			AutomationTest testToRun = (testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("Library Tab", 5);

				testRunner.NavigateToFolder("Local Library Row Item Collection");
				testRunner.Wait(1);
				testRunner.ClickByName("Row Item Calibration - Box");
				testRunner.ClickByName("Row Item Calibration - Box View Button");
				testRunner.Wait(1);

				SystemWindow systemWindow;
				GuiWidget partPreview = testRunner.GetWidgetByName("View3DWidget", out systemWindow, 3);
				View3DWidget view3D = partPreview as View3DWidget;

				Assert.IsFalse(view3D.Scene.HasSelection);
				testRunner.Select3DPart("Calibration - Box");
				Assert.IsTrue(view3D.Scene.HasSelection);

				testRunner.ClickByName("3D View Copy", 3);
				// wait for the copy to finish
				testRunner.Wait(.1);
				testRunner.ClickByName("3D View Remove", 3);
				testRunner.ClickByName("Save As Menu", 3);
				testRunner.ClickByName("Save As Menu Item", 3);

				testRunner.Wait(1);

				testRunner.Type("0Test Part");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				testRunner.ClickByName("Save As Save Button", 1);

				view3D.CloseOnIdle();
				testRunner.Wait(.5);

				// ensure that it is now in the library folder (that the folder updated)
				Assert.IsTrue(testRunner.WaitForName("Row Item 0Test Part", 5), "The part we added should be in the library");

				testRunner.Wait(.5);

				return Task.FromResult(0);
			};

			await MatterControlUtilities.RunTest(testToRun, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items, overrideWidth: 600);
		}
	}
}