using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
    class SliceSettingsLayerSelector
    {
        static SliceSettingsLayerSelector globalInstance = null;
        int activeLayerIndex = 1;

        public static SliceSettingsLayerSelector Instance
        {
            get
            {
                if (globalInstance == null)
                {
                    globalInstance = new SliceSettingsLayerSelector();
                }

                return globalInstance;
            }
        }

        public void SaveSetting(string settingKey, string settingValue)
        {
            ActiveSliceSettings.Instance.SaveValue(settingKey, settingValue, this.activeLayerIndex);
        }
    }
}
