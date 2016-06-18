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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	using ConfigurationPage.PrintLeveling;
	using SettingsDictionary = Dictionary<string, string>;
	using DataStorage;
	using Agg.PlatformAbstract;
	using Newtonsoft.Json.Linq;
	using MeshVisualizer;

	public static class SettingsKey
	{
		public const string bed_shape = nameof(bed_shape);
		public const string bed_size = nameof(bed_size);
		public const string bed_temperature = nameof(bed_temperature);
		public const string has_heated_bed = nameof(has_heated_bed);
		public const string resume_position_before_z_home = nameof(resume_position_before_z_home);
		public const string z_homes_to_max = nameof(z_homes_to_max);
		public const string nozzle_diameter = nameof(nozzle_diameter);
		public const string printer_name = nameof(printer_name);
		public const string min_fan_speed = nameof(min_fan_speed);
		public const string extruder_count = nameof(extruder_count);
		public const string extruders_share_temperature = nameof(extruders_share_temperature);
		public const string fill_density = nameof(fill_density);
	};

	public class SettingsProfile
	{
		private static string configFileExtension = "slice";

		public RootedObjectEventHandler DoPrintLevelingChanged = new RootedObjectEventHandler();

		private PrinterSettings layeredProfile;

		public bool PrinterSelected => layeredProfile.OemProfile.OemLayer.Keys.Count > 0;

		internal SettingsProfile(PrinterSettings profile)
		{
			layeredProfile = profile;
		}

		#region LayeredProfile Proxies

		public string ID
		{
			get
			{
				return layeredProfile.ID;
			}
			set
			{
				layeredProfile.ID = value;
			}
		}


		public string ActiveQualityKey
		{
			get
			{
				return layeredProfile.ActiveQualityKey;
			}
			set
			{
				layeredProfile.ActiveQualityKey = value;
			}
		}

		public PrinterSettingsLayer BaseLayer => layeredProfile.BaseLayer;

		public PrinterSettingsLayer OemLayer => layeredProfile.OemProfile.OemLayer;

		public PrinterSettingsLayer UserLayer => layeredProfile.UserLayer;

		public List<PrinterSettingsLayer> MaterialLayers => layeredProfile.MaterialLayers;

		public List<PrinterSettingsLayer> QualityLayers => layeredProfile.QualityLayers;

		public List<GCodeMacro> Macros => layeredProfile.Macros;

		///<summary>
		///Returns the first matching value discovered while enumerating the settings layers
		///</summary>
		public string GetValue(string sliceSetting, IEnumerable<PrinterSettingsLayer> layerCascade = null)
		{
			return layeredProfile.GetValue(sliceSetting, layerCascade);
		}

		public void SetActiveValue(string sliceSetting, string sliceValue, PrinterSettingsLayer persistenceLayer = null)
		{
			layeredProfile.SetValue(sliceSetting, sliceValue, persistenceLayer);
		}

		public void ClearValue(string sliceSetting, PrinterSettingsLayer persistenceLayer = null)
		{
			layeredProfile.ClearValue(sliceSetting, persistenceLayer);
		}

		internal void SaveChanges()
		{
			layeredProfile.Save();
		}

		internal void SetMaterialPreset(int extruderIndex, string text)
		{
			layeredProfile.SetMaterialPreset(extruderIndex, text);
		}

		internal List<string> MaterialSettingsKeys()
		{
			return layeredProfile.MaterialSettingsKeys;
		}

		internal string MaterialPresetKey(int extruderIndex)
		{
			return layeredProfile.GetMaterialPresetKey(extruderIndex);
		}

		#endregion

		internal void RunInTransaction(Action<SettingsProfile> action)
		{
			// TODO: Implement RunInTransaction
			// Suspend writes
			action(this);
			// Commit
		}

		/* jlewin - delete after confirmation
		public class SettingsConverter
		{
			public static void LoadConfigurationSettingsFromFileAsUnsaved(string pathAndFileName)
			{
				try
				{
					if (File.Exists(pathAndFileName))
					{
						string[] lines = System.IO.File.ReadAllLines(pathAndFileName);
						foreach (string line in lines)
						{
							//Ignore commented lines
							if (line.Trim() != "" && !line.StartsWith("#"))
							{
								string[] settingLine = line.Split('=');
								if (settingLine.Length > 1)
								{
									string keyName = settingLine[0].Trim();
									string settingDefaultValue = settingLine[1].Trim();

									//Add the setting to the active layer
									//SaveValue(keyName, settingDefaultValue);
									throw new NotImplementedException("load to dictionary");
								}
							}
						}
					}
				}
				catch (Exception e)
				{
					Debug.Print(e.Message);
					GuiWidget.BreakInDebugger();
					Debug.WriteLine(string.Format("Error loading configuration: {0}", e));
				}
			}
		}*/

		public void ClearUserOverrides()
		{
			var userOverrides = this.UserLayer.Keys.ToArray();

			// Leave user layer items that have no Organizer definition and thus cannot be changed by the user
			var keysToRetain = new HashSet<string>(userOverrides.Except(this.KnownSettings));

			foreach (var item in SliceSettingsOrganizer.Instance.SettingsData.Where(settingsItem => !settingsItem.ShowAsOverride))
			{
				switch (item.SlicerConfigName)
				{
					case "baud_rate":
					case "auto_connect":
						// These items are marked as not being overrides but should be cleared on 'reset to defaults'
						break;
					default:
						// All other non-overrides should be retained
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

		public string ExtruderTemperature(int extruderIndex)
		{
			if (extruderIndex >= layeredProfile.MaterialSettingsKeys.Count)
			{
				// MaterialSettingsKeys is empty or lacks a value for the given extruder index
				//
				// If extruder index zero was requested, return the layer cascade temperature value, otherwise null
				return (extruderIndex == 0) ? layeredProfile.GetValue("temperature") : null;
			}

			string materialKey = layeredProfile.MaterialSettingsKeys[extruderIndex];

			if (extruderIndex == 0 && (string.IsNullOrEmpty(materialKey) || layeredProfile.UserLayer.ContainsKey("temperature")))
			{
				// In the case where a user override exists or MaterialSettingsKeys is populated with multiple extruder 
				// positions but position 0 is empty and thus unassigned, use layer cascade to resolve temp
				return layeredProfile.GetValue("temperature");
			}

			// Otherwise, use the SettingsLayers that is bound to this extruder
			PrinterSettingsLayer layer = layeredProfile.GetMaterialLayer(materialKey);

			string result = "0";
			layer?.TryGetValue("temperature", out result);
			return result;
		}

		public int[] LayerToPauseOn()
		{
			string[] userValues = GetValue("layer_to_pause").Split(';');

			int temp;
			return userValues.Where(v => int.TryParse(v, out temp)).Select(v =>
			{
				//Convert from 0 based index to 1 based index
				int val = int.Parse(v);

				// Special case for user entered zero that pushes 0 to 1, otherwise val = val - 1 for 1 based index
				return val == 0 ? 1 : val - 1;
			}).ToArray();
		}

		private static double ParseDouble(string firstLayerValueString)
		{
			double firstLayerValue;
			if (!double.TryParse(firstLayerValueString, out firstLayerValue))
			{
				throw new Exception(string.Format("Format cannot be parsed. FirstLayerHeight '{0}'", firstLayerValueString));
			}
			return firstLayerValue;
		}

		public Vector2 ExtruderOffset(int extruderIndex)
		{
			string currentOffsets = GetValue("extruder_offset");
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

		private PrintLevelingData printLevelingData = null;
		public PrintLevelingData GetPrintLevelingData()
		{
			if (printLevelingData == null)
			{
				printLevelingData = PrintLevelingData.Create(
					ActiveSliceSettings.Instance,
					layeredProfile.GetValue("print_leveling_data"),
					layeredProfile.GetValue("MatterControl.PrintLevelingProbePositions"));

				PrintLevelingPlane.Instance.SetPrintLevelingEquation(
					printLevelingData.SampledPosition0,
					printLevelingData.SampledPosition1,
					printLevelingData.SampledPosition2,
					ActiveSliceSettings.Instance.GetValue<Vector2>("print_center"));
			}

			return printLevelingData;
		}

		public void SetPrintLevelingData(PrintLevelingData data)
		{
			printLevelingData = data;
			layeredProfile.SetValue("print_leveling_data", JsonConvert.SerializeObject(data));

		}

		public void DoPrintLeveling(bool doLeveling)
		{
			// Early exit if already set
			if (doLeveling == this.GetValue<bool>("print_leveling_enabled"))
			{
				return;
			}

			layeredProfile.SetValue("print_leveling_enabled", doLeveling ? "1" : "0");

			DoPrintLevelingChanged.CallEvents(this, null);

			if (doLeveling)
			{
				PrintLevelingData levelingData = ActiveSliceSettings.Instance.GetPrintLevelingData();
				PrintLevelingPlane.Instance.SetPrintLevelingEquation(
					levelingData.SampledPosition0,
					levelingData.SampledPosition1,
					levelingData.SampledPosition2,
					ActiveSliceSettings.Instance.GetValue<Vector2>("print_center"));
			}
		}

		private static readonly SlicingEngineTypes defaultEngineType = SlicingEngineTypes.MatterSlice;

		public SlicingEngineTypes ActiveSliceEngineType()
		{
			string engineType = layeredProfile.GetValue("slicing_engine");
			if (string.IsNullOrEmpty(engineType))
			{
				return defaultEngineType;
			}

			var engine = (SlicingEngineTypes)Enum.Parse(typeof(SlicingEngineTypes), engineType);
			return engine;
		}

		public void ActiveSliceEngineType(SlicingEngineTypes type)
		{
			SetActiveValue("slicing_engine", type.ToString());
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

		#region Migrate to LayeredProfile 

		///<summary>
		///Returns the first matching value discovered while enumerating the settings layers
		///</summary>
		public T GetValue<T>(string settingsKey) where T : IConvertible
		{
			if (typeof(T) == typeof(bool))
			{
				return (T)(object)(this.GetValue(settingsKey) == "1");
			}
			else if (typeof(T) == typeof(int))
			{
				if (settingsKey == SettingsKey.extruder_count
					&& this.GetValue<bool>(SettingsKey.extruders_share_temperature))
				{
					return (T)(object)1;
				}

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
				valueAsVector2.x = ParseDouble(twoValues[0]);
				valueAsVector2.y = ParseDouble(twoValues[1]);
				return (T)(object)(valueAsVector2);
			}
			else if (typeof(T) == typeof(double))
			{
				string settingsStringh = GetValue(settingsKey);
				if (settingsStringh.Contains("%"))
				{
					string onlyNumber = settingsStringh.Replace("%", "");
					double ratio = ParseDouble(onlyNumber) / 100;

					if (settingsKey == "first_layer_height")
					{
						return (T)(object)(GetValue<double>("layer_height") * ratio);
					}
					else if (settingsKey == "first_layer_extrusion_width")
					{
						return (T)(object)(GetValue<double>("layer_height") * ratio);
					}

					return (T)(object)(ratio);
				}

				if (settingsKey == SettingsKey.bed_temperature
					&& !this.GetValue<bool>("has_heated_bed"))
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
						throw new NotImplementedException(string.Format("'{0}' is not a known bed_shape.", GetValue("bed_shape")));
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
			if (layeredProfile == null)
			{
				return false;
			}

			switch (layer)
			{
				case NamedSettingsLayers.Quality:
					return layeredProfile?.QualityLayer?.ContainsKey(sliceSetting) == true;
				case NamedSettingsLayers.Material:
					return layeredProfile?.MaterialLayer?.ContainsKey(sliceSetting) == true;
				case NamedSettingsLayers.User:
					return layeredProfile?.UserLayer?.ContainsKey(sliceSetting) == true;
				default:
					return false;
			}
		}

		public void ExportAsMatterControlConfig()
		{
			FileDialog.SaveFileDialog(
			new SaveFileDialogParams("MatterControl Printer Export|*.printer", title: "Export Printer Settings"),
			(saveParams) =>
			{
				File.WriteAllText(saveParams.FileName, JsonConvert.SerializeObject(layeredProfile, Formatting.Indented));
			});
		}

		public void ExportAsSlic3rConfig()
		{
			FileDialog.SaveFileDialog(
				new SaveFileDialogParams("Save Slice Configuration".Localize() + "|*." + configFileExtension)
				{
					FileName = "default_settings.ini"
				},
				(saveParams) =>
				{
					if (!string.IsNullOrEmpty(saveParams.FileName))
					{
						GenerateConfigFile(saveParams.FileName, false);
					}
				});
		}

		public void ExportAsCuraConfig()
		{
			throw new NotImplementedException();
		}

		public long GetLongHashCode()
		{
			var bigStringForHashCode = new StringBuilder();

			foreach (var keyValue in this.BaseLayer)
			{
				string activeValue = GetValue(keyValue.Key);
				bigStringForHashCode.Append(keyValue.Key);
				bigStringForHashCode.Append(activeValue);
			}

			string value = bigStringForHashCode.ToString();

			return agg_basics.ComputeHash(bigStringForHashCode.ToString());
		}

		public void GenerateConfigFile(string fileName, bool replaceMacroValues)
		{
			using (var outstream = new StreamWriter(fileName))
			{
				foreach (var key in this.KnownSettings.Where(k => !k.StartsWith("MatterControl.")))
				{
					string activeValue = GetValue(key);
					if (replaceMacroValues)
					{
						activeValue = GCodeProcessing.ReplaceMacroValues(activeValue);
					}
					outstream.Write(string.Format("{0} = {1}\n", key, activeValue));
					activeValue = GCodeProcessing.ReplaceMacroValues(activeValue);
				}
			}
		}

		public bool IsValid()
		{
			try
			{
				if (GetValue<double>("layer_height") > GetValue<double>(SettingsKey.nozzle_diameter))
				{
					string error = "'Layer Height' must be less than or equal to the 'Nozzle Diameter'.".Localize();
					string details = string.Format("Layer Height = {0}\nNozzle Diameter = {1}".Localize(), GetValue<double>("layer_height"), GetValue<double>(SettingsKey.nozzle_diameter));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'General' -> 'Layers/Surface'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}
				else if (GetValue<double>("first_layer_height") > GetValue<double>(SettingsKey.nozzle_diameter))
				{
					string error = "'First Layer Height' must be less than or equal to the 'Nozzle Diameter'.".Localize();
					string details = string.Format("First Layer Height = {0}\nNozzle Diameter = {1}".Localize(), GetValue<double>("first_layer_height"), GetValue<double>(SettingsKey.nozzle_diameter));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'General' -> 'Layers/Surface'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				// If we have print leveling turned on then make sure we don't have any leveling commands in the start gcode.
				if (PrinterConnectionAndCommunication.Instance.ActivePrinter.GetValue<bool>("print_leveling_enabled"))
				{
					string[] startGCode = GetValue("start_gcode").Replace("\\n", "\n").Split('\n');
					foreach (string startGCodeLine in startGCode)
					{
						if (startGCodeLine.StartsWith("G29"))
						{
							string error = "Start G-Code cannot contain G29 if Print Leveling is enabled.".Localize();
							string details = "Your Start G-Code should not contain a G29 if you are planning on using print leveling. Change your start G-Code or turn off print leveling".Localize();
							string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Printer' -> 'Custom G-Code' -> 'Start G-Code'".Localize();
							StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
							return false;
						}

						if (startGCodeLine.StartsWith("G30"))
						{
							string error = "Start G-Code cannot contain G30 if Print Leveling is enabled.".Localize();
							string details = "Your Start G-Code should not contain a G30 if you are planning on using print leveling. Change your start G-Code or turn off print leveling".Localize();
							string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Printer' -> 'Custom G-Code' -> 'Start G-Code'".Localize();
							StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
							return false;
						}
					}
				}

				if (GetValue<double>("first_layer_extrusion_width") > GetValue<double>(SettingsKey.nozzle_diameter) * 4)
				{
					string error = "'First Layer Extrusion Width' must be less than or equal to the 'Nozzle Diameter' * 4.".Localize();
					string details = string.Format("First Layer Extrusion Width = {0}\nNozzle Diameter = {1}".Localize(), GetValue("first_layer_extrusion_width"), GetValue<double>(SettingsKey.nozzle_diameter));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Extrusion' -> 'First Layer'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (GetValue<double>("first_layer_extrusion_width") <= 0)
				{
					string error = "'First Layer Extrusion Width' must be greater than 0.".Localize();
					string details = string.Format("First Layer Extrusion Width = {0}".Localize(), GetValue("first_layer_extrusion_width"));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Extrusion' -> 'First Layer'".Localize();
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
				if (!ValidateGoodSpeedSettingGreaterThan0("first_layer_speed", normalSpeedLocation)) return false;
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
				|| (ActiveSliceSettings.Instance.ActiveSliceEngine().MapContains(speedSetting)
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

		public Vector3 ManualMovementSpeeds()
		{
			Vector3 feedRate = new Vector3(3000, 3000, 315);

			string savedSettings = ActiveSliceSettings.Instance.GetValue("manual_movement_speeds");
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
				string savedSettings = GetValue("manual_movement_speeds");
				if (!string.IsNullOrEmpty(savedSettings))
				{
					presets = savedSettings;
				}
			}

			return presets;
		}

		#endregion

		public void SetAutoConnect(bool autoConnectPrinter)
		{
			layeredProfile.SetValue("auto_connect", autoConnectPrinter ? "1" : "0");
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
			UserSettings.Instance.set("ActiveProfileID", "");

			UiThread.RunOnIdle(() => ActiveSliceSettings.Instance = ProfileManager.LoadEmptyProfile());
		}

		public void SetBaudRate(string baudRate)
		{
			layeredProfile.SetValue("baud_rate", baudRate);
		}

		public string ComPort()
		{
			return layeredProfile.GetValue($"{Environment.MachineName}_com_port");
		}

		public void SetComPort(string port)
		{
			layeredProfile.SetValue($"{Environment.MachineName}_com_port", port);
		}

		public void SetComPort(string port, PrinterSettingsLayer layer)
		{
			layeredProfile.SetValue($"{Environment.MachineName}_com_port", port, layer);
		}

		public void SetSlicingEngine(string engine)
		{
			layeredProfile.SetValue("slicing_engine", engine);
		}

		public void SetDriverType(string driver)
		{
			layeredProfile.SetValue("driver_type", driver);
		}

		public void SetDeviceToken(string token)
		{
			if (layeredProfile.GetValue("device_token") != token)
			{
				layeredProfile.SetValue("device_token", token);
			}
		}

		public void SetName(string name)
		{
			layeredProfile.SetValue(SettingsKey.printer_name, name);
		}

		HashSet<string> knownSettings = null;

		[JsonIgnore]
		public HashSet<string> KnownSettings
		{
			get
			{
				if (knownSettings == null)
				{
					string propertiesJson = StaticData.Instance.ReadAllText(Path.Combine("SliceSettings", "Properties.json"));
					var settingsData = JArray.Parse(propertiesJson);

					knownSettings = new HashSet<string>(settingsData.Select(s => s["SlicerConfigName"].Value<string>()));
				}

				return knownSettings;
			}
		}

		public void SetManualMovementSpeeds(string speed)
		{
			layeredProfile.SetValue("manual_movement_speeds", speed);
		}

	}

	public class PrinterSettingsLayer : SettingsDictionary
	{
		public PrinterSettingsLayer() { }

		public PrinterSettingsLayer(Dictionary<string, string> settingsDictionary)
		{
			foreach(var keyValue in settingsDictionary)
			{
				this[keyValue.Key] = keyValue.Value;
			}
		}

		public string LayerID
		{
			get
			{
				string layerKey = ValueOrDefault("layer_id");
				if (string.IsNullOrEmpty(layerKey))
				{
					// Generate a new GUID when missing or empty. We can't do this in the constructor as the dictionary deserialization will fail if
					// an existing key exists for layer_id and on new empty layers, we still need to construct an initial identifier. 
					layerKey = Guid.NewGuid().ToString();
					LayerID = layerKey;
				}

				return layerKey;
			}
			set
			{
				this["layer_id"] = value;
			}
		}

		public string Name
		{
			get
			{
				return ValueOrDefault("layer_name");
			}
			set
			{
				this["layer_name"] = value;
			}
		}

		public string Source
		{
			get
			{
				return ValueOrDefault("layer_source");
			}
			set
			{
				this["layer_source"] = value;
			}
		}

		public string ETag
		{
			get
			{
				return ValueOrDefault("layer_etag");
			}
			set
			{
				this["layer_etag"] = value;
			}
		}

		public string ValueOrDefault(string key, string defaultValue = "")
		{
			string foundValue;
			this.TryGetValue(key, out foundValue);

			return foundValue ?? defaultValue;
		}

		public string ValueOrNull(string key)
		{
			string foundValue;
			this.TryGetValue(key, out foundValue);

			return foundValue;
		}

		public static PrinterSettingsLayer LoadFromIni(TextReader reader)
		{
			var layer = new PrinterSettingsLayer();

			string line;
			while ((line = reader.ReadLine()) != null)
			{
				var segments = line.Split('=');
				if (!line.StartsWith("#") && !string.IsNullOrEmpty(line))
				{
					string key = segments[0].Trim();
					layer[key] = segments[1].Trim();
				}
			}

			return layer;
		}

		public static PrinterSettingsLayer LoadFromIni(string filePath)
		{
			var settings = from line in File.ReadAllLines(filePath)
						   let segments = line.Split('=')
						   where !line.StartsWith("#") && !string.IsNullOrEmpty(line) && segments.Length == 2
						   select new
						   {
							   Key = segments[0].Trim(),
							   Value = segments[1].Trim()
						   };

			var layer = new PrinterSettingsLayer();
			foreach (var setting in settings)
			{
				layer[setting.Key] = setting.Value;
			}

			return layer;
		}

		public PrinterSettingsLayer Clone()
		{
			string id = Guid.NewGuid().ToString();
			return new PrinterSettingsLayer(this as Dictionary<string, string>)
			{
				LayerID = id,
				Name = this.Name,
				ETag = this.ETag,
				Source = this.Source
			};
		}
	}

	public class PrinterInfo
	{
		public string ComPort { get; set; }
		public string ID { get; set; }
		public string Name { get; set; }
		public bool MarkedForDelete { get; set; }
		public string SHA1 { get; internal set; }
	}
}