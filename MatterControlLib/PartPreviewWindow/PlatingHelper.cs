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
			VertexStorage output = new VertexStorage();

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

		public static void ArrangeOnBed(List<IObject3D> object3DList, Vector3 bedCenter)
		{
			if (object3DList.Count == 0)
			{
				return;
			}

			// move them all out of the way
			for (int i = 0; i < object3DList.Count; i++)
			{
				object3DList[i].Matrix *= Matrix4X4.CreateTranslation(10000, 10000, 0);
			}

			// sort them by size
			object3DList.Sort(SortOnSize);

			double ratioPerMeshGroup = 1.0 / object3DList.Count;
			double currentRatioDone = 0;
			// put them onto the plate (try the center) starting with the biggest and moving down
			for (int meshGroupIndex = 0; meshGroupIndex < object3DList.Count; meshGroupIndex++)
			{
				var object3D = object3DList[meshGroupIndex];
				Vector3 meshLowerLeft = object3D.GetAxisAlignedBoundingBox().MinXYZ;
				object3D.Matrix *= Matrix4X4.CreateTranslation(-meshLowerLeft);

				PlatingHelper.MoveToOpenPositionRelativeGroup(object3D, object3DList);

				currentRatioDone += ratioPerMeshGroup;

				// and put it on the bed
				PlatingHelper.PlaceOnBed(object3D);
			}

			// and finally center whatever we have as a group
			{
				AxisAlignedBoundingBox bounds = object3DList[0].GetAxisAlignedBoundingBox();
				for (int i = 1; i < object3DList.Count; i++)
				{
					bounds = AxisAlignedBoundingBox.Union(bounds, object3DList[i].GetAxisAlignedBoundingBox());
				}

				Vector3 boundsCenter = (bounds.MaxXYZ + bounds.MinXYZ) / 2;
				for (int i = 0; i < object3DList.Count; i++)
				{
					object3DList[i].Matrix *= Matrix4X4.CreateTranslation(-boundsCenter + new Vector3(0, 0, bounds.ZSize / 2) + bedCenter);
				}
			}
		}

		private static int SortOnSize(IObject3D x, IObject3D y)
		{
			AxisAlignedBoundingBox xAABB = x.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox yAABB = y.GetAxisAlignedBoundingBox();
			return Math.Max(xAABB.XSize, xAABB.YSize).CompareTo(Math.Max(yAABB.XSize, yAABB.YSize));
		}

		public static void PlaceOnBed(IObject3D object3D)
		{
			AxisAlignedBoundingBox bounds = object3D.GetAxisAlignedBoundingBox();
			Vector3 boundsCenter = (bounds.MaxXYZ + bounds.MinXYZ) / 2;

			object3D.Matrix *= Matrix4X4.CreateTranslation(new Vector3(0, 0, -boundsCenter.Z + bounds.ZSize / 2));
		}

		/// <summary>
		/// Moves the target object to the first non-colliding position, starting from the lower left corner of the bounding box containing all sceneItems
		/// </summary>
		/// <param name="objectToAdd">The object to position</param>
		/// <param name="itemsToAvoid">The objects to hit test against</param>
		public static void MoveToOpenPositionRelativeGroup(IObject3D objectToAdd, IEnumerable<IObject3D> itemsToAvoid)
		{
			if (objectToAdd == null || !itemsToAvoid.Any())
			{
				return;
			}

			// find the bounds of all items in the scene
			AxisAlignedBoundingBox allPlacedMeshBounds = itemsToAvoid.GetUnionedAxisAlignedBoundingBox();

			// move the part to the total bounds lower left side
			Vector3 meshLowerLeft = objectToAdd.GetAxisAlignedBoundingBox().MinXYZ;
			objectToAdd.Matrix *= Matrix4X4.CreateTranslation(-meshLowerLeft + allPlacedMeshBounds.MinXYZ);

			// make sure it is on the 0 plane
			var aabb = objectToAdd.GetAxisAlignedBoundingBox();
			objectToAdd.Matrix *= Matrix4X4.CreateTranslation(0, 0, -aabb.MinXYZ.Z);

			// keep moving the item until its in an open slot
			MoveToOpenPosition(objectToAdd, itemsToAvoid);
		}

		/// <summary>
		/// Moves the target object to the first non-colliding position, starting at the initial position of the target object
		/// </summary>
		/// <param name="itemToMove">The item to move</param>
		/// <param name="itemsToAvoid">The objects to hit test against</param>
		public static void MoveToOpenPosition(IObject3D itemToMove, IEnumerable<IObject3D> itemsToAvoid)
		{
			// find a place to put it that doesn't hit anything
			AxisAlignedBoundingBox itemToMoveBounds = itemToMove.GetAxisAlignedBoundingBox();

			// add in a few mm so that it will not be touching
			itemToMoveBounds.MinXYZ -= new Vector3(2, 2, 0);
			itemToMoveBounds.MaxXYZ += new Vector3(2, 2, 0);

			Matrix4X4 transform = Matrix4X4.Identity;
			int currentSize = 1;
			bool partPlaced = false;

			while (!partPlaced && itemToMove != null)
			{
				int yStep = 0;
				int xStep = currentSize;
				// check far right edge
				for (yStep = 0; yStep < currentSize; yStep++)
				{
					partPlaced = CheckPosition(itemsToAvoid, itemToMove, itemToMoveBounds, yStep, xStep, ref transform);

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
						partPlaced = CheckPosition(itemsToAvoid, itemToMove, itemToMoveBounds, yStep, xStep, ref transform);

						if (partPlaced)
						{
							break;
						}
					}

					if (!partPlaced)
					{
						xStep = currentSize;
						// check top right point
						partPlaced = CheckPosition(itemsToAvoid, itemToMove, itemToMoveBounds, yStep, xStep, ref transform);
					}
				}

				currentSize++;
			}

			itemToMove.Matrix *= transform;
		}

		private static bool CheckPosition(IEnumerable<IObject3D> itemsToAvoid, IObject3D itemToMove, AxisAlignedBoundingBox meshToMoveBounds, int yStep, int xStep, ref Matrix4X4 transform)
		{
			double xStepAmount = 5;
			double yStepAmount = 5;

			Matrix4X4 positionTransform = Matrix4X4.CreateTranslation(xStep * xStepAmount, yStep * yStepAmount, 0);
			Vector3 newPosition = Vector3Ex.Transform(Vector3.Zero, positionTransform);

			transform = Matrix4X4.CreateTranslation(newPosition);

			AxisAlignedBoundingBox testBounds = meshToMoveBounds.NewTransformed(transform);

			foreach (IObject3D meshToTest in itemsToAvoid)
			{
				if (meshToTest != itemToMove)
				{
					AxisAlignedBoundingBox existingMeshBounds = meshToTest.GetAxisAlignedBoundingBox();
					AxisAlignedBoundingBox intersection = AxisAlignedBoundingBox.Intersection(testBounds, existingMeshBounds);
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
