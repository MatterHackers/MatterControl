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

        public virtual string CuraValue { get { return SlicerValue; } }
    }
}
