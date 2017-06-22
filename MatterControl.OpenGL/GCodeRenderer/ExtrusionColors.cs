using MatterHackers.Agg;

/*
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
using System.Linq;

namespace MatterHackers.GCodeVisualizer
{
	public class ExtrusionColors
	{
		private SortedList<float, RGBA_Bytes> speedColorLookup = new SortedList<float, RGBA_Bytes>();

		public RGBA_Bytes GetColorForSpeed(float speed)
		{
			if (speed > 0)
			{
				lock(speedColorLookup)
				{
					double startColor = 223.0 / 360.0;
					double endColor = 5.0 / 360.0;
					double delta = startColor - endColor;

					if (!speedColorLookup.ContainsKey(speed))
					{
						RGBA_Bytes color = RGBA_Floats.FromHSL(startColor, .99, .49).GetAsRGBA_Bytes();
						speedColorLookup.Add(speed, color);

						if (speedColorLookup.Count > 1)
						{
							double step = delta / (speedColorLookup.Count - 1);
							for (int index = 0; index < speedColorLookup.Count; index++)
							{
								double offset = step * index;
								double fixedColor = startColor - offset;
								KeyValuePair<float, RGBA_Bytes> keyValue = speedColorLookup.ElementAt(index);
								speedColorLookup[keyValue.Key] = RGBA_Floats.FromHSL(fixedColor, .99, .49).GetAsRGBA_Bytes();
							}
						}
					}

					return speedColorLookup[speed];
				}
			}

			return RGBA_Bytes.Black;
		}
	}
}