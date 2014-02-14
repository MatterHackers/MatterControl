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
        public enum SlicingEngine { Slic3r, CuraEngine, MatterSlice };

        static readonly SlicingEngine defaultEngine = SlicingEngine.Slic3r;
        static ActivePrinterProfile globalInstance = null;

        public RootedObjectEventHandler ActivePrinterChanged = new RootedObjectEventHandler();
        public RootedObjectEventHandler DoPrintLevelingChanged = new RootedObjectEventHandler();

        // private so that it can only be gotten through the Instance
        ActivePrinterProfile()
        {
        }

        Printer activePrinter = null;
        public Printer ActivePrinter 
        {
            get { return activePrinter; }
            set 
            {
                if (activePrinter != value)
                {
                    PrinterCommunication.Instance.Disable();
                    activePrinter = value;
                    globalInstance.OnActivePrinterChanged(null);
                }
            } 
        }

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
        }

        public SlicingEngine ActiveSliceEngine
        {
            get
            {
                if (ActivePrinter != null)
                {
                    foreach (SlicingEngine engine in SlicingEngine.GetValues(typeof(SlicingEngine)))
                    {
                        if (ActivePrinter.CurrentSlicingEngine == engine.ToString())
                        {
                            return engine;
                        }
                    }

                    // It is not set in the slice settings, so set it and save it.
                    ActivePrinter.CurrentSlicingEngine = defaultEngine.ToString();
                    ActivePrinter.Commit();
                }
                return defaultEngine;
            }

            set
            {
                if (ActiveSliceEngine != value)
                {
                    ActivePrinter.CurrentSlicingEngine = value.ToString();
                    ActivePrinter.Commit();
                }
            }
        }

        public void OnActivePrinterChanged(EventArgs e)
        {
            ActivePrinterChanged.CallEvents(this, e);
        }

        public bool DoPrintLeveling
        {
            get
            {
                if (ActivePrinter != null)
                {
                    return ActivePrinter.DoPrintLeveling;
                }
                return false;
            }

            set
            {
                if (ActivePrinter != null && ActivePrinter.DoPrintLeveling != value)
                {
                    ActivePrinter.DoPrintLeveling = value;
                    DoPrintLevelingChanged.CallEvents(this, null);
                    ActivePrinter.Commit();

                    if (DoPrintLeveling)
                    {
                        PrintLeveling.Instance.SetPrintLevelingEquation(
                            GetPrintLevelingProbePosition(0),
                            GetPrintLevelingProbePosition(1),
                            GetPrintLevelingProbePosition(2),
                            ActiveSliceSettings.Instance.PrintCenter);
                    }
                }
            }
        }

        /// <summary>
        /// This function returns one of the three positions that will be probed when setting
        /// up print leveling.
        /// </summary>
        /// <param name="position0To2"></param>
        /// <returns></returns>
        public Vector3 GetPrintLevelingProbePosition(int position0To2)
        {
            if (ActivePrinter != null)
            {
                double[] positions = ActivePrinter.GetPrintLevelingPositions();
                switch (position0To2)
                {
                    case 0:
                        return new Vector3(positions[0], positions[1], positions[2]);
                    case 1:
                        return new Vector3(positions[3], positions[4], positions[5]);
                    case 2:
                        return new Vector3(positions[6], positions[7], positions[8]);
                    default:
                        throw new Exception("there are only 3 probe positions.");
                }
            }

            return Vector3.Zero;
        }

        public void SetPrintLevelingProbePositions(double[] printLevelingPositions3_xyz)
        {
            if (ActivePrinter != null)
            {
                ActivePrinter.SetPrintLevelingPositions(printLevelingPositions3_xyz);
                ActivePrinter.Commit();
            }
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