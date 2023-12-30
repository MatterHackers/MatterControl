﻿/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SettingsManagement;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class ProfileManager : IDisposable
	{
		public static RootedObjectEventHandler ProfilesListChanged = new RootedObjectEventHandler();

		public static event EventHandler UserChanged;

		private static ProfileManager _instance = null;

		public static ProfileManager Instance
		{
			get => _instance;
			private set
			{
				_instance?.Dispose();
				_instance = value;
			}
		}

		public const string ProfileExtension = ".printer";
		public const string ProfileDocExtension = ".profiles";

		private object writeLock = new object();

		static ProfileManager()
		{
			ReloadActiveUser();
		}

		public ProfileManager()
		{
			// Register listeners
			PrinterSettings.AnyPrinterSettingChanged += this.Printer_SettingsChanged;
		}

		public Task Initialize()
		{
			return Task.CompletedTask;
		}

		public string UserName { get; set; }

		/// <summary>
		/// Gets the user specific path to the Profiles directory
		/// </summary>
		[JsonIgnore]
		public string UserProfilesDirectory => GetProfilesDirectoryForUser(this.UserName);

		[JsonIgnore]
		public string ProfileThemeSetPath => Path.Combine(UserProfilesDirectory, "themeset.json");

		[JsonIgnore]
		public string OpenTabsPath => Path.Combine(UserProfilesDirectory, "opentabs.json");

		/// <summary>
		/// Gets the user specific path to the Profiles document
		/// </summary>
		[JsonIgnore]
		public string ProfilesDocPath => GetProfilesDocPathForUser(this.UserName);

		private static string GetProfilesDocPathForUser(string userName)
		{
			return Path.Combine(GetProfilesDirectoryForUser(userName), $"{userName}{ProfileDocExtension}");
		}

		private static string GetProfilesDirectoryForUser(string userName)
		{
			string userAndEnvName = (userName == "guest") ? userName : ApplicationController.EnvironmentName + userName;
			string userProfilesDirectory = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "Profiles", userAndEnvName);

			// Ensure directory exists
			Directory.CreateDirectory(userProfilesDirectory);

			return userProfilesDirectory;
		}

		private static Dictionary<string,  List<(string key, string currentValue, string newValue)>> oemSettingsNeedingUpdateCache
			= new Dictionary<string, List<(string key, string currentValue, string newValue)>>();

		public static IEnumerable<(string key, string currentValue, string newValue)> GetOemSettingsNeedingUpdate(PrinterConfig printer)
		{
			var key = printer.Settings.ID;
			Task.Run(async () =>
			{
				ProfileManager.oemSettingsNeedingUpdateCache[key] = await GetChangedOemSettings(printer);
			});

			if (oemSettingsNeedingUpdateCache.TryGetValue(key, out List<(string key, string currentValue, string newValue)> cache))
			{
				foreach (var item in cache)
				{
					if (PrinterSettings.SettingsData.ContainsKey(item.key))
					{
						yield return (item.key, item.currentValue, item.newValue);
					}
				}
			}
		}

		public static async Task<List<(string key, string currentValue, string newValue)>> GetChangedOemSettings(PrinterConfig printer)
		{
			var oemSettingsNeedingUpdateCache = new List<(string key, string currentValue, string newValue)>();

			var make = printer.Settings.GetValue(SettingsKey.make);
			var model = printer.Settings.GetValue(SettingsKey.model);
            throw new NotImplementedException();

            var ignoreSettings = new HashSet<string>()
			{
				SettingsKey.created_date,
				SettingsKey.active_material_key,
				SettingsKey.active_quality_key,
				SettingsKey.oem_profile_token,
				SettingsKey.extruder_offset,
			};

			var serverValuesToIgnore = new Dictionary<string, string>()
			{
				[SettingsKey.probe_offset] = "0,0,0"
			};

			return oemSettingsNeedingUpdateCache;
		}

		[JsonIgnore]
		public bool IsGuestProfile => this.UserName == "guest";

		/// <summary>
		/// Updates ProfileManager.Instance to reflect the current authenticated/guest user
		/// </summary>
		public static void ReloadActiveUser()
		{
			string userName = AuthenticationData.Instance.FileSystemSafeUserName;
			if (!string.IsNullOrEmpty(userName) && Instance?.UserName == userName)
			{
				// No work needed if user hasn't changed
				return;
			}

			if (Instance?.Profiles != null)
			{
				// Release event registration
				Instance.Profiles.CollectionChanged -= Profiles_CollectionChanged;
			}

			Instance = Load(userName);

			// Wire up the CollectionChanged event
			Instance.Profiles.CollectionChanged += Profiles_CollectionChanged;

			// Only execute RestoreUserTabs if the application is up and running, never during startup
			// During startup this behavior must be executed after the MainViewWidget has loaded
			if (!AppContext.IsLoading)
			{
				UiThread.RunOnIdle(() =>
				{
					// Delay then load user tabs
					ApplicationController.Instance.RestoreUserTabs().ConfigureAwait(false);
				}, .2);
			}
		}

		/// <summary>
		/// Loads a ProfileManager for the given user
		/// </summary>
		/// <param name="userName">The user name to load</param>
		public static ProfileManager Load(string userName)
		{
			if (string.IsNullOrEmpty(userName))
			{
				userName = "guest";
			}

			string profilesDocPath = GetProfilesDocPathForUser(userName);

			ProfileManager loadedInstance;

			// Deserialize from disk or if missing, initialize a new instance
			if (File.Exists(profilesDocPath))
			{
				string json = File.ReadAllText(profilesDocPath);
				loadedInstance = JsonConvert.DeserializeObject<ProfileManager>(json);
				loadedInstance.UserName = userName;
			}
			else
			{
				loadedInstance = new ProfileManager() { UserName = userName };
			}

			ProfileManager.UserChanged?.Invoke(loadedInstance, null);

			return loadedInstance;
		}

		public ObservableCollection<PrinterInfo> Profiles { get; } = new ObservableCollection<PrinterInfo>();

		[JsonIgnore]
		public IEnumerable<PrinterInfo> ActiveProfiles => Profiles.Where(profile => !profile.MarkedForDelete).ToList();

		public static bool DebugPrinterDelete { get; set; } = false;

		public PrinterInfo this[string profileID]
		{
			get
			{
				if (DebugPrinterDelete)
				{
					return null;
				}

				return Profiles.Where(p => p.ID == profileID).FirstOrDefault();
			}
		}

		private List<string> profileIDsBackingField = null;

		private List<string> _activeProfileIDs
		{
			get
			{
				// Lazy load from db if null
				if (profileIDsBackingField == null)
				{
					profileIDsBackingField = new List<string>();
				}

				return profileIDsBackingField;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether indicates if given import has been run for the current user. For the guest profile, this means the
		/// Sqlite import has been run and all db printers are now in the guest profile. For normal users
		/// this means the CopyGuestProfilesToUser wizard has been completed and one or more printers were
		/// imported or the "Don't ask me again" option was selected
		/// </summary>
		public bool PrintersImported { get; set; } = false;

		public string ProfilePath(string printerID)
		{
			var printer = this[printerID];
			if (printer != null)
			{
				return ProfilePath(printer);
			}

			// the printer may have been deleted
			return null;
		}

		public string ProfilePath(PrinterInfo printer)
		{
			return Path.Combine(UserProfilesDirectory, printer.ID + ProfileExtension);
		}

		public PrinterSettings LoadSettingsWithoutRecovery(string profileID)
		{
			var printerInfo = Instance[profileID];

			string profilePath = printerInfo?.ProfilePath;
			if (profilePath != null
				&& File.Exists(profilePath)
				&& !printerInfo.MarkedForDelete)
			{
				try
				{
					return PrinterSettings.LoadFile(profilePath);
				}
				catch
				{
					return null;
				}
			}

			return null;
		}

		public static bool SaveOnSingleSettingChange { get; set; } = true;

		// Settings persistence moved from PrinterSettings into ProfileManager to break dependency around ProfileManager paths/MatterControl specific details
		private static void PrinterSettings_SettingChanged(object sender, StringEventArgs e)
		{
			if (SaveOnSingleSettingChange
				&& sender is PrinterSettings settings)
			{
				settings.Save();
			}
		}

		private static bool warningWindowOpen = false;

		public static void WarnAboutRevert(PrinterInfo profile)
		{
			if (!warningWindowOpen)
			{
				warningWindowOpen = true;
				UiThread.RunOnIdle(() =>
				{
					StyledMessageBox.ShowMessageBox((clicedOk) =>
					{
						warningWindowOpen = false;
					},
					"The profile you are attempting to load has been corrupted. We loaded your last usable {0} {1} profile from your recent profile history instead.".Localize()
						.FormatWith(profile.Make, profile.Model),
					"Recovered printer profile".Localize(),
					messageType: StyledMessageBox.MessageType.OK);
				});
			}
		}

		public static PrinterSettings RestoreFromOemProfile(PrinterInfo profile)
		{
			PrinterSettings oemProfile = null;

			try
			{
				var publicDevice = OemSettings.Instance.OemProfiles[profile.Make][profile.Model];
				string cacheScope = Path.Combine("public-profiles", profile.Make);

				string publicProfileToLoad = ApplicationController.CacheablePath(cacheScope, publicDevice.CacheKey);

				oemProfile = JsonConvert.DeserializeObject<PrinterSettings>(File.ReadAllText(publicProfileToLoad));
				oemProfile.ID = profile.ID;
				oemProfile.SetValue(SettingsKey.printer_name, profile.Name);
				oemProfile.DocumentVersion = PrinterSettings.LatestVersion;

				oemProfile.Helpers.SetComPort(profile.ComPort);
				oemProfile.Save(userDrivenChange: false);
			}
			catch
			{
			}

			return oemProfile;
		}

		internal static bool ImportFromExisting(string settingsFilePath, bool resetSettingsForNewProfile, out string printerName)
		{
			printerName = Path.GetFileNameWithoutExtension(settingsFilePath);

			if (string.IsNullOrEmpty(settingsFilePath) || !File.Exists(settingsFilePath))
			{
				return false;
			}

			string fileName = Path.GetFileNameWithoutExtension(settingsFilePath);
			var existingPrinterNames = new HashSet<string>(Instance.ActiveProfiles.Select(p => p.Name));

			var printerInfo = new PrinterInfo
			{
				Name = Util.GetNonCollidingName(fileName, existingPrinterNames),
				ID = Guid.NewGuid().ToString(),
				Make = "Other",
				Model = "Other",
			};

			bool importSuccessful = false;

			string importType = Path.GetExtension(settingsFilePath).ToLower();
			switch (importType)
			{
				case ProfileManager.ProfileExtension:
					// Add the Settings as a profile before performing any actions on it to ensure file paths resolve
					{
						Instance.Profiles.Add(printerInfo);

						var printerSettings = PrinterSettings.LoadFile(settingsFilePath);
						printerSettings.ID = printerInfo.ID;
						printerSettings.ClearValue(SettingsKey.device_token);
						printerInfo.DeviceToken = "";

						// TODO: Resolve name conflicts
						printerSettings.Helpers.SetName(printerInfo.Name);

						if (printerSettings.OemLayer.ContainsKey(SettingsKey.make))
						{
							printerInfo.Make = printerSettings.OemLayer[SettingsKey.make];
						}

						if (printerSettings.OemLayer.ContainsKey(SettingsKey.model))
						{
							printerInfo.Model = printerSettings.OemLayer[SettingsKey.model] ?? "Other";
						}

						if (resetSettingsForNewProfile)
						{
							printerSettings.ResetSettingsForNewProfile();
						}

						printerSettings.Save(userDrivenChange: false);
						importSuccessful = true;
					}

					break;

				case ".fff": // simplify profile
					{
						// load the material settings
						var materials = PrinterSettingsLayer.LoadMaterialSettingsFromFff(settingsFilePath);
						// load the quality settings
						var qualitySettings = PrinterSettingsLayer.LoadQualitySettingsFromFff(settingsFilePath);
						// load the main settings
						var settingsToImport = PrinterSettingsLayer.LoadFromFff(settingsFilePath);
						var printerSettings = new PrinterSettings()
						{
							ID = printerInfo.ID,
						};

						// create the profile we will populate
						printerSettings.OemLayer = new PrinterSettingsLayer
						{
							[SettingsKey.make] = "Other",
							[SettingsKey.model] = "Other"
						};

						// add all the main settings
						foreach (var item in settingsToImport)
						{
							if (printerSettings.Contains(item.Key))
							{
								string currentValue = printerSettings.GetValue(item.Key).Trim();
								// Compare the value to import to the layer cascade value and only set if different
								if (currentValue != item.Value)
								{
									printerSettings.OemLayer[item.Key] = item.Value;
								}
							}
						}

						printerName = settingsToImport[SettingsKey.printer_name];
						printerSettings.UserLayer[SettingsKey.printer_name] = printerName;

						printerSettings.ClearValue(SettingsKey.device_token);
						printerInfo.DeviceToken = "";
						printerInfo.Name = printerName;

						Instance.Profiles.Add(printerInfo);

						printerSettings.Helpers.SetName(printerName);

						// copy in the material settings
						if (materials.Count > 0)
						{
							printerSettings.MaterialLayers.AddRange(materials.Values);
							// set the preferred setting if described
							if (settingsToImport.ContainsKey("printMaterial"))
							{
								foreach (var material in printerSettings.MaterialLayers)
								{
									if (material.Name == settingsToImport["printMaterial"])
									{
										printerSettings.SetValue(SettingsKey.active_material_key, material.LayerID);
										break;
									}
								}
							}
						}

						// copy in the quality settings
						if (qualitySettings.Count > 0)
						{
							printerSettings.QualityLayers.AddRange(qualitySettings.Values);
							// set the preferred setting if described
							if (settingsToImport.ContainsKey("printQuality"))
							{
								foreach (var qualitySetting in printerSettings.QualityLayers)
								{
									if (qualitySetting.Name == settingsToImport["printQuality"])
									{
										printerSettings.SetValue(SettingsKey.active_quality_key, qualitySetting.LayerID);
										break;
									}
								}
							}
						}

						printerSettings.ResetSettingsForNewProfile();

						printerSettings.Save(userDrivenChange: false);
						importSuccessful = true;
					}

					break;

				case ".ini":
					// Scope variables
					{
						var settingsToImport = PrinterSettingsLayer.LoadFromIni(settingsFilePath);
						var printerSettings = new PrinterSettings()
						{
							ID = printerInfo.ID,
						};

						bool containsValidSetting = false;

						printerSettings.OemLayer = new PrinterSettingsLayer
						{
							[SettingsKey.make] = "Other",
							[SettingsKey.model] = "Other"
						};

						foreach (var item in settingsToImport)
						{
							if (printerSettings.Contains(item.Key))
							{
								containsValidSetting = true;
								string currentValue = printerSettings.GetValue(item.Key).Trim();
								// Compare the value to import to the layer cascade value and only set if different
								if (currentValue != item.Value)
								{
									printerSettings.OemLayer[item.Key] = item.Value;
								}
							}
						}

						if (containsValidSetting)
						{
							printerSettings.UserLayer[SettingsKey.printer_name] = printerInfo.Name;

							printerSettings.ClearValue(SettingsKey.device_token);
							printerInfo.DeviceToken = "";

							printerInfo.Make = printerSettings.OemLayer[SettingsKey.make] ?? "Other";
							printerInfo.Model = printerSettings.OemLayer[SettingsKey.model] ?? "Other";

							Instance.Profiles.Add(printerInfo);

							printerSettings.Helpers.SetName(printerInfo.Name);

							if (resetSettingsForNewProfile)
							{
								printerSettings.ResetSettingsForNewProfile();
							}

							printerSettings.Save(userDrivenChange: false);
							importSuccessful = true;
						}
					}

					break;
			}

			return importSuccessful;
		}

		private static void Profiles_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			// Any time the list changes, persist the updates to disk
			Instance.Save();

			ProfilesListChanged.CallEvents(null, null);

			// Queue sync after any collection change event
			throw new NotImplementedException();
		}

		private void Printer_SettingsChanged(object sender, StringEventArgs e)
		{
			var settings = sender as PrinterSettings;
			string printerID = settings?.ID;

			if (string.IsNullOrWhiteSpace(printerID))
			{
				// Exit early if PrinterSettings or ID is invalid
				return;
			}

			var profile = Instance[printerID];
			if (profile == null)
			{
				// Exit early if printer is not known
				return;
			}

			string settingsKey = e?.Data;
			switch (settingsKey)
			{
				case SettingsKey.printer_name:
					profile.Name = settings.GetValue(SettingsKey.printer_name);
					Instance.Save();
					break;

				case SettingsKey.com_port:
					profile.ComPort = settings.Helpers.ComPort();
					Instance.Save();
					break;
			}
		}

		internal void ChangeID(string oldID, string newID)
		{
			if (_activeProfileIDs.Contains(oldID))
			{
				_activeProfileIDs.Remove(oldID);
				_activeProfileIDs.Add(newID);
			}
		}

		public void Save()
		{
			lock (writeLock)
			{
				try
				{
					File.WriteAllText(ProfilesDocPath, JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented));
				}
				catch (Exception ex)
                {
					ApplicationController.Instance.ShowNotification($"Profile Save Error: {ex.Message}");
                }
			}
		}

		public void Dispose()
		{
			// Unregister listeners
			PrinterSettings.AnyPrinterSettingChanged -= this.Printer_SettingsChanged;
		}
	}
}