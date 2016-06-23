using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SettingsManagement;
using System.IO;
using System.Net;

namespace MatterHackers.MatterControl.VersionManagement
{
	class RetrievePublicProfileRequest
	{
		internal static string DownloadBaseUrl { get; set; }

		public RetrievePublicProfileRequest()
		{
#if DEBUG
			DownloadBaseUrl = "https://mattercontrol-test.appspot.com/api/1/device/get-public-profile";
#else
			DownloadBaseUrl = "https://mattercontrol.appspot.com/api/1/device/get-public-profile";
#endif
		}

		public string getPrinterProfileByMakeModel(string make, string model)
		{
			string deviceToken = OemSettings.Instance.OemProfiles[make][model];
			string profiletext = DownloadPrinterProfile(deviceToken);
			return profiletext;
		}

		internal static string DownloadPrinterProfile(string deviceToken)
		{
			// TODO: Enable caching
			//Keept track of version. When retrieving check version

			string url = DownloadBaseUrl + string.Format("/{0}",deviceToken);

			string profilePath = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath,"Profiles",string.Format("{0}.json",deviceToken));
			WebClient client = new WebClient();
			string profileText = client.DownloadString(url);
			//File.WriteAllText(profileText, profilePath);

			return profileText;
			//HttpClient client = new HttpClient();

			//Get a pemporaty path to write to during download. If download completes without error we move this file to the proper path
			//string tempFilePath = ApplicationDataStorage.Instance.GetTempFileName(".json");

			//byte[] buffer = new byte[65536];
			//using (var writeStream = File.Create(tempFilePath))
			//using (var instream = await client.GetStreamAsync(url))
			//{
			//	int bytesRead = await instream.ReadAsync(buffer, 0, buffer.Length);
			//	while(bytesRead != 0)
			//	{
			//		writeStream.Write(buffer, 0, bytesRead);

			//		bytesRead = await instream.ReadAsync(buffer, 0, buffer.Length);
			//	}
			//}

			//File.Move(tempFilePath, profilePath);

			//return profilePath;
		}
		//Used in test to access test server before changes go onto live server
	}
}
