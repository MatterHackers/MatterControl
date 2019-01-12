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
using Net3dBool;
using NUnit.Framework;

namespace MatterHackers.PolygonMesh.UnitTests
{
	[TestFixture, Category("Agg.PolygonMesh.Csg")]
	public class MeshCsgTests
	{
		[Test]
		public void CsgCylinderMinusCylinder()
		{
			//AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			//MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			//AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(5, "MatterControl", "StaticData"));
			//MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(5));

			//// check that we subtract two 3 sided cylinders
			//{
			//	double topHeight = 10;
			//	int sides = 3;
			//	IObject3D keep = CylinderObject3D.Create(20, topHeight * 2, sides);
			//	IObject3D subtract = CylinderObject3D.Create(10, topHeight * 2, sides);

			//	var keepMesh = keep.Mesh;
			//	var subtractMesh = subtract.Mesh;

			//	if (false)
			//	{
			//		var split1 = new DebugFace()
			//		{
			//			EvaluateHeight = topHeight,
			//			FileName = "Split1"
			//		};

			//		BooleanModeller.Object1SplitFace = split1.Split;
			//		BooleanModeller.Object1SplitResults = split1.Result;

			//		BooleanModeller.Object1ClassifyFace = split1.Classify1;
			//		BooleanModeller.Object2ClassifyFace = split1.Classify2;
			//	}

			//	var resultMesh = keepMesh.Subtract(subtractMesh, null, CancellationToken.None);

			//	// this is for debugging the operation
			//	//split1.FinishOutput();
			//	//resultMesh.Save("c:/temp/mesh1.stl", CancellationToken.None);

			//	var topZero = new Vector3(0, 0, topHeight);
			//	foreach (var topVertex in keepMesh.Vertices
			//		.Where((v) => v.Position.Z == topHeight && v.Position != topZero)
			//		.Select((gv) => gv.Position))
			//	{
			//		Assert.IsTrue(resultMesh.Vertices.Where((v) => v.Position == topVertex).Any(), "Have all top vertexes");
			//	}
			//	foreach (var topVertex in subtractMesh.Vertices
			//		.Where((v) => v.Position.Z == topHeight && v.Position != topZero)
			//		.Select((gv) => gv.Position))
			//	{
			//		Assert.IsTrue(resultMesh.Vertices.Where((v) => v.Position == topVertex).Any(), "Have all top vertexes");
			//	}
			//}

			//// check that we subtract two 3 side cylinders
			//{
			//	int sides = 3;
			//	IObject3D keep = CylinderObject3D.Create(20, 20, sides);
			//	IObject3D subtract = CylinderObject3D.Create(10, 22, sides);

			//	var keepMesh = keep.Mesh;
			//	var subtractMesh = subtract.Mesh;

			//	if (false)
			//	{
			//		var split1 = new DebugFace()
			//		{
			//			EvaluateHeight = 10,
			//			FileName = "Split2"
			//		};

			//		BooleanModeller.Object1SplitFace = split1.Split;
			//		BooleanModeller.Object1SplitResults = split1.Result;
			//	}

			//	var resultMesh = keepMesh.Subtract(subtractMesh, null, CancellationToken.None);

			//	// this is for debugging the operation
			//	//split1.FinishOutput();
			//	//resultMesh.Save("c:/temp/mesh2.stl", CancellationToken.None);

			//	foreach (var topVertex in keepMesh.Vertices
			//		.Where((v) => v.Position.Z == 10 && v.Position != new Vector3(0, 0, 10))
			//		.Select((gv) => gv.Position))
			//	{
			//		Assert.IsTrue(resultMesh.Vertices.Where((v) => v.Position == topVertex).Any(), "Have all top vertexes");
			//	}
			//	foreach (var topVertex in subtractMesh.Vertices
			//		.Where((v) => v.Position.Z == 11 && v.Position != new Vector3(0, 0, 11))
			//		.Select((gv) => gv.Position))
			//	{
			//		Assert.IsTrue(resultMesh.Vertices
			//			.Where((v) => v.Position.Equals(new Vector3(topVertex.X, topVertex.Y, 10), .0001))
			//			.Any(), "Have all top vertexes");
			//	}
			//}
		}
	}

	public class DebugFace
	{
		private int currentIndex;
		private StringBuilder allSplitPolygonDebug = new StringBuilder();
		private StringBuilder allResultsPolygonDebug = new StringBuilder();
		private StringBuilder classifiedFaces1 = new StringBuilder();
		private StringBuilder classifiedFaces2 = new StringBuilder();
		private StringBuilder htmlContent = new StringBuilder();
		private StringBuilder individualPolygonDebug = new StringBuilder();

		public string FileName { get; set; } = "DebugFace";
		public double EvaluateHeight { get; set; } = -3;
		int svgHeight = 540;
		int svgWidth = 540;
		Vector2 offset = new Vector2(10, 18);
		double scale = 13;

