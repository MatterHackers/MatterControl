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

using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.MarchingSquares;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.MatterControl
{
	using Polygon = List<IntPoint>;

	using Polygons = List<List<IntPoint>>;

	public static class CreateDiscreteMeshes
	{
		public static List<Mesh> SplitConnectedIntoMeshes(MeshGroup meshGroupToSplit, ReportProgressRatio reportProgress)
		{
			List<Mesh> discreteMeshes = new List<Mesh>();
			double ratioPerDiscreetMesh = 1.0 / meshGroupToSplit.Meshes.Count;
			double currentRatioDone = 0;
			foreach (Mesh mesh in meshGroupToSplit.Meshes)
			{
				List<Mesh> discreteVolumes = SplitVolumesIntoMeshes(mesh, (double progress0To1, string processingState, out bool continueProcessing) =>
				{
					if (reportProgress != null)
					{
						double progress = (currentRatioDone + ratioPerDiscreetMesh * progress0To1);
						reportProgress(progress, "Split Into Meshes", out continueProcessing);
					}
					else
					{
						continueProcessing = true;
					}
				});
				discreteMeshes.AddRange(discreteVolumes);

				currentRatioDone += ratioPerDiscreetMesh;
			}

			return discreteMeshes;
		}

		public static List<Mesh> SplitVolumesIntoMeshes(Mesh meshToSplit, ReportProgressRatio reportProgress)
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

					MeshMaterialData materialDataToCopy = MeshMaterialData.Get(meshToSplit);
					MeshMaterialData newMaterialData = MeshMaterialData.Get(meshFromCurrentVolume);
					newMaterialData.MaterialIndex = materialDataToCopy.MaterialIndex;

					while (attachedFaces.Count > 0)
					{
						Face faceToAdd = attachedFaces.Pop();
						foreach (Vertex attachedVertex in faceToAdd.Vertices())
						{
							foreach (Face faceAttachedToVertex in attachedVertex.ConnectedFaces())
							{
								if (!facesThatHaveBeenAdded.Contains(faceAttachedToVertex))
								{
									// marke that this face has been taken care of
									facesThatHaveBeenAdded.Add(faceAttachedToVertex);
									// add it to the list of faces we need to walk
									attachedFaces.Push(faceAttachedToVertex);

									// Add a new face to the new mesh we are creating.
									List<Vertex> faceVertices = new List<Vertex>();
									foreach (FaceEdge faceEdgeToAdd in faceAttachedToVertex.FaceEdges())
									{
										Vertex newVertex = meshFromCurrentVolume.CreateVertex(faceEdgeToAdd.firstVertex.Position, CreateOption.CreateNew, SortOption.WillSortLater);
										faceVertices.Add(newVertex);
									}

									meshFromCurrentVolume.CreateFace(faceVertices.ToArray(), CreateOption.CreateNew);
								}
							}
						}
					}

					meshFromCurrentVolume.CleanAndMergMesh();
					discreetVolumes.Add(meshFromCurrentVolume);
					meshFromCurrentVolume = null;
				}
				if (reportProgress != null)
				{
					double progress = faceIndex / (double)meshToSplit.Faces.Count;
					bool continueProcessing;
					reportProgress(progress, "Split Into Meshes", out continueProcessing);
				}
			}

			return discreetVolumes;
		}

		public static Mesh[] SplitIntoMeshesOnOrthographicZ(Mesh meshToSplit, Vector3 buildVolume, ReportProgressRatio reportProgress)
		{
			// check if the part is bigger than the build plate (if it is we need to use that as our size)
			AxisAlignedBoundingBox partBounds = meshToSplit.GetAxisAlignedBoundingBox();

			buildVolume.x = Math.Max(buildVolume.x, partBounds.XSize + 2);
			buildVolume.y = Math.Max(buildVolume.y, partBounds.YSize + 2);
			buildVolume.z = Math.Max(buildVolume.z, partBounds.ZSize + 2);

			// Find all the separate objects that are on the plate
			// Create a 2D image the size of the printer bed at some scale with the parts draw on it top down

			double scaleFactor = 5;
			ImageBuffer partPlate = new ImageBuffer((int)(buildVolume.x * scaleFactor), (int)(buildVolume.y * scaleFactor), 32, new BlenderBGRA());
			Vector2 renderOffset = new Vector2(buildVolume.x / 2, buildVolume.y / 2) - new Vector2(partBounds.Center.x, partBounds.Center.y);

			PolygonMesh.Rendering.OrthographicZProjection.DrawTo(partPlate.NewGraphics2D(), meshToSplit, renderOffset, scaleFactor, RGBA_Bytes.White);

			bool continueProcessin = true;
			if (reportProgress != null)
			{
				reportProgress(.2, "", out continueProcessin);
			}

			//ImageIO.SaveImageData("test part plate 0.png", partPlate);
			// expand the bounds a bit so that we can collect all the vertices and polygons within each bound
			Dilate.DoDilate3x3Binary(partPlate, 1);
			//ImageIO.SaveImageData("test part plate 1.png", partPlate);

			// trace all the bounds of the objects on the plate
			PolyTree polyTreeForPlate = FindDistictObjectBounds(partPlate);
			if (polyTreeForPlate == null)
			{
				Mesh[] singleMesh = new Mesh[1];
				singleMesh[0] = meshToSplit;
				return singleMesh;
			}

			// get all the discrete areas that are polygons so we can search them
			Polygons discreteAreas = new Polygons();
			GetAreasRecursive(polyTreeForPlate, discreteAreas);
			if (discreteAreas.Count == 0)
			{
				return null;
			}
			else if (discreteAreas.Count == 1)
			{
				Mesh[] singleMesh = new Mesh[1];
				singleMesh[0] = meshToSplit;
				return singleMesh;
			}

			Graphics2D graphics2D = partPlate.NewGraphics2D();
			graphics2D.Clear(RGBA_Bytes.Black);
			Random rand = new Random();
			foreach (Polygon polygon in discreteAreas)
			{
				graphics2D.Render(PlatingHelper.PolygonToPathStorage(polygon), new RGBA_Bytes(rand.Next(128, 255), rand.Next(128, 255), rand.Next(128, 255)));
			}
			if (reportProgress != null)
			{
				reportProgress(.5, "", out continueProcessin);
			}
			//ImageIO.SaveImageData("test part plate 2.png", partPlate);

			// add each of the separate bounds polygons to new meshes
			Mesh[] discreteMeshes = new Mesh[discreteAreas.Count];
			for (int i = 0; i < discreteAreas.Count; i++)
			{
				discreteMeshes[i] = new Mesh();
			}

			foreach (Face face in meshToSplit.Faces)
			{
				bool faceDone = false;
				// figure out which area one or more of the vertices are in add the face to the right new mesh
				foreach (FaceEdge faceEdge in face.FaceEdges())
				{
					Vector2 position = new Vector2(faceEdge.firstVertex.Position.x, faceEdge.firstVertex.Position.y);
					position += renderOffset;
					position *= scaleFactor;

					for (int areaIndex = discreteAreas.Count - 1; areaIndex >= 0; areaIndex--)
					{
						if (PointInPolygon(discreteAreas[areaIndex], new IntPoint((int)position.x, (int)position.y)))
						{
							List<Vertex> faceVertices = new List<Vertex>();
							foreach (FaceEdge faceEdgeToAdd in face.FaceEdges())
							{
								Vertex newVertex = discreteMeshes[areaIndex].CreateVertex(faceEdgeToAdd.firstVertex.Position);
								faceVertices.Add(newVertex);
							}

							discreteMeshes[areaIndex].CreateFace(faceVertices.ToArray());
							faceDone = true;
							break;
						}
					}

					if (faceDone)
					{
						break;
					}
				}
			}

			if (reportProgress != null)
			{
				reportProgress(.8, "", out continueProcessin);
			}

			for (int i = 0; i < discreteMeshes.Count(); i++)
			{
				Mesh mesh = discreteMeshes[i];
			}

			return discreteMeshes;
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