﻿using System.Collections.Generic;
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
using Xunit;


namespace MatterHackers.MatterControl.Tests.Automation
{
    
	public class SheetDataTests
    {
        [StaFact]
		public void Calculations()
        {
			var sheetData = new SheetData(4, 4);

            void Test(string cell, string expression, string expected)
            {
                sheetData[cell].Expression = expression;
                sheetData.Recalculate();
                Assert.Equal(expected, sheetData.GetCellValue(cell));
            }

            // simple multiply retrived upper and lower case
            Test("A1", "=4*2", "8");
            Test("a1", "=4*2", "8");

            // make sure functions are working, max in this case
            Test("a2", "=max(4, 5)", "5");

            // make sure cell references are working
            Test("a3", "=a1+a2", "13");

            // complex formulas are working
            Test("a4", "=((4+5)/3+7)/5", "2");

            // complex formulas with references are working
            Test("b1", "=(a4+a3)*.5", "7.5");

            // constants work, like pi
            Test("b2", "=pi", "3.141592653589793");

            // check that we get string data back unmodified
            Test("b3", "hello", "hello");
        }
    }

    [Collection("MatterControl.UI.Automation")]
    public class PrimitiveAndSheetsTests
	{
		public PrimitiveAndSheetsTests()
		{
			StaticData.RootPath = MatterControlUtilities.StaticDataPath;
			MatterControlUtilities.OverrideAppDataLocation(MatterControlUtilities.RootPath);
		}

		[StaFact]
		public void SheetEditorLayoutAndNavigation()
		{
			var systemWindow = new SystemWindow(800, 600)
			{
				Name = "Main Window",
			};

			InternalTextEditWidget.AddTextWidgetRightClickMenu(ApplicationController.Instance.MenuTheme);

			AutomationRunner.TimeToMoveMouse = .1;

			var sheetData = new SheetData(5, 5);
			var undoBuffer = new UndoBuffer();
			var theme = ApplicationController.Instance.Theme;
			var sheetEditor = new SheetEditorWidget(sheetData, null, undoBuffer, theme);

			systemWindow.AddChild(sheetEditor);

			AutomationRunner.ShowWindowAndExecuteTests(systemWindow, testRunner =>
			{
				testRunner.Delay(1);

				return Task.CompletedTask;
			},
			2000);
		}

		[StaFact]
		public async Task DimensionsWorkWhenNoSheet()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenPartTab();

				var primitive = "Cube";
				var primitiveName = "Row Item " + primitive;
				testRunner.DoubleClickByName(primitiveName);

				// Get View3DWidget
				View3DWidget view3D = testRunner.GetWidgetByName("View3DWidget", out var window, 3) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				testRunner.WaitForName(primitive);
				Assert.Equal(1, scene.Children.Count); //, "Should have 1 part");

                var cube = testRunner.GetObjectByName(primitive, out _) as CubeObject3D;

				Assert.Equal(20, cube.Width.Value(cube), .001);

				// Select scene object
				testRunner.Select3DPart(primitive);

				// Scale it wider
				testRunner.DragDropByName("ScaleWidthRight",
					"ScaleWidthRight",
					offsetDrag: new Point2D(0, 0),
					offsetDrop: new Point2D(0, 10));
				Assert.True(cube.Width.Value(cube) > 20.0);
				
				testRunner.ClickByName("3D View Undo");
				Assert.Equal(20, cube.Width.Value(cube), .0001);

				// try scaling by text entry
				testRunner.ClickByName("ScaleWidthLeft")
					.ClickByName("XValueDisplay")
					//.Assert(() => testRunner.GetWidgetByName("XValueDisplay", out var _).ContainsFocus, "Focus") // Sometimes, when the moves over to XValueDisplay, XValueDisplay just isn't there.
					.Type("35")
					//.Assert(() => ((CustomWidgets.InlineEditControl)testRunner.GetWidgetByName("XValueDisplay", out var _)).Value == 35, "text")
					.Type("{Enter}");
				//.WaitFor(() => 35 == cube.Width.Value(cube));

				/*if (35 != cube.Width.Value(cube))
				{
					System.Diagnostics.Debugger.Launch();
					System.Diagnostics.Debugger.Break();
				}*/

				// NOTE: Happened once: Expected: 35, But was:  20.0d
				//       This can happen when running only this test, and alt-tabbing frequently.
				//       Failure again after platform focus fix.
				//       This can happen when running only this test, and not alt-tabbing frequently.
				//       Failure can happen in original .NET Framework MatterControl testing too.
				//       Possible cause is OnMouseMove(MatterHackers.Agg.UI.MouseEventArgs) (MatterHackers.MatterControl.PartPreviewWindow.Object3DControlsLayer) not updating the hover control in time.
				Assert.Equal(35, cube.Width.Value(cube));

				testRunner.ClickByName("3D View Undo");
				Assert.Equal(20, cube.Width.Value(cube), .0001);

				// try scaling by text entry of an equation
				testRunner.ClickByName("Width Field")
					.Type("=40 + 5")
					.Type("{Enter}");

				Assert.Equal(45, cube.Width.Value(cube));

				// Select Nothing
				testRunner.ClickByName("View3DWidget");
				testRunner.SelectNone();
				Assert.Null(scene.SelectedItem);
				// and re-select the object
				testRunner.SelectAll();
				Assert.Equal(1, scene.Children.Count);
				Assert.Equal(cube, scene.SelectedItem);

				// now that has an equation in the width it should not have an x edge controls
				Assert.False(testRunner.NameExists("ScaleWidthRight", .2));

				testRunner.ClickByName("3D View Undo");
				Assert.Equal(20, cube.Width.Value(cube), .0001);

				return Task.CompletedTask;
			}, overrideWidth: 1300, maxTimeToRun: 60);
		}

		[StaFact]
		public async Task ScaleObjectWorksWithAndWithoutSheet()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.OpenPartTab();

				var primitive = "Cube";
				var primitiveName = "Row Item " + primitive;
				testRunner.DoubleClickByName(primitiveName);

				// Get View3DWidget
				View3DWidget view3D = testRunner.GetWidgetByName("View3DWidget", out _, 3) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				testRunner.WaitForName(primitive);
				Assert.Equal(1, scene.Children.Count); //, "Should have 1 part");

                var cube = testRunner.GetObjectByName(primitive, out _) as CubeObject3D;

				Assert.Equal(20, cube.Width.Value(cube));

				// Select scene object and add a scale
				testRunner.Select3DPart(primitive)
					.ClickByName("Scale Inner SplitButton")
					.ClickByName("Width Edit")
					.Type("25")
					.Type("{Enter}")
					.Delay();

				var scale = testRunner.GetObjectByName("Scale", out _) as ScaleObject3D_3;
				var scaleAabb = scale.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
				Assert.Equal(25, scaleAabb.XSize);

				// add a scale to the object

				return Task.CompletedTask;
			}, overrideWidth: 1300, maxTimeToRun: 60);
		}

		[StaFact]
		public void SheetEditorNavigationTests()
		{
			var systemWindow = new SystemWindow(800, 600)
			{
				Name = "Main Window",
			};

			InternalTextEditWidget.AddTextWidgetRightClickMenu(ApplicationController.Instance.MenuTheme);

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
			var sheetEditorWidget = new SheetEditorWidget(sheetData, null, undoBuffer, theme);

			container.AddChild(sheetEditorWidget);

			systemWindow.RunTest(testRunner =>
			{
				//testRunner.Delay(60);
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
