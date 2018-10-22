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

	public class SerializedTheme : IColorTheme
	{
		public string Name { get; set; }

		public Color DefaultColor { get; set; }

		public IEnumerable<string> ThemeNames { get; set; }

		public ThemeSet GetTheme(string mode, Color accentColor)
		{
			throw new System.NotImplementedException();
		}
	}

	public class DirectoryTheme : IColorTheme
	{
		private string path;
		public DirectoryTheme()
		{
		}

		public DirectoryTheme(string directory)
		{
			var themeSetData = JsonConvert.DeserializeObject<SerializedTheme>(
				AggContext.StaticData.ReadAllText(Path.Combine(directory, "theme.json")));

			path = directory;

			this.Name = Path.GetFileName(directory);
			this.ThemeNames = AggContext.StaticData.GetFiles(directory).Where(p => Path.GetFileName(p) != "theme.json").Select(p => Path.GetFileNameWithoutExtension(p)).ToArray();

			this.DefaultColor = themeSetData.DefaultColor;
		}

		public string Name { get; }

		public Color DefaultColor { get; }

		public IEnumerable<string> ThemeNames { get; }

		public ThemeSet GetTheme(string themeName, Color accentColor)
		{
			var themeset = JsonConvert.DeserializeObject<ThemeSet>(
				AggContext.StaticData.ReadAllText(Path.Combine(path, themeName + ".json")));

			themeset.ThemeID = themeName;
			themeset.SetAccentColor(accentColor);

			return themeset;
		}
	}
}