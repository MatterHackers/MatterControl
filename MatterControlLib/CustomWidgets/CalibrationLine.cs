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
		private bool _isActive;

		static CalibrationLine()
		{
			CalibrationLine.CreateGlyphs();
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

		public bool IsActive
		{
			get => _isActive;
			set
			{
				if (_isActive != value)
				{
					_isActive = value;
					this.Invalidate();
				}
			}
		}

		public bool IsNegative { get; set; }

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
			Color lineColor = this.IsActive ? theme.PrimaryAccentColor : theme.TextColor;

			var centerX = this.LocalBounds.XCenter + .5;
			var centerY = this.LocalBounds.YCenter - .5;

			var start = new Vector2(centerX, (glyph == null) ? 20 : (this.IsNegative) ? 6 : 9 );
			var end = new Vector2(centerX, this.LocalBounds.Height);

			if (!verticalLine)
			{
				start = new Vector2(0, centerY);
				end = new Vector2(this.LocalBounds.Width - ((glyph == null) ? 20 : (this.IsNegative) ? 6 : 9), centerY);
			}

			graphics2D.Line(start, end, lineColor, 1);

			// Draw line end
			if (glyph != null)
			{
				int offset = IsNegative ? 18 : 11;

				graphics2D.Render(
					glyph,
					verticalLine ? new Vector2(centerX, offset) : new Vector2(this.Width - offset, centerY),
					lineColor);
			}

			base.OnDraw(graphics2D);
		}

		private static void CreateGlyphs()
		{
			Glyphs = new Dictionary<int, IVertexSource>();

			var half = glyphSize / 2;

			var triangle = new VertexStorage();
			triangle.MoveTo(half, glyphSize);
			triangle.LineTo(0, 0);
			triangle.LineTo(glyphSize, 0);
			triangle.LineTo(half, glyphSize);
			triangle.ClosePolygon();

			var square = new VertexStorage();
			square.MoveTo(half, glyphSize);
			square.LineTo(0, glyphSize);
			square.LineTo(0, 0);
			square.LineTo(glyphSize, 0);
			square.LineTo(glyphSize, glyphSize);
			square.LineTo(half, glyphSize);
			square.ClosePolygon();

			var diamond = new VertexStorage();
			diamond.MoveTo(half, glyphSize);
			diamond.LineTo(0, half);
			diamond.LineTo(half, 0);
			diamond.LineTo(glyphSize, half);
			diamond.LineTo(half, glyphSize);
			diamond.ClosePolygon();

			var circle = new Ellipse(Vector2.Zero, half).Rotate(90, AngleType.Degrees).Translate(half, half);

			var center = new VertexStorage();
			center.MoveTo(half, glyphSize);
			center.LineTo(0, 0);
			center.LineTo(half, glyphSize - 4);
			center.LineTo(glyphSize, 0);
			center.LineTo(half, glyphSize);
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
