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

using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class CalibrationLine : GuiWidget
	{
		public static Dictionary<int, IVertexSource> Glyphs { get; private set; }
		private static int glyphSize = 8;

		private bool mouseInBounds;
		private bool verticalLine;

		private ThemeConfig theme;
		private IVertexSource glyph = null;

		static CalibrationLine()
		{
			int glyphCenter = glyphSize / 2;
			CalibrationLine.CreateGlyphs(glyphCenter);
		}

		public CalibrationLine(FlowDirection parentDirection, int glyphIndex, ThemeConfig theme)
		{
			if (parentDirection == FlowDirection.LeftToRight)
			{
				this.Width = 8;
				this.HAnchor = HAnchor.Absolute;
				this.VAnchor = VAnchor.Stretch;
			}
			else
			{
				this.Height = 8;
				this.HAnchor = HAnchor.Stretch;
				this.VAnchor = VAnchor.Absolute;
			}

			verticalLine = parentDirection == FlowDirection.LeftToRight;

			if (Glyphs.TryGetValue(glyphIndex, out IVertexSource glyph))
			{
				if (!verticalLine)
				{
					// Rotate glyph to match horizontal line
					glyph = new VertexSourceApplyTransform(glyph, Affine.NewRotation(MathHelper.DegreesToRadians(90)));
				}

				this.glyph = glyph;
			}

			this.theme = theme;
		}

		public bool IsNegative { get; internal set; }

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			mouseInBounds = true;
			this.Invalidate();

			base.OnMouseEnterBounds(mouseEvent);
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			mouseInBounds = false;
			this.Invalidate();

			base.OnMouseLeaveBounds(mouseEvent);
		}

		public override Color BackgroundColor
		{
			get => mouseInBounds ? theme.AccentMimimalOverlay : base.BackgroundColor;
			set => base.BackgroundColor = value;
		}

		public int OffsetIndex { get; set; }

		public override void OnDraw(Graphics2D graphics2D)
		{
			var centerX = this.LocalBounds.XCenter + .5;
			var centerY = this.LocalBounds.YCenter - .5;

			var start = new Vector2(centerX, (glyph == null) ? 20 : 9);
			var end = new Vector2(centerX, this.LocalBounds.Height);

			if (!verticalLine)
			{
				start = new Vector2(0, centerY);
				end = new Vector2(this.LocalBounds.Width - ((glyph == null) ? 20 : 9), centerY);
			}

			graphics2D.Line(start, end, theme.TextColor, 1);

			// Draw line end
			if (glyph != null)
			{
				graphics2D.Render(
					glyph,
					verticalLine ? new Vector2(centerX, 11) : new Vector2(this.Width - 11, centerY),
					theme.TextColor);
			}

			// Draw negative adornment after glyphs 
			if (glyph != null
				&& this.IsNegative)
			{
				graphics2D.Line(
					verticalLine ? new Vector2(centerX, 0) : new Vector2(this.Width - 5, centerY),
					verticalLine ? new Vector2(centerX, 5) : new Vector2(this.Width, centerY),
					theme.TextColor,
					1);
			}

			base.OnDraw(graphics2D);
		}

		private static void CreateGlyphs(int glyphCenter)
		{
			Glyphs = new Dictionary<int, IVertexSource>();

			var half = -(glyphSize / 2);

			var triangle = new VertexStorage();
			triangle.MoveTo(0, 0);
			triangle.LineTo(glyphSize, 0);
			triangle.LineTo(glyphSize / 2, glyphSize);
			triangle.ClosePolygon();
			
			//triangle.ClosePolygon();

			var square = new VertexStorage();
			square.MoveTo(0, 0);
			square.LineTo(glyphSize, 0);
			square.LineTo(glyphSize, glyphSize);
			square.LineTo(0, glyphSize);
			square.ClosePolygon();

			var diamond = new VertexStorage();
			diamond.MoveTo(glyphCenter, 0);
			diamond.LineTo(glyphSize, glyphCenter);
			diamond.LineTo(glyphCenter, glyphSize);
			diamond.LineTo(0, glyphCenter);
			diamond.ClosePolygon();

			var circle = new Ellipse(new Vector2(glyphCenter, glyphCenter), glyphCenter);

			var center = new VertexStorage();
			center.MoveTo(0, 0);
			center.LineTo(glyphCenter, glyphSize);
			center.LineTo(glyphSize, 0);
			center.LineTo(glyphCenter, glyphSize - 4);
			center.LineTo(0, 0);
			center.ClosePolygon();

			var transform = Affine.NewTranslation(-glyphSize / 2, -glyphSize);
			Glyphs.Add(0, new VertexSourceApplyTransform(triangle, transform));
			Glyphs.Add(5, new VertexSourceApplyTransform(diamond, transform));
			Glyphs.Add(10, new VertexSourceApplyTransform(square, transform));
			Glyphs.Add(15, new VertexSourceApplyTransform(circle, transform));

			Glyphs.Add(20, new VertexSourceApplyTransform(center, transform));

			Glyphs.Add(25, new VertexSourceApplyTransform(circle, transform));
			Glyphs.Add(30, new VertexSourceApplyTransform(square, transform));
			Glyphs.Add(35, new VertexSourceApplyTransform(diamond, transform));
			Glyphs.Add(40, new VertexSourceApplyTransform(triangle, transform));
		}
	}
}
