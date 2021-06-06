using System.Threading;
using System.Threading.Tasks;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintQueue;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class PrimitiveAndSheetsTests
	{
		[Test]
		public async Task DimensionsWorkWithSheets()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenEmptyPartTab();

				testRunner.AddItemToBedplate();

				// Get View3DWidget
				View3DWidget view3D = testRunner.GetWidgetByName("View3DWidget", out _, 3) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				testRunner.WaitForName("Calibration - Box.stl");
				Assert.AreEqual(1, scene.Children.Count, "Should have 1 part before copy");

				// Select scene object
				testRunner.Select3DPart("Calibration - Box.stl");

				// Click Copy button and count Scene.Children
				testRunner.ClickByName("Duplicate Button");
				testRunner.WaitFor(() => scene.Children.Count == 2);
				Assert.AreEqual(2, scene.Children.Count, "Should have 2 parts after copy");

				// Click Copy button a second time and count Scene.Children
				testRunner.ClickByName("Duplicate Button");
				testRunner.WaitFor(() => scene.Children.Count > 2);
				Assert.AreEqual(3, scene.Children.Count, "Should have 3 parts after 2nd copy");

				return Task.CompletedTask;
			}, overrideWidth: 1300, maxTimeToRun: 60);
		}
	}
}
