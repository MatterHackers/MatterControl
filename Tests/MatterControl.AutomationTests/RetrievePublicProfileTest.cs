using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using MatterHackers.MatterControl.VersionManagement;
using System.IO;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SettingsManagement;
using System.Threading;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.Agg.UI.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.Agg.UI;
using Newtonsoft.Json;
using MatterHackers.Agg.PlatformAbstract;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture, RunInApplicationDomain]
	public class RetrievePublicProfileTest
	{
		private string deviceToken = null;

		[Test,RunInApplicationDomain]
		public void RetrievePrinterProfileListWorking()
		{

			StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData(Path.Combine("..", "..", "..", "..", "StaticData"));

			string profilePath = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "temp", "cache", "profiles", "oemprofiles.json");
			if(File.Exists(profilePath))
			{
				File.Delete(profilePath);
			}
			//MatterControlUtilities.OverrideAppDataLocation();
			AutoResetEvent requestCompleteWaiter = new AutoResetEvent(false);
			PublicProfilesRequest retrieveProfiles = new PublicProfilesRequest();
			retrieveProfiles.URI = "https://mattercontrol-test.appspot.com/api/1/device/get-public-profile-list";


			retrieveProfiles.RequestComplete += (sender, eArgs) => { requestCompleteWaiter.Set(); };

			retrieveProfiles.Request();
			Assert.IsTrue(requestCompleteWaiter.WaitOne());

			Assert.IsTrue(File.Exists(profilePath));

			//Call Retrieve Profile next
			RetrievePrinterProfileWorking();
		}

		//[Test,Category("CloudProfiles")]
		public void RetrievePrinterProfileWorking()
		{
			string make = OemSettings.Instance.OemProfiles.First().Key;
			string model = OemSettings.Instance.OemProfiles[make].First().Key;
			deviceToken = OemSettings.Instance.OemProfiles[make][model];
			string expectedProfilePath = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "Profiles", string.Format("{0}.json", deviceToken));
			if (File.Exists(expectedProfilePath))
			{
				File.Delete(expectedProfilePath);
			}
			RetrievePublicProfileRequest request = new RetrievePublicProfileRequest();
			RetrievePublicProfileRequest.DownloadBaseUrl = "https://mattercontrol-test.appspot.com/api/1/device/get-public-profile";
			string recievedPrinterProfile = request.getPrinterProfileByMakeModel(make,model);
			RetrievePublicProfileRequest.DownloadBaseUrl = "https://mattercontrol.appspot.com/api/1/device/get-public-profile";

			Assert.IsNotNullOrEmpty(recievedPrinterProfile);
			//Assert.AreEqual(expectedProfilePath, recievedProfilePath,"Recieved Profile path does not match expected path.");
			//Assert.IsTrue(File.Exists(expectedProfilePath));
		}

		
	}
}
