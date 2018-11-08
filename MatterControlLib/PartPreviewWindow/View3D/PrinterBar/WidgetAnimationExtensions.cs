/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public static class WidgetAnimationExtensions
	{
		public static void FlashBackground(this GuiWidget widget, Color hightlightColor)
		{
			double displayTime = 2;
			double pulseTime = .5;
			double totalSeconds = 0;
			Color backgroundColor = widget.BackgroundColor;
			// Show a highlight on the button as the user did not click it
			Animation flashBackground = null;
			flashBackground = new Animation()
			{
				DrawTarget = widget,
				FramesPerSecond = 10,
				Update = (s1, updateEvent) =>
				{
					totalSeconds += updateEvent.SecondsPassed;
					if (totalSeconds < displayTime)
					{
						double blend = AttentionGetter.GetFadeInOutPulseRatio(totalSeconds, pulseTime);
						widget.BackgroundColor = new Color(hightlightColor, (int)(blend * 255));
					}
					else
					{
						widget.BackgroundColor = backgroundColor;
						flashBackground.Stop();
					}
				}
			};
			flashBackground.Start();
		}

		public static void SlideToNewState(this RadioIconButton widget, RadioIconButton newActiveButton, OverflowBar parent, Action animationComplete, ThemeConfig theme)
		{
			double displayTime = 600;
			double elapsedMs = 0;

			var box = new GuiWidget()
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute,
				Position = widget.Position + new VectorMath.Vector2(widget.Margin.Width, widget.Margin.Height),
				Size = widget.Size,
				BackgroundColor = theme.AccentMimimalOverlay,
				Border = 1,
				BorderColor = theme.PrimaryAccentColor
			};
			parent.AddChildDirect(box);

			var startX = box.Position.X;
			var startY = box.Position.Y;
			var xdistance = (newActiveButton.Position.X + newActiveButton.Margin.Width) - startX;
			var direction = xdistance > 0 ? 1 : -1;
			var startedMS = UiThread.CurrentTimerMs;

			Animation animation = null;
			animation = new Animation()
			{
				DrawTarget = widget,
				FramesPerSecond = 20,
				Update = (s1, updateEvent) =>
				{
					elapsedMs = UiThread.CurrentTimerMs - startedMS;
					if (elapsedMs < (displayTime + 300))
					{
						var ratio = Math.Min(1, elapsedMs / displayTime);
						double blend = Easing.Cubic.In(ratio);
						box.Position = new VectorMath.Vector2(startX + (xdistance * blend), startY);

						//Console.WriteLine("Ms: {0}, Ratio: {1}, Easing: {2}, Position: {3}", elapsedMs, ratio, blend, box.Position);
						box.Invalidate();
					}
					else
					{
						animation.Stop();

						animationComplete?.Invoke();

						UiThread.RunOnIdle(box.Close, .3);
					}
				}
			};

			animation.Start();
		}
	}
}