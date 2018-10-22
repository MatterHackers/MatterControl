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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Newtonsoft.Json;

	public class ClassicColorsTheme : IColorTheme
	{
		public ClassicColorsTheme()
		{
			this.Colors = namedColors.Values.Take(namedColors.Count / 2);
			this.DefaultColor = namedColors["Red - Dark"];
			this.DefaultMode = "Dark";

			this.Modes = new[] { "Dark", "Light" };
		}

		public Color DefaultColor { get; }
		public string DefaultMode { get; }
		public IEnumerable<Color> Colors { get; }
		public IEnumerable<string> Modes { get; }

		private static Dictionary<string, Color> namedColors = new Dictionary<string, Color>()
		{
			//Dark themes
			{ "Red - Dark", new Color(172, 25, 61) },
			{ "Pink - Dark", new Color(220, 79, 173) },
			{ "Orange - Dark", new Color(255, 129, 25) },
			{ "Green - Dark", new Color(0, 138, 23) },
			{ "Blue - Dark", new Color(0, 75, 139) },
			{ "Teal - Dark", new Color(0, 130, 153) },
			{ "Light Blue - Dark", new Color(93, 178, 255) },
			{ "Purple - Dark", new Color(70, 23, 180) },
			{ "Magenta - Dark", new Color(140, 0, 149) },
			{ "Grey - Dark", new Color(88, 88, 88) },

			//Light themes
			{ "Red - Light", new Color(172, 25, 61) },
			{ "Pink - Light", new Color(220, 79, 173) },
			{ "Orange - Light", new Color(255, 129, 25) },
			{ "Green - Light", new Color(0, 138, 23) },
			{ "Blue - Light", new Color(0, 75, 139) },
			{ "Teal - Light", new Color(0, 130, 153) },
			{ "Light Blue - Light", new Color(93, 178, 255) },
			{ "Purple - Light", new Color(70, 23, 180) },
			{ "Magenta - Light", new Color(140, 0, 149) },
			{ "Grey - Light", new Color(88, 88, 88) },
		};

		public ThemeSet GetTheme(string mode, Color accentColor)
		{
			bool darkTheme = mode == "Dark";
			Console.WriteLine("Requesting theme for " + accentColor.Html);

			var (colors, modifiedAccentColor) = ThemeColors.Create(accentColor, darkTheme);

			Console.WriteLine("Generated: PrimaryAccent: " + modifiedAccentColor + " source: " + colors.SourceColor);

			return ThemeFromColors(colors, modifiedAccentColor, darkTheme);
		}

		public static ThemeSet ThemeFromColors(ThemeColors colors, Color accentColor, bool darkTheme)
		{
			var json = JsonConvert.SerializeObject(colors);

			var clonedColors = JsonConvert.DeserializeObject<ThemeColors>(json);
			clonedColors.PrimaryTextColor = new Color("#222");

			var menuTheme = BuildTheme(clonedColors, accentColor, darkTheme);
			menuTheme.ActiveTabColor = new Color("#f1f1f1");
			menuTheme.IsDarkTheme = false;

			return new ThemeSet()
			{
				Theme = BuildTheme(colors, accentColor, darkTheme),
				MenuTheme = menuTheme
			};
		}

		private static ThemeConfig BuildTheme(ThemeColors colors, Color accentColor, bool darkTheme)
		{
			var theme = new ThemeConfig();

			theme.Colors = colors;
			theme.IsDarkTheme = darkTheme;
			theme.PrimaryAccentColor = accentColor;

			theme.SlightShade = new Color(0, 0, 0, 40);
			theme.MinimalShade = new Color(0, 0, 0, 15);
			theme.Shade = new Color(0, 0, 0, 120);
			theme.DarkShade = new Color(0, 0, 0, 190);

			theme.ActiveTabColor = theme.ResolveColor(
				new Color(darkTheme ? "#3E3E3E" : "#BEBEBE"),
				new Color(
					Color.White,
					(darkTheme) ? 3 : 25));
			theme.TabBarBackground = theme.ActiveTabColor.WithLightnessAdjustment(0.85).ToColor();
			theme.ThumbnailBackground = Color.Transparent;
			theme.AccentMimimalOverlay = new Color(accentColor, 50);
			theme.InteractionLayerOverlayColor = new Color(theme.ActiveTabColor, 240);
			theme.InactiveTabColor = theme.ResolveColor(theme.ActiveTabColor, new Color(Color.White, darkTheme ? 20 : 60));

			theme.SplitterBackground = theme.ActiveTabColor.WithLightnessAdjustment(0.87).ToColor();

			theme.BorderColor = new Color(darkTheme ? "#C8C8C8" : "#333");

			theme.RowBorder = new Color(darkTheme ? "#474747" : "#A6A6A6");

			theme.EditFieldColors = new ThemeConfig.ThreeStateColor()
			{
				Focused = new ThemeConfig.StateColor()
				{
					BackgroundColor = new Color("#eee"),
					TextColor = new Color("#222"),
					BorderColor = Color.Orange
				},
				Hovered = new ThemeConfig.StateColor()
				{
					BackgroundColor = new Color("#eee"),
					BorderColor = Color.Orange
				},
				Inactive = new ThemeConfig.StateColor()
				{
					BackgroundColor = new Color("#eee"),
					TextColor = new Color("#222")
				}
			};

			theme.DropList = new ThemeConfig.DropListStyle()
			{
				Inactive = new ThemeConfig.StateColor()
				{
					BackgroundColor = (darkTheme) ? new Color("#404040") : new Color("#c4c4c4"),
					TextColor = (darkTheme) ? new Color("#eee") : new Color("#222"),
					BorderColor = (darkTheme) ? new Color("#656565") : new Color("#919191")
				},
				Hovered = new ThemeConfig.StateColor()
				{
					BackgroundColor = (darkTheme) ? new Color("#404040") : new Color("#aaa"),
					TextColor = (darkTheme) ? new Color("#eee") : new Color("#222"),
					BorderColor = Color.Orange
				},
				Open = new ThemeConfig.StateColor()
				{
					BackgroundColor = (darkTheme) ? new Color("#8A8A8A") : new Color("#dbdbdb"),
					TextColor = (darkTheme) ? new Color("#eee") : new Color("#222"),
				},
				Focused = new ThemeConfig.StateColor()
				{
					BackgroundColor = (darkTheme) ? new Color("#282828") : new Color("#aaa"),
					TextColor = (darkTheme) ? new Color("#eee") : new Color("#222"),
					BorderColor = Color.Orange
				},
				Menu = new ThemeConfig.StateColor()
				{
					BackgroundColor = new Color("#eee"),
					TextColor = new Color("#333"),
					BorderColor = new Color("#333")
				}
			};

			theme.SectionBackgroundColor = new Color(darkTheme ? "#393939" : "#B9B9B9");

			theme.SplashAccentColor = accentColor;

			theme.BedBackgroundColor = theme.ResolveColor(theme.ActiveTabColor, new Color(Color.Black, 20));

			theme.PresetColors = new PresetColors();

			return theme;
		}
	}
}