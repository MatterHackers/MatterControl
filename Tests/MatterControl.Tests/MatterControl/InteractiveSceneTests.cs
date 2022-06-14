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
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using NUnit.Framework;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture]
	public class InteractiveSceneTests
	{
		public static void StartupMC()
		{
			StaticData.RootPath = MatterControlUtilities.StaticDataPath;
			MatterControlUtilities.OverrideAppDataLocation(MatterControlUtilities.RootPath);

			// Automation runner must do as much as program.cs to spin up platform
			string platformFeaturesProvider = "MatterHackers.MatterControl.WindowsPlatformsFeatures, MatterControl.Winforms";
			AppContext.Platform = AggContext.CreateInstanceFrom<INativePlatformFeatures>(platformFeaturesProvider);
			AppContext.Platform.InitPluginFinder();
			AppContext.Platform.ProcessCommandline();
		}

		[Test, Category("InteractiveScene")]
		public async Task CombineTests()
		{
			StartupMC();

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

				union.Apply(null);

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
				combine.Apply(undoBuffer);

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
				combine.Cancel(undoBuffer);

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
				combine.Cancel(undoBuffer);

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

				union.Apply(null);
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
			StartupMC();

			// Subtract has correct number of results
			{
				var root = new Object3D();
				var cubeA = await CubeObject3D.Create(20, 20, 20);
				var cubeB = await CubeObject3D.Create(20, 20, 20);
				var offsetCubeB = new TranslateObject3D(cubeB, 10);

				var subtract = new SubtractObject3D_2();
				subtract.Children.Add(cubeA);
				subtract.Children.Add(offsetCubeB);
				subtract.SelectedChildren.Add(offsetCubeB.ID);
				root.Children.Add(subtract);

				subtract.Subtract();
				subtract.Apply(null);

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
		public async Task HoleTests()
		{
			StartupMC();

			// Subtract has correct number of results with combine
			{
				var root = new Object3D();
				var cubeA1 = await CubeObject3D.Create(20, 20, 20);
				var cubeA2 = await CubeObject3D.Create(20, 20, 20);
				var cubeB = await CubeObject3D.Create(20, 20, 20);
				var offsetCubeB = new TranslateObject3D(cubeB, 0, 10);

				var align = new AlignObject3D_2();
				align.Children.Add(cubeA1);
				align.Children.Add(cubeA2);
				align.OutputType = PrintOutputTypes.Hole;
				await align.Rebuild();

				var combine = new CombineObject3D_2();
				combine.Children.Add(align);
				combine.Children.Add(offsetCubeB);
				root.Children.Add(combine);
				await combine.Rebuild();

				Assert.AreEqual(1, root.Children.Count());
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.AreEqual(10, rootAabb.YSize, .001);
			}

			// Subtract has correct number of results with group
			{
				var root = new Object3D();
				var cubeA1 = await CubeObject3D.Create(20, 20, 20);
				var cubeA2 = await CubeObject3D.Create(20, 20, 20);
				var cubeB = await CubeObject3D.Create(20, 20, 20);
				var offsetCubeB = new TranslateObject3D(cubeB, 0, 10);

				var align = new AlignObject3D_2();
				align.Children.Add(cubeA1);
				align.Children.Add(cubeA2);
				align.OutputType = PrintOutputTypes.Hole;
				await align.Rebuild();

				var combine = new GroupHolesAppliedObject3D();
				combine.Children.Add(align);
				combine.Children.Add(offsetCubeB);
				root.Children.Add(combine);
				await combine.Rebuild();

				Assert.AreEqual(1, root.Children.Count());
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.AreEqual(10, rootAabb.YSize, .001);
			}
		}

		[Test, Category("InteractiveScene")]
		public async Task AabbCalculatedCorrectlyForPinchedFitObjects()
		{
			StartupMC();

			// build without pinch
			{
				var root = new Object3D();
				var cube = await CubeObject3D.Create(20, 20, 20);
				root.Children.Add(cube);
				Assert.IsTrue(root.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(-10, -10, -10), new Vector3(10, 10, 10)), .001));
				root.Children.Remove(cube);
				var fit = await FitToBoundsObject3D_3.Create(cube);

				fit.Width = 50;
				fit.Depth = 20;
				fit.Height = 20;
				fit.Invalidate(InvalidateType.Properties);
				root.Children.Add(fit);
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(rootAabb.Equals(new AxisAlignedBoundingBox(new Vector3(-25, -10, -10), new Vector3(25, 10, 10)), .001));
			}

			// build with pinch
			{
				var root = new Object3D();
				var cube = await CubeObject3D.Create(20, 20, 20);
				var fit = await FitToBoundsObject3D_3.Create(cube);

				fit.Width = 50;
				fit.Depth = 20;
				fit.Height = 20;

				var pinch = new PinchObject3D_3();
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

				var pinch = new PinchObject3D_3();
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
				var fit = await FitToBoundsObject3D_3.Create(cube);

				fit.Width = 50;
				fit.Depth = 20;
				fit.Height = 20;

				var translate = new TranslateObject3D(fit, 11, 0, 0);

				var pinch = new PinchObject3D_3();
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
				var scaleObject = new ScaleObject3D_3();
				scaleObject.WrapItems(new IObject3D[] { cube }, undoBuffer);

				// ensure that the object did not move
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
				var scaleObject = new ScaleObject3D_3(cube);

				// ensure that the object did not move
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
				var scaleObject = new ScaleObject3D_3(cube);

				// ensure that the object did not move
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
			StartupMC();

			var root = new Object3D();
			var cube = await CubeObject3D.Create(20, 20, 20);
			var fit = await FitToBoundsObject3D_3.Create(cube);

			fit.Width = 10;
			fit.Depth = 10;
			fit.Height = 6;
			fit.Invalidate(InvalidateType.Properties);

			Assert.IsTrue(fit.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(-5, -5, -10), new Vector3(5, 5, -4)), .01));

			var bigCube = await CubeObject3D.Create(20, 20, 20);

			Assert.IsTrue(bigCube.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(-10, -10, -10), new Vector3(10, 10, 10)), .01));

			var align = new AlignObject3D_2();
			align.Children.Add(bigCube);
			align.Children.Add(fit);

			await align.Rebuild();
			align.XAlign = Align.Center;
			align.YAlign = Align.Center;
			align.ZAlign = Align.Max;
			align.XOptions = true;
			align.YOptions = true;
			align.ZOptions = true;
			align.ZOffset = 1;

			await align.Rebuild();

			var alignAabb = align.GetAxisAlignedBoundingBox();
			Assert.IsTrue(alignAabb.Equals(new AxisAlignedBoundingBox(new Vector3(-10, -10, -10), new Vector3(10, 10, 11)), .01));

			alignAabb = align.GetAxisAlignedBoundingBox();
			root.Children.Add(align);
			var rootAabb = root.GetAxisAlignedBoundingBox();
			Assert.IsTrue(rootAabb.Equals(new AxisAlignedBoundingBox(new Vector3(-10, -10, -10), new Vector3(10, 10, 11)), .01));
		}

		[Test]
		public void CombineTests2()
        {
			// overlaping results in simple new mesh
			{
				var meshA = PlatonicSolids.CreateCube(10, 10, 10);
				meshA.Translate(0, 0, 0);
				var meshB = PlatonicSolids.CreateCube(10, 10, 10);
				meshB.Translate(0, 5, 0);
				var mesh = Object3D.CombineParticipants(null,
					new IObject3D[]
					{
						new Object3D() { Mesh = meshA },
						new Object3D() { Mesh = meshB },
					},
					new CancellationToken());
				Assert.AreEqual(12, mesh.Faces.Count());
				var aabb = mesh.GetAxisAlignedBoundingBox();
				Assert.AreEqual(15, aabb.YSize, .001);
			}

			// multiple overlaping faces all combine
			{
				var meshA = PlatonicSolids.CreateCube(10, 10, 10);
				meshA.Translate(0, 0, 0);
				var meshB = PlatonicSolids.CreateCube(10, 10, 10);
				meshB.Translate(0, -3, 0);
				var meshC = PlatonicSolids.CreateCube(10, 10, 10);
				meshC.Translate(0, 3, 0);
				var mesh = Object3D.CombineParticipants(null,
					new IObject3D[]
					{
						new Object3D() { Mesh = meshA },
						new Object3D() { Mesh = meshB },
						new Object3D() { Mesh = meshC },
					},
					new CancellationToken());
				Assert.AreEqual(12, mesh.Faces.Count());
				var aabb = mesh.GetAxisAlignedBoundingBox();
				Assert.AreEqual(16, aabb.YSize, .001);
			}

			// a back face against a front face, both are removed
			{
				var meshA = PlatonicSolids.CreateCube(10, 10, 10);
				meshA.Translate(0, -5, 0);
				var meshB = PlatonicSolids.CreateCube(10, 10, 10);
				meshB.Translate(0, 5, 0);
				var mesh = Object3D.CombineParticipants(null,
					new IObject3D[]
					{
						new Object3D() { Mesh = meshA },
						new Object3D() { Mesh = meshB },
					},
					new CancellationToken());
				// StlProcessing.Save(mesh, @"C:\temp\temp.stl", new CancellationToken());
				Assert.AreEqual(12, mesh.Faces.Count());
				var aabb = mesh.GetAxisAlignedBoundingBox();
				Assert.AreEqual(20, aabb.YSize, .001);
			}

			// multiple overlaping faces all combine
			{
				// right side at 0
				var meshA = PlatonicSolids.CreateCube(10, 5, 10);
				meshA.Translate(-5, 0, 0);
				// left side at -5
				var meshB = PlatonicSolids.CreateCube(10, 5, 10);
				meshB.Translate(0, 0, 0);
				// right side at 0
				var meshC = PlatonicSolids.CreateCube(5, 10, 10);
				meshC.Translate(-2.5, 0, 0);
				var mesh = Object3D.CombineParticipants(null,
					new IObject3D[]
					{
						new Object3D() { Mesh = meshA },
						new Object3D() { Mesh = meshB },
						new Object3D() { Mesh = meshC },
					},
					new CancellationToken());
				// StlProcessing.Save(mesh, @"C:\temp\temp.stl", new CancellationToken());
				Assert.AreEqual(44, mesh.Faces.Count());
				var aabb = mesh.GetAxisAlignedBoundingBox();
				Assert.AreEqual(10, aabb.YSize, .001);
			}

		}

		[Test, Category("InteractiveScene")]
		public async Task AlignObjectHasCorrectPositionsOnXAxis()
		{
			StartupMC();

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
			var align = new AlignObject3D_2();
			align.AddSelectionAsChildren(scene, scene.SelectedItem);

			var unalignedBounds = align.GetAxisAlignedBoundingBox();

			// assert the align in built correctly
			Assert.AreEqual(1, scene.Children.Count);
			Assert.AreEqual(2, align.Children.Count);
			Assert.IsTrue(align.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(0, 0, 0), new Vector3(60, 70, 40)), 1.0));

			align.SelectedChild = new SelectedChildren() { cube.ID.ToString() };

			Assert.IsTrue(align.GetAxisAlignedBoundingBox().Equals(unalignedBounds, 1.0));

			// turn align on
			align.XAlign = Align.Min;
			await align.Rebuild();
			Assert.IsTrue(align.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(40, 0, 0), new Vector3(80, 70, 40)), 1.0));

			// turn it off
			align.XAlign = Align.None;
			await align.Rebuild();
			Assert.IsTrue(align.GetAxisAlignedBoundingBox().Equals(unalignedBounds, 1.0));

			// turn it back on
			align.XAlign = Align.Min;
			await align.Rebuild();
			Assert.IsTrue(align.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(40, 0, 0), new Vector3(80, 70, 40)), 1.0));

			// remove the align and assert stuff moved back to where it started
			align.Cancel(null);
			Assert.IsTrue(cube.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(40, 50, 0), new Vector3(60, 70, 20)), .01));
			Assert.IsTrue(bigCube.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(0, 0, 0), new Vector3(40, 40, 40)), .01));
		}

		[Test, Category("InteractiveScene")]
		public async Task AabbCalculatedCorrectlyForCurvedFitObjects()
		{
			StartupMC();

			var root = new Object3D();
			var cube = await CubeObject3D.Create(20, 20, 20);
			var fit = await FitToBoundsObject3D_3.Create(cube);

			fit.Width = 50;
			fit.Depth = 20;
			fit.Height = 20;
			fit.Invalidate(InvalidateType.Properties);

			Assert.IsTrue(fit.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(-25, -10, -10), new Vector3(25, 10, 10)), 1.0));

			var curve = new CurveObject3D_3()
			{
				BendType = CurveObject3D_3.BendTypes.Diameter,
				Diameter = 50
			};
			curve.Children.Add(fit);
			await curve.Rebuild();
			var curveAabb = curve.GetAxisAlignedBoundingBox();
			root.Children.Add(curve);
			var rootAabb = root.GetAxisAlignedBoundingBox();
			Assert.IsTrue(rootAabb.Equals(new AxisAlignedBoundingBox(new Vector3(-17.5, -9.9, -10), new Vector3(17.5, 11.97, 10)), 1.0));
		}
	}
}
