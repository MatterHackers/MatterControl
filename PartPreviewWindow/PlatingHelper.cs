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
using System.Text;
using System.ComponentModel;

using ClipperLib;

using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    public class PlatingMeshData
    {
        public double currentScale = 1;
        public double xSpacing;
        public IRayTraceable traceableData;
    }

    public static class PlatingHelper
    {
        public static Mesh DoMerge(List<Mesh> meshesToMerge, BackgroundWorker backgroundWorker, int startPercent, int endPercent, bool doCSGMerge = false)
        {
            int lengthPercent = endPercent - startPercent;

            Mesh allPolygons = new Mesh();
            if (doCSGMerge)
            {
                for (int i = 0; i < meshesToMerge.Count; i++)
                {
                    Mesh mesh = meshesToMerge[i];
                    allPolygons = CsgOperations.PerformOperation(allPolygons, mesh, CsgNode.Union);
                }
            }
            else
            {
                for (int i = 0; i < meshesToMerge.Count; i++)
                {
                    Mesh mesh = meshesToMerge[i];
                    foreach (Face face in mesh.Faces)
                    {
                        List<Vertex> faceVertices = new List<Vertex>();
                        foreach (FaceEdge faceEdgeToAdd in face.FaceEdgeIterator())
                        {
                            // we allow duplicates (the true) to make sure we are not changing the loaded models acuracy.
                            Vertex newVertex = allPolygons.CreateVertex(faceEdgeToAdd.firstVertex.Position, true, true);
                            faceVertices.Add(newVertex);
                        }

                        // we allow duplicates (the true) to make sure we are not changing the loaded models acuracy.
                        allPolygons.CreateFace(faceVertices.ToArray(), true);
                    }

                    int nextPercent = startPercent + (i + 1) * lengthPercent / meshesToMerge.Count;
                    backgroundWorker.ReportProgress(nextPercent);
                }

                allPolygons.CleanAndMergMesh();
            }

            return allPolygons;
        }

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

        public static void PlaceMeshOnBed(List<Mesh> meshesList, List<Matrix4X4> meshTransforms, int index, bool alsoCenterXY = true)
        {
            AxisAlignedBoundingBox bounds = meshesList[index].GetAxisAlignedBoundingBox(meshTransforms[index]);
            Vector3 boundsCenter = (bounds.maxXYZ + bounds.minXYZ) / 2;
            if (alsoCenterXY)
            {
                meshTransforms[index] *= Matrix4X4.CreateTranslation(-boundsCenter + new Vector3(0, 0, bounds.ZSize / 2));
            }
            else
            {
                meshTransforms[index] *= Matrix4X4.CreateTranslation(new Vector3(0, 0, -boundsCenter.z + bounds.ZSize / 2));
            }
        }

        public static void PlaceAllMeshesOnBed(List<Mesh> meshesList, List<Matrix4X4> meshTransforms)
        {
            for (int i = 0; i < meshesList.Count; i++)
            {
                PlaceMeshOnBed(meshesList, meshTransforms, i);
            }
        }

        public static void CenterMeshesXY(List<Mesh> meshesList, List<Matrix4X4> meshTransforms)
        {
            bool first = true;
            AxisAlignedBoundingBox totalBounds = new AxisAlignedBoundingBox(new Vector3(), new Vector3());
            for(int index= 0; index<meshesList.Count; index++)
            {
                if(first)
                {
                    totalBounds = meshesList[index].GetAxisAlignedBoundingBox(meshTransforms[index]);
                    first = false;
                }
                else
                {
                    AxisAlignedBoundingBox bounds = meshesList[index].GetAxisAlignedBoundingBox(meshTransforms[index]);
                    totalBounds = AxisAlignedBoundingBox.Union(totalBounds, bounds);
                }
            }

            Vector3 boundsCenter = (totalBounds.maxXYZ + totalBounds.minXYZ) / 2;
            boundsCenter.z = 0;

            for (int index = 0; index < meshesList.Count; index++)
            {
                meshTransforms[index] *= Matrix4X4.CreateTranslation(-boundsCenter);
            }
        }

        public static void FindPositionForPartAndAddToPlate(Mesh meshToAdd, Matrix4X4 meshTransform, List<PlatingMeshData> perMeshInfo, List<Mesh> meshesToAvoid, List<Matrix4X4> meshTransforms)
        {
            if (meshToAdd == null || meshToAdd.Vertices.Count < 3)
            {
                return;
            }

            meshesToAvoid.Add(meshToAdd);

            PlatingMeshData newMeshInfo = new PlatingMeshData();
            perMeshInfo.Add(newMeshInfo);
            meshTransforms.Add(meshTransform);

            int index = meshesToAvoid.Count-1;
            MoveMeshToOpenPosition(index, perMeshInfo, meshesToAvoid, meshTransforms);

            PlaceMeshOnBed(meshesToAvoid, meshTransforms, index, false);
        }

        public static void MoveMeshToOpenPosition(int meshToMoveIndex, List<PlatingMeshData> perMeshInfo, List<Mesh> allMeshes, List<Matrix4X4> meshTransforms)
        {
            Mesh meshToMove = allMeshes[meshToMoveIndex];
            // find a place to put it that doesn't hit anything
            AxisAlignedBoundingBox meshToMoveBounds = meshToMove.GetAxisAlignedBoundingBox(meshTransforms[meshToMoveIndex]);
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
                for(int i=0; i<allMeshes.Count; i++)
                {
                    Mesh meshToTest = allMeshes[i];
                    if (meshToTest != meshToMove)
                    {
                        AxisAlignedBoundingBox existingMeshBounds = meshToTest.GetAxisAlignedBoundingBox(meshTransforms[i]);
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

            meshTransforms[meshToMoveIndex] *= transform;
        }

        public static void CreateITraceableForMesh(List<PlatingMeshData> perMeshInfo, List<Mesh> meshes, int i)
        {
            if (meshes[i] != null)
            {
                List<IRayTraceable> allPolys = new List<IRayTraceable>();
                List<Vector3> positions = new List<Vector3>();
                foreach (Face face in meshes[i].Faces)
                {
                    positions.Clear();
                    foreach (Vertex vertex in face.VertexIterator())
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
                }

                perMeshInfo[i].traceableData = BoundingVolumeHierarchy.CreateNewHierachy(allPolys);
            }
        }

    }
}
