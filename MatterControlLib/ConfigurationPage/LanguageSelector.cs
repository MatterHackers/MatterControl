using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public class LanguageSelector : MHDropDownList
	{
		private Dictionary<string, string> languageDict;

		public LanguageSelector(ThemeConfig theme)
			: base("Default", theme)
		{
			this.MinimumSize = new Vector2(this.LocalBounds.Width, this.LocalBounds.Height);
			CreateLanguageDict();

			foreach (KeyValuePair<string, string> entry in languageDict)
			{
				AddItem(entry.Key, entry.Value);
			}

			string languageCode = UserSettings.Instance.get(UserSettingsKey.Language);
			foreach (KeyValuePair<string, string> entry in languageDict)
			{
				if (languageCode == entry.Value)
				{
					SelectedLabel = entry.Key;
					break;
				}
			}
		}

		private void CreateLanguageDict()
		{
			languageDict = new Dictionary<string, string>
			{
				["Default"] = "EN",
				["English"] = "EN",
				["Čeština"] = "CS",
				["Chinese "] = "ZH",
				["Dansk"] = "DA",
				["Deutsch"] = "DE",
				["Español"] = "ES",
				["ελληνικά"] = "EL",
				["Français"] = "FR",
				["Italiano"] = "IT",
				["Japanese"] = "JA",
				["Norsk"] = "NO",
				["Polski"] = "PL",
				//["Português"] = "CR",
				["Русский"] = "RU",
				["Română"] = "RO",
				["Türkçe"] = "TR",
				["Vlaams"] = "NL",
			};

#if DEBUG
			languageDict["L10N"] = "L10N";
#endif
		}
	}
}