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
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SpeedsLegend : FlowLayoutWidget
	{
		public SpeedsLegend(GCodeFile gcodeFileTest, ThemeConfig theme, int pointSize)
			: base(FlowDirection.TopToBottom)
		{
			HashSet<float> speeds = new HashSet<float>();
			PrinterMachineInstruction previousInstruction = gcodeFileTest.Instruction(0);
			for (int i = 1; i < gcodeFileTest.LineCount; i++)
			{
				PrinterMachineInstruction instruction = gcodeFileTest.Instruction(i);
				if (instruction.EPosition > previousInstruction.EPosition && (instruction.Line.IndexOf('X') != -1 || instruction.Line.IndexOf('Y') != -1))
				{
					speeds.Add((float)instruction.FeedRate);
				}
				previousInstruction = instruction;
			}

			ExtrusionColors extrusionColors = new ExtrusionColors();

			speeds.Select(speed => extrusionColors.GetColorForSpeed(speed)).ToArray();

			if(speeds.Count <= 0)
			{
				// There are no paths so don't generate the rest of the widget.
				return;
			}

			float min = speeds.Min();
			float max = speeds.Max();
			int maxItems = Math.Min(7, speeds.Count);

			int count = maxItems - 1;
			float increment = (max - min) / count;
			int index = 0;

			int[] rangeValues;
			if (speeds.Count < 8)
			{
				rangeValues = speeds.Select(s => (int)s).OrderBy(i => i).ToArray();
			}
			else
			{
				rangeValues = Enumerable.Range(0, maxItems).Select(x => (int)(min + increment * index++)).ToArray();
			}

			Color[] speedColors = rangeValues.OrderBy(s => s).Select(speed => extrusionColors.GetColorForSpeed(speed)).ToArray();

			for (int i = 0; i < speedColors.Length; i++)
			{
				int feedrate = rangeValues[i];

				this.AddChild(
					new SpeedLegendRow(speedColors[i], millimetersPerSecond: feedrate / 60, pointSize: pointSize)
					{
						Margin = new BorderDouble(2),
						HAnchor = HAnchor.Stretch
					});
			}
		}

		private class SpeedLegendRow : FlowLayoutWidget
		{
			public SpeedLegendRow(Color color, double millimetersPerSecond, int pointSize)
				: base(FlowDirection.LeftToRight)
			{
				this.AddChild(
					new GuiWidget
					{
						VAnchor = VAnchor.Center,
						Width = 13,
						Height = 13,
						BackgroundColor = color,
					});

				this.AddChild(
					new TextWidget($"{millimetersPerSecond} mm/s", pointSize: pointSize, textColor: ActiveTheme.Instance.PrimaryTextColor)
					{
						VAnchor = VAnchor.Center,
						Margin = new BorderDouble(8, 0),
					});
			}
		}
	}
}