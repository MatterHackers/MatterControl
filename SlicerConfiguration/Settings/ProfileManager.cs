﻿/*
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
	using Agg;
	using Localizations;
	using VersionManagement;
	using System.Collections.ObjectModel;
	using System.Collections.Specialized;
	using System.Net;
	using SettingsManagement;

	public class ProfileManager
	{
		private static readonly string userDataPath = DataStorage.ApplicationDataStorage.ApplicationUserDataPath;
		internal static readonly string ProfilesPath = Path.Combine(userDataPath, "Profiles");
		private static readonly string profilesDBPath = Path.Combine(ProfilesPath, "profiles.json");

		public static ProfileManager Instance;

		private static EventHandler unregisterEvents;

		// Should be executed after deleting the profile from MCWS and should probably be moved to CloudLibrary
		public void PermanentDelete(string printerToken)
		{
			// Find and delete the PrinterInfo from the Profiles list
			var printerInfo = Profiles.Where(p => p.ID == printerToken).FirstOrDefault();
			Profiles.Remove(printerInfo);

			// Delete the 
			File.Delete(printerInfo.ProfilePath);

			Instance.Save();
		}

		public static RootedObjectEventHandler ProfilesListChanged = new RootedObjectEventHandler();

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
				Instance.Profiles.CollectionChanged += Profiles_CollectionChanged;
			}
		}

		public ProfileManager()
		{
		}

		internal static void SettingsChanged(object sender, EventArgs e)
		{
			string settingsKey = ((StringEventArgs)e).Data;
			switch (settingsKey)
			{
				case SettingsKey.printer_name:
					Instance.ActiveProfile.Name = ActiveSliceSettings.Instance.GetValue(SettingsKey.printer_name);
					Instance.Save();
					break;

				case "com_port":
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
			var empytProfile = new SettingsProfile(new PrinterSettings(new OemProfile()));

			empytProfile.SetActiveValue(SettingsKey.printer_name.ToString(), "Printers...".Localize());

			return empytProfile;
		}

		internal static SettingsProfile LoadProfileFromMCWS(string deviceToken)
		{
			WebClient client = new WebClient();
			string json = client.DownloadString($"{MatterControlApplication.MCWSBaseUri}/api/1/device/get-profile?PrinterToken={deviceToken}");

			var printerSettings = JsonConvert.DeserializeObject<PrinterSettings>(json);
			return new SettingsProfile(printerSettings);
		}

		internal static SettingsProfile LoadProfile(string profileID)
		{
			//return LoadProfileFromMCWS(profileID);

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

					var layeredProfile = new PrinterSettings(oemProfile)
					{
						ID = printerInfo.ID,
					};

					// TODO: Resolve name conflicts
					layeredProfile.UserLayer[SettingsKey.printer_name.ToString()] = printerInfo.Name;

					Instance.Profiles.Add(printerInfo);

					layeredProfile.Save();

					break;
			}

			ProfileManager.Instance.Save();
		}

		internal static void AcquireNewProfile(string make, string model, string printerName)
		{
			string guid = Guid.NewGuid().ToString();

			OemProfile printerProfile = LoadHttpOemProfile(make, model);

			var layeredProfile = new PrinterSettings(printerProfile)
			{
				ID = guid,
				// TODO: This should really be set by the system that generates the source documents 
				DocumentVersion = PrinterSettings.LatestVersion
			};
			layeredProfile.UserLayer[SettingsKey.printer_name.ToString()] = printerName;

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
			foreach (var materialPreset in layeredProfile.OemProfile.MaterialLayers)
			{
				layeredProfile.MaterialLayers.Add(materialPreset);
			}
			foreach (var qualityPreset in layeredProfile.OemProfile.QualityLayers)
			{
				layeredProfile.QualityLayers.Add(qualityPreset);
			}

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
			string deviceToken = OemSettings.Instance.OemProfiles[make][model];
			return MatterControlApplication.LoadCacheable<OemProfile>(
				String.Format("{0}.json", deviceToken),
				"profiles",
				() =>
				{
					string responseText = null;

					responseText = RetrievePublicProfileRequest.DownloadPrinterProfile(deviceToken);

					return responseText;
				});
		}


		private static void Profiles_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			// Any time the list changes, persist the updates to disk
			Instance.Save();

			ProfilesListChanged.CallEvents(null, null);
		}

		public void Save()
		{
			File.WriteAllText(profilesDBPath, JsonConvert.SerializeObject(this, Formatting.Indented));
		}
	}

}