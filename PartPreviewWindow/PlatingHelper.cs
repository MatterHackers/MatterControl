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
	using Polygon = List<IntPoint>;

	using Polygons = List<List<IntPoint>>;

	public class PlatingMeshGroupData
	{
		public Vector3 currentScale = new Vector3(1, 1, 1);
		public double xSpacing;
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

		public static void PlaceMeshGroupOnBed(List<MeshGroup> meshesGroupList, List<ScaleRotateTranslate> meshTransforms, int index)
		{
			AxisAlignedBoundingBox bounds = meshesGroupList[index].GetAxisAlignedBoundingBox(meshTransforms[index].TotalTransform);
			Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;

			ScaleRotateTranslate moved = meshTransforms[index];
			moved.translation *= Matrix4X4.CreateTranslation(new Vector3(0, 0, -boundsCenter.z + bounds.ZSize / 2));
			meshTransforms[index] = moved;
		}

		public static void CenterMeshGroupXY(List<MeshGroup> meshesGroupList, List<ScaleRotateTranslate> meshTransforms, int index)
		{
			AxisAlignedBoundingBox bounds = meshesGroupList[index].GetAxisAlignedBoundingBox(meshTransforms[index].TotalTransform);
			Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;

			ScaleRotateTranslate moved = meshTransforms[index];
			moved.translation *= Matrix4X4.CreateTranslation(new Vector3(-boundsCenter.x + bounds.XSize / 2, -boundsCenter.y + bounds.YSize / 2, 0));
			meshTransforms[index] = moved;
		}

		public static void FindPositionForGroupAndAddToPlate(MeshGroup meshGroupToAdd, ScaleRotateTranslate meshTransform, List<PlatingMeshGroupData> perMeshInfo, List<MeshGroup> meshesGroupsToAvoid, List<ScaleRotateTranslate> meshTransforms)
		{
			if (meshGroupToAdd == null || meshGroupToAdd.Meshes.Count < 1)
			{
				return;
			}

			meshesGroupsToAvoid.Add(meshGroupToAdd);

			PlatingMeshGroupData newMeshInfo = new PlatingMeshGroupData();
			perMeshInfo.Add(newMeshInfo);
			meshTransform.SetCenteringForMeshGroup(meshGroupToAdd);
			meshTransforms.Add(meshTransform);

			int meshGroupIndex = meshesGroupsToAvoid.Count - 1;

			// now actually center the part we are going to finde a position for
			CenterMeshGroupXY(meshesGroupsToAvoid, meshTransforms, meshGroupIndex);

			MoveMeshGroupToOpenPosition(meshGroupIndex, perMeshInfo, meshesGroupsToAvoid, meshTransforms);

			PlaceMeshGroupOnBed(meshesGroupsToAvoid, meshTransforms, meshGroupIndex);
		}

		public static void MoveMeshGroupToOpenPosition(int meshGroupToMoveIndex, List<PlatingMeshGroupData> perMeshInfo, List<MeshGroup> allMeshGroups, List<ScaleRotateTranslate> meshTransforms)
		{
			MeshGroup meshGroupToMove = allMeshGroups[meshGroupToMoveIndex];
			// find a place to put it that doesn't hit anything
			AxisAlignedBoundingBox meshToMoveBounds = meshGroupToMove.GetAxisAlignedBoundingBox(meshTransforms[meshGroupToMoveIndex].TotalTransform);
			// add in a few mm so that it will not be touching
			meshToMoveBounds.minXYZ -= new Vector3(2, 2, 0);
			meshToMoveBounds.maxXYZ += new Vector3(2, 2, 0);
			double ringDist = Math.Min(meshToMoveBounds.XSize, meshToMoveBounds.YSize);
			double currentDist = 0;
			double angle = 0;
			double angleIncrement = MathHelper.Tau / 64;
			Matrix4X4 transform;
			while (true)
			{
				Matrix4X4 positionTransform = Matrix4X4.CreateTranslation(currentDist, 0, 0);
				positionTransform *= Matrix4X4.CreateRotationZ(angle);
				Vector3 newPosition = Vector3.Transform(Vector3.Zero, positionTransform);
				transform = Matrix4X4.CreateTranslation(newPosition);
				AxisAlignedBoundingBox testBounds = meshToMoveBounds.NewTransformed(transform);
				bool foundHit = false;
				for (int i = 0; i < allMeshGroups.Count; i++)
				{
					MeshGroup meshToTest = allMeshGroups[i];
					if (meshToTest != meshGroupToMove)
					{
						AxisAlignedBoundingBox existingMeshBounds = meshToTest.GetAxisAlignedBoundingBox(meshTransforms[i].TotalTransform);
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
					break;
				}

				angle += angleIncrement;
				if (angle >= MathHelper.Tau)
				{
					angle = 0;
					currentDist += ringDist;
				}
			}

			ScaleRotateTranslate moved = meshTransforms[meshGroupToMoveIndex];
			moved.translation *= transform;
			meshTransforms[meshGroupToMoveIndex] = moved;
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

					// only allow limited recusion to speed this up building this data
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
				positions.Clear();
				foreach (Vertex vertex in face.Vertices())
				{
					positions.Add(vertex.Position);
				}

				// We should use the teselator for this if it is greater than 3.
				Vector3 next = positions[1];
				for (int positionIndex = 2; positionIndex < positions.Count; positionIndex++)
				{
					TriangleShape triangel = new TriangleShape(positions[0], next, positions[positionIndex], null);
					allPolys.Add(triangel);
					next = positions[positionIndex];
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
	}
}