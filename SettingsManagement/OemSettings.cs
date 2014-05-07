using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MatterHackers.MatterControl.DataStorage;

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
                    string pathToOemSettings = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "OEMSettings", "Settings.json");
                    string oemSettings = File.ReadAllText(pathToOemSettings);
                    instance = (OemSettings)Newtonsoft.Json.JsonConvert.DeserializeObject<OemSettings>(oemSettings);
                }

                return instance;
            }
        }

        public string AffiliateCode = "";

        public string WindowTitleExtra = "";

        public bool ShowShopButton = true;
        
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
            affiliateCode = "testcode";
            string pathToOemSettings = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "OEMSettings", "Settings.json");
            File.WriteAllText(pathToOemSettings, Newtonsoft.Json.JsonConvert.SerializeObject(this));
#endif
        }
    }
}
