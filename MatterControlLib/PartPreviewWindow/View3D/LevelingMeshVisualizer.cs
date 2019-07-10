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

using System.Collections.Generic;
using System.Linq;
using MatterControl.Printing.PrintLeveling;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class LevelingMeshVisualizer
	{
		public static Mesh BuildMeshFromLevelingData(PrinterConfig printer)
		{
			var printerShim = ApplicationController.Instance.Shim(printer);
			PrintLevelingData levelingData = printer.Settings.Helpers.PrintLevelingData;
			var currentLevelingFunctions = new LevelingFunctions(printerShim, levelingData);

			var vertices = new List<Vector3Float>();

			var pointCounts = new Dictionary<Vector3Float, int>();

			var points = new int[3];

			var faces = new FaceList();

			// Add top faces
			foreach (var region in currentLevelingFunctions.Regions)
			{
				int i = 0;

				foreach (var point in new[] { new Vector3Float(region.V0), new Vector3Float(region.V1), new Vector3Float(region.V2) })
				{
					int index = vertices.IndexOf(point);
					if (index == -1)
					{
						index = vertices.Count;
						vertices.Add(point);
					}

					if (!pointCounts.TryGetValue(point, out int pointCount))
					{
						pointCount = 0;
					}

					pointCounts[point] = pointCount + 1;

					points[i++] = index;
				}

				faces.Add(new Face(points[0], points[2], points[1], vertices));
			}

			List<Vector3Float> outerPoints = GetOuterPoints(vertices, pointCounts, printer.Bed.BedCenter);

			// Add reflected hull points at the bed - reflected point for item in negative z should be the negative value
			var reflectedVertices = outerPoints.Select(h => new Vector3Float(h.X, h.Y, h.Z > 0 ? 0 : h.Z)).ToList();
			vertices.AddRange(reflectedVertices);

			int lastReflected = vertices.IndexOf(reflectedVertices.Last());

			int currIndex, reflectedIndex, nextIndex = -1;

			var anchorIndex = vertices.IndexOf(reflectedVertices.First());

			// Loop over all outer points, reflecting a point onto the bed and stitching the current, reflect and next points together
			for (var i = 0; i < outerPoints.Count; i++)
			{
				var point = outerPoints[i];
				var reflected = reflectedVertices[i];

				bool lastIndex = (i == outerPoints.Count - 1);

				Vector3Float nextPoint = lastIndex ? outerPoints.First() : outerPoints[i + 1];

				currIndex = vertices.IndexOf(point);
				nextIndex = vertices.IndexOf(nextPoint);
				reflectedIndex = vertices.IndexOf(reflected);

				Face faceA, faceB;

				// Add face back to previous
				faces.Add(faceB = new Face(currIndex, lastReflected, reflectedIndex, vertices));

				// Add face for current
				faces.Add(faceA = new Face(currIndex, reflectedIndex, nextIndex, vertices));

				lastReflected = reflectedIndex;
			}

			// Add bottom faces
			foreach (var region in currentLevelingFunctions.Regions)
			{
				int i = 0;

				foreach (var point in new[] { GetReflected(region.V0), GetReflected(region.V1), GetReflected(region.V2) })
				{
					int index = vertices.IndexOf(point);
					if (index == -1)
					{
						index = vertices.Count;
						vertices.Add(point);
					}

					points[i++] = index;
				}

				faces.Add(new Face(points[0], points[1], points[2], vertices));
			}

			return new Mesh(vertices, faces);
		}

		private static Vector3Float GetReflected(Vector3 point)
		{
			return new Vector3Float(point.X, point.Y, (point.Z > 0) ? 0 : point.Z);
		}

		private static List<Vector3Float> GetOuterPoints(List<Vector3Float> vertices, Dictionary<Vector3Float, int> pointCounts, Vector2 bedCenter)
		{
			// Filter to only outer points
			var outerPointsOnly = pointCounts.Where(kvp => kvp.Value <= 3).Select(kvp => kvp.Key).ToList();
			var outerPoints = vertices.Where(p => outerPointsOnly.Contains(p)).ToList();

			// Project to points with their computed angles and lengths
			var computed = outerPoints.Select(p =>
			{
				var point = new Vector2(p) - bedCenter;

				return new
				{
					Point = p,
					Angle = point.GetAngle(),
					Lenth = point.Length
				};
			});

			// Return the original points ordered by angle/length
			var ordered = computed.OrderBy(c => c.Angle).ThenBy(c => c.Lenth);
			return ordered.Select(c => c.Point).ToList();
		}
	}
}
