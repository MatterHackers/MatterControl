using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using System.IO;
using System.Net;

namespace MatterHackers.MatterControl.VersionManagement
{
	public class RetrievePublicProfileRequest
	{
#if DEBUG
		internal static string DownloadBaseUrl { get; } = "https://mattercontrol-test.appspot.com/api/1/device/get-public-profile";
#else
		internal static string DownloadBaseUrl { get; } = "https://mattercontrol.appspot.com/api/1/device/get-public-profile";
#endif

		internal static string DownloadPrinterProfile(string deviceToken)
		{
			// Keep track of version. When retrieving check version
			string url = DownloadBaseUrl + string.Format("/{0}",deviceToken);

			string profilePath = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath,"Profiles",string.Format("{0}{1}",deviceToken, ProfileManager.ProfileExtension));

			WebClient client = new WebClient();

			string profileText = client.DownloadString(url);
			//File.WriteAllText(profileText, profilePath);
			return profileText;
		}
	}
}
