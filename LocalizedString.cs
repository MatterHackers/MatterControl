﻿//#define DEBUG_SHOW_TRANSLATED_STRINGS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.Localizations
{
    public class LocalizedString
    {
        static TranslationMap MatterControlTranslationMap;

        string englishText;
        string EnglishText 
        {
            get
            {
                return englishText;
            }
        }

        public string Translated
        {
            get
            {
                string language = "fr";
                if (language == "en")
                {
                    return EnglishText;
                }
                else
                {
                    if (MatterControlTranslationMap == null)
                    {
                        string pathToTranslationsFolder = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "Translations");
                        MatterControlTranslationMap = new TranslationMap(pathToTranslationsFolder, language);
                    }
#if DEBUG_SHOW_TRANSLATED_STRINGS && DEBUG
                return "El " + EnglishText + " o";
#else
                    return MatterControlTranslationMap.Translate(EnglishText);
                }
#endif
            }
        }

        public LocalizedString(string EnglishText)
        {
            this.englishText = EnglishText;
        }
    }
}
