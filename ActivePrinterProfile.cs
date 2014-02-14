using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Globalization;

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

        public RootedObjectEventHandler ActivePrinterChanged = new RootedObjectEventHandler();

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
                    globalInstance.OnActivePrinterChanged(null);
                }
            }
        }

        public void OnActivePrinterChanged(EventArgs e)
        {
            ActivePrinterChanged.CallEvents(this, e);
        }

        public static void CheckForAndDoAutoConnect()
        {
            DataStorage.Printer autoConnectProfile = ActivePrinterProfile.GetAutoConnectProfile();
            if (autoConnectProfile != null)
            {
                ActivePrinterProfile.Instance.ActivePrinter = autoConnectProfile;
                PrinterCommunication.Instance.HaltConnectionThread();
                PrinterCommunication.Instance.ConnectToActivePrinter();
            }
        }

        public static DataStorage.Printer GetAutoConnectProfile()
        {
            string query = string.Format("SELECT * FROM Printer;");
            IEnumerable<Printer> printer_profiles = (IEnumerable<Printer>)Datastore.Instance.dbSQLite.Query<Printer>(query);
            string[] comportNames = SerialPort.GetPortNames();

            foreach (DataStorage.Printer printer in printer_profiles)
            {
                if (printer.AutoConnectFlag)
                {
                    bool portIsAvailable = comportNames.Contains(printer.ComPort);
                    if (portIsAvailable)
                    {
                        return printer;
                    }
                }
            }
            return null;
        }
    }
}