using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Xml;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Utilities;

namespace MatterHackers.MatterControl
{
    public class VersionInfo
    {
        static VersionInfoContainer globalInstance;        

        public static VersionInfoContainer Instance
        {
            get
            {
                if (globalInstance == null)
                {

                    VersionInfoHandler versionInfoHandler = new VersionInfoHandler();
                    globalInstance = versionInfoHandler.ImportVersionInfoFromJson();
                }
                return globalInstance;
            }
        }
    }

    public class VersionInfoContainer
    {        
        public VersionInfoContainer()
        {

        }

        public string ReleaseVersion{ get;set;}
        public string BuildVersion{ get;set;}
        public string BuildToken{ get;set;}
        public string ProjectToken{ get;set;}

    }


    class VersionInfoHandler
    {
        string defaultPathAndFileName;

        public VersionInfoHandler()
        {
            defaultPathAndFileName = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "BuildInfo.txt");   
        }
        
        public VersionInfoContainer ImportVersionInfoFromJson(string loadedFileName = null)
        {
            if (loadedFileName == null)
            {
                loadedFileName = defaultPathAndFileName;
            }

            if (System.IO.File.Exists(loadedFileName))
            {
                StreamReader sr = new System.IO.StreamReader(loadedFileName);
                VersionInfoContainer versionInfo = (VersionInfoContainer)Newtonsoft.Json.JsonConvert.DeserializeObject(sr.ReadToEnd(), typeof(VersionInfoContainer));
                sr.Close();
                if (versionInfo == null)
                {
                    return new VersionInfoContainer();
                }
                return versionInfo;
            }
            else
            {
                return null;
            }
        }

        
    }
}
