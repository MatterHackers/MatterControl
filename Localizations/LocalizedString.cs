﻿//#define DEBUG_SHOW_TRANSLATED_STRINGS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Localizations
{
    public class LocalizedString
    {
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
#if DEBUG_SHOW_TRANSLATED_STRINGS && DEBUG
                return "El " + EnglishText + " o";
#else
                return EnglishText;
#endif
            }
        }

        public LocalizedString(string EnglishText)
        {
            this.englishText = EnglishText;
        }
    }
}
