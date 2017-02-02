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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class PrinterSettings
	{
		// Latest version should be in the form of:
		// Year|month|day|versionForDay (to support multiple revisions on a given day)
		public static int LatestVersion { get; } = 201606271;

		public static RootedObjectEventHandler PrintLevelingEnabledChanged = new RootedObjectEventHandler();

		public static PrinterSettings Empty { get; }

		public int DocumentVersion { get; set; } = LatestVersion;

		public string ID { get; set; }

		public static Func<bool> ShouldShowAuthPanel { get; set; }

		private static object writeLock = new object();

		[JsonIgnore]
		internal PrinterSettingsLayer QualityLayer { get; private set; }

		[JsonIgnore]
		internal PrinterSettingsLayer MaterialLayer { get; private set; }

		public PrinterSettingsLayer StagedUserSettings { get; set; } = new PrinterSettingsLayer();

		static PrinterSettings()
		{
			Empty = new PrinterSettings() { ID = "EmptyProfile" };
			Empty.UserLayer[SettingsKey.printer_name] = "Printers...".Localize();
		}

		public PrinterSettings()
		{
			this.Helpers = new SettingsHelpers(this);
		}

		public List<GCodeMacro> Macros { get; set; } = new List<GCodeMacro>();

		public IEnumerable<GCodeMacro> UserMacros() => Macros.Where(m => !m.ActionGroup);
		public IEnumerable<GCodeMacro> ActionMacros() => Macros.Where(m => m.ActionGroup);

		/// <summary>
		/// Restore deactivated user overrides by iterating the active preset and removing/restoring matching items
		/// </summary>
		public void RestoreConflictingUserOverrides(PrinterSettingsLayer settingsLayer)
		{
			if (settingsLayer == null)
			{
				return;
			}

			foreach (var settingsKey in settingsLayer.Keys)
			{
				RestoreUserOverride(settingsLayer, settingsKey);
			}
		}

		private void RestoreUserOverride(PrinterSettingsLayer settingsLayer, string settingsKey)
		{
			string stagedUserOverride;
			if (StagedUserSettings.TryGetValue(settingsKey, out stagedUserOverride))
			{
				StagedUserSettings.Remove(settingsKey);
				UserLayer[settingsKey] = stagedUserOverride;
			}
		}

		/// <summary>
		/// Move conflicting user overrides to the temporary staging area, allowing presets values to take effect
		/// </summary>
		public void DeactivateConflictingUserOverrides(PrinterSettingsLayer settingsLayer)
		{
			if (settingsLayer == null)
			{
				return;
			}

			foreach (var settingsKey in settingsLayer.Keys)
			{
				StashUserOverride(settingsLayer, settingsKey);
			}
		}

		/// <summary>
		/// Move conflicting user overrides to the temporary staging area, allowing presets values to take effect
		/// </summary>
		private void StashUserOverride(PrinterSettingsLayer settingsLayer, string settingsKey)
		{
			string userOverride;
			if (this.UserLayer.TryGetValue(settingsKey, out userOverride))
			{
				this.UserLayer.Remove(settingsKey);
				this.StagedUserSettings.Add(settingsKey, userOverride);
			}
		}

		[OnDeserialized]
		internal void OnDeserializedMethod(StreamingContext context)
		{
			QualityLayer = GetQualityLayer(ActiveQualityKey);

			string materialSettingsKey = GetMaterialPresetKey(0);
			if (!string.IsNullOrEmpty(materialSettingsKey))
			{
				MaterialLayer = GetMaterialLayer(materialSettingsKey);
			}
		}

		public PrinterSettingsLayer OemLayer { get; set; }

		public void Merge(PrinterSettingsLayer destinationLayer, PrinterSettings settingsToImport, List<PrinterSettingsLayer> rawSourceFilter, bool setLayerName)
		{
			HashSet<string> skipKeys = new HashSet<string>
			{
				"layer_id",
			};

			if (!setLayerName)
			{
				skipKeys.Add(SettingsKey.layer_name);
			}

			var destinationFilter = new List<PrinterSettingsLayer>
			{
				OemLayer,
				BaseLayer,
				destinationLayer,
			}.Where(layer => layer != null);

			var sourceFilter = rawSourceFilter.Where(layer => layer != null);

			foreach (var keyName in PrinterSettings.KnownSettings)
			{
				if (settingsToImport.Contains(keyName))
				{
					// Compare the value to import to the layer cascade value and only set if different
					string currentValue = this.GetValue(keyName, destinationFilter).Trim();
					string importValue = settingsToImport.GetValue(keyName, sourceFilter).Trim();

					if (!string.IsNullOrEmpty(importValue)
						&& currentValue != keyName
						&& !skipKeys.Contains(keyName))
					{
						destinationLayer[keyName] = importValue;
					}
				}
			}

			if (setLayerName)
			{
				destinationLayer[SettingsKey.layer_name] = settingsToImport.GetValue(SettingsKey.layer_name, sourceFilter);
			}

			this.Save();

			ApplicationController.Instance.ReloadAdvancedControlsPanel();
		}

		internal PrinterSettingsLayer GetMaterialLayer(string layerID)
		{
			if (string.IsNullOrEmpty(layerID))
			{
				return null;
			}

			return MaterialLayers.Where(layer => layer.LayerID == layerID).FirstOrDefault();
		}

		private PrinterSettingsLayer GetQualityLayer(string layerID)
		{
			return QualityLayers.Where(layer => layer.LayerID == layerID).FirstOrDefault();
		}

		public string ActiveQualityKey
		{
			get
			{
				return GetValue(SettingsKey.active_quality_key);
			}
			internal set
			{
				SetValue(SettingsKey.active_quality_key, value);
				QualityLayer = GetQualityLayer(value);
				Save();
			}
		}

		public string GetMaterialPresetKey(int extruderIndex)
		{
			if (extruderIndex >= MaterialSettingsKeys.Count)
			{
				return null;
			}

			return MaterialSettingsKeys[extruderIndex];
		}

		public void SetMaterialPreset(int extruderIndex, string materialKey)
		{
			if (extruderIndex >= PrinterCommunication.PrinterConnectionAndCommunication.MAX_EXTRUDERS)
			{
				throw new ArgumentOutOfRangeException("Requested extruder index is outside of bounds: " + extruderIndex);
			}

			// TODO: This should really be in PrinterProfile and should be run when the extruder count changes
			if (MaterialSettingsKeys.Count <= extruderIndex)
			{
				var resizedArray = new string[extruderIndex + 1];
				MaterialSettingsKeys.CopyTo(resizedArray);
				MaterialSettingsKeys = new List<string>(resizedArray);
			}

			MaterialSettingsKeys[extruderIndex] = materialKey;

			if (extruderIndex == 0)
			{
				MaterialLayer = GetMaterialLayer(materialKey);
				ApplicationController.Instance.ReloadAdvancedControlsPanel();
			}

			Save();
		}

		public List<string> MaterialSettingsKeys { get; set; } = new List<string>();

		private string GenerateSha1()
		{
			// Maybe be UTF8 encoded, may not...
			using (var fileStream = new FileStream(DocumentPath, FileMode.Open))
			using (var bufferedStream = new BufferedStream(fileStream, 1200000))
			{
				return GenerateSha1(bufferedStream);
			}
		}

		private string GenerateSha1(Stream stream)
		{
			// var timer = Stopwatch.StartNew();
			using (var sha1 = System.Security.Cryptography.SHA1.Create())
			{
				byte[] hash = sha1.ComputeHash(stream);
				string SHA1 = BitConverter.ToString(hash).Replace("-", String.Empty);

				// Console.WriteLine("{0} {1} {2}", SHA1, timer.ElapsedMilliseconds, filePath);
				return SHA1;
			}
		}

		private string DocumentPath => ProfileManager.Instance.ProfilePath(this.ID);

		[JsonIgnore]
		public bool AutoSave { get; set; } = true;

		public void Save()
		{
			// Skip save operation if on the EmptyProfile
			if (!this.PrinterSelected || !this.AutoSave)
			{
				return;
			}

			Save(DocumentPath);
		}

		public void Save(string filePath)
		{
			lock (writeLock)
			{
				string json = this.ToJson();

				var printerInfo = ProfileManager.Instance[this.ID];
				if (printerInfo != null)
				{
					printerInfo.ContentSHA1 = this.ComputeSha1(json);
					ProfileManager.Instance.Save();
				}

				File.WriteAllText(filePath, json);
			}

			if (ActiveSliceSettings.Instance?.ID == this.ID)
			{
				ActiveSliceSettings.ActiveProfileModified.CallEvents(null, null);
			}
		}

		public string ComputeSha1()
		{
			return ComputeSha1(this.ToJson());
		}

		private string ComputeSha1(string json)
		{
			// SHA1 value is based on UTF8 encoded file contents
			using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
			{
				return GenerateSha1(memoryStream);
			}
		}

		/// <summary>
		/// User settings overrides
		/// </summary>
		public PrinterSettingsLayer UserLayer { get; } = new PrinterSettingsLayer();

		public static PrinterSettings LoadFile(string printerProfilePath, bool performMigrations = false)
		{
			if (performMigrations)
			{
				JObject jObject = null;

				try
				{
					jObject = JObject.Parse(File.ReadAllText(printerProfilePath));
				}
				catch
				{
					return null;
				}

				int documentVersion = jObject?.GetValue("DocumentVersion")?.Value<int>() ?? PrinterSettings.LatestVersion;
				if (documentVersion < PrinterSettings.LatestVersion)
				{
					printerProfilePath = ProfileMigrations.MigrateDocument(printerProfilePath, documentVersion);
				}
			}

			try
			{
				return JsonConvert.DeserializeObject<PrinterSettings>(File.ReadAllText(printerProfilePath));
			}
			catch
			{
				return null;
			}
		}

		public async static Task<PrinterSettings> RecoverProfile(PrinterInfo printerInfo)
		{
			bool userIsLoggedIn = !ShouldShowAuthPanel?.Invoke() ?? false;
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
					printerSettings.Save();
				}

				if (printerSettings != null)
				{
					// Persist any profile recovered above as the current
					printerSettings.Save();

					// Update active instance without calling ReloadAll
					ActiveSliceSettings.RefreshActiveInstance(printerSettings);

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
					}, String.Format("The profile you are attempting to load has been corrupted. We loaded your last usable {0} {1} profile from your recent profile history instead.".Localize(), profile.Make, profile.Model), "Recovered printer profile".Localize(), messageType: StyledMessageBox.MessageType.OK);
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
				oemProfile.Save();
			}
			catch { }

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
					catch { }
				};
			}

			return null;
		}

		/// <summary>
		/// Should contain both user created and oem specified material layers
		/// </summary>
		public ObservableCollection<PrinterSettingsLayer> MaterialLayers { get; } = new ObservableCollection<PrinterSettingsLayer>();

		/// <summary>
		/// Should contain both user created and oem specified quality layers
		/// </summary>
		public ObservableCollection<PrinterSettingsLayer> QualityLayers { get; } = new ObservableCollection<PrinterSettingsLayer>();

		///<summary>
		///Returns the settings value at the 'top' of the stack
		///Returns the first matching value discovered while enumerating the settings layers
		///</summary>
		public string GetValue(string sliceSetting, IEnumerable<PrinterSettingsLayer> layerCascade = null)
		{
			if (layerCascade == null)
			{
				layerCascade = defaultLayerCascade;
			}

			foreach (PrinterSettingsLayer layer in layerCascade)
			{
				string value;
				if (layer.TryGetValue(sliceSetting, out value))
				{
					return value;
				}
			}

			return "";
		}

		public bool Contains(string sliceSetting, IEnumerable<PrinterSettingsLayer> layerCascade = null)
		{
			if (layerCascade == null)
			{
				layerCascade = defaultLayerCascade;
			}
			foreach (PrinterSettingsLayer layer in layerCascade)
			{
				if (layer.ContainsKey(sliceSetting))
				{
					return true;
				}
			}
			return false;
		}

		[JsonIgnore]
		public PrinterSettingsLayer BaseLayer { get; set; } = SliceSettingsOrganizer.Instance.GetDefaultSettings();

		private IEnumerable<PrinterSettingsLayer> defaultLayerCascade
		{
			get
			{
				if (this.UserLayer != null)
				{
					yield return this.UserLayer;
				}

				if (this.MaterialLayer != null)
				{
					yield return this.MaterialLayer;
				}

				if (this.QualityLayer != null)
				{
					yield return this.QualityLayer;
				}

				if (this.OemLayer != null)
				{
					yield return this.OemLayer;
				}

				yield return this.BaseLayer;
			}
		}
		[JsonIgnore]
		public SettingsHelpers Helpers { get; set; }

		[JsonIgnore]
		public bool PrinterSelected => OemLayer?.Keys.Count > 0;

		internal void RunInTransaction(Action<PrinterSettings> action)
		{
			// TODO: Implement RunInTransaction
			// Suspend writes
			action(this);
			// Commit
		}

		public void ClearUserOverrides()
		{
			var userOverrides = this.UserLayer.Keys.ToArray();

			// Leave user layer items that have no Organizer definition and thus cannot be changed by the user
			var keysToRetain = new HashSet<string>(userOverrides.Except(KnownSettings));

			// Print leveling data has no SliceSettingsWidget editor but should be removed on 'Reset to Defaults'
			keysToRetain.Remove(SettingsKey.print_leveling_data);
			keysToRetain.Remove(SettingsKey.print_leveling_enabled);

			// Iterate all items that have .ShowAsOverride = false and conditionally add to the retention list
			foreach (var item in ActiveSliceSettings.SettingsData.Where(settingsItem => settingsItem.ShowAsOverride == false))
			{
				switch (item.SlicerConfigName)
				{
					case SettingsKey.baud_rate:
					case SettingsKey.auto_connect:
						// Items *should* reset to defaults
						break;
					default:
						//Items should *not* reset to defaults
						keysToRetain.Add(item.SlicerConfigName);
						break;
				}
			}

			var keysToRemove = (from keyValue in this.UserLayer
								where !keysToRetain.Contains(keyValue.Key)
								select keyValue.Key).ToList();

			foreach (string key in keysToRemove)
			{
				this.UserLayer.Remove(key);
			}
		}

		#region Migrate to LayeredProfile 

		static Dictionary<string, Type> expectedMappingTypes = new Dictionary<string, Type>()
		{
			[SettingsKey.extruders_share_temperature] = typeof(int),
			[SettingsKey.extruders_share_temperature] = typeof(bool),
			[SettingsKey.has_heated_bed] = typeof(bool),
			[SettingsKey.nozzle_diameter] = typeof(double),
			[SettingsKey.bed_temperature] = typeof(double),
		};

		void ValidateType<T>(string settingsKey)
		{
			if (expectedMappingTypes.ContainsKey(settingsKey))
			{
				if (expectedMappingTypes[settingsKey] != typeof(T))
				{
					throw new Exception("You must request the correct type of this settingsKey.");
				}
			}

			if (settingsKey.Contains("%"))
			{
				if (typeof(T) != typeof(double))
				{
					throw new Exception("To get processing of a % you must request the type as double.");
				}
			}
		}

		///<summary>
		///Returns the first matching value discovered while enumerating the settings layers
		///</summary>
		public T GetValue<T>(string settingsKey) where T : IConvertible
		{
#if DEBUG
			ValidateType<T>(settingsKey);
#endif
			if (typeof(T) == typeof(bool))
			{
				return (T)(object)(this.GetValue(settingsKey) == "1");
			}
			else if (typeof(T) == typeof(int))
			{
				int result;
				int.TryParse(this.GetValue(settingsKey), out result);
				return (T)(object)(result);
			}
			else if (typeof(T) == typeof(Vector2))
			{
				string[] twoValues = GetValue(settingsKey).Split(',');
				if (twoValues.Length != 2)
				{
					throw new Exception(string.Format("Not parsing {0} as a Vector2", settingsKey));
				}
				Vector2 valueAsVector2 = new Vector2();
				valueAsVector2.x = Helpers.ParseDouble(twoValues[0]);
				valueAsVector2.y = Helpers.ParseDouble(twoValues[1]);
				return (T)(object)(valueAsVector2);
			}
			else if (typeof(T) == typeof(double))
			{
				string settingsStringh = GetValue(settingsKey);
				if (settingsStringh.Contains("%"))
				{
					string onlyNumber = settingsStringh.Replace("%", "");
					double ratio = Helpers.ParseDouble(onlyNumber) / 100;

					if (settingsKey == SettingsKey.first_layer_height)
					{
						return (T)(object)(GetValue<double>(SettingsKey.layer_height) * ratio);
					}
					else if (settingsKey == SettingsKey.first_layer_extrusion_width)
					{
						return (T)(object)(GetValue<double>(SettingsKey.nozzle_diameter) * ratio);
					}

					return (T)(object)(ratio);
				}
				else if (settingsKey == SettingsKey.first_layer_extrusion_width)
				{
					double extrusionResult;
					double.TryParse(this.GetValue(settingsKey), out extrusionResult);
					return (T)(object)(extrusionResult == 0 ? GetValue<double>(SettingsKey.nozzle_diameter) : extrusionResult);
				}

				if (settingsKey == SettingsKey.bed_temperature
					&& !this.GetValue<bool>(SettingsKey.has_heated_bed))
				{
					return (T)Convert.ChangeType(0, typeof(double));
				}

				double result;
				double.TryParse(this.GetValue(settingsKey), out result);
				return (T)(object)(result);
			}
			else if (typeof(T) == typeof(BedShape))
			{
				switch (GetValue(settingsKey))
				{
					case "rectangular":
						return (T)(object)BedShape.Rectangular;

					case "circular":
						return (T)(object)BedShape.Circular;

					default:
#if DEBUG
						throw new NotImplementedException(string.Format("'{0}' is not a known bed_shape.", GetValue(SettingsKey.bed_shape)));
#else
						return (T)(object)BedShape.Rectangular;
#endif
				}
			}


			return (T)default(T);
		}

		/// <summary>
		/// Returns whether or not the setting is overridden by the active layer
		/// </summary>
		public bool SettingExistsInLayer(string sliceSetting, NamedSettingsLayers layer)
		{
			switch (layer)
			{
				case NamedSettingsLayers.Quality:
					return QualityLayer?.ContainsKey(sliceSetting) == true;
				case NamedSettingsLayers.Material:
					return MaterialLayer?.ContainsKey(sliceSetting) == true;
				case NamedSettingsLayers.User:
					return UserLayer?.ContainsKey(sliceSetting) == true;
				default:
					return false;
			}
		}

		public long GetLongHashCode()
		{
			var bigStringForHashCode = new StringBuilder();

			foreach (var keyValue in this.BaseLayer)
			{
				SliceSettingData data = SliceSettingsOrganizer.Instance.GetSettingsData(keyValue.Key);
				if (data.RebuildGCodeOnChange)
				{
					string activeValue = GetValue(keyValue.Key);
					bigStringForHashCode.Append(keyValue.Key);
					bigStringForHashCode.Append(activeValue);
				}
			}

			string value = bigStringForHashCode.ToString();

			return agg_basics.ComputeHash(bigStringForHashCode.ToString());
		}

		public bool IsValid()
		{
			try
			{
				if (GetValue<double>(SettingsKey.layer_height) > GetValue<double>(SettingsKey.nozzle_diameter))
				{
					string error = "'Layer Height' must be less than or equal to the 'Nozzle Diameter'.".Localize();
					string details = string.Format("Layer Height = {0}\nNozzle Diameter = {1}".Localize(), GetValue<double>(SettingsKey.layer_height), GetValue<double>(SettingsKey.nozzle_diameter));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'General' -> 'Layers/Surface'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}
				else if (GetValue<double>(SettingsKey.first_layer_height) > GetValue<double>(SettingsKey.nozzle_diameter))
				{
					string error = "'First Layer Height' must be less than or equal to the 'Nozzle Diameter'.".Localize();
					string details = string.Format("First Layer Height = {0}\nNozzle Diameter = {1}".Localize(), GetValue<double>(SettingsKey.first_layer_height), GetValue<double>(SettingsKey.nozzle_diameter));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'General' -> 'Layers/Surface'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				// Print recovery can only work with a manually leveled or software leveled bed. Hardware leveling does not work.
				if (GetValue<bool>(SettingsKey.recover_is_enabled))
				{
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Printer' -> 'Print Recovery' -> 'Enable Recovery'".Localize();
					string[] startGCode = GetValue("start_gcode").Replace("\\n", "\n").Split('\n');
					foreach (string startGCodeLine in startGCode)
					{
						if (startGCodeLine.StartsWith("G29"))
						{
							string error = "Start G-Code cannot contain G29 if Print Recovery is enabled.".Localize();
							string details = "Your Start G-Code should not contain a G29 if you are planning on using Print Recovery. Change your start G-Code or turn off Print Recovery".Localize();
							StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
							return false;
						}

						if (startGCodeLine.StartsWith("G30"))
						{
							string error = "Start G-Code cannot contain G30 if Print Leveling is enabled.".Localize();
							string details = "Your Start G-Code should not contain a G30 if you are planning on using Print Recovery. Change your start G-Code or turn off Print Recovery".Localize();
							StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
							return false;
						}
					}
				}

				// If we have print leveling turned on then make sure we don't have any leveling commands in the start gcode.
				if (GetValue<bool>(SettingsKey.print_leveling_enabled))
				{
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Printer' -> 'Custom G-Code' -> 'Start G-Code'".Localize();
					string[] startGCode = GetValue("start_gcode").Replace("\\n", "\n").Split('\n');
					foreach (string startGCodeLine in startGCode)
					{
						if (startGCodeLine.StartsWith("G29"))
						{
							string error = "Start G-Code cannot contain G29 if Print Leveling is enabled.".Localize();
							string details = "Your Start G-Code should not contain a G29 if you are planning on using print leveling. Change your start G-Code or turn off print leveling".Localize();
							StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
							return false;
						}

						if (startGCodeLine.StartsWith("G30"))
						{
							string error = "Start G-Code cannot contain G30 if Print Leveling is enabled.".Localize();
							string details = "Your Start G-Code should not contain a G30 if you are planning on using print leveling. Change your start G-Code or turn off print leveling".Localize();
							StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
							return false;
						}
					}
				}

				if (GetValue<double>(SettingsKey.first_layer_extrusion_width) > GetValue<double>(SettingsKey.nozzle_diameter) * 4)
				{
					string error = "'First Layer Extrusion Width' must be less than or equal to the 'Nozzle Diameter' * 4.".Localize();
					string details = string.Format("First Layer Extrusion Width = {0}\nNozzle Diameter = {1}".Localize(), GetValue(SettingsKey.first_layer_extrusion_width), GetValue<double>(SettingsKey.nozzle_diameter));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Extrusion' -> 'First Layer'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (GetValue<double>(SettingsKey.first_layer_extrusion_width) <= 0)
				{
					string error = "'First Layer Extrusion Width' must be greater than 0.".Localize();
					string details = string.Format("First Layer Extrusion Width = {0}".Localize(), GetValue(SettingsKey.first_layer_extrusion_width));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Extrusion' -> 'First Layer'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (GetValue<double>(SettingsKey.external_perimeter_extrusion_width) > GetValue<double>(SettingsKey.nozzle_diameter) * 4)
				{
					string error = "'External Perimeter Extrusion Width' must be less than or equal to the 'Nozzle Diameter' * 4.".Localize();
					string details = string.Format("External Perimeter Extrusion Width = {0}\nNozzle Diameter = {1}".Localize(), GetValue(SettingsKey.external_perimeter_extrusion_width), GetValue<double>(SettingsKey.nozzle_diameter));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Extrusion' -> 'External Perimeter'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (GetValue<double>(SettingsKey.external_perimeter_extrusion_width) <= 0)
				{
					string error = "'External Perimeter Extrusion Width' must be greater than 0.".Localize();
					string details = string.Format("External Perimeter Extrusion Width = {0}".Localize(), GetValue(SettingsKey.external_perimeter_extrusion_width));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Extrusion' -> 'External Perimeter'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (GetValue<double>(SettingsKey.min_fan_speed) > 100)
				{
					string error = "The Minimum Fan Speed can only go as high as 100%.".Localize();
					string details = string.Format("It is currently set to {0}.".Localize(), GetValue<double>(SettingsKey.min_fan_speed));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Cooling'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (GetValue<double>("max_fan_speed") > 100)
				{
					string error = "The Maximum Fan Speed can only go as high as 100%.".Localize();
					string details = string.Format("It is currently set to {0}.".Localize(), GetValue<double>("max_fan_speed"));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Cooling'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (GetValue<int>(SettingsKey.extruder_count) < 1)
				{
					string error = "The Extruder Count must be at least 1.".Localize();
					string details = string.Format("It is currently set to {0}.".Localize(), GetValue<int>(SettingsKey.extruder_count));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Printer' -> 'Features'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (GetValue<double>(SettingsKey.fill_density) < 0 || GetValue<double>(SettingsKey.fill_density) > 1)
				{
					string error = "The Fill Density must be between 0 and 1.".Localize();
					string details = string.Format("It is currently set to {0}.".Localize(), GetValue<double>(SettingsKey.fill_density));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'General' -> 'Infill'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (GetValue<double>(SettingsKey.fill_density) == 1
					&& GetValue("infill_type") != "LINES")
				{
					string error = "Solid Infill works best when set to LINES.".Localize();
					string details = string.Format("It is currently set to {0}.".Localize(), GetValue("infill_type"));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'General' -> 'Infill Type'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return true;
				}


				string normalSpeedLocation = "Location: 'Settings & Controls' -> 'Settings' -> 'General' -> 'Speed'".Localize();
				// If the given speed is part of the current slice engine then check that it is greater than 0.
				if (!ValidateGoodSpeedSettingGreaterThan0("bridge_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("external_perimeter_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0(SettingsKey.first_layer_speed, normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("gap_fill_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("infill_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("perimeter_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("small_perimeter_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("solid_infill_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("support_material_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("top_solid_infill_speed", normalSpeedLocation)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("travel_speed", normalSpeedLocation)) return false;

				string retractSpeedLocation = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Filament' -> 'Retraction'".Localize();
				if (!ValidateGoodSpeedSettingGreaterThan0("retract_speed", retractSpeedLocation)) return false;
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
				GuiWidget.BreakInDebugger();
				string stackTraceNoBackslashRs = e.StackTrace.Replace("\r", "");
				ContactFormWindow.Open("Parse Error while slicing".Localize(), e.Message + stackTraceNoBackslashRs);
				return false;
			}

			return true;
		}

		private bool ValidateGoodSpeedSettingGreaterThan0(string speedSetting, string speedLocation)
		{
			string actualSpeedValueString = GetValue(speedSetting);
			string speedValueString = actualSpeedValueString;
			if (speedValueString.EndsWith("%"))
			{
				speedValueString = speedValueString.Substring(0, speedValueString.Length - 1);
			}
			bool valueWasNumber = true;
			double speedToCheck;
			if (!double.TryParse(speedValueString, out speedToCheck))
			{
				valueWasNumber = false;
			}

			if (!valueWasNumber
				|| (ActiveSliceSettings.Instance.Helpers.ActiveSliceEngine().MapContains(speedSetting)
				&& speedToCheck <= 0))
			{
				SliceSettingData data = SliceSettingsOrganizer.Instance.GetSettingsData(speedSetting);
				if (data != null)
				{
					string error = string.Format("The '{0}' must be greater than 0.".Localize(), data.PresentationName);
					string details = string.Format("It is currently set to {0}.".Localize(), actualSpeedValueString);
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2} -> '{3}'", error, details, speedLocation, data.PresentationName), "Slice Error".Localize());
				}
				return false;
			}
			return true;
		}

		#endregion

		[JsonIgnore]
		private static HashSet<string> knownSettings;
		public static HashSet<string> KnownSettings
		{
			get
			{
				if (knownSettings == null)
				{
					knownSettings = LoadSettingsNamesFromPropertiesJson();
				}

				return knownSettings;
			}
		}

		private static HashSet<string> LoadSettingsNamesFromPropertiesJson()
		{
			string propertiesJson = StaticData.Instance.ReadAllText(Path.Combine("SliceSettings", "Properties.json"));
			var settingsData = JArray.Parse(propertiesJson);

			return new HashSet<string>(settingsData.Select(s => s["SlicerConfigName"].Value<string>()));
		}

		public void SetValue(string settingsKey, string settingsValue, PrinterSettingsLayer layer = null)
		{
			// Stash user overrides if a non-user override is being set
			if (layer != null && layer != UserLayer)
			{
				StashUserOverride(layer, settingsKey);
			}
			else
			{
				// Remove any staged/conflicting user override, making this the new and active user override
				if (StagedUserSettings.ContainsKey(settingsKey))
				{
					StagedUserSettings.Remove(settingsKey);
				}
			}

			var persistenceLayer = layer ?? UserLayer;

			// If the setting exists and is set the requested value, exit without setting or saving
			string existingValue;
			if (persistenceLayer.TryGetValue(settingsKey, out existingValue) && existingValue == settingsValue)
			{
				return;
			}

			// Otherwise, set and save
			persistenceLayer[settingsKey] = settingsValue;
			Save();

			ActiveSliceSettings.OnSettingChanged(settingsKey);
		}

		public string ToJson(Formatting formatting = Formatting.Indented)
		{
			return JsonConvert.SerializeObject(this, formatting);
		}

		internal void ClearValue(string settingsKey, PrinterSettingsLayer layer = null)
		{
			var persistenceLayer = layer ?? UserLayer;
			if (persistenceLayer.ContainsKey(settingsKey))
			{
				persistenceLayer.Remove(settingsKey);

				// Restore user overrides if a non-user override is being cleared
				if (layer != null && layer != UserLayer)
				{
					RestoreUserOverride(layer, settingsKey);
				}

				Save();

				ActiveSliceSettings.OnSettingChanged(settingsKey);
			}
		}
	}
}