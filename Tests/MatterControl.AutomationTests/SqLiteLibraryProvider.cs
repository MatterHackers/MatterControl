using System.Threading;
using System.Threading.Tasks;
using MatterHackers.MatterControl.PartPreviewWindow;
using NUnit.Framework;
using TestInvoker;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation")]
	public class SqLiteLibraryProviderTests
	{
		[Test, ChildProcessTest]
		public async Task LibraryQueueViewRefreshesOnAddItem()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenPartTab()
					.AddItemToBed();

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				testRunner.WaitFor(() => scene.SelectedItem != null);
				Assert.IsNotNull(scene.SelectedItem, "Expect part selection after Add to Bed action");

				testRunner.ClickByName("Duplicate Button")
					// wait for the copy to finish
					.Delay(.1)
					.ClickByName("Remove Button")
					.SaveBedplateToFolder("0Test Part", "Local Library Row Item Collection")
					// Click Home -> Local Library
					.NavigateToLibraryHome()
					.NavigateToFolder("Local Library Row Item Collection");

				// ensure that it is now in the library folder (that the folder updated)
				Assert.IsTrue(testRunner.WaitForName("Row Item 0Test Part"), "The part we added should be in the library");

				testRunner.Delay(.5);

				return Task.CompletedTask;
			}, queueItemFolderToAdd: QueueTemplate.Three_Queue_Items, overrideWidth: 1300);
		}
	}
}