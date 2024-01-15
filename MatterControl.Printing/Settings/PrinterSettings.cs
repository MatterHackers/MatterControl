﻿/*
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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
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

	public enum NamedSettingsLayers
	{
		MHBaseSettings,
		OEMSettings,
		Quality,
		Material,
		Scene,
		User,
		All
	}

	public class PrinterSettings
	{
		/// <summary>
		/// Application level settings control MatterControl behaviors but aren't used or passed through to the slice engine. Putting settings
		/// in this list ensures they show up for all slice engines and the lack of a MappedSetting for the engine guarantees that it won't pass
		/// through into the slicer config file.
		/// </summary>
		public static readonly HashSet<string> DefaultFFFSettings = new HashSet<string>()
		{
			SettingsKey.additional_printing_errors,
			SettingsKey.auto_connect,
			SettingsKey.auto_release_motors,
			SettingsKey.backup_firmware_before_update,
			SettingsKey.baud_rate,
			SettingsKey.bed_remove_part_temperature,
			SettingsKey.bed_shape,
			SettingsKey.bed_size,
			SettingsKey.bed_surface,
			SettingsKey.bed_temperature,
			SettingsKey.bed_temperature_blue_tape,
			SettingsKey.bed_temperature_buildtak,
			SettingsKey.bed_temperature_garolite,
			SettingsKey.bed_temperature_glass,
			SettingsKey.bed_temperature_kapton,
			SettingsKey.bed_temperature_pei,
			SettingsKey.bed_temperature_pp,
			SettingsKey.before_toolchange_gcode,
			SettingsKey.before_toolchange_gcode_1,
			SettingsKey.before_toolchange_gcode_2,
			SettingsKey.before_toolchange_gcode_3,
			SettingsKey.build_height,
			SettingsKey.cancel_gcode,
			SettingsKey.clear_bed_gcode,
			SettingsKey.com_port,
			SettingsKey.conductive_pad_center,
			SettingsKey.conductive_probe_min_z,
			SettingsKey.connect_gcode,
			SettingsKey.create_brim,
			SettingsKey.create_raft,
			SettingsKey.create_skirt,
			SettingsKey.created_date,
			SettingsKey.emulate_endstops,
			SettingsKey.enable_fan,
			SettingsKey.enable_firmware_sounds,
			SettingsKey.enable_line_splitting,
			SettingsKey.enable_network_printing,
			SettingsKey.enable_retractions,
			SettingsKey.enable_sailfish_communication,
			SettingsKey.extruder_offset,
			SettingsKey.extruder_wipe_temperature,
			SettingsKey.extruders_share_temperature,
			SettingsKey.filament_1_has_been_loaded,
			SettingsKey.filament_cost,
			SettingsKey.filament_density,
			SettingsKey.filament_has_been_loaded,
			SettingsKey.filament_runout_sensor,
			SettingsKey.firmware_type,
			SettingsKey.first_layer_bed_temperature,
			SettingsKey.g0,
			SettingsKey.has_c_axis,
			SettingsKey.has_conductive_nozzle,
			SettingsKey.has_fan,
			SettingsKey.has_fan_per_extruder,
			SettingsKey.has_hardware_leveling,
			SettingsKey.has_heated_bed,
			SettingsKey.has_independent_z_motors,
			SettingsKey.has_power_control,
			SettingsKey.has_sd_card_reader,
			SettingsKey.has_swappable_bed,
			SettingsKey.has_z_probe,
			SettingsKey.has_z_servo,
			SettingsKey.heat_extruder_before_homing,
			SettingsKey.inactive_cool_down,
			SettingsKey.include_firmware_updater,
			SettingsKey.insert_filament_1_markdown,
			SettingsKey.insert_filament_markdown2,
			SettingsKey.ip_address,
			SettingsKey.ip_port,
			SettingsKey.laser_speed_025,
			SettingsKey.laser_speed_100,
			SettingsKey.layer_to_pause,
			SettingsKey.leveling_sample_points,
			SettingsKey.load_filament_length,
			SettingsKey.load_filament_speed,
			SettingsKey.make,
			SettingsKey.material_color,
			SettingsKey.material_color_1,
			SettingsKey.material_color_2,
			SettingsKey.material_color_3,
			SettingsKey.material_sku,
			SettingsKey.max_print_speed,
			SettingsKey.measure_probe_offset_conductively,
			SettingsKey.model,
			SettingsKey.number_of_first_layers,
			SettingsKey.pause_gcode,
			SettingsKey.print_center,
			SettingsKey.print_leveling_insets,
			SettingsKey.print_leveling_probe_start,
			SettingsKey.print_leveling_required_to_print,
			SettingsKey.print_leveling_solution,
			SettingsKey.print_time_estimate_multiplier,
			SettingsKey.printer_name,
			SettingsKey.printer_sku,
			SettingsKey.probe_has_been_calibrated,
			SettingsKey.probe_offset,
			SettingsKey.progress_reporting,
			SettingsKey.read_regex,
			SettingsKey.recover_first_layer_speed,
			SettingsKey.recover_is_enabled,
			SettingsKey.recover_position_before_z_home,
			SettingsKey.report_runout_sensor_data,
			SettingsKey.resume_gcode,
			SettingsKey.running_clean_1_markdown,
			SettingsKey.running_clean_markdown2,
			SettingsKey.runout_sensor_check_distance,
			SettingsKey.runout_sensor_trigger_ratio,
			SettingsKey.seconds_to_reheat,
			SettingsKey.selector_ip_address,
			SettingsKey.send_with_checksum,
			SettingsKey.show_reset_connection,
			SettingsKey.slice_engine,
			SettingsKey.solid_shell,
			SettingsKey.t0_inset,
			SettingsKey.t1_extrusion_move_speed_multiplier,
			SettingsKey.t1_inset,
			SettingsKey.temperature,
			SettingsKey.temperature1,
			SettingsKey.temperature2,
			SettingsKey.temperature3,
			SettingsKey.toolchange_gcode,
			SettingsKey.toolchange_gcode_1,
			SettingsKey.toolchange_gcode_2,
			SettingsKey.toolchange_gcode_3,
			SettingsKey.trim_filament_markdown,
			SettingsKey.unload_filament_length,
			SettingsKey.use_z_probe,
			SettingsKey.validate_layer_height,
			SettingsKey.validate_leveling,
			SettingsKey.validation_threshold,
			SettingsKey.write_regex,
			SettingsKey.xy_offsets_have_been_calibrated,
			SettingsKey.z_homes_to_max,
			SettingsKey.z_offset,
			SettingsKey.z_probe_samples,
			SettingsKey.z_servo_depolyed_angle,
			SettingsKey.z_servo_retracted_angle,
		};

		private static HashSet<string> knownSettings;

		private static HashSet<string> ScaledSpeedFields = new HashSet<string>()
		{
			SettingsKey.first_layer_speed,
			SettingsKey.external_perimeter_speed,
			SettingsKey.raft_print_speed,
			SettingsKey.infill_speed,
			SettingsKey.min_print_speed,
			SettingsKey.perimeter_speed,
			SettingsKey.retract_speed,
			SettingsKey.support_material_speed,
			SettingsKey.travel_speed,
			SettingsKey.load_filament_speed,
			SettingsKey.max_print_speed,
		};

		private PrinterSettingsLayer _baseLayer;

		private object _slicer = null;

		static PrinterSettings()
		{
            // Convert settings array into dictionary on initial load using settings key (SlicerConfigName)
            SettingsData = SliceSettingsFields.AllSettings().ToDictionary(s => s.SlicerConfigName);

            Layout = new SettingsLayout();

			Empty = new PrinterSettings() { ID = "EmptyProfile" };
			Empty.UserLayer[SettingsKey.printer_name] = "Empty Printer";
		}

		public PrinterSettings()
		{
			this.Helpers = new SettingsHelpers(this);
		}

		public static event EventHandler<StringEventArgs> AnyPrinterSettingChanged;

		public event EventHandler MacrosChanged;

		public event EventHandler PrintLevelingEnabledChanged;

		public event EventHandler<StringEventArgs> SettingChanged;

		public static PrinterSettings Empty { get; }

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

		// Latest version should be in the form of:
		// Year|month|day|versionForDay (to support multiple revisions on a given day)
		public static int LatestVersion { get; } = 201606271;

		public static SettingsLayout Layout { get; }

		public static Dictionary<string, SliceSettingData> SettingsData { get; }

		public static Dictionary<string, object> SliceEngines { get; } = new Dictionary<string, object>();

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

					// Deactivate conflicting user overrides by iterating the Material preset we've just switched to
					this.DeactivateConflictingUserOverrides(this.MaterialLayer);
				}
			}
		}

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

					// Deactivate conflicting user overrides by iterating the Quality preset we've just switched to
					this.DeactivateConflictingUserOverrides(this.QualityLayer);
				}
			}
		}

		[JsonIgnore]
		public bool AutoSave { get; set; } = true;

		[JsonIgnore]
		public PrinterSettingsLayer BaseLayer
		{
			get
			{
				if (_baseLayer == null)
				{
					var settingsLayer = new PrinterSettingsLayer();

					foreach (var settingsData in SettingsData.Values)
					{
						settingsLayer[settingsData.SlicerConfigName] = settingsData.DefaultValue;
					}

					_baseLayer = settingsLayer;
				}

				return _baseLayer;
			}
		}

		// Properties
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


		[JsonIgnore]
		private RectangleDouble _meshAllowedBounds;
        
		/// <summary>
		/// The bounds that a mesh can be placed at and the gcode it creates will be within the bed
		/// </summary>
		[JsonIgnore]
		public RectangleDouble MeshAllowedBounds
		{
			get
			{
				if (_meshAllowedBounds.Width == 0)
				{
					CacluateMeshAllowedBounds();
				}

				return _meshAllowedBounds;
			}
		}

		private void CacluateMeshAllowedBounds()
		{
			var firstLayerExtrusionWidth = GetDouble(SettingsKey.first_layer_extrusion_width);
			var bedBounds = BedBounds;
			var totalOffset = 0.0;

			if (GetBool(SettingsKey.create_raft))
			{
				// The slicing engine creates a raft 3x the extrusion width
				firstLayerExtrusionWidth *= 3;
				totalOffset += firstLayerExtrusionWidth;
				totalOffset += GetDouble(SettingsKey.raft_extra_distance_around_part);
			}

			if (GetBool(SettingsKey.create_skirt))
			{
				totalOffset += GetValue<double>(SettingsKey.skirt_distance);
				totalOffset += (GetDouble(SettingsKey.skirts) + .5) * firstLayerExtrusionWidth;
				// for every 400mm of min skirt length add another skirt loops
				totalOffset += GetDouble(SettingsKey.min_skirt_length) / (20 * 20);
			}

			if (GetBool(SettingsKey.create_brim)
				&& !GetBool(SettingsKey.create_raft))
			{
				totalOffset += GetValue<double>(SettingsKey.brims) * GetDouble(SettingsKey.first_layer_extrusion_width);
			}

			bedBounds.Inflate(-totalOffset);

			_meshAllowedBounds = bedBounds;
		}

		[JsonIgnore]
		public IEnumerable<PrinterSettingsLayer> DefaultLayerCascade
		{
			get
			{
				if (this.UserLayer != null)
				{
					yield return this.UserLayer;
				}

				var sceneLayer = this.GetSceneLayer?.Invoke();
				if (sceneLayer != null)
				{
					yield return sceneLayer;
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

		public int DocumentVersion { get; set; } = LatestVersion;

		[JsonIgnore]
		public SettingsHelpers Helpers { get; set; }

		public string ID { get; set; }

		[JsonIgnore]
		public PrinterSettingsLayer MaterialLayer { get; set; }

		[JsonIgnore]
		public IEnumerable<PrinterSettingsLayer> MaterialLayerCascade
		{
			get
			{
				if (this.MaterialLayer != null)
				{
					yield return this.MaterialLayer;
				}

				if (this.OemLayer != null)
				{
					yield return this.OemLayer;
				}

				yield return this.BaseLayer;
			}
		}

		public List<PrinterSettingsLayer> MaterialLayers { get; set; } = new List<PrinterSettingsLayer>();

		public PrinterSettingsLayer OemLayer { get; set; } = new PrinterSettingsLayer();

		[JsonIgnore]
		public bool PrinterSelected => OemLayer?.Keys.Count > 0;

		[JsonIgnore]
		public PrinterSettingsLayer QualityLayer { get; set; }

		[JsonIgnore]
		public IEnumerable<PrinterSettingsLayer> QualityLayerCascade
		{
			get
			{
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
		public IEnumerable<PrinterSettingsLayer> SceneLayerCascade
		{
			get
			{
				if (this.SceneLayer != null)
				{
					yield return this.SceneLayer;
				}

				if (this.OemLayer != null)
				{
					yield return this.OemLayer;
				}

				yield return this.BaseLayer;
			}
		}

		public List<PrinterSettingsLayer> QualityLayers { get; private set; } = new List<PrinterSettingsLayer>();

		[JsonIgnore]
		public object Slicer
		{
			get
			{
				if (_slicer == null)
				{
					string userSlicer = this.GetValue(SettingsKey.slice_engine);

					if (SliceEngines.TryGetValue(userSlicer, out object slicer))
					{
						_slicer = slicer;
					}
					else
					{
						_slicer = SliceEngines.Values.First();
					}
				}

				return _slicer;
			}

			set => _slicer = value;
		}

		public PrinterSettingsLayer StagedUserSettings { get; set; } = new PrinterSettingsLayer();

		/// <summary>
		/// Gets the bounds that are accessible for a given hotend
		/// </summary>
		[JsonIgnore]
		public RectangleDouble[] ToolBounds { get; private set; }

		/// <summary>
		/// User settings overrides
		/// </summary>
		public PrinterSettingsLayer UserLayer { get; private set; } = new PrinterSettingsLayer();

		[JsonIgnore]
		public PrinterSettingsLayer SceneLayer
		{
			get
			{
				return GetSceneLayer?.Invoke();
			}
		}

		/// <summary>
		/// Scene settings override (this comes from a SliceSettingsObject3D being in the scene
		/// </summary>
		[JsonIgnore]
		public Func<PrinterSettingsLayer> GetSceneLayer;

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

				int documentVersion = jObject?.GetValue("DocumentVersion")?.Value<int>() ?? LatestVersion;
				if (documentVersion < LatestVersion)
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

		public void ClearUserOverrides()
		{
			var userOverrides = this.UserLayer.Keys.ToArray();

			// Leave user layer items that have no Organizer definition and thus cannot be changed by the user
			var keysToRetain = new HashSet<string>(userOverrides.Except(KnownSettings));

			// Print leveling data has no SliceSettingsWidget editor but should be removed on 'Reset to Defaults'
			keysToRetain.Remove(SettingsKey.print_leveling_data);
			keysToRetain.Remove(SettingsKey.print_leveling_enabled);

			// Iterate all items that have .ShowAsOverride = false and conditionally add to the retention list
			foreach (var item in SettingsData.Values.Where(settingsItem => settingsItem.ShowAsOverride == false))
			{
				switch (item.SlicerConfigName)
				{
					case SettingsKey.baud_rate:
					case SettingsKey.auto_connect:
						// Items *should* reset to defaults
						break;

					default:
						// Items should *not* reset to defaults
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

		public void ClearValue(string settingsKey, PrinterSettingsLayer layer = null)
		{
			var persistenceLayer = layer ?? UserLayer;
			if (persistenceLayer.ContainsKey(settingsKey))
			{
				persistenceLayer.Remove(settingsKey);

				// Restore user overrides if a non-user override is being cleared
				if (layer != null && layer != UserLayer)
				{
					RestoreUserOverride(settingsKey);
				}

				if (SettingsData.TryGetValue(settingsKey, out SliceSettingData settingData))
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

		public bool Contains(string sliceSetting, IEnumerable<PrinterSettingsLayer> layerCascade = null)
		{
			if (layerCascade == null)
			{
				layerCascade = DefaultLayerCascade;
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
				StashUserOverride(settingsKey);
			}
		}

		/// <summary>
		/// Call the given function for every active tool
		/// </summary>
		/// <typeparam name="T">The type for the given key</typeparam>
		/// <param name="key">The settings key to lookup per tool</param>
		/// <param name="applyToEach">The function to call per active tool</param>
		public void ForTools<T>(string key, Action<string, T, int> applyToEach)
		{
			string Tool(int toolIndex)
			{
				if (toolIndex == 0)
				{
					return key;
				}

				// If the setting has a described method for constructing the keys per tool, use it.
				var perToolName = SettingsData[key].PerToolName;
				if (perToolName != null)
				{
					return perToolName(toolIndex);
				}

				return $"{key}_{toolIndex}";
			}

			var extruderCount = this.GetValue<int>(SettingsKey.extruder_count);
			for (int i = 0; i < extruderCount; i++)
			{
				var toolKey = Tool(i);
				applyToEach(toolKey, this.GetValue<T>(toolKey), i);
			}
		}

		// Helper method to debug settings layers per setting
		public List<(string layerName, string currentValue)> GetLayerValues(string sliceSetting, IEnumerable<PrinterSettingsLayer> layerCascade = null)
		{
			if (layerCascade == null)
			{
				layerCascade = DefaultLayerCascade;
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

		/// <summary>
		/// Gets the first matching value discovered while enumerating the settings layers
		/// </summary>
		public string GetValue(string sliceSetting, IEnumerable<PrinterSettingsLayer> layerCascade = null)
		{
			if (layerCascade == null)
			{
				layerCascade = DefaultLayerCascade;
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

		public (string currentValue, string layerName) GetValueAndLayerName(string sliceSetting, IEnumerable<PrinterSettingsLayer> layerCascade = null)
		{
			if (layerCascade == null)
			{
				layerCascade = DefaultLayerCascade;
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
					else if (layer == this.SceneLayer)
                    {
						layerName = "Scene";
                    }

					return (value, layerName);
				}
			}

			return ("", "");
		}

		public bool IsActive(string canonicalSettingsName)
		{
			throw new NotImplementedException();
		}

		public bool IsOverride(string sliceSetting, IEnumerable<PrinterSettingsLayer> layers)
		{
			string GetDefaultValue()
			{
				foreach (var layer in layers)
				{
					if (layer == this.BaseLayer
						&& layer.ContainsKey(sliceSetting))
					{
						return layer[sliceSetting];
					}
					else if (layer == this.OemLayer
						&& layer.ContainsKey(sliceSetting))
					{
						return layer[sliceSetting];
					}
				}

				return null;
			}

			string GetOverrideValue()
			{
				foreach (var layer in layers)
				{
					if (layer == this.QualityLayer
						&& layer.ContainsKey(sliceSetting))
					{
						return layer[sliceSetting];
					}
					else if (layer == this.MaterialLayer
						&& layer.ContainsKey(sliceSetting))
					{
						return layer[sliceSetting];
					}
					else if (layer == this.SceneLayer
						&& layer.ContainsKey(sliceSetting))
					{
						return layer[sliceSetting];
					}
				}

				return null;
			}

			string GetUserValue()
			{
				foreach (var layer in layers)
				{
					if (layer == this.UserLayer
						&& layer.ContainsKey(sliceSetting))
					{
						return layer[sliceSetting];
					}
				}

				return null;
			}

			string defaultValue = GetDefaultValue();
			string overrideValue = GetOverrideValue();
			string userValue = GetUserValue();

			if (!string.IsNullOrEmpty(overrideValue))
			{
				// any override (even if the user overrides that) means we need to show the value
				return true;
			}
			else if (!string.IsNullOrEmpty(userValue)
				&& userValue != defaultValue)
			{
				return true;
			}

			return false;
		}

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

			foreach (var keyName in KnownSettings)
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

		public void NotifyMacrosChanged()
		{
			this.MacrosChanged?.Invoke(this, null);
		}

		public void OnSettingChanged(string slicerConfigName)
		{
			if (slicerConfigName == SettingsKey.t0_inset
				|| slicerConfigName == SettingsKey.t1_inset
				|| slicerConfigName == SettingsKey.bed_size
				|| slicerConfigName == SettingsKey.print_center)
			{
				this.ResetHotendBounds();
			}

			if (slicerConfigName == SettingsKey.first_layer_extrusion_width
				|| slicerConfigName == SettingsKey.create_raft
				|| slicerConfigName == SettingsKey.raft_extra_distance_around_part
				|| slicerConfigName == SettingsKey.create_skirt
				|| slicerConfigName == SettingsKey.skirt_distance
				|| slicerConfigName == SettingsKey.skirts
				|| slicerConfigName == SettingsKey.min_skirt_length
				|| slicerConfigName == SettingsKey.create_brim
				|| slicerConfigName == SettingsKey.print_center
				|| slicerConfigName == SettingsKey.bed_size
				|| slicerConfigName == SettingsKey.bed_shape)
			{
                // cleare this so it will be recaculated
				_meshAllowedBounds = new RectangleDouble();
			}

			SettingChanged?.Invoke(this, new StringEventArgs(slicerConfigName));
			AnyPrinterSettingChanged?.Invoke(this, new StringEventArgs(slicerConfigName));
		}

		private static readonly Regex ConstantFinder = new Regex("(?<=\\[).+?(?=\\])", RegexOptions.CultureInvariant | RegexOptions.Compiled);
		private static readonly Regex SettingsFinder = new Regex("(?<=\\[).+?(?=\\])|(?<=\\{).+?(?=\\})", RegexOptions.CultureInvariant | RegexOptions.Compiled);

		public string ReplaceSettingsNamesWithValues(string stringWithSettingsNames, bool includeCurlyBrackets = true)
		{
			string Replace(string inputString, string setting)
			{
				var value = this.ResolveValue(setting);

				if (setting == SettingsKey.bed_temperature)
                {
					value = this.Helpers.ActiveBedTemperature.ToString();
                }

				if(string.IsNullOrEmpty(value))
				{
					return inputString;
				}

				if (ScaledSpeedFields.Contains(setting)
					&& double.TryParse(value, out double doubleValue))
				{
					doubleValue *= 60;
					value = $"{doubleValue:0.###}";
				}

                // Use bed_temperature if the slice engine does not have first_layer_bed_temperature
                throw new NotImplementedException();


                // braces then brackets replacement
                inputString = inputString.Replace("{" + setting + "}", value);
				inputString = inputString.Replace("[" + setting + "]", value);
				return inputString;
			}

			MatchCollection matches;
			if (includeCurlyBrackets)
			{
				matches = SettingsFinder.Matches(stringWithSettingsNames);
			}
			else
			{
				matches = ConstantFinder.Matches(stringWithSettingsNames);
			}

			for (int i=0; i< matches.Count; i++)
			{
				var replacementTerm = matches[i].Value;
				stringWithSettingsNames = Replace(stringWithSettingsNames, replacementTerm);
			}

			return stringWithSettingsNames;
		}

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
				RestoreUserOverride(settingsKey);
			}
		}

		public void SetValue(string settingsKey, string settingsValue, PrinterSettingsLayer layer = null)
		{
			// Stash user overrides if a non-user override is being set
			if (layer != null && layer != UserLayer)
			{
				StashUserOverride(settingsKey);
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

			// delay to make sure all settings changes have completed
			UiThread.RunOnIdle(() => this.OnSettingChanged(settingsKey));
		}

		public string ToJson()
		{
			return JsonConvert.SerializeObject(this, Formatting.Indented);
		}

		internal PrinterSettingsLayer GetMaterialLayer(string layerID)
		{
			if (string.IsNullOrEmpty(layerID))
			{
				return null;
			}

			return MaterialLayers.Where(layer => layer.LayerID == layerID).FirstOrDefault();
		}

		internal void OnPrintLevelingEnabledChanged(object s, EventArgs e)
		{
			PrintLevelingEnabledChanged?.Invoke(s, e);
		}

		internal void RunInTransaction(Action<PrinterSettings> action)
		{
			// TODO: Implement RunInTransaction
			// Suspend writes
			action(this);
			// Commit
		}

		private static HashSet<string> LoadSettingsNamesFromPropertiesJson()
		{
			return new HashSet<string>(SettingsData.Keys);
		}

		private PrinterSettingsLayer GetQualityLayer(string layerID)
		{
			return QualityLayers.Where(layer => layer.LayerID == layerID).FirstOrDefault();
		}

		private void OnDeserialize()
		{
			this.ResetHotendBounds();
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

		private void RestoreUserOverride(string settingsKey)
		{
			if (StagedUserSettings.TryGetValue(settingsKey, out string stagedUserOverride))
			{
				StagedUserSettings.Remove(settingsKey);
				UserLayer[settingsKey] = stagedUserOverride;
			}
		}

		/// <summary>
		/// Move conflicting user overrides to the temporary staging area, allowing presets values to take effect
		/// </summary>
		private void StashUserOverride(string settingsKey)
		{
			if (this.UserLayer.TryGetValue(settingsKey, out string userOverride))
			{
				this.UserLayer.Remove(settingsKey);
				this.StagedUserSettings[settingsKey] = userOverride;
			}
		}

		#region Migrate to LayeredProfile

		private static Dictionary<string, Type> expectedMappingTypes = new Dictionary<string, Type>()
		{
			[SettingsKey.extruders_share_temperature] = typeof(int),
			[SettingsKey.extruders_share_temperature] = typeof(bool),
			[SettingsKey.has_heated_bed] = typeof(bool),
			[SettingsKey.nozzle_diameter] = typeof(double),
			[SettingsKey.bed_temperature] = typeof(double),
		};

		public ulong GetGCodeCacheKey()
		{
			var bigStringForHashCode = new StringBuilder();

			// Loop over all known settings
			foreach (var keyValue in SettingsData)
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

		public bool GetBool(string settingsKey)
		{
			return GetValue<bool>(settingsKey);
		}

		public int GetInt(string settingsKey)
		{
			return GetValue<int>(settingsKey);
		}

		public double GetDouble(string settingsKey)
		{
			return GetValue<double>(settingsKey);
		}

		/// <summary>
		/// Returns the first matching value discovered while enumerating the settings layers
		/// </summary>
		/// <typeparam name="T">The type to return</typeparam>
		/// <param name="settingsKey">The setting to look up</param>
		/// <param name="layerCascade">The settings layers to look at in order</param>
		/// <returns>The value of the setting cast to type</returns>
		public T GetValue<T>(string settingsKey, IEnumerable<PrinterSettingsLayer> layerCascade = null)
		{
#if DEBUG
			ValidateType<T>(settingsKey);
#endif

			if (layerCascade == null)
			{
				layerCascade = DefaultLayerCascade;
			}

			if (settingsKey == "external_perimeters_first")
			{
				var a = 0;
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
						return (T)(object)LevelingSystem.Probe3Points;

					case "7 Point Disk":
						return (T)(object)LevelingSystem.Probe7PointRadial;

					case "13 Point Disk":
						return (T)(object)LevelingSystem.Probe13PointRadial;

					case "100 Point Disk":
						return (T)(object)LevelingSystem.Probe100PointRadial;

					case "3x3 Mesh":
						return (T)(object)LevelingSystem.Probe3x3Mesh;

					case "5x5 Mesh":
						return (T)(object)LevelingSystem.Probe5x5Mesh;

					case "10x10 Mesh":
						return (T)(object)LevelingSystem.Probe10x10Mesh;

					case "Custom Points":
						return (T)(object)LevelingSystem.ProbeCustom;

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
				int.TryParse(this.ResolveValue(settingsKey), out int result);
				return (T)(object)result;
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
				double.TryParse(this.ResolveValue(settingsKey), out double doubleValue);
				return (T)(object)doubleValue;
			}
			else if (typeof(T) == typeof(BedShape))
			{
				switch (GetValue(settingsKey, layerCascade))
				{
					case "rectangular":
						return (T)(object)BedShape.Rectangular;

					case "circular":
						return (T)(object)BedShape.Circular;

					default:
#if DEBUG
						throw new NotImplementedException("{0} is not a known bed_shape.".FormatWith(GetValue(SettingsKey.bed_shape, layerCascade)));
#else
						return (T)(object)BedShape.Rectangular;
#endif
				}
			}

			return (T)default(T);
		}

		public string ResolveValue(string settingsKey)
		{
			string value = this.GetValue(settingsKey);

			if (SettingsData.TryGetValue(settingsKey, out SliceSettingData settingsData)
				&& settingsData.Converter != null)
			{
				return settingsData.Converter.Convert(value, this);
			}

			return value;
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

				case NamedSettingsLayers.Scene:
					return SceneLayer?.ContainsKey(sliceSetting) == true;

				case NamedSettingsLayers.User:
					return UserLayer?.ContainsKey(sliceSetting) == true;

				default:
					return false;
			}
		}

		private void ValidateType<T>(string settingsKey)
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
				if (typeof(T) != typeof(double)
					|| typeof(T) != typeof(int))
				{
					throw new Exception("To get processing of a % you must request the type as double.");
				}
			}
		}

		#endregion Migrate to LayeredProfile

		/// <summary>
		/// Provides a one-way import mechanism for ActiveMaterialKey from the retired MaterialSettingsKeys array
		/// </summary>
		private class PrinterSettingsConverter : JsonConverter
		{
			public override bool CanRead => true;

			public override bool CanWrite => false;

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
				if (settings.OemLayer?.ContainsKey("z_probe_z_offset") == true)
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

			public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
			{
				throw new NotImplementedException();
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
		}
	}
}