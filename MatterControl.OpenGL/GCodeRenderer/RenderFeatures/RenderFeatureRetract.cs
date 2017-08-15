using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

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

namespace MatterHackers.GCodeVisualizer
{
	public class RenderFeatureRetract : RenderFeatureBase
	{
		public static double RetractionDrawRadius = 1;

		private float extrusionAmount;
		private float mmPerSecond;
		private Vector3Float position;

		public RenderFeatureRetract(Vector3 position, double extrusionAmount, int extruderIndex, double mmPerSecond)
			: base(extruderIndex)
		{
			this.extrusionAmount = (float)extrusionAmount;
			this.mmPerSecond = (float)mmPerSecond;

			this.position = new Vector3Float(position);
		}

		private double Radius(double layerScale)
		{
			double radius = RetractionDrawRadius * layerScale;
			double area = Math.PI * radius * radius;
			area *= Math.Abs(extrusionAmount);
			radius = Math.Sqrt(area / Math.PI);
			return radius;
		}

		public override void CreateRender3DData(VectorPOD<ColorVertexData> colorVertexData, VectorPOD<int> indexData, GCodeRenderInfo renderInfo)
		{
			if ((renderInfo.CurrentRenderType & RenderType.Retractions) == RenderType.Retractions)
			{
				Vector3 position = new Vector3(this.position);

				if (renderInfo.CurrentRenderType.HasFlag(RenderType.HideExtruderOffsets))
				{
					Vector2 offset = renderInfo.GetExtruderOffset(extruderIndex);
					position = position + new Vector3(offset);
				}

				// retract and unretract are the extruder color
				RGBA_Bytes color = renderInfo.GetMaterialColor(extruderIndex);
				// except for extruder 0 where they are the red and blue we are familiar with
				if (extruderIndex == 0)
				{
					if (extrusionAmount > 0)
					{
						color = RGBA_Bytes.Blue;
					}
					else
					{
						color = RGBA_Bytes.Red;
					}
				}
				if (extrusionAmount > 0)
				{
					// unretraction
					CreatePointer(colorVertexData, indexData, position + new Vector3(0, 0, 1.3), position + new Vector3(0, 0, .3), Radius(1), 5, color);
				}
				else
				{
					// retraction
					CreatePointer(colorVertexData, indexData, position + new Vector3(0, 0, .3), position + new Vector3(0, 0, 1.3), Radius(1), 5, color);
				}
			}
		}

		public override void Render(Graphics2D graphics2D, GCodeRenderInfo renderInfo)
		{
			if ((renderInfo.CurrentRenderType & RenderType.Retractions) == RenderType.Retractions)
			{
				double radius = Radius(renderInfo.LayerScale);
				Vector2 position = new Vector2(this.position.x, this.position.y);

				if (renderInfo.CurrentRenderType.HasFlag(RenderType.HideExtruderOffsets))
				{
					Vector2 offset = renderInfo.GetExtruderOffset(extruderIndex);
					position = position + offset;
				}

				renderInfo.Transform.transform(ref position);

				RGBA_Bytes retractionColor = new RGBA_Bytes(RGBA_Bytes.Red, 200);
				if (extrusionAmount > 0)
				{
					// unretraction
					retractionColor = new RGBA_Bytes(RGBA_Bytes.Blue, 200);
				}

				// render the part using opengl
				Graphics2DOpenGL graphics2DGl = graphics2D as Graphics2DOpenGL;
				if (graphics2DGl != null)
				{
					graphics2DGl.DrawAACircle(position, radius, retractionColor);
				}
				else
				{
					Ellipse extrusion = new Ellipse(position, radius);
					graphics2D.Render(extrusion, retractionColor);
				}
			}
		}
	}
}