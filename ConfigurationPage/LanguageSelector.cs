using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public class LanguageSelector : DropDownList
	{
		private Dictionary<string, string> languageDict;

		public LanguageSelector()
			: base("Default")
		{
			this.MinimumSize = new Vector2(this.LocalBounds.Width, this.LocalBounds.Height);

			CreateLanguageDict();

			string languageCode = UserSettings.Instance.get("Language");

			foreach (KeyValuePair<string, string> entry in languageDict)
			{
				AddItem(entry.Key, entry.Value);
			}

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
			languageDict = new Dictionary<string, string>();
			languageDict["Default"] = "EN";
			languageDict["English"] = "EN";
			languageDict["Deutsch"] = "DE";
			languageDict["Español"] = "ES";
			languageDict["Français"] = "FR";
			languageDict["ελληνικά"] = "EL";
			languageDict["Polski"] = "PL";
			languageDict["Türkçe"] = "TR";
#if DEBUG
			languageDict["L10N"] = "L10N";
#endif
		}
	}
}