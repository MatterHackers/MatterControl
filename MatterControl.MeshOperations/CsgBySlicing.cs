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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
    using Polygons = List<List<IntPoint>>;

    public class CsgBySlicing
    {
        private int totalOperations;
        private List<Mesh> transformedMeshes;
        private List<ITraceable> bvhAccelerators;
        private List<List<Plane>> plansByMesh;
        private PlaneNormalXSorter planeSorter;
        private Dictionary<Plane, (Matrix4X4 matrix, Matrix4X4 inverted)> transformTo0Planes;

		public CsgBySlicing()
		{
		}

		public void Setup(IEnumerable<(Mesh mesh, Matrix4X4 matrix)> meshAndMatrix,
			Action<double, string> progressReporter,
			CancellationToken cancellationToken)
		{
			totalOperations = 0;
			transformedMeshes = new List<Mesh>();
			bvhAccelerators = new List<ITraceable>();
			foreach (var (mesh, matrix) in meshAndMatrix)
			{
				totalOperations += mesh.Faces.Count;
				var meshCopy = mesh.Copy(cancellationToken);
				transformedMeshes.Add(meshCopy);
				meshCopy.Transform(matrix);
				bvhAccelerators.Add(MeshToBVH.Convert(meshCopy));
			}

			plansByMesh = new List<List<Plane>>();
			var uniquePlanes = new HashSet<Plane>();
			for (int i = 0; i < transformedMeshes.Count; i++)
			{
				var mesh = transformedMeshes[i];
				plansByMesh.Add(new List<Plane>());
				for (int j = 0; j < transformedMeshes[i].Faces.Count; j++)
				{
					var face = mesh.Faces[j];
					var cutPlane = new Plane(mesh.Vertices[face.v0].AsVector3(), mesh.Vertices[face.v1].AsVector3(), mesh.Vertices[face.v2].AsVector3());
					plansByMesh[i].Add(cutPlane);
					uniquePlanes.Add(cutPlane);
				}

				if (cancellationToken.IsCancellationRequested)
                {
					return;
                }
			}

			planeSorter = new PlaneNormalXSorter(uniquePlanes);
			transformTo0Planes = new Dictionary<Plane, (Matrix4X4 matrix, Matrix4X4 inverted)>();
			foreach (var plane in uniquePlanes)
			{
				var matrix = SliceLayer.GetTransformTo0Plane(plane);
				transformTo0Planes[plane] = (matrix, matrix.Inverted);
			}

		}

		public Mesh Calculate(CsgModes operation,
			Action<double, string> progressReporter,
			CancellationToken cancellationToken)
        {
            double amountPerOperation = 1.0 / totalOperations;
            double ratioCompleted = 0;

            var resultsMesh = new Mesh();

            // keep track of all the faces added by their plane
            var coPlanarFaces = new CoPlanarFaces();

            for (var mesh1Index = 0; mesh1Index < transformedMeshes.Count; mesh1Index++)
            {
                var mesh1 = transformedMeshes[mesh1Index];

                for (int faceIndex = 0; faceIndex < mesh1.Faces.Count; faceIndex++)
                {
                    var face = mesh1.Faces[faceIndex];

                    var cutPlane = plansByMesh[mesh1Index][faceIndex];
                    var totalSlice = new Polygons();
                    var firstSlice = true;

                    var transformTo0Plane = transformTo0Planes[cutPlane].matrix;
                    for (var sliceMeshIndex = 0; sliceMeshIndex < transformedMeshes.Count; sliceMeshIndex++)
                    {
                        if (mesh1Index == sliceMeshIndex)
                        {
                            continue;
                        }

                        var mesh2 = transformedMeshes[sliceMeshIndex];
                        // calculate and add the PWN face from the loops
                        var slice = SliceLayer.CreateSlice(mesh2, cutPlane, transformTo0Plane, bvhAccelerators[sliceMeshIndex]);
                        if (firstSlice)
                        {
                            totalSlice = slice;
                            firstSlice = false;
                        }
                        else
                        {
                            totalSlice = totalSlice.Union(slice);
                        }
                    }

                    // now we have the total loops that this polygon can intersect from the other meshes
                    // make a polygon for this face
                    var facePolygon = CoPlanarFaces.GetFacePolygon(mesh1, faceIndex, transformTo0Plane);

                    var polygonShape = new Polygons();
                    // clip against the slice based on the parameters
                    var clipper = new Clipper();
                    clipper.AddPath(facePolygon, PolyType.ptSubject, true);
                    clipper.AddPaths(totalSlice, PolyType.ptClip, true);
                    var expectedFaceNormal = face.normal;

                    switch (operation)
                    {
                        case CsgModes.Union:
                            clipper.Execute(ClipType.ctDifference, polygonShape);
                            break;

                        case CsgModes.Subtract:
                            if (mesh1Index == 0)
                            {
                                clipper.Execute(ClipType.ctDifference, polygonShape);
                            }
                            else
                            {
                                expectedFaceNormal *= -1;
                                clipper.Execute(ClipType.ctIntersection, polygonShape);
                            }

                            break;

                        case CsgModes.Intersect:
                            clipper.Execute(ClipType.ctIntersection, polygonShape);
                            break;
                    }

                    var faceCountPreAdd = resultsMesh.Faces.Count;

                    if (polygonShape.Count == 1
                        && polygonShape[0].Count == 3
                        && facePolygon.Contains(polygonShape[0][0])
                        && facePolygon.Contains(polygonShape[0][1])
                        && facePolygon.Contains(polygonShape[0][2]))
                    {
                        resultsMesh.AddFaceCopy(mesh1, faceIndex);
                    }
                    else
                    {
                        var preAddCount = resultsMesh.Vertices.Count;
                        // mesh the new polygon and add it to the resultsMesh
                        polygonShape.Vertices(1).TriangulateFaces(null, resultsMesh, 0, transformTo0Planes[cutPlane].inverted);
                        var postAddCount = resultsMesh.Vertices.Count;

                        for (int addedIndex = preAddCount; addedIndex < postAddCount; addedIndex++)
                        {
                            // TODO: map all the added vertices that can be back to the original polygon positions
                            for (int meshIndex = 0; meshIndex < transformedMeshes.Count; meshIndex++)
                            {
                                var bvhAccelerator = bvhAccelerators[meshIndex];
                                var mesh = transformedMeshes[meshIndex];
                                var addedPosition = resultsMesh.Vertices[addedIndex];
                                var touchingBvhItems = bvhAccelerator.GetTouching(new Vector3(addedPosition), .0001);
                                foreach (var touchingBvhItem in touchingBvhItems)
                                {
                                    if (touchingBvhItem is TriangleShape triangleShape)
                                    {
                                        var sourceFaceIndex = triangleShape.Index;
                                        var sourceFace = mesh.Faces[sourceFaceIndex];
                                        var sourceVertexIndices = new int[] { sourceFace.v0, sourceFace.v1, sourceFace.v2 };
                                        foreach (var sourceVertexIndex in sourceVertexIndices)
                                        {
                                            var sourcePosition = mesh.Vertices[sourceVertexIndex];
                                            var deltaSquared = (addedPosition - sourcePosition).LengthSquared;
                                            if (deltaSquared > 0 && deltaSquared < .00001)
                                            {
                                                // add the vertex and set the face position index to the new vertex
                                                resultsMesh.Vertices[addedIndex] = sourcePosition;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (resultsMesh.Faces.Count - faceCountPreAdd > 0)
                    {
                        // keep track of the adds so we can process the coplanar faces after
                        for (int i = faceCountPreAdd; i < resultsMesh.Faces.Count; i++)
                        {
                            coPlanarFaces.StoreFaceAdd(planeSorter, cutPlane, mesh1Index, faceIndex, i);
                            // make sure our added faces are the right direction
                            if (resultsMesh.Faces[i].normal.Dot(expectedFaceNormal) < 0)
                            {
                                resultsMesh.FlipFace(i);
                            }
                        }
                    }
                    else // we did not add any faces but we will still keep track of this polygons plan
                    {
                        coPlanarFaces.StoreFaceAdd(planeSorter, cutPlane, mesh1Index, faceIndex, -1);
                    }

                    ratioCompleted += amountPerOperation;
                    progressReporter?.Invoke(ratioCompleted, "");

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }
                }
            }

            // handle the co-planar faces
            ProcessCoplanarFaces(operation, resultsMesh, coPlanarFaces);

            resultsMesh.CleanAndMerge();
            return resultsMesh;
        }

        private void ProcessCoplanarFaces(CsgModes operation, Mesh resultsMesh, CoPlanarFaces coPlanarFaces)
        {
            var faceIndicesToRemove = new HashSet<int>();
            foreach (var plane in coPlanarFaces.Planes)
            {
                var meshIndices = coPlanarFaces.MeshIndicesForPlane(plane);
                if (meshIndices.Count() > 1)
                {
                    // check if more than one mesh has this polygons on this plan
                    var flattenedMatrix = transformTo0Planes[plane].matrix;

                    // depending on the operation add or remove polygons that are planar
                    switch (operation)
                    {
                        case CsgModes.Union:
                            coPlanarFaces.UnionFaces(plane, transformedMeshes, resultsMesh, flattenedMatrix);
                            break;

                        case CsgModes.Subtract:
                            coPlanarFaces.SubtractFaces(plane, transformedMeshes, resultsMesh, flattenedMatrix, faceIndicesToRemove);
                            break;

                        case CsgModes.Intersect:
                            coPlanarFaces.IntersectFaces(plane, transformedMeshes, resultsMesh, flattenedMatrix, faceIndicesToRemove);
                            break;
                    }

                }
            }

            // now rebuild the face list without the remove polygons
            if (faceIndicesToRemove.Count > 0)
            {
                var newFaces = new FaceList();
                for (int i = 0; i < resultsMesh.Faces.Count; i++)
                {
                    // if the face is NOT in the remove faces
                    if (!faceIndicesToRemove.Contains(i))
                    {
                        var face = resultsMesh.Faces[i];
                        newFaces.Add(face);
                    }
                }

                resultsMesh.Faces = newFaces;
            }
        }
    }
}