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

		public Vector3Float Start => start;

		public Vector3Float End => end;

		public RenderFeatureTravel(int instructionIndex, Vector3 start, Vector3 end, int toolIndex, double travelSpeed)
			: base(instructionIndex, toolIndex)
		{
			this.toolIndex = toolIndex;
			this.start = new Vector3Float(start);
			this.end = new Vector3Float(end);
			this.travelSpeed = (float)travelSpeed;
		}

		public override void CreateRender3DData(VectorPOD<ColorVertexData> colorVertexData, VectorPOD<int> indexData, GCodeRenderInfo renderInfo)
		{
			if ((renderInfo.CurrentRenderType & RenderType.Moves) == RenderType.Moves)
			{
				CreateCylinder(colorVertexData, indexData, new Vector3(start), new Vector3(end), .1, 6, GCodeRenderer.TravelColor, .2);
			}
		}

		protected void Render3DStartEndMarkers(Graphics2DOpenGL graphics2DGl, double radius, Vector2 startPoint, Vector2 endPoint)
		{
			graphics2DGl.DrawAACircle(startPoint, radius, RenderFeatureBase.StartColor);
			graphics2DGl.DrawAACircle(endPoint, radius, RenderFeatureBase.EndColor);
		}

		public override void Render(Graphics2D graphics2D, GCodeRenderInfo renderInfo, bool highlightFeature = false)
		{
			if ((renderInfo.CurrentRenderType & RenderType.Moves) == RenderType.Moves)
			{
				double movementLineWidth = 0.2 * renderInfo.LayerScale;
				Color movementColor = (highlightFeature) ? RenderFeatureBase.HighlightColor : new Color(10, 190, 15);

				if (graphics2D is Graphics2DOpenGL graphics2DGl)
				{
					// render using opengl
					var startPoint = new Vector2(start.X, start.Y);
					renderInfo.Transform.transform(ref startPoint);

					var endPoint = new Vector2(end.X, end.Y);
					renderInfo.Transform.transform(ref endPoint);

					if (renderInfo.CurrentRenderType.HasFlag(RenderType.TransparentExtrusion))
					{
						movementColor = new Color(movementColor, 200);
					}

					graphics2DGl.DrawAALineRounded(startPoint, endPoint, movementLineWidth, movementColor);
				}
				else
				{
					// render using agg
					var pathStorage = new VertexStorage();
					var transformedPathStorage = new VertexSourceApplyTransform(pathStorage, renderInfo.Transform);
					var stroke = new Stroke(transformedPathStorage, movementLineWidth)
					{
						LineCap = LineCap.Round,
						LineJoin = LineJoin.Round
					};

					pathStorage.Add(start.X, start.Y, ShapePath.FlagsAndCommand.MoveTo);
					if (end.X != start.X || end.Y != start.Y)
					{
						pathStorage.Add(end.X, end.Y, ShapePath.FlagsAndCommand.LineTo);
					}
					else
					{
						pathStorage.Add(end.X + .01, end.Y, ShapePath.FlagsAndCommand.LineTo);
					}

					graphics2D.Render(stroke, movementColor);
				}
			}
		}
	}
}