using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.MatterControl.CustomWidgets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ColorGradientWidget : FlowLayoutWidget
	{
		public ColorGradientWidget(GCodeFile gcodeFileTest)
			: base(FlowDirection.TopToBottom)
		{
			BackgroundColor = new RGBA_Bytes(0, 0, 0, 120);

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
			int maxItems = Math.Min(7, speeds.Count());

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
				RGBA_Bytes color = speedColors[i];
				int speed = rangeValues[i];

				GuiWidget colorWidget = new GuiWidget();
				colorWidget.Width = 20;
				colorWidget.Height = 20;
				colorWidget.BackgroundColor = color;
				colorWidget.Margin = new BorderDouble(2);
				double feedRateToMMPerSecond = speed / 60;

				ColorToSpeedWidget colorToSpeedWidget = new ColorToSpeedWidget(colorWidget, feedRateToMMPerSecond);
				this.AddChild(colorToSpeedWidget);
			}
		}

		public class ColorToSpeedWidget : FlowLayoutWidget
		{
			public string layerSpeed;
			public ColorToSpeedWidget(GuiWidget colorWidget, double speed)
				: base(FlowDirection.LeftToRight)
			{
				Margin = new BorderDouble(2);

				layerSpeed = "{0} mm/s".FormatWith(speed);

				colorWidget.Margin = new BorderDouble(left: 2);

				TextWidget speedTextBox = new TextWidget(layerSpeed, pointSize: 12);
				speedTextBox.TextColor = RGBA_Bytes.White;
				speedTextBox.VAnchor = VAnchor.ParentCenter;
				speedTextBox.Margin = new BorderDouble(5, 0);

				this.AddChild(colorWidget);
				this.AddChild(new HorizontalSpacer());
				this.AddChild(speedTextBox);

				this.HAnchor |= HAnchor.ParentLeftRight;
			}
		}
	}
}