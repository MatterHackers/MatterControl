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
using MatterHackers.Agg.PlatformAbstract;

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
        public VersionInfoHandler()
        {
        }
        
        public VersionInfoContainer ImportVersionInfoFromJson(string loadedFileName = null)
        {
            // TODO: Review all cases below. What happens if we return a default instance of VersionInfoContainer or worse yet, null. It seems likely we end up with a null
            // reference error when someone attempts to use VersionInfo.Instance and it's invalide. Consider removing the error handing below and throwing an error when
            // an error condition is found rather than masking it until the user goes to a section that relies on the instance - thus moving detection of the problem to
            // an earlier stage and expanding the number of cases where it would be noticed.
            string content = loadedFileName == null ? 
                    StaticData.Instance.ReadAllText(Path.Combine("BuildInfo.txt")) :
                    System.IO.File.Exists(loadedFileName) ? System.IO.File.ReadAllText(loadedFileName) : "";
            
            if (!string.IsNullOrWhiteSpace(content))
            {
                VersionInfoContainer versionInfo = (VersionInfoContainer)Newtonsoft.Json.JsonConvert.DeserializeObject(content, typeof(VersionInfoContainer));
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
