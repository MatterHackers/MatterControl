/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using ClipperLib;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;
	
	public class CoPlanarFaces
	{
		private new Dictionary<Plane, Dictionary<int, List<(int sourceFaceIndex, int destFaceIndex)>>> coPlanarFaces
			= new Dictionary<Plane, Dictionary<int, List<(int sourceFaceIndex, int destFaceIndex)>>>();

		public IEnumerable<Plane> Planes
		{
			get
			{
				foreach (var plane in coPlanarFaces.Keys)
				{
					yield return plane;
				}
			}
		}

		public IEnumerable<int> MeshIndicesForPlane(Plane plane)
		{
			foreach (var kvp in coPlanarFaces[plane])
			{
				yield return kvp.Key;
			}
		}

		public IEnumerable<(int sourceFaceIndex, int destFaceIndex)> FacesSetsForPlaneAndMesh(Plane plane, int meshIndex)
        {
			if (coPlanarFaces[plane].ContainsKey(meshIndex))
			{
				foreach (var faceIndices in coPlanarFaces[plane][meshIndex])
				{
					yield return faceIndices;
				}
			}
        }

		public static Matrix4X4 GetFlattenedMatrix(Plane cutPlane)
		{
			var rotation = new Quaternion(cutPlane.Normal, Vector3.UnitZ);
			var flattenedMatrix = Matrix4X4.CreateRotation(rotation);
			flattenedMatrix *= Matrix4X4.CreateTranslation(0, 0, -cutPlane.DistanceFromOrigin);

			return flattenedMatrix;
		}

		public static Polygon GetFacePolygon(Mesh mesh1, int faceIndex, Plane cutPlane, Matrix4X4 flattenedMatrix)
		{
			var meshTo0Plane = flattenedMatrix * Matrix4X4.CreateScale(1000);
			var facePolygon = new Polygon();
			var vertices = mesh1.Vertices;
			var face = mesh1.Faces[faceIndex];
			var vertIndices = new int[] { face.v0, face.v1, face.v2 };
			var vertsOnPlane = new Vector3[3];
			for (int i = 0; i < 3; i++)
			{
				vertsOnPlane[i] = Vector3Ex.Transform(vertices[vertIndices[i]].AsVector3(), meshTo0Plane);
				var pointOnPlane = new IntPoint(vertsOnPlane[i].X, vertsOnPlane[i].Y);
				facePolygon.Add(pointOnPlane);
			}

			return facePolygon;

		}

		public void SubtractFaces(Plane plane, List<Mesh> transformedMeshes, Mesh resultsMesh, Matrix4X4 flattenedMatrix, HashSet<int> faceIndicesToRemove)
		{
			// get all meshes that have faces on this plane
			var meshesWithFaces = MeshIndicesForPlane(plane).ToList();

			// we need more than one mesh and one of them needs to be the source (mesh 0)
			if (meshesWithFaces.Count < 2
				|| !meshesWithFaces.Contains(0))
			{
				// no faces to add
				return;
			}

			// sort them so we can process each group into intersections
			meshesWithFaces.Sort();

			// add the faces that we should
			foreach(var meshIndex in meshesWithFaces)
            {
				foreach(var faces in FacesSetsForPlaneAndMesh(plane, meshIndex))
                {
					faceIndicesToRemove.Add(faces.destFaceIndex);
                }
            }

			// subtract every face from the mesh 0 faces
			// teselate and add what is left
			var keepPolygons = new Polygons();
			foreach (var keepFaceSets in FacesSetsForPlaneAndMesh(plane, 0))
			{
				keepPolygons = keepPolygons.Union(GetFacePolygon(transformedMeshes[0], keepFaceSets.sourceFaceIndex, plane, flattenedMatrix));
			}

			// iterate all the meshes that need to be subtracted
			var removePoygons = new Polygons();
			for (int removeMeshIndex= 1; removeMeshIndex < meshesWithFaces.Count; removeMeshIndex++)
            {
				foreach (var removeFaceSets in FacesSetsForPlaneAndMesh(plane, removeMeshIndex))
				{
					removePoygons = removePoygons.Union(GetFacePolygon(transformedMeshes[removeMeshIndex], removeFaceSets.sourceFaceIndex, plane, flattenedMatrix));
				}
			}

			var polygonShape = new Polygons();
			var clipper = new Clipper();
			clipper.AddPaths(keepPolygons, PolyType.ptSubject, true);
			clipper.AddPaths(removePoygons, PolyType.ptClip, true);
			clipper.Execute(ClipType.ctDifference, polygonShape);

			// teselate and add all the new polygons
			polygonShape.Vertices().TriangulateFaces(null, resultsMesh, 0, flattenedMatrix.Inverted);
		}

		public void UnionFaces(Plane plane, List<Mesh> transformedMeshes, Mesh resultsMesh, Matrix4X4 flattenedMatrix)
		{
			// get all meshes that have faces on this plane
			var meshesWithFaces = MeshIndicesForPlane(plane).ToList();

			if (meshesWithFaces.Count < 2)
            {
				// no faces to add
				return;
            }

			// sort them so we can process each group into intersections
			meshesWithFaces.Sort();

			var meshPolygons = new List<Polygons>();
			for (int i = 0; i < meshesWithFaces.Count; i++)
			{
				meshPolygons.Add(new Polygons());
				var addedFaces = new HashSet<int>();
				foreach (var (sourceFaceIndex, destFaceIndex) in this.FacesSetsForPlaneAndMesh(plane, i))
				{
					if (!addedFaces.Contains(sourceFaceIndex))
					{
						meshPolygons[i].Add(GetFacePolygon(transformedMeshes[i], sourceFaceIndex, plane, flattenedMatrix));
						addedFaces.Add(sourceFaceIndex);
					}
				}
			}

			var intersectionSets = new List<Polygons>();
			// now intersect each set of meshes to get all the sets of intersections
			for (int i = 0; i < meshesWithFaces.Count; i++)
			{
				// add all the faces for mesh j
				for (int j=i+1; j<meshesWithFaces.Count; j++)
                {
					var clipper = new Clipper();
					clipper.AddPaths(meshPolygons[i], PolyType.ptSubject, true);
					clipper.AddPaths(meshPolygons[j], PolyType.ptClip, true);

					var intersection = new Polygons();
					clipper.Execute(ClipType.ctIntersection, intersection);

					intersectionSets.Add(intersection);
				}
			}

			// now union all the intersections
			var totalSlices = new Polygons(intersectionSets[0]);
			for (int i = 1; i < intersectionSets.Count; i++)
            {
				// clip against the slice based on the parameters
				var clipper = new Clipper();
				clipper.AddPaths(totalSlices, PolyType.ptSubject, true);
				clipper.AddPaths(intersectionSets[i], PolyType.ptClip, true);
				clipper.Execute(ClipType.ctUnion, totalSlices);
			}

			// teselate and add all the new polygons
			totalSlices.Vertices().TriangulateFaces(null, resultsMesh, 0, flattenedMatrix.Inverted);
		}

		public void StoreFaceAdd(Plane facePlane,
			int sourceMeshIndex,
			int sourceFaceIndex,
			int destFaceIndex)
		{
			foreach (var plane in coPlanarFaces.Keys)
			{
				// check if they are close enough
				if (facePlane.Equals(plane))
				{
					facePlane = plane;
					break;
				}
			}

			if (!coPlanarFaces.ContainsKey(facePlane))
			{
				coPlanarFaces[facePlane] = new Dictionary<int, List<(int sourceFace, int destFace)>>();
			}

			if (!coPlanarFaces[facePlane].ContainsKey(sourceMeshIndex))
			{
				coPlanarFaces[facePlane][sourceMeshIndex] = new List<(int sourceFace, int destFace)>();
			}

			coPlanarFaces[facePlane][sourceMeshIndex].Add((sourceFaceIndex, destFaceIndex));
		}
	}
}