/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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

using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.VectorMath;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture]
	public class InteractiveSceneTests
	{
		[Test, Category("InteractiveScene")]
		public async Task CombineTests()
		{
			// Combine has correct results
			{
				var root = new Object3D();
				var cubeA = await CubeObject3D.Create(20, 20, 20);
				var cubeB = await CubeObject3D.Create(20, 20, 20);
				var offsetCubeB = new TranslateObject3D(cubeB, 10);
				Assert.IsTrue(offsetCubeB.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(
					0, -10, -10,
					20, 10, 10), .001));

				var union = new CombineObject3D_2();
				union.Children.Add(cubeA);
				union.Children.Add(offsetCubeB);
				root.Children.Add(union);

				Assert.IsTrue(union.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(
					-10, -10, -10,
					20, 10, 10), .001));

				Assert.IsTrue(root.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(
					-10, -10, -10,
					20, 10, 10), .001));

				union.Combine();
				Assert.IsTrue(union.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(
					-10, -10, -10,
					20, 10, 10), .001));

				union.Flatten(null);

				Assert.AreEqual(1, root.Children.Count());
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(new AxisAlignedBoundingBox(
					-10, -10, -10,
					20, 10, 10).Equals(rootAabb, .001));
			}

			// Combine has correct results when inner content is changed
			{
				var root = new Object3D();
				var cubeA = await CubeObject3D.Create(20, 20, 20);
				var cubeB = await CubeObject3D.Create(20, 20, 20);

				var union = new CombineObject3D_2();
				union.Children.Add(cubeA);
				union.Children.Add(cubeB);
				root.Children.Add(union);

				union.Combine();

				Assert.AreEqual(5, root.Descendants().Count());
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(new AxisAlignedBoundingBox(
					-10, -10, -10,
					10, 10, 10).Equals(rootAabb, .001));

				var offsetCubeB = new TranslateObject3D(cubeB, 10);

				union.Combine();
				Assert.AreEqual(7, root.Descendants().Count());
				rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(new AxisAlignedBoundingBox(
					-10, -10, -10,
					20, 10, 10).Equals(rootAabb, .001));
			}

			// now make sure undo has the right results for flatten
			{
				var root = new Object3D();
				var cubeA = await CubeObject3D.Create(20, 20, 20);
				var cubeB = await CubeObject3D.Create(20, 20, 20);
				var offsetCubeB = new TranslateObject3D(cubeB, 10);

				var combine = new CombineObject3D_2();
				combine.Children.Add(cubeA);
				combine.Children.Add(offsetCubeB);
				root.Children.Add(combine);
				Assert.AreEqual(5, root.Descendants().Count(), "Should have the 1 combine, 2 cubeA, 3 cubeB, 4 offset cubeB, 5 offset sourceItem");

				combine.Combine();
				Assert.AreEqual(7, root.Descendants().Count(), "Should have the 1 combine, 2 cubeA, 3 wrapped cubeA, 4 cubeB, 5 offset cubeB, 6 offset sourceItem, wrapped cubeB");
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(new AxisAlignedBoundingBox(
					-10, -10, -10,
					20, 10, 10).Equals(rootAabb, .001));

				var undoBuffer = new UndoBuffer();
				combine.Flatten(undoBuffer);

				Assert.AreEqual(1, root.Descendants().Count());
				Assert.AreEqual(1, root.Children.Count());
				rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(new AxisAlignedBoundingBox(
					-10, -10, -10,
					20, 10, 10).Equals(rootAabb, .001));

				undoBuffer.Undo();
				Assert.AreEqual(7, root.Descendants().Count(), "Should have the 1 combine, 2 cubeA, 3 wrapped cubeA, 4 cubeB, 5 offset cubeB, 6 offset sourceItem, wrapped cubeB");
			}

			// now make sure undo has the right results for remove
			{
				var root = new Object3D();
				var cubeA = await CubeObject3D.Create(20, 20, 20);
				cubeA.Name = "cubeA";
				var cubeB = await CubeObject3D.Create(20, 20, 20);
				cubeB.Name = "cubeB";

				var combine = new CombineObject3D_2();
				combine.Children.Add(cubeA);
				combine.Children.Add(cubeB);
				root.Children.Add(combine);
				Assert.AreEqual(3, root.Descendants().Count(), "Should have the 1 combine, 2 cubeA, 3 cubeB");

				combine.Combine();
				Assert.AreEqual(5, root.Descendants().Count(), "Should have the 1 combine, 2 cubeA, 3 wrapped cubeA, 4 cubeB, 5 wrapped cubeB");
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(new AxisAlignedBoundingBox(
					-10, -10, -10,
					10, 10, 10).Equals(rootAabb, .001));

				var undoBuffer = new UndoBuffer();
				combine.Remove(undoBuffer);

				Assert.AreEqual(2, root.Descendants().Count(), "Should have the 1 cubeA, 2 cubeB");
				rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(new AxisAlignedBoundingBox(
					-10, -10, -10,
					10, 10, 10).Equals(rootAabb, .001));

				undoBuffer.Undo();
				Assert.AreEqual(5, root.Descendants().Count(), "Should have the 1 combine, 2 cubeA, 3 wrapped cubeA, 4 cubeB, 5 wrapped cubeB");
			}

			// now make sure undo has the right results for remove
			{
				var root = new Object3D();
				var cubeA = await CubeObject3D.Create(20, 20, 20);
				var cubeB = await CubeObject3D.Create(20, 20, 20);
				var offsetCubeB = new TranslateObject3D(cubeB, 10);

				var combine = new CombineObject3D_2();
				combine.Children.Add(cubeA);
				combine.Children.Add(offsetCubeB);
				root.Children.Add(combine);
				Assert.AreEqual(5, root.Descendants().Count(), "Should have the 1 combine, 2 cubeA, 3 cubeB, 4 offset cubeB, 5 offset sourceItem");

				combine.Combine();
				Assert.AreEqual(7, root.Descendants().Count(), "Should have the 1 combine, 2 cubeA, 3 wrapped cubeA, 4 cubeB, 5 offset cubeB, 6 offset sourceItem, wrapped cubeB");
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(new AxisAlignedBoundingBox(
					-10, -10, -10,
					20, 10, 10).Equals(rootAabb, .001));

				var undoBuffer = new UndoBuffer();
				combine.Remove(undoBuffer);

				Assert.AreEqual(4, root.Descendants().Count(), "Should have the 1 cubeA, 2 cubeB, 3 offset cubeB, 4 offset sourceItem");
				rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(new AxisAlignedBoundingBox(
					-10, -10, -10,
					20, 10, 10).Equals(rootAabb, .001));

				undoBuffer.Undo();
				Assert.AreEqual(7, root.Descendants().Count(), "Should have the 1 combine, 2 cubeA, 3 wrapped cubeA, 4 cubeB, 5 offset cubeB, 6 offset sourceItem, wrapped cubeB");
			}

			// make sure the MatterCAD add function is working
			{
				var cubeA = await CubeObject3D.Create(20, 20, 20);
				var cubeB = await CubeObject3D.Create(20, 20, 20);
				var offsetCubeB = new TranslateObject3D(cubeB, 10);

				var plus = cubeA.Plus(offsetCubeB, true);

				Assert.AreEqual(0, plus.Children.Count());
				Assert.IsTrue(plus.Mesh != null);
				var aabb = plus.GetAxisAlignedBoundingBox();
				Assert.IsTrue(new AxisAlignedBoundingBox(
					-10, -10, -10,
					20, 10, 10).Equals(aabb, .001));
			}

			// test single object combine
			{
				var root = new Object3D();
				var cubeA = await CubeObject3D.Create(20, 20, 20);
				var cubeB = await CubeObject3D.Create(20, 20, 20);
				var offsetCubeB = new TranslateObject3D(cubeB, 10);

				var group = new Object3D();
				group.Children.Add(cubeA);
				group.Children.Add(offsetCubeB);

				var union = new CombineObject3D_2();
				union.Children.Add(group);

				root.Children.Add(union);

				union.Combine();
				Assert.AreEqual(8, root.Descendants().Count(), "group, union, wa, a, wtb, tb, b");

				union.Flatten(null);
				Assert.AreEqual(1, root.Descendants().Count(), "Should have the union result");

				Assert.AreEqual(1, root.Children.Count());
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(new AxisAlignedBoundingBox(
					-10, -10, -10,
					20, 10, 10).Equals(rootAabb, .001));
			}
		}


		[Test, Category("InteractiveScene")]
		public async Task SubtractTests()
		{
			// Subtract has correct number of results
			{
				var root = new Object3D();
				var cubeA = await CubeObject3D.Create(20, 20, 20);
				var cubeB = await CubeObject3D.Create(20, 20, 20);
				var offsetCubeB = new TranslateObject3D(cubeB, 10);

				var subtract = new SubtractObject3D();
				subtract.Children.Add(cubeA);
				subtract.Children.Add(offsetCubeB);
				subtract.SelectedChildren.Add(offsetCubeB.ID);
				root.Children.Add(subtract);

				subtract.Subtract();
				subtract.Flatten(null);

				Assert.AreEqual(1, root.Children.Count());
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(new AxisAlignedBoundingBox(
					-10, -10, -10,
					0, 10, 10).Equals(rootAabb, .001));
			}

			// make sure the MatterCAD subtract function is working
			{
				var cubeA = await CubeObject3D.Create(20, 20, 20);
				var cubeB = await CubeObject3D.Create(20, 20, 20);
				var offsetCubeB = new TranslateObject3D(cubeB, 10);

				var subtract = cubeA.Minus(offsetCubeB);

				Assert.AreEqual(0, subtract.Children.Count());
				Assert.IsTrue(subtract.Mesh != null);
				var aabb = subtract.GetAxisAlignedBoundingBox();
				Assert.IsTrue(new AxisAlignedBoundingBox(
					-10, -10, -10,
					0, 10, 10).Equals(aabb, .001));
			}
		}

		[Test, Category("InteractiveScene")]
		public async Task AabbCalculatedCorrectlyForPinchedFitObjects()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			// Automation runner must do as much as program.cs to spin up platform
			string platformFeaturesProvider = "MatterHackers.MatterControl.WindowsPlatformsFeatures, MatterControl.Winforms";
			AppContext.Platform = AggContext.CreateInstanceFrom<INativePlatformFeatures>(platformFeaturesProvider);
			AppContext.Platform.InitPluginFinder();
			AppContext.Platform.ProcessCommandline();
			
			// build without pinch
			{
				var root = new Object3D();
				var cube = await CubeObject3D.Create(20, 20, 20);
				root.Children.Add(cube);
				Assert.IsTrue(root.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(-10, -10, -10), new Vector3(10, 10, 10)), .001));
				root.Children.Remove(cube);
				var fit = await FitToBoundsObject3D_2.Create(cube);

				fit.SizeX = 50;
				fit.SizeY = 20;
				fit.SizeZ = 20;
				root.Children.Add(fit);
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(rootAabb.Equals(new AxisAlignedBoundingBox(new Vector3(-25, -10, -10), new Vector3(25, 10, 10)), .001));
			}

			// build with pinch
			{
				var root = new Object3D();
				var cube = await CubeObject3D.Create(20, 20, 20);
				var fit = await FitToBoundsObject3D_2.Create(cube);

				fit.SizeX = 50;
				fit.SizeY = 20;
				fit.SizeZ = 20;

				var pinch = new PinchObject3D_2();
				pinch.Children.Add(fit);
				await pinch.Rebuild();
				root.Children.Add(pinch);
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(rootAabb.Equals(new AxisAlignedBoundingBox(new Vector3(-10, -10, -10), new Vector3(10, 10, 10)), .001));
			}

			// build with translate
			{
				var root = new Object3D();
				var cube = await CubeObject3D.Create(20, 20, 20);

				var translate = new TranslateObject3D(cube, 11, 0, 0);

				root.Children.Add(translate);
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(rootAabb.Equals(new AxisAlignedBoundingBox(new Vector3(1, -10, -10), new Vector3(21, 10, 10)), .001));
			}

			// build with pinch and translate
			{
				var root = new Object3D();
				var cube = await CubeObject3D.Create(20, 20, 20);

				var translate = new TranslateObject3D(cube, 11, 0, 0);

				var pinch = new PinchObject3D_2();
				pinch.Children.Add(translate);
				root.Children.Add(pinch);
				await pinch.Rebuild();
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(rootAabb.Equals(new AxisAlignedBoundingBox(new Vector3(1, -10, -10), new Vector3(21, 10, 10)), .001));
			}

			// build with pinch and translate
			{
				var root = new Object3D();
				var cube = await CubeObject3D.Create(20, 20, 20);
				var fit = await FitToBoundsObject3D_2.Create(cube);

				fit.SizeX = 50;
				fit.SizeY = 20;
				fit.SizeZ = 20;

				var translate = new TranslateObject3D(fit, 11, 0, 0);

				var pinch = new PinchObject3D_2();
				pinch.Children.Add(translate);
				await pinch.Rebuild();
				root.Children.Add(pinch);
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(rootAabb.Equals(new AxisAlignedBoundingBox(new Vector3(1, -10, -10), new Vector3(21, 10, 10)), .001));
			}
		}

		[Test, Category("InteractiveScene")]
		public async Task ScaleObjectMaintainsCorrectAabb()
		{
			// build cube with scale and undo
			{
				// create a simple cube with translation
				var root = new Object3D();
				var cube = await CubeObject3D.Create(20, 20, 20);
				cube.Matrix = Matrix4X4.CreateTranslation(50, 60, 10);
				root.Children.Add(cube);
				Assert.AreEqual(2, root.DescendantsAndSelf().Count());
				var preScaleAabb = root.GetAxisAlignedBoundingBox();

				var undoBuffer = new UndoBuffer();

				// add a scale to it (that is not scaled)
				var scaleObject = new ScaleObject3D();
				scaleObject.WrapItems(new IObject3D[] { cube }, undoBuffer);

				// ensure that the object did not move
				Assert.IsTrue(scaleObject.ScaleAbout.Equals(Vector3.Zero), "The objects have been moved to be scalling about 0.");
				Assert.AreEqual(4, root.DescendantsAndSelf().Count());
				var postScaleAabb = root.GetAxisAlignedBoundingBox();

				Assert.IsTrue(preScaleAabb.Equals(postScaleAabb, .001));

				Assert.AreNotEqual(cube, scaleObject.UntransformedChildren.First(), "There is an undo buffer, there should have been a clone");
			}
			
			// build cube with scale
			{
				// create a simple cube with translation
				var root = new Object3D();
				var cube = await CubeObject3D.Create(20, 20, 20);
				cube.Matrix = Matrix4X4.CreateTranslation(50, 60, 10);
				root.Children.Add(cube);
				Assert.AreEqual(2, root.DescendantsAndSelf().Count());
				var preScaleAabb = root.GetAxisAlignedBoundingBox();

				// add a scale to it (that is not scaled)
				var scaleObject = new ScaleObject3D(cube);

				// ensure that the object did not move
				Assert.IsTrue(scaleObject.ScaleAbout.Equals(Vector3.Zero), "The objects have been moved to be scalling about 0.");
				Assert.AreEqual(4, root.DescendantsAndSelf().Count());
				var postScaleAabb = root.GetAxisAlignedBoundingBox();

				Assert.IsTrue(preScaleAabb.Equals(postScaleAabb, .001));

				Assert.AreEqual(cube, scaleObject.UntransformedChildren.First(), "There is no undo buffer, there should not have been a clone");
			}
		}

		[Test, Category("InteractiveScene")]
		public async Task ScaleAndRotateMaintainsCorrectAabb()
		{
			{
				// create a simple cube with translation
				var root = new Object3D();
				var cube = await CubeObject3D.Create(20, 20, 20);
				cube.Matrix = Matrix4X4.CreateTranslation(50, 60, 10);
				root.Children.Add(cube);
				Assert.AreEqual(2, root.DescendantsAndSelf().Count());
				var preScaleAabb = root.GetAxisAlignedBoundingBox();

				// add a scale to it (that is not scaled)
				var scaleObject = new ScaleObject3D(cube);

				// ensure that the object did not move
				Assert.IsTrue(scaleObject.ScaleAbout.Equals(Vector3.Zero), "The objects have been moved to be scalling about 0.");
				Assert.AreEqual(4, root.DescendantsAndSelf().Count());
				var postScaleAabb = root.GetAxisAlignedBoundingBox();

				Assert.IsTrue(preScaleAabb.Equals(postScaleAabb, .001));

				Assert.AreEqual(cube, scaleObject.UntransformedChildren.First(), "There is no undo buffer, there should not have been a clone");

				var rotateScaleObject = new RotateObject3D_2(cube);
				// ensure that the object did not move
				Assert.AreEqual(6, root.DescendantsAndSelf().Count());
				var postRotateScaleAabb = root.GetAxisAlignedBoundingBox();

				Assert.IsTrue(preScaleAabb.Equals(postRotateScaleAabb, .001));

				Assert.AreEqual(cube, rotateScaleObject.UntransformedChildren.First(), "There is no undo buffer, there should not have been a clone");
			}
		}

		[Test, Category("InteractiveScene")]
		public async Task RotateMaintainsCorrectAabb()
		{
			{
				// create a simple cube with translation
				var root = new Object3D();
				var cube = await CubeObject3D.Create(20, 20, 20);
				cube.Matrix = Matrix4X4.CreateTranslation(50, 60, 10);
				root.Children.Add(cube);
				Assert.AreEqual(2, root.DescendantsAndSelf().Count());
				var preRotateAabb = root.GetAxisAlignedBoundingBox();

				// add a rotate to it (that is not rotated)
				var rotateObject = new RotateObject3D_2(cube);

				// ensure that the object did not move
				Assert.IsTrue(rotateObject.RotateAbout.Origin.Equals(new Vector3(50, 60, 10)));
				Assert.AreEqual(4, root.DescendantsAndSelf().Count());
				var postRotateAabb = root.GetAxisAlignedBoundingBox();

				Assert.IsTrue(preRotateAabb.Equals(postRotateAabb, .001));

				Assert.AreEqual(cube, rotateObject.UntransformedChildren.First(), "There is no undo buffer, there should not have been a clone");
			}
		}

		[Test, Category("InteractiveScene")]
		public async Task AabbCalculatedCorrectlyForAlignedFitObject()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			// Automation runner must do as much as program.cs to spin up platform
			string platformFeaturesProvider = "MatterHackers.MatterControl.WindowsPlatformsFeatures, MatterControl.Winforms";
			AppContext.Platform = AggContext.CreateInstanceFrom<INativePlatformFeatures>(platformFeaturesProvider);
			AppContext.Platform.ProcessCommandline();

			var root = new Object3D();
			var cube = await CubeObject3D.Create(20, 20, 20);
			var fit = await FitToBoundsObject3D_2.Create(cube);

			fit.SizeX = 10;
			fit.SizeY = 10;
			fit.SizeZ = 6;

			Assert.IsTrue(fit.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(-5, -5, -10), new Vector3(5, 5, -4)), .01));

			var bigCube = await CubeObject3D.Create(20, 20, 20);

			Assert.IsTrue(bigCube.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(-10, -10, -10), new Vector3(10, 10, 10)), .01));

			var align = new AlignObject3D();
			align.Children.Add(bigCube);
			align.Children.Add(fit);

			await align.Rebuild();
			align.XAlign = Align.Center;
			align.YAlign = Align.Center;
			align.ZAlign = Align.Max;
			align.Advanced = true;
			align.ZOffset = 1;

			await align.Rebuild();

			var alignAabb = align.GetAxisAlignedBoundingBox();
			Assert.IsTrue(alignAabb.Equals(new AxisAlignedBoundingBox(new Vector3(-10, -10, -10), new Vector3(10, 10, 11)), .01));

			alignAabb = align.GetAxisAlignedBoundingBox();
			root.Children.Add(align);
			var rootAabb = root.GetAxisAlignedBoundingBox();
			Assert.IsTrue(rootAabb.Equals(new AxisAlignedBoundingBox(new Vector3(-10, -10, -10), new Vector3(10, 10, 11)), .01));
		}

		[Test, Category("InteractiveScene")]
		public async Task AlignObjectHasCorrectPositionsOnXAxis()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			// Automation runner must do as much as program.cs to spin up platform
			string platformFeaturesProvider = "MatterHackers.MatterControl.WindowsPlatformsFeatures, MatterControl.Winforms";
			AppContext.Platform = AggContext.CreateInstanceFrom<INativePlatformFeatures>(platformFeaturesProvider);
			AppContext.Platform.ProcessCommandline();

			var scene = new InteractiveScene();

			var cube = await CubeObject3D.Create(20, 20, 20);
			cube.Matrix = Matrix4X4.CreateTranslation(50, 60, 10);
			Assert.IsTrue(cube.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(40, 50, 0), new Vector3(60, 70, 20)), .01));
			scene.Children.Add(cube);

			var bigCube = await CubeObject3D.Create(40, 40, 40);
			bigCube.Matrix = Matrix4X4.CreateTranslation(20, 20, 20);
			Assert.IsTrue(bigCube.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(0, 0, 0), new Vector3(40, 40, 40)), .01));
			scene.Children.Add(bigCube);

			// select them
			scene.SelectedItem = cube;
			scene.AddToSelection(bigCube);

			// create an align of them
			var align = new AlignObject3D();
			align.AddSelectionAsChildren(scene, scene.SelectedItem);

			// assert the align in built correctly
			Assert.AreEqual(1, scene.Children.Count);
			Assert.AreEqual(2, align.Children.Count);
			Assert.IsTrue(align.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(0, 0, 0), new Vector3(60, 70, 40)), 1.0));

			align.SelectedChild = new SelectedChildren() { cube.ID.ToString() };
			// turn align on
			align.XAlign = Align.Min;
			await align.Rebuild();
			Assert.IsTrue(align.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(40, 0, 0), new Vector3(80, 70, 40)), 1.0));

			// turn it off
			align.XAlign = Align.None;
			await align.Rebuild();
			Assert.IsTrue(align.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(0, 0, 0), new Vector3(60, 70, 40)), 1.0));

			// turn it back on
			align.XAlign = Align.Min;
			await align.Rebuild();
			Assert.IsTrue(align.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(40, 0, 0), new Vector3(80, 70, 40)), 1.0));

			// remove the align and assert stuff moved back to where it started
			align.Remove(null);
			Assert.IsTrue(cube.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(40, 50, 0), new Vector3(60, 70, 20)), .01));
			Assert.IsTrue(bigCube.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(0, 0, 0), new Vector3(40, 40, 40)), .01));
		}

		[Test, Category("InteractiveScene")]
		public async Task AabbCalculatedCorrectlyForCurvedFitObjects()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			// Automation runner must do as much as program.cs to spin up platform
			string platformFeaturesProvider = "MatterHackers.MatterControl.WindowsPlatformsFeatures, MatterControl.Winforms";
			AppContext.Platform = AggContext.CreateInstanceFrom<INativePlatformFeatures>(platformFeaturesProvider);
			AppContext.Platform.ProcessCommandline();

			var root = new Object3D();
			var cube = await CubeObject3D.Create(20, 20, 20);
			var fit = await FitToBoundsObject3D_2.Create(cube);

			fit.SizeX = 50;
			fit.SizeY = 20;
			fit.SizeZ = 20;

			Assert.IsTrue(fit.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(-25, -10, -10), new Vector3(25, 10, 10)), 1.0));

			var curve = new CurveObject3D_2();
			curve.Children.Add(fit);
			await curve.Rebuild();
			var curveAabb = curve.GetAxisAlignedBoundingBox();
			root.Children.Add(curve);
			var rootAabb = root.GetAxisAlignedBoundingBox();
			Assert.IsTrue(rootAabb.Equals(new AxisAlignedBoundingBox(new Vector3(-17.5, -9.9, -10), new Vector3(17.5, 11.97, 10)), 1.0));
		}
	}
}
