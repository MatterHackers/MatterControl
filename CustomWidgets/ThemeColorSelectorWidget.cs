/*
Copyright (c) 2014, Kevin Pope
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
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class ThemeColorSelectorWidget : FlowLayoutWidget
	{
		private GuiWidget colorToChangeTo;
		private int containerHeight = (int)(34 * GuiWidget.DeviceScale + .5);
		private int colorSelectSize = (int)(32 * GuiWidget.DeviceScale + .5);

		public ThemeColorSelectorWidget(GuiWidget colorToChangeTo)
		{
			this.Padding = new BorderDouble(2, 0);
			this.colorToChangeTo = colorToChangeTo;
			int themeCount = ActiveTheme.AvailableThemes.Count;

			var allThemes = ActiveTheme.AvailableThemes;

			int index = 0;
			for (int x = 0; x < themeCount / 2; x++)
			{
				var columnContainer = new FlowLayoutWidget(Agg.UI.FlowDirection.TopToBottom)
				{
					Width = containerHeight
				};
				columnContainer.AddChild(CreateThemeButton(allThemes[index], index));

				int secondRowIndex = index + themeCount / 2;
				columnContainer.AddChild(CreateThemeButton(allThemes[secondRowIndex], secondRowIndex));

				this.AddChild(columnContainer);

				index++;
			}
			this.BackgroundColor = RGBA_Bytes.White;
			this.Width = containerHeight * (themeCount / 2);
		}

		public Button CreateThemeButton(IThemeColors theme, int index)
		{
			var normal = new GuiWidget(colorSelectSize, colorSelectSize);
			normal.BackgroundColor = theme.PrimaryAccentColor;

			var hover = new GuiWidget(colorSelectSize, colorSelectSize);
			hover.BackgroundColor = theme.SecondaryAccentColor;

			var pressed = new GuiWidget(colorSelectSize, colorSelectSize);
			pressed.BackgroundColor = theme.SecondaryAccentColor;

			var disabled = new GuiWidget(colorSelectSize, colorSelectSize);

			var colorButton = new Button(0, 0, new ButtonViewStates(normal, hover, pressed, disabled))
			{
				Name = index.ToString()
			};
			colorButton.Click += (s, e) =>
			{
				string themeIndexText = ((GuiWidget)s).Name;
				int themeIndex;

				if (int.TryParse(themeIndexText, out themeIndex) && themeIndex < ActiveTheme.AvailableThemes.Count)
				{
					ActiveSliceSettings.Instance.SetValue(SettingsKey.active_theme_index, themeIndex.ToString());
					ActiveTheme.Instance = ActiveTheme.AvailableThemes[themeIndex];
				}
			};

			colorButton.MouseEnterBounds += (s, e) =>
			{
				colorToChangeTo.BackgroundColor = theme.PrimaryAccentColor;
			};

			colorButton.MouseLeaveBounds += (s, e) =>
			{
				colorToChangeTo.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
			};

			return colorButton;
		}
	}
}