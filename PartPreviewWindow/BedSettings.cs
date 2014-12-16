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

using System.IO;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;

namespace MatterHackers.MatterControl.SettingsManagement
{
    public class BedSettings
    {
        static BedSettings instance = null;

        public static BedSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new BedSettings();
                }

                return instance;
            }
        }

        public static void SetMakeAndModel(string make, string model)
        {
			string pathToBedSettings = Path.Combine("PrinterSettings", make, model, "BedSettings.json");
			if (StaticData.Instance.FileExists(pathToBedSettings))
			{
				string content = StaticData.Instance.ReadAllText(pathToBedSettings);
				instance = (BedSettings)Newtonsoft.Json.JsonConvert.DeserializeObject<BedSettings>(content);
			}
			else
			{
				instance = new BedSettings();
			}
        }

        public RectangleInt ActualBedInImage;

        BedSettings()
        {
            //File.WriteAllText("test.json", Newtonsoft.Json.JsonConvert.SerializeObject(this));
        }
    }
}
