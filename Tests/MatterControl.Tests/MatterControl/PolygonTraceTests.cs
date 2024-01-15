/*
Copyright (c) 2014, Lars Brubaker
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
using MatterHackers.Agg.Tests;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using NUnit.Framework;
using System.IO;
using System.Threading;

namespace MatterHackers.RayTracer
{
	[TestFixture, Category("Agg.RayTracer")]
	public class PolygonTraceTests
	{
		[Test, Ignore("WorkInProgress")]
		public void RayBundleSameResultAsIndividualRays()
		{
		}

		[Test]
		public void TriangleMajorAxis()
		{
			var triangle0 = new TriangleShape(
				new Vector3(-47.06726, 16.94526, 1.143421),
				new Vector3(-30.66048, 39.52726, 1.143422),
				new Vector3(-45.91038, 19.8672, 1.143422),
				null);

			Assert.AreEqual(2, triangle0.MajorAxis);

			var triangle1 = new TriangleShape(
				new Vector3(0, 1, 0),
				new Vector3(0, 0, 1),
				new Vector3(0, 0, 0),
				null);

			Assert.AreEqual(0, triangle1.MajorAxis);

			var triangle2 = new TriangleShape(
				new Vector3(1, 0, 0),
				new Vector3(0, 0, 1),
				new Vector3(0, 0, 0),
				null);

			Assert.AreEqual(1, triangle2.MajorAxis);
		}

		[Test]
		public void CorrectRayOnCircle()
		{
			var testPartPath = TestContext.CurrentContext.ResolveProjectPath(new string[] { "..", "..", "..", "examples", "RayTracerTest" });

			var testPart = Path.Combine(testPartPath, "circle_100x100_centered.stl");
			Mesh simpleMesh = StlProcessing.Load(testPart, CancellationToken.None);
			var bvhCollection = MeshToBVH.Convert(simpleMesh);

			var scene = new Scene();
			scene.shapes.Add(bvhCollection);

			RayTracer raytracer = new RayTracer()
			{
				AntiAliasing = AntiAliasing.None,
				MultiThreaded = false,
			};

			int samples = 40;
			var advance = MathHelper.Tau / samples;

			TestSingleAngle(scene, raytracer, advance, 15);

			for (int i = 0; i < samples; i++)
			{
				TestSingleAngle(scene, raytracer, advance, i);
			}
		}

		private static void TestSingleAngle(Scene scene, RayTracer raytracer, double advance, int i)
		{
			var sampleXY = new Vector2(48, 0);
			sampleXY.Rotate(advance * i);
			Vector3 rayOrigin = new Vector3(sampleXY, 10);

			Ray ray = new Ray(rayOrigin, -Vector3.UnitZ);
			IntersectInfo primaryInfo = raytracer.TracePrimaryRay(ray, scene);
			Assert.IsTrue(primaryInfo.HitType == IntersectionType.FrontFace, "always have a hit");
		}

		[Test]
		public void PolygonHitTests()
		{
			SolidMaterial redStuff = new SolidMaterial(new ColorF(1, 0, 0), 0, 0, 2);
			{
				TriangleShape facingPositiveX = new TriangleShape(new Vector3(0, 1, -1), new Vector3(0, 0, 1), new Vector3(0, -1, -1), redStuff);
				IntersectInfo positiveXInfo = facingPositiveX.GetClosestIntersection(new Ray(new Vector3(1, 0, 0), new Vector3(-1, 0, 0)));
				Assert.IsTrue(positiveXInfo.HitPosition == new Vector3(0, 0, 0));
				Assert.IsTrue(positiveXInfo.HitType == IntersectionType.FrontFace);
				Assert.IsTrue(positiveXInfo.ClosestHitObject == facingPositiveX);
				Assert.IsTrue(positiveXInfo.DistanceToHit == 1);

				IntersectInfo negativeXInfo = facingPositiveX.GetClosestIntersection(new Ray(new Vector3(-1, 0, 0), new Vector3(1, 0, 0)));
				Assert.IsTrue(negativeXInfo == null);
			}
			{
				TriangleShape facingNegativeX = new TriangleShape(new Vector3(0, -1, -1), new Vector3(0, 0, 1), new Vector3(0, 1, -1), redStuff);
				IntersectInfo positiveXInfo = facingNegativeX.GetClosestIntersection(new Ray(new Vector3(1, 0, 0), new Vector3(-1, 0, 0)));
				Assert.IsTrue(positiveXInfo == null);

				IntersectInfo negativeXInfo = facingNegativeX.GetClosestIntersection(new Ray(new Vector3(-1, 0, 0), new Vector3(1, 0, 0)));
				Assert.IsTrue(negativeXInfo.HitPosition == new Vector3(0, 0, 0));
				Assert.IsTrue(negativeXInfo.HitType == IntersectionType.FrontFace);
				Assert.IsTrue(negativeXInfo.ClosestHitObject == facingNegativeX);
				Assert.IsTrue(negativeXInfo.DistanceToHit == 1);
			}
		}
	}
}