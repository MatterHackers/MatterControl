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
using System.IO;
using System.Linq;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using MatterHackers.Agg.PlatformAbstract;
using System.Runtime.Serialization;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public static class SettingsKey
	{
		public const string active_theme_name = nameof(active_theme_name);
		public const string auto_connect = nameof(auto_connect);
		public const string baby_step_z_offset = nameof(baby_step_z_offset);
		public const string baud_rate = nameof(baud_rate);
		public const string bed_remove_part_temperature = nameof(bed_remove_part_temperature);
		public const string bed_shape = nameof(bed_shape);
		public const string bed_size = nameof(bed_size);
		public const string bed_temperature = nameof(bed_temperature);
		public const string bottom_clip_amount = nameof(bottom_clip_amount);
		public const string build_height = nameof(build_height);
		public const string cancel_gcode = nameof(cancel_gcode);
		public const string center_part_on_bed = nameof(center_part_on_bed);
		public const string com_port = nameof(com_port);
		public const string connect_gcode = nameof(connect_gcode);
		public const string created_date = nameof(created_date);
		public const string default_material_presets = nameof(default_material_presets);
		public const string device_token = nameof(device_token);
		public const string device_type = nameof(device_type);
		public const string expand_thin_walls = nameof(expand_thin_walls);
		public const string merge_overlapping_lines = nameof(merge_overlapping_lines);
		public const string extruder_count = nameof(extruder_count);
		public const string extruders_share_temperature = nameof(extruders_share_temperature);
		public const string external_perimeter_extrusion_width = nameof(external_perimeter_extrusion_width);
		public const string filament_cost = nameof(filament_cost);
		public const string filament_density = nameof(filament_density);
		public const string filament_diameter = nameof(filament_diameter);
		public const string fill_density = nameof(fill_density);
		public const string fill_thin_gaps = nameof(fill_thin_gaps);
		public const string first_layer_extrusion_width = nameof(first_layer_extrusion_width);
		public const string first_layer_height = nameof(first_layer_height);
		public const string g0 = nameof(g0);
		public const string has_fan = nameof(has_fan);
		public const string has_hardware_leveling = nameof(has_hardware_leveling);
		public const string has_heated_bed = nameof(has_heated_bed);
		public const string has_power_control = nameof(has_power_control);
		public const string has_sd_card_reader = nameof(has_sd_card_reader);
		public const string heat_extruder_before_homing = nameof(heat_extruder_before_homing);
		public const string layer_gcode = nameof(layer_gcode);
		public const string layer_height = nameof(layer_height);
		public const string layer_name = nameof(layer_name);
		public const string layer_to_pause = nameof(layer_to_pause);
		public const string make = nameof(make);
		public const string min_fan_speed = nameof(min_fan_speed);
		public const string model = nameof(model);
		public const string nozzle_diameter = nameof(nozzle_diameter);
		public const string oem_profile_token = nameof(oem_profile_token);
		public const string pause_gcode = nameof(pause_gcode);
		public const string perimeter_start_end_overlap = nameof(perimeter_start_end_overlap);
		public const string print_center = nameof(print_center);
		public const string print_leveling_data = nameof(print_leveling_data);
		public const string print_leveling_enabled = nameof(print_leveling_enabled);
		public const string print_leveling_probe_start = nameof(print_leveling_probe_start);
		public const string print_leveling_required_to_print = nameof(print_leveling_required_to_print);
		public const string printer_name = nameof(printer_name);
		public const string publish_bed_image = nameof(publish_bed_image);
		public const string recover_first_layer_speed = nameof(recover_first_layer_speed);
		public const string recover_is_enabled = nameof(recover_is_enabled);
		public const string recover_position_before_z_home = nameof(recover_position_before_z_home);
		public const string resume_gcode = nameof(resume_gcode);
		public const string show_reset_connection = nameof(show_reset_connection);
		public const string spiral_vase = nameof(spiral_vase);
		public const string start_gcode = nameof(start_gcode);
		public const string temperature = nameof(temperature);
		public const string windows_driver = nameof(windows_driver);
		public const string z_can_be_negative = nameof(z_can_be_negative);
		public const string z_homes_to_max = nameof(z_homes_to_max);
		public const string enable_sailfish_communication = nameof(enable_sailfish_communication);
		public const string enable_network_printing = nameof(enable_network_printing);
		public const string ip_address = nameof(ip_address);
		public const string ip_port = nameof(ip_port);
		public const string first_layer_speed = nameof(first_layer_speed);
		public const string active_quality_key = nameof(active_quality_key);
		public const string extrusion_ratio = nameof(extrusion_ratio);
		public const string feedrate_ratio = nameof(feedrate_ratio);
		public const string manual_movement_speeds = nameof(manual_movement_speeds);
	}

	public class SettingsHelpers
	{
		private PrinterSettings printerSettings;

		public SettingsHelpers(PrinterSettings printerSettings)
		{
			this.printerSettings = printerSettings;
		}

		public double ExtruderTemperature(int extruderIndex)
		{
			if (extruderIndex == 0)
			{
				return printerSettings.GetValue<double>(SettingsKey.temperature);
			}
			else
			{
				// Check if there is a material override for this extruder
				// Otherwise, use the SettingsLayers that is bound to this extruder
				if (extruderIndex < printerSettings.MaterialSettingsKeys.Count)
				{
					string materialKey = printerSettings.MaterialSettingsKeys[extruderIndex];
					PrinterSettingsLayer layer = printerSettings.GetMaterialLayer(materialKey);

					if (layer != null)
					{
						string result = "0";
						if (layer.TryGetValue(SettingsKey.temperature, out result))
						{
							double value = 0;
							if (double.TryParse(result, out value))
							{
								return value;
							}
						}
					}
				}

				// else return the normal settings cascade
				return printerSettings.GetValue<double>(SettingsKey.temperature);
			}
		}

		public int[] LayerToPauseOn()
		{
			string[] userValues = printerSettings.GetValue("layer_to_pause").Split(';');

			int temp;
			return userValues.Where(v => int.TryParse(v, out temp)).Select(v =>
			{
				//Convert from 0 based index to 1 based index
				int val = int.Parse(v);

				// Special case for user entered zero that pushes 0 to 1, otherwise val = val - 1 for 1 based index
				return val == 0 ? 1 : val - 1;
			}).ToArray();
		}

		internal double ParseDouble(string firstLayerValueString)
		{
			double firstLayerValue;
			if (!double.TryParse(firstLayerValueString, out firstLayerValue))
			{
				throw new Exception(string.Format("Format cannot be parsed. FirstLayerHeight '{0}'", firstLayerValueString));
			}
			return firstLayerValue;
		}

		public void SetMarkedForDelete(bool markedForDelete)
		{
			var printerInfo = ProfileManager.Instance.ActiveProfile;
			if (printerInfo != null)
			{
				printerInfo.MarkedForDelete = markedForDelete;
				ProfileManager.Instance.Save();
			}

			// Clear selected printer state
			ProfileManager.Instance.LastProfileID = "";

			UiThread.RunOnIdle(() =>
			{
				ActiveSliceSettings.Instance = PrinterSettings.Empty;

				// Notify listeners of a ProfileListChange event due to this printers removal
				ProfileManager.ProfilesListChanged.CallEvents(this, null);

				// Force sync after marking for delete
				ApplicationController.SyncPrinterProfiles("SettingsHelpers.SetMarkedForDelete()", null);
			});
		}

		public void SetBaudRate(string baudRate)
		{
			printerSettings.SetValue(SettingsKey.baud_rate, baudRate);
		}

		public string ComPort()
		{
			return printerSettings.GetValue($"{Environment.MachineName}_com_port");
		}

		public void SetComPort(string port)
		{
			printerSettings.SetValue($"{Environment.MachineName}_com_port", port);
		}

		public void SetComPort(string port, PrinterSettingsLayer layer)
		{
			printerSettings.SetValue($"{Environment.MachineName}_com_port", port, layer);
		}

		public void SetSlicingEngine(string engine)
		{
			printerSettings.SetValue("slicing_engine", engine);
		}

		public void SetDriverType(string driver)
		{
			printerSettings.SetValue("driver_type", driver);
		}

		public void SetDeviceToken(string token)
		{
			if (printerSettings.GetValue(SettingsKey.device_token) != token)
			{
				printerSettings.SetValue(SettingsKey.device_token, token);
			}
		}

		public void SetName(string name)
		{
			printerSettings.SetValue(SettingsKey.printer_name, name);
		}

		private PrintLevelingData printLevelingData = null;
		public PrintLevelingData GetPrintLevelingData()
		{
			if (printLevelingData == null)
			{
				printLevelingData = PrintLevelingData.Create(
					ActiveSliceSettings.Instance,
					printerSettings.GetValue(SettingsKey.print_leveling_data),
					printerSettings.GetValue("MatterControl.PrintLevelingProbePositions"));

				if (printLevelingData.SampledPositions.Count == 3)
				{
					PrintLevelingPlane.Instance.SetPrintLevelingEquation(
						printLevelingData.SampledPositions[0],
						printLevelingData.SampledPositions[1],
						printLevelingData.SampledPositions[2],
						ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center));
				}
			}

			return printLevelingData;
		}

		public void SetPrintLevelingData(PrintLevelingData data, bool clearUserZOffset)
		{
			if (clearUserZOffset)
			{
				ActiveSliceSettings.Instance.SetValue(SettingsKey.baby_step_z_offset, "0");
			}
			printLevelingData = data;
			printerSettings.SetValue(SettingsKey.print_leveling_data, JsonConvert.SerializeObject(data));
		}

		public void DoPrintLeveling(bool doLeveling)
		{
			// Early exit if already set
			if (doLeveling == printerSettings.GetValue<bool>(SettingsKey.print_leveling_enabled))
			{
				return;
			}

			printerSettings.SetValue(SettingsKey.print_leveling_enabled, doLeveling ? "1" : "0");

			if (doLeveling)
			{
				UpdateLevelSettings();
			}

			PrinterSettings.PrintLevelingEnabledChanged?.CallEvents(this, null);
		}

		public void UpdateLevelSettings()
		{
			PrintLevelingData levelingData = ActiveSliceSettings.Instance.Helpers.GetPrintLevelingData();
			if (levelingData.SampledPositions.Count > 2)
			{
				PrintLevelingPlane.Instance.SetPrintLevelingEquation(
					levelingData.SampledPositions[0],
					levelingData.SampledPositions[1],
					levelingData.SampledPositions[2],
					ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center));
			}
		}

		public Vector2 ExtruderOffset(int extruderIndex)
		{
			string currentOffsets = printerSettings.GetValue("extruder_offset");
			string[] offsets = currentOffsets.Split(',');
			int count = 0;
			foreach (string offset in offsets)
			{
				if (count == extruderIndex)
				{
					string[] xy = offset.Split('x');
					return new Vector2(double.Parse(xy[0]), double.Parse(xy[1]));
				}
				count++;
			}

			return Vector2.Zero;
		}

		public SlicingEngineTypes ActiveSliceEngineType()
		{
			if (OsInformation.OperatingSystem == OSType.Android
				|| SlicingQueue.AvailableSliceEngines.Count == 1)
			{
				// android only has MatterSlice available, so always return it.
				return SlicingEngineTypes.MatterSlice;
			}

			string engineType = printerSettings.GetValue("slicing_engine");
			if (string.IsNullOrEmpty(engineType))
			{
				return SlicingEngineTypes.MatterSlice;
			}

			var engine = (SlicingEngineTypes)Enum.Parse(typeof(SlicingEngineTypes), engineType);
			return engine;
		}

		public void ActiveSliceEngineType(SlicingEngineTypes type)
		{
			printerSettings.SetValue("slicing_engine", type.ToString());
		}

		public SliceEngineMapping ActiveSliceEngine()
		{
			switch (ActiveSliceEngineType())
			{
				case SlicingEngineTypes.CuraEngine:
					return EngineMappingCura.Instance;

				case SlicingEngineTypes.MatterSlice:
					return EngineMappingsMatterSlice.Instance;

				case SlicingEngineTypes.Slic3r:
					return Slic3rEngineMappings.Instance;

				default:
					return null;
			}
		}

		public void ExportAsMatterControlConfig()
		{
			FileDialog.SaveFileDialog(
			new SaveFileDialogParams("MatterControl Printer Export|*.printer", title: "Export Printer Settings")
			{
				FileName = printerSettings.GetValue(SettingsKey.printer_name)
			},
			(saveParams) =>
			{
				try
				{
					if (!string.IsNullOrWhiteSpace(saveParams.FileName))
					{
						File.WriteAllText(saveParams.FileName, JsonConvert.SerializeObject(printerSettings, Formatting.Indented));
					}
				}
				catch (Exception e)
				{
					UiThread.RunOnIdle (() => {
						StyledMessageBox.ShowMessageBox(null, e.Message, "Couldn't save file".Localize());
					});
				}
			});
		}

		public void ExportAsSlic3rConfig()
		{
			FileDialog.SaveFileDialog(
				new SaveFileDialogParams("Save Slice Configuration".Localize() + "|*.ini")
				{
					FileName = printerSettings.GetValue(SettingsKey.printer_name)
				},
				(saveParams) =>
				{
					try
					{
						if (!string.IsNullOrWhiteSpace(saveParams.FileName))
						{
							Slic3rEngineMappings.WriteSliceSettingsFile(saveParams.FileName);
						}
					}
					catch (Exception e)
					{
						UiThread.RunOnIdle (() => {
							StyledMessageBox.ShowMessageBox(null, e.Message, "Couldn't save file".Localize());
						});
					}
				});
		}

		public void ExportAsCuraConfig()
		{
			throw new NotImplementedException();
		}

		public Vector3 ManualMovementSpeeds()
		{
			Vector3 feedRate = new Vector3(3000, 3000, 315);

			string savedSettings = ActiveSliceSettings.Instance.GetValue(SettingsKey.manual_movement_speeds);
			if (!string.IsNullOrEmpty(savedSettings))
			{
				var segments = savedSettings.Split(',');
				feedRate.x = double.Parse(segments[1]);
				feedRate.y = double.Parse(segments[3]);
				feedRate.z = double.Parse(segments[5]);
			}

			return feedRate;
		}

		public Dictionary<string, double> GetMovementSpeeds()
		{
			Dictionary<string, double> speeds = new Dictionary<string, double>();
			string movementSpeedsString = GetMovementSpeedsString();
			string[] allSpeeds = movementSpeedsString.Split(',');
			for (int i = 0; i < allSpeeds.Length / 2; i++)
			{
				speeds.Add(allSpeeds[i * 2 + 0], double.Parse(allSpeeds[i * 2 + 1]));
			}

			return speeds;
		}

		public string GetMovementSpeedsString()
		{
			string presets = "x,3000,y,3000,z,315,e0,150"; // stored x,value,y,value,z,value,e1,value,e2,value,e3,value,...
			if (PrinterConnectionAndCommunication.Instance != null)
			{
				string savedSettings = printerSettings.GetValue(SettingsKey.manual_movement_speeds);
				if (!string.IsNullOrEmpty(savedSettings))
				{
					presets = savedSettings;
				}
			}

			return presets;
		}

		public int NumberOfHotEnds()
		{
			if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.extruders_share_temperature))
			{
				return 1;
			}

			return ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count);
		}
	}

	public class PrinterInfo
	{
		public string ComPort { get; set; }

		[JsonProperty(PropertyName = "ID")]
		private string id;

		[JsonIgnore]
		public string ID
		{
			get { return id; }
			set
			{
				// Update in memory state if IDs match
				if (ActiveSliceSettings.Instance.ID == this.ID)
				{
					ActiveSliceSettings.Instance.ID = value;
				}

				// Ensure the local file with the old ID moves with the new ID change
				string existingProfilePath = ProfilePath;
				if (File.Exists(existingProfilePath))
				{
					// Profile ID change must come after existingProfilePath calculation and before ProfilePath getter
					this.id = value;
					File.Move(existingProfilePath, ProfilePath);
				}
				else
				{
					this.id = value;
				}

				// If the local file exists and the PrinterInfo has been added to ProfileManager, then it's safe to call profile.Save, otherwise...
				if (File.Exists(ProfilePath) && ProfileManager.Instance[this.id] != null)
				{
					var profile = PrinterSettings.LoadFile(ProfilePath);
					profile.ID = value;
					profile.Save();
				}
			}
		}

		public string Name { get; set; }
		public string Make { get; set; }
		public string Model { get; set; }
		public string DeviceToken { get; set; }
		public bool IsDirty => this.ServerSHA1 != this.ContentSHA1;
		public bool MarkedForDelete { get; set; } = false;
		public string ContentSHA1 { get; set; }
		public string ServerSHA1 { get; set; }

		[OnDeserialized]
		public void OnDeserialized(StreamingContext context)
		{
			if (string.IsNullOrEmpty(this.Make))
			{
				this.Make = "Other";
			}

			if (string.IsNullOrEmpty(this.Model))
			{
				this.Model = "Other";
			}
		}

		[JsonIgnore]
		public string ProfilePath => ProfileManager.Instance.ProfilePath(this);
	}
}