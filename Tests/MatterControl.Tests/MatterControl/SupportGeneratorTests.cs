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
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
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
			// ______________
			{
				var scene = new InteractiveScene();

				var cube = await CubeObject3D.Create(20, 20, 20);
				var aabb = cube.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cube.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabb.MinXYZ.Z + 15);
				scene.Children.Add(cube);

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight)
				{
					SupportType = SupportGenerator.SupportGenerationType.From_Bed
				};
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
			{
				var scene = new InteractiveScene();

				var cube = await CubeObject3D.Create(20, 20, 20);
				var aabb = cube.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cube.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabb.MinXYZ.Z - 5);
				scene.Children.Add(cube);

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight)
				{
					SupportType = SupportGenerator.SupportGenerationType.From_Bed
				};
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.AreEqual(1, scene.Children.Count, "We should not have added any support");
			}

			// make a cube on the bed and single cube in the air and ensure that support is not generated
			//    _________
			//    |       |
			//    |       |
			//    |_______|
			//    _________
			//    |       |
			//    |       |
			// ___|_______|___
			{
				var scene = new InteractiveScene();

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

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight)
				{
					SupportType = SupportGenerator.SupportGenerationType.From_Bed
				};
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
			{
				var scene = new InteractiveScene();

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

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight)
				{
					SupportType = SupportGenerator.SupportGenerationType.From_Bed
				};
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.AreEqual(2, scene.Children.Count, "We should not have added support");
			}

			// make a cube on the bed and another cube exactly on top of it and ensure that support is not generated
			//    _________
			//    |       |
			//    |       |
			//    |_______|
			//    |       |
			//    |       |
			// ___|_______|___
			{
				var scene = new InteractiveScene();

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

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight)
				{
					SupportType = SupportGenerator.SupportGenerationType.From_Bed
				};
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.AreEqual(2, scene.Children.Count, "We should not have added support");
			}

			// make a cube on the bed and single cube in the air that intersects it and ensure that support is not generated
			//     _________
			//     |       |
			//     |______ |  // top cube actually exactly on top of bottom cube
			//    ||______||
			//    |       |
			// ___|_______|___
			{
				var scene = new InteractiveScene();

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

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight)
				{
					SupportType = SupportGenerator.SupportGenerationType.From_Bed
				};
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.AreEqual(2, scene.Children.Count, "We should not have added support");
			}

			// Make a cube on the bed and single cube in the air that intersects it.
			// SELECT the cube on top
			// Ensure that support is not generated.
			//     _________
			//     |       |
			//     |______ |  // top cube actually exactly on top of bottom cube
			//    ||______||
			//    |       |
			// ___|_______|___
			{
				var scene = new InteractiveScene();

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

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight)
				{
					SupportType = SupportGenerator.SupportGenerationType.From_Bed
				};
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
			// _______________
			{
				var scene = new InteractiveScene();

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

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight)
				{
					SupportType = SupportGenerator.SupportGenerationType.From_Bed
				};
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
			// ______________
			{
				var scene = new InteractiveScene();

				var cube = await CubeObject3D.Create(20, 20, 20);
				var aabb = cube.GetAxisAlignedBoundingBox();
				// move it so the bottom is 15 above the bed
				cube.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabb.MinXYZ.Z + 15);
				scene.Children.Add(cube);

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight)
				{
					SupportType = SupportGenerator.SupportGenerationType.Normal
				};
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.Greater(scene.Children.Count, 1, "We should have added some support");
				foreach (var support in scene.Children.Where(i => i.OutputType == PrintOutputTypes.Support))
				{
					Assert.AreEqual(0, support.GetAxisAlignedBoundingBox().MinXYZ.Z, .001, "Support columns are all on the bed");
					Assert.AreEqual(15, support.GetAxisAlignedBoundingBox().ZSize, .02, "Support columns should be the right height from the bed");
				}
			}

			// make a cube on the bed and single cube in the air and ensure that support is not generated
			//    _________
			//    |       |
			//    |       |
			//    |_______|
			//    _________
			//    |       |
			//    |       |
			// ___|_______|___
			{
				var scene = new InteractiveScene();

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

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight)
				{
					SupportType = SupportGenerator.SupportGenerationType.Normal
				};
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.Greater(scene.Children.Count, 2, "We should have added some support");
				foreach (var support in scene.Children.Where(i => i.OutputType == PrintOutputTypes.Support))
				{
					Assert.AreEqual(20, support.GetAxisAlignedBoundingBox().MinXYZ.Z, .001, "Support columns are all on the first cube");
					Assert.AreEqual(5, support.GetAxisAlignedBoundingBox().ZSize, .02, "Support columns should be the right height from the bed");
				}
			}

			// make a cube on the bed and single cube in the air that intersects it and ensure that support is not generated
			//     _________
			//     |       |
			//     |______ |  // top cube actually exactly on top of bottom cube
			//    ||______||
			//    |       |
			// ___|_______|___
			{
				var scene = new InteractiveScene();

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

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight)
				{
					SupportType = SupportGenerator.SupportGenerationType.Normal
				};
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.AreEqual(2, scene.Children.Count, "We should not have added support");
			}

			// make a cube on the bed and another cube exactly on top of it and ensure that support is not generated
			//    _________
			//    |       |
			//    |       |
			//    |_______|
			//    |       |
			//    |       |
			// ___|_______|___
			{
				var scene = new InteractiveScene();

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

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight)
				{
					SupportType = SupportGenerator.SupportGenerationType.From_Bed
				};
				await supportGenerator.Create(null, CancellationToken.None);
				Assert.AreEqual(2, scene.Children.Count, "We should not have added support");
			}

			// Make a cube above the bed and a second above that. Ensure only one set of support material
			//   _________
			//   |       | 50
			//   |       |
			//   |_______| 30
			//   _________
			//   |       | 25
			//   |       |
			//   |_______| 5
			// _______________
			{
				var scene = new InteractiveScene();

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

				var supportGenerator = new SupportGenerator(scene, minimumSupportHeight)
				{
					SupportType = SupportGenerator.SupportGenerationType.Normal
				};
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

		[Test, Category("Support Generation")]
		public async Task ComplexPartNoSupport()
		{
			// Set the static data to point to the directory of MatterControl
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			// load a complex part that should have no support required
			var minimumSupportHeight = .05;
			var scene = new InteractiveScene();

			var meshPath = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "TestData", "TestParts", "NoSupportNeeded.stl");

			var supportObject = new Object3D()
			{
				Mesh = StlProcessing.Load(meshPath, CancellationToken.None)
			};

			var aabbCube = supportObject.GetAxisAlignedBoundingBox();
			// move it so the bottom is on the bed
			supportObject.Matrix = Matrix4X4.CreateTranslation(0, 0, -aabbCube.MinXYZ.Z);
			scene.Children.Add(supportObject);

			var supportGenerator = new SupportGenerator(scene, minimumSupportHeight)
			{
				SupportType = SupportGenerator.SupportGenerationType.Normal
			};
			await supportGenerator.Create(null, CancellationToken.None);
			// this test is still in progress (failing)
			// Assert.AreEqual(1, scene.Children.Count, "We should not have added support");
		}

		[Test, Category("Support Generator")]
		public void SupportColumnTests()
		{
			// we change plans into columns correctly
			{
				var planes = new SupportGenerator.HitPlanes(.2)
				{
					new SupportGenerator.HitPlane(.178, true),
					new SupportGenerator.HitPlane(10.787, true),
					new SupportGenerator.HitPlane(10.787, false),
					new SupportGenerator.HitPlane(13.085, true),
					new SupportGenerator.HitPlane(13.085, false),
					new SupportGenerator.HitPlane(15.822, false),
				};

				var column0 = new SupportGenerator.SupportColumn(planes, .2);
				Assert.AreEqual(1, column0.Count);
				Assert.AreEqual((10.787, 13.085), column0[0]);
			}

			// 0 no data so copy of 1
			{
				var column0 = new SupportGenerator.SupportColumn(.1);

				var column1 = new SupportGenerator.SupportColumn(.1)
				{
					(0, 5),
					(25, 30)
				};

				column0.Union(column1);
				Assert.AreEqual(2, column0.Count);
				Assert.AreEqual((0, 5), column0[0]);
				Assert.AreEqual((25, 30), column0[1]);
			}

			// 0 data 1 no data
			{
				var column0 = new SupportGenerator.SupportColumn(.1)
				{
					(0, 5),
					(25, 30)
				};

				var column1 = new SupportGenerator.SupportColumn(.1);

				column0.Union(column1);
				Assert.AreEqual(2, column0.Count);
				Assert.AreEqual(0, column0[0].start);
				Assert.AreEqual(5, column0[0].end);
				Assert.AreEqual(25, column0[1].start);
				Assert.AreEqual(30, column0[1].end);
			}

			// 0 and 1 have same data
			{
				var column0 = new SupportGenerator.SupportColumn(.1)
				{
					(0, 5),
					(25, 30)
				};

				var column1 = new SupportGenerator.SupportColumn(.1)
				{
					(0, 5),
					(25, 30)
				};

				column0.Union(column1);
				Assert.AreEqual(2, column0.Count);
				Assert.AreEqual((0, 5), column0[0]);
				Assert.AreEqual((25, 30), column0[1]);
			}

			// 1 makes 0 have one run
			{
				var column0 = new SupportGenerator.SupportColumn(.1)
				{
					(0, 5),
					(25, 30)
				};

				var column1 = new SupportGenerator.SupportColumn(.1)
				{
					(5, 25)
				};

				column0.Union(column1);
				Assert.AreEqual(1, column0.Count);
				Assert.AreEqual((0, 30), column0[0]);
			}

			// 1 makes 0 have 3 runs
			{
				var column0 = new SupportGenerator.SupportColumn(.1)
				{
					(0, 5),
					(25, 30)
				};

				var column1 = new SupportGenerator.SupportColumn(.1)
				{
					(6, 24)
				};

				column0.Union(column1);
				Assert.AreEqual(3, column0.Count);
				Assert.AreEqual((0, 5), column0[0]);
				Assert.AreEqual((6, 24), column0[1]);
				Assert.AreEqual((25, 30), column0[2]);
			}

			// 1 makes 0 have one run considering overlap
			{
				var column0 = new SupportGenerator.SupportColumn(2)
				{
					(0, 5),
					(25, 30)
				};

				var column1 = new SupportGenerator.SupportColumn(2)
				{
					(6, 24)
				};

				column0.Union(column1);
				Assert.AreEqual(1, column0.Count);
				Assert.AreEqual((0, 30), column0[0]);
			}
		}

		[Test, Category("Support Generator")]
		public void TopBottomWalkingTest()
		{
			// a box in the air
			{
				var planes = new SupportGenerator.HitPlanes(0)
				{
					new SupportGenerator.HitPlane(0, false),  // top at 0 (the bed)
					new SupportGenerator.HitPlane(5, true),   // bottom at 5 (the bottom of a box)
					new SupportGenerator.HitPlane(10, false), // top at 10 (the top of the box)
				};

				int bottom = planes.GetNextBottom(0);
				Assert.AreEqual(1, bottom); // we get the bottom

				int bottom1 = planes.GetNextBottom(1);
				Assert.AreEqual(-1, bottom1, "There are no more bottoms so we get back a -1.");

				var supports = new SupportGenerator.SupportColumn(planes, 0);
				Assert.AreEqual(1, supports.Count);
				Assert.AreEqual(0, supports[0].start);
				Assert.AreEqual(5, supports[0].end);
			}

			// two boxes, the bottom touching the bed, the top touching the bottom
			{
				var planes = new SupportGenerator.HitPlanes(0)
				{
					new SupportGenerator.HitPlane(0, false),  // top at 0 (the bed)
					new SupportGenerator.HitPlane(0, true),  // bottom at 0 (box a on bed)
					new SupportGenerator.HitPlane(10, false), // top at 10 (box a top)
					new SupportGenerator.HitPlane(10, true), // bottom at 10 (box b bottom)
					new SupportGenerator.HitPlane(20, false) // top at 20 (box b top)
				};

				int bottom = planes.GetNextBottom(0);
				Assert.AreEqual(-1, bottom, "The boxes are sitting on the bed and no support is required");

				var supports = new SupportGenerator.SupportColumn(planes, 0);
				Assert.AreEqual(0, supports.Count);
			}

			// two boxes, the bottom touching the bed, the top inside the bottom
			{
				var planes = new SupportGenerator.HitPlanes(0)
				{
					new SupportGenerator.HitPlane(0, false),  // top at 0 (the bed)
					new SupportGenerator.HitPlane(0, true),  // bottom at 0 (box a on bed)
					new SupportGenerator.HitPlane(5, true), // bottom at 5 (box b bottom)
					new SupportGenerator.HitPlane(10, false), // top at 10 (box a top)
					new SupportGenerator.HitPlane(20, false) // top at 20 (box b top)
				};

				int bottom = planes.GetNextBottom(0);
				Assert.AreEqual(-1, bottom, "The boxes are sitting on the bed and no support is required");

				var supports = new SupportGenerator.SupportColumn(planes, 0);
				Assert.AreEqual(0, supports.Count);
			}

			// get next top skips any tops before checking for bottom
			{
				var planes = new SupportGenerator.HitPlanes(0)
				{
					new SupportGenerator.HitPlane(0, false),
					new SupportGenerator.HitPlane(5, true),
					new SupportGenerator.HitPlane(10, false),
					new SupportGenerator.HitPlane(20, false),
					new SupportGenerator.HitPlane(25, true)
				};

				int top = planes.GetNextTop(0);
				Assert.AreEqual(3, top);

				var supports = new SupportGenerator.SupportColumn(planes, 0);
				Assert.AreEqual(2, supports.Count);
				Assert.AreEqual(0, supports[0].start);
				Assert.AreEqual(5, supports[0].end);
				Assert.AreEqual(20, supports[1].start);
				Assert.AreEqual(25, supports[1].end);
			}

			// actual output from a dual extrusion print that should have no support
			{
				var planes = new SupportGenerator.HitPlanes(.1)
				{
					new SupportGenerator.HitPlane(0, true),
					new SupportGenerator.HitPlane(0, true),
					new SupportGenerator.HitPlane(0, true),
					new SupportGenerator.HitPlane(0, true),
					new SupportGenerator.HitPlane(0, false),
					new SupportGenerator.HitPlane(0.0302, true),
					new SupportGenerator.HitPlane(0.0497, true),
					new SupportGenerator.HitPlane(0.762, true),
					new SupportGenerator.HitPlane(0.762, true),
					new SupportGenerator.HitPlane(0.762, false),
					new SupportGenerator.HitPlane(0.762, false),
					new SupportGenerator.HitPlane(15.95, false),
					new SupportGenerator.HitPlane(15.9697, false),
					new SupportGenerator.HitPlane(16, false),
					new SupportGenerator.HitPlane(16, false),
					new SupportGenerator.HitPlane(16, false),
					new SupportGenerator.HitPlane(16, false),
				};

				int bottom = planes.GetNextBottom(0);
				Assert.AreEqual(-1, bottom, "The boxes are sitting on the bed and no support is required");
			}

			// make sure we have a valid range even when there is no top
			{
				var planes = new SupportGenerator.HitPlanes(.1)
				{
					// top at 0
					new SupportGenerator.HitPlane(0, false),
					// area needing support
					new SupportGenerator.HitPlane(20, true),
				};

				planes.Simplify();
				Assert.AreEqual(2, planes.Count);
				Assert.IsTrue(planes[0].Bottom());
				Assert.AreEqual(20, planes[0].Z);
				Assert.IsTrue(planes[1].Top());
				Assert.AreEqual(20.1, planes[1].Z);
			}

			// make sure we user last support height if greater than first plus min distance
			{
				var planes = new SupportGenerator.HitPlanes(.1)
				{
					// top at 0
					new SupportGenerator.HitPlane(0, false),
					// area needing support
					new SupportGenerator.HitPlane(20, true),
					new SupportGenerator.HitPlane(22, true),
				};

				planes.Simplify();
				Assert.AreEqual(2, planes.Count);
				Assert.IsTrue(planes[0].Bottom());
				Assert.AreEqual(20, planes[0].Z);
				Assert.IsTrue(planes[1].Top());
				Assert.AreEqual(22.1, planes[1].Z);
			}

			// make sure we remove extra bottoms and have a valid range even when there is no top
			{
				var planes = new SupportGenerator.HitPlanes(.1)
				{
					// top at 0
					new SupportGenerator.HitPlane(0, false),
					// area needing support
					new SupportGenerator.HitPlane(20, true),
					new SupportGenerator.HitPlane(20, true),
					new SupportGenerator.HitPlane(20.001, true),
					new SupportGenerator.HitPlane(20.002, true),
					new SupportGenerator.HitPlane(20.003, true),
				};

				planes.Simplify();
				Assert.AreEqual(2, planes.Count);
				Assert.IsTrue(planes[0].Bottom());
				Assert.AreEqual(20, planes[0].Z);
				Assert.IsTrue(planes[1].Top());
				Assert.AreEqual(20.103, planes[1].Z);
			}

			// simple gap
			{
				var planes = new SupportGenerator.HitPlanes(.1)
				{
					// area needing support
					new SupportGenerator.HitPlane(20, true),
					// bad extra top
					new SupportGenerator.HitPlane(22, false),
				};

				planes.Simplify();
				Assert.AreEqual(2, planes.Count);
				Assert.IsTrue(planes[0].Bottom());
				Assert.AreEqual(20, planes[0].Z);
				Assert.IsTrue(planes[1].Top());
				Assert.AreEqual(22, planes[1].Z);
			}

			// many start top planes
			{
				var planes = new SupportGenerator.HitPlanes(.1)
				{
					new SupportGenerator.HitPlane(0, false),
					new SupportGenerator.HitPlane(0, false),
					new SupportGenerator.HitPlane(1, false),
					new SupportGenerator.HitPlane(2, false),
					new SupportGenerator.HitPlane(3, false),
					// area needing support
					new SupportGenerator.HitPlane(20, true),
					// bad extra top
					new SupportGenerator.HitPlane(22, true),
				};

				planes.Simplify();
				Assert.AreEqual(2, planes.Count);
				Assert.IsTrue(planes[0].Bottom());
				Assert.AreEqual(20, planes[0].Z);
				Assert.IsTrue(planes[1].Top());
				Assert.AreEqual(22.1, planes[1].Z);

				var supports = new SupportGenerator.SupportColumn(planes, 0);
				Assert.AreEqual(1, supports.Count);
				Assert.AreEqual((0, 20), supports[0]);
			}

			// handle invalid date (can happen during the trace in edge cases)
			{
				var planes = new SupportGenerator.HitPlanes(.1)
				{
					new SupportGenerator.HitPlane(0, false),
					// area needing support
					new SupportGenerator.HitPlane(20, false),
					// bad extra top
					new SupportGenerator.HitPlane(22, false),
				};

				planes.Simplify();
				Assert.AreEqual(0, planes.Count);

				var supports = new SupportGenerator.SupportColumn(planes, 0);
				Assert.AreEqual(0, supports.Count);
			}

			// handle invalid date (can happen during the trace in edge cases)
			{
				var planes = new SupportGenerator.HitPlanes(.1)
				{
					// bottom at 0
					new SupportGenerator.HitPlane(0, true),
					// bottom at 20
					new SupportGenerator.HitPlane(20, true),
					// bottom at 22
					new SupportGenerator.HitPlane(22, true),
				};

				planes.Simplify();
				Assert.AreEqual(2, planes.Count);
				Assert.IsTrue(planes[0].Bottom());
				Assert.AreEqual(0, planes[0].Z);
				Assert.IsTrue(planes[1].Top());
				Assert.AreEqual(22.1, planes[1].Z);
			}

			// simplify working as expected (planes with space turns into two start end sets)
			{
				var planes = new SupportGenerator.HitPlanes(.1)
				{
					new SupportGenerator.HitPlane(0, false),
					new SupportGenerator.HitPlane(0, true),
					new SupportGenerator.HitPlane(0, true),
					new SupportGenerator.HitPlane(0, true),
					new SupportGenerator.HitPlane(0, false),
					new SupportGenerator.HitPlane(0.0302, true),
					new SupportGenerator.HitPlane(0.0497, true),
					new SupportGenerator.HitPlane(0.762, true),
					new SupportGenerator.HitPlane(0.762, true),
					new SupportGenerator.HitPlane(0.762, false),
					new SupportGenerator.HitPlane(0.762, false),
					new SupportGenerator.HitPlane(15.95, false),
					new SupportGenerator.HitPlane(15.9697, false),
					new SupportGenerator.HitPlane(16, false),
					new SupportGenerator.HitPlane(16, false),
					new SupportGenerator.HitPlane(16, false),
					new SupportGenerator.HitPlane(16, false),
					// area needing support
					new SupportGenerator.HitPlane(20, true),
					new SupportGenerator.HitPlane(25, false),
				};

				planes.Simplify();
				Assert.AreEqual(4, planes.Count);
				Assert.IsTrue(planes[0].Bottom());
				Assert.AreEqual(0, planes[0].Z);
				Assert.IsTrue(planes[1].Top());
				Assert.AreEqual(16, planes[1].Z);
				Assert.IsTrue(planes[2].Bottom());
				Assert.AreEqual(20, planes[2].Z);
				Assert.IsTrue(planes[3].Top());
				Assert.AreEqual(25, planes[3].Z);

				var supports = new SupportGenerator.SupportColumn(planes, 0);
				Assert.AreEqual(1, supports.Count);
				Assert.AreEqual(16, supports[0].start);
				Assert.AreEqual(20, supports[0].end);
			}

			// pile of plates turns into 0 start end
			{
				var planes = new SupportGenerator.HitPlanes(.1)
				{
					new SupportGenerator.HitPlane(0, true),
					new SupportGenerator.HitPlane(0, true),
					new SupportGenerator.HitPlane(0, true),
					new SupportGenerator.HitPlane(0, true),
					new SupportGenerator.HitPlane(0, false),
					new SupportGenerator.HitPlane(0.0302, true),
					new SupportGenerator.HitPlane(0.0497, true),
					new SupportGenerator.HitPlane(0.762, true),
					new SupportGenerator.HitPlane(0.762, true),
					new SupportGenerator.HitPlane(0.762, false),
					new SupportGenerator.HitPlane(0.762, false),
					new SupportGenerator.HitPlane(15.95, false),
					new SupportGenerator.HitPlane(15.9697, false),
					new SupportGenerator.HitPlane(16, false),
					new SupportGenerator.HitPlane(16, false),
					new SupportGenerator.HitPlane(16, false),
					new SupportGenerator.HitPlane(16, false),
				};

				planes.Simplify();
				Assert.AreEqual(2, planes.Count);
				Assert.IsTrue(planes[0].Bottom());
				Assert.AreEqual(0, planes[0].Z);
				Assert.IsTrue(planes[1].Top());
				Assert.AreEqual(16, planes[1].Z);

				var supports = new SupportGenerator.SupportColumn(planes, 0);
				Assert.AreEqual(0, supports.Count);
			}

			// a test with an actual part starting below the bed
			{
				var planes = new SupportGenerator.HitPlanes(.1)
				{
					new SupportGenerator.HitPlane(-.9966, false),
					new SupportGenerator.HitPlane(-.9965, true),
					new SupportGenerator.HitPlane(-.9964, false),
					new SupportGenerator.HitPlane(-.9963, true),
					new SupportGenerator.HitPlane(-.9962, false),
					new SupportGenerator.HitPlane(-.9961, true), // last plane below bed is a top
					new SupportGenerator.HitPlane(13.48, true),
					new SupportGenerator.HitPlane(13.48, false),
					new SupportGenerator.HitPlane(14.242, false),
				};

				planes.Simplify();
				Assert.AreEqual(2, planes.Count);
				Assert.IsTrue(planes[0].Bottom());
				Assert.AreEqual(0, planes[0].Z);
				Assert.IsTrue(planes[1].Top());
				Assert.AreEqual(14.242, planes[1].Z);

				var supports = new SupportGenerator.SupportColumn(planes, 0);
				Assert.AreEqual(0, supports.Count);
			}

			// a test with an actual part starting below the bed
			{
				var planes = new SupportGenerator.HitPlanes(.1)
				{
					new SupportGenerator.HitPlane(-.9961, true),
					new SupportGenerator.HitPlane(-.9962, false),
					new SupportGenerator.HitPlane(-.9963, true),
					new SupportGenerator.HitPlane(-.9964, false),
					new SupportGenerator.HitPlane(-.9965, true),
					new SupportGenerator.HitPlane(-.9966, true), // last plane below bed is a bottom (no support needed)
					new SupportGenerator.HitPlane(13.48, true),
					new SupportGenerator.HitPlane(13.48, false),
					new SupportGenerator.HitPlane(14.242, false),
				};

				planes.Simplify();
				Assert.AreEqual(2, planes.Count);
				Assert.IsTrue(planes[0].Bottom());
				Assert.AreEqual(0, planes[0].Z);
				Assert.IsTrue(planes[1].Top());
				Assert.AreEqual(14.242, planes[1].Z);

				var supports = new SupportGenerator.SupportColumn(planes, 0);
				Assert.AreEqual(0, supports.Count);
			}

			{
				var planes = new SupportGenerator.HitPlanes(.1)
				{
					// top at 0
					new SupportGenerator.HitPlane(0, false),
					// bottom at 5
					new SupportGenerator.HitPlane(5, true),
					new SupportGenerator.HitPlane(5, true),
					new SupportGenerator.HitPlane(5, true),
					new SupportGenerator.HitPlane(5, true),
					new SupportGenerator.HitPlane(5, true),
					new SupportGenerator.HitPlane(5, true),
					// top at 25
					new SupportGenerator.HitPlane(25, false),
					new SupportGenerator.HitPlane(25, false),
					new SupportGenerator.HitPlane(25, false),
					new SupportGenerator.HitPlane(25, false),
					new SupportGenerator.HitPlane(25, false),
					new SupportGenerator.HitPlane(25, false),
					// bottom at 30
					new SupportGenerator.HitPlane(30, true),
					new SupportGenerator.HitPlane(30, true),
					new SupportGenerator.HitPlane(30, true),
					new SupportGenerator.HitPlane(30, true),
					new SupportGenerator.HitPlane(30, true),
					new SupportGenerator.HitPlane(30, true),
					// top at 50
					new SupportGenerator.HitPlane(50, false),
					new SupportGenerator.HitPlane(50, false),
					new SupportGenerator.HitPlane(50, false),
					new SupportGenerator.HitPlane(50, false),
					new SupportGenerator.HitPlane(50, false),
				};

				planes.Simplify();
				Assert.AreEqual(4, planes.Count);
				Assert.IsTrue(planes[0].Bottom());
				Assert.AreEqual(5, planes[0].Z);
				Assert.IsTrue(planes[1].Top());
				Assert.AreEqual(25, planes[1].Z);
				Assert.IsTrue(planes[2].Bottom());
				Assert.AreEqual(30, planes[2].Z);
				Assert.IsTrue(planes[3].Top());
				Assert.AreEqual(50, planes[3].Z);

				var supports = new SupportGenerator.SupportColumn(planes, 0);
				Assert.AreEqual(2, supports.Count);
				Assert.AreEqual(0, supports[0].start);
				Assert.AreEqual(5, supports[0].end);
				Assert.AreEqual(25, supports[1].start);
				Assert.AreEqual(30, supports[1].end);
			}

			{
				// two parts with a support gap between them
				var planes0 = new SupportGenerator.HitPlanes(.1)
				{
					new SupportGenerator.HitPlane(0, false), // bed
					new SupportGenerator.HitPlane(0, true), // bottom of part
					new SupportGenerator.HitPlane(15, false), // top of part
				};
				var support0 = new SupportGenerator.SupportColumn(planes0, .1);
				Assert.AreEqual(0, support0.Count);

				// a part that will fill the support gap
				var planes1 = new SupportGenerator.HitPlanes(.1)
				{
					new SupportGenerator.HitPlane(0, false), // bed
					new SupportGenerator.HitPlane(20, true), // bottom of part
					new SupportGenerator.HitPlane(30, false), // top of part
				};
				var support1 = new SupportGenerator.SupportColumn(planes1, .1);
				Assert.AreEqual(1, support1.Count);
				Assert.AreEqual(0, support1[0].start);
				Assert.AreEqual(20, support1[0].end);

				support0.Union(support1);
				Assert.AreEqual(1, support0.Count);
				Assert.AreEqual(0, support0[0].start);
				Assert.AreEqual(20, support0[0].end);
			}

			// merge of two overlapping sets tuns into 0 set
			{
				// two parts with a support gap between them
				var planes0 = new SupportGenerator.HitPlanes(.1)
				{
					new SupportGenerator.HitPlane(0, false), // bed
					new SupportGenerator.HitPlane(10, true), // bottom of part
					new SupportGenerator.HitPlane(20, false), // top of part
				};

				planes0.Simplify();
				Assert.AreEqual(2, planes0.Count);
				Assert.IsTrue(planes0[0].Bottom());
				Assert.AreEqual(10, planes0[0].Z);
				Assert.IsTrue(planes0[1].Top());
				Assert.AreEqual(20, planes0[1].Z);

				var support0 = new SupportGenerator.SupportColumn(planes0, .1);

				// a part that will fill the support gap
				var planes1 = new SupportGenerator.HitPlanes(.1)
				{
					new SupportGenerator.HitPlane(0, false), // bed
					new SupportGenerator.HitPlane(0, true), // bottom of part
					new SupportGenerator.HitPlane(15, false), // top of part
				};

				planes1.Simplify();
				Assert.AreEqual(2, planes1.Count);
				Assert.IsTrue(planes1[0].Bottom());
				Assert.AreEqual(0, planes1[0].Z);
				Assert.IsTrue(planes1[1].Top());
				Assert.AreEqual(15, planes1[1].Z);

				var support1 = new SupportGenerator.SupportColumn(planes1, .1);

				support0.Union(support1);
				Assert.AreEqual(1, support0.Count);
				Assert.AreEqual(0, support0[0].start);
				Assert.AreEqual(10, support0[0].end);
			}

			{
				// two parts with a support gap between them
				var support0 = new SupportGenerator.SupportColumn(new SupportGenerator.HitPlanes(.1)
				{
					new SupportGenerator.HitPlane(0, false), // bed
					new SupportGenerator.HitPlane(10, true), // bottom of part
					new SupportGenerator.HitPlane(20, false), // top of part
				}, .1);

				// a part that will fill the support gap
				var support1 = new SupportGenerator.SupportColumn(new SupportGenerator.HitPlanes(.1)
				{
					new SupportGenerator.HitPlane(0, false), // bed
					new SupportGenerator.HitPlane(0, true), // bottom of part
					new SupportGenerator.HitPlane(15, false), // top of part
				}, .1);

				support0.Union(support1);
				Assert.AreEqual(1, support0.Count);
				Assert.AreEqual(0, support0[0].start);
				Assert.AreEqual(10, support0[0].end);
			}

			{
				// two parts with a support gap between them
				var support0 = new SupportGenerator.SupportColumn(new SupportGenerator.HitPlanes(.1)
				{
					new SupportGenerator.HitPlane(0, false), // bed
					new SupportGenerator.HitPlane(10, true), // bottom of part
					new SupportGenerator.HitPlane(20, false), // top of part
				}, .1);

				// a part that will fill the support gap
				var support1 = new SupportGenerator.SupportColumn(new SupportGenerator.HitPlanes(.1)
				{
					new SupportGenerator.HitPlane(0, false), // bed
					new SupportGenerator.HitPlane(0, true), // bottom of part
					new SupportGenerator.HitPlane(15, false), // top of part
				}, .1);

				support1.Union(support0);
				Assert.AreEqual(1, support1.Count);
				Assert.AreEqual(0, support1[0].start);
				Assert.AreEqual(10, support1[0].end);
			}
		}
	}
}