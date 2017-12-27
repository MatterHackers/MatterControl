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

using MatterHackers.Agg;
using MatterHackers.VectorMath;

namespace MatterHackers.GCodeVisualizer
{
	public abstract class RenderFeatureBase
	{
		protected int extruderIndex;

		public abstract void Render(Graphics2D graphics2D, GCodeRenderInfo renderInfo);

		public abstract void CreateRender3DData(VectorPOD<ColorVertexData> colorVertexData, VectorPOD<int> indexData, GCodeRenderInfo renderInfo);

		public RenderFeatureBase(int extruderIndex)
		{
			this.extruderIndex = extruderIndex;
		}

		static public void CreateCylinder(VectorPOD<ColorVertexData> colorVertexData, VectorPOD<int> indexData, Vector3 startPos, Vector3 endPos, double radius, int steps, Color color, double layerHeight)
		{
			Vector3 direction = endPos - startPos;
			Vector3 directionNormal = direction.GetNormal();
			Vector3 startSweepDirection = Vector3.GetPerpendicular(startPos, endPos).GetNormal();

			int[] tubeStartIndices = new int[steps];
			int[] tubeEndIndices = new int[steps];

			int[] capStartIndices = new int[steps];
			int[] capEndIndices = new int[steps];

			double halfHeight = layerHeight / 2; // + (layerHeight * .1);
			double halfWidth = radius;
			double zScale = halfHeight / radius;
			double xScale = halfWidth / radius;

			// Adjust start/end positions to be centered on Z for the given layer height
			startPos.Z -= halfHeight;
			endPos.Z -= halfHeight;

			Vector3 scale = new Vector3(xScale, xScale, zScale);

			for (int i = 0; i < steps; i++)
			{
				// create tube ends verts
				Vector3 tubeNormal = Vector3.Transform(startSweepDirection, Matrix4X4.CreateRotation(direction, MathHelper.Tau / (steps * 2) + MathHelper.Tau / (steps) * i));
				Vector3 offset = Vector3.Transform(startSweepDirection * radius, Matrix4X4.CreateRotation(direction, MathHelper.Tau / (steps * 2) + MathHelper.Tau / (steps) * i));
				offset *= scale;

				Vector3 tubeStart = startPos + offset;
				tubeStartIndices[i] = colorVertexData.Count;
				colorVertexData.Add(new ColorVertexData(tubeStart, tubeNormal, color));

				Vector3 tubeEnd = endPos + offset;
				tubeEndIndices[i] = colorVertexData.Count;
				colorVertexData.Add(new ColorVertexData(tubeEnd, tubeNormal, color));

				// create cap verts
				Vector3 rotateAngle = Vector3.Cross(startSweepDirection, direction);
				Vector3 capStartNormal = Vector3.Transform(startSweepDirection, Matrix4X4.CreateRotation(rotateAngle, MathHelper.Tau / 8));
				capStartNormal = Vector3.Transform(capStartNormal, Matrix4X4.CreateRotation(direction, MathHelper.Tau / (steps * 2) + MathHelper.Tau / (steps) * i));
				capStartNormal = (capStartNormal * scale).GetNormal();
				Vector3 capStartOffset = capStartNormal * radius;
				capStartOffset *= scale;
				Vector3 capStart = startPos + capStartOffset;
				capStartIndices[i] = colorVertexData.Count;
				colorVertexData.Add(new ColorVertexData(capStart, capStartNormal, color));

				Vector3 capEndNormal = Vector3.Transform(startSweepDirection, Matrix4X4.CreateRotation(-rotateAngle, MathHelper.Tau / 8));
				capEndNormal = Vector3.Transform(capEndNormal, Matrix4X4.CreateRotation(direction, MathHelper.Tau / (steps * 2) + MathHelper.Tau / (steps) * i));
				capEndNormal = (capEndNormal * scale).GetNormal();
				Vector3 capEndOffset = capEndNormal * radius;
				capEndOffset *= scale;
				Vector3 capEnd = endPos + capEndOffset;
				capEndIndices[i] = colorVertexData.Count;
				colorVertexData.Add(new ColorVertexData(capEnd, capEndNormal, color));
			}

			int tipStartIndex = colorVertexData.Count;
			Vector3 tipOffset = directionNormal * radius;
			tipOffset *= scale;
			colorVertexData.Add(new ColorVertexData(startPos - tipOffset, -directionNormal, color));
			int tipEndIndex = colorVertexData.Count;
			colorVertexData.Add(new ColorVertexData(endPos + tipOffset, directionNormal, color));

			for (int i = 0; i < steps; i++)
			{
				// create tube polys
				indexData.Add(tubeStartIndices[i]);
				indexData.Add(tubeEndIndices[i]);
				indexData.Add(tubeEndIndices[(i + 1) % steps]);

				indexData.Add(tubeStartIndices[i]);
				indexData.Add(tubeEndIndices[(i + 1) % steps]);
				indexData.Add(tubeStartIndices[(i + 1) % steps]);

				// create start cap polys
				indexData.Add(tubeStartIndices[i]);
				indexData.Add(capStartIndices[i]);
				indexData.Add(capStartIndices[(i + 1) % steps]);

				indexData.Add(tubeStartIndices[i]);
				indexData.Add(capStartIndices[(i + 1) % steps]);
				indexData.Add(tubeStartIndices[(i + 1) % steps]);

				// create end cap polys
				indexData.Add(tubeEndIndices[i]);
				indexData.Add(capEndIndices[i]);
				indexData.Add(capEndIndices[(i + 1) % steps]);

				indexData.Add(tubeEndIndices[i]);
				indexData.Add(capEndIndices[(i + 1) % steps]);
				indexData.Add(tubeEndIndices[(i + 1) % steps]);

				// create start tip polys
				indexData.Add(tipStartIndex);
				indexData.Add(capStartIndices[i]);
				indexData.Add(capStartIndices[(i + 1) % steps]);

				// create end tip polys
				indexData.Add(tipEndIndex);
				indexData.Add(capEndIndices[i]);
				indexData.Add(capEndIndices[(i + 1) % steps]);
			}
		}

		static public void CreatePointer(VectorPOD<ColorVertexData> colorVertexData, VectorPOD<int> indexData, Vector3 startPos, Vector3 endPos, double radius, int steps, Color color)
		{
			Vector3 direction = endPos - startPos;
			Vector3 directionNormal = direction.GetNormal();
			Vector3 startSweepDirection = Vector3.GetPerpendicular(startPos, endPos).GetNormal();

			int[] tubeStartIndices = new int[steps];

			for (int i = 0; i < steps; i++)
			{
				// create tube ends verts
				Vector3 tubeNormal = Vector3.Transform(startSweepDirection, Matrix4X4.CreateRotation(direction, MathHelper.Tau / (steps * 2) + MathHelper.Tau / (steps) * i));
				Vector3 offset = Vector3.Transform(startSweepDirection * radius, Matrix4X4.CreateRotation(direction, MathHelper.Tau / (steps * 2) + MathHelper.Tau / (steps) * i));
				Vector3 tubeStart = startPos + offset;
				tubeStartIndices[i] = colorVertexData.Count;
				colorVertexData.Add(new ColorVertexData(tubeStart, tubeNormal, color));
				Vector3 tubeEnd = endPos + offset;
			}

			int tipEndIndex = colorVertexData.Count;
			colorVertexData.Add(new ColorVertexData(endPos, directionNormal, color));

			for (int i = 0; i < steps; i++)
			{
				// create tube polys
				indexData.Add(tubeStartIndices[i]);
				indexData.Add(tubeStartIndices[(i + 1) % steps]);

				indexData.Add(tipEndIndex);
			}
		}
	}
}