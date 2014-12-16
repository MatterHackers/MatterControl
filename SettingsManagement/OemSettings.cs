﻿/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met: 

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer. 
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution. 

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies, 
either expressed or implied, of the FreeBSD Project.
*/

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
#if false
                    string output = JsonConvert.SerializeObject(instance, Formatting.Indented);
                    using (StreamWriter outfile = new StreamWriter("Settings.json"))
                    {
                        outfile.Write(output);
                    }
#endif
                }

                return instance;
            }
        }

        public bool UseSimpleModeByDefault = false;

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
            string pathToOemSettings = Path.Combine(".", "OEMSettings", "Settings.json");
            File.WriteAllText(pathToOemSettings, JsonConvert.SerializeObject(this, Formatting.Indented));
#endif
        }
    }
}
