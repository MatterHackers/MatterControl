using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Text;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
    public class ActivePrinterProfile
    {
        static ActivePrinterProfile globalInstance = null;

        // private so that it can only be gotten through the Instance
        ActivePrinterProfile()
        {
        }

        public Printer ActivePrinter { get; set; }

        public static ActivePrinterProfile Instance
        {
            get
            {
                if (globalInstance == null)
                {
                    globalInstance = new ActivePrinterProfile();
                }

                return globalInstance;
            }

            set
            {
                if (globalInstance != value)
                {
                    PrinterCommunication.Instance.Disable();
                    globalInstance = value;
                    PrinterCommunication.Instance.OnActivePrinterChanged(null);
                }
            }
        }
    }
}