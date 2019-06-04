/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.IO;
using System.Linq;
using System.Text;
using MatterHackers.Agg;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public enum NamedSettingsLayers
	{
		MHBaseSettings,
		OEMSettings,
		Quality,
		Material,
		User,
		All
	}

	public enum BedShape
	{
		Rectangular,
		Circular
	}

	[JsonConverter(typeof(StringEnumConverter))]
	public enum LevelingSystem
	{
		Probe3Points,
		Probe7PointRadial,
		Probe13PointRadial,
		Probe100PointRadial,
		Probe3x3Mesh,
		Probe5x5Mesh,
		Probe10x10Mesh,
		ProbeCustom
	}

	public class PrinterSettings
	{
		// Latest version should be in the form of:
		// Year|month|day|versionForDay (to support multiple revisions on a given day)
		public static int LatestVersion { get; } = 201606271;

		public static Dictionary<string, SliceSettingData> SettingsData { get; }

		public static SettingsLayout Layout { get; }

		public static event EventHandler<StringEventArgs> AnyPrinterSettingChanged;

		public event EventHandler<StringEventArgs> SettingChanged;

		public void OnSettingChanged(string slicerConfigName)
		{
			if (slicerConfigName == SettingsKey.t0_inset
				|| slicerConfigName == SettingsKey.t1_inset
				|| slicerConfigName == SettingsKey.bed_size
				|| slicerConfigName == SettingsKey.print_center)
			{
				this.ResetHotendBounds();
			}

			SettingChanged?.Invoke(this, new StringEventArgs(slicerConfigName));
			AnyPrinterSettingChanged?.Invoke(this, new StringEventArgs(slicerConfigName));
		}

		public event EventHandler PrintLevelingEnabledChanged;

		public event EventHandler MacrosChanged;

		public static PrinterSettings Empty { get; }

		public int DocumentVersion { get; set; } = LatestVersion;

		public string ID { get; set; }

		[JsonIgnore]
		public PrinterSettingsLayer QualityLayer { get; private set; }

		[JsonIgnore]
		public PrinterSettingsLayer MaterialLayer { get; private set; }

		public PrinterSettingsLayer StagedUserSettings { get; set; } = new PrinterSettingsLayer();

		static PrinterSettings()
		{
			// Convert settings array into dictionary on initial load using settings key (SlicerConfigName)
			PrinterSettings.SettingsData = SliceSettingsFields.AllSettings().ToDictionary(s => s.SlicerConfigName);

			PrinterSettings.Layout = new SettingsLayout();

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
			if (StagedUserSettings.TryGetValue(settingsKey, out string stagedUserOverride))
			{
				StagedUserSettings.Remove(settingsKey);
				UserLayer[settingsKey] = stagedUserOverride;
			}
		}

		public void NotifyMacrosChanged()
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
			if (this.UserLayer.TryGetValue(settingsKey, out string userOverride))
			{
				this.UserLayer.Remove(settingsKey);
				this.StagedUserSettings[settingsKey] = userOverride;
			}
		}

		public PrinterSettingsLayer OemLayer { get; set; }

		public void Merge(PrinterSettingsLayer destinationLayer, PrinterSettings settingsToImport, List<PrinterSettingsLayer> rawSourceFilter, bool setLayerName)
		{
			var skipKeys = new HashSet<string>
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

			this.OnSettingChanged("na");
		}

		public void CopyFrom(PrinterSettings printerSettings)
		{
			this.OemLayer = printerSettings.OemLayer;
			this.MaterialLayers = printerSettings.MaterialLayers;
			this.QualityLayers = printerSettings.QualityLayers;
			this.UserLayer = printerSettings.UserLayer;

			this.ID = printerSettings.ID;
			this.QualityLayer = GetQualityLayer(printerSettings.ActiveQualityKey);
			this.MaterialLayer = GetMaterialLayer(printerSettings.ActiveMaterialKey);
			this.StagedUserSettings = printerSettings.StagedUserSettings;
			this.Macros = printerSettings.Macros;
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

		// Properties

		[JsonIgnore]
		public string ActiveQualityKey
		{
			get => GetValue(SettingsKey.active_quality_key);
			set
			{
				if (this.ActiveQualityKey != value)
				{
					SetValue(SettingsKey.active_quality_key, value);
					QualityLayer = GetQualityLayer(value);
				}
			}
		}

		[JsonIgnore]
		public string ActiveMaterialKey
		{
			get => GetValue(SettingsKey.active_material_key);
			set
			{
				if (this.ActiveMaterialKey != value)
				{
					SetValue(SettingsKey.active_material_key, value);
					MaterialLayer = GetMaterialLayer(value);
				}
			}
		}

		[JsonIgnore]
		public RectangleDouble BedBounds
		{
			get
			{
				var bedSize = this.GetValue<Vector2>(SettingsKey.bed_size);
				var printCenter = this.GetValue<Vector2>(SettingsKey.print_center);

				return new RectangleDouble(printCenter.X - bedSize.X / 2,
					printCenter.Y - bedSize.Y / 2,
					printCenter.X + bedSize.X / 2,
					printCenter.Y + bedSize.Y / 2);
			}
		}

		private void ResetHotendBounds()
		{
			var bounds = this.BedBounds;

			RectangleDouble GetHotendBounds(int index)
			{
				string settingsKey = index == 0 ? SettingsKey.t0_inset : SettingsKey.t1_inset;
				var inset = this.GetValue<Vector4>(settingsKey);

				return new RectangleDouble(
					bounds.Left + inset.X,
					bounds.Bottom + inset.Y,
					bounds.Right - inset.Z,
					bounds.Top - inset.W);
			}

			this.ToolBounds = new[]
			{
				GetHotendBounds(0),
				GetHotendBounds(1),
			};
		}

		/// <summary>
		/// Gets the bounds that are accessible for a given hotend
		/// </summary>
		[JsonIgnore]
		public RectangleDouble[] ToolBounds { get; private set; }

		[JsonIgnore]
		public bool AutoSave { get; set; } = true;

		public string ComputeSHA1()
		{
			return ComputeSHA1(this.ToJson());
		}

		public string ComputeSHA1(string json)
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
		public PrinterSettingsLayer UserLayer { get; private set; } = new PrinterSettingsLayer();

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
				var settings = JsonConvert.DeserializeObject<PrinterSettings>(File.ReadAllText(printerProfilePath), new PrinterSettingsConverter());
				settings.OnDeserialize();

				return settings;
			}
			catch
			{
				return null;
			}
		}

		private void OnDeserialize()
		{
			this.ResetHotendBounds();
		}

		internal void OnPrintLevelingEnabledChanged(object s, EventArgs e)
		{
			PrintLevelingEnabledChanged?.Invoke(s, e);
		}

		public List<PrinterSettingsLayer> MaterialLayers { get; private set; } = new List<PrinterSettingsLayer>();

		public List<PrinterSettingsLayer> QualityLayers { get; private set; } = new List<PrinterSettingsLayer>();

		/// <summary>
		/// Gets the first matching value discovered while enumerating the settings layers
		/// </summary>
		public string GetValue(string sliceSetting, IEnumerable<PrinterSettingsLayer> layerCascade = null)
		{
			if (layerCascade == null)
			{
				layerCascade = defaultLayerCascade;
			}

			foreach (PrinterSettingsLayer layer in layerCascade)
			{
				if (layer.TryGetValue(sliceSetting, out string value))
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

		private PrinterSettingsLayer _baseLayer;

		[JsonIgnore]
		public PrinterSettingsLayer BaseLayer
		{
			get
			{
				if (_baseLayer == null)
				{
					var settingsLayer = new PrinterSettingsLayer();

					foreach (var settingsData in PrinterSettings.SettingsData.Values)
					{
						settingsLayer[settingsData.SlicerConfigName] = settingsData.DefaultValue;
					}

					_baseLayer = settingsLayer;
				}

				return _baseLayer;
			}
		}

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

		public IEnumerable<PrinterSettingsLayer> GetDefaultLayerCascade() => defaultLayerCascade;

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
			foreach (var item in PrinterSettings.SettingsData.Values.Where(settingsItem => settingsItem.ShowAsOverride == false))
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
		public T GetValue<T>(string settingsKey, IEnumerable<PrinterSettingsLayer> layerCascade = null)
		{
#if DEBUG
			ValidateType<T>(settingsKey);
#endif

			if (layerCascade == null)
			{
				layerCascade = defaultLayerCascade;
			}

			string settingsValue = null;

			foreach (PrinterSettingsLayer layer in layerCascade)
			{
				if (layer.TryGetValue(settingsKey, out settingsValue))
				{
					break;
				}
			}

			if (settingsValue == null)
			{
				settingsValue = "";
			}

			if (typeof(T) == typeof(string))
			{
				// this way we can use the common pattern without error
				return (T)(object)settingsValue;
			}
			else if (typeof(T) == typeof(LevelingSystem))
			{
				switch (settingsValue)
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
			}
			else if (typeof(T) == typeof(bool))
			{
				return (T)(object)(settingsValue == "1");
			}
			else if (typeof(T) == typeof(int))
			{
				int.TryParse(settingsValue, out int result);
				return (T)(object)(result);
			}
			else if (typeof(T) == typeof(Vector2))
			{
				return (T)(object)Vector2.Parse(settingsValue);
			}
			else if (typeof(T) == typeof(Vector3))
			{
				return (T)(object)Vector3.Parse(settingsValue);
			}
			else if (typeof(T) == typeof(Vector4))
			{
				return (T)(object)Vector4.Parse(settingsValue);
			}
			else if (typeof(T) == typeof(double))
			{
				if (settingsValue.Contains("%"))
				{
					// Remove % and parse out double value
					double.TryParse(settingsValue.Replace("%", ""), out double doubleValue);

					double ratio = doubleValue / 100;

					if (settingsKey == SettingsKey.first_layer_height)
					{
						return (T)(object)(this.GetValue<double>(SettingsKey.layer_height) * ratio);
					}
					else if (settingsKey == SettingsKey.first_layer_extrusion_width
						|| settingsKey == SettingsKey.external_perimeter_extrusion_width)
					{
						return (T)(object)(this.GetValue<double>(SettingsKey.nozzle_diameter) * ratio);
					}

					return (T)(object)(ratio);
				}
				else if (settingsKey == SettingsKey.first_layer_extrusion_width
					|| settingsKey == SettingsKey.external_perimeter_extrusion_width)
				{
					double.TryParse(settingsValue, out double extrusionResult);
					return (T)(object)(extrusionResult == 0 ? this.GetValue<double>(SettingsKey.nozzle_diameter) : extrusionResult);
				}

				if (settingsKey == SettingsKey.bed_temperature
					&& !this.GetValue<bool>(SettingsKey.has_heated_bed))
				{
					return (T)Convert.ChangeType(0, typeof(double));
				}

				double.TryParse(settingsValue, out double result);
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

		public ulong GetGCodeCacheKey()
		{
			var bigStringForHashCode = new StringBuilder();

			// Loop over all known settings
			foreach (var keyValue in PrinterSettings.SettingsData)
			{
				// Add key/value to accumulating string for hash
				if (keyValue.Value?.RebuildGCodeOnChange == true)
				{
					bigStringForHashCode.Append(keyValue.Key);
					bigStringForHashCode.Append(this.GetValue(keyValue.Key));
				}
			}

			return bigStringForHashCode.ToString().GetLongHashCode();
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
			return new HashSet<string>(PrinterSettings.SettingsData.Keys);
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
			if (persistenceLayer.TryGetValue(settingsKey, out string existingValue) 
				&& existingValue == settingsValue)
			{
				return;
			}

			// Otherwise, set and save
			persistenceLayer[settingsKey] = settingsValue;

			this.OnSettingChanged(settingsKey);
		}

		public string ToJson()
		{
			return JsonConvert.SerializeObject(this, Formatting.Indented);
		}

		public void ClearValue(string settingsKey, PrinterSettingsLayer layer = null)
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

				if (PrinterSettings.SettingsData.TryGetValue(settingsKey, out SliceSettingData settingData))
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

				this.OnSettingChanged(settingsKey);
			}
		}

		/// <summary>
		/// Provides a one-way import mechanism for ActiveMaterialKey from the retired MaterialSettingsKeys array
		/// </summary>
		private class PrinterSettingsConverter : JsonConverter
		{
			public override bool CanConvert(Type objectType)
			{
				return objectType == typeof(PrinterSettings);
			}

			public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
			{
				// Load object from reader
				JObject jObject = JObject.Load(reader);

				// Deserialize using default serializer
				var settings = jObject.ToObject<PrinterSettings>();

				// Look for and import retired MaterialSettingsKeys property if ActiveMaterialKey unset
				if (string.IsNullOrWhiteSpace(settings.ActiveMaterialKey)
					&& jObject["MaterialSettingsKeys"] is JArray materialSettingsKeys
					&& materialSettingsKeys.Count > 0)
				{
					string firstValue = materialSettingsKeys[0].Value<string>();
					settings.UserLayer[SettingsKey.active_material_key] = firstValue;
				}

				settings.QualityLayer = settings.GetQualityLayer(settings.ActiveQualityKey);

				if (!string.IsNullOrEmpty(settings.ActiveMaterialKey))
				{
					settings.MaterialLayer = settings.GetMaterialLayer(settings.ActiveMaterialKey);
				}

				// Migrate deprecated OemLayer probe setting
				if (settings.OemLayer.ContainsKey("z_probe_z_offset"))
				{
					MigrateProbeOffset(settings.OemLayer);
				}

				// Migrate deprecated UserLayer probe setting
				if (settings.UserLayer.ContainsKey("z_probe_z_offset"))
				{
					MigrateProbeOffset(settings.UserLayer, settings.GetValue<Vector3>(SettingsKey.probe_offset));
				}

				return settings;
			}

			private static void MigrateProbeOffset(PrinterSettingsLayer settingsLayer, Vector3 position = default(Vector3))
			{
				if (settingsLayer.ContainsKey("z_probe_xy_offset")
					|| settingsLayer.ContainsKey("z_probe_z_offset"))
				{
					if (double.TryParse(settingsLayer["z_probe_z_offset"], out double zOffset))
					{
						// and  negate it as it was stored in the opposite direction before
						position.Z = -zOffset;
					}

					if (settingsLayer.ContainsKey("z_probe_xy_offset"))
					{
						var probeXyOffset = settingsLayer["z_probe_xy_offset"];
						if (!string.IsNullOrEmpty(probeXyOffset))
						{
							var split = probeXyOffset.Split(',');
							if (split.Length == 2)
							{
								double.TryParse(split[0], out position.X);
								double.TryParse(split[1], out position.Y);
							}
						}
					}

					settingsLayer[SettingsKey.probe_offset] = $"{position.X},{position.Y},{position.Z}";

					// clear it
					settingsLayer.Remove("z_probe_z_offset");
					settingsLayer.Remove("z_probe_xy_offset");
				}
			}

			public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
			{
				throw new NotImplementedException();
			}

			public override bool CanRead => true;

			public override bool CanWrite => false;
		}
	}
}
