using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MatterHackers.MatterControl;

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
                if (scale != 1)
                {
                    return (double.Parse(base.MappedValue) * scale).ToString();
                }
                return base.MappedValue;
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
                    double ratio = double.Parse(withoutPercent) / 100.0;
                    string originalReferenceString = ActiveSliceSettings.Instance.GetActiveValue(originalReference);
                    double valueToModify = double.Parse(originalReferenceString);
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
