using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MatterHackers.MatterControl.DataStorage;
<<<<<<< HEAD
using Newtonsoft.Json;
=======
using MatterHackers.Agg.PlatformAbstract;
>>>>>>> Initial StaticData platform abstraction

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
                    //instance = new OemSettings();
                    //return instance;
                    string oemSettings = StaticData.Instance.ReadAllText(Path.Combine("OEMSettings", "Settings.json"));
                    instance = (OemSettings)Newtonsoft.Json.JsonConvert.DeserializeObject<OemSettings>(oemSettings);
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
