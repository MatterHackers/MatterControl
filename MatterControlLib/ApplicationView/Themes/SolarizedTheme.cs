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
	using Agg.Image;

	public class SolarizedTheme : IColorTheme
	{
		private SolarizedColors solarized = new SolarizedColors();

		public SolarizedTheme()
		{
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

			this.Modes = new[] { "Dark", "Light" };
		}

		public Color DefaultColor { get; }
		public string DefaultMode { get; }
		public IEnumerable<Color> Colors { get; }
		public IEnumerable<string> Modes { get; }

		public ThemeSet GetTheme(string mode, Color accentColor)
		{
			bool darkTheme = mode == "Dark";
			var baseColors = darkTheme ? solarized.Dark : solarized.Light;

			return new ThemeSet()
			{
				Theme = new ThemeConfig()
				{
					IsDarkTheme = darkTheme,
					PrimaryAccentColor = accentColor,
					Colors = new ThemeColors()
					{
						PrimaryTextColor = baseColors.Base0,
						SourceColor = accentColor
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
							BackgroundColor = baseColors.Base0,
							TextColor = baseColors.Base02
						},
						Hovered = new ThemeConfig.StateColor()
						{
							BackgroundColor = baseColors.Base01
						},
						Inactive = new ThemeConfig.StateColor()
						{
							BackgroundColor = baseColors.Base02,
							TextColor = baseColors.Base0
						}
					},

					RowBorder = new Color("#00303D"),

					DropList = new ThemeConfig.DropListStyle()
					{
						Inactive = new ThemeConfig.StateColor()
						{
							BorderColor = new Color("#282828"),
							/////////////////////////////
						},
						Open = new ThemeConfig.StateColor()
						{
							BackgroundColor = new Color("#282828"),
							TextColor = baseColors.Base0,
						},
						Menu = new ThemeConfig.StateColor()
						{
							BackgroundColor = new Color("#333333"),
							TextColor = new Color("#eee"),
							BorderColor = new Color("#333")
						}
					},

					SectionBackgroundColor = new Color("#002630"),

					SlightShade = new Color("#00000028"),
					MinimalShade = new Color("#0000000F"),
					Shade = new Color("#00000078"),
					DarkShade = new Color("#000000BE"),

					ActiveTabColor = baseColors.Base03,
					TabBarBackground = baseColors.Base03.Blend(Color.Black, darkTheme ? 0.4 : 0.1),
					//TabBarBackground = new Color(darkTheme ? "#00212B" : "#EEE8D5"),

					InactiveTabColor = baseColors.Base02,
					InteractionLayerOverlayColor = new Color(baseColors.Base03, 240),
					SplitterBackground = baseColors.Base02,
					TabBodyBackground = new Color("#00000000"),
					ToolbarButtonBackground = new Color("#00000000"),
					ThumbnailBackground = new Color("#00000000"),
					AccentMimimalOverlay = new Color(accentColor, 80),
					BorderColor = baseColors.Base0,
					SplashAccentColor = new Color("#eee"),
					BedBackgroundColor = ThemeConfig.ResolveColor2(baseColors.Base03, new Color(Color.Black, 20))
				},
				MenuTheme = (darkTheme) ? this.DarkMenu(baseColors, accentColor) : this.LightMenu(baseColors, accentColor)
			};
		}

		private ThemeConfig LightMenu(BaseColors baseColors, Color accentColor)
		{
			var backgroundColor = new Color("#f1f1f1");

			return new ThemeConfig()
			{
				IsDarkTheme = false,
				PrimaryAccentColor = accentColor,
				Colors = new ThemeColors()
				{
					PrimaryTextColor = new Color("#555"),
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
				BorderColor = new Color("#666666"),
			};
		}

		private ThemeConfig DarkMenu(BaseColors baseColors, Color accentColor)
		{
			var backgroundColor = new Color("#2d2f31");

			return new ThemeConfig()
			{
				IsDarkTheme = true,
				PrimaryAccentColor = accentColor,
				Colors = new ThemeColors()
				{
					PrimaryTextColor = baseColors.Base1,
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

		public Color GetAdjustedAccentColor(Color accentColor, Color backgroundColor)
		{
			return ThemeColors.GetAdjustedAccentColor(accentColor, backgroundColor);
		}

		public class SolarizedColors
		{
			public BaseColors Dark { get; } = new BaseColors()
			{
				Base03 = new Color("#002b36"),
				Base02 = new Color("#073642"),
				Base01 = new Color("#586e75"),
				Base00 = new Color("#657b83"),
				Base0 = new Color("#839496"),
				Base1 = new Color("#93a1a1"),
				Base2 = new Color("#eee8d5"),
				Base3 = new Color("#fdf6e3")
			};

			public BaseColors Light { get; } = new BaseColors()
			{
				Base03 = new Color("#fdf6e3"),
				Base02 = new Color("#eee8d5"),
				Base01 = new Color("#93a1a1"),
				Base00 = new Color("#839496"),
				Base0 = new Color("#657b83"),
				Base1 = new Color("#586e75"),
				Base2 = new Color("#073642"),
				Base3 = new Color("#002b36")
			};

			public Color Yellow { get; } = new Color("#b58900");
			public Color Orange { get; } = new Color("#cb4b16");
			public Color Red { get; } = new Color("#dc322f");
			public Color Magenta { get; } = new Color("#d33682");
			public Color Violet { get; } = new Color("#6c71c4");
			public Color Blue { get; } = new Color("#268bd2");
			public Color Cyan { get; } = new Color("#2aa198");
			public Color Green { get; } = new Color("#859900");
		}

		public class BaseColors
		{
			public Color Base03 { get; set; }
			public Color Base02 { get; set; }
			public Color Base01 { get; set; }
			public Color Base00 { get; set; }
			public Color Base0 { get; set; }
			public Color Base1 { get; set; }
			public Color Base2 { get; set; }
			public Color Base3 { get; set; }
		}
	}
}