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
using System.Runtime.Serialization;
using System.Text;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public enum NamedSettingsLayers { MHBaseSettings, OEMSettings, Quality, Material, User, All }

	public enum BedShape { Rectangular, Circular };

	[JsonConverter(typeof(StringEnumConverter))]
	public enum LevelingSystem { Probe3Points, Probe7PointRadial, Probe13PointRadial, Probe100PointRadial, Probe3x3Mesh, Probe5x5Mesh, Probe10x10Mesh, ProbeCustom }

	public class PrinterSettings
	{
		// Latest version should be in the form of:
		// Year|month|day|versionForDay (to support multiple revisions on a given day)
		public static int LatestVersion { get; } = 201606271;

		public static EventHandler AnyPrinterSettingChanged;

		public event EventHandler SettingChanged;

		public event EventHandler MaterialPresetChanged;

		public void OnMaterialPresetChanged()
		{
			MaterialPresetChanged?.Invoke(null, null);
		}

		public void OnSettingChanged(string slicerConfigName)
		{
			SettingChanged?.Invoke(this, new StringEventArgs(slicerConfigName));
			AnyPrinterSettingChanged?.Invoke(this, new StringEventArgs(slicerConfigName));
		}

		public event EventHandler PrintLevelingEnabledChanged;

		public event EventHandler MacrosChanged;

		public static PrinterSettings Empty { get; }

		public int DocumentVersion { get; set; } = LatestVersion;

		public string ID { get; set; }

		private static object writeLock = new object();

		[JsonIgnore]
		internal PrinterSettingsLayer QualityLayer { get; private set; }

		[JsonIgnore]
		internal PrinterSettingsLayer MaterialLayer { get; private set; }

		public PrinterSettingsLayer StagedUserSettings { get; set; } = new PrinterSettingsLayer();


		static PrinterSettings()
		{
			Empty = new PrinterSettings() { ID = "EmptyProfile" };
			Empty.UserLayer[SettingsKey.printer_name] = "Empty Printer";
		}

		public PrinterSettings()
		{
			this.Helpers = new SettingsHelpers(this);
		}

		public List<GCodeMacro> Macros { get; set; } = new List<GCodeMacro>();

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

		internal void NotifyMacrosChanged()
		{
			this.MacrosChanged?.Invoke(this, null);
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
		/// Determines if a given field should be shown given its filter
		/// </summary>
		/// <param name="filter">The view filter - order of precedence &amp;, |, !, =</param>
		/// <returns>An indicator if the field should be shown given the current filter</returns>
		public bool ParseShowString(string filter)
		{
			if (!string.IsNullOrEmpty(filter))
			{
				string[] splitOnAnd = filter.Split('&');
				foreach (var andGroup in splitOnAnd)
				{
					bool orResult = false;
					string[] splitOnOr = andGroup.Split('|');
					foreach (var orGroup in splitOnOr)
					{
						var matchString = "1";
						var orItem = orGroup;
						bool negate = orItem.StartsWith("!");
						if (negate)
						{
							orItem = orItem.Substring(1);
						}

						string sliceSettingValue = "";
						if (orItem.Contains("="))
						{
							string[] splitOnEquals = orItem.Split('=');

							sliceSettingValue = this.GetValue(splitOnEquals[0]);
							matchString = splitOnEquals[1];
						}
						else if (orItem.Contains(">"))
						{
							matchString = "no_match";
							string[] splitOnGreater = orItem.Split('>');

							sliceSettingValue = this.GetValue(splitOnGreater[0]);
							if (double.TryParse(sliceSettingValue, out double doubleValue))
							{
								if (double.TryParse(splitOnGreater[1], out double greater))
								{
									if (doubleValue > greater)
									{
										matchString = sliceSettingValue;
									}
								}
							}
						}
						else
						{
							sliceSettingValue = this.GetValue(orItem);
						}

						if ((!negate && sliceSettingValue == matchString)
							|| (negate && sliceSettingValue != matchString))
						{
							orResult = true;
						}
					}

					if (orResult == false)
					{
						return false;
					}
				}
			}

			return true;
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

			if (!string.IsNullOrEmpty(ActiveMaterialKey))
			{
				MaterialLayer = GetMaterialLayer(ActiveMaterialKey);
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
						&& currentValue != importValue
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

		public string ActiveMaterialKey
		{
			get
			{
				if (MaterialSettingsKeys.Count > 0)
				{
					return MaterialSettingsKeys[0];
				}

				return null;
			}
			internal set
			{
				SetMaterialPreset(0, value);
			}
		}

		private void SetMaterialPreset(int extruderIndex, string materialKey)
		{
			if (extruderIndex >= PrinterCommunication.PrinterConnection.MAX_EXTRUDERS)
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

				this.OnMaterialPresetChanged();
			}

			Save();
		}

		public List<string> MaterialSettingsKeys { get; set; } = new List<string>();

		[JsonIgnore]
		public string DocumentPath => ProfileManager.Instance.ProfilePath(this.ID);

		[JsonIgnore]
		public bool AutoSave { get; set; } = true;

		Dictionary<string, string> blackListSettings = new Dictionary<string, string>()
		{
			[SettingsKey.spiral_vase] = "0",
			[SettingsKey.layer_to_pause] = "",
			[SettingsKey.print_leveling_data] = "",
			[SettingsKey.print_leveling_enabled] = "0",
			[SettingsKey.probe_has_been_calibrated] = "0",
			[SettingsKey.filament_has_been_loaded] = "0"
		};

		public void Save(bool clearBlackListSettings = false)
		{
			// Skip save operation if on the EmptyProfile
			if (!this.PrinterSelected || !this.AutoSave)
			{
				return;
			}

			if(clearBlackListSettings)
			{
				foreach(var kvp in blackListSettings)
				{
					if (UserLayer.ContainsKey(kvp.Key))
					{
						UserLayer.Remove(kvp.Key);
					}
					OemLayer[kvp.Key] = kvp.Value;
				}
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
					printerInfo.ContentSHA1 = this.ComputeSHA1(json);
					ProfileManager.Instance.Save();
				}

				File.WriteAllText(filePath, json);
			}

			if (ApplicationController.Instance.ActivePrinters.FirstOrDefault(p => p.Settings.ID == this.ID) is PrinterConfig printer)
			{
				ApplicationController.Instance.ActiveProfileModified.CallEvents(printer.Settings, null);
			}
		}

		internal string ComputeSHA1()
		{
			return ComputeSHA1(this.ToJson());
		}

		private string ComputeSHA1(string json)
		{
			// SHA1 value is based on UTF8 encoded file contents
			using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
			{
				return HashGenerator.ComputeSHA1(memoryStream);
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

		internal void OnPrintLevelingEnabledChanged(object s, EventArgs e)
		{
			PrintLevelingEnabledChanged?.Invoke(s, e);
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

		public bool IsOverride(string sliceSetting)
		{
			var values = new List<string>();

			string firstBaseValue = null;

			foreach (PrinterSettingsLayer layer in defaultLayerCascade)
			{
				if (layer.TryGetValue(sliceSetting, out string value))
				{
					if (layer == this.BaseLayer
						|| layer == this.OemLayer)
					{
						firstBaseValue = value;
						break;
					}

					values.Add(value);
				}
			}

			string currentValue = values.FirstOrDefault();

			string firstPresetValue = values.Skip(1).FirstOrDefault();

			bool differsFromPreset = values.Count > 0
				&& firstPresetValue != null
				&& firstPresetValue != currentValue;

			bool differsFromBase = currentValue != firstBaseValue;

			return currentValue != null
				&& (differsFromPreset || differsFromBase);
		}

		// Helper method to debug settings layers per setting
		public List<(string layerName, string currentValue)> GetLayerValues(string sliceSetting, IEnumerable<PrinterSettingsLayer> layerCascade = null)
		{
			if (layerCascade == null)
			{
				layerCascade = defaultLayerCascade;
			}

			var results = new List<(string layerName, string currentValue)>();

			foreach (PrinterSettingsLayer layer in layerCascade)
			{
				if (layer.TryGetValue(sliceSetting, out string value))
				{
					string layerName = "User";

					if (layer == this.BaseLayer)
					{
						layerName = "Base";
					}
					else if (layer == this.OemLayer)
					{
						layerName = "Oem";
					}
					else if (layer == this.MaterialLayer)
					{
						layerName = "Material";
					}
					else if (layer == this.QualityLayer)
					{
						layerName = "Quality";
					}

					results.Add((layerName, value));
				}
			}

			return results;
		}

		public (string currentValue, string layerName) GetValueAndLayerName(string sliceSetting, IEnumerable<PrinterSettingsLayer> layerCascade = null)
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
					string layerName = "User";

					if (layer == this.BaseLayer)
					{
						layerName = "Base";
					}
					else if (layer == this.OemLayer)
					{
						layerName = "Oem";
					}
					else if (layer == this.MaterialLayer)
					{
						layerName = "Material";
					}
					else if (layer == this.QualityLayer)
					{
						layerName = "Quality";
					}

					return (value, layerName);
				}
			}

			return ("", "");
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

		PrinterSettingsLayer _baseLayer;
		[JsonIgnore]
		public PrinterSettingsLayer BaseLayer
		{
			get
			{
				if (_baseLayer == null)
				{
					string propertiesFileContents = AggContext.StaticData.ReadAllText(Path.Combine("SliceSettings", "Properties.json"));

					var settingsLayer = new PrinterSettingsLayer();
					foreach (var settingsData in JsonConvert.DeserializeObject<List<SliceSettingData>>(propertiesFileContents))
					{
						settingsLayer[settingsData.SlicerConfigName] = settingsData.DefaultValue;
					}

					_baseLayer = settingsLayer;
				}

				return _baseLayer;
			}
		}

		internal IEnumerable<PrinterSettingsLayer> defaultLayerCascade
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
			foreach (var item in SettingsOrganizer.SettingsData.Values.Where(settingsItem => settingsItem.ShowAsOverride == false))
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
			if (typeof(T) == typeof(string))
			{
				// this way we can use the common pattern without error
				return (T)(object)this.GetValue(settingsKey);
			}
			else if(typeof(T) == typeof(LevelingSystem))
			{
				switch(this.GetValue(settingsKey))
				{
					case "3 Point Plane":
						return (T)(object)(LevelingSystem.Probe3Points);
					case "7 Point Disk":
						return (T)(object)(LevelingSystem.Probe7PointRadial);
					case "13 Point Disk":
						return (T)(object)(LevelingSystem.Probe13PointRadial);
					case "100 Point Disk":
						return (T)(object)(LevelingSystem.Probe100PointRadial);
					case "3x3 Mesh":
						return (T)(object)(LevelingSystem.Probe3x3Mesh);
					case "5x5 Mesh":
						return (T)(object)(LevelingSystem.Probe5x5Mesh);
					case "10x10 Mesh":
						return (T)(object)(LevelingSystem.Probe10x10Mesh);
					case "Custom Points":
						return (T)(object)(LevelingSystem.ProbeCustom);
					default:
#if DEBUG
						throw new NotImplementedException();
#else
						break;
#endif
				}

				return (T)(object)(LevelingSystem.Probe3Points);
			}
			else if (typeof(T) == typeof(bool))
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
					throw new Exception("Not parsing {0} as a Vector2".FormatWith(settingsKey));
				}
				Vector2 valueAsVector2 = new Vector2();
				valueAsVector2.X = Helpers.ParseDouble(twoValues[0]);
				valueAsVector2.Y = Helpers.ParseDouble(twoValues[1]);
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
					else if (settingsKey == SettingsKey.first_layer_extrusion_width
						|| settingsKey == SettingsKey.external_perimeter_extrusion_width)
					{
						return (T)(object)(GetValue<double>(SettingsKey.nozzle_diameter) * ratio);
					}

					return (T)(object)(ratio);
				}
				else if (settingsKey == SettingsKey.first_layer_extrusion_width
					|| settingsKey == SettingsKey.external_perimeter_extrusion_width)
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
						throw new NotImplementedException("{0} is not a known bed_shape.".FormatWith(GetValue(SettingsKey.bed_shape)));
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
				// Add key/value to accumulating string for hash
				SliceSettingData data = SettingsOrganizer.Instance.GetSettingsData(keyValue.Key);
				if (data?.RebuildGCodeOnChange == true)
				{
					bigStringForHashCode.Append(keyValue.Key);
					bigStringForHashCode.Append(this.GetValue(keyValue.Key));
				}
			}

			return agg_basics.ComputeHash(bigStringForHashCode.ToString());
		}

		#endregion

		private static HashSet<string> knownSettings;
		[JsonIgnore]
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
			string propertiesJson = AggContext.StaticData.ReadAllText(Path.Combine("SliceSettings", "Properties.json"));
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

			// If the setting exists and is set to the requested value, exit without setting or saving
			string existingValue;
			if (persistenceLayer.TryGetValue(settingsKey, out existingValue) && existingValue == settingsValue)
			{
				return;
			}

			// Otherwise, set and save
			persistenceLayer[settingsKey] = settingsValue;
			Save();

			this.OnSettingChanged(settingsKey);
		}

		public string ToJson()
		{
			return JsonConvert.SerializeObject(this, Formatting.Indented);
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

				if (SettingsOrganizer.SettingsData.TryGetValue(settingsKey, out SliceSettingData settingData))
				{
					if (settingData.DataEditType == SliceSettingData.DataEditTypes.CHECK_BOX)
					{
						string checkedKey = this.GetValue<bool>(settingsKey) ? "OnValue" : "OffValue";

						// Linked settings should be updated in all cases (user clicked checkbox, user clicked clear)
						foreach (var setSettingsData in settingData.SetSettingsOnChange)
						{
							if (setSettingsData.TryGetValue(checkedKey, out string targetValue))
							{
								if (this.GetValue(setSettingsData["TargetSetting"]) != targetValue)
								{
									this.SetValue(setSettingsData["TargetSetting"], targetValue);
								}
							}
						}
					}
				}

				Save();

				this.OnSettingChanged(settingsKey);
			}
		}
	}
}
