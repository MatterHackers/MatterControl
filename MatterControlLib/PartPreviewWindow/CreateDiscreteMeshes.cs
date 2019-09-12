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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ClipperLib;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Image.ThresholdFunctions;
using MatterHackers.MarchingSquares;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using Polygon = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;

namespace MatterHackers.MatterControl
{
	public static class CreateDiscreteMeshes
	{
		public static List<Mesh> SplitVolumesIntoMeshes(Mesh meshToSplit, CancellationToken cancellationToken, Action<double, string> reportProgress)
		{
			var maxProgressReport = Stopwatch.StartNew();
			var discreetVolumes = new List<Mesh>();
			var facesThatHaveBeenAdded = new HashSet<int>();
			Mesh meshFromCurrentVolume = null;
			var attachedFaces = new Stack<int>();
			int faceCount = meshToSplit.Faces.Count;
			var facesSharingVertex = meshToSplit.NewVertexFaceLists();
			var totalBounds = meshToSplit.GetAxisAlignedBoundingBox();

			for (int faceIndex = 0; faceIndex < faceCount; faceIndex++)
			{
				if (reportProgress != null)
				{
					if (maxProgressReport.ElapsedMilliseconds > 200)
					{
						reportProgress(faceIndex / (double)faceCount, "Merging Mesh Edges");
						maxProgressReport.Restart();
						if (cancellationToken.IsCancellationRequested)
						{
							return null;
						}
					}
				}

				// If this face as not been added to any volume, create a new volume and add all of the attached faces.
				if (!facesThatHaveBeenAdded.Contains(faceIndex))
				{
					attachedFaces.Push(faceIndex);
					meshFromCurrentVolume = new Mesh();

					while (attachedFaces.Count > 0)
					{
						var faceToAdd = meshToSplit.Faces[attachedFaces.Pop()];
						var vertices = new int[] { faceToAdd.v0, faceToAdd.v1, faceToAdd.v2 };

						foreach (var attachedVertex in vertices)
						{
							foreach (var sharedFaceIndex in facesSharingVertex[attachedVertex].Faces)
							{
								if (!facesThatHaveBeenAdded.Contains(sharedFaceIndex))
								{
									// mark that this face has been taken care of
									facesThatHaveBeenAdded.Add(sharedFaceIndex);
									// add it to the list of faces we need to walk
									attachedFaces.Push(sharedFaceIndex);

									// Add a new face to the new mesh we are creating.
									meshFromCurrentVolume.CreateFace(new Vector3Float[]
                                    {
										meshToSplit.Vertices[meshToSplit.Faces[sharedFaceIndex].v0],
										meshToSplit.Vertices[meshToSplit.Faces[sharedFaceIndex].v1],
										meshToSplit.Vertices[meshToSplit.Faces[sharedFaceIndex].v2]
                                    });
								}
							}
						}
					}

					meshFromCurrentVolume.CleanAndMerge();
					var bounds = meshFromCurrentVolume.GetAxisAlignedBoundingBox();
					var oneTenThousandth = totalBounds.Size.Length / 10000.0;
					if (meshFromCurrentVolume.Vertices.Count > 2
						&& (bounds.XSize > oneTenThousandth
						|| bounds.YSize > oneTenThousandth
						|| bounds.ZSize > oneTenThousandth)
						&& meshFromCurrentVolume.Faces.Any(f => f.GetArea(meshFromCurrentVolume) > oneTenThousandth))
					{
						discreetVolumes.Add(meshFromCurrentVolume);
					}

					meshFromCurrentVolume = null;
				}

				if (reportProgress != null)
				{
					double progress = faceIndex / (double)meshToSplit.Faces.Count;
					reportProgress(progress, "Split Into Meshes");
				}
			}

			return discreetVolumes;
		}

		public static bool PointInPolygon(Polygon polygon, IntPoint testPosition)
		{
			int numPoints = polygon.Count;
			bool result = false;
			for (int i = 0; i < numPoints; i++)
			{
				int prevIndex = i - 1;
				if (prevIndex < 0)
				{
					prevIndex += numPoints;
				}

				if ((((polygon[i].Y <= testPosition.Y) && (testPosition.Y < polygon[prevIndex].Y))
					|| ((polygon[prevIndex].Y <= testPosition.Y) && (testPosition.Y < polygon[i].Y)))
					&& (testPosition.X - polygon[i].X < (polygon[prevIndex].X - polygon[i].X) * (testPosition.Y - polygon[i].Y) / (polygon[prevIndex].Y - polygon[i].Y)))
				{
					result = !result;
				}
			}

			return result;
		}

		private static void GetAreasRecursive(PolyNode polyTreeForPlate, Polygons discreteAreas)
		{
			if (!polyTreeForPlate.IsHole)
			{
				discreteAreas.Add(polyTreeForPlate.Contour);
			}

			foreach (PolyNode child in polyTreeForPlate.Childs)
			{
				GetAreasRecursive(child, discreteAreas);
			}
		}

		public static PolyTree FindDistictObjectBounds(ImageBuffer image)
		{
			var intensity = new MapOnMaxIntensity();
			var marchingSquaresData = new MarchingSquaresByte(image, intensity.ZeroColor, intensity.Threshold, 0);
			marchingSquaresData.CreateLineSegments();
			Polygons lineLoops = marchingSquaresData.CreateLineLoops(1);

			if (lineLoops.Count == 1)
			{
				return null;
			}

			// create a bounding polygon to clip against
			var min = new IntPoint(long.MaxValue, long.MaxValue);
			var max = new IntPoint(long.MinValue, long.MinValue);
			foreach (Polygon polygon in lineLoops)
			{
				foreach (IntPoint point in polygon)
				{
					min.X = Math.Min(point.X - 10, min.X);
					min.Y = Math.Min(point.Y - 10, min.Y);
					max.X = Math.Max(point.X + 10, max.X);
					max.Y = Math.Max(point.Y + 10, max.Y);
				}
			}

			var boundingPoly = new Polygon
			{
				min,
				new IntPoint(min.X, max.Y),
				max,
				new IntPoint(max.X, min.Y)
			};

			// now clip the polygons to get the inside and outside polys
			var clipper = new Clipper();
			clipper.AddPaths(lineLoops, PolyType.ptSubject, true);
			clipper.AddPath(boundingPoly, PolyType.ptClip, true);

			var polyTreeForPlate = new PolyTree();
			clipper.Execute(ClipType.ctIntersection, polyTreeForPlate);

			return polyTreeForPlate;
		}
	}
}