using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MatterHackers.MatterControl;
using MatterHackers.Agg;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
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
                foreach (string line in PreStartGCode())
                {
                    newStartGCode.Append(line + "\n");
                }

                newStartGCode.Append(ReplaceMacroValues(base.MappedValue));

                foreach (string line in PostStartGCode())
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

        string[] replaceWithSettingsStrings = new string[] 
        {
            "first_layer_temperature",
            "temperature", 
            "first_layer_bed_temperature",
            "bed_temperature",
        };

        private string ReplaceMacroValues(string gcodeWithMacros)
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

        public MapStartGCode(string mappedKey, string originalKey, bool replaceCRs)
            : base(mappedKey, originalKey)
        {
            this.replaceCRs = replaceCRs;
        }

        public List<string> PreStartGCode()
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
            
            string extruderTemperature = ActiveSliceSettings.Instance.GetActiveValue("temperature");
            if (extruderTemperature != "0")
            {
                bool printUsesExtruder2 = false;
                if(ActiveSliceSettings.Instance.ExtruderCount > 1
                    && printUsesExtruder2)
                {
                    // start heating the extra extruders 
                    //M104 T1 S190.000000
                }
                string setTempString = string.Format("M104 S{0}", extruderTemperature);
                AddDefaultIfNotPresent(preStartGCode, setTempString, preStartGCodeLines, "set temperature");
            }
            // make sure we are on extruder 0

            AddDefaultIfNotPresent(preStartGCode, "T0", preStartGCodeLines, "set the active extruder to 0");
            preStartGCode.Add("; settings from start_gcode");

            return preStartGCode;
        }

        public List<string> PostStartGCode()
        {
            string startGCode = ActiveSliceSettings.Instance.GetActiveValue("start_gcode");
            string[] postStartGCodeLines = startGCode.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);

            List<string> postStartGCode = new List<string>();
            postStartGCode.Add("; automatic settings after start_gcode");
            string extruderTemperature = ActiveSliceSettings.Instance.GetActiveValue("temperature");
            if (extruderTemperature != "0")
            {
                string setTempString = "M109 S{0}".FormatWith(extruderTemperature);
                AddDefaultIfNotPresent(postStartGCode, setTempString, postStartGCodeLines, "wait for temperature");
            }
            bool printUsesExtruder2 = false;
            double extruder2Temp = 0;
            if (ActiveSliceSettings.Instance.ExtruderCount > 1
                && printUsesExtruder2
                && extruder2Temp > 0)
            {
                // start heating the extra extruders 
                //M109 T1 S190.000000
            }

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
