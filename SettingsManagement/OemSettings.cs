/*
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

using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.VersionManagement;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.SettingsManagement
{
	using Agg.UI;
	using OemProfileDictionary = Dictionary<string, Dictionary<string, string>>;

	public class OemSettings
	{
		private static OemSettings instance = null;

		public static OemSettings Instance
		{
			get
			{
				if (instance == null)
				{
					string oemSettings = StaticData.Instance.ReadAllText(Path.Combine("OEMSettings", "Settings.json"));
					instance = JsonConvert.DeserializeObject<OemSettings>(oemSettings) as OemSettings;
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

		public List<string> PrinterWhiteList { get; private set; } = new List<string>();

		public List<ManufacturerNameMapping> ManufacturerNameMappings { get; set; }

		public List<string> PreloadedLibraryFiles { get; } = new List<string>();

		internal void SetManufacturers(IEnumerable<KeyValuePair<string, string>> unorderedManufacturers, List<string> whitelist = null)
		{
			// Sort manufacturers by name
			List<KeyValuePair<string, string>> manufacturers = new List<KeyValuePair<string, string>>();
			KeyValuePair<string, string> otherInfo = new KeyValuePair<string, string>(null, null);
			foreach (var printer in unorderedManufacturers.OrderBy(k => k.Value))
			{
				if (printer.Value == "Other")
				{
					otherInfo = printer;
				}
				else
				{
					manufacturers.Add(printer);
				}
			}

			if (otherInfo.Key != null)
			{
				// add it at the end
				manufacturers.Add(otherInfo);
			}

			if (whitelist != null)
			{
				this.PrinterWhiteList = whitelist;
			}

			// Apply whitelist
			var whiteListedItems = manufacturers?.Where(keyValue => PrinterWhiteList.Contains(keyValue.Key));

			if (whiteListedItems == null
				|| whiteListedItems.Count() == 0)
			{
				AllOems = new List<KeyValuePair<string, string>>(manufacturers);
				return;
			}

			var newItems = new List<KeyValuePair<string, string>>();

			// Apply manufacturer name mappings
			foreach (var keyValue in whiteListedItems)
			{
				string labelText = keyValue.Value;

				// Override the manufacturer name if a manufacturerNameMappings exists
				string mappedName = ManufacturerNameMappings.Where(m => m.NameOnDisk == keyValue.Key).FirstOrDefault()?.NameOnDisk;
				if (!string.IsNullOrEmpty(mappedName))
				{
					labelText = mappedName;
				}

				newItems.Add(new KeyValuePair<string, string>(keyValue.Key, labelText));
			}

			AllOems = newItems;
		}

		public List<KeyValuePair<string, string>> AllOems { get; private set; }

		public OemProfileDictionary OemProfiles { get; set; }

		[OnDeserialized]
		private void Deserialized(StreamingContext context)
		{
			// Load from StaticData to prepopulate oemProfiles for when user create a printer before load cacheable is done
			OemProfiles = JsonConvert.DeserializeObject<OemProfileDictionary>(StaticData.Instance.ReadAllText(Path.Combine("Profiles", "oemprofiles.json")));

			var manufacturesList = OemProfiles.Keys.ToDictionary(oem => oem).ToList();
			SetManufacturers(manufacturesList);
		}

		public async Task ReloadOemProfiles(IProgress<SyncReportType> syncReport = null)
		{
			// In public builds this won't be assigned to and we should abort and exit early
			if (ApplicationController.GetPublicProfileList == null)
			{
				return;
			}

			var oemProfiles = await ApplicationController.LoadCacheableAsync<OemProfileDictionary>(
				"oemprofiles.json",
				"profiles",
				ApplicationController.GetPublicProfileList);

			// If we failed to get anything from load cacheable don't override potentially populated fields
			if (oemProfiles != null)
			{
				OemProfiles = oemProfiles;

				var manufactures = oemProfiles.Keys.ToDictionary(oem => oem);
				SetManufacturers(manufactures);

				await DownloadMissingProfiles(syncReport);
			}
		}

		private async Task DownloadMissingProfiles(IProgress<SyncReportType> syncReport)
		{
			string cacheDirectory = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "temp", "cache", "profiles");
			SyncReportType reportValue = new SyncReportType();
			int index = 0;
			foreach (string oem in OemProfiles.Keys)
			{
				index++;
				foreach (string profileKey in OemProfiles[oem].Values)
				{
					string cacheKey = profileKey + ProfileManager.ProfileExtension;
					string cachePath = Path.Combine(cacheDirectory, cacheKey);

					if (!File.Exists(cachePath))
					{
						var profile = await ApplicationController.DownloadPublicProfileAsync(profileKey);

						string profileJson = JsonConvert.SerializeObject(profile);
						if (!string.IsNullOrEmpty(profileJson))
						{
							File.WriteAllText(cachePath, profileJson);
						}
						if (syncReport != null)
						{
							reportValue.actionLabel = String.Format("Downloading public profiles for {0}...", oem);
							reportValue.percComplete = (double)index / OemProfiles.Count;
							syncReport.Report(reportValue);
						}
					}
					
				}
			}
		}

		private OemSettings()
		{
			this.ManufacturerNameMappings = new List<ManufacturerNameMapping>();
		}
	}

	public class ManufacturerNameMapping
	{
		public string NameOnDisk { get; set; }

		public string NameToDisplay { get; set; }
	}
}

