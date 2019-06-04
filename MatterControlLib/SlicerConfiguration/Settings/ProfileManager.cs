/*
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
		public const string ConfigFileExtension = ".slice";
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
		/// The user specific path to the Profiles directory
		/// </summary>
		[JsonIgnore]
		private string UserProfilesDirectory => GetProfilesDirectoryForUser(this.UserName);

		[JsonIgnore]
		public string ProfileThemeSetPath => Path.Combine(UserProfilesDirectory, "themeset.json");

		[JsonIgnore]
		public string OpenTabsPath => Path.Combine(UserProfilesDirectory, "opentabs.json");

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

		public void DeletePrinter(string printerID)
		{
			var printerInfo = ProfileManager.Instance[printerID];
			if (printerInfo != null)
			{
				printerInfo.MarkedForDelete = true;
				ProfileManager.Instance.Save();
			}

			// TODO: Consolidate ActivePrinters into ProfileManager.OpenPrinters
			var openedPrinter = ApplicationController.Instance.ActivePrinters.FirstOrDefault(p => p.Settings.ID == printerID);
			if (openedPrinter != null)
			{
				// Clear selected printer state
				ProfileManager.Instance.ClosePrinter(printerID);
			}

			_activeProfileIDs.Remove(printerID);

			UiThread.RunOnIdle(() =>
			{
				if (openedPrinter != null)
				{
					ApplicationController.Instance.ClosePrinter(openedPrinter);
				}

				// Notify listeners of a ProfileListChange event due to this printers removal
				ProfileManager.ProfilesListChanged.CallEvents(this, null);

				// Queue sync after marking printer for delete
				ApplicationController.QueueCloudProfileSync?.Invoke("SettingsHelpers.SetMarkedForDelete()");
			});
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

			// Ensure SQLite printers are imported
			loadedInstance.EnsurePrintersImported();

			return loadedInstance;
		}

		public ObservableCollection<PrinterInfo> Profiles { get; } = new ObservableCollection<PrinterInfo>();

		[JsonIgnore]
		public IEnumerable<PrinterInfo> ActiveProfiles => Profiles.Where(profile => !profile.MarkedForDelete).ToList();

		public PrinterInfo this[string profileID]
		{
			get
			{
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

		public void ClosePrinter(string printerID)
		{
			try
			{
				// Unregister listener
				if (ApplicationController.Instance.ActivePrinters.FirstOrDefault(p => p.Settings.ID == printerID) is PrinterConfig printer)
				{
					printer.Settings.SettingChanged -= PrinterSettings_SettingChanged;
				}
			}
			catch
			{
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

		/// <summary>
		/// Loads the specified printer settings, performing recovery options if required
		/// </summary>
		/// <param name="printerID">The printer ID to load</param>
		/// <param name="useActiveInstance">Return the in memory instance if already loaded. Alternatively, reload from disk</param>
		/// <returns></returns>
		public static async Task<PrinterSettings> LoadSettingsAsync(string printerID, bool useActiveInstance = true)
		{
			// Check loaded printers for printerID and return if found
			if (ApplicationController.Instance.ActivePrinters.FirstOrDefault(p => p.Settings.ID == printerID) is PrinterConfig activePrinter)
			{
				return activePrinter.Settings;
			}

			// Only load profiles by ID that are defined in the profiles document
			var printerInfo = Instance[printerID];
			if (printerInfo == null)
			{
				return null;
			}

			// Attempt to load from disk, pull from the web or fall back using recovery logic
			PrinterSettings printerSettings = Instance.LoadSettingsWithoutRecovery(printerID);
			if (printerSettings != null)
			{
				// Make sure we have the name set
				if (printerSettings.GetValue(SettingsKey.printer_name) == "")
				{
					// This can happen when a profile is pushed to a user account from the web.
					printerSettings.SetValue(SettingsKey.printer_name, printerInfo.Name);
				}
			}
			else if (ApplicationController.GetPrinterProfileAsync != null)
			{
				// Attempt to load from MCWS if missing on disk
				printerSettings = await ApplicationController.GetPrinterProfileAsync(printerInfo, null);
				if (printerSettings != null)
				{
					// If successful, persist downloaded profile and return
					printerSettings.Save(userDrivenChange: false);
				}
			}

			// Recover to a default working profile if still null
			if (printerSettings == null)
			{
				printerSettings = await RecoverProfile(printerInfo);
			}

			// Register listener on non-null settings
			if (printerSettings != null)
			{
				// TODO: This is likely to leak and keep printerSettings in memory in some cases until we combine PrinterConfig 
				// loading into profile manager and have a single owner of loaded printers that can unwire this when their 
				// tabs close
				//
				// Register listener
				printerSettings.SettingChanged += PrinterSettings_SettingChanged;
			}

			return printerSettings;
		}

		// Settings persistence moved from PrinterSettings into ProfileManager to break dependency around ProfileManager paths/MatterControl specific details
		private static void PrinterSettings_SettingChanged(object sender, StringEventArgs e)
		{
			if (sender is PrinterSettings settings)
			{
				settings.Save();
			}
		}

		public async static Task<PrinterSettings> RecoverProfile(PrinterInfo printerInfo)
		{
			bool userIsLoggedIn = !ApplicationController.GuestUserActive?.Invoke() ?? false;
			if (userIsLoggedIn && printerInfo != null)
			{
				// Attempt to load from MCWS history
				var printerSettings = await GetFirstValidHistoryItem(printerInfo);
				if (printerSettings == null)
				{
					// Fall back to OemProfile defaults if load from history fails
					printerSettings = RestoreFromOemProfile(printerInfo);
				}

				if (printerSettings == null)
				{
					// If we still have failed to recover a profile, create an empty profile with
					// just enough data to delete the printer
					printerSettings = PrinterSettings.Empty;
					printerSettings.ID = printerInfo.ID;
					printerSettings.UserLayer[SettingsKey.device_token] = printerInfo.DeviceToken;
					printerSettings.Helpers.SetComPort(printerInfo.ComPort);
					printerSettings.SetValue(SettingsKey.printer_name, printerInfo.Name);

					// Add any setting value to the OemLayer to pass the .PrinterSelected property
					printerSettings.OemLayer = new PrinterSettingsLayer();
					printerSettings.OemLayer.Add("empty", "setting");
					printerSettings.Save(userDrivenChange: false);
				}

				if (printerSettings != null)
				{
					// Persist any profile recovered above as the current
					printerSettings.Save(userDrivenChange: false);

					WarnAboutRevert(printerInfo);
				}

				return printerSettings;
			}

			return null;
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

		private static async Task<PrinterSettings> GetFirstValidHistoryItem(PrinterInfo printerInfo)
		{
			var recentProfileHistoryItems = await ApplicationController.GetProfileHistory?.Invoke(printerInfo.DeviceToken);
			if (recentProfileHistoryItems != null)
			{
				// Iterate history, skipping the first item, limiting to the next five, attempt to load and return the first success
				foreach (var keyValue in recentProfileHistoryItems.OrderByDescending(kvp => kvp.Key).Skip(1).Take(5))
				{
					// Attempt to download and parse each profile, returning if successful
					try
					{
						var printerSettings = await ApplicationController.GetPrinterProfileAsync(printerInfo, keyValue.Value);
						if (printerSettings != null)
						{
							return printerSettings;
						}
					}
					catch
					{
					}
				}
			}

			return null;
		}

		internal static bool ImportFromExisting(string settingsFilePath, bool clearBlackList = true)
		{
			if (string.IsNullOrEmpty(settingsFilePath) || !File.Exists(settingsFilePath))
			{
				return false;
			}

			string fileName = Path.GetFileNameWithoutExtension(settingsFilePath);
			var existingPrinterNames = Instance.ActiveProfiles.Select(p => p.Name);

			var printerInfo = new PrinterInfo
			{
				Name = agg_basics.GetNonCollidingName(fileName, existingPrinterNames),
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

						if (clearBlackList)
						{
							printerSettings.ClearBlackList();
						}

						printerSettings.Save(userDrivenChange: false);
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

						if (containsValidSetting)
						{
							printerSettings.UserLayer[SettingsKey.printer_name] = printerInfo.Name;

							printerSettings.ClearValue(SettingsKey.device_token);
							printerInfo.DeviceToken = "";

							printerInfo.Make = printerSettings.OemLayer[SettingsKey.make] ?? "Other";
							printerInfo.Model = printerSettings.OemLayer[SettingsKey.model] ?? "Other";

							Instance.Profiles.Add(printerInfo);

							printerSettings.Helpers.SetName(printerInfo.Name);

							if (clearBlackList)
							{
								printerSettings.ClearBlackList();
							}

							printerSettings.Save(userDrivenChange: false);
							importSuccessful = true;
						}
					}

					break;
			}

			return importSuccessful;
		}

		internal static async Task<PrinterConfig> CreatePrinterAsync(string make, string model, string printerName)
		{
			string guid = Guid.NewGuid().ToString();

			var publicDevice = OemSettings.Instance.OemProfiles[make][model];
			if (publicDevice == null)
			{
				return null;
			}

			var printerSettings = await LoadOemSettingsAsync(publicDevice, make, model);
			if (printerSettings == null)
			{
				return null;
			}

			printerSettings.ID = guid;
			printerSettings.DocumentVersion = PrinterSettings.LatestVersion;

			printerSettings.UserLayer[SettingsKey.printer_name] = printerName;

			// Add to Profiles - fires ProfileManager.Save due to ObservableCollection event listener
			Instance.Profiles.Add(new PrinterInfo
			{
				Name = printerName,
				ID = guid,
				Make = make,
				Model = model
			});

			// Persist changes to PrinterSettings - must come after adding to Profiles above
			printerSettings.ClearBlackList();
			printerSettings.Save(userDrivenChange: false);

			// Set as active profile
			return await ApplicationController.Instance.OpenEmptyPrinter(guid);
		}

		public async static Task<PrinterSettings> LoadOemSettingsAsync(PublicDevice publicDevice, string make, string model)
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

		private void EnsurePrintersImported()
		{
			if (IsGuestProfile && !PrintersImported)
			{
				int intialCount = this.Profiles.Count;

				// Import Sqlite printer profiles into local json files
				DataStorage.ClassicDB.ClassicSqlitePrinterProfiles.ImportPrinters(Instance, UserProfilesDirectory);
				PrintersImported = true;

				if (intialCount != this.Profiles.Count)
				{
					this.Save();
				}
			}
		}

		private static void Profiles_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			// Any time the list changes, persist the updates to disk
			Instance.Save();

			ProfilesListChanged.CallEvents(null, null);

			// Queue sync after any collection change event
			ApplicationController.QueueCloudProfileSync?.Invoke("ProfileManager.Profiles_CollectionChanged()");
		}

		private void Printer_SettingsChanged(object sender, StringEventArgs e)
		{
			var settings = (sender as PrinterSettings);
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
			lock(writeLock)
			{
				File.WriteAllText(ProfilesDocPath, JsonConvert.SerializeObject(this, Formatting.Indented));
			}
		}

		public void Dispose()
		{
			// Unregister listeners
			PrinterSettings.AnyPrinterSettingChanged -= this.Printer_SettingsChanged;
		}
	}
}