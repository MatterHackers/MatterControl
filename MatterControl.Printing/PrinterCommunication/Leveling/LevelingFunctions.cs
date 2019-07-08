/*
Copyright (c) 2019, Lars Brubaker
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

using System;
using System.Collections.Generic;
using System.Text;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using MIConvexHull;

namespace MatterControl.Printing.PrintLeveling
{
	public class LevelingFunctions
	{
		private Vector2 bedSize;
		private Dictionary<(int, int), int> positionToRegion = new Dictionary<(int, int), int>();
		private PrintHostConfig printer;

		public LevelingFunctions(PrintHostConfig printer, PrintLevelingData levelingData)
		{
			this.printer = printer;
			this.SampledPositions = new List<Vector3>(levelingData.SampledPositions);

			bedSize = printer.Settings.GetValue<Vector2>(SettingsKey.bed_size);

			// get the delaunay triangulation
			var zDictionary = new Dictionary<(double, double), double>();
			var vertices = new List<DefaultVertex>();

			if (SampledPositions.Count > 2)
			{
				foreach (var sample in SampledPositions)
				{
					vertices.Add(new DefaultVertex()
					{
						Position = new double[] { sample.X, sample.Y }
					});
					var key = (sample.X, sample.Y);
					if (!zDictionary.ContainsKey(key))
					{
						zDictionary.Add(key, sample.Z);
					}
				};
			}
			else
			{
				vertices.Add(new DefaultVertex()
				{
					Position = new double[] { 0, 0 }
				});
				zDictionary.Add((0, 0), 0);

				vertices.Add(new DefaultVertex()
				{
					Position = new double[] { 200, 0 }
				});
				zDictionary.Add((200, 0), 0);

				vertices.Add(new DefaultVertex()
				{
					Position = new double[] { 100, 200 }
				});
				zDictionary.Add((100, 200), 0);
			}

			int extraXPosition = -50000;
			vertices.Add(new DefaultVertex()
			{
				Position = new double[] { extraXPosition, vertices[0].Position[1] }
			});

			var triangles = DelaunayTriangulation<DefaultVertex, DefaultTriangulationCell<DefaultVertex>>.Create(vertices, .001);

			var probeZOffset = default(Vector3);

			if (printer.Settings.Helpers.UseZProbe())
			{
				probeZOffset = new Vector3(0, 0, printer.Settings.GetValue<Vector3>(SettingsKey.probe_offset).Z);
			}

			// make all the triangle planes for these triangles
			foreach (var triangle in triangles.Cells)
			{
				var p0 = triangle.Vertices[0].Position;
				var p1 = triangle.Vertices[1].Position;
				var p2 = triangle.Vertices[2].Position;
				if (p0[0] != extraXPosition && p1[0] != extraXPosition && p2[0] != extraXPosition)
				{
					var v0 = new Vector3(p0[0], p0[1], zDictionary[(p0[0], p0[1])]);
					var v1 = new Vector3(p1[0], p1[1], zDictionary[(p1[0], p1[1])]);
					var v2 = new Vector3(p2[0], p2[1], zDictionary[(p2[0], p2[1])]);
					// add all the regions
					Regions.Add(new LevelingTriangle(v0 + probeZOffset, v1 + probeZOffset, v2 + probeZOffset));
				}
			}
		}

		public List<Vector3> SampledPositions { get; }

		public List<LevelingTriangle> Regions { get; } = new List<LevelingTriangle>();

		public string ApplyLeveling(string lineBeingSent, Vector3 destination)
		{
			bool hasMovement = lineBeingSent.Contains("X") || lineBeingSent.Contains("Y") || lineBeingSent.Contains("Z");
			if (!hasMovement)
			{
				// Leave non-leveling lines untouched
				return lineBeingSent;
			}

			double extruderDelta = 0;
			GCodeFile.GetFirstNumberAfter("E", lineBeingSent, ref extruderDelta);
			double feedRate = 0;
			GCodeFile.GetFirstNumberAfter("F", lineBeingSent, ref feedRate);

			var newLine = new StringBuilder("G1");

			// Position data is not optional for leveling - fall back to fixed defaults when not yet known
			var correctedPosition = new Vector3(
			(destination.X == double.PositiveInfinity) ? 0 : destination.X,
			(destination.Y == double.PositiveInfinity) ? 0 : destination.Y,
			(destination.Z == double.PositiveInfinity) ? 0 : destination.Z);

			// get the offset to the active extruder
			var extruderOffset = printer.Settings.Helpers.ExtruderOffset(printer.Connection.ActiveExtruderIndex);
			correctedPosition += extruderOffset;

			// level it
			Vector3 outPosition = GetPositionWithZOffset(correctedPosition);

			// take the extruder offset back out
			outPosition -= extruderOffset;

			// Only output known positions
			if (destination.X != double.PositiveInfinity)
			{
				newLine.Append($" X{outPosition.X:0.##}");
			}

			if (destination.Y != double.PositiveInfinity)
			{
				newLine.Append($" Y{outPosition.Y:0.##}");
			}

			newLine.Append($" Z{outPosition.Z:0.##}");

			if (lineBeingSent.Contains("E"))
			{
				newLine.Append($" E{extruderDelta:0.###}");
			}

			if (lineBeingSent.Contains("F"))
			{
				newLine.Append($" F{feedRate:0.##}");
			}

			return newLine.ToString();
		}

		public Vector3 GetPositionWithZOffset(Vector3 currentDestination)
		{
			LevelingTriangle region = GetCorrectRegion(currentDestination);

			return region.GetPositionWithZOffset(currentDestination);
		}

		private LevelingTriangle GetCorrectRegion(Vector3 currentDestination)
		{
			int xIndex = (int)Math.Round(currentDestination.X * 100 / bedSize.X);
			int yIndex = (int)Math.Round(currentDestination.Y * 100 / bedSize.Y);

			int bestIndex;
			if (!positionToRegion.TryGetValue((xIndex, yIndex), out bestIndex))
			{
				// else calculate the region and store it
				double bestDist = double.PositiveInfinity;

				currentDestination.Z = 0;
				for (int regionIndex = 0; regionIndex < Regions.Count; regionIndex++)
				{
					var dist = (Regions[regionIndex].Center - currentDestination).LengthSquared;
					if (Regions[regionIndex].PointInPolyXY(currentDestination.X, currentDestination.Y))
					{
						// we found the one it is in
						return Regions[regionIndex];
					}
					if (dist < bestDist)
					{
						bestIndex = regionIndex;
						bestDist = dist;
					}
				}

				positionToRegion.Add((xIndex, yIndex), bestIndex);
			}

			return Regions[bestIndex];
		}

		public class LevelingTriangle
		{
			public LevelingTriangle(Vector3 v0, Vector3 v1, Vector3 v2)
			{
				this.V0 = v0;
				this.V1 = v1;
				this.V2 = v2;
				this.Center = (V0 + V1 + V2) / 3;
				this.Plane = new Plane(V0, V1, V2);
			}

			public Vector3 Center { get; }
			public Plane Plane { get; }
			public Vector3 V0 { get; }
			public Vector3 V1 { get; }
			public Vector3 V2 { get; }

			public Vector3 GetPositionWithZOffset(Vector3 currentDestination)
			{
				var destinationAtZ0 = new Vector3(currentDestination.X, currentDestination.Y, 0);

				double hitDistance = this.Plane.GetDistanceToIntersection(destinationAtZ0, Vector3.UnitZ);
				currentDestination.Z += hitDistance;

				return currentDestination;
			}

			private int FindSideOfLine(Vector2 sidePoint0, Vector2 sidePoint1, Vector2 testPosition)
			{
				if (Vector2.Cross(testPosition - sidePoint0, sidePoint1 - sidePoint0) < 0)
				{
					return 1;
				}

				return -1;
			}

			public bool PointInPolyXY(double x, double y)
			{
				// check the bounding rect
				Vector2 vertex0 = new Vector2(V0[0], V0[1]);
				Vector2 vertex1 = new Vector2(V1[0], V1[1]);
				Vector2 vertex2 = new Vector2(V2[0], V2[1]);
				Vector2 hitPosition = new Vector2(x, y);
				int sumOfLineSides = FindSideOfLine(vertex0, vertex1, hitPosition);
				sumOfLineSides += FindSideOfLine(vertex1, vertex2, hitPosition);
				sumOfLineSides += FindSideOfLine(vertex2, vertex0, hitPosition);
				if (sumOfLineSides == -3 || sumOfLineSides == 3)
				{
					return true;
				}

				return false;
			}
		}
	}
}