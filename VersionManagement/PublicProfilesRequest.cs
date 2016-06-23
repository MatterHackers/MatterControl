using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SettingsManagement;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.VersionManagement
{
	class PublicProfilesRequest : WebRequestBase
	{
		public PublicProfilesRequest()
		{
			//requestValues["ProjectToken"] = VersionInfo.Instance.ProjectToken;
#if DEBUG
			uri = "https://mattercontrol-test.appspot.com/api/1/device/get-public-profile-list";
#else
			uri = "https://mattercontrol.appspot.com/api/1/device/get-public-profile-list";
#endif
		}

		internal string URI { get { return uri; } set { uri = value; } }

		public override void ProcessSuccessResponse(JsonResponseDictionary responseValues)
		{
			//For now, refresh every time
			string cacheDirectory = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "temp", "cache", "profiles");
			//Ensure directory exists
			Directory.CreateDirectory(cacheDirectory);
			//Cache File Path
			string cachePath = Path.Combine(cacheDirectory, "oemprofiles.json");
			File.WriteAllText(cachePath, responseValues["ProfileList"]);
			

			OemSettings.Instance.OemProfiles = JsonConvert.DeserializeObject<Dictionary<string,Dictionary<string,string>>>(responseValues["ProfileList"]);
			OemSettings.Instance.SetManufacturers(OemSettings.Instance.OemProfiles.Select(m => new KeyValuePair<string, string>(m.Key, m.Key)).ToList());
		}


	}
}
