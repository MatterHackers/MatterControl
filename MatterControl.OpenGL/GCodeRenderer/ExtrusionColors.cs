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
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;

namespace MatterHackers.GCodeVisualizer
{
	public class ExtrusionColors
	{
		private Dictionary<float, Color> speedColors = new Dictionary<float, Color>();

		private double startColor = 223.0 / 360.0;
		private double endColor = 5.0 / 360.0;
		private double range;
		private double delta;
		private float min;
		private float max;

		public ExtrusionColors(HashSet<float> speeds)
		{
			if (speeds.Any())
			{
				min = speeds.Min();
				max = speeds.Max();
			}
			else
			{
				min = 0;
				max = 1;
			}
			range = max - min;
			delta = startColor - endColor;

			foreach (var speed in speeds)
			{
				speedColors[speed] = this.ComputeColor(speed);
			}
		}

		public Color GetColorForSpeed(float speed)
		{
			if (speedColors.TryGetValue(speed, out Color color))
			{
				return color;
			}

			// Compute value if missing from dictionary (legend uses non-existing speeds)
			return this.ComputeColor(speed);
		}

		private Color ComputeColor(float speed)
		{
			var rangedValue = speed - min;
			var factor = range == 0 ? 1 : rangedValue / range;

			double offset = factor * delta;
			double fixedColor = startColor - offset;

			return ColorF.FromHSL(fixedColor, .99, .49).ToColor();
		}
	}
}