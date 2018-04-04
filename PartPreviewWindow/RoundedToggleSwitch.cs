/*
Copyright (c) 2018, John Lewin
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
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class RoundedToggleSwitch : GuiWidget, ICheckbox
	{
		private ThemeConfig theme;

		private Color inactiveBarColor;
		private Color activeBarColor;

		private bool mouseIsDown;

		public bool Checked { get; set; }

		public event EventHandler CheckedStateChanged;

		public RoundedToggleSwitch(ThemeConfig theme)
		{
			this.theme = theme;
			//this.DoubleBuffer = true;
			inactiveBarColor = theme.ResolveColor(theme.ActiveTabColor, theme.SlightShade);
			activeBarColor = new Color(theme.Colors.PrimaryAccentColor, 100);

			this.MinimumSize = new Vector2(50, theme.ButtonHeight);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			mouseIsDown = true;
			base.OnMouseDown(mouseEvent);

			this.Invalidate();
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			mouseIsDown = false;
			base.OnMouseUp(mouseEvent);

			this.Invalidate();
		}


		public override void OnClick(MouseEventArgs mouseEvent)
		{
			base.OnClick(mouseEvent);

			bool newValue = !this.Checked;

			bool checkStateChanged = (newValue != this.Checked);

			this.Checked = newValue;

			// After setting CheckedState, fire event if different
			if (checkStateChanged)
			{
				OnCheckStateChanged();
				this.Invalidate();
			}
		}

		public virtual void OnCheckStateChanged()
		{
			CheckedStateChanged?.Invoke(this, null);
		}

		public bool ShowBubble { get; set; }

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			var center = this.LocalBounds.Center;

			var toggleRadius = 10;
			var bubbleRadius = toggleRadius * 1.7;

			var halfBarHeight = 14 / 2;
			var halfBarWidth = 36 / 2;

			//var barRect = new RectangleDouble(
			//	new Vector2(26, 0),
			//	new Vector2(0, 14));

			var left = center.X - halfBarWidth;
			var right = center.X + halfBarWidth;

			Color barColor = (this.Checked) ? activeBarColor : inactiveBarColor;

			if (mouseIsDown)
			{
				if (this.ShowBubble)
				{
					graphics2D.Render(
						new RoundedRect(
							new RectangleDouble(
								left,
								center.Y - halfBarHeight,
								right,
								center.Y + halfBarHeight),
							halfBarHeight),
						barColor);

					graphics2D.Circle(
						new Vector2(
							(this.Checked) ? right : left,
							center.Y),
						bubbleRadius,
						new Color(this.Checked ? theme.Colors.PrimaryAccentColor : Color.Gray, 80));
				}
				else
				{
					barColor = (this.Checked) ? Color.Gray : theme.Colors.PrimaryAccentColor;
				}
			}

			// Draw bar
			graphics2D.Render(
				new RoundedRect(
					new RectangleDouble(
						left,
						center.Y - halfBarHeight,
						right,
						center.Y + halfBarHeight),
					halfBarHeight),
				barColor);

			// Draw toggle circle
			var toggleColor = (this.Checked) ? theme.Colors.PrimaryAccentColor : Color.Gray;
			graphics2D.Circle(
				new Vector2(
					(this.Checked) ? right - halfBarHeight : left + halfBarHeight,
					center.Y),
				toggleRadius,
				toggleColor);
		}
	}
}