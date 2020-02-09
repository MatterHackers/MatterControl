/*
Copyright (c) 2019, John Lewin
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

using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ValueTag : Popover
	{
		private IVertexSource rightShape;
		private Stroke rightStroke;
		private Color rightBackground;

		public ValueTag(string title, string value, BorderDouble padding, int notchSize, int p2, bool autoBorderColor = true)
			: this (title, value, padding, notchSize, p2, AppContext.Theme, autoBorderColor)
		{
		}

		public ValueTag(string title, string value, BorderDouble padding, int notchSize, int p2, ThemeConfig theme, bool autoBorderColor = true)
			: base(ArrowDirection.Right, padding, notchSize, p2, autoBorderColor)
		{
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;
			this.FlowDirection = FlowDirection.LeftToRight;
			this.TagColor = theme.ResolveColor(AppContext.Theme.BackgroundColor, theme.AccentMimimalOverlay.WithAlpha(50));

			this.AddChild(
				new TextWidget(title, pointSize: theme.DefaultFontSize - 1, textColor: theme.TextColor)
				{
					Margin = new BorderDouble(right: 12)
				});

			this.AddChild(
				new TextWidget(value, pointSize: theme.DefaultFontSize - 1, textColor: theme.TextColor)
				{
					Margin = new BorderDouble(left: 5)
				});
		}

		public override Color TagColor
		{
			get => base.TagColor;
			set
			{
				base.TagColor = value;
				rightBackground = value.AdjustLightness(.85).ToColor();
			}
		}

		protected override void RebuildShape()
		{
			if (this.Children.Count() < 2)
			{
				return;
			}

			var firstChild = this.Children.FirstOrDefault();
			var bounds = this.LocalBounds;

			// Get the first child bounds in our coords
			var firstChildBounds =	firstChild.TransformToParentSpace(this, firstChild.LocalBounds);

			// Build region inclusive of our padding
			firstChildBounds = new RectangleDouble(bounds.Left, bounds.Bottom, bounds.Left + firstChildBounds.Right + firstChild.Margin.Width, bounds.Top);

			var secondChildBounds = firstChildBounds;
			secondChildBounds.Left = firstChildBounds.Right - 10;
			secondChildBounds.Right = bounds.Right;

			// Build label region
			tabShape = Popover.GetShape(ArrowDirection.Right, firstChildBounds, this.NotchSize, this.ArrowOffset);
			tabStroke = new Stroke(tabShape);

			// Build details region
			// TODO: Grow second child bounds so that it grows into left region and gets its borders clipped
			rightShape = Popover.GetShape(ArrowDirection.None, secondChildBounds, this.NotchSize, this.ArrowOffset);
			rightStroke = new Stroke(rightShape);
		}

		public override void OnDrawBackground(Graphics2D graphics2D)
		{
			if (rightShape != null)
			{
				graphics2D.Render(rightShape, rightBackground);
			}

			if (this.BorderColor != Color.Transparent)
			{
				graphics2D.Render(rightStroke, this.BorderColor);
			}

			base.OnDrawBackground(graphics2D);
		}
	}
}