		public void Result(List<CsgFace> splitResults)
		{
			if (splitResults.Count > 0
				&& FaceAtHeight(splitResults[0], EvaluateHeight))
			{
				individualPolygonDebug.AppendLine($"<br>Result: {currentIndex}</br>");
				individualPolygonDebug.AppendLine($"<svg height='{svgHeight}' width='{svgWidth}'>");
				foreach (var face in splitResults)
				{
					individualPolygonDebug.AppendLine(GetCoords(face));
					allResultsPolygonDebug.AppendLine(GetCoords(face));
				}
				individualPolygonDebug.AppendLine("</svg>");
			}
		}

		public void Split(CsgFace faceToSplit, CsgFace splitAtFace)
		{
			if (FaceAtHeight(faceToSplit, EvaluateHeight))
			{
				allSplitPolygonDebug.AppendLine(GetCoords(faceToSplit));

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
				individualPolygonDebug.AppendLine(GetCoords(faceToSplit));
				individualPolygonDebug.AppendLine(GetCoords(splitAtFace));
				individualPolygonDebug.AppendLine("</svg>");
			}
		}

		public void FinishOutput()
		{
			htmlContent.AppendLine($"<svg height='{svgHeight}' width='{svgWidth}'>");
			htmlContent.Append(allSplitPolygonDebug.ToString());
			htmlContent.AppendLine("</svg>");

			htmlContent.AppendLine($"<svg height='{svgHeight}' width='{svgWidth}'>");
			htmlContent.Append(allResultsPolygonDebug.ToString());
			htmlContent.AppendLine("</svg>");

			htmlContent.Append(individualPolygonDebug.ToString());

			htmlContent.AppendLine($"<svg height='{svgHeight}' width='{svgWidth}'>");
			htmlContent.Append(classifiedFaces1.ToString());
			htmlContent.AppendLine("</svg>");

			htmlContent.AppendLine($"<svg height='{svgHeight}' width='{svgWidth}'>");
			htmlContent.Append(classifiedFaces2.ToString());
			htmlContent.AppendLine("</svg>");

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

		public static bool FaceAtHeight(CsgFace face, double height)
		{
			if (!AreEqual(face.v1.Position.Z, height))
			{
				return false;
			}

			if (!AreEqual(face.v2.Position.Z, height))
			{
				return false;
			}

			if (!AreEqual(face.v3.Position.Z, height))
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

		public string GetCoords(CsgFace face)
		{
			return GetCoords(face, Color.Black, new Color(Color.Red, 100));
		}

		public string GetCoords(CsgFace face, Color strokeColor, Color fillColor, double lineWidth = 1)
		{
			Vector2 p1 = (new Vector2(face.v1.Position.X, -face.v1.Position.Y) + offset) * scale;
			Vector2 p2 = (new Vector2(face.v2.Position.X, -face.v2.Position.Y) + offset) * scale;
			Vector2 p3 = (new Vector2(face.v3.Position.X, -face.v3.Position.Y) + offset) * scale;
			string coords = $"{p1.X:0.0}, {p1.Y:0.0}";
			coords += $", {p2.X:0.0}, {p2.Y:0.0}";
			coords += $", {p3.X:0.0}, {p3.Y:0.0}";
			return $"<polygon points=\"{coords}\" style=\"fill: {fillColor.Html}; stroke: {strokeColor}; stroke - width:.1\" />";
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

		public void Classify1(CsgFace face)
		{
			//if (FaceAtHeight(face, EvaluateHeight))
			{
				Color color = new Color();
				switch (face.Status)
				{
					case FaceStatus.Unknown:
						color = Color.Cyan;
						break;
					case FaceStatus.Inside:
						color = Color.Green;
						break;
					case FaceStatus.Outside:
						color = Color.Red;
						break;
					case FaceStatus.Same:
						color = Color.Gray;
						break;
					case FaceStatus.Opposite:
						color = Color.Yellow;
						break;
					case FaceStatus.Boundary:
						color = Color.Indigo;
						break;
					default:
						throw new NotImplementedException();
				}

				// make it transparent
				color = new Color(color, 100);

				classifiedFaces1.AppendLine(GetCoords(face, Color.Black, color));
			}
		}

		public void Classify2(CsgFace face)
		{
			if (FaceAtHeight(face, EvaluateHeight))
			{
				Color color = new Color();
				switch (face.Status)
				{
					case FaceStatus.Unknown:
						color = Color.Cyan;
						break;
					case FaceStatus.Inside:
						color = Color.Green;
						break;
					case FaceStatus.Outside:
						color = Color.Red;
						break;
					case FaceStatus.Same:
						color = Color.Gray;
						break;
					case FaceStatus.Opposite:
						color = Color.Yellow;
						break;
					case FaceStatus.Boundary:
						color = Color.Indigo;
						break;
					default:
						throw new NotImplementedException();
				}

				// make it transparent
				color = new Color(color, 100);

				classifiedFaces2.AppendLine(GetCoords(face, Color.Black, color));
			}
		}
	}
}