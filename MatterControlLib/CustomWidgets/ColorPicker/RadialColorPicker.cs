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

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets.ColorPicker
{
	public class RadialColorPicker : GuiWidget
	{
		private double colorAngle = 0;
		private Vector2 unitTrianglePosition = new Vector2(1, .5);
		private float alpha;
		private Color downColor;

		private enum DownState
		{
			None,
			OnRing,
			OnTriangle,
		}

		private DownState downState = DownState.None;

		public RadialColorPicker()
		{
			BackgroundColor = Color.White;

			this.Width = 100;
			this.Height = 100;

			if (!TriangleToWidgetTransform(0).Transform(new Vector2(1, .5)).Equals(new Vector2(88, 50), .01))
			{
				// throw new Exception("Incorrect transform");
			}

			if (!TriangleToWidgetTransform(0).InverseTransform(new Vector2(88, 50)).Equals(new Vector2(1, .5), .01))
			{
				// throw new Exception("Incorrect transform");
			}

			if (!TriangleToWidgetTransform(0).Transform(new Vector2(0, .5)).Equals(new Vector2(23.13, 50), .01))
			{
				// throw new Exception("Incorrect transform");
			}
		}

		public event EventHandler IncrementalColorChanged;


		public event EventHandler SelectedColorChanged;

		public double RingWidth { get => Width / 10; }

		public void SetColorWithoutChangeEvent(Color color)
		{
			color.ToColorF().GetHSL(out double h, out double s, out double l);
			// if the color is not white or black, set the angle (otherwise leave it where it is)
			if ((color.red != color.green || color.green != color.blue || color.blue != 0)
				&& (color.red != color.green || color.green != color.blue || color.blue != 255))
			{
				colorAngle = h * MathHelper.Tau;
			}
			unitTrianglePosition.X = s;
			unitTrianglePosition.Y = l;
			alpha = color.Alpha0To1;

			CLampTrianglePosition(ref unitTrianglePosition);
			Invalidate();
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			if (downState != DownState.None
				&& keyEvent.KeyCode == Keys.Escape)
			{
				downState = DownState.None;
				SelectedColor = downColor;
			}

			base.OnKeyDown(keyEvent);
		}

		public Color SelectedColor
		{
			get
			{
				return ColorF.FromHSL(colorAngle / MathHelper.Tau, unitTrianglePosition.X, unitTrianglePosition.Y, alpha).ToColor();
			}

			set
			{
				if (value != SelectedColor)
				{
					SetColorWithoutChangeEvent(value);

					SelectedColorChanged?.Invoke(this, null);
				}
			}
		}

		public Color SelectedHueColor
		{
			get
			{
				return ColorF.FromHSL(colorAngle / MathHelper.Tau, 1, .5).ToColor();
			}

			set
			{
				value.ToColorF().GetHSL(out double h, out _, out _);
				colorAngle = h * MathHelper.Tau;
			}
		}

		private double InnerRadius
		{
			get
			{
				return RingRadius - RingWidth / 2;
			}
		}

		private double RingRadius
		{
			get
			{
				return Width / 2 - RingWidth / 2 - 2;
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var center = new Vector2(Width / 2, Height / 2);

			// draw the big outside ring (color part)
			DrawColorRing(graphics2D, RingRadius, RingWidth);

			// draw the inner triangle (color part)
			DrawColorTriangle(graphics2D, InnerRadius, SelectedHueColor);

			// draw the big ring outline
			graphics2D.Ring(center, RingRadius + RingWidth / 2, 1, Color.Black);
			graphics2D.Ring(center, RingRadius - RingWidth / 2, 1, Color.Black);

			// draw the triangle outline
			graphics2D.Line(GetTrianglePoint(0, InnerRadius, colorAngle), GetTrianglePoint(1, InnerRadius, colorAngle), Color.Black);
			graphics2D.Line(GetTrianglePoint(1, InnerRadius, colorAngle), GetTrianglePoint(2, InnerRadius, colorAngle), Color.Black);
			graphics2D.Line(GetTrianglePoint(2, InnerRadius, colorAngle), GetTrianglePoint(0, InnerRadius, colorAngle), Color.Black);

			// draw the color circle on the triangle
			var triangleColorCenter = TriangleToWidgetTransform(colorAngle).Transform(unitTrianglePosition);
			graphics2D.Circle(triangleColorCenter, RingWidth / 2 - 2, new Color(SelectedColor, 255));
			graphics2D.Ring(triangleColorCenter, RingWidth / 2 - 2, 2, Color.White);

			// draw the color circle on the ring
			var ringColorCenter = center + Vector2.Rotate(new Vector2(RingRadius, 0), colorAngle);
			graphics2D.Circle(ringColorCenter,
				RingWidth / 2 - 2,
				SelectedHueColor);
			graphics2D.Ring(ringColorCenter,
				RingWidth / 2 - 2,
				2,
				Color.White);

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			downColor = SelectedColor;

			var center = new Vector2(Width / 2, Height / 2);
			var direction = mouseEvent.Position - center;
			var startColor = SelectedColor;

			if (mouseEvent.Button == MouseButtons.Left)
			{
				if (direction.Length > RingRadius - RingWidth / 2
				&& direction.Length < RingRadius + RingWidth / 2)
				{
					downState = DownState.OnRing;

					colorAngle = Math.Atan2(direction.Y, direction.X);
					if (colorAngle < 0)
					{
						colorAngle += MathHelper.Tau;
					}

					Invalidate();
				}
				else
				{
					var (inside, position) = WidgetToUnitTriangle(mouseEvent.Position);

					if (inside)
					{
						downState = DownState.OnTriangle;
						unitTrianglePosition = position;
					}

					Invalidate();
				}
			}

			if (startColor != SelectedColor)
			{
				IncrementalColorChanged?.Invoke(this, null);
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			var startColor = SelectedColor;

			switch (downState)
			{
				case DownState.OnRing:
					var center = new Vector2(Width / 2, Height / 2);

					var direction = mouseEvent.Position - center;
					colorAngle = Math.Atan2(direction.Y, direction.X);
					if (colorAngle < 0)
					{
						colorAngle += MathHelper.Tau;
					}

					Invalidate();
					break;

				case DownState.OnTriangle:
					unitTrianglePosition = WidgetToUnitTriangle(mouseEvent.Position).position;
					Invalidate();
					break;
			}

			if (startColor != SelectedColor)
			{
				IncrementalColorChanged?.Invoke(this, null);
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			downState = DownState.None;

			if (downColor != SelectedColor)
			{
				SelectedColorChanged?.Invoke(this, null);
			}

			base.OnMouseUp(mouseEvent);
		}

		private void DrawColorRing(Graphics2D graphics2D, double radius, double width)
		{
			if (graphics2D is Graphics2DOpenGL graphicsGL)
			{
				graphicsGL.PushOrthoProjection();

				GL.Disable(EnableCap.Texture2D);
				GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
				GL.Enable(EnableCap.Blend);

				var outer = radius + width / 2;
				var inner = radius - width / 2;
				GL.Begin(BeginMode.TriangleStrip);

				for (int i = 0; i <= 360; i++)
				{
					var color = ColorF.FromHSL(i / 360.0, 1, .5);
					var angle = MathHelper.DegreesToRadians(i);

					GL.Color4(color.Red0To255, color.Green0To255, color.Blue0To255, color.Alpha0To255);
					GL.Vertex2(GetAtAngle(angle, outer, true));
					GL.Vertex2(GetAtAngle(angle, inner, true));
				}

				GL.End();

				graphicsGL.PopOrthoProjection();
			}
		}

		private void DrawColorTriangle(Graphics2D graphics2D, double radius, Color color)
		{
			if (graphics2D is Graphics2DOpenGL graphicsGL)
			{
				graphicsGL.PushOrthoProjection();

				GL.Disable(EnableCap.Texture2D);
				GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
				GL.Enable(EnableCap.Blend);

				GL.Begin(BeginMode.Triangles);
				GL.Color4(color.Red0To255, color.Green0To255, color.Blue0To255, color.Alpha0To255);
				GL.Vertex2(GetTrianglePoint(0, radius, colorAngle, true));
				GL.Color4(Color.White);
				GL.Vertex2(GetTrianglePoint(1, radius, colorAngle, true));
				GL.Color4(Color.Black);
				GL.Vertex2(GetTrianglePoint(2, radius, colorAngle, true));

				GL.End();

				graphicsGL.PopOrthoProjection();
			}
		}

		private Vector2 GetAtAngle(double angle, double radius, bool screenSpace)
		{
			var start = new Vector2(radius, 0);

			var position = default(Vector2);
			if (screenSpace)
			{
				position = this.TransformToScreenSpace(this.Position);
			}

			var center = new Vector2(Width / 2, Height / 2);
			return position + center + Vector2.Rotate(start, angle);
		}

		private Vector2 GetTrianglePoint(int index, double radius, double pontingAngle, bool screenSpace = false)
		{
			switch (index)
			{
				case 0:
					return GetAtAngle(pontingAngle, radius, screenSpace);

				case 1:
					return GetAtAngle(pontingAngle + MathHelper.DegreesToRadians(120), radius, screenSpace);

				case 2:
					return GetAtAngle(pontingAngle + MathHelper.DegreesToRadians(240), radius, screenSpace);
			}

			return Vector2.Zero;
		}

		private Affine TriangleToWidgetTransform(double angle)
		{
			var center = new Vector2(Width / 2, Height / 2);
			var leftSize = .5;
			var sizeToTop = Math.Sin(MathHelper.DegreesToRadians(60));

			Affine total = Affine.NewIdentity();
			// scale to -1 to 1 coordinates
			total *= Affine.NewScaling(1 + leftSize, sizeToTop * 2);
			// center
			total *= Affine.NewTranslation(-leftSize, -sizeToTop);
			// rotate to correct color
			total *= Affine.NewRotation(angle);
			// scale to radius
			total *= Affine.NewScaling(InnerRadius);
			// move to center
			total *= Affine.NewTranslation(center);
			return total;
		}

		private (bool inside, Vector2 position) WidgetToUnitTriangle(Vector2 widgetPosition)
		{
			var trianglePosition = TriangleToWidgetTransform(colorAngle)
				.InverseTransform(widgetPosition);

			bool changed = CLampTrianglePosition(ref trianglePosition);

			return (!changed, trianglePosition);
		}

		private static bool CLampTrianglePosition(ref Vector2 trianglePosition)
		{
			bool changed = false;
			trianglePosition.X = agg_basics.Clamp(trianglePosition.X, 0, 1, ref changed);
			trianglePosition.Y = agg_basics.Clamp(trianglePosition.Y, 0, 1, ref changed);

			trianglePosition.Y = agg_basics.Clamp(trianglePosition.Y,
				.5 - (1 - trianglePosition.X) / 2,
				.5 + (1 - trianglePosition.X) / 2,
				ref changed);

			return changed;
		}
	}
}