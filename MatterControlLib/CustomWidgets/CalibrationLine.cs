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
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class CalibrationLine : GuiWidget
	{
		private bool mouseInBounds;
		private ThemeConfig theme;
		private Dictionary<int, IVertexSource> glyphs;
		private int glyphSize = 8;

		public CalibrationLine(ThemeConfig theme)
		{
			this.theme = theme;

			int glyphCenter = glyphSize / 2;

			this.CreateGlyphs(glyphCenter);
		}

		public int GlyphIndex { get; set; } = -1;

		public bool IsNegative { get; internal set; }

		public bool Vertical { get; set; } = true;

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

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (this.Vertical)
			{
				int centerX = (int)this.LocalBounds.XCenter;

				if (this.GlyphIndex == -1)
				{
					// Draw primary line
					graphics2D.Line(
						new Vector2(centerX + .5, (this.GlyphIndex == -1) ? 20 : 9 /*+ .5*/),
						new Vector2(centerX + .5, this.LocalBounds.Height /*+ .5*/),
						theme.TextColor,
						1);
				}
				else
				{
					// Draw primary line
					graphics2D.Line(
						new Vector2(centerX, (this.GlyphIndex == -1) ? 20 : 9 /*+ .5*/),
						new Vector2(centerX, this.LocalBounds.Height /*+ .5*/),
						theme.TextColor,
						2);
				}

				// Draw line end
				if (this.GlyphIndex != -1
					&& glyphs.TryGetValue(this.GlyphIndex, out IVertexSource vertexSource))
				{
					graphics2D.Render(
						vertexSource,
						new Vector2((int)(this.LocalBounds.XCenter - (glyphSize / 2)), 5),
						theme.TextColor);
				}

				// Draw negative adornment below glyphs 
				if (this.GlyphIndex != -1
					&& this.IsNegative)
				{
					graphics2D.Line(
						new Vector2(this.LocalBounds.XCenter, 5),
						new Vector2(this.LocalBounds.XCenter, 0),
						theme.TextColor,
						1);
				}
			}

			base.OnDraw(graphics2D);
		}

		private void CreateGlyphs(int glyphCenter)
		{
			glyphs = new Dictionary<int, IVertexSource>();

			var triangle = new VertexStorage();
			triangle.LineTo(glyphSize, 0);
			triangle.LineTo(glyphSize / 2, glyphSize);
			triangle.LineTo(0, 0);
			//triangle.ClosePolygon();

			var square = new VertexStorage();
			square.LineTo(glyphSize, 0);
			square.LineTo(glyphSize, glyphSize);
			square.LineTo(0, glyphSize);
			square.LineTo(0, 0);

			var diamond = new VertexStorage();
			diamond.MoveTo(glyphCenter, 0);
			diamond.LineTo(glyphSize, glyphCenter);
			diamond.LineTo(glyphCenter, glyphSize);
			diamond.LineTo(0, glyphCenter);
			diamond.MoveTo(glyphCenter, 0);

			var circle = new Ellipse(new Vector2(glyphCenter, glyphCenter), glyphCenter);

			var center = new VertexStorage();
			center.MoveTo(0, 0);
			center.LineTo(glyphCenter, glyphSize);
			center.LineTo(glyphSize, 0);
			center.LineTo(glyphCenter, glyphSize - 4);
			center.LineTo(0, 0);
			center.ClosePolygon();

			glyphs.Add(0, triangle);
			glyphs.Add(5, diamond);
			glyphs.Add(10, square);
			glyphs.Add(15, circle);

			glyphs.Add(20, center);

			glyphs.Add(25, circle);
			glyphs.Add(30, square);
			glyphs.Add(35, diamond);
			glyphs.Add(40, triangle);
		}
	}
}
