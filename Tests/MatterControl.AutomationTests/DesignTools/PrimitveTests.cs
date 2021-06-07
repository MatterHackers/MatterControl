using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools;
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

				var primitive = "Cube";
				var primitiveName = "Row Item " + primitive;
				testRunner.DoubleClickByName(primitiveName);

				// Get View3DWidget
				View3DWidget view3D = testRunner.GetWidgetByName("View3DWidget", out _, 3) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				testRunner.WaitForName(primitive);
				Assert.AreEqual(1, scene.Children.Count, "Should have 1 part");

				var cube = testRunner.GetObjectByName(primitive, out _) as CubeObject3D;

				Assert.AreEqual(20, cube.Width.Value(cube));

				// Select scene object
				testRunner.Select3DPart(primitive);

				// Scale it wider
				testRunner.DragDropByName("ScaleWidthRight",
					"ScaleWidthRight",
					offsetDrag: new Point2D(0, 0),
					offsetDrop: new Point2D(0, 10));
				Assert.Greater(cube.Width.Value(cube), 20.0);
				
				testRunner.ClickByName("3D View Undo");
				Assert.AreEqual(20, cube.Width.Value(cube));

				// try scaling by text entry
				testRunner.ClickByName("ScaleWidthLeft")
					.ClickByName("XValueDisplay")
					.Type("35")
					.Type("{Enter}");

				Assert.AreEqual(35, cube.Width.Value(cube));

				testRunner.ClickByName("3D View Undo");
				Assert.AreEqual(20, cube.Width.Value(cube));

				// try scaling by text entry of an equation
				testRunner.ClickByName("Width Field")
					.Type("=40 + 5")
					.Type("{Enter}");

				Assert.AreEqual(45, cube.Width.Value(cube));

				// Select Nothing
				testRunner.ClickByName("View3DWidget");
				testRunner.Type(" ");
				Assert.AreEqual(null, scene.SelectedItem);
				// and re-select the object
				testRunner.Type("^a");
				Assert.AreEqual(1, scene.Children.Count);
				Assert.AreEqual(cube, scene.SelectedItem);

				// now that has an equation in the width it should not have an x edge controls
				Assert.IsFalse(testRunner.NameExists("ScaleWidthRight", .2));

				testRunner.ClickByName("3D View Undo");
				Assert.AreEqual(20, cube.Width.Value(cube));

				return Task.CompletedTask;
			}, overrideWidth: 1300, maxTimeToRun: 60);
		}
	}
}
