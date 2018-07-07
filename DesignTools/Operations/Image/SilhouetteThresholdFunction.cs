/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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

namespace MatterHackers.MatterControl.DesignTools
{
	public class SilhouetteThresholdFunction : IThresholdFunction
	{
		protected double rangeStart = .1;
		protected double rangeEnd = 1.0;

		public SilhouetteThresholdFunction()
		{
		}

		public SilhouetteThresholdFunction(double rangeStart, double rangeEnd)
		{
			this.rangeStart = Math.Max(0, Math.Min(1, rangeStart));
			this.rangeEnd = Math.Max(0, Math.Min(1, rangeEnd));
		}

		public double Transform(Color color)
		{
			// we invert the gray value so we have black being the color we are finding
			return 1 - ((color.Red0To1 * 0.2989) + (color.Blue0To1 * 0.5870) + (color.Green0To1 * 0.1140));
		}

		public double Threshold(Color color)
		{
			// this is on I from HSI
			return GetThresholded0To1(Transform(color));
		}

		public Color ZeroColor => Color.White;

		protected double GetThresholded0To1(double rawValue)
		{
			double outValue = 0;
			if (rawValue < rangeStart)
			{
				outValue = 0;
			}
			else if (rawValue > rangeEnd)
			{
				outValue = 0;
			}
			else
			{
				outValue = (double)(rawValue - rangeStart) / (double)(rangeEnd - rangeStart);
			}

			return outValue;
		}
	}
}