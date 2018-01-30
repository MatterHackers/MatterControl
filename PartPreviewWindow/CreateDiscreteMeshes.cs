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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.MarchingSquares;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	using Polygon = List<IntPoint>;

	using Polygons = List<List<IntPoint>>;

	public static class CreateDiscreteMeshes
	{
		public static List<Mesh> SplitConnectedIntoMeshes(MeshGroup meshGroupToSplit, CancellationToken cancellationToken, Action<double, string> reportProgress)
		{
			List<Mesh> discreteMeshes = new List<Mesh>();
			double ratioPerDiscreetMesh = 1.0 / meshGroupToSplit.Meshes.Count;
			double currentRatioDone = 0;
			foreach (Mesh mesh in meshGroupToSplit.Meshes)
			{
				List<Mesh> discreteVolumes = SplitVolumesIntoMeshes(mesh, cancellationToken, (double progress0To1, string processingState) =>
				{
					if (reportProgress != null)
					{
						double progress = (currentRatioDone + ratioPerDiscreetMesh * progress0To1);
						reportProgress.Invoke(progress, "Split Into Meshes");
					}
				});
				discreteMeshes.AddRange(discreteVolumes);

				currentRatioDone += ratioPerDiscreetMesh;
			}

			return discreteMeshes;
		}

		public static List<Mesh> SplitVolumesIntoMeshes(Mesh meshToSplit, CancellationToken cancellationToken, Action<double, string> reportProgress)
		{
			List<Mesh> discreetVolumes = new List<Mesh>();
			HashSet<Face> facesThatHaveBeenAdded = new HashSet<Face>();
			Mesh meshFromCurrentVolume = null;
			Stack<Face> attachedFaces = new Stack<Face>();
			for (int faceIndex = 0; faceIndex < meshToSplit.Faces.Count; faceIndex++)
			{
				Face currentFace = meshToSplit.Faces[faceIndex];
				// If this face as not been added to any volume, create a new volume and add all of the attached faces.
				if (!facesThatHaveBeenAdded.Contains(currentFace))
				{
					attachedFaces.Push(currentFace);
					meshFromCurrentVolume = new Mesh();

					while (attachedFaces.Count > 0)
					{
						Face faceToAdd = attachedFaces.Pop();
						foreach (IVertex attachedVertex in faceToAdd.Vertices())
						{
							foreach (Face faceAttachedToVertex in attachedVertex.ConnectedFaces())
							{
								if (!facesThatHaveBeenAdded.Contains(faceAttachedToVertex))
								{
									// mark that this face has been taken care of
									facesThatHaveBeenAdded.Add(faceAttachedToVertex);
									// add it to the list of faces we need to walk
									attachedFaces.Push(faceAttachedToVertex);

									// Add a new face to the new mesh we are creating.
									var faceVertices = new List<IVertex>();
									foreach (FaceEdge faceEdgeToAdd in faceAttachedToVertex.FaceEdges())
									{
										var newVertex = meshFromCurrentVolume.CreateVertex(faceEdgeToAdd.FirstVertex.Position, CreateOption.CreateNew, SortOption.WillSortLater);
										faceVertices.Add(newVertex);
									}

									meshFromCurrentVolume.CreateFace(faceVertices.ToArray(), CreateOption.CreateNew);
								}
							}
						}
					}

					meshFromCurrentVolume.CleanAndMergeMesh(cancellationToken);
					discreetVolumes.Add(meshFromCurrentVolume);
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

		static private void GetAreasRecursive(PolyNode polyTreeForPlate, Polygons discreteAreas)
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

		static public PolyTree FindDistictObjectBounds(ImageBuffer image)
		{
			MarchingSquaresByte marchingSquaresData = new MarchingSquaresByte(image, 5, 0);
			marchingSquaresData.CreateLineSegments();
			Polygons lineLoops = marchingSquaresData.CreateLineLoops(1);

			if (lineLoops.Count == 1)
			{
				return null;
			}

			// create a bounding polygon to clip against
			IntPoint min = new IntPoint(long.MaxValue, long.MaxValue);
			IntPoint max = new IntPoint(long.MinValue, long.MinValue);
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

			Polygon boundingPoly = new Polygon();
			boundingPoly.Add(min);
			boundingPoly.Add(new IntPoint(min.X, max.Y));
			boundingPoly.Add(max);
			boundingPoly.Add(new IntPoint(max.X, min.Y));

			// now clip the polygons to get the inside and outside polys
			Clipper clipper = new Clipper();
			clipper.AddPaths(lineLoops, PolyType.ptSubject, true);
			clipper.AddPath(boundingPoly, PolyType.ptClip, true);

			PolyTree polyTreeForPlate = new PolyTree();
			clipper.Execute(ClipType.ctIntersection, polyTreeForPlate);

			return polyTreeForPlate;
		}
	}
}