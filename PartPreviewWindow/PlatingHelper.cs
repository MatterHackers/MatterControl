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
using MatterHackers.Agg.VertexSource;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	using Localizations;
	using System.Collections;
	using Polygon = List<IntPoint>;

	using Polygons = List<List<IntPoint>>;

	public class PlatingMeshGroupData
	{
		public Vector2 spacing;
		public List<IPrimitive> meshTraceableData = new List<IPrimitive>();
	}

	public static class PlatingHelper
	{
		public static PathStorage PolygonToPathStorage(Polygon polygon)
		{
			PathStorage output = new PathStorage();

			bool first = true;
			foreach (IntPoint point in polygon)
			{
				if (first)
				{
					output.Add(point.X, point.Y, ShapePath.FlagsAndCommand.CommandMoveTo);
					first = false;
				}
				else
				{
					output.Add(point.X, point.Y, ShapePath.FlagsAndCommand.CommandLineTo);
				}
			}

			output.ClosePolygon();

			output.Add(0, 0, ShapePath.FlagsAndCommand.CommandStop);

			return output;
		}

		public static PathStorage PolygonToPathStorage(Polygons polygons)
		{
			PathStorage output = new PathStorage();

			foreach (Polygon polygon in polygons)
			{
				bool first = true;
				foreach (IntPoint point in polygon)
				{
					if (first)
					{
						output.Add(point.X, point.Y, ShapePath.FlagsAndCommand.CommandMoveTo);
						first = false;
					}
					else
					{
						output.Add(point.X, point.Y, ShapePath.FlagsAndCommand.CommandLineTo);
					}
				}

				output.ClosePolygon();
			}
			output.Add(0, 0, ShapePath.FlagsAndCommand.CommandStop);

			return output;
		}

		public static void ArrangeMeshGroups(List<MeshGroup> asyncMeshGroups, List<Matrix4X4> asyncMeshGroupTransforms, List<PlatingMeshGroupData> asyncPlatingDatas,
			Action<double, string> reportProgressChanged)
		{
			// move them all out of the way
			for (int i = 0; i < asyncMeshGroups.Count; i++)
			{
				asyncMeshGroupTransforms[i] *= Matrix4X4.CreateTranslation(10000, 10000, 0);
			}

			// sort them by size
			for (int i = 0; i < asyncMeshGroups.Count; i++)
			{
				AxisAlignedBoundingBox iAABB = asyncMeshGroups[i].GetAxisAlignedBoundingBox(asyncMeshGroupTransforms[i]);
				for (int j = i + 1; j < asyncMeshGroups.Count; j++)
				{
					AxisAlignedBoundingBox jAABB = asyncMeshGroups[j].GetAxisAlignedBoundingBox(asyncMeshGroupTransforms[j]);
					if (Math.Max(iAABB.XSize, iAABB.YSize) < Math.Max(jAABB.XSize, jAABB.YSize))
					{
						PlatingMeshGroupData tempData = asyncPlatingDatas[i];
						asyncPlatingDatas[i] = asyncPlatingDatas[j];
						asyncPlatingDatas[j] = tempData;

						MeshGroup tempMeshGroup = asyncMeshGroups[i];
						asyncMeshGroups[i] = asyncMeshGroups[j];
						asyncMeshGroups[j] = tempMeshGroup;

						Matrix4X4 iTransform = asyncMeshGroupTransforms[i];
						Matrix4X4 jTransform = asyncMeshGroupTransforms[j];
						Matrix4X4 tempTransform = iTransform;
						iTransform = jTransform;
						jTransform = tempTransform;

						asyncMeshGroupTransforms[i] = jTransform;
						asyncMeshGroupTransforms[j] = iTransform;

						iAABB = jAABB;
					}
				}
			}

			double ratioPerMeshGroup = 1.0 / asyncMeshGroups.Count;
			double currentRatioDone = 0;
			// put them onto the plate (try the center) starting with the biggest and moving down
			for (int meshGroupIndex = 0; meshGroupIndex < asyncMeshGroups.Count; meshGroupIndex++)
			{
				reportProgressChanged(currentRatioDone, "Calculating Positions...".Localize());

				MeshGroup meshGroup = asyncMeshGroups[meshGroupIndex];
				Vector3 meshLowerLeft = meshGroup.GetAxisAlignedBoundingBox(asyncMeshGroupTransforms[meshGroupIndex]).minXYZ;
				asyncMeshGroupTransforms[meshGroupIndex] *= Matrix4X4.CreateTranslation(-meshLowerLeft);

				PlatingHelper.MoveMeshGroupToOpenPosition(meshGroupIndex, asyncPlatingDatas, asyncMeshGroups, asyncMeshGroupTransforms);

				// and create the trace info so we can select it
				if (asyncPlatingDatas[meshGroupIndex].meshTraceableData.Count == 0)
				{
					PlatingHelper.CreateITraceableForMeshGroup(asyncPlatingDatas, asyncMeshGroups, meshGroupIndex, null);
				}

				currentRatioDone += ratioPerMeshGroup;

				// and put it on the bed
				PlatingHelper.PlaceMeshGroupOnBed(asyncMeshGroups, asyncMeshGroupTransforms, meshGroupIndex);
			}

			// and finally center whatever we have as a group
			{
				AxisAlignedBoundingBox bounds = asyncMeshGroups[0].GetAxisAlignedBoundingBox(asyncMeshGroupTransforms[0]);
				for (int i = 1; i < asyncMeshGroups.Count; i++)
				{
					bounds = AxisAlignedBoundingBox.Union(bounds, asyncMeshGroups[i].GetAxisAlignedBoundingBox(asyncMeshGroupTransforms[i]));
				}

				Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;
				for (int i = 0; i < asyncMeshGroups.Count; i++)
				{
					asyncMeshGroupTransforms[i] *= Matrix4X4.CreateTranslation(-boundsCenter + new Vector3(0, 0, bounds.ZSize / 2));
				}
			}
		}

		public static void PlaceMeshGroupOnBed(List<MeshGroup> meshesGroupList, List<Matrix4X4> meshTransforms, int index)
		{
			AxisAlignedBoundingBox bounds = GetAxisAlignedBoundingBox(meshesGroupList[index], meshTransforms[index]);
			Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;

			meshTransforms[index] *= Matrix4X4.CreateTranslation(new Vector3(0, 0, -boundsCenter.z + bounds.ZSize / 2));
		}

		public static void PlaceMeshAtHeight(List<MeshGroup> meshesGroupList, List<Matrix4X4> meshTransforms, int index, double zHeight)
		{
			AxisAlignedBoundingBox bounds = GetAxisAlignedBoundingBox(meshesGroupList[index], meshTransforms[index]);
			Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;

			meshTransforms[index] *= Matrix4X4.CreateTranslation(new Vector3(0, 0, zHeight - boundsCenter.z + bounds.ZSize / 2));
		}

		public static void CenterMeshGroupXY(List<MeshGroup> meshesGroupList, List<Matrix4X4> meshTransforms, int index)
		{
			AxisAlignedBoundingBox bounds = GetAxisAlignedBoundingBox(meshesGroupList[index], meshTransforms[index]);
			Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;

			meshTransforms[index] *= Matrix4X4.CreateTranslation(new Vector3(-boundsCenter.x + bounds.XSize / 2, -boundsCenter.y + bounds.YSize / 2, 0));
		}

		public static void FindPositionForGroupAndAddToPlate(MeshGroup meshGroupToAdd, Matrix4X4 meshTransform, List<PlatingMeshGroupData> perMeshInfo, List<MeshGroup> meshesGroupsToAvoid, List<Matrix4X4> meshTransforms)
		{
			if (meshGroupToAdd == null || meshGroupToAdd.Meshes.Count < 1)
			{
				return;
			}

			// first find the bounds of what is already here.
			AxisAlignedBoundingBox allPlacedMeshBounds = GetAxisAlignedBoundingBox(meshesGroupsToAvoid[0], meshTransforms[0]);
			for (int i = 1; i < meshesGroupsToAvoid.Count; i++)
			{
				AxisAlignedBoundingBox nextMeshBounds = GetAxisAlignedBoundingBox(meshesGroupsToAvoid[i], meshTransforms[i]);
				allPlacedMeshBounds = AxisAlignedBoundingBox.Union(allPlacedMeshBounds, nextMeshBounds);
			}

			meshesGroupsToAvoid.Add(meshGroupToAdd);

			PlatingMeshGroupData newMeshInfo = new PlatingMeshGroupData();
			perMeshInfo.Add(newMeshInfo);
			meshTransforms.Add(meshTransform);

			int meshGroupIndex = meshesGroupsToAvoid.Count - 1;

			// move the part to the total bounds lower left side
			MeshGroup meshGroup = meshesGroupsToAvoid[meshGroupIndex];
			Vector3 meshLowerLeft = GetAxisAlignedBoundingBox(meshGroup, meshTransforms[meshGroupIndex]).minXYZ;
			meshTransforms[meshGroupIndex] *= Matrix4X4.CreateTranslation(-meshLowerLeft + allPlacedMeshBounds.minXYZ);

			MoveMeshGroupToOpenPosition(meshGroupIndex, perMeshInfo, meshesGroupsToAvoid, meshTransforms);

			PlaceMeshGroupOnBed(meshesGroupsToAvoid, meshTransforms, meshGroupIndex);
		}

		static AxisAlignedBoundingBox GetAxisAlignedBoundingBox(MeshGroup meshGroup, Matrix4X4 transform)
		{
			return meshGroup.GetAxisAlignedBoundingBox(transform);
		}

		public static void MoveMeshGroupToOpenPosition(int meshGroupToMoveIndex, List<PlatingMeshGroupData> perMeshInfo, List<MeshGroup> allMeshGroups, List<Matrix4X4> meshTransforms)
		{
			AxisAlignedBoundingBox allPlacedMeshBounds = GetAxisAlignedBoundingBox(allMeshGroups[0], meshTransforms[0]);
			for (int i = 1; i < meshGroupToMoveIndex; i++)
			{
				AxisAlignedBoundingBox nextMeshBounds = GetAxisAlignedBoundingBox(allMeshGroups[i], meshTransforms[i]);
				allPlacedMeshBounds = AxisAlignedBoundingBox.Union(allPlacedMeshBounds, nextMeshBounds);
			}

			double xStart = allPlacedMeshBounds.minXYZ.x;
			double yStart = allPlacedMeshBounds.minXYZ.y;

			MeshGroup meshGroupToMove = allMeshGroups[meshGroupToMoveIndex];
			// find a place to put it that doesn't hit anything
			AxisAlignedBoundingBox meshToMoveBounds = GetAxisAlignedBoundingBox(meshGroupToMove, meshTransforms[meshGroupToMoveIndex]);
			// add in a few mm so that it will not be touching
			meshToMoveBounds.minXYZ -= new Vector3(2, 2, 0);
			meshToMoveBounds.maxXYZ += new Vector3(2, 2, 0);

			Matrix4X4 transform = Matrix4X4.Identity;
			int currentSize = 1;
			bool partPlaced = false;
			while (!partPlaced && meshGroupToMoveIndex > 0)
			{
				int yStep = 0;
				int xStep = currentSize;
				// check far right edge
				for (yStep = 0; yStep < currentSize; yStep++)
				{
					partPlaced = CheckPosition(meshGroupToMoveIndex, allMeshGroups, meshTransforms, meshGroupToMove, meshToMoveBounds, yStep, xStep, ref transform);

					if (partPlaced)
					{
						break;
					}
				}

				if (!partPlaced)
				{
					yStep = currentSize;
					// check top edge 
					for (xStep = 0; xStep < currentSize; xStep++)
					{
						partPlaced = CheckPosition(meshGroupToMoveIndex, allMeshGroups, meshTransforms, meshGroupToMove, meshToMoveBounds, yStep, xStep, ref transform);

						if (partPlaced)
						{
							break;
						}
					}

					if (!partPlaced)
					{
						xStep = currentSize;
						// check top right point
						partPlaced = CheckPosition(meshGroupToMoveIndex, allMeshGroups, meshTransforms, meshGroupToMove, meshToMoveBounds, yStep, xStep, ref transform);
					}
				}

				currentSize++;
			}

			meshTransforms[meshGroupToMoveIndex] *= transform;
		}

		private static bool CheckPosition(int meshGroupToMoveIndex, List<MeshGroup> allMeshGroups, List<Matrix4X4> meshTransforms, MeshGroup meshGroupToMove, AxisAlignedBoundingBox meshToMoveBounds, int yStep, int xStep, ref Matrix4X4 transform)
		{
			double xStepAmount = 5;
			double yStepAmount = 5;

			Matrix4X4 positionTransform = Matrix4X4.CreateTranslation(xStep * xStepAmount, yStep * yStepAmount, 0);
			Vector3 newPosition = Vector3.Transform(Vector3.Zero, positionTransform);
			transform = Matrix4X4.CreateTranslation(newPosition);
			AxisAlignedBoundingBox testBounds = meshToMoveBounds.NewTransformed(transform);
			bool foundHit = false;
			for (int i = 0; i < meshGroupToMoveIndex; i++)
			{
				MeshGroup meshToTest = allMeshGroups[i];
				if (meshToTest != meshGroupToMove)
				{
					AxisAlignedBoundingBox existingMeshBounds = GetAxisAlignedBoundingBox(meshToTest, meshTransforms[i]);
					AxisAlignedBoundingBox intersection = AxisAlignedBoundingBox.Intersection(testBounds, existingMeshBounds);
					if (intersection.XSize > 0 && intersection.YSize > 0)
					{
						foundHit = true;
						break;
					}
				}
			}

			if (!foundHit)
			{
				return true;
			}

			return false;
		}

		public static void CreateITraceableForMeshGroup(List<PlatingMeshGroupData> perMeshGroupInfo, List<MeshGroup> meshGroups, int meshGroupIndex, ReportProgressRatio reportProgress)
		{
			if (meshGroups != null)
			{
				MeshGroup meshGroup = meshGroups[meshGroupIndex];
				perMeshGroupInfo[meshGroupIndex].meshTraceableData.Clear();
				int totalActionCount = 0;
				foreach (Mesh mesh in meshGroup.Meshes)
				{
					totalActionCount += mesh.Faces.Count;
				}
				int currentAction = 0;
				bool needUpdateTitle = true;
				for (int i = 0; i < meshGroup.Meshes.Count; i++)
				{
					Mesh mesh = meshGroup.Meshes[i];
					List<IPrimitive> allPolys = AddTraceDataForMesh(mesh, totalActionCount, ref currentAction, ref needUpdateTitle, reportProgress);

					needUpdateTitle = true;
					if (reportProgress != null)
					{
						bool continueProcessing;
						reportProgress(currentAction / (double)totalActionCount, "Creating Trace Group", out continueProcessing);
					}

					// only allow limited recursion to speed this up building this data
					IPrimitive traceData = BoundingVolumeHierarchy.CreateNewHierachy(allPolys, 0);
					perMeshGroupInfo[meshGroupIndex].meshTraceableData.Add(traceData);
				}
			}
		}

		public static IPrimitive CreateTraceDataForMesh(Mesh mesh)
		{
			int unusedInt = 0;
			bool unusedBool = false;
			List<IPrimitive> allPolys = AddTraceDataForMesh(mesh, 0, ref unusedInt, ref unusedBool, null);
			return BoundingVolumeHierarchy.CreateNewHierachy(allPolys);
		}

		private static List<IPrimitive> AddTraceDataForMesh(Mesh mesh, int totalActionCount, ref int currentAction, ref bool needToUpdateProgressReport, ReportProgressRatio reportProgress)
		{
			bool continueProcessing;

			List<IPrimitive> allPolys = new List<IPrimitive>();
			List<Vector3> positions = new List<Vector3>();

			foreach (Face face in mesh.Faces)
			{
				if (false)
				{
					MeshFaceTraceable triangle = new MeshFaceTraceable(face);
					allPolys.Add(triangle);
				}
				else
				{
					positions.Clear();
					foreach (Vertex vertex in face.Vertices())
					{
						positions.Add(vertex.Position);
					}

					// We should use the tessellator for this if it is greater than 3.
					Vector3 next = positions[1];
					for (int positionIndex = 2; positionIndex < positions.Count; positionIndex++)
					{
						TriangleShape triangle = new TriangleShape(positions[0], next, positions[positionIndex], null);
						allPolys.Add(triangle);
						next = positions[positionIndex];
					}
				}

				if (reportProgress != null)
				{
					if ((currentAction % 256) == 0 || needToUpdateProgressReport)
					{
						reportProgress(currentAction / (double)totalActionCount, "Creating Trace Polygons", out continueProcessing);
						needToUpdateProgressReport = false;
					}
					currentAction++;
				}
			}

			return allPolys;
		}

		public static Matrix4X4 ApplyAtCenter(IHasAABB meshToApplyTo, Matrix4X4 currentTransform, Matrix4X4 transformToApply)
		{
			return ApplyAtCenter(meshToApplyTo.GetAxisAlignedBoundingBox(currentTransform), currentTransform, transformToApply);
		}

		public static Matrix4X4 ApplyAtCenter(AxisAlignedBoundingBox boundsToApplyTo, Matrix4X4 currentTransform, Matrix4X4 transformToApply)
		{
			return ApplyAtPosition(currentTransform, transformToApply, boundsToApplyTo.Center);
		}

		public static Matrix4X4 ApplyAtPosition(Matrix4X4 currentTransform, Matrix4X4 transformToApply, Vector3 postionToApplyAt)
		{
			currentTransform *= Matrix4X4.CreateTranslation(-postionToApplyAt);
			currentTransform *= transformToApply;
			currentTransform *= Matrix4X4.CreateTranslation(postionToApplyAt);
			return currentTransform;
		}
	}

	public class MeshFaceTraceable : IPrimitive
	{
		Face face;
		public MeshFaceTraceable(Face face)
		{
			this.face = face;
		}

		public RGBA_Floats GetColor(IntersectInfo info) { return RGBA_Floats.Red; }

		public MaterialAbstract Material { get { return null; } set { } }

		public bool GetContained(List<IPrimitive> results, AxisAlignedBoundingBox subRegion)
		{
			AxisAlignedBoundingBox bounds = GetAxisAlignedBoundingBox();
			if (bounds.Contains(subRegion))
			{
				results.Add(this);
				return true;
			}

			return false;
		}

		public IntersectInfo GetClosestIntersection(Ray ray)
		{
			// find the point on the plane
			Vector3[] positions = new Vector3[3];
			int index = 0;
			foreach (FaceEdge faceEdge in face.FaceEdges())
			{
				positions[index++] = faceEdge.firstVertex.Position;
				if(index==3)
				{
					break;
				}
            }
			Plane plane = new Plane(positions[0], positions[1], positions[2]);
			double distanceToHit;
			bool hitFrontOfPlane;
			if (plane.RayHitPlane(ray, out distanceToHit, out hitFrontOfPlane))
			{
				Vector3 polyPlaneIntersection = ray.origin + ray.directionNormal * distanceToHit;
				if (face.PointInPoly(polyPlaneIntersection))
				{
					IntersectInfo info = new IntersectInfo();
					info.closestHitObject = this;
					info.distanceToHit = distanceToHit;
					info.hitPosition = polyPlaneIntersection;
                    info.normalAtHit = face.normal;
					info.hitType = IntersectionType.FrontFace;
					return info;
	            }
			}

			return null;
		}

		public int FindFirstRay(RayBundle rayBundle, int rayIndexToStartCheckingFrom)
		{
			throw new NotImplementedException();
		}

		public void GetClosestIntersections(RayBundle rayBundle, int rayIndexToStartCheckingFrom, IntersectInfo[] intersectionsForBundle)
		{
			throw new NotImplementedException();
		}

		public IEnumerable IntersectionIterator(Ray ray)
		{
			throw new NotImplementedException();
		}

		public double GetSurfaceArea()
		{
			AxisAlignedBoundingBox aabb = GetAxisAlignedBoundingBox();

			double minDimension = Math.Min(aabb.XSize, Math.Min(aabb.YSize, aabb.ZSize));
			if(minDimension == aabb.XSize)
			{
				return aabb.YSize * aabb.ZSize;
			}
			else if(minDimension == aabb.YSize)
			{
				return aabb.XSize * aabb.ZSize;
			}

			return aabb.XSize * aabb.YSize;
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			return face.GetAxisAlignedBoundingBox();
        }

		public Vector3 GetCenter()
		{
			return face.GetCenter();
		}

		public double GetIntersectCost()
		{
			return 700;
		}
	}
}
