using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;

namespace MatterHackers.Localizations
{
    public class TraslationMap
    {
        TraslationMap()
        {
            string TwoLetterISOLanguageName = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
        }

        static TraslationMap instance = null;
        static TraslationMap Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new TraslationMap();
                }

                return instance;
            }
        }
    }
}
