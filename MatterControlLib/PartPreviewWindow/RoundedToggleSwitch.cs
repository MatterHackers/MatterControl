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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using static MatterHackers.VectorMath.Easing;

namespace MatterHackers.MatterControl.CustomWidgets
{
	internal class ToggleSwitchAnimation : Animation
	{
		internal double animationRatio = 0;
		internal double finalRadius = 22 * GuiWidget.DeviceScale;

		public override bool OnUpdate(UpdateEvent updateEvent)
		{
			if (IsRunning)
			{
				animationRatio += 1.0 / 7.0;
			}

			if (animationRatio >= 1)
			{
				Stop();
			}

			return base.OnUpdate(updateEvent);
		}
	}

	public class RoundedToggleSwitch : GuiWidget, ICheckbox
	{
		private ThemeConfig theme;

		private Color inactiveBarColor;
		private Color activeBarColor;

		private bool mouseIsDown;
		private bool mouseInBounds = false;

		private double centerY;
		private double left;
		private double right;
		private RoundedRect backgroundBar;

		private double minWidth = 45 * DeviceScale;
		private double barHeight = 12.6 * DeviceScale;
		private double toggleRadius = 9 * DeviceScale;
		private double toggleRadiusPlusPadding = 10 * DeviceScale;

		private ToggleSwitchAnimation animation;

		private bool _checked;

		public bool Checked
		{
			get => _checked;
			set
			{
				if (_checked != value)
				{
					_checked = value;
					this.Invalidate();
				}
			}
		}

		public event EventHandler CheckedStateChanged;

		public RoundedToggleSwitch(ThemeConfig theme)
		{
			this.theme = theme;
			// this.DoubleBuffer = true;
			inactiveBarColor = theme.IsDarkTheme ? theme.Shade : theme.SlightShade;
			activeBarColor = new Color(theme.PrimaryAccentColor, theme.IsDarkTheme ? 100 : 70);

			this.MinimumSize = new Vector2(minWidth, theme.ButtonHeight);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			mouseIsDown = true;
			base.OnMouseDown(mouseEvent);

			// animation up
			animation = new ToggleSwitchAnimation()
			{
				DrawTarget = this,
				FramesPerSecond = 30
			};
			animation.Start();

			this.Parents<SystemWindow>().First().AfterDraw += RoundedToggleSwitch_AfterDraw;

			this.Invalidate();
		}

		private void RoundedToggleSwitch_AfterDraw(object sender, DrawEventArgs e)
		{
			var position = new Vector2(this.Checked ? LocalBounds.Right - toggleRadiusPlusPadding : toggleRadiusPlusPadding, centerY);
			position = this.TransformToScreenSpace(position);
			Color toggleColor = this.Checked ? theme.PrimaryAccentColor : Color.Gray;

			e.Graphics2D.Circle(position,
				animation.finalRadius * Quadratic.Out(animation.animationRatio),
				new Color(toggleColor, 50));

			if (animation.IsRunning == false && animation.animationRatio == 0)
			{
				((GuiWidget)sender).AfterDraw -= RoundedToggleSwitch_AfterDraw;
			}
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			this.Parents<SystemWindow>().First().AfterDraw -= RoundedToggleSwitch_AfterDraw;

			animation.Stop();
			animation.animationRatio = 0;
			mouseIsDown = false;
			base.OnMouseUp(mouseEvent);

			this.Invalidate();
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			var inBounds = this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y);

			if (inBounds != mouseInBounds)
			{
				mouseInBounds = inBounds;
				this.Invalidate();
			}

			base.OnMouseMove(mouseEvent);
		}

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			base.OnClick(mouseEvent);

			bool newValue = !this.Checked;

			bool checkStateChanged = newValue != this.Checked;

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

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			var position = this.Checked ? LocalBounds.Right - toggleRadiusPlusPadding : toggleRadiusPlusPadding;

			Color barColor = this.Checked ? activeBarColor : inactiveBarColor;
			Color toggleColor = this.Checked ? theme.PrimaryAccentColor : Color.Gray;

			if (mouseIsDown && mouseInBounds)
			{
				barColor = this.Checked ? inactiveBarColor : activeBarColor;
			}

			if (!Enabled)
			{
				graphics2D.Render(new Stroke(backgroundBar), inactiveBarColor);
				graphics2D.Circle(
					new Vector2(
						position,
						centerY),
					toggleRadius,
					inactiveBarColor);
			}
			else
			{
				// Draw bar
				graphics2D.Render(backgroundBar, barColor);

				// Draw toggle circle
				graphics2D.Circle(
					new Vector2(
						position,
						centerY),
					toggleRadius,
					toggleColor);
			}
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			centerY = this.LocalBounds.YCenter;

			var halfBarHeight = barHeight / 2;

			var diff = toggleRadiusPlusPadding - halfBarHeight;

			right = LocalBounds.Right - diff;
			left = diff;

			backgroundBar = new RoundedRect(
					new RectangleDouble(
						left,
						centerY - halfBarHeight,
						right,
						centerY + halfBarHeight),
					halfBarHeight);

			base.OnBoundsChanged(e);
		}
	}
}