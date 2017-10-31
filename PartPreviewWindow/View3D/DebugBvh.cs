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

using MatterHackers.VectorMath;
using System;
using System.Text;
using MatterHackers.RayTracer;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.Agg;
using System.Collections.Generic;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.DataConverters3D;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class DebugBvh
	{
		int startRenderLevel;
		int endRenderLevel;
		Stack<Matrix4X4> transform = new Stack<Matrix4X4>();

		public static void Render(IPrimitive bvhToRender, Matrix4X4 startingTransform, int startRenderLevel = 0, int endRenderLevel = int.MaxValue)
		{
			DebugBvh visitor = new DebugBvh(startRenderLevel, endRenderLevel);
			visitor.transform.Push(startingTransform);
            visitor.RenderRecursive((dynamic)bvhToRender);
		}

		public DebugBvh(int startRenderLevel = 0, int endRenderLevel = int.MaxValue)
		{
			this.startRenderLevel = startRenderLevel;
			this.endRenderLevel = endRenderLevel;
		}

		Mesh lineMesh = PlatonicSolids.CreateCube(1, 1, 1);
		private void RenderLine(Matrix4X4 transform, Vector3 start, Vector3 end, Color color, bool zBuffered = true)
		{
			Vector3 lineCenter = (start + end) / 2;

			Vector3 delta = start - end;
			Matrix4X4 rotateTransform = Matrix4X4.CreateRotation(new Quaternion(Vector3.UnitX + new Vector3(.0001, -.00001, .00002), delta.GetNormal()));
			Matrix4X4 scaleTransform = Matrix4X4.CreateScale((end - start).Length, 1, 1);
			Matrix4X4 lineTransform = scaleTransform * rotateTransform * Matrix4X4.CreateTranslation(lineCenter) * transform;

			if (zBuffered)
			{
				GLHelper.Render(lineMesh, Color.Black, lineTransform, RenderTypes.Shaded);
				//drawEvent.graphics2D.Line(cornerPositionScreen, cornerPositionCcwScreen, RGBA_Bytes.Gray);
			}
			else
			{
				// render on top of everything very lightly
				GLHelper.Render(lineMesh, new Color(Color.Black, 5), lineTransform, RenderTypes.Shaded);
			}
		}

		private void RenderBounds(AxisAlignedBoundingBox aabb)
		{
			Color color = Color.Red;

			// the bottom
			RenderLine(transform.Peek(), aabb.GetBottomCorner(0), aabb.GetBottomCorner(1), color);
			RenderLine(transform.Peek(), aabb.GetBottomCorner(1), aabb.GetBottomCorner(2), color);
			RenderLine(transform.Peek(), aabb.GetBottomCorner(2), aabb.GetBottomCorner(3), color);
			RenderLine(transform.Peek(), aabb.GetBottomCorner(3), aabb.GetBottomCorner(0), color);

			// the top
			RenderLine(transform.Peek(), aabb.GetTopCorner(0), aabb.GetTopCorner(1), color);
			RenderLine(transform.Peek(), aabb.GetTopCorner(1), aabb.GetTopCorner(2), color);
			RenderLine(transform.Peek(), aabb.GetTopCorner(2), aabb.GetTopCorner(3), color);
			RenderLine(transform.Peek(), aabb.GetTopCorner(3), aabb.GetTopCorner(0), color);

			// the sides
			RenderLine(transform.Peek(),
				new Vector3(aabb.minXYZ.X, aabb.minXYZ.Y, aabb.minXYZ.Z),
				new Vector3(aabb.minXYZ.X, aabb.minXYZ.Y, aabb.maxXYZ.Z),
				color);
			RenderLine(transform.Peek(),
				new Vector3(aabb.maxXYZ.X, aabb.minXYZ.Y, aabb.minXYZ.Z),
				new Vector3(aabb.maxXYZ.X, aabb.minXYZ.Y, aabb.maxXYZ.Z),
				color);
			RenderLine(transform.Peek(),
				new Vector3(aabb.minXYZ.X, aabb.maxXYZ.Y, aabb.minXYZ.Z),
				new Vector3(aabb.minXYZ.X, aabb.maxXYZ.Y, aabb.maxXYZ.Z),
				color);
			RenderLine(transform.Peek(),
				new Vector3(aabb.maxXYZ.X, aabb.maxXYZ.Y, aabb.minXYZ.Z),
				new Vector3(aabb.maxXYZ.X, aabb.maxXYZ.Y, aabb.maxXYZ.Z),
				color);
		}

		#region Visitor Pattern Functions

		public void RenderRecursive(object objectToProcess, int level = 0)
		{
			throw new Exception("You must write the specialized function for this type.");
		}

		public void RenderRecursive(UnboundCollection objectToProcess, int level = 0)
		{
			RenderBounds(objectToProcess.GetAxisAlignedBoundingBox());
			foreach (var child in objectToProcess.Items)
			{
				RenderRecursive((dynamic)child, level + 1);
			}
		}

		public void RenderRecursive(MeshFaceTraceable objectToProcess, int level = 0)
		{
			RenderBounds(objectToProcess.GetAxisAlignedBoundingBox());
		}

		public void RenderRecursive(Transform objectToProcess, int level = 0)
		{
			RenderBounds(objectToProcess.GetAxisAlignedBoundingBox());
			transform.Push(objectToProcess.Transform);
			RenderRecursive((dynamic)objectToProcess.Child, level + 1);
		}

		#endregion Visitor Patern Functions
	}
}