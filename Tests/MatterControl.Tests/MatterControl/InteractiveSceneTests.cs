﻿/*
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

using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.VectorMath;
using NUnit.Framework;
using System.Linq;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture]
	public class InteractiveSceneTests
	{
		[Test, Category("InteractiveScene")]
		public void FlatenAsExpectedForBooleanOperations()
		{
			// Combine has correct number of results
			{
				var root = new Object3D();
				var cubeA = new CubeObject3D(20, 20, 20);
				var cubeB = new CubeObject3D(20, 20, 20);
				var offsetCubeB = new TranslateObject3D(cubeB, 10);

				var union = new CombineObject3D();
				union.Children.Add(cubeA);
				union.Children.Add(offsetCubeB);
				root.Children.Add(union);

				union.Combine();
				union.Flatten(null);

				Assert.AreEqual(1, root.Children.Count());
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(new AxisAlignedBoundingBox(
					-10, -10, -10,
					20, 10, 10).Equals(rootAabb, .001));
			}

			// make sure the MatterCAD add function is working
			{
				var cubeA = new CubeObject3D(20, 20, 20);
				var cubeB = new CubeObject3D(20, 20, 20);
				var offsetCubeB = new TranslateObject3D(cubeB, 10);

				var plus = cubeA.Plus(offsetCubeB);

				Assert.AreEqual(0, plus.Children.Count());
				Assert.IsTrue(plus.Mesh != null);
				var aabb = plus.GetAxisAlignedBoundingBox();
				Assert.IsTrue(new AxisAlignedBoundingBox(
					-10, -10, -10,
					20, 10, 10).Equals(aabb, .001));
			}

			// Subtract has correct number of results
			{
				var root = new Object3D();
				var cubeA = new CubeObject3D(20, 20, 20);
				var cubeB = new CubeObject3D(20, 20, 20);
				var offsetCubeB = new TranslateObject3D(cubeB, 10);

				var subtract = new SubtractObject3D();
				subtract.Children.Add(cubeA);
				subtract.Children.Add(offsetCubeB);
				subtract.ItemsToSubtract.Add(offsetCubeB.ID);
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
				var cubeA = new CubeObject3D(20, 20, 20);
				var cubeB = new CubeObject3D(20, 20, 20);
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
		public void AabbCalculatedCorrectlyForPinchedFitObjects()
		{
			// build without pinch
			{
				var root = new Object3D();
				var cube = new CubeObject3D(20, 20, 20);
				root.Children.Add(cube);
				Assert.IsTrue(root.GetAxisAlignedBoundingBox().Equals(new AxisAlignedBoundingBox(new Vector3(-10, -10, -10), new Vector3(10, 10, 10)), .001));
				root.Children.Remove(cube);
				var fit = FitToBoundsObject3D_2.Create(cube);

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
				var cube = new CubeObject3D(20, 20, 20);
				var fit = FitToBoundsObject3D_2.Create(cube);

				fit.SizeX = 50;
				fit.SizeY = 20;
				fit.SizeZ = 20;

				var pinch = new PinchObject3D();
				pinch.Children.Add(fit);
				root.Children.Add(pinch);
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(rootAabb.Equals(new AxisAlignedBoundingBox(new Vector3(-10, -10, -10), new Vector3(10, 10, 10)), .001));
			}

			// build with translate
			{
				var root = new Object3D();
				var cube = new CubeObject3D(20, 20, 20);

				var translate = new TranslateObject3D(cube, 11, 0, 0);

				root.Children.Add(translate);
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(rootAabb.Equals(new AxisAlignedBoundingBox(new Vector3(1, -10, -10), new Vector3(21, 10, 10)), .001));
			}

			// build with pinch and translate
			{
				var root = new Object3D();
				var cube = new CubeObject3D(20, 20, 20);

				var translate = new TranslateObject3D(cube, 11, 0, 0);

				var pinch = new PinchObject3D();
				pinch.Children.Add(translate);
				root.Children.Add(pinch);
				pinch.Invalidate(new InvalidateArgs(pinch, InvalidateType.Properties));
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(rootAabb.Equals(new AxisAlignedBoundingBox(new Vector3(1, -10, -10), new Vector3(21, 10, 10)), .001));
			}

			// build with pinch and translate
			{
				var root = new Object3D();
				var cube = new CubeObject3D(20, 20, 20);
				var fit = FitToBoundsObject3D_2.Create(cube);

				fit.SizeX = 50;
				fit.SizeY = 20;
				fit.SizeZ = 20;

				var translate = new TranslateObject3D(fit, 11, 0, 0);

				var pinch = new PinchObject3D();
				pinch.Children.Add(translate);
				pinch.Invalidate(new InvalidateArgs(pinch, InvalidateType.Properties));
				root.Children.Add(pinch);
				var rootAabb = root.GetAxisAlignedBoundingBox();
				Assert.IsTrue(rootAabb.Equals(new AxisAlignedBoundingBox(new Vector3(1, -10, -10), new Vector3(21, 10, 10)), .001));
			}
		}

		[Test, Category("InteractiveScene")]
		public void AabbCalculatedCorrectlyForCurvedFitObjects()
		{
			var root = new Object3D();
			var cube = new CubeObject3D(20, 20, 20);
			var fit = FitToBoundsObject3D_2.Create(cube);

			fit.SizeX = 50;
			fit.SizeY = 20;
			fit.SizeZ = 20;

			var curve = new CurveObject3D();
			curve.Children.Add(fit);
			curve.Invalidate(new InvalidateArgs(curve, InvalidateType.Properties));
			root.Children.Add(curve);
			var rootAabb = root.GetAxisAlignedBoundingBox();
			Assert.IsTrue(rootAabb.Equals(new AxisAlignedBoundingBox(new Vector3(-25, 4, -10), new Vector3(25, 15, 10)), 1.0));
		}
	}
}
