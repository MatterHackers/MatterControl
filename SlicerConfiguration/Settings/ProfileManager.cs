/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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

using MatterHackers.Agg.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	using Localizations;
	using System.Collections.ObjectModel;
	using System.Net;

	public class ProfileManager
	{
		private static readonly string userDataPath = DataStorage.ApplicationDataStorage.ApplicationUserDataPath;
		internal static readonly string ProfilesPath = Path.Combine(userDataPath, "Profiles");
		private static readonly string profilesDBPath = Path.Combine(ProfilesPath, "profiles.json");

		public static ProfileManager Instance;

		private static EventHandler unregisterEvents;

		static ProfileManager()
		{
			SliceSettingsWidget.SettingChanged.RegisterEvent(SettingsChanged, ref unregisterEvents);

			// Ensure the profiles directory exists
			Directory.CreateDirectory(ProfilesPath);

			Instance = new ProfileManager();

			// One time import
			if (!File.Exists(profilesDBPath))
			{
				// Import classic db based profiles into local json files
				DataStorage.ClassicDB.ClassicSqlitePrinterProfiles.ImportPrinters(Instance, ProfilesPath);
			}

			// Load the profiles.json document
			if (File.Exists(profilesDBPath))
			{
				Instance = JsonConvert.DeserializeObject<ProfileManager>(File.ReadAllText(profilesDBPath));
			}
		}

		public ProfileManager()
		{
			Profiles.CollectionChanged += Profiles_CollectionChanged;
		}

		internal static void SettingsChanged(object sender, EventArgs e)
		{
			string settingsKey = ((StringEventArgs)e).Data;
			switch (settingsKey)
			{
				case "MatterControl.PrinterName":
					Instance.ActiveProfile.Name = ActiveSliceSettings.Instance.GetValue("MatterControl.PrinterName");
					Instance.Save();
					break;

				case "MatterControl.ComPort":
					Instance.ActiveProfile.ComPort = ActiveSliceSettings.Instance.ComPort();
					Instance.Save();
					break;
			}
		}

		public ObservableCollection<PrinterInfo> Profiles { get; set; } = new ObservableCollection<PrinterInfo>();

		[JsonIgnore]
		public IEnumerable<PrinterInfo> ActiveProfiles => Profiles.Where(profile => !profile.MarkedForDelete);

		[JsonIgnore]
		public PrinterInfo ActiveProfile => this[ActiveSliceSettings.Instance.ID];

		public PrinterInfo this[string profileID]
		{
			get
			{
				return Profiles.Where(p => p.ID == profileID).FirstOrDefault();
			}
		}

		public static SettingsProfile LoadEmptyProfile()
		{
			var empytProfile = new SettingsProfile(
				new PrinterSettings(
					new OemProfile(), 
					SliceSettingsOrganizer.Instance.GetDefaultSettings()));

			empytProfile.SetActiveValue("MatterControl.PrinterName", "Printers...".Localize());

			return empytProfile;
		}

		internal static SettingsProfile LoadProfile(string profileID)
		{
			// Conceptually, LoadProfile should...
			// 
			// Find and load a locally cached copy of the profile
			//   - Query the webservice for the given profile passing along our ETAG
			//      Result: 304 or error
			//          Use locally cached copy as it's the latest or we're offline or the service has errored
			//      Result: 200 (Document updated remotely)
			//          Determine if the local profile is dirty. If so, we need to perform conflict resolution to work through the issues
			//          If not, simply write the profile to disk as latest, load and return

			// Only load profiles by ID that are defined in the profiles.json document
			if (ProfileManager.Instance[profileID] == null)
			{
				return null;
			}

			string profilePath = Path.Combine(ProfilesPath, profileID + ".json");
			return File.Exists(profilePath) ? LoadProfileFromDisk(profilePath) : null;
		}

		internal static SettingsProfile LoadProfileFromDisk(string profilePath)
		{
			return new SettingsProfile(PrinterSettings.LoadFile(profilePath));
		}

		internal static void ImportFromExisting(string settingsFilePath)
		{
			if (string.IsNullOrEmpty(settingsFilePath) || !File.Exists(settingsFilePath))
			{
				return;
			}

			var printerInfo = new PrinterInfo
			{
				Name = Path.GetFileNameWithoutExtension(settingsFilePath),
				ID = Guid.NewGuid().ToString()
			};

			string importType = Path.GetExtension(settingsFilePath).ToLower();
			switch (importType)
			{
				case ".printer":
					var profile = ProfileManager.LoadProfileFromDisk(settingsFilePath);
					profile.ID = printerInfo.ID;

					// TODO: Resolve name conflicts
					profile.SetName(printerInfo.Name);

					Instance.Profiles.Add(printerInfo);

					profile.SaveChanges();
					break;

				case ".ini":
					var settingsToImport = PrinterSettingsLayer.LoadFromIni(settingsFilePath);

					var oemProfile = new OemProfile(settingsToImport);
					PrinterSettingsLayer baseConfig = SliceSettingsOrganizer.Instance.GetDefaultSettings();

					var layeredProfile = new PrinterSettings(oemProfile, baseConfig)
					{
						ID = printerInfo.ID,
					};

					// TODO: Resolve name conflicts
					layeredProfile.UserLayer["MatterControl.PrinterName"] = printerInfo.Name;

					Instance.Profiles.Add(printerInfo);

					layeredProfile.Save();

					break;
			}


			ActiveSliceSettings.SwitchToProfile(printerInfo.ID);
		}

		internal static void AcquireNewProfile(string make, string model, string printerName)
		{
			string guid = Guid.NewGuid().ToString();

			OemProfile printerProfile = LoadHttpOemProfile(make, model);
			PrinterSettingsLayer baseConfig = SliceSettingsOrganizer.Instance.GetDefaultSettings();

			var layeredProfile = new PrinterSettings(printerProfile, baseConfig)
			{
				ID = guid
			};
			layeredProfile.UserLayer["MatterControl.PrinterName"] = printerName;

			// Import named macros as defined in the following printers: (Airwolf Axiom, HD, HD-R, HD2x, HDL, HDx, Me3D Me2, Robo R1[+])
			var classicDefaultMacros = layeredProfile.GetValue("default_macros");
			if (!string.IsNullOrEmpty(classicDefaultMacros))
			{
				var namedMacros = new Dictionary<string, string>();
				namedMacros["Lights On"] = "M42 P6 S255";
				namedMacros["Lights Off"] = "M42 P6 S0";
				namedMacros["Offset 0.8"] = "M565 Z0.8;\nM500";
				namedMacros["Offset 0.9"] = "M565 Z0.9;\nM500";
				namedMacros["Offset 1"] = "M565 Z1;\nM500";
				namedMacros["Offset 1.1"] = "M565 Z1.1;\nM500";
				namedMacros["Offset 1.2"] = "M565 Z1.2;\nM500";
				namedMacros["Z Offset"] = "G1 Z10;\nG28;\nG29;\nG1 Z10;\nG1 X5 Y5 F4000;\nM117;";

				foreach (string namedMacro in classicDefaultMacros.Split(','))
				{
					string gcode;
					if (namedMacros.TryGetValue(namedMacro.Trim(), out gcode))
					{
						layeredProfile.Macros.Add(new GCodeMacro()
						{
							Name = namedMacro.Trim(),
							GCode = gcode
						});
					}
				}
			}

			// Copy OemProfile presets into user layers
			layeredProfile.MaterialLayers.AddRange(layeredProfile.OemProfile.MaterialLayers);
			layeredProfile.QualityLayers.AddRange(layeredProfile.OemProfile.QualityLayers);

			layeredProfile.OemProfile.MaterialLayers.Clear();
			layeredProfile.OemProfile.QualityLayers.Clear();

			layeredProfile.Save();

			Instance.Profiles.Add(new PrinterInfo
			{
				Name = printerName,
				ID = guid
			});

			UserSettings.Instance.set("ActiveProfileID", guid);

			ActiveSliceSettings.Instance = new SettingsProfile(layeredProfile);
		}

		private static OemProfile LoadHttpOemProfile(string make, string model)
		{
			string url = string.Format(
				"http://matterdata.azurewebsites.net/api/oemprofiles?manufacturer={0}&model={1}",
				WebUtility.UrlEncode(make),
				WebUtility.UrlEncode(model));

			var client = new WebClient();

			string profileText = client.DownloadString(url);
			var printerProfile = JsonConvert.DeserializeObject<OemProfile>(profileText);
			return printerProfile;
		}

		private static void Profiles_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
			{
				// TODO: This doesn't look right. We need to delete the removed ID not the active on, in case they're different!!!!
				string profilePath = Path.Combine(ProfilesPath, ActiveSliceSettings.Instance.ID + ".json");
				if (File.Exists(profilePath))
				{
					File.Delete(profilePath);
				}

				// Refresh after remove
				UiThread.RunOnIdle(() => ActiveSliceSettings.Instance = LoadEmptyProfile());
			}
		}

		/*
		private static void LoadProfilesFromDisk()
		{
			foreach (string filePath in Directory.GetFiles(ProfilesPath, "*.json"))
			{
				string fileName = Path.GetFileName(filePath);
				if (fileName == "config.json" || fileName == "profiles.json")
				{
					continue;
				}

				try
				{
					var profile = new SettingsProfile(PrinterSettings.LoadFile(filePath));
					ProfileManager.Instance.Profiles.Add(new PrinterInfo()
					{
						ComPort = profile.ComPort(),
						ID = profile.ID,
						Name = profile.GetValue("MatterControl.PrinterName"),
					});
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine("Error loading profile: {1}\r\n{2}", filePath, ex.Message);
				}
			}
		}*/

		public void Save()
		{
			File.WriteAllText(profilesDBPath, JsonConvert.SerializeObject(this, Formatting.Indented));
		}
	}

}