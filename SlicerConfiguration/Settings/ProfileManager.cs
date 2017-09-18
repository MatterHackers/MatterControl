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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SettingsManagement;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class ProfileManager
	{
		public static RootedObjectEventHandler ProfilesListChanged = new RootedObjectEventHandler();

		private static ProfileManager activeInstance = null;
		public static ProfileManager Instance
		{
			get
			{
				return activeInstance;
			}
			private set
			{
				activeInstance = value;

				// If profile is not loaded, load itthe loaded slice settings do not match the last active settings for this profile, change to the last active
				if (!ApplicationController.Instance.ActivePrinters.Where(p => p.Settings.ID == activeInstance.LastProfileID).Any())
				{
					// Load or download on a background thread the last loaded settings
					var printerSettings = LoadProfileAsync(activeInstance.LastProfileID).Result;

					if (MatterControlApplication.IsLoading)
					{
						ActiveSliceSettings.Instance = printerSettings ?? PrinterSettings.Empty;
					}
					else
					{
						UiThread.RunOnIdle(() =>
						{
							// Assign on the UI thread
							ActiveSliceSettings.Instance = printerSettings ?? PrinterSettings.Empty;
						});
					}

					ApplicationController.Instance.ActivePrinters.Add(new PrinterConfig(true, printerSettings));
				}
			}
		}

		private static EventHandler unregisterEvents;

		public const string ProfileExtension = ".printer";
		public const string ConfigFileExtension = ".slice";
		public const string ProfileDocExtension = ".profiles";

		private object writeLock = new object();

		static ProfileManager()
		{
			ActiveSliceSettings.SettingChanged.RegisterEvent(SettingsChanged, ref unregisterEvents);
			ReloadActiveUser();
		}

		public string UserName { get; set; }

		/// <summary>
		/// The user specific path to the Profiles directory
		/// </summary>
		[JsonIgnore]
		private string UserProfilesDirectory => GetProfilesDirectoryForUser(this.UserName);

		/// <summary>
		/// The user specific path to the Profiles document
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

			return loadedInstance;
		}

		internal static void SettingsChanged(object sender, EventArgs e)
		{
			if (Instance?.ActiveProfile == null)
			{
				return;
			}

			string settingsKey = ((StringEventArgs)e).Data;
			switch (settingsKey)
			{
				case SettingsKey.printer_name:
					Instance.ActiveProfile.Name = ActiveSliceSettings.Instance.GetValue(SettingsKey.printer_name);
					Instance.Save();
					break;

				case SettingsKey.com_port:
					Instance.ActiveProfile.ComPort = ActiveSliceSettings.Instance.Helpers.ComPort();
					Instance.Save();
					break;
			}
		}

		public ObservableCollection<PrinterInfo> Profiles { get; } = new ObservableCollection<PrinterInfo>();

		[JsonIgnore]
		public IEnumerable<PrinterInfo> ActiveProfiles => Profiles.Where(profile => !profile.MarkedForDelete).ToList();

		[JsonIgnore]
		public PrinterInfo ActiveProfile
		{
			get
			{
				var activeID = ActiveSliceSettings.Instance?.ID;
				if (activeID == null)
				{
					return null;
				}

				return this[activeID];
			}
		}

		public PrinterInfo this[string profileID]
		{
			get
			{
				return Profiles.Where(p => p.ID == profileID).FirstOrDefault();
			}
		}

		[JsonIgnore]
		public string LastProfileID
		{
			get
			{
				return UserSettings.Instance.get($"ActiveProfileID-{UserName}");
			}
			set
			{
				UserSettings.Instance.set($"ActiveProfileID-{UserName}", value);
			}
		}

		/// <summary>
		/// Indicates if given import has been run for the current user. For the guest profile, this means the
		/// Sqlite import has been run and all db printers are now in the guest profile. For normal users
		/// this means the CopyGuestProfilesToUser wizard has been completed and one or more printers were 
		/// imported or the "Don't ask me again" option was selected
		/// </summary>
		public bool PrintersImported { get; set; } = false;

		public string ProfilePath(string printerID)
		{
			return ProfilePath(this[printerID]);
		}

		public string ProfilePath(PrinterInfo printer)
		{
			return Path.Combine(UserProfilesDirectory, printer.ID + ProfileExtension);
		}

		public PrinterSettings LoadWithoutRecovery(string profileID)
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

		/// <summary>
		/// Loads the specified PrinterProfile, performing recovery options if required
		/// </summary>
		/// <param name="profileID">The profile ID to load</param>
		/// <param name="useActiveInstance">Return the in memory instance if already loaded. Alternatively, reload from disk</param>
		/// <returns></returns>
		public static async Task<PrinterSettings> LoadProfileAsync(string profileID, bool useActiveInstance = true)
		{
			if (useActiveInstance && ActiveSliceSettings.Instance?.ID == profileID)
			{
				return ActiveSliceSettings.Instance;
			}

			// Only load profiles by ID that are defined in the profiles document
			var printerInfo = Instance[profileID];
			if (printerInfo == null)
			{
				return null;
			}

			// Attempt to load from disk, pull from the web or fall back using recovery logic
			PrinterSettings printerSettings = Instance.LoadWithoutRecovery(profileID);
			if (printerSettings != null)
			{
				// Make sure we have the name set
				if (printerSettings.GetValue(SettingsKey.printer_name) == "")
				{
					// This can happen when a profile is pushed to a user account from the web.
					printerSettings.SetValue(SettingsKey.printer_name, printerInfo.Name);
				}
				return printerSettings;
			}
			else if (ApplicationController.GetPrinterProfileAsync != null)
			{
				// Attempt to load from MCWS if missing on disk
				printerSettings = await ApplicationController.GetPrinterProfileAsync(printerInfo, null);
				if (printerSettings != null)
				{
					// If successful, persist downloaded profile and return
					printerSettings.Save();
					return printerSettings;
				}
			}

			// Otherwise attempt to recover to a working profile
			return await PrinterSettings.RecoverProfile(printerInfo);
		}

		internal static bool ImportFromExisting(string settingsFilePath)
		{
			if (string.IsNullOrEmpty(settingsFilePath) || !File.Exists(settingsFilePath))
			{
				return false;
			}

			string fileName = Path.GetFileNameWithoutExtension(settingsFilePath);
			var existingPrinterNames = Instance.ActiveProfiles.Select(p => p.Name);

			var printerInfo = new PrinterInfo
			{
				Name = agg_basics.GetNonCollidingName(existingPrinterNames, fileName),
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

						printerSettings.Save();
						importSuccessful = true;
					}
					break;

				case ".ini":
					//Scope variables
					{
						var settingsToImport = PrinterSettingsLayer.LoadFromIni(settingsFilePath);
						var printerSettings = new PrinterSettings()
						{
							ID = printerInfo.ID,
						};

						bool containsValidSetting = false;

						printerSettings.OemLayer = new PrinterSettingsLayer();

						printerSettings.OemLayer[SettingsKey.make] = "Other";
						printerSettings.OemLayer[SettingsKey.model] = "Other";

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

						if(containsValidSetting)
						{
							printerSettings.UserLayer[SettingsKey.printer_name] = printerInfo.Name;

							printerSettings.ClearValue(SettingsKey.device_token);
							printerInfo.DeviceToken = "";

							printerInfo.Make = printerSettings.OemLayer[SettingsKey.make] ?? "Other";
							printerInfo.Model = printerSettings.OemLayer[SettingsKey.model] ?? "Other";

							Instance.Profiles.Add(printerInfo);

							printerSettings.Helpers.SetName(printerInfo.Name);

							printerSettings.Save();
							importSuccessful = true;
						}
					}
					break;
			}
			return importSuccessful;
		}

		internal static async Task<PrinterConfig> CreateProfileAsync(string make, string model, string printerName)
		{
			string guid = Guid.NewGuid().ToString();

			var publicDevice = OemSettings.Instance.OemProfiles[make][model];
			if (publicDevice == null)
			{
				return null;
			}

			var printerSettings = await LoadOemProfileAsync(publicDevice, make, model);
			if (printerSettings == null)
			{
				return null;
			}

			printerSettings.ID = guid;
			printerSettings.DocumentVersion = PrinterSettings.LatestVersion;

			printerSettings.UserLayer[SettingsKey.printer_name.ToString()] = printerName;

			//If the active printer has no theme we set it to the current theme color
			printerSettings.UserLayer[SettingsKey.active_theme_name] = ActiveTheme.Instance.Name;

			// Add to Profiles - fires ProfileManager.Save due to ObservableCollection event listener
			Instance.Profiles.Add(new PrinterInfo
			{
				Name = printerName,
				ID = guid,
				Make = make,
				Model = model
			});

			// Persist changes to PrinterSettings - must come after adding to Profiles above
			printerSettings.Save();

			// Set as active profile
			ProfileManager.Instance.LastProfileID = guid;

			var printer = new PrinterConfig(false, printerSettings);
			ApplicationController.Instance.ActivePrinters.Add(printer);

			ActiveSliceSettings.Instance = printerSettings;

			return printer;
		}

		public static List<string> ThemeIndexNameMapping = new List<string>()
		{
			"Blue - Dark",
			"Teal - Dark",
			"Green - Dark",
			"Light Blue - Dark",
			"Orange - Dark",
			"Purple - Dark",
			"Red - Dark",
			"Pink - Dark",
			"Grey - Dark",
			"Pink - Dark",

			//Light themes
			"Blue - Light",
			"Teal - Light",
			"Green - Light",
			"Light Blue - Light",
			"Orange - Light",
			"Purple - Light",
			"Red - Light",
			"Pink - Light",
			"Grey - Light",
			"Pink - Light",
		};

		public async static Task<PrinterSettings> LoadOemProfileAsync(PublicDevice publicDevice, string make, string model)
		{
			string cacheScope = Path.Combine("public-profiles", make);
			string cachePath = ApplicationController.CacheablePath(cacheScope, publicDevice.CacheKey);

			return await ApplicationController.LoadCacheableAsync<PrinterSettings>(
				publicDevice.CacheKey,
				cacheScope,
				async () =>
				{
					// The collector specifically returns null to ensure LoadCacheable skips writing the
					// result to the cache. After this result is returned, it will attempt to load from
					// the local cache if the collector yielded no result
					if(File.Exists(cachePath) 
						|| ApplicationController.DownloadPublicProfileAsync == null)
					{
						return null;
					}
					else
					{
						// If the cache file for the current deviceToken does not exist, attempt to download it.
						// An http 304 results in a null value and LoadCacheable will then load from the cache
						return await ApplicationController.DownloadPublicProfileAsync(publicDevice.ProfileToken);
					}
				},
				Path.Combine("Profiles", make, model + ProfileManager.ProfileExtension));
		}

		public void EnsurePrintersImported()
		{
			if (IsGuestProfile && !PrintersImported)
			{
				// Import Sqlite printer profiles into local json files
				DataStorage.ClassicDB.ClassicSqlitePrinterProfiles.ImportPrinters(Instance, UserProfilesDirectory);
				PrintersImported = true;
				Save();
			}
		}

		private static void Profiles_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			// Any time the list changes, persist the updates to disk
			Instance.Save();

			ProfilesListChanged.CallEvents(null, null);

			// Force sync after any collection change event
			ApplicationController.SyncPrinterProfiles?.Invoke("ProfileManager.Profiles_CollectionChanged()", null);
		}

		public void Save()
		{
			lock(writeLock)
			{
				File.WriteAllText(ProfilesDocPath, JsonConvert.SerializeObject(this, Formatting.Indented));
			}
		}
	}
}