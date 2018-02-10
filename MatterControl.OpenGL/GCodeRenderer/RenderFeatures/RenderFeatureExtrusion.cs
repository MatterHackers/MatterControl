using MatterHackers.Agg;
using MatterHackers.Agg.UI;
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
	public class RenderFeatureExtrusion : RenderFeatureTravel
	{
		private float extrusionVolumeMm3;
		private float layerHeight;
		private Color color;

		public RenderFeatureExtrusion(Vector3 start, Vector3 end, int extruderIndex, double travelSpeed, double totalExtrusionMm, double filamentDiameterMm, double layerHeight, Color color)
			: base(start, end, extruderIndex, travelSpeed)
		{
			this.color = color;
			double filamentRadius = filamentDiameterMm / 2;
			double areaSquareMm = (filamentRadius * filamentRadius) * Math.PI;

			this.extrusionVolumeMm3 = (float)(areaSquareMm * totalExtrusionMm);
			this.layerHeight = (float)layerHeight;
		}

		private double GetRadius(RenderType renderType)
		{
			return GetExtrusionWidth(renderType) / 2;
		}

		private double GetExtrusionWidth(RenderType renderType)
		{
			double width = GCodeRenderer.ExtruderWidth;
			if ((renderType & RenderType.SimulateExtrusion) == RenderType.SimulateExtrusion)
			{
				double moveLength = (end - start).Length;

				if (moveLength > .1) // we get truncation errors from the slice engine when the length is very small, so don't do them
				{
					double area = extrusionVolumeMm3 / moveLength;
					width = area / layerHeight;
				}
			}

			return width;
		}

		public override void CreateRender3DData(VectorPOD<ColorVertexData> colorVertexData, VectorPOD<int> indexData, GCodeRenderInfo renderInfo)
		{
			if ((renderInfo.CurrentRenderType & RenderType.Extrusions) == RenderType.Extrusions)
			{
				Vector3Float start = this.GetStart(renderInfo);
				Vector3Float end = this.GetEnd(renderInfo);
				double radius = GetRadius(renderInfo.CurrentRenderType);

				Color lineColor;

				if (renderInfo.CurrentRenderType.HasFlag(RenderType.SpeedColors))
				{
					lineColor = color;
				}
				else if (renderInfo.CurrentRenderType.HasFlag(RenderType.GrayColors))
				{
					lineColor = ActiveTheme.Instance.IsDarkTheme ? Color.DarkGray : Color.Gray;
				}
				else
				{
					lineColor = renderInfo.GetMaterialColor(extruderIndex);
				}

				CreateCylinder(colorVertexData, indexData, new Vector3(start), new Vector3(end), radius, 6, lineColor, layerHeight);
			}
		}

		public override void Render(Graphics2D graphics2D, GCodeRenderInfo renderInfo)
		{
			if (renderInfo.CurrentRenderType.HasFlag(RenderType.Extrusions))
			{
				double extrusionLineWidths = GetExtrusionWidth(renderInfo.CurrentRenderType) * 2 * renderInfo.LayerScale;

				Color extrusionColor = Color.Black;

				if (renderInfo.CurrentRenderType.HasFlag(RenderType.SpeedColors))
				{
					extrusionColor = color;
				}
				else if (renderInfo.CurrentRenderType.HasFlag(RenderType.GrayColors))
				{
					extrusionColor = Color.Gray;
				}
				else
				{
					extrusionColor = renderInfo.GetMaterialColor(extruderIndex);
				}

				if (renderInfo.CurrentRenderType.HasFlag(RenderType.TransparentExtrusion))
				{
					extrusionColor = new Color(extrusionColor, 200);
				}

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

					graphics2DGl.DrawAALineRounded(start, end, extrusionLineWidths / 2, extrusionColor);
				}
				else
				{
					VertexStorage pathStorage = new VertexStorage();
					VertexSourceApplyTransform transformedPathStorage = new VertexSourceApplyTransform(pathStorage, renderInfo.Transform);
					Stroke stroke = new Stroke(transformedPathStorage, extrusionLineWidths / 2);

					stroke.line_cap(LineCap.Round);
					stroke.line_join(LineJoin.Round);

					Vector3Float start = this.GetStart(renderInfo);
					Vector3Float end = this.GetEnd(renderInfo);

					pathStorage.Add(start.x, start.y, ShapePath.FlagsAndCommand.CommandMoveTo);
					pathStorage.Add(end.x, end.y, ShapePath.FlagsAndCommand.CommandLineTo);

					graphics2D.Render(stroke, 0, extrusionColor);
				}
			}
		}
	}
}