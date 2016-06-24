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

		public JsonResponseDictionary ResponseValues { get; set; }

		public override void ProcessSuccessResponse(JsonResponseDictionary responseValues)
		{
			ResponseValues = responseValues;
		}
	}
}
