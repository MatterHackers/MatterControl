/*
Copyright (c) 2018, Lars Brubaker
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
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl.CustomWidgets.ColorPicker
{
	public class RadialColorPicker : GuiWidget
	{
		private double colorAngle = 0;

		private bool mouseDownOnRing;

		public RadialColorPicker()
		{
			BackgroundColor = Color.White;
		}

		public Color SelectedHueColor
		{
			get
			{
				return ColorF.FromHSL(colorAngle / MathHelper.Tau, 1, .5).ToColor();
			}
		}

		public Color SelectedColor
		{
			get
			{
				return ColorF.FromHSL(colorAngle / MathHelper.Tau, 1, .5).ToColor();
			}
		}

		public double RingWidth { get => Width / 10; }
		double RingRadius
		{
			get
			{
				return Width / 2 - RingWidth / 2 - 2;
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var center = new Vector2(Width / 2, Height / 2);
			var radius = new Vector2(RingRadius, RingRadius);

			// draw the big outside ring (color part)
			DrawColorRing(graphics2D, center, RingRadius, RingWidth);

			// draw the inner triangle (color part)
			DrawColorTriangle(graphics2D, center, RingRadius - RingWidth / 2, colorAngle,
				SelectedHueColor);

			// draw the big ring outline
			graphics2D.Ring(center, RingRadius + RingWidth / 2, 1, Color.Black);
			graphics2D.Ring(center, RingRadius - RingWidth / 2, 1, Color.Black);

			// draw the triangle outline

			// draw the color circle on the triangle

			// draw the color circle on the ring
			var ringColorCenter = center + Vector2.Rotate(new Vector2(RingRadius, 0), colorAngle);
			graphics2D.Circle(ringColorCenter,
				RingWidth / 2 - 4,
				SelectedHueColor);
			graphics2D.Ring(ringColorCenter,
				RingWidth / 2 - 2,
				2,
				Color.White);

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			var center = new Vector2(Width / 2, Height / 2);
			var direction = mouseEvent.Position - center;

			if (mouseEvent.Button == MouseButtons.Left
				&& direction.Length > RingRadius - RingWidth
				&& direction.Length < RingRadius + RingWidth)
			{
				mouseDownOnRing = true;

				colorAngle = Math.Atan2(direction.Y, direction.X);
				if (colorAngle < 0)
				{
					colorAngle += MathHelper.Tau;
				}
				Invalidate();
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (mouseDownOnRing)
			{
				var center = new Vector2(Width / 2, Height / 2);

				var direction = mouseEvent.Position - center;
				colorAngle = Math.Atan2(direction.Y, direction.X);
				if (colorAngle < 0)
				{
					colorAngle += MathHelper.Tau;
				}
			}
			Invalidate();

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			mouseDownOnRing = false;

			base.OnMouseUp(mouseEvent);
		}

		private void DrawColorRing(Graphics2D graphics2D, Vector2 center, double radius, double width)
		{
			if (graphics2D is Graphics2DOpenGL graphicsGL)
			{
				graphicsGL.PushOrthoProjection();

				GL.Disable(EnableCap.Texture2D);
				GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
				GL.Enable(EnableCap.Blend);

				var outer = new Vector2(radius + width / 2, 0);
				var inner = new Vector2(radius - width / 2, 0);
				GL.Begin(BeginMode.TriangleStrip);

				for (int i = 0; i <= 360; i++)
				{
					var color = ColorF.FromHSL(i / 360.0, 1, .5);
					var angle = MathHelper.DegreesToRadians(i);

					GL.Color4(color.Red0To255, color.Green0To255, color.Blue0To255, color.Alpha0To255);
					GL.Vertex2(center + Vector2.Rotate(outer, angle));
					GL.Vertex2(center + Vector2.Rotate(inner, angle));
				}

				GL.End();

				graphicsGL.PopOrthoProjection();
			}
		}

		private void DrawColorTriangle(Graphics2D graphics2D, Vector2 center, double radius, double angle, Color color)
		{
			if (graphics2D is Graphics2DOpenGL graphicsGL)
			{
				graphicsGL.PushOrthoProjection();

				GL.Disable(EnableCap.Texture2D);
				GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
				GL.Enable(EnableCap.Blend);

				var start = new Vector2(radius, 0);
				GL.Begin(BeginMode.Triangles);
				GL.Color4(color.Red0To255, color.Green0To255, color.Blue0To255, color.Alpha0To255);
				GL.Vertex2(center + Vector2.Rotate(start, angle));
				GL.Color4(Color.Black);
				GL.Vertex2(center + Vector2.Rotate(start, angle + MathHelper.DegreesToRadians(120)));
				GL.Color4(Color.White);
				GL.Vertex2(center + Vector2.Rotate(start, angle + MathHelper.DegreesToRadians(240)));

				GL.End();

				graphicsGL.PopOrthoProjection();
			}
		}
	}

	public static class Graphics2DOverrides
	{
		public static void Ring(this Graphics2D graphics2D, Vector2 center, double radius, double width, Color color)
		{
			var ring = new Ellipse(center, radius);
			var ringStroke = new Stroke(ring, width);
			graphics2D.Render(ringStroke, color);
		}
	}
}