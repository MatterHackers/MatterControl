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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using global::MatterControl.Printing;
using Markdig.Agg;
using Markdig.Renderers.Agg;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.Extensibility;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.MatterControl.Plugins;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Tour;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using MatterHackers.VectorMath.TrackBall;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("MatterControl.Tests")]
[assembly: InternalsVisibleTo("MatterControl.AutomationTests")]
[assembly: InternalsVisibleTo("CloudServices.Tests")]

namespace MatterHackers.MatterControl
{

	public static class AppContext
	{
		/// <summary>
		/// Gets or sets native platform features
		/// </summary>
		public static INativePlatformFeatures Platform { get; set; }

		public static MatterControlOptions Options { get; set; } = new MatterControlOptions();

		public static bool IsLoading { get; internal set; } = true;

		/// <summary>
		/// Gets the root SystemWindow
		/// </summary>
		public static SystemWindow RootSystemWindow { get; internal set; }

		public static ThemeConfig Theme => themeset.Theme;

		public static ThemeConfig MenuTheme => themeset.MenuTheme;

		private static ThemeSet themeset;

		public static ThemeSet ThemeSet => themeset;

		public static Dictionary<string, IColorTheme> ThemeProviders { get; }

		private static Dictionary<string, string> themes = new Dictionary<string, string>();

		static AppContext()
		{
			ThemeProviders = new Dictionary<string, IColorTheme>();

			string themesPath = Path.Combine("Themes", "System");

			var staticData = AggContext.StaticData;

			// Load available themes from StaticData
			if (staticData.DirectoryExists(themesPath))
			{
				var themeFiles = staticData.GetDirectories(themesPath).SelectMany(d => staticData.GetFiles(d).Where(p => Path.GetExtension(p) == ".json"));
				foreach (var themeFile in themeFiles)
				{
					themes[Path.GetFileNameWithoutExtension(themeFile)] = themeFile;
				}

				foreach (var directoryTheme in AggContext.StaticData.GetDirectories(themesPath).Where(d => Path.GetFileName(d) != "Menus").Select(d => new DirectoryTheme(d)))
				{
					ThemeProviders.Add(directoryTheme.Name, directoryTheme);
				}
			}

			// Load theme
			try
			{
				if (File.Exists(ProfileManager.Instance.ProfileThemeSetPath))
				{
					themeset = JsonConvert.DeserializeObject<ThemeSet>(File.ReadAllText(ProfileManager.Instance.ProfileThemeSetPath));
					themeset.Theme.EnsureDefaults();

					// If the serialized format is older than the current format, null and fall back to latest default below
					if (themeset.SchemeVersion != ThemeSet.LatestSchemeVersion)
					{
						themeset = null;
					}
				}
			}
			catch { }

			if (themeset == null)
			{
				var themeProvider = ThemeProviders["Modern"];
				themeset = themeProvider.GetTheme("Modern-Dark");
			}

			DefaultThumbView.ThumbColor = new Color(themeset.Theme.TextColor, 30);

			ToolTipManager.CreateToolTip = MatterControlToolTipWidget;
		}

		private static GuiWidget MatterControlToolTipWidget(string toolTipText)
		{
			var toolTipPopover = new ClickablePopover(ArrowDirection.Up, new BorderDouble(0, 0), 7, 0);

			var markdownWidegt = new MarkdownWidget(Theme, false)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Fit,
				Width = 350 * GuiWidget.DeviceScale,
				BackgroundColor = Theme.BackgroundColor,
				Border = 1,
				BorderColor = Color.Black,
			};

			markdownWidegt.Markdown = toolTipText;
			markdownWidegt.Width = 350;
			var maxLineWidth = markdownWidegt.Descendants<ParagraphX>().Max(i => i.MaxLineWidth);
			markdownWidegt.Width = maxLineWidth + 15;

			return markdownWidegt;
		}

		public static ThemeConfig LoadTheme(string themeName)
		{
			try
			{
				if (themes.TryGetValue(themeName, out string themePath))
				{
					string json = AggContext.StaticData.ReadAllText(themePath);

					var themeConfig = JsonConvert.DeserializeObject<ThemeConfig>(json);
					themeConfig.EnsureDefaults();

					return themeConfig;
				}
			}
			catch
			{
				Console.WriteLine("Error loading theme: " + themeName);
			}

			return new ThemeConfig();
		}

		public static void SetThemeAccentColor(Color accentColor)
		{
			themeset.SetAccentColor(accentColor);
			AppContext.SetTheme(themeset);
		}

		public static void SetTheme(ThemeSet themeSet)
		{
			themeset = themeSet;

			File.WriteAllText(
				ProfileManager.Instance.ProfileThemeSetPath,
				JsonConvert.SerializeObject(
					themeset,
					Formatting.Indented,
					new JsonSerializerSettings
					{
						ContractResolver = new ThemeContractResolver()
					}));

			UiThread.RunOnIdle(() =>
			{
				UserSettings.Instance.set(UserSettingsKey.ActiveThemeName, themeset.Name);

				// Explicitly fire ReloadAll in response to user interaction
				ApplicationController.Instance.ReloadAll().ConfigureAwait(false);
			});
		}

		public class MatterControlOptions
		{
			public bool McwsTestEnvironment { get; set; }
		}
	}
}
