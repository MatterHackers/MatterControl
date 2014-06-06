using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
    public class PrintLevelingData
    {
        static bool activelyLoading = false;

        [JsonConverter(typeof(StringEnumConverter))]
        public enum LevelingSystem { Probe3Points, Probe2Points }

        [JsonIgnoreAttribute ]
        Vector3 position0Private;
        public Vector3 position0
        {
            get { return position0Private; }
            set
            {
                if (position0Private != value)
                {
                    position0Private = value;
                    Commit();
                }
            }
        }

        Vector3 position1Private;
        public Vector3 position1
        {
            get { return position1Private; }
            set
            {
                if (position1Private != value)
                {
                    position1Private = value;
                    Commit();
                }
            }
        }

        Vector3 position2Private;
        public Vector3 position2
        {
            get { return position2Private; }
            set
            {
                if (position2Private != value)
                {
                    position2Private = value;
                    Commit();
                }
            }
        }

        LevelingSystem levelingSystemPrivate = LevelingSystem.Probe3Points;
        public LevelingSystem levelingSystem
        {
            get { return levelingSystemPrivate; }
            set
            {
                if (value != levelingSystemPrivate)
                {
                    levelingSystemPrivate = value;
                    Commit();
                }
            }
        }

        bool needsPrintLevelingPrivate;
        public bool needsPrintLeveling 
        {
            get { return needsPrintLevelingPrivate; }
            set
            {
                if (needsPrintLevelingPrivate != value)
                {
                    needsPrintLevelingPrivate = value;
                    Commit();
                }
            }
        }

        void Commit()
        {
            if (!activelyLoading)
            {
                string newLevelingInfo = Newtonsoft.Json.JsonConvert.SerializeObject(this);

                // clear the legacy value
                ActivePrinterProfile.Instance.ActivePrinter.PrintLevelingProbePositions = "";
                // set the new value
                ActivePrinterProfile.Instance.ActivePrinter.PrintLevelingJsonData = newLevelingInfo;
                ActivePrinterProfile.Instance.ActivePrinter.Commit();
            }
        }

        public static PrintLevelingData CreateFromJsonOrLegacy(string jsonData, string depricatedPositionsCsv3ByXYZ)
        {
            PrintLevelingData instance;
            if (jsonData != null)
            {
                activelyLoading = true;
                instance = (PrintLevelingData)Newtonsoft.Json.JsonConvert.DeserializeObject<PrintLevelingData>(jsonData);
                activelyLoading = false;
            }
            else if (depricatedPositionsCsv3ByXYZ != null)
            {
                instance = new PrintLevelingData();
                instance.ParseDepricatedPrintLevelingMeasuredPositions(depricatedPositionsCsv3ByXYZ);
            }
            else
            {
                instance = new PrintLevelingData();
            }

            return instance;
        }
        
        /// <summary>
        /// Gets the 9 {3 * (x, y, z)} positions that were probed during the print leveling setup.
        /// </summary>
        /// <returns></returns>
        void ParseDepricatedPrintLevelingMeasuredPositions(string depricatedPositionsCsv3ByXYZ)
        {
            if (depricatedPositionsCsv3ByXYZ != null)
            {
                string[] lines = depricatedPositionsCsv3ByXYZ.Split(',');
                if (lines.Length == 9)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        position0Private[i % 3] = double.Parse(lines[0 * 3 + i]);
                        position1Private[i % 3] = double.Parse(lines[1 * 3 + i]);
                        position2Private[i % 3] = double.Parse(lines[2 * 3 + i]);
                    }
                }
            }
        }
    }
}
