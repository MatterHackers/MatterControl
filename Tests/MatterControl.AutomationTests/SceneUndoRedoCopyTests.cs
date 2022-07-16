/*
Copyright (c) 2022, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using NUnit.Framework;
using TestInvoker;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("MatterControl.UI.Automation"), Parallelizable(ParallelScope.Children)]
	public class SceneUndoRedoCopyTests
	{
		private const string CoinName = "MatterControl - Coin.stl";

		[SetUp]
		public void TestSetup()
		{
			StaticData.RootPath = MatterControlUtilities.StaticDataPath;
			MatterControlUtilities.OverrideAppDataLocation(MatterControlUtilities.RootPath);
		}

		[Test, ChildProcessTest]
		public async Task CopyRemoveUndoRedo()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.OpenPartTab();

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				// Initialize
				testRunner.AddItemToBed();

				testRunner.Select3DPart("Calibration - Box.stl")
					.WaitForName("Duplicate Button");
				Assert.AreEqual(1, scene.Children.Count, "Should have 1 part before copy");

				for (int i = 0; i <= 4; i++)
				{
					testRunner.ClickByName("Duplicate Button");
					testRunner.WaitFor(() => scene.Children.Count == i + 2);
					Assert.AreEqual(i + 2, scene.Children.Count);
				}

				testRunner.ClickByName("Remove Button");
				testRunner.WaitFor(() => scene.Children.Count == 5);
				Assert.AreEqual(5, scene.Children.Count, "Should have 5 parts after Remove");

				testRunner.ClickByName("3D View Undo");
				testRunner.WaitFor(() => scene.Children.Count == 6);
				Assert.AreEqual(6, scene.Children.Count, "Should have 6 parts after Undo");

				testRunner.ClickByName("3D View Redo");
				testRunner.WaitFor(() => scene.Children.Count == 5);
				Assert.AreEqual(5, scene.Children.Count, "Should have 5 parts after Redo");

				return Task.CompletedTask;
			}, overrideWidth: 1300);
		}

		[Test, ChildProcessTest]
		public async Task UndoRedoCopy()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.OpenPartTab()
					.AddItemToBed();

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				testRunner.Select3DPart("Calibration - Box.stl");

				Assert.AreEqual(1, scene.Children.Count, "There should be 1 part on the bed after AddDefaultFileToBedplate()");

				// Add 5 items
				for (int i = 0; i <= 4; i++)
				{
					testRunner.ClickByName("Duplicate Button");
					testRunner.Delay(.5);
				}

				Assert.AreEqual(6, scene.Children.Count, "There should be 6 parts on the bed after the copy loop");

				// Perform and validate 5 undos
				for (int x = 0; x <= 4; x++)
				{
					int meshCountBeforeUndo = scene.Children.Count;
					testRunner.ClickByName("3D View Undo");

					testRunner.WaitFor(() => scene.Children.Count == meshCountBeforeUndo - 1);
					Assert.AreEqual(scene.Children.Count, meshCountBeforeUndo - 1);
				}

				testRunner.Delay(.2);

				// Perform and validate 5 redoes
				for (int z = 0; z <= 4; z++)
				{
					int meshCountBeforeRedo = scene.Children.Count;
					testRunner.ClickByName("3D View Redo");

					testRunner.WaitFor(() => meshCountBeforeRedo + 1 == scene.Children.Count);
					Assert.AreEqual(meshCountBeforeRedo + 1, scene.Children.Count);
				}

				return Task.CompletedTask;
			}, overrideWidth: 1300);
		}

		[Test, ChildProcessTest]
		public async Task ValidateDoUndoOnUnGroupSingleMesh()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.OpenPartTab();

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				// Initialize
				testRunner.AddItemToBed(partName: "Row Item MH Logo.stl")
					.Delay(.1)
					.ClickByName("MH Logo.stl");
				Assert.IsNotNull(scene.SelectedItem);
				testRunner.WaitFor(() => scene.Children.Count() == 1);
				Assert.AreEqual(1, scene.Children.Count());

				// test un-group single mesh
				testRunner.RunDoUndoTest(
					scene,
					() =>
					{
						// Set focus
						testRunner.ClickByName("View3DWidget")
							// Ungroup
							.SelectAll()
							.ClickByName("Ungroup Button")
							// Blur
							.SelectNone()
							// Assert
							.WaitFor(() => scene.Children.Count() == 3);
						Assert.AreEqual(3, scene.Children.Count());
					});

				return Task.CompletedTask;
			}, overrideWidth: 1300);
		}

		[Test, ChildProcessTest]
		public async Task ValidateDoUndoOnGroup2Items()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.OpenPartTab();

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				// Initialize
				AddBoxABoxBToBed(testRunner, scene);
				Assert.AreEqual(2, scene.Children.Count());

				// test group 2 objects
				testRunner.RunDoUndoTest(
					scene,
					() =>
					{
						// Select all
						testRunner.ClickByName("View3DWidget")
							.SelectAll()
							// Group items
							.ClickByName("Group Button")
							// Clear selection
							.SelectNone()
							// Assert
							.WaitFor(() => scene.SelectedItem == null)
							.WaitFor(() => scene.Children.Count() == 1);
						Assert.AreEqual(1, scene.Children.Count());
					});

				return Task.CompletedTask;
			}, overrideWidth: 1300);
		}

		[Test, ChildProcessTest]
		public async Task ValidateDoUndoUnGroup2Items()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.OpenPartTab();

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				// Initialize
				AddBoxABoxBToBed(testRunner, scene);
				Assert.AreEqual(2, scene.Children.Count());
				Assert.AreEqual(3, scene.DescendantsAndSelf().Count(), "The scene and the 2 objects");

				// Select all
				testRunner.ClickByName("View3DWidget")
					.SelectAll()
					// Group
					.ClickByName("Group Button")
					// Blur
					.SelectNone()
					.WaitFor(() => scene.Children.Count() == 1);
				Assert.AreEqual(1, scene.Children.Count());
				// group object can now process holes so it is a source object and has an extra object in it.
				Assert.AreEqual(5, scene.DescendantsAndSelf().Count(), "The scene, the group and the 2 objects");

				// test un-group 2 grouped objects
				testRunner.RunDoUndoTest(
					scene,
					() =>
					{
						// Ungroup
						testRunner.SelectAll();
						testRunner.ClickByName("Ungroup Button");

						// Blur
						testRunner.SelectNone();
						testRunner.WaitFor(() => scene.Children.Count() == 2);

						// Assert
						Assert.AreEqual(2, scene.Children.Count());
						Assert.AreEqual(3, scene.DescendantsAndSelf().Count(), "The scene and the 2 objects");
					});

				return Task.CompletedTask;
			}, overrideWidth: 1300);
		}

		[Test, ChildProcessTest]
		public async Task ValidateDoUndoMirror()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.OpenPartTab();

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				// Initialize
				AddCoinToBed(testRunner, scene);

				// test mirror operations
				testRunner.RunDoUndoTest(
					scene,
					() =>
					{
						// Mirror
						testRunner.RightClickByName(CoinName, offset: new Point2D(-5, 0))
							.ClickByName("Modify Menu Item")
							.ClickByName("Transform Menu Item")
							.ClickByName("Mirror Menu Item")
							.ClickByName("Mirror On DropDownList");
					});

				return Task.CompletedTask;
			}, overrideWidth: 1300);
		}

		// NOTE: This test once failed on GLFW. Could be timing or accidental input.
		[Test, ChildProcessTest]
		public async Task ValidateDoUndoTranslateXY()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.OpenPartTab();

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				// Initialize
				AddCoinToBed(testRunner, scene);

				// NOTE: Test failed with this once:
				//       Should be same (6): '      "Matrix": "[1.0,0.0,0.0,0.0,0.0,1.0,0.0,0.0,0.0,0.0,1.0,0.0,-10.000661239027977,-19.05065578967333,-5.421010862427522E-17,1.0]",
				//                         ' '      "Matrix": "[1.0,0.0,0.0,0.0,0.0,1.0,0.0,0.0,0.0,0.0,1.0,0.0,2.999338760972023,-24.00067986547947,-5.421010862427522E-17,1.0]",
				//       Expected: True
				//       But was: False
				// UndoTestExtensionMethods.SceneFilesAreSame(String fileName1, String fileName2, Boolean expectedResult) line 474
				// UndoTestExtensionMethods.AssertUndoRedo(AutomationRunner testRunner, InteractiveScene scene, String scenePath, String preOperationPath, String postOperationPath, Int32 preOperationDescendantCount, Int32 postOperationDescendantCount) line 541
				// UndoTestExtensionMethods.RunDoUndoTest(AutomationRunner testRunner, InteractiveScene scene, Action performOperation) line 453

				// test drag x y translation
				testRunner.RunDoUndoTest(
					scene,
					() =>
					{
						// Drag in XY
						var part = testRunner.GetObjectByName(CoinName, out _) as IObject3D;
						var start = part.GetAxisAlignedBoundingBox(Matrix4X4.Identity).Center;
						testRunner.DragDropByName(CoinName, CoinName, offsetDrag: new Point2D(-4, 0), offsetDrop: new Point2D(40, 0));
						var end = part.GetAxisAlignedBoundingBox(Matrix4X4.Identity).Center;

						// NOTE: Test failed with this once: Expected: greater than 15.399987526237965d, But was:  15.399987526237965d
						//       ClickWidget now waits for 2 redraws in case there is more deferred processing.

						// Assert
						Assert.Greater(end.X, start.X);
						Assert.Less(end.Y, start.Y);
						Assert.True(Math.Abs(end.Z - start.Z) < .001);
					});

				return Task.CompletedTask;
			}, overrideWidth: 1300);
		}
		// Parallel testing of this single test.
		//[Test, ChildProcessTest] public async Task ValidateDoUndoTranslateXY1() => await ValidateDoUndoTranslateXY();
		//[Test, ChildProcessTest] public async Task ValidateDoUndoTranslateXY2() => await ValidateDoUndoTranslateXY();
		//[Test, ChildProcessTest] public async Task ValidateDoUndoTranslateXY3() => await ValidateDoUndoTranslateXY();
		//[Test, ChildProcessTest] public async Task ValidateDoUndoTranslateXY4() => await ValidateDoUndoTranslateXY();
		//[Test, ChildProcessTest] public async Task ValidateDoUndoTranslateXY5() => await ValidateDoUndoTranslateXY();
		//[Test, ChildProcessTest] public async Task ValidateDoUndoTranslateXY6() => await ValidateDoUndoTranslateXY();
		//[Test, ChildProcessTest] public async Task ValidateDoUndoTranslateXY7() => await ValidateDoUndoTranslateXY();
		//[Test, ChildProcessTest] public async Task ValidateDoUndoTranslateXY8() => await ValidateDoUndoTranslateXY();
		//[Test, ChildProcessTest] public async Task ValidateDoUndoTranslateXY9() => await ValidateDoUndoTranslateXY();
		//[Test, ChildProcessTest] public async Task ValidateDoUndoTranslateXYa() => await ValidateDoUndoTranslateXY();
		//[Test, ChildProcessTest] public async Task ValidateDoUndoTranslateXYb() => await ValidateDoUndoTranslateXY();


		[Test, ChildProcessTest]
		public async Task ValidateDoUndoTranslateZ()
		{
			await MatterControlUtilities.RunTest(testRunner =>
			{
				testRunner.OpenPartTab();

				var view3D = testRunner.GetWidgetByName("View3DWidget", out _) as View3DWidget;
				var scene = view3D.Object3DControlLayer.Scene;

				// Initialize
				AddCoinToBed(testRunner, scene);

				// test z translation
				testRunner.RunDoUndoTest(
					scene,
					() =>
					{
						// Drag in Z
						var part = testRunner.GetObjectByName(CoinName, out _) as IObject3D;
						var startZ = part.GetAxisAlignedBoundingBox(Matrix4X4.Identity).Center.Z;
						testRunner.DragDropByName("MoveInZControl", "MoveInZControl", offsetDrag: new Point2D(0, 0), offsetDrop: new Point2D(0, 40))
						.Delay();
						var endZ = part.GetAxisAlignedBoundingBox(Matrix4X4.Identity).Center.Z;

						// Assert
						Assert.Greater(endZ, startZ);
					});

				return Task.CompletedTask;
			}, overrideWidth: 1300);
		}

		private static void AddBoxABoxBToBed(AutomationRunner testRunner, InteractiveScene scene)
		{
			var item = "Calibration - Box.stl";
			// NOTE: Test once failed here. Probably due to timing.
			testRunner.AddItemToBed()
				.Delay(.1)
				// move the first one over
				.DragDropByName(item, item, offsetDrop: new Point2D(40, 40));
			var part = testRunner.GetObjectByName(item, out _) as IObject3D;
			part.Name = "BoxA";

			testRunner.AddItemToBed()
				.Delay(.1);

			part = testRunner.GetObjectByName(item, out _) as IObject3D;
			part.Name = "BoxB";
		}

		private static void AddCoinToBed(AutomationRunner testRunner, InteractiveScene scene)
		{
			testRunner.AddItemToBed(partName: "Row Item MatterControl - Coin.stl")
				.Delay(.1)
				// TODO: assert the part is centered on the bed
				.ClickByName(CoinName, offset: new Point2D(-4, 0));
			Assert.IsNotNull(scene.SelectedItem);
		}
	}

	public static class UndoTestExtensionMethods
	{
		public static void RunDoUndoTest(this AutomationRunner testRunner, InteractiveScene scene, Action performOperation)
		{
			string scenePath = Path.Combine(MatterControlUtilities.RootPath, "Tests", "temp", "undo_test_scene_" + Path.GetRandomFileName());

			Directory.CreateDirectory(scenePath);
			Object3D.AssetsPath = Path.Combine(scenePath, "Assets");

			Object3D.AssetsPath = Path.Combine(scenePath, "Assets");

			// save the scene
			string preOperationPath = Path.Combine(scenePath, "preOperation.mcx");
			scene.Save(preOperationPath);

			var preOperationDescendantCount = scene.DescendantsAndSelf().Count();

			// Do the operation
			performOperation();

			var postOperationDescendantCount = scene.DescendantsAndSelf().Count();

			// save the scene
			string postOperationPath = Path.Combine(scenePath, scenePath, "postOperation.mcx");
			scene.Save(postOperationPath);

			Assert.AreEqual(postOperationDescendantCount, scene.DescendantsAndSelf().Count());

			// assert new save is different
			SceneFilesAreSame(postOperationPath, preOperationPath, false);

			// select the part
			testRunner.Type("^a"); // clear the selection (type a space)
			testRunner.WaitFor(() => scene.SelectedItem != null);
			Assert.IsNotNull(scene.SelectedItem);

			// with the part selected
			AssertUndoRedo(
				testRunner,
				scene,
				scenePath,
				preOperationPath,
				postOperationPath,
				preOperationDescendantCount,
				postOperationDescendantCount);

			// unselect the part
			testRunner.Type(" ") // clear the selection (type a space)
				.WaitFor(() => scene.SelectedItem == null);
			Assert.IsNull(scene.SelectedItem);

			// with the part unselected
			AssertUndoRedo(
				testRunner,
				scene,
				scenePath,
				preOperationPath,
				postOperationPath,
				preOperationDescendantCount,
				postOperationDescendantCount);
		}

		private static void SceneFilesAreSame(string fileName1, string fileName2, bool expectedResult)
		{
			bool areSame = true;
			string[] fileContent1 = File.ReadAllLines(fileName1);
			string[] fileContent2 = File.ReadAllLines(fileName2);

			for (int i = 0; i < Math.Min(fileContent1.Length, fileContent2.Length); i++)
			{
				areSame &= ValidateSceneLine(fileContent1[i], fileContent2[i]);
				if (expectedResult)
				{
					Assert.IsTrue(areSame, $"Should be same ({i}): '{fileContent1[i]}' '{fileContent2[i]}");
				}
			}

			areSame &= fileContent1.Length == fileContent2.Length;
			if (expectedResult)
			{
				Assert.IsTrue(areSame, $"Should be same length: '{fileName1}' '{fileName2}");
			}

			Assert.IsTrue(expectedResult == areSame, $"Should be different: '{fileName1}' '{fileName2}");
		}

		private static bool ValidateSceneLine(string v1, string v2)
		{
			if (v1 == v2)
			{
				return true;
			}

			if (v1.Contains("Matrix")
				&& v2.Contains("Matrix"))
			{
				double[] test = new double[] { 0, 1, 2, 3 };
				var expected = JsonConvert.SerializeObject(test, Formatting.Indented);

				// Figure out if the value content of these lines are equivalent.
				var data1 = v1.Substring(v1.IndexOf('['), v1.IndexOf(']') - v1.IndexOf('[') + 1);
				var matrix1 = new Matrix4X4(JsonConvert.DeserializeObject<double[]>(data1));

				var data2 = v2.Substring(v2.IndexOf('['), v2.IndexOf(']') - v2.IndexOf('[') + 1);
				var matrix2 = new Matrix4X4(JsonConvert.DeserializeObject<double[]>(data2));

				if (matrix1.Equals(matrix2, .001))
				{
					return true;
				}
			}

			return false;
		}

		private static void AssertUndoRedo(AutomationRunner testRunner,
			InteractiveScene scene,
			string scenePath, string preOperationPath, string postOperationPath,
			int preOperationDescendantCount, int postOperationDescendantCount)
		{
			var preUndoDescendantsCount = scene.DescendantsAndSelf().Count();

			// do an undo
			testRunner.ClickByName("3D View Undo");

			testRunner.WaitFor(() => preOperationDescendantCount == scene.DescendantsAndSelf().Count());
			Assert.AreEqual(preOperationDescendantCount, scene.DescendantsAndSelf().Count());

			// save the undo data
			string undoScenePath = Path.Combine(scenePath, "undoScene.mcx");
			var totalSceneItems = scene.DescendantsAndSelf().Count();
			var selectedItem = scene.SelectedItem;

			Object3D.AssetsPath = Path.Combine(scenePath, "Assets");

			scene.Save(undoScenePath);
			Assert.AreEqual(totalSceneItems, scene.DescendantsAndSelf().Count());
			Assert.AreEqual(selectedItem, scene.SelectedItem);

			// After undo action, validate the persisted undoScene with the original 'before do' scene
			SceneFilesAreSame(preOperationPath, undoScenePath, true);

			// now redo the undo
			testRunner.ClickByName("3D View Redo");

			testRunner.WaitFor(() => postOperationDescendantCount == scene.DescendantsAndSelf().Count());
			Assert.AreEqual(postOperationDescendantCount, scene.DescendantsAndSelf().Count());

			// save the redo
			string redoScenePath = Path.Combine(scenePath, "redoScene.mcx");
			scene.Save(redoScenePath);

			// After redo action, validate the persisted redoScene with the original 'after do' scene
			SceneFilesAreSame(postOperationPath, redoScenePath, true);
		}
	}
}