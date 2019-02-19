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

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl
{
	public class NozzleOffsetTemplateWidget : FlowLayoutWidget
	{
		private double _activeOffset;
		private ThemeConfig theme;

		public event EventHandler OffsetChanged;

		public NozzleOffsetTemplateWidget(double[] activeOffsets, FlowDirection direction, ThemeConfig theme)
			: base(direction)
		{
			this.theme = theme;

			if (direction == FlowDirection.LeftToRight)
			{
				this.HAnchor = HAnchor.Stretch;
				this.VAnchor = VAnchor.Absolute;
				this.Height = 110;
			}
			else
			{
				this.HAnchor = HAnchor.Absolute;
				this.VAnchor = VAnchor.Stretch;
				this.Width = 110;
			}

			for (var i = 0; i <= 40; i++)
			{
				var calibrationLine = new CalibrationLine(direction, (i % 5 == 0) ? i : -1, theme)
				{
					// Margin = 1,
					IsNegative = i < 20,
					OffsetIndex = i,
				};
				calibrationLine.Click += (s, e) =>
				{
					this.ActiveOffset = activeOffsets[calibrationLine.OffsetIndex] * -1;
				};
				this.AddChild(calibrationLine);

				// Add spacers to stretch to size
				if (i < 40)
				{
					if (this.FlowDirection == FlowDirection.LeftToRight)
					{
						this.AddChild(new HorizontalSpacer());
					}
					else
					{
						this.AddChild(new VerticalSpacer());
					}
				}
			}
		}

		public double ActiveOffset
		{
			get => _activeOffset;
			set
			{
				if (value != _activeOffset)
				{
					this.OffsetChanged?.Invoke(this, null);
				}
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			int strokeWidth = 3;

			RectangleDouble rect;

			if (this.FlowDirection == FlowDirection.LeftToRight)
			{
				rect = new RectangleDouble(0, 20, this.LocalBounds.Width, this.LocalBounds.Height);
			}
			else
			{
				rect = new RectangleDouble(0, 0, this.LocalBounds.Width - 20, this.LocalBounds.Height);
			}

			rect.Inflate(-1);

			var center = rect.Center;

			graphics2D.Rectangle(rect, theme.TextColor, strokeWidth);

			if (this.FlowDirection == FlowDirection.LeftToRight)
			{
				graphics2D.Line(rect.Left, center.Y, rect.Right, center.Y, theme.TextColor, strokeWidth);
			}
			else
			{
				graphics2D.Line(rect.XCenter, rect.Top, rect.XCenter, rect.Bottom, theme.TextColor, strokeWidth);
			}

			base.OnDraw(graphics2D);
		}
	}
}
