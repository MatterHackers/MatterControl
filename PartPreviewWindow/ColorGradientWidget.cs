using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ColorGradientWidget : FlowLayoutWidget
	{
		public ColorGradientWidget(GCodeFile gcodeFileTest, ThemeConfig theme, int pointSize)
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

			RGBA_Bytes[] speedColors = rangeValues.OrderBy(s => s).Select(speed => extrusionColors.GetColorForSpeed(speed)).ToArray();

			for (int i = 0; i < speedColors.Length; i++)
			{
				int feedrate = rangeValues[i];

				this.AddChild(
					new ColorToSpeedWidget(speedColors[i], millimetersPerSecond: feedrate / 60, pointSize: pointSize)
					{
						Margin = new BorderDouble(2),
						HAnchor = HAnchor.Stretch
					});
			}
		}

		public class ColorToSpeedWidget : FlowLayoutWidget
		{
			public ColorToSpeedWidget(RGBA_Bytes color, double millimetersPerSecond, int pointSize)
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