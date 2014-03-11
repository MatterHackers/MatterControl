﻿//#define DEBUG_SHOW_TRANSLATED_STRINGS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.Localizations
{
    public static class LocalizedString
    {
        static TranslationMap MatterControlTranslationMap;

        public static string Get(string EnglishText)
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
}
