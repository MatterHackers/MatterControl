using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MatterHackers.MatterControl.DataStorage;
using Newtonsoft.Json;
using MatterHackers.Agg.PlatformAbstract;

namespace MatterHackers.MatterControl.SettingsManagement
{
    public class OemSettings
    {
        static OemSettings instance = null;

        public static OemSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    string oemSettings = StaticData.Instance.ReadAllText(Path.Combine("OEMSettings", "Settings.json"));
                    instance = JsonConvert.DeserializeObject<OemSettings>(oemSettings) as OemSettings;
                }

                return instance;
            }
        }

        public string ThemeColor = "";

        public string AffiliateCode = "";

        public string WindowTitleExtra = "";

        public bool ShowShopButton = true;

        public bool CheckForUpdatesOnFirstRun = false;
        
        List<string> printerWhiteList = new List<string>();
        public List<string> PrinterWhiteList { get { return printerWhiteList; } }

        // TODO: Is this ever initialized and if so, how, given there's no obvious references and only one use of the property
        List<string> preloadedLibraryFiles = new List<string>();
        public List<string> PreloadedLibraryFiles { get { return preloadedLibraryFiles; } }

        OemSettings()
        {
#if false // test saving the file
            printerWhiteList.Add("one");
            printerWhiteList.Add("two");
            PreloadedLibraryFiles.Add("uno");
            PreloadedLibraryFiles.Add("dos");
            AffiliateCode = "testcode";
            string pathToOemSettings = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "OEMSettings", "Settings.json");
            File.WriteAllText(pathToOemSettings, JsonConvert.SerializeObject(this));
#endif
        }
    }
}
