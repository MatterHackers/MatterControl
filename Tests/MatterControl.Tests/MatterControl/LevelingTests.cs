using MatterHackers.MatterControl;
using NUnit.Framework;
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Linq;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.VectorMath;

namespace MatterControl.Tests.MatterControl
{

    [TestFixture]
    public class LevelingTests
    {
        [Test, Category("Leveling")]
        public void Leveling7PointsCorectInterpolation()
        {
			PrintLevelingData levelingData = new PrintLevelingData();

			double radius = 100;
			levelingData.SampledPositions = new List<Vector3>();
			Vector2 currentEdgePoint = new Vector2(radius, 0);
			for (int i = 0; i < 6; i++)
			{
				levelingData.SampledPositions.Add(new Vector3(currentEdgePoint, i));
				currentEdgePoint.Rotate(MathHelper.Tau / 6);
			}

			levelingData.SampledPositions.Add(new Vector3(0, 0, 6));

			for (int curPoint = 0; curPoint < 6; curPoint++)
			{
				int nextPoint = curPoint < 5 ? curPoint + 1 : 0;

				// test actual sample position
				Vector2 currentTestPoint = new Vector2(radius, 0);
				currentTestPoint.Rotate(MathHelper.Tau / 6 * curPoint);
				Vector3 outPosition = LevelWizard7PointRadial.GetPositionWithZOffset(new Vector3(currentTestPoint, 0), levelingData);
				Assert.AreEqual(outPosition.z, levelingData.SampledPositions[curPoint].z, .001);

				// test mid point between samples
				Vector3 midPoint = (levelingData.SampledPositions[curPoint] + levelingData.SampledPositions[nextPoint]) / 2;
				currentTestPoint = new Vector2(midPoint.x, midPoint.y);
				outPosition = LevelWizard7PointRadial.GetPositionWithZOffset(new Vector3(currentTestPoint, 0), levelingData);
				Assert.AreEqual(outPosition.z, midPoint.z, .001);

				// test mid point between samples with offset
				Vector3 midPointWithOffset = (levelingData.SampledPositions[curPoint] + levelingData.SampledPositions[nextPoint]) / 2 + new Vector3(0, 0, 3);
				currentTestPoint = new Vector2(midPointWithOffset.x, midPointWithOffset.y);
				outPosition = LevelWizard7PointRadial.GetPositionWithZOffset(new Vector3(currentTestPoint, 3), levelingData);
				Assert.AreEqual(outPosition.z, midPointWithOffset.z, .001);

				// test 1/2 angles (mid way between samples on radius)
				currentTestPoint = new Vector2(radius, 0);
				currentTestPoint.Rotate(MathHelper.Tau / 6 * (curPoint + .5));
				outPosition = LevelWizard7PointRadial.GetPositionWithZOffset(new Vector3(currentTestPoint, 0), levelingData);
				// the center is the higest point so the point on the radius has to be less than the mid point of the sample points (it is lower)
				Assert.IsTrue(outPosition.z < (levelingData.SampledPositions[curPoint].z + levelingData.SampledPositions[nextPoint].z) / 2 - .001);

				// test 1/2 to center
				currentTestPoint = new Vector2(radius / 2, 0);
				currentTestPoint.Rotate(MathHelper.Tau / 6 * curPoint);
				outPosition = LevelWizard7PointRadial.GetPositionWithZOffset(new Vector3(currentTestPoint, 0), levelingData);
				Assert.AreEqual(outPosition.z, (levelingData.SampledPositions[curPoint].z + levelingData.SampledPositions[6].z) / 2, .001);
			}
			
			Vector3 outPosition2 = LevelWizard7PointRadial.GetPositionWithZOffset(Vector3.Zero, levelingData);
			Assert.AreEqual(outPosition2.z, levelingData.SampledPositions[6].z, .001);
		}
    }
}
