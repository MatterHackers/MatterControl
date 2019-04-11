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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.VectorMath;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture]
	public class SupportGeneratorTests
	{
		[Test, Category("Support Generator")]
		public async Task SupportsFromBedTests()
		{
			var minimumSupportHeight = .05;

			// Set the static data to point to the directory of MatterControl
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			// make a single cube in the air and ensure that support is generated
			//   _________
			//   |       |
			//   |       |
			//   |_______|
			//
			//______________
			{
				InteractiveScene scene = new InteractiveScene();

				var cube = await CubeObject3D.Create(20, 20, 20);
				var aabb = cube.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cube.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabb.MinXYZ.Z + 15);
				scene.Children.Add(cube);

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight);
				supportGenerator.SupportType = SupportGenerator.SupportGenerationType.From_Bed;
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.Greater(scene.Children.Count, 1, "We should have added some support");
				foreach (var support in scene.Children.Where(i => i.OutputType == PrintOutputTypes.Support))
				{
					Assert.AreEqual(0, support.GetAxisAlignedBoundingBox().MinXYZ.Z, .001, "Support columns are all on the bed");
					Assert.AreEqual(15, support.GetAxisAlignedBoundingBox().ZSize, .02, "Support columns should be the right height from the bed");
				}
			}

			// make a single cube in the bed and ensure that no support is generated
			//   _________
			//   |       |
			// __|       |__
			//   |_______|
			//
			{
				InteractiveScene scene = new InteractiveScene();

				var cube = await CubeObject3D.Create(20, 20, 20);
				var aabb = cube.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cube.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabb.MinXYZ.Z - 5);
				scene.Children.Add(cube);

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight);
				supportGenerator.SupportType = SupportGenerator.SupportGenerationType.From_Bed;
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.AreEqual(1, scene.Children.Count, "We should not have added any support");
			}

			// make a cube on the bed and single cube in the air and ensure that support is not generated
			//   _________
			//   |       |
			//   |       |
			//   |_______|
			//   _________
			//   |       |
			//   |       |
			//___|_______|___
			{
				InteractiveScene scene = new InteractiveScene();

				var cubeOnBed = await CubeObject3D.Create(20, 20, 20);
				var aabbBed = cubeOnBed.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cubeOnBed.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabbBed.MinXYZ.Z - 5);
				scene.Children.Add(cubeOnBed);

				var cubeInAir = await CubeObject3D.Create(20, 20, 20);
				var aabbAir = cubeInAir.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cubeInAir.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabbAir.MinXYZ.Z + 25);
				scene.Children.Add(cubeInAir);

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight);
				supportGenerator.SupportType = SupportGenerator.SupportGenerationType.From_Bed;
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.AreEqual(2, scene.Children.Count, "We should not have added support");
			}

			// make a single cube in the bed and another cube on top, ensure that no support is generated
			//   _________
			//   |       |
			//   |       |
			//   |_______|
			//   _________
			//   |       |
			// __|       |__
			//   |_______|
			//
			{
				InteractiveScene scene = new InteractiveScene();

				var cubeOnBed = await CubeObject3D.Create(20, 20, 20);
				var aabbBed = cubeOnBed.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cubeOnBed.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabbBed.MinXYZ.Z);
				scene.Children.Add(cubeOnBed);

				var cubeInAir = await CubeObject3D.Create(20, 20, 20);
				var aabbAir = cubeInAir.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cubeInAir.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabbAir.MinXYZ.Z + 25);
				scene.Children.Add(cubeInAir);

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight);
				supportGenerator.SupportType = SupportGenerator.SupportGenerationType.From_Bed;
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.AreEqual(2, scene.Children.Count, "We should not have added support");
			}

			// make a cube on the bed and another cube exactly on top of it and ensure that support is not generated
			//   _________
			//   |       |
			//   |       |
			//   |_______|
			//   |       |
			//   |       |
			//___|_______|___
			{
				InteractiveScene scene = new InteractiveScene();

				var cubeOnBed = await CubeObject3D.Create(20, 20, 20);
				var aabbBed = cubeOnBed.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cubeOnBed.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabbBed.MinXYZ.Z);
				scene.Children.Add(cubeOnBed);

				var cubeInAir = await CubeObject3D.Create(20, 20, 20);
				var aabbAir = cubeInAir.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cubeInAir.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabbAir.MinXYZ.Z + 20);
				scene.Children.Add(cubeInAir);

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight);
				supportGenerator.SupportType = SupportGenerator.SupportGenerationType.From_Bed;
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.AreEqual(2, scene.Children.Count, "We should not have added support");
			}

			// make a cube on the bed and single cube in the air that intersects it and ensure that support is not generated
			//    _________
			//    |       |
			//    |______ |  // top cube actually exactly on top of bottom cube
			//   ||______||
			//   |       |
			//___|_______|___
			{
				InteractiveScene scene = new InteractiveScene();

				var cubeOnBed = await CubeObject3D.Create(20, 20, 20);
				var aabbBed = cubeOnBed.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cubeOnBed.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabbBed.MinXYZ.Z);
				scene.Children.Add(cubeOnBed);

				var cubeInAir = await CubeObject3D.Create(20, 20, 20);
				var aabbAir = cubeInAir.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cubeInAir.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabbAir.MinXYZ.Z + 15);
				scene.Children.Add(cubeInAir);

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight);
				supportGenerator.SupportType = SupportGenerator.SupportGenerationType.From_Bed;
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.AreEqual(2, scene.Children.Count, "We should not have added support");
			}

			// Make a cube on the bed and single cube in the air that intersects it. 
			// SELECT the cube on top
			// Ensure that support is not generated.
			//    _________
			//    |       |
			//    |______ |  // top cube actually exactly on top of bottom cube
			//   ||______||
			//   |       |
			//___|_______|___
			{
				InteractiveScene scene = new InteractiveScene();

				var cubeOnBed = await CubeObject3D.Create(20, 20, 20);
				var aabbBed = cubeOnBed.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cubeOnBed.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabbBed.MinXYZ.Z);
				scene.Children.Add(cubeOnBed);

				var cubeInAir = await CubeObject3D.Create(20, 20, 20);
				var aabbAir = cubeInAir.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cubeInAir.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabbAir.MinXYZ.Z + 15);
				scene.Children.Add(cubeInAir);

				scene.SelectedItem = cubeInAir;

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight);
				supportGenerator.SupportType = SupportGenerator.SupportGenerationType.From_Bed;
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.AreEqual(2, scene.Children.Count, "We should not have added support");
			}

			// Make a cube above the bed and a second above that. Ensure only one set of support material
			//   _________
			//   |       |
			//   |       |
			//   |_______|
			//   _________
			//   |       |
			//   |       |
			//   |_______|
			//_______________
			{
				InteractiveScene scene = new InteractiveScene();

				var cube5AboveBed = await CubeObject3D.Create(20, 20, 20);
				var aabb5Above = cube5AboveBed.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cube5AboveBed.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabb5Above.MinXYZ.Z + 5);
				scene.Children.Add(cube5AboveBed);

				var cube30AboveBed = await CubeObject3D.Create(20, 20, 20);
				var aabb30Above = cube30AboveBed.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cube30AboveBed.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabb30Above.MinXYZ.Z + 30);
				scene.Children.Add(cube30AboveBed);

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight);
				supportGenerator.SupportType = SupportGenerator.SupportGenerationType.From_Bed;
				await supportGenerator.Create(null, CancellationToken.None);

				Assert.Greater(scene.Children.Count, 1, "We should have added some support");
				foreach (var support in scene.Children.Where(i => i.OutputType == PrintOutputTypes.Support))
				{
					Assert.AreEqual(0, support.GetAxisAlignedBoundingBox().MinXYZ.Z, .001, "Support columns are all on the bed");
					Assert.AreEqual(5, support.GetAxisAlignedBoundingBox().ZSize, .02, "Support columns should be the right height from the bed");
				}
			}
		}

		[Test, Category("Support Generator")]
		public async Task SupportsEverywhereTests()
		{
			var minimumSupportHeight = .05;

			// Set the static data to point to the directory of MatterControl
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			// make a single cube in the air and ensure that support is generated
			//   _________
			//   |       |
			//   |       |
			//   |_______|
			//
			//______________
			{
				InteractiveScene scene = new InteractiveScene();

				var cube = await CubeObject3D.Create(20, 20, 20);
				var aabb = cube.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cube.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabb.MinXYZ.Z + 15);
				scene.Children.Add(cube);

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight);
				supportGenerator.SupportType = SupportGenerator.SupportGenerationType.Normal;
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.Greater(scene.Children.Count, 1, "We should have added some support");
				foreach (var support in scene.Children.Where(i => i.OutputType == PrintOutputTypes.Support))
				{
					Assert.AreEqual(0, support.GetAxisAlignedBoundingBox().MinXYZ.Z, .001, "Support columns are all on the bed");
					Assert.AreEqual(15, support.GetAxisAlignedBoundingBox().ZSize, .02, "Support columns should be the right height from the bed");
				}
			}

			// make a cube on the bed and single cube in the air and ensure that support is not generated
			//   _________
			//   |       |
			//   |       |
			//   |_______|
			//   _________
			//   |       |
			//   |       |
			//___|_______|___
			{
				InteractiveScene scene = new InteractiveScene();

				var cubeOnBed = await CubeObject3D.Create(20, 20, 20);
				var aabbBed = cubeOnBed.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cubeOnBed.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabbBed.MinXYZ.Z);
				scene.Children.Add(cubeOnBed);

				var cubeInAir = await CubeObject3D.Create(20, 20, 20);
				var aabbAir = cubeInAir.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cubeInAir.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabbAir.MinXYZ.Z + 25);
				scene.Children.Add(cubeInAir);

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight);
				supportGenerator.SupportType = SupportGenerator.SupportGenerationType.Normal;
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.Greater(scene.Children.Count, 2, "We should have added some support");
				foreach (var support in scene.Children.Where(i => i.OutputType == PrintOutputTypes.Support))
				{
					Assert.AreEqual(20, support.GetAxisAlignedBoundingBox().MinXYZ.Z, .001, "Support columns are all on the first cube");
					Assert.AreEqual(5, support.GetAxisAlignedBoundingBox().ZSize, .02, "Support columns should be the right height from the bed");
				}
			}

			// make a cube on the bed and single cube in the air that intersects it and ensure that support is not generated
			//    _________
			//    |       |
			//    |______ |  // top cube actually exactly on top of bottom cube
			//   ||______||
			//   |       |
			//___|_______|___
			{
				InteractiveScene scene = new InteractiveScene();

				var cubeOnBed = await CubeObject3D.Create(20, 20, 20);
				var aabbBed = cubeOnBed.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cubeOnBed.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabbBed.MinXYZ.Z);
				scene.Children.Add(cubeOnBed);

				var cubeInAir = await CubeObject3D.Create(20, 20, 20);
				var aabbAir = cubeInAir.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cubeInAir.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabbAir.MinXYZ.Z + 15);
				scene.Children.Add(cubeInAir);

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight);
				supportGenerator.SupportType = SupportGenerator.SupportGenerationType.Normal;
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.AreEqual(2, scene.Children.Count, "We should not have added support");
			}

			// make a cube on the bed and another cube exactly on top of it and ensure that support is not generated
			//   _________
			//   |       |
			//   |       |
			//   |_______|
			//   |       |
			//   |       |
			//___|_______|___
			{
				InteractiveScene scene = new InteractiveScene();

				var cubeOnBed = await CubeObject3D.Create(20, 20, 20);
				var aabbBed = cubeOnBed.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cubeOnBed.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabbBed.MinXYZ.Z);
				scene.Children.Add(cubeOnBed);

				var cubeInAir = await CubeObject3D.Create(20, 20, 20);
				var aabbAir = cubeInAir.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cubeInAir.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabbAir.MinXYZ.Z + 20);
				scene.Children.Add(cubeInAir);

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight);
				supportGenerator.SupportType = SupportGenerator.SupportGenerationType.From_Bed;
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.AreEqual(2, scene.Children.Count, "We should not have added support");
			}

			// Make a cube above the bed and a second above that. Ensure only one set of support material
			//   _________
			//   |       |
			//   |       |
			//   |_______|
			//   _________
			//   |       |
			//   |       |
			//   |_______|
			//_______________
			{
				InteractiveScene scene = new InteractiveScene();

				var cube5AboveBed = await CubeObject3D.Create(20, 20, 20);
				var aabb5Above = cube5AboveBed.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cube5AboveBed.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabb5Above.MinXYZ.Z + 5);
				scene.Children.Add(cube5AboveBed);

				var cube30AboveBed = await CubeObject3D.Create(20, 20, 20);
				var aabb30Above = cube30AboveBed.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cube30AboveBed.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabb30Above.MinXYZ.Z + 30);
				scene.Children.Add(cube30AboveBed);

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight);
				supportGenerator.SupportType = SupportGenerator.SupportGenerationType.Normal;
				await supportGenerator.Create(null, CancellationToken.None);

				Assert.Greater(scene.Children.Count, 2, "We should have added some support");
				var bedSupportCount = 0;
				var airSupportCount = 0;
				foreach (var support in scene.Children.Where(i => i.OutputType == PrintOutputTypes.Support))
				{
					var aabb = support.GetAxisAlignedBoundingBox();
					Assert.AreEqual(5, aabb.ZSize, .001, "Support columns should be the right height from the bed");
					if (aabb.MinXYZ.Z > -.001 && aabb.MinXYZ.Z < .001) // it is on the bed
					{
						// keep track of the count
						bedSupportCount++;
					}
					else
					{
						airSupportCount++;
						// make sure it is the right height
						Assert.AreEqual(25, aabb.MinXYZ.Z, .001, "Support columns are all on the bed");
					}
				}

				Assert.AreEqual(bedSupportCount, airSupportCount, "Same number of support columns in each space.");
			}
		}

		[Test, Category("Support Generator")]
		public void TopBottomWalkingTest()
		{
			// a box in the air
			{
				var planes = new List<(double z, bool bottom)>()
				{
					(0, false),  // top at 0 (the bed)
					(5, true),   // bottom at 5 (the bottom of a box)
					(10, false), // top at 10 (the top of the box)
				};

				int bottom = SupportGenerator.GetNextBottom(0, planes, 0);
				Assert.AreEqual(1, bottom); // we get the bottom

				int bottom1 = SupportGenerator.GetNextBottom(1, planes, 0);
				Assert.AreEqual(-1, bottom1, "There are no more bottoms so we get back a -1.");
			}

			// two boxes, the bottom touching the bed, the top touching the bottom
			{
				var planes = new List<(double z, bool bottom)>()
				{
					(0, false),  // top at 0 (the bed)
					(0, true),  // bottom at 0 (box a on bed)
					(10, false), // top at 10 (box a top)
					(10, true), // bottom at 10 (box b bottom)
					(20, false) // top at 20 (box b top)
				};

				int bottom = SupportGenerator.GetNextBottom(0, planes, 0);
				Assert.AreEqual(-1, bottom, "The boxes are sitting on the bed and no support is required");
			}

			// two boxes, the bottom touching the bed, the top inside the bottom
			{
				var planes = new List<(double z, bool bottom)>()
				{
					(0, false),  // top at 0 (the bed)
					(0, true),  // bottom at 0 (box a on bed)
					(5, true), // bottom at 5 (box b bottom)
					(10, false), // top at 10 (box a top)
					(20, false) // top at 20 (box b top)
				};

				int bottom = SupportGenerator.GetNextBottom(0, planes, 0);
				Assert.AreEqual(-1, bottom, "The boxes are sitting on the bed and no support is required");
			}

			// get next top skips any tops before checking for bottom
			{
				var planes = new List<(double z, bool bottom)>()
				{
					(0, false),
					(5, true), 
					(10, false),
					(20, false),
					(25, true)
				};

				int top = SupportGenerator.GetNextTop(0, planes, 0);
				Assert.AreEqual(3, top);
			}
		}
	}
}
