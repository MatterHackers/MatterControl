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
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.VectorMath;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture]
	public class LevelingTests
	{
		[Test, Category("Leveling")]
		public void Leveling7PointsNeverGetsTooHigh()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var printerSettings = ActiveSliceSettings.Instance;
			var levelingData = new PrintLevelingData(printerSettings);

			double radius = 100;
			levelingData.SampledPositions = new List<Vector3>();
			levelingData.SampledPositions.Add(new Vector3(130.00, 0.00, 0));
			levelingData.SampledPositions.Add(new Vector3(65.00, 112.58, 10));
			levelingData.SampledPositions.Add(new Vector3(-65.00, 112.58, 0));
			levelingData.SampledPositions.Add(new Vector3(-130.00, 0.00, 10));
			levelingData.SampledPositions.Add(new Vector3(-65.00, -112.58, 0));
			levelingData.SampledPositions.Add(new Vector3(65.00, -112.58, 10));

			levelingData.SampledPositions.Add(new Vector3(0, 0, 0));

			levelingData.SampledPositions.Add(new Vector3(0, 0, 6));

			Vector2 bedCenter = Vector2.Zero;

			RadialLevlingFunctions levelingFunctions7Point = new RadialLevlingFunctions(printerSettings, 6, levelingData, bedCenter);
			int totalPoints = 2000;
			for (int curPoint = 0; curPoint < totalPoints; curPoint++)
			{
				Vector2 currentTestPoint = new Vector2(radius, 0);
				currentTestPoint.Rotate(MathHelper.Tau / totalPoints * curPoint);
				Vector3 destPosition = new Vector3(currentTestPoint, 0);

				Vector3 outPosition = levelingFunctions7Point.GetPositionWithZOffset(destPosition);
				Assert.IsTrue(outPosition.Z <= 10);

				string outPositionString = levelingFunctions7Point.DoApplyLeveling(GetGCodeString(destPosition), destPosition);
				double outZ = 0;
				Assert.IsTrue(GCodeFile.GetFirstNumberAfter("Z", outPositionString, ref outZ));
				Assert.IsTrue(outZ <= 10);
			}
		}

		[Test, Category("Leveling")]
		public void Leveling7PointsCorectInterpolation()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var printerSettings = ActiveSliceSettings.Instance;
			var levelingData = new PrintLevelingData(printerSettings);

			double radius = 100;
			levelingData.SampledPositions = new List<Vector3>();
			Vector2 currentEdgePoint = new Vector2(radius, 0);
			for (int i = 0; i < 6; i++)
			{
				levelingData.SampledPositions.Add(new Vector3(currentEdgePoint, i));
				currentEdgePoint.Rotate(MathHelper.Tau / 6);
			}

			levelingData.SampledPositions.Add(new Vector3(0, 0, 6));

			Vector2 bedCenter = Vector2.Zero;

			RadialLevlingFunctions levelingFunctions7Point = new RadialLevlingFunctions(printerSettings, 6, levelingData, bedCenter);
			for (int curPoint = 0; curPoint < 6; curPoint++)
			{
				int nextPoint = curPoint < 5 ? curPoint + 1 : 0;

				// test actual sample position
				Vector2 currentTestPoint = new Vector2(radius, 0);
				currentTestPoint.Rotate(MathHelper.Tau / 6 * curPoint);
				Vector3 destPosition = new Vector3(currentTestPoint, 0);
				Vector3 outPosition = levelingFunctions7Point.GetPositionWithZOffset(destPosition);
				Assert.AreEqual(outPosition.Z, levelingData.SampledPositions[curPoint].Z, .001);
				string outPositionString = levelingFunctions7Point.DoApplyLeveling(GetGCodeString(destPosition), destPosition);
				Assert.AreEqual(GetGCodeString(outPosition), outPositionString);

				// test mid point between samples
				Vector3 midPoint = (levelingData.SampledPositions[curPoint] + levelingData.SampledPositions[nextPoint]) / 2;
				currentTestPoint = new Vector2(midPoint.X, midPoint.Y);
				destPosition = new Vector3(currentTestPoint, 0);
				outPosition = levelingFunctions7Point.GetPositionWithZOffset(destPosition);
				Assert.AreEqual(outPosition.Z, midPoint.Z, .001);
				outPositionString = levelingFunctions7Point.DoApplyLeveling(GetGCodeString(destPosition), destPosition);
				Assert.AreEqual(GetGCodeString(outPosition), outPositionString);

				// test mid point between samples with offset
				Vector3 midPointWithOffset = (levelingData.SampledPositions[curPoint] + levelingData.SampledPositions[nextPoint]) / 2 + new Vector3(0, 0, 3);
				currentTestPoint = new Vector2(midPointWithOffset.X, midPointWithOffset.Y);
				destPosition = new Vector3(currentTestPoint, 3);
				outPosition = levelingFunctions7Point.GetPositionWithZOffset(destPosition);
				Assert.AreEqual(outPosition.Z, midPointWithOffset.Z, .001);
				outPositionString = levelingFunctions7Point.DoApplyLeveling(GetGCodeString(destPosition), destPosition);
				Assert.AreEqual(GetGCodeString(outPosition), outPositionString);

				// test 1/2 angles (mid way between samples on radius)
				currentTestPoint = new Vector2(radius, 0);
				currentTestPoint.Rotate(MathHelper.Tau / 6 * (curPoint + .5));
				destPosition = new Vector3(currentTestPoint, 0);
				outPosition = levelingFunctions7Point.GetPositionWithZOffset(destPosition);
				// the center is the higest point so the point on the radius has to be less than the mid point of the sample points (it is lower)
				Assert.IsTrue(outPosition.Z < (levelingData.SampledPositions[curPoint].Z + levelingData.SampledPositions[nextPoint].Z) / 2 - .001);
				outPositionString = levelingFunctions7Point.DoApplyLeveling(GetGCodeString(destPosition), destPosition);
				Assert.AreEqual(GetGCodeString(outPosition), outPositionString);

				// test 1/2 to center
				currentTestPoint = new Vector2(radius / 2, 0);
				currentTestPoint.Rotate(MathHelper.Tau / 6 * curPoint);
				destPosition = new Vector3(currentTestPoint, 0);
				outPosition = levelingFunctions7Point.GetPositionWithZOffset(destPosition);
				Assert.AreEqual(outPosition.Z, (levelingData.SampledPositions[curPoint].Z + levelingData.SampledPositions[6].Z) / 2, .001);
				outPositionString = levelingFunctions7Point.DoApplyLeveling(GetGCodeString(destPosition), destPosition);
				Assert.AreEqual(GetGCodeString(outPosition), outPositionString);
			}

			Vector3 outPosition2 = levelingFunctions7Point.GetPositionWithZOffset(Vector3.Zero);
			Assert.AreEqual(outPosition2.Z, levelingData.SampledPositions[6].Z, .001);
		}


		[Test, Category("Leveling")]
		public void LevelingMesh3x3CorectInterpolation()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var printerSettings = ActiveSliceSettings.Instance;
			// a 2 x 2 mesh that goes form 0 on the left to 10 on the right
			{
				var levelingData = new PrintLevelingData(printerSettings);

				// put them in left to right - bottom to top
				levelingData.SampledPositions = new List<Vector3>();
				levelingData.SampledPositions.Add(new Vector3(0, 0, 0));
				levelingData.SampledPositions.Add(new Vector3(10, 0, 10));
				levelingData.SampledPositions.Add(new Vector3(0, 10, 0));
				levelingData.SampledPositions.Add(new Vector3(10, 10, 10));

				MeshLevlingFunctions levelingFunctionsMesh2x2 = new MeshLevlingFunctions(printerSettings, 2, 2, levelingData);

				// check on points
				AssertMeshLevelPoint(new Vector3(0, 0, 0), new Vector3(0, 0, 0), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(10, 0, 0), new Vector3(10, 0, 10), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(10, 10, 0), new Vector3(10, 10, 10), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(0, 10, 0), new Vector3(0, 10, 0), levelingFunctionsMesh2x2);

				// check raised on ponits
				AssertMeshLevelPoint(new Vector3(0, 0, 5), new Vector3(0, 0, 5), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(10, 0, 5), new Vector3(10, 0, 15), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(10, 10, 5), new Vector3(10, 10, 15), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(0, 10, 5), new Vector3(0, 10, 5), levelingFunctionsMesh2x2);

				// check between points
				AssertMeshLevelPoint(new Vector3(5, 0, 0), new Vector3(5, 0, 5), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(5, 0, 5), new Vector3(5, 0, 10), levelingFunctionsMesh2x2);

				// check outside points
				AssertMeshLevelPoint(new Vector3(-5, 0, 0), new Vector3(-5, 0, -5), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(-5, 0, 5), new Vector3(-5, 0, 0), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(15, 0, 0), new Vector3(15, 0, 15), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(15, 0, 5), new Vector3(15, 0, 20), levelingFunctionsMesh2x2);
			}

			// a 3 x 3 mesh that goes form 0 on the left to 10 on the right
			{
				var levelingData = new PrintLevelingData(printerSettings);

				// put them in left to right - bottom to top
				levelingData.SampledPositions = new List<Vector3>();
				levelingData.SampledPositions.Add(new Vector3(0, 0, 0));
				levelingData.SampledPositions.Add(new Vector3(5, 0, 5));
				levelingData.SampledPositions.Add(new Vector3(10, 0, 10));
				levelingData.SampledPositions.Add(new Vector3(0, 5, 0));
				levelingData.SampledPositions.Add(new Vector3(5, 5, 5));
				levelingData.SampledPositions.Add(new Vector3(10, 5, 10));
				levelingData.SampledPositions.Add(new Vector3(0, 10, 0));
				levelingData.SampledPositions.Add(new Vector3(5, 10, 5));
				levelingData.SampledPositions.Add(new Vector3(10, 10, 10));

				MeshLevlingFunctions levelingFunctionsMesh2x2 = new MeshLevlingFunctions(printerSettings, 3, 3, levelingData);

				// check on points
				AssertMeshLevelPoint(new Vector3(0, 0, 0), new Vector3(0, 0, 0), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(10, 0, 0), new Vector3(10, 0, 10), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(10, 10, 0), new Vector3(10, 10, 10), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(0, 10, 0), new Vector3(0, 10, 0), levelingFunctionsMesh2x2);

				// check raised on ponits
				AssertMeshLevelPoint(new Vector3(0, 0, 5), new Vector3(0, 0, 5), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(10, 0, 5), new Vector3(10, 0, 15), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(10, 10, 5), new Vector3(10, 10, 15), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(0, 10, 5), new Vector3(0, 10, 5), levelingFunctionsMesh2x2);

				// check between points
				AssertMeshLevelPoint(new Vector3(5, 0, 0), new Vector3(5, 0, 5), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(5, 0, 5), new Vector3(5, 0, 10), levelingFunctionsMesh2x2);

				// check outside points
				AssertMeshLevelPoint(new Vector3(-5, 0, 0), new Vector3(-5, 0, -5), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(-5, 0, 5), new Vector3(-5, 0, 0), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(15, 0, 0), new Vector3(15, 0, 15), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(15, 0, 5), new Vector3(15, 0, 20), levelingFunctionsMesh2x2);
			}

		}

		void AssertMeshLevelPoint(Vector3 testUnleveled, Vector3 controlLeveled, MeshLevlingFunctions levelingFunctions)
		{
			Vector3 testLeveled = levelingFunctions.GetPositionWithZOffset(testUnleveled);
			Assert.AreEqual(testLeveled.X, testUnleveled.X, .001, "We don't adjust the x or y on mesh leveling");
			Assert.AreEqual(testLeveled.X, controlLeveled.X, .001, "We don't adjust the x or y on mesh leveling");
			Assert.AreEqual(testLeveled.Y, testUnleveled.Y, .001, "We don't adjust the x or y on mesh leveling");
			Assert.AreEqual(testLeveled.Y, controlLeveled.Y, .001, "We don't adjust the x or y on mesh leveling");
			Assert.AreEqual(testLeveled.Z, controlLeveled.Z, .001);
			string outPositionString = levelingFunctions.DoApplyLeveling(GetGCodeString(testUnleveled), testUnleveled);
			Assert.AreEqual(GetGCodeString(testLeveled), outPositionString);
		}

		private string GetGCodeString(Vector3 destPosition)
		{
			return "G1 X{0:0.##} Y{1:0.##} Z{2:0.###}".FormatWith(destPosition.X, destPosition.Y, destPosition.Z);
		}
	}
}
