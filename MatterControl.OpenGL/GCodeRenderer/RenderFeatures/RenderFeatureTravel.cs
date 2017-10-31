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
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.GCodeVisualizer
{
	public class RenderFeatureTravel : RenderFeatureBase
	{
		protected Vector3Float start;
		protected Vector3Float end;
		protected float travelSpeed;

		protected Vector3Float GetStart(GCodeRenderInfo renderInfo)
		{
			if (renderInfo.CurrentRenderType.HasFlag(RenderType.HideExtruderOffsets))
			{
				Vector3Float start = this.start;
				Vector2 offset = renderInfo.GetExtruderOffset(extruderIndex);
				start.x += (float)offset.X;
				start.y += (float)offset.Y;
				return start;
			}

			return this.start;
		}

		protected Vector3Float GetEnd(GCodeRenderInfo renderInfo)
		{
			if (renderInfo.CurrentRenderType.HasFlag(RenderType.HideExtruderOffsets))
			{
				Vector3Float end = this.end;
				Vector2 offset = renderInfo.GetExtruderOffset(extruderIndex);
				end.x += (float)offset.X;
				end.y += (float)offset.Y;
				return end;
			}

			return this.end;
		}

		public RenderFeatureTravel(Vector3 start, Vector3 end, int extruderIndex, double travelSpeed)
			: base(extruderIndex)
		{
			this.extruderIndex = extruderIndex;
			this.start = new Vector3Float(start);
			this.end = new Vector3Float(end);
			this.travelSpeed = (float)travelSpeed;
		}

		public override void CreateRender3DData(VectorPOD<ColorVertexData> colorVertexData, VectorPOD<int> indexData, GCodeRenderInfo renderInfo)
		{
			if ((renderInfo.CurrentRenderType & RenderType.Moves) == RenderType.Moves)
			{
				Vector3Float start = this.GetStart(renderInfo);
				Vector3Float end = this.GetEnd(renderInfo);
				CreateCylinder(colorVertexData, indexData, new Vector3(start), new Vector3(end), .1, 6, GCodeRenderer.TravelColor, .2);
			}
		}

		public override void Render(Graphics2D graphics2D, GCodeRenderInfo renderInfo)
		{
			if ((renderInfo.CurrentRenderType & RenderType.Moves) == RenderType.Moves)
			{
				double movementLineWidth = 0.35 * renderInfo.LayerScale;
				Color movementColor = new Color(10, 190, 15);

				// render the part using opengl
				Graphics2DOpenGL graphics2DGl = graphics2D as Graphics2DOpenGL;
				if (graphics2DGl != null)
				{
					Vector3Float startF = this.GetStart(renderInfo);
					Vector3Float endF = this.GetEnd(renderInfo);
					Vector2 start = new Vector2(startF.x, startF.y);
					renderInfo.Transform.transform(ref start);

					Vector2 end = new Vector2(endF.x, endF.y);
					renderInfo.Transform.transform(ref end);

					graphics2DGl.DrawAALineRounded(start, end, movementLineWidth, movementColor);
				}
				else
				{
					VertexStorage pathStorage = new VertexStorage();
					VertexSourceApplyTransform transformedPathStorage = new VertexSourceApplyTransform(pathStorage, renderInfo.Transform);
					Stroke stroke = new Stroke(transformedPathStorage, movementLineWidth);

					stroke.line_cap(LineCap.Round);
					stroke.line_join(LineJoin.Round);

					Vector3Float start = this.GetStart(renderInfo);
					Vector3Float end = this.GetEnd(renderInfo);

					pathStorage.Add(start.x, start.y, ShapePath.FlagsAndCommand.CommandMoveTo);
					if (end.x != start.x || end.y != start.y)
					{
						pathStorage.Add(end.x, end.y, ShapePath.FlagsAndCommand.CommandLineTo);
					}
					else
					{
						pathStorage.Add(end.x + .01, end.y, ShapePath.FlagsAndCommand.CommandLineTo);
					}

					graphics2D.Render(stroke, 0, movementColor);
				}
			}
		}
	}
}