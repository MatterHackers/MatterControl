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
using MatterHackers.Agg;
using MatterHackers.MatterControl.DesignTools.EditableTypes;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MeshVisualizer
{
	public static class WorldViewExtensions
	{
		public static void RenderCylinderOutline(this WorldView world, Matrix4X4 worldMatrix, Vector3 center, double diameter, double height, int sides, Color color, double lineWidth = 1, double extendLineLength = 0)
		{
			world.RenderCylinderOutline(worldMatrix, center, diameter, height, sides, color, color, lineWidth, extendLineLength);
		}

		public static void RenderCylinderOutline(this WorldView world, Matrix4X4 worldMatrix, Vector3 center, double diameter, double height, int sides, Color topBottomRingColor, Color sideLinesColor, double lineWidth = 1, double extendLineLength = 0, double phase = 0)
		{
			GLHelper.PrepareFor3DLineRender(true);
			Frustum frustum = world.GetClippingFrustum();

			for (int i = 0; i < sides; i++)
			{
				var startAngle = MathHelper.Tau * i / sides + phase;
				var rotatedPoint = new Vector3(Math.Cos(startAngle), Math.Sin(startAngle), 0) * diameter / 2;
				var sideTop = Vector3Ex.Transform(center + rotatedPoint + new Vector3(0, 0, height / 2), worldMatrix);
				var sideBottom = Vector3Ex.Transform(center + rotatedPoint + new Vector3(0, 0, -height / 2), worldMatrix);
				var endAngle = MathHelper.Tau * (i + 1) / sides + phase;
				var rotated2Point = new Vector3(Math.Cos(endAngle), Math.Sin(endAngle), 0) * diameter / 2;
				var topStart = sideTop;
				var topEnd = Vector3Ex.Transform(center + rotated2Point + new Vector3(0, 0, height / 2), worldMatrix);
				var bottomStart = sideBottom;
				var bottomEnd = Vector3Ex.Transform(center + rotated2Point + new Vector3(0, 0, -height / 2), worldMatrix);

				if (extendLineLength > 0)
				{
					GLHelper.ExtendLineEnds(ref sideTop, ref sideBottom, extendLineLength);
				}

				if (sideLinesColor != Color.Transparent)
				{
					world.Render3DLineNoPrep(frustum, sideTop, sideBottom, sideLinesColor, lineWidth);
				}

				if (topBottomRingColor != Color.Transparent)
				{
					world.Render3DLineNoPrep(frustum, topStart, topEnd, topBottomRingColor, lineWidth);
					world.Render3DLineNoPrep(frustum, bottomStart, bottomEnd, topBottomRingColor, lineWidth);
				}
			}

			// turn the lighting back on
			GL.Enable(EnableCap.Lighting);
		}

		public static void RenderAabb(this WorldView world, AxisAlignedBoundingBox bounds, Matrix4X4 matrix, Color color, double width, double extendLineLength = 0)
		{
			GLHelper.PrepareFor3DLineRender(true);

			Frustum frustum = world.GetClippingFrustum();
			for (int i = 0; i < 4; i++)
			{
				Vector3 sideStartPosition = Vector3Ex.Transform(bounds.GetBottomCorner(i), matrix);
				Vector3 sideEndPosition = Vector3Ex.Transform(bounds.GetTopCorner(i), matrix);

				Vector3 bottomStartPosition = sideStartPosition;
				Vector3 bottomEndPosition = Vector3Ex.Transform(bounds.GetBottomCorner((i + 1) % 4), matrix);

				Vector3 topStartPosition = sideEndPosition;
				Vector3 topEndPosition = Vector3Ex.Transform(bounds.GetTopCorner((i + 1) % 4), matrix);

				if (extendLineLength > 0)
				{
					GLHelper.ExtendLineEnds(ref sideStartPosition, ref sideEndPosition, extendLineLength);
					GLHelper.ExtendLineEnds(ref topStartPosition, ref topEndPosition, extendLineLength);
					GLHelper.ExtendLineEnds(ref bottomStartPosition, ref bottomEndPosition, extendLineLength);
				}

				// draw each of the edge lines (4) and their touching top and bottom lines (2 each)
				world.Render3DLineNoPrep(frustum, sideStartPosition, sideEndPosition, color, width);
				world.Render3DLineNoPrep(frustum, topStartPosition, topEndPosition, color, width);
				world.Render3DLineNoPrep(frustum, bottomStartPosition, bottomEndPosition, color, width);
			}

			GL.Enable(EnableCap.Lighting);
		}

		public static void RenderAxis(this WorldView world, Vector3 position, Matrix4X4 matrix, double size, double lineWidth)
		{
			GLHelper.PrepareFor3DLineRender(true);

			Frustum frustum = world.GetClippingFrustum();
			Vector3 length = Vector3.One * size;
			for (int i = 0; i < 3; i++)
			{
				var min = position;
				min[i] -= length[i];
				Vector3 start = Vector3Ex.Transform(min, matrix);

				var max = position;
				max[i] += length[i];
				Vector3 end = Vector3Ex.Transform(max, matrix);

				var color = Agg.Color.Red;
				switch (i)
				{
					case 1:
						color = Agg.Color.Green;
						break;

					case 2:
						color = Agg.Color.Blue;
						break;
				}

				// draw each of the edge lines (4) and their touching top and bottom lines (2 each)
				world.Render3DLineNoPrep(frustum, start, end, color, lineWidth);
			}

			GL.Enable(EnableCap.Lighting);
		}

		public static void RenderDirectionAxis(this WorldView world, DirectionAxis axis, Matrix4X4 matrix, double size)
		{
			GLHelper.PrepareFor3DLineRender(true);

			Frustum frustum = world.GetClippingFrustum();
			Vector3 length = axis.Normal * size;
			var color = Agg.Color.Red;

			// draw center line
			{
				var min = axis.Origin - length;
				Vector3 start = Vector3Ex.Transform(min, matrix);

				var max = axis.Origin + length;
				Vector3 end = Vector3Ex.Transform(max, matrix);

				world.Render3DLineNoPrep(frustum, start, end, color, 1);
			}

			var perpendicular = Vector3.GetPerpendicular(axis.Normal, Vector3.Zero).GetNormal();
			// draw some lines to mark the rotation plane
			int count = 20;
			bool first = true;
			var firstEnd = Vector3.Zero;
			var lastEnd = Vector3.Zero;
			var center = Vector3Ex.Transform(axis.Origin, matrix);
			for (int i = 0; i < count; i++)
			{
				var rotation = size / 4 * Vector3Ex.Transform(perpendicular, Matrix4X4.CreateRotation(axis.Normal, MathHelper.Tau * i / count));
				// draw center line
				var max = axis.Origin + rotation;
				Vector3 end = Vector3Ex.Transform(max, matrix);

				world.Render3DLineNoPrep(frustum, center, end, color, 1);
				if (!first)
				{
					world.Render3DLineNoPrep(frustum, end, lastEnd, color, 1);
				}
				else
				{
					firstEnd = end;
				}

				lastEnd = end;
				first = false;
			}

			world.Render3DLineNoPrep(frustum, firstEnd, lastEnd, color, 1);

			GL.Enable(EnableCap.Lighting);
		}
	}
}
