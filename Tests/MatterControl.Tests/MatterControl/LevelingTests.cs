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
using MatterControl.Printing.PrintLeveling;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl;
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
		public void LevelingMesh3x3CorectInterpolation()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var printerSettings = new PrinterSettings();
			printerSettings.SetValue(SettingsKey.probe_offset, "0,0,0");

			var printer = new PrinterConfig(printerSettings);

			// a 2 x 2 mesh that goes form 0 on the left to 10 on the right
			{
				var levelingData = new PrintLevelingData();

				// put them in left to right - bottom to top
				levelingData.SampledPositions = new List<Vector3>();
				levelingData.SampledPositions.Add(new Vector3(0, 0, 0));
				levelingData.SampledPositions.Add(new Vector3(10, 0, 10));
				levelingData.SampledPositions.Add(new Vector3(0, 10, 0));
				levelingData.SampledPositions.Add(new Vector3(10, 10, 10));

				LevelingFunctions levelingFunctionsMesh2x2 = new LevelingFunctions(printer.Shim(), levelingData);

				// check on points
				AssertMeshLevelPoint(new Vector3(0, 0, 0), new Vector3(0, 0, 0), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(10, 0, 0), new Vector3(10, 0, 10), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(10, 10, 0), new Vector3(10, 10, 10), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(0, 10, 0), new Vector3(0, 10, 0), levelingFunctionsMesh2x2);

				// check raised on points
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
				var levelingData = new PrintLevelingData();

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

				LevelingFunctions levelingFunctionsMesh2x2 = new LevelingFunctions(printer.Shim(), levelingData);

				// check on points
				AssertMeshLevelPoint(new Vector3(0, 0, 0), new Vector3(0, 0, 0), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(10, 0, 0), new Vector3(10, 0, 10), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(10, 10, 0), new Vector3(10, 10, 10), levelingFunctionsMesh2x2);
				AssertMeshLevelPoint(new Vector3(0, 10, 0), new Vector3(0, 10, 0), levelingFunctionsMesh2x2);

				// check raised on points
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

		void AssertMeshLevelPoint(Vector3 testUnleveled, Vector3 controlLeveled, LevelingFunctions levelingFunctions)
		{
			Vector3 testLeveled = levelingFunctions.GetPositionWithZOffset(testUnleveled);
			Assert.AreEqual(testLeveled.X, testUnleveled.X, .001, "We don't adjust the x or y on mesh leveling");
			Assert.AreEqual(testLeveled.X, controlLeveled.X, .001, "We don't adjust the x or y on mesh leveling");
			Assert.AreEqual(testLeveled.Y, testUnleveled.Y, .001, "We don't adjust the x or y on mesh leveling");
			Assert.AreEqual(testLeveled.Y, controlLeveled.Y, .001, "We don't adjust the x or y on mesh leveling");
			Assert.AreEqual(testLeveled.Z, controlLeveled.Z, .001);
			string outPositionString = levelingFunctions.ApplyLeveling(GetGCodeString(testUnleveled), testUnleveled);
			Assert.AreEqual(GetGCodeString(testLeveled), outPositionString);
		}

		private string GetGCodeString(Vector3 destPosition)
		{
			return "G1 X{0:0.##} Y{1:0.##} Z{2:0.###}".FormatWith(destPosition.X, destPosition.Y, destPosition.Z);
		}
	}
}
