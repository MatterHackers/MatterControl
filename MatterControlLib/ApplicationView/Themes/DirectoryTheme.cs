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

namespace MatterHackers.MatterControl
{
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using MatterHackers.Agg.Platform;
	using Newtonsoft.Json;

	public class DirectoryTheme : IColorTheme
	{
		private string path;

		public DirectoryTheme()
		{
		}

		public DirectoryTheme(string directory)
		{
			path = directory;

			this.Name = Path.GetFileName(directory);
			this.ThemeNames = AggContext.StaticData.GetFiles(directory).Where(p => Path.GetExtension(p) == ".themeset").Select(p => Path.GetFileNameWithoutExtension(p)).ToArray();
		}

		public string Name { get; }

		public IEnumerable<string> ThemeNames { get; }

		public ThemeSet GetTheme(string themeName, Color accentColor)
		{
			var themeset = this.LoadTheme(themeName);
			themeset.SetAccentColor(accentColor);

			return themeset;
		}

		public ThemeSet GetTheme(string themeName)
		{
			var themeset = this.LoadTheme(themeName);

			try
			{
				var defaultColor = themeset.AccentColors[themeset.DefaultColorIndex];
				themeset.SetAccentColor(defaultColor);
			}
			catch
			{
				themeset.SetAccentColor(themeset.AccentColors.First());
			}

			return themeset;
		}

		private ThemeSet LoadTheme(string themeName)
		{
			ThemeSet themeset;
			try
			{
				themeset = JsonConvert.DeserializeObject<ThemeSet>(
					AggContext.StaticData.ReadAllText(Path.Combine(path, themeName + ".themeset")));
			}
			catch
			{
				themeset = JsonConvert.DeserializeObject<ThemeSet>(
					AggContext.StaticData.ReadAllText(Path.Combine(path, this.ThemeNames.First() + ".themeset")));
			}

			themeset.Theme = AppContext.LoadTheme(themeset.ThemeID);
			themeset.MenuTheme = AppContext.LoadTheme(themeset.MenuThemeID);

			// Set SchemaVersion at construction time
			themeset.SchemeVersion = ThemeSet.LatestSchemeVersion;
			themeset.ThemesetID = themeName;

			return themeset;
		}
	}
}