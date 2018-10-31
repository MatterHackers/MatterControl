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
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ColorSwatchSelector : FlowLayoutWidget
	{
		public ColorSwatchSelector(ThemeConfig theme, BorderDouble buttonSpacing, int buttonSize = 32, Action<Color> colorNotifier = null)
			: base(FlowDirection.TopToBottom)
		{
			var scaledButtonSize = buttonSize * GuiWidget.DeviceScale;

			int colorCount = 9;

			double[] lightness = new double[] { .7, .5, .3 };

			Action<Color> colorChanged = (color) =>
			{
				colorNotifier?.Invoke(color);
			};

			var grayLevel = new Color[] { Color.White, new Color(180, 180, 180), Color.Gray };

			for (int rowIndex = 0; rowIndex < lightness.Length; rowIndex++)
			{
				var colorRow = new FlowLayoutWidget();
				AddChild(colorRow);

				for (int colorIndex = 0; colorIndex < colorCount; colorIndex++)
				{
					var color = ColorF.FromHSL(colorIndex / (double)colorCount, 1, lightness[rowIndex]).ToColor();
					colorRow.AddChild(MakeColorButton(color, scaledButtonSize, buttonSpacing, colorChanged));
				}

				// put in white and black buttons
				colorRow.AddChild(MakeColorButton(grayLevel[rowIndex], scaledButtonSize, buttonSpacing, colorChanged));

				switch(rowIndex)
				{
					case 0:
						var resetButton = new IconButton(AggContext.StaticData.LoadIcon("transparent_grid.png"), theme)
						{
							Width = scaledButtonSize,
							Height = scaledButtonSize,
							Margin = buttonSpacing,
							VAnchor = VAnchor.Absolute
						};
						resetButton.Click += (s, e) =>
						{
							// The colorChanged action displays the given color - use .MinimalHighlight rather than no color
							colorChanged(Color.Transparent);
						};
						colorRow.AddChild(resetButton);
						break;

					case 1:
						colorRow.AddChild(MakeColorButton(new Color("#555"), scaledButtonSize, buttonSpacing, colorChanged));
						break;

					case 2:
						colorRow.AddChild(MakeColorButton(new Color("#222"), scaledButtonSize, buttonSpacing, colorChanged));
						break;
				}
			}
		}

		private GuiWidget MakeColorButton(Color color, double buttonSize, BorderDouble buttonSpacing, Action<Color> colorChanged)
		{
			var button = new ColorButton(color)
			{
				Width = buttonSize,
				Height = buttonSize,
				Margin = buttonSpacing
			};

			button.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					colorChanged(button.BackgroundColor);
				});
			};

			return button;
		}
	}

	public class ColorButton : GuiWidget
	{
		public ColorButton(Color color)
		{
			this.BackgroundColor = color;

			// Default DisabledColor to the grayscale of current color
			this.DisabledColor = color.ToGrayscale();
		}

		public bool DrawGrid { get; set; }

		public override Color BackgroundColor
		{
			get => (this.Enabled) ? base.BackgroundColor : this.DisabledColor;
			set
			{
				base.BackgroundColor = value;
			}
		}

		public Color DisabledColor { get; set; }

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (this.DrawGrid)
			{
				var numberOfStripes = 3;

				int ix = 20;

				for (var i = 0; i < numberOfStripes * 2; i++)
				{
					var thickness = ix / numberOfStripes;

					graphics2D.Line(
						i * thickness + thickness / 2 - ix,
						0,
						i * thickness + thickness / 2,
						ix,
						Color.Gray,
						0.05);
				}
			}

			base.OnDraw(graphics2D);
		}

		public Color SourceColor { get; set; }
	}

	public static class ColorExtensions
	{
		public static Color ToGrayscale(this Color color)
		{
			int y = (color.red * 77) + (color.green * 151) + (color.blue * 28);
			int gray = (y >> 8);

			return new Color(gray, gray, gray, 255);
		}
	}
}