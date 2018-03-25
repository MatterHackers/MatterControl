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
#define DEBUG_INTO_TGAS

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using NUnit.Framework;

namespace MatterHackers.PolygonMesh.UnitTests
{
	[TestFixture, Category("Agg.PolygonMesh.Csg")]
	public class MeshCsgTests
	{
		//[Test]
		public void CylinderMinusCylinder()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(5, "MatterControl", "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(5));

			int sides = 3;
			IObject3D keep = CylinderAdvancedObject3D.Create(20, 20, sides);
			IObject3D subtract = CylinderAdvancedObject3D.Create(10, 21, sides);
			IObject3D subtractCentered = new SetCenter(subtract, keep.GetCenter());

			var keepMesh = keep.Mesh;
			var subtractMesh = subtract.Mesh;
			subtractMesh.Transform(subtract.WorldMatrix());

			var split1 = new DebugFace()
			{
				EvaluateHeight = 10,
				FileName = "Split1"
			};

			var resultMesh = keepMesh.Subtract(subtractMesh, null, CancellationToken.None,
				split1.Split, split1.Result);

			split1.FinishOutput();

			resultMesh.Save("c:/temp/mesh.stl", CancellationToken.None);
		}
	}

	public class DebugFace
	{
		private int currentIndex;
		private StringBuilder allPolygonDebug = new StringBuilder();
		private StringBuilder htmlContent = new StringBuilder();
		private StringBuilder individualPolygonDebug = new StringBuilder();

		public string FileName { get; set; } = "DebugFace";
		public double EvaluateHeight { get; set; } = -3;
		int svgHeight = 540;
		int svgWidth = 540;

		public void Result(List<Vector3[]> splitResults)
		{
			if (splitResults.Count > 0
				&& FaceAtHeight(splitResults[0], EvaluateHeight))
			{
				individualPolygonDebug.AppendLine($"<br>Result: {currentIndex}</br>");
				individualPolygonDebug.AppendLine($"<svg height='{svgHeight}' width='{svgWidth}'>");
				foreach (var face in splitResults)
				{
					individualPolygonDebug.AppendLine(GetCoords(face));
				}
				individualPolygonDebug.AppendLine("</svg>");
			}
		}

		public void Split(Vector3[] faceToSplit, Vector3[] splitAtFace)
		{
			if (FaceAtHeight(faceToSplit, EvaluateHeight))
			{
				string faceToSplitCoords = GetCoords(faceToSplit);
				string splitAtFaceCoords = GetCoords(splitAtFace);

				allPolygonDebug.AppendLine(faceToSplitCoords);

				if (currentIndex == 0)
				{
					htmlContent.AppendLine("<!DOCTYPE html>");
					htmlContent.AppendLine("<html>");
					htmlContent.AppendLine("<body>");
					htmlContent.AppendLine("<br>Full</br>");
				}

				currentIndex++;
				individualPolygonDebug.AppendLine($"<br>{currentIndex}</br>");
				individualPolygonDebug.AppendLine($"<svg height='{svgHeight}' width='{svgWidth}'>");
				individualPolygonDebug.AppendLine(faceToSplitCoords);
				individualPolygonDebug.AppendLine(splitAtFaceCoords);
				individualPolygonDebug.AppendLine("</svg>");
			}
		}

		public void FinishOutput()
		{
			htmlContent.AppendLine($"<svg height='{svgHeight}' width='640'>");

			htmlContent.Append(allPolygonDebug.ToString());

			htmlContent.AppendLine("</svg>");

			htmlContent.Append(individualPolygonDebug.ToString());

			htmlContent.AppendLine("</body>");
			htmlContent.AppendLine("</html>");

			File.WriteAllText($"C:/Temp/{FileName}.html", htmlContent.ToString());
		}

		public static bool AreEqual(double a, double b, double errorRange = .001)
		{
			if (a < b + errorRange
				&& a > b - errorRange)
			{
				return true;
			}

			return false;
		}

		public static bool FaceAtHeight(Vector3[] face, double height)
		{
			if (!AreEqual(face[0].Z, height))
			{
				return false;
			}

			if (!AreEqual(face[1].Z, height))
			{
				return false;
			}

			if (!AreEqual(face[2].Z, height))
			{
				return false;
			}

			return true;
		}

		public static bool FaceAtXy(Vector3[] face, double x, double y)
		{
			if (!AreEqual(face[0].X, x)
				|| !AreEqual(face[0].Y, y))
			{
				return false;
			}

			if (!AreEqual(face[1].X, x)
				|| !AreEqual(face[1].Y, y))
			{
				return false;
			}

			if (!AreEqual(face[2].X, x)
				|| !AreEqual(face[2].Y, y))
			{
				return false;
			}

			return true;
		}

		public static string GetCoords(Vector3[] face)
		{
			var offset = new Vector2(10, 15);
			var scale = 15;
			Vector2 p1 = (new Vector2(face[0].X, -face[0].Y) + offset) * scale;
			Vector2 p2 = (new Vector2(face[1].X, -face[1].Y) + offset) * scale;
			Vector2 p3 = (new Vector2(face[2].X, -face[2].Y) + offset) * scale;
			string coords = $"{p1.X:0.0}, {p1.Y:0.0}";
			coords += $", {p2.X:0.0}, {p2.Y:0.0}";
			coords += $", {p3.X:0.0}, {p3.Y:0.0}";
			return $"<polygon points=\"{coords}\" style=\"fill: #FF000022; stroke: purple; stroke - width:1\" />";
		}

		public static bool HasPosition(Vector3[] face, Vector3 position)
		{
			if (face[0].Equals(position, .0001))
			{
				return true;
			}
			if (face[1].Equals(position, .0001))
			{
				return true;
			}
			if (face[2].Equals(position, .0001))
			{
				return true;
			}

			return false;
		}
	}
}