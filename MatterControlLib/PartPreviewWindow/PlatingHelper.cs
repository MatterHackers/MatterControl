/*
Copyright (c) 2023, Lars Brubaker
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
using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using Polygon = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;

namespace MatterHackers.MatterControl
{
    public static class PlatingHelper
    {
        public static VertexStorage PolygonToPathStorage(this Polygons polygons)
        {
            var output = new VertexStorage();

            foreach (Polygon polygon in polygons)
            {
                bool first = true;
                foreach (IntPoint point in polygon)
                {
                    if (first)
                    {
                        output.Add(point.X, point.Y, ShapePath.FlagsAndCommand.MoveTo);
                        first = false;
                    }
                    else
                    {
                        output.Add(point.X, point.Y, ShapePath.FlagsAndCommand.LineTo);
                    }
                }

                output.ClosePolygon();
            }

            output.Add(0, 0, ShapePath.FlagsAndCommand.Stop);

            return output;
        }

        public enum PositionType
        {
            Center,
            LowerLeft,
            None,
        }

        /// <summary>
        /// Arrange the given parts on the bed and return a list of the parts that were arranged
        /// </summary>
        /// <param name="object3DList">The parts to arrange</param>
        /// <param name="arangePosition">A position to arrange around</param>
        /// <param name="positionType">The way to consider the possition</param>
        /// <param name="bedBounds">Optional bounds to arrange into</param>
        /// <param name="progressReporter">The current progress of arranging</param>
        /// <returns>A list of the parts that were arranged</returns>
        public static List<IObject3D> ArrangeOnBed(List<IObject3D> object3DList,
            Vector3 arangePosition,
            PositionType positionType,
            RectangleDouble? bedBounds = null,
            Action<double, string> progressReporter = null)
        {
            if (object3DList.Count == 0)
            {
                return null;
            }

            var objectsThatWereArrange = new List<IObject3D>();
            var objectsThatHaveBeenPlaced = new List<IObject3D>();

            // sort them by size
            object3DList.Sort(SortOnBigToLittle);

            double ratioPerMeshGroup = 1.0 / object3DList.Count;
            double currentRatioDone = 0;
            // put them onto the plate (try the center) starting with the biggest and moving down
            for (int meshGroupIndex = 0; meshGroupIndex < object3DList.Count; meshGroupIndex++)
            {
                var object3D = object3DList[meshGroupIndex];
                Vector3 meshLowerLeft = object3D.GetAxisAlignedBoundingBox().MinXYZ;
                object3D.Matrix *= Matrix4X4.CreateTranslation(-meshLowerLeft);

                if (MoveToOpenPositionRelativeGroup(object3D, objectsThatHaveBeenPlaced, bedBounds))
                {
                    objectsThatHaveBeenPlaced.Add(object3D);
                    objectsThatWereArrange.Add(object3D);
                }

                progressReporter?.Invoke(Util.GetRatio(0, 1, meshGroupIndex, object3DList.Count), null);

                currentRatioDone += ratioPerMeshGroup;

                // and put it on the bed (set the bottom to z = 0)
                PlaceOnBed(object3D);
            }

            // and finally center whatever we have as a group
            if (positionType != PositionType.None)
            {
                AxisAlignedBoundingBox bounds = object3DList[0].GetAxisAlignedBoundingBox();
                for (int i = 1; i < object3DList.Count; i++)
                {
                    bounds = AxisAlignedBoundingBox.Union(bounds, object3DList[i].GetAxisAlignedBoundingBox());
                }

                Vector3 offset = bounds.MinXYZ;
                if (positionType == PositionType.Center)
                {
                    offset = (bounds.MaxXYZ + bounds.MinXYZ) / 2;
                    offset.Z = 0;
                }

                for (int i = 0; i < object3DList.Count; i++)
                {
                    object3DList[i].Matrix *= Matrix4X4.CreateTranslation(arangePosition - offset);
                }
            }

            return objectsThatWereArrange;
        }

        private static int SortOnBigToLittle(IObject3D a, IObject3D b)
        {
            AxisAlignedBoundingBox xAABB = b.GetAxisAlignedBoundingBox();
            AxisAlignedBoundingBox yAABB = a.GetAxisAlignedBoundingBox();
            return Math.Max(xAABB.XSize, xAABB.YSize).CompareTo(Math.Max(yAABB.XSize, yAABB.YSize));
        }

        public static void PlaceOnBed(IObject3D object3D)
        {
            AxisAlignedBoundingBox bounds = object3D.GetAxisAlignedBoundingBox();
            object3D.Matrix *= Matrix4X4.CreateTranslation(new Vector3(0, 0, -bounds.MinXYZ.Z));
        }

        /// <summary>
        /// Moves the target object to the first non-colliding position, starting from the lower left corner of the bounding box containing all sceneItems
        /// </summary>
        /// <param name="objectToAdd">The object to position</param>
        /// <param name="itemsToAvoid">The objects to hit test against</param>
        public static bool MoveToOpenPositionRelativeGroup(IObject3D objectToAdd, IEnumerable<IObject3D> itemsToAvoid, RectangleDouble? bedBounds = null)
        {
            if (objectToAdd == null)
            {
                return false;
            }

            // move the part to the total bounds lower left side
            Vector3 meshLowerLeft = objectToAdd.GetAxisAlignedBoundingBox().MinXYZ;
            objectToAdd.Matrix *= Matrix4X4.CreateTranslation(-meshLowerLeft);

            // keep moving the item until its in an open slot
            return MoveToOpenPosition(objectToAdd, itemsToAvoid, bedBounds);
        }

        /// <summary>
        /// Moves the target object to the first non-colliding position, starting at the initial position of the target object
        /// </summary>
        /// <param name="itemToMove">The object to position</param>
        /// <param name="itemsToAvoid">The objects to hit test against</param>
        public static bool MoveToOpenPosition(IObject3D itemToMove, IEnumerable<IObject3D> itemsToAvoid, RectangleDouble? bedBounds = null)
        {
            if (itemToMove == null)
            {
                return false;
            }

            // find a place to put it that doesn't hit anything
            var currentBounds = itemToMove.GetAxisAlignedBoundingBox();
            var itemToMoveBounds = new AxisAlignedBoundingBox(currentBounds.MinXYZ, currentBounds.MaxXYZ);

            // add in a few mm so that it will not be touching
            itemToMoveBounds.MinXYZ -= new Vector3(2, 2, 0);
            itemToMoveBounds.MaxXYZ += new Vector3(2, 2, 0);

            while (true)
            {
                int distance = 0;
                while (true)
                {
                    for (int i = 0; i <= distance; i++)
                    {
                        Matrix4X4 transform;
                        if (CheckPosition(itemsToAvoid, itemToMove, itemToMoveBounds, i, distance, out transform))
                        {
                            AxisAlignedBoundingBox testBounds = itemToMoveBounds.NewTransformed(transform);

                            if (bedBounds != null
                                && (distance + testBounds.MaxXYZ.X > bedBounds.Value.Width
                                || distance + testBounds.MaxXYZ.Y > bedBounds.Value.Height))
                            {
                                return false;
                            }

                            itemToMove.Matrix *= transform;
                            return true;
                        }

                        // don't check if the position is the same as the one we just checked
                        if (distance != i
                            && CheckPosition(itemsToAvoid, itemToMove, itemToMoveBounds, distance, i, out transform))
                        {
                            AxisAlignedBoundingBox testBounds = itemToMoveBounds.NewTransformed(transform);

                            if (bedBounds != null
                                && (distance + testBounds.MaxXYZ.X > bedBounds.Value.Width
                                || distance + testBounds.MaxXYZ.Y > bedBounds.Value.Height))
                            {
                                return false;
                            }

                            itemToMove.Matrix *= transform;
                            return true;
                        }
                    }

                    distance++;
                }
            }
        }

        private static bool CheckPosition(IEnumerable<IObject3D> itemsToAvoid, IObject3D itemToMove, AxisAlignedBoundingBox meshToMoveBounds, int yStep, int xStep, out Matrix4X4 transform)
        {
            double xStepAmount = 5;
            double yStepAmount = 5;

            var positionTransform = Matrix4X4.CreateTranslation(xStep * xStepAmount, yStep * yStepAmount, 0);
            Vector3 newPosition = Vector3Ex.Transform(Vector3.Zero, positionTransform);

            transform = Matrix4X4.CreateTranslation(newPosition);

            AxisAlignedBoundingBox testBounds = meshToMoveBounds.NewTransformed(transform);

            foreach (IObject3D meshToTest in itemsToAvoid)
            {
                if (meshToTest != itemToMove)
                {
                    AxisAlignedBoundingBox existingMeshBounds = meshToTest.GetAxisAlignedBoundingBox();
                    var intersection = AxisAlignedBoundingBox.Intersection(testBounds, existingMeshBounds);
                    if (intersection.XSize > 0 && intersection.YSize > 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
