using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class PrimitiveAndSheetsTests
	{
		[SetUp]
		public void TestSetup()
		{
			StaticData.RootPath = TestContext.CurrentContext.ResolveProjectPath(4, "StaticData");
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));
		}

		[Test]
		public void SheetEditorLayoutAndNavigation()
		{
			var systemWindow = new SystemWindow(800, 600)
			{
				Name = "Main Window",
			};

			Application.AddTextWidgetRightClickMenu();

			AutomationRunner.TimeToMoveMouse = .1;

			var sheetData = new SheetData(5, 5);
			var undoBuffer = new UndoBuffer();
			var theme = ApplicationController.Instance.Theme;
			var sheetEditor = new SheetEditorWidget(sheetData, undoBuffer, theme);

			systemWindow.AddChild(sheetEditor);

			AutomationRunner.ShowWindowAndExecuteTests(systemWindow, testRunner =>
			{
				testRunner.Delay(100);

				return Task.CompletedTask;
			},
			2000);
		}

		[Test]
		public async Task DimensionsWorkWhenNoSheet()
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
				testRunner.SelectNone();
				Assert.AreEqual(null, scene.SelectedItem);
				// and re-select the object
				testRunner.SelectAll();
				Assert.AreEqual(1, scene.Children.Count);
				Assert.AreEqual(cube, scene.SelectedItem);

				// now that has an equation in the width it should not have an x edge controls
				Assert.IsFalse(testRunner.NameExists("ScaleWidthRight", .2));

				testRunner.ClickByName("3D View Undo");
				Assert.AreEqual(20, cube.Width.Value(cube));

				return Task.CompletedTask;
			}, overrideWidth: 1300, maxTimeToRun: 60);
		}

		[Test]
		public async Task ScaleObjectWorksWithAndWithoutSheet()
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

				// Select scene object and add a scale
				testRunner.Select3DPart(primitive)
					.ClickByName("Scale Inner SplitButton")
					.ClickByName("Width Edit")
					.Type("25")
					.Type("{Enter}")
					.Delay();

				var scale = testRunner.GetObjectByName("Scale", out _) as ScaleObject3D_3;
				var scaleAabb = scale.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
				Assert.AreEqual(25, scaleAabb.XSize);

				// add a scale to the object

				return Task.CompletedTask;
			}, overrideWidth: 1300, maxTimeToRun: 60);
		}

		[Test]
		public void SheetEditorNavigationTests()
		{
			var systemWindow = new SystemWindow(800, 600)
			{
				Name = "Main Window",
			};

			Application.AddTextWidgetRightClickMenu();

			AutomationRunner.TimeToMoveMouse = .1;

			var theme = ApplicationController.Instance.Theme;
			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			systemWindow.AddChild(container);

			var sheetData = new SheetData(5, 5);
			var undoBuffer = new UndoBuffer();
			var sheetEditorWidget = new SheetEditorWidget(sheetData, undoBuffer, theme);

			container.AddChild(sheetEditorWidget);

			systemWindow.RunTest(testRunner =>
			{
				testRunner.Delay(60);
				return Task.CompletedTask;
			},
			2000);
		}
	}

	public static class RunnerX
	{
		public static Task RunTest(this SystemWindow systemWindow, AutomationTest automationTest, int timeout)
		{
			return AutomationRunner.ShowWindowAndExecuteTests(systemWindow, automationTest, timeout);
		}
	}
}
