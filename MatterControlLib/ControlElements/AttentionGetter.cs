﻿/*
Copyright (c) 2014, Lars Brubaker
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
using System.Diagnostics;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using static MatterHackers.VectorMath.Easing;

namespace MatterHackers.MatterControl
{
	public class AttentionGetter
	{
		private static readonly HashSet<GuiWidget> RunningAttentions = new HashSet<GuiWidget>();
		private readonly double animationDelay = 1 / 20.0;
		private readonly int cycles = 1;
		private readonly double lightnessChange = 1;
		private readonly double pulseTime = 1.38;
		private Color startColor;
		private Stopwatch timeSinceStart = null;
		private GuiWidget widgetToHighlight;

		private AttentionGetter(GuiWidget widgetToHighlight)
		{
			this.widgetToHighlight = widgetToHighlight;
			widgetToHighlight.AfterDraw += ConnectToWidget;
		}

		public static AttentionGetter GetAttention(GuiWidget widgetToHighlight)
		{
			if (!RunningAttentions.Contains(widgetToHighlight))
			{
				RunningAttentions.Add(widgetToHighlight);
				return new AttentionGetter(widgetToHighlight);
			}

			return null;
		}

		public static double GetFadeInOutPulseRatio(double elapsedTime, double pulseTime)
		{
			double ratio = elapsedTime;
			while (ratio > pulseTime)
			{
				ratio -= pulseTime;
			}

			ratio = ratio * 2 / pulseTime;
			if (ratio > 1)
			{
				ratio = 1 - (ratio - 1);
			}

			return ratio;
		}

		private void ChangeBackgroundColor()
		{
			if (widgetToHighlight != null)
			{
				double time = GetFadeInOutPulseRatio(timeSinceStart.Elapsed.TotalSeconds, pulseTime);
				double lightnessMultiplier = Quadratic.InOut(time);

				widgetToHighlight.BackgroundColor = startColor.AdjustLightness(1 + lightnessChange * lightnessMultiplier).ToColor();
				if (widgetToHighlight.HasBeenClosed || timeSinceStart.Elapsed.TotalSeconds > cycles * pulseTime)
				{
					widgetToHighlight.BackgroundColor = startColor;
					widgetToHighlight.AfterDraw -= ConnectToWidget;
					RunningAttentions.Remove(widgetToHighlight);
					widgetToHighlight = null;
					return;
				}
			}
		}

		private void ConnectToWidget(object drawingWidget, DrawEventArgs e)
		{
			var parent = drawingWidget as GuiWidget;
			while (parent.BackgroundColor.Alpha0To255 == 0)
			{
				parent = parent.Parent;
			}

			startColor = parent.BackgroundColor;
			timeSinceStart = Stopwatch.StartNew();
			widgetToHighlight.AfterDraw -= ConnectToWidget;
			var runningInterval = UiThread.SetInterval(ChangeBackgroundColor, animationDelay);
			parent.Closed += (s, e2) => UiThread.ClearInterval(runningInterval);
		}
	}
}