using System.Threading;
using System.Threading.Tasks;
using MatterHackers.MatterControl.PartPreviewWindow;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class SqLiteLibraryProviderTests
	{
		[Test]
		public async Task LibraryQueueViewRefreshesOnAddItem()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.CloseSignInAndPrinterSelect();

				testRunner.ClickByName("Library Tab");

				testRunner.AddDefaultFileToBedplate();

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;
				Assert.IsFalse(view3D.Scene.HasSelection);

				testRunner.Select3DPart("Calibration - Box.stl");
				Assert.IsTrue(view3D.Scene.HasSelection);

				testRunner.ClickByName("3D View Copy");

				// wait for the copy to finish
				testRunner.Delay(.1);
				testRunner.ClickByName("3D View Remove");
				testRunner.ClickByName("Save As Menu");
				testRunner.ClickByName("Save As Menu Item");

				testRunner.Delay(1);

				testRunner.Type("0Test Part");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				testRunner.ClickByName("Save As Save Button");

				// Give the SaveAs window time to close to ensure we click the right Home button
				testRunner.Delay(2);

				// Click Home -> Local Library
				testRunner.ClickByName("Bread Crumb Button Home");
				testRunner.NavigateToFolder("Local Library Row Item Collection");

				// ensure that it is now in the library folder (that the folder updated)
				Assert.IsTrue(testRunner.WaitForName("Row Item 0Test Part", 5), "The part we added should be in the library");

				testRunner.Delay(.5);

				return Task.CompletedTask;
			}, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items, overrideWidth: 1300);
		}
	}
}