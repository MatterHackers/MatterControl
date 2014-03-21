using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MatterHackers.MatterControl;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
    public class MapItem
    {
        string curaKey;
        string defaultKey;

        internal MapItem(string curaKey, string defaultKey)
        {
            this.curaKey = curaKey;
            this.defaultKey = defaultKey;
        }

        public string CuraKey { get { return curaKey; } }
        public string DefaultKey { get { return defaultKey; } }

        public string SlicerValue { get { return ActiveSliceSettings.Instance.GetActiveValue(defaultKey); } }

        public virtual string TranslatedValue { get { return SlicerValue; } }
    }

    public class NotPassedItem : MapItem
    {
        public override string TranslatedValue
        {
            get
            {
                return null;
            }
        }

        public NotPassedItem(string cura, string slicer)
            : base(cura, slicer)
        {
        }
    }

    public class MapItemToBool : MapItem
    {
        public override string TranslatedValue
        {
            get
            {
                if (base.TranslatedValue == "1")
                {
                    return "True";
                }

                return "False";
            }
        }

        public MapItemToBool(string cura, string slicer)
            : base(cura, slicer)
        {
        }
    }

    public class ScaledSingleNumber : MapItem
    {
        internal double scale;
        public override string TranslatedValue
        {
            get
            {
                if (scale != 1)
                {
                    return (double.Parse(base.TranslatedValue) * scale).ToString();
                }
                return base.TranslatedValue;
            }
        }

        internal ScaledSingleNumber(string cura, string slicer, double scale = 1)
            : base(cura, slicer)
        {
            this.scale = scale;
        }
    }

    public class AsPercentOfReferenceOrDirect : ScaledSingleNumber
    {
        internal string slicerReference;
        public override string TranslatedValue
        {
            get
            {
                if (SlicerValue.Contains("%"))
                {
                    string withoutPercent = SlicerValue.Replace("%", "");
                    double ratio = double.Parse(withoutPercent) / 100.0;
                    string slicerReferenceString = ActiveSliceSettings.Instance.GetActiveValue(slicerReference);
                    double valueToModify = double.Parse(slicerReferenceString);
                    double finalValue = valueToModify * ratio * scale;
                    return finalValue.ToString();
                }

                return base.TranslatedValue;
            }
        }

        internal AsPercentOfReferenceOrDirect(string cura, string slicer, string slicerReference, double scale = 1)
            : base(cura, slicer, scale)
        {
            this.slicerReference = slicerReference;
        }
    }
}
