/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met: 

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer. 
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution. 

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies, 
either expressed or implied, of the FreeBSD Project.
*/

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
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
    public class ActivePrinterProfile
    {
        public enum SlicingEngineTypes { Slic3r, CuraEngine, MatterSlice };

        static readonly SlicingEngineTypes defaultEngineType = SlicingEngineTypes.Slic3r;
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

        public SlicingEngineTypes ActiveSliceEngineType
        {
            get
            {
                if (ActivePrinter != null)
                {
                    foreach (SlicingEngineTypes engine in SlicingEngineTypes.GetValues(typeof(SlicingEngineTypes)))
                    {
                        if (ActivePrinter.CurrentSlicingEngine == engine.ToString())
                        {
                            return engine;
                        }
                    }

                    // It is not set in the slice settings, so set it and save it.
                    ActivePrinter.CurrentSlicingEngine = defaultEngineType.ToString();
                    ActivePrinter.Commit();
                }
                return defaultEngineType;
            }

            set
            {
                if (ActiveSliceEngineType != value)
                {
                    ActivePrinter.CurrentSlicingEngine = value.ToString();
                    ActivePrinter.Commit();
                }
            }
        }

        public SliceEngineMaping ActiveSliceEngine
        {
            get
            {
                switch (ActiveSliceEngineType)
                {
                    case SlicingEngineTypes.CuraEngine:
                        return EngineMappingCura.Instance;

                    case SlicingEngineTypes.MatterSlice:
                        return EngineMappingsMatterSlice.Instance;

                    case SlicingEngineTypes.Slic3r:
                        return Slic3rEngineMappings.Instance;

                    default:
                        return null;
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
                            GetPrintLevelingMeasuredPosition(0),
                            GetPrintLevelingMeasuredPosition(1),
                            GetPrintLevelingMeasuredPosition(2),
                            ActiveSliceSettings.Instance.PrintCenter);
                    }
                }
            }
        }

        /// <summary>
        /// This function returns one of the three positions as it was actually measured
        /// </summary>
        /// <param name="position0To2"></param>
        /// <returns></returns>
        public Vector3 GetPrintLevelingMeasuredPosition(int position0To2)
        {
            if (ActivePrinter != null)
            {
                double[] positions = ActivePrinter.GetPrintLevelingMeasuredPositions();
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

        public void SetPrintLevelingMeasuredPositions(double[] printLevelingPositions3_xyz)
        {
            if (ActivePrinter != null)
            {
                ActivePrinter.SetPrintLevelingMeasuredPositions(printLevelingPositions3_xyz);
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