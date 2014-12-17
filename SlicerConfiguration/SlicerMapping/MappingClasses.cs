using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MatterHackers.MatterControl;
using MatterHackers.Agg;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
    public static class GCodeProcessing
    {
        static string[] replaceWithSettingsStrings = new string[] 
        {
            "first_layer_temperature",
            "temperature", 
            "first_layer_bed_temperature",
            "bed_temperature",
        };

        public static string ReplaceMacroValues(string gcodeWithMacros)
        {
            foreach (string name in replaceWithSettingsStrings)
            {
                // do the replacement with {} (curly brackets)
                {
                    string thingToReplace = "{" + "{0}".FormatWith(name) + "}";
                    gcodeWithMacros = gcodeWithMacros.Replace(thingToReplace, ActiveSliceSettings.Instance.GetActiveValue(name));
                }
                // do the replacement with [] (square brackets)
                {
                    string thingToReplace = "[" + "{0}".FormatWith(name) + "]";
                    gcodeWithMacros = gcodeWithMacros.Replace(thingToReplace, ActiveSliceSettings.Instance.GetActiveValue(name));
                }
            }

            return gcodeWithMacros;
        }
    }

    public class MapItem
    {
        string mappedKey;
        string originalKey;

        public MapItem(string mappedKey, string originalKey)
        {
            this.mappedKey = mappedKey;
            this.originalKey = originalKey;
        }

        protected static double ParseValueString(string valueString, double valueOnError = 0)
        {
            double value = valueOnError;

            if (!double.TryParse(valueString, out value))
            {
#if DEBUG
                throw new Exception("Slicing value is not a double.");
#endif
            }

            return value;
        }

        public static double GetValueForKey(string originalKey, double valueOnError = 0)
        {
            return ParseValueString(ActiveSliceSettings.Instance.GetActiveValue(originalKey), valueOnError);
        }

        public string MappedKey { get { return mappedKey; } }
        public string OriginalKey { get { return originalKey; } }

        public string OriginalValue { get { return ActiveSliceSettings.Instance.GetActiveValue(originalKey); } }

        public virtual string MappedValue { get { return OriginalValue; } }
    }

    public class NotPassedItem : MapItem
    {
        public override string MappedValue
        {
            get
            {
                return null;
            }
        }

        public NotPassedItem(string mappedKey, string originalKey)
            : base(mappedKey, originalKey)
        {
        }
    }

    public class MapStartGCode : InjectGCodeCommands
    {
        bool replaceCRs;

        public override string MappedValue
        {
            get
            {
                StringBuilder newStartGCode = new StringBuilder();
                foreach (string line in PreStartGCode(SlicingQueue.extrudersUsed))
                {
                    newStartGCode.Append(line + "\n");
                }

                newStartGCode.Append(GCodeProcessing.ReplaceMacroValues(base.MappedValue));

                foreach (string line in PostStartGCode(SlicingQueue.extrudersUsed))
                {
                    newStartGCode.Append("\n");
                    newStartGCode.Append(line);
                }

                if (replaceCRs)
                {
                    return newStartGCode.ToString().Replace("\n", "\\n");
                }

                return newStartGCode.ToString();
            }
        }

        public MapStartGCode(string mappedKey, string originalKey, bool replaceCRs)
            : base(mappedKey, originalKey)
        {
            this.replaceCRs = replaceCRs;
        }

        public List<string> PreStartGCode(List<bool> extrudersUsed)
        {
            string startGCode = ActiveSliceSettings.Instance.GetActiveValue("start_gcode");
            string[] preStartGCodeLines = startGCode.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);

            List<string> preStartGCode = new List<string>();
            preStartGCode.Add("; automatic settings before start_gcode");
            AddDefaultIfNotPresent(preStartGCode, "G21", preStartGCodeLines, "set units to millimeters");
            AddDefaultIfNotPresent(preStartGCode, "M107", preStartGCodeLines, "fan off");
            double bed_temperature = MapItem.GetValueForKey("bed_temperature");
            if (bed_temperature > 0)
            {
                string setBedTempString = string.Format("M190 S{0}", bed_temperature);
                AddDefaultIfNotPresent(preStartGCode, setBedTempString, preStartGCodeLines, "wait for bed temperature to be reached");
            }


            int numberOfHeatedExtruders = 1;
            if (!ActiveSliceSettings.Instance.ExtrudersShareTemperature)
            {
                numberOfHeatedExtruders = ActiveSliceSettings.Instance.ExtruderCount;
            }

            for (int i = 0; i < numberOfHeatedExtruders; i++)
            {
                if (extrudersUsed.Count > i
                    && extrudersUsed[i])
                {
                    string materialTemperature = ActiveSliceSettings.Instance.GetMaterialValue("temperature", i);
                    if (materialTemperature != "0")
                    {
                        string setTempString = "M104 T{0} S{1}".FormatWith(i, materialTemperature);
                        AddDefaultIfNotPresent(preStartGCode, setTempString, preStartGCodeLines, string.Format("start heating extruder {0}", i));
                    }
                }
            }

            SwitchToFirstActiveExtruder(extrudersUsed, preStartGCodeLines, preStartGCode);
            preStartGCode.Add("; settings from start_gcode");

            return preStartGCode;
        }

        private void SwitchToFirstActiveExtruder(List<bool> extrudersUsed, string[] preStartGCodeLines, List<string> preStartGCode)
        {
            // make sure we are on the first active extruder
            for (int i = 0; i < extrudersUsed.Count; i++)
            {
                if (extrudersUsed[i])
                {
                    // set the active extruder to the first one that will be printing
                    AddDefaultIfNotPresent(preStartGCode, "T{0}".FormatWith(i), preStartGCodeLines, "set the active extruder to {0}".FormatWith(i));
                    break; // then break so we don't set it to a different ones
                }
            }
        }

        public List<string> PostStartGCode(List<bool> extrudersUsed)
        {
            string startGCode = ActiveSliceSettings.Instance.GetActiveValue("start_gcode");
            string[] postStartGCodeLines = startGCode.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);

            List<string> postStartGCode = new List<string>();
            postStartGCode.Add("; automatic settings after start_gcode");

            int numberOfHeatedExtruders = 1;
            if (!ActiveSliceSettings.Instance.ExtrudersShareTemperature)
            {
                numberOfHeatedExtruders = ActiveSliceSettings.Instance.ExtruderCount;
            }

            for (int i = 0; i < numberOfHeatedExtruders; i++)
            {
                if (extrudersUsed.Count > i
                    && extrudersUsed[i])
                {
                    string materialTemperature = ActiveSliceSettings.Instance.GetMaterialValue("temperature", i + 1);
                    if (materialTemperature != "0")
                    {
                        string setTempString = "M109 T{0} S{1}".FormatWith(i, materialTemperature);
                        AddDefaultIfNotPresent(postStartGCode, setTempString, postStartGCodeLines, string.Format("wait for extruder {0} to reach temperature", i));
                    }
                }
            }

            SwitchToFirstActiveExtruder(extrudersUsed, postStartGCodeLines, postStartGCode);
            AddDefaultIfNotPresent(postStartGCode, "G90", postStartGCodeLines, "use absolute coordinates");
            postStartGCode.Add(string.Format("{0} ; {1}", "G92 E0", "reset the expected extruder position"));
            AddDefaultIfNotPresent(postStartGCode, "M82", postStartGCodeLines, "use absolute distance for extrusion");

            return postStartGCode;
        }
    }

    public class MapItemToBool : MapItem
    {
        public override string MappedValue
        {
            get
            {
                if (base.MappedValue == "1")
                {
                    return "True";
                }

                return "False";
            }
        }

        public MapItemToBool(string mappedKey, string originalKey)
            : base(mappedKey, originalKey)
        {
        }
    }

    public class ScaledSingleNumber : MapItem
    {
        internal double scale;
        public override string MappedValue
        {
            get
            {
                double ratio = 0;
                if (OriginalValue.Contains("%"))
                {
                    string withoutPercent = OriginalValue.Replace("%", "");
                    ratio = MapItem.ParseValueString(withoutPercent) / 100.0;
                }
                else
                {
                     ratio = MapItem.ParseValueString(base.MappedValue);
                }

                return (ratio * scale).ToString();
            }
        }

        internal ScaledSingleNumber(string mappedKey, string originalKey, double scale = 1)
            : base(mappedKey, originalKey)
        {
            this.scale = scale;
        }
    }

    public class InjectGCodeCommands : ConvertCRs
    {
        public InjectGCodeCommands(string mappedKey, string originalKey)
            : base(mappedKey, originalKey)
        {
        }

        protected void AddDefaultIfNotPresent(List<string> linesAdded, string commandToAdd, string[] linesToCheckIfAlreadyPresent, string comment)
        {
            string command = commandToAdd.Split(' ')[0].Trim();
            bool foundCommand = false;
            foreach (string line in linesToCheckIfAlreadyPresent)
            {
                if (line.StartsWith(command))
                {
                    foundCommand = true;
                    break;
                }
            }

            if (!foundCommand)
            {
                linesAdded.Add(string.Format("{0} ; {1}", commandToAdd, comment));
            }
        }
    }

    public class ConvertCRs : MapItem
    {
        public override string MappedValue
        {
            get
            {
                string actualCRs = base.MappedValue.Replace("\\n", "\n");
                return actualCRs;
            }
        }

        public ConvertCRs(string mappedKey, string originalKey)
            : base(mappedKey, originalKey)
        {
        }
    }

    public class AsPercentOfReferenceOrDirect : ScaledSingleNumber
    {
        internal string originalReference;
        public override string MappedValue
        {
            get
            {
                if (OriginalValue.Contains("%"))
                {
                    string withoutPercent = OriginalValue.Replace("%", "");
                    double ratio = MapItem.ParseValueString(withoutPercent) / 100.0;
                    string originalReferenceString = ActiveSliceSettings.Instance.GetActiveValue(originalReference);
                    double valueToModify = MapItem.ParseValueString(originalReferenceString);
                    double finalValue = valueToModify * ratio * scale;
                    return finalValue.ToString();
                }

                return base.MappedValue;
            }
        }

        public AsPercentOfReferenceOrDirect(string mappedKey, string originalKey, string originalReference, double scale = 1)
            : base(mappedKey, originalKey, scale)
        {
            this.originalReference = originalReference;
        }
    }
}
