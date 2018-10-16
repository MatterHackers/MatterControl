/*
Copyright (c) 2018, John Lewin
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

namespace MatterHackers.MatterControl
{
	using System.Collections.Generic;
	using static MatterHackers.MatterControl.SolarizedTheme;

	public class AffinityTheme : IColorTheme
	{
		public AffinityTheme()
		{
			var solarized = new SolarizedColors();

			this.Colors = new[]
			{
				solarized.Blue,
				solarized.Cyan,
				solarized.Green,
				solarized.Magenta,
				solarized.Orange,
				solarized.Red,
				solarized.Violet,
				solarized.Yellow
			};

			this.DefaultColor = solarized.Yellow;
			this.DefaultMode = "Dark";

			this.Modes = new[] { "Dark" };
		}

		public Color DefaultColor { get; }
		public string DefaultMode { get; }
		public IEnumerable<Color> Colors { get; }
		public IEnumerable<string> Modes { get; }

		public ThemeSet GetTheme(string mode, Color accentColor)
		{
			bool darkTheme = mode == "Dark";

			var textColor = new Color("#bbb");

			return new ThemeSet()
			{
				Theme = new ThemeConfig()
				{
					PrimaryAccentColor = accentColor,
					IsDarkTheme = darkTheme,
					Colors = new ThemeColors()
					{
						PrimaryTextColor = textColor,
					},
					PresetColors = new PresetColors()
					{
						MaterialPreset = new Color("#FF7F00"),
						QualityPreset = new Color("#FFFF00"),
						UserOverride = new Color("#445FDC96")
					},
					EditFieldColors = new ThemeConfig.ThreeStateColor()
					{
						Focused = new ThemeConfig.StateColor()
						{
							BackgroundColor = new Color("#999"),
							TextColor = new Color("#222"),
							BorderColor = new Color("#FF7F00")
						},
						Hovered = new ThemeConfig.StateColor()
						{
							BackgroundColor = new Color("#333333"),
							TextColor = new Color("#fff"),
							BorderColor = new Color("#FF7F00")
						},
						Inactive = new ThemeConfig.StateColor()
						{
							BackgroundColor = new Color("#333333"),
							TextColor = textColor,
							BorderColor = new Color("#282828")
						}
					},
					SlightShade = new Color("#00000028"),
					MinimalShade = new Color("#0000000F"),
					Shade = new Color("#00000078"),
					DarkShade = new Color("#000000BE"),

					ActiveTabColor = new Color("#373737"),
					TabBarBackground = new Color("#282828"),

					InactiveTabColor = new Color("#404040"),
					InteractionLayerOverlayColor = new Color(new Color("#373737"), 240),
					SplitterBackground = new Color("#282828"),
					TabBodyBackground = new Color("#00000000"),
					ToolbarButtonBackground = new Color("#00000000"),
					ThumbnailBackground = new Color("#00000000"),
					AccentMimimalOverlay = new Color(accentColor, 80),
					BorderColor = new Color("#000"),
					SplashAccentColor = new Color("#eee")
				},
				MenuTheme = this.DarkMenu(accentColor)
			};
		}

		private ThemeConfig DarkMenu(Color accentColor)
		{
			var backgroundColor = new Color("#2d2f31");

			return new ThemeConfig()
			{
				IsDarkTheme = true,
				Colors = new ThemeColors()
				{
					PrimaryTextColor = new Color("#eee"),
					SourceColor = accentColor
				},
				PresetColors = new PresetColors()
				{
					MaterialPreset = new Color("#FF7F00"),
					QualityPreset = new Color("#FFFF00"),
					UserOverride = new Color("#445FDC96")
				},
				SlightShade = new Color("#00000028"),
				MinimalShade = new Color("#0000000F"),
				Shade = new Color("#00000078"),
				DarkShade = new Color("#000000BE"),

				ActiveTabColor = backgroundColor,
				TabBarBackground = new Color("#B1B1B1"),
				InactiveTabColor = new Color("#D0D0D0"),
				InteractionLayerOverlayColor = new Color("#D0D0D0F0"),
				SplitterBackground = new Color("#B5B5B5"),
				TabBodyBackground = new Color("#00000000"),
				ToolbarButtonBackground = new Color("#00000000"),
				ThumbnailBackground = new Color("#00000000"),
				AccentMimimalOverlay = new Color(accentColor, 80),
				BorderColor = new Color("#c8c8c8"),
			};
		}
	}
}