/*
Copyright (c) 2017, Kevin Pope, John Lewin
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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class ThemeColorSelectorWidget : FlowLayoutWidget
	{
		private ThemePreviewButton darkPreviewButton;
		private ThemePreviewButton lightPreviewButton;

		private int containerHeight = (int)(30 * GuiWidget.DeviceScale + .5);
		private int colorSelectSize = (int)(28 * GuiWidget.DeviceScale + .5);

		public ThemeColorSelectorWidget(ThemePreviewButton darkPreview, ThemePreviewButton lightPreview)
		{
			this.Padding = new BorderDouble(2, 0);

			this.darkPreviewButton = darkPreview;
			this.lightPreviewButton = lightPreview;

			int themeCount = ActiveTheme.AvailableThemes.Count;

			var allThemes = ActiveTheme.AvailableThemes;

			int index = 0;
			for (int x = 0; x < themeCount / 2; x++)
			{
				var themeButton = CreateThemeButton(allThemes[index], index);
				themeButton.Width = containerHeight;

				this.AddChild(themeButton);

				index++;
			}

			this.Width = containerHeight * (themeCount / 2);
		}

		private int hoveredThemeIndex = 0;
		private int midPoint = ActiveTheme.AvailableThemes.Count / 2;
			
		public Button CreateThemeButton(IThemeColors darkTheme, int darkThemeIndex)
		{
			var normal = new GuiWidget(colorSelectSize, colorSelectSize);
			normal.BackgroundColor = darkTheme.PrimaryAccentColor;

			var hover = new GuiWidget(colorSelectSize, colorSelectSize);
			hover.BackgroundColor = darkTheme.SecondaryAccentColor;

			var pressed = new GuiWidget(colorSelectSize, colorSelectSize);
			pressed.BackgroundColor = darkTheme.SecondaryAccentColor;

			var disabled = new GuiWidget(colorSelectSize, colorSelectSize);

			int lightThemeIndex = darkThemeIndex + midPoint;
			var lightTheme = ActiveTheme.AvailableThemes[lightThemeIndex];

			var colorButton = new Button(0, 0, new ButtonViewStates(normal, hover, pressed, disabled));
			colorButton.Cursor = Cursors.Hand;
			colorButton.Click += (s, e) =>
			{
				// Determine if we should set the dark or light version of the theme
				var activeThemeIndex = ActiveTheme.AvailableThemes.IndexOf(ActiveTheme.Instance);

				bool useLightTheme = activeThemeIndex >= midPoint;

				SetTheme(darkThemeIndex, useLightTheme);
			};

			colorButton.MouseEnterBounds += (s, e) =>
			{
				darkPreviewButton.SetThemeColors(darkTheme);
				lightPreviewButton.SetThemeColors(lightTheme);

				hoveredThemeIndex = darkThemeIndex;
			};

			colorButton.MouseLeaveBounds += (s, e) =>
			{
				// darkPreviewButton.SetThemeColors(ActiveTheme.Instance);
			};

			return colorButton;
		}

		private static void SetTheme(int themeIndex, bool useLightTheme)
		{
			if (useLightTheme)
			{
				themeIndex += (ActiveTheme.AvailableThemes.Count / 2);
			}

			// save it for this printer
			SetTheme(ActiveTheme.AvailableThemes[themeIndex].Name);
		}

		public static void SetTheme(string themeName)
		{
			// save it for this printer
			ActiveSliceSettings.Instance.SetValue(SettingsKey.active_theme_name, themeName);

			//Set new user selected Default
			ActiveTheme.Instance = ActiveTheme.GetThemeColors(themeName);
		}
	}
}