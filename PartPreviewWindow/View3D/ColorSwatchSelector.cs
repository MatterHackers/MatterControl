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
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ColorSwatchSelector : FlowLayoutWidget
	{
		public ColorSwatchSelector(InteractiveScene scene, ThemeConfig theme, int buttonSize = 32, double spacing = 3)
			: base(FlowDirection.TopToBottom)
		{
			int colorCount = 9;

			double[] lightness = new double[] { .7, .5, .3 };

			var grayLevel = new Color[] { Color.White, new Color(180, 180, 180), Color.Gray };

			for (int rowIndex = 0; rowIndex < lightness.Length; rowIndex++)
			{
				var colorRow = new FlowLayoutWidget();
				AddChild(colorRow);

				for (int colorIndex = 0; colorIndex < colorCount; colorIndex++)
				{
					var color = ColorF.FromHSL(colorIndex / (double)colorCount, 1, lightness[rowIndex]).ToColor();
					colorRow.AddChild(MakeColorButton(scene, color, buttonSize, spacing));
				}

				// put in white and black buttons
				colorRow.AddChild(MakeColorButton(scene, grayLevel[rowIndex], buttonSize, spacing));

				switch(rowIndex)
				{
					case 0:
						var resetButton = new IconButton(AggContext.StaticData.LoadIcon("transparent_grid.png"), theme)
						{
							Width = buttonSize,
							Height = buttonSize,
							Margin = spacing
						};
						resetButton.Click += (s, e) =>
						{
							scene.UndoBuffer.AddAndDo(new ChangeColor(scene.SelectedItem, Color.Transparent));
						};
						colorRow.AddChild(resetButton);
						break;

					case 1:
						colorRow.AddChild(MakeColorButton(scene, new Color("#555"), buttonSize, spacing));
						break;

					case 2:
						colorRow.AddChild(MakeColorButton(scene, new Color("#222"), buttonSize, spacing));
						break;
				}
			}
		}

		private GuiWidget MakeColorButton(InteractiveScene scene, Color color, int buttonSize, double spacing)
		{
			var colorWidget = new GuiWidget()
			{
				BackgroundColor = color,
				Width = buttonSize,
				Height = buttonSize,
				Margin = spacing,
				Border = new BorderDouble(1),
				BorderColor = Color.Black,
			};

			colorWidget.Click += (s, e) =>
			{
				scene.UndoBuffer.AddAndDo(new ChangeColor(scene.SelectedItem, colorWidget.BackgroundColor));
			};

			return colorWidget;
		}
	}
}