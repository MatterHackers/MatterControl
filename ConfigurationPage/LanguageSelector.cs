﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
    public class LanguageSelector : StyledDropDownList
    {
        Dictionary<string, string> languageDict;

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
            languageDict["Español"] = "ES";
			//languageDict["Français"] = "FR";
            languageDict["Deutsch"] = "DE";
			languageDict["Polski"] = "PL";
			languageDict["Türkçe"] = "TR";
        }
    }
}
