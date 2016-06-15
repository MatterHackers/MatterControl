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

		public PrinterSettingsLayer BaseLayer => layeredProfile.BaseLayer;

		public PrinterSettingsLayer OemLayer => layeredProfile.OemProfile.OemLayer;

		public PrinterSettingsLayer UserLayer => layeredProfile.UserLayer;

		public string ActiveMaterialKey
		{
			get
			{
				return layeredProfile.ActiveMaterialKey;
			}
			set
			{
				layeredProfile.ActiveMaterialKey = value;
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

		internal void RunInTransaction(Action<SettingsProfile> action)
		{
			// TODO: Implement RunInTransaction
			// Suspend writes
			action(this);
			// Commit
		}

		public List<PrinterSettingsLayer> MaterialLayers => layeredProfile.MaterialLayers;

		public List<PrinterSettingsLayer> QualityLayers => layeredProfile.QualityLayers;

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
		}

		public void ClearUserOverrides()
		{
			var userOverrides = this.UserLayer.Keys.ToArray();

			// Leave user layer items that have no Organizer definition and thus cannot be changed by the user
			var keysToRetain = new HashSet<string>(userOverrides.Except(this.KnownSettings));

			foreach (var item in SliceSettingsOrganizer.Instance.SettingsData.Where(settingsItem => !settingsItem.ShowAsOverride))
			{
				switch (item.SlicerConfigName)
				{
					case "MatterControl.BaudRate":
					case "MatterControl.AutoConnect":
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

		internal void SaveChanges()
		{
			layeredProfile.Save();
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

		internal PrinterSettingsLayer MaterialLayer(string key)
		{
			return layeredProfile.GetMaterialLayer(key);
		}

		internal PrinterSettingsLayer QualityLayer(string key)
		{
			return layeredProfile.GetQualityLayer(key);
		}

		internal void SetMaterialPreset(int extruderIndex, string text)
		{
			layeredProfile.SetMaterialPreset(extruderIndex, text);
		}

		public double BedTemperature()
		{
			double targetTemp = 0;
			if (this.GetValue<bool>("has_heated_bed"))
			{
				double.TryParse(ActiveValue("bed_temperature"), out targetTemp);
			}
			return targetTemp;
		}

		public bool ShowFirmwareUpdater()
		{
			return ActiveValue("include_firmware_updater") == "Simple Arduino";
		}

		public int SupportExtruder()
		{
			return int.Parse(ActiveValue("support_material_extruder"));
		}

		public int[] LayerToPauseOn()
		{
			string[] userValues = ActiveValue("layer_to_pause").Split(';');

			int temp;
			return userValues.Where(v => int.TryParse(v, out temp)).Select(v =>
			{
					//Convert from 0 based index to 1 based index
					int val = int.Parse(v);

					// Special case for user entered zero that pushes 0 to 1, otherwise val = val - 1 for 1 based index
					return val == 0 ? 1 : val - 1;
			}).ToArray();
		}

		public double ProbePaperWidth()
		{
			return double.Parse(ActiveValue("manual_probe_paper_width"));
		}

		public int RaftExtruder()
		{
			return int.Parse(ActiveValue("raft_extruder"));
		}

		public double MaxFanSpeed()
		{
			return ParseDouble(ActiveValue("max_fan_speed"));
		}

		public double FillDensity()
		{
			string fillDensityValueString = ActiveValue("fill_density");
			if (fillDensityValueString.Contains("%"))
			{
				string onlyNumber = fillDensityValueString.Replace("%", "");
				double ratio = ParseDouble(onlyNumber) / 100;
				return ratio;
			}
			else
			{
				return ParseDouble(ActiveValue("fill_density"));
			}
		}

		public double MinFanSpeed()
		{
			return ParseDouble(ActiveValue("min_fan_speed"));
		}

		internal string MaterialPresetKey(int extruderIndex)
		{
			return layeredProfile.GetMaterialPresetKey(extruderIndex);
		}

		public double FirstLayerExtrusionWidth()
		{
				AsPercentOfReferenceOrDirect mapper = new AsPercentOfReferenceOrDirect("first_layer_extrusion_width", "notused", "nozzle_diameter");

				double firstLayerValue = ParseDouble(mapper.Value);
				return firstLayerValue;
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

		public double LayerHeight()
		{
			return ParseDouble(ActiveValue("layer_height"));
		}

		public Vector2 BedSize()
		{
			return ActiveVector2("bed_size");
		}

		public MeshVisualizer.MeshViewerWidget.BedShape BedShape()
		{
			switch (ActiveValue("bed_shape"))
			{
				case "rectangular":
					return MeshVisualizer.MeshViewerWidget.BedShape.Rectangular;

				case "circular":
					return MeshVisualizer.MeshViewerWidget.BedShape.Circular;

				default:
#if DEBUG
					throw new NotImplementedException(string.Format("'{0}' is not a known bed_shape.", ActiveValue("bed_shape")));
#else
                        return MeshVisualizer.MeshViewerWidget.BedShape.Rectangular;
#endif
			}
		}

		public Vector2 BedCenter()
		{
			return ActiveVector2("print_center");
		}

		public double BuildHeight()
		{
			return ParseDouble(ActiveValue("build_height"));
		}

		public Vector2 PrintCenter()
		{
			return ActiveVector2("print_center");
		}

		public int ExtruderCount()
		{
			if (this.GetValue<bool>("extruders_share_temperature"))
			{
				return 1;
			}

			int extruderCount;
			string extruderCountString = ActiveValue("extruder_count");
			if (!int.TryParse(extruderCountString, out extruderCount))
			{
				return 1;
			}

			return extruderCount;
		}

		public Vector2 ExtruderOffset(int extruderIndex)
		{
			string currentOffsets = ActiveValue("extruder_offset");
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

		public double FilamentDiameter()
		{
			return ParseDouble(ActiveValue("filament_diameter"));
		}

		private PrintLevelingData printLevelingData = null;
		public PrintLevelingData GetPrintLevelingData()
		{
			if (printLevelingData == null)
			{
				printLevelingData = PrintLevelingData.Create(
					ActiveSliceSettings.Instance,
					layeredProfile.GetValue("MatterControl.PrintLevelingData"),
					layeredProfile.GetValue("MatterControl.PrintLevelingProbePositions"));

				PrintLevelingPlane.Instance.SetPrintLevelingEquation(
					printLevelingData.SampledPosition0,
					printLevelingData.SampledPosition1,
					printLevelingData.SampledPosition2,
					ActiveSliceSettings.Instance.PrintCenter());
			}

			return printLevelingData;
		}

		public void SetPrintLevelingData(PrintLevelingData data)
		{
			printLevelingData = data;
			layeredProfile.SetActiveValue("MatterControl.PrintLevelingData", JsonConvert.SerializeObject(data));

		}

		public void DoPrintLeveling(bool doLeveling)
		{
			// Early exit if already set
			if (doLeveling == this.GetValue<bool>("MatterControl.PrintLevelingEnabled"))
			{
				return;
			}

			layeredProfile.SetActiveValue("MatterControl.PrintLevelingEnabled", doLeveling ? "1" : "0");

			DoPrintLevelingChanged.CallEvents(this, null);

			if (doLeveling)
			{
				PrintLevelingData levelingData = ActiveSliceSettings.Instance.GetPrintLevelingData();
				PrintLevelingPlane.Instance.SetPrintLevelingEquation(
					levelingData.SampledPosition0,
					levelingData.SampledPosition1,
					levelingData.SampledPosition2,
					ActiveSliceSettings.Instance.PrintCenter());
			}
		}

		private static readonly SlicingEngineTypes defaultEngineType = SlicingEngineTypes.MatterSlice;

		public SlicingEngineTypes ActiveSliceEngineType()
		{
				string engineType = layeredProfile.GetValue("MatterControl.SlicingEngine");
				if (string.IsNullOrEmpty(engineType))
				{
					return defaultEngineType;
				}

				var engine = (SlicingEngineTypes)Enum.Parse(typeof(SlicingEngineTypes), engineType);
				return engine;
		}

		public void ActiveSliceEngineType(SlicingEngineTypes type)
		{
			SetActiveValue("MatterControl.SlicingEngine", type.ToString());
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

		///<summary>
		///Returns the first matching value discovered while enumerating the settings layers
		///</summary>
		public string ActiveValue(string sliceSetting)
		{
			return layeredProfile.GetValue(sliceSetting);
		}

		public T GetValue<T>(string settingsKey) where T : IConvertible
		{
			if (typeof(T) == typeof(bool))
			{
				return (T)(object) (this.ActiveValue(settingsKey) == "1");
			}
			else if (typeof(T) == typeof(int))
			{
				int result;
				int.TryParse(this.ActiveValue(settingsKey), out result);
				return (T)(object)(result);
			}
			else if (typeof(T) == typeof(double))
			{
				string settingsStringh = ActiveValue(settingsKey);
				if (settingsStringh.Contains("%"))
				{
					string onlyNumber = settingsStringh.Replace("%", "");
					double ratio = ParseDouble(onlyNumber) / 100;

					if (settingsKey == "first_layer_height")
					{
						return (T)(object)(LayerHeight() * ratio);
					}

					return (T)(object)(ratio);
				}

				double result;
				double.TryParse(this.ActiveValue(settingsKey), out result);
				return (T)(object)(result);
			}

			return (T)default(T);
		}

		public string GetActiveValue(string sliceSetting, IEnumerable<PrinterSettingsLayer> layerCascade)
		{
			return layeredProfile.GetValue(sliceSetting, layerCascade);
		}

		public void SetActiveValue(string sliceSetting, string sliceValue)
		{
			layeredProfile.SetActiveValue(sliceSetting, sliceValue);
		}

		public void SetActiveValue(string sliceSetting, string sliceValue, PrinterSettingsLayer persistenceLayer)
		{
			layeredProfile.SetActiveValue(sliceSetting, sliceValue, persistenceLayer);
		}

		public void ClearValue(string sliceSetting)
		{
			layeredProfile.ClearValue(sliceSetting);
		}

		public void ClearValue(string sliceSetting, PrinterSettingsLayer persistenceLayer)
		{
			layeredProfile.ClearValue(sliceSetting, persistenceLayer);
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

		public Vector2 ActiveVector2(string sliceSetting)
		{
			string[] twoValues = ActiveValue(sliceSetting).Split(',');
			if (twoValues.Length != 2)
			{
				throw new Exception(string.Format("Not parsing {0} as a Vector2", sliceSetting));
			}
			Vector2 valueAsVector2 = new Vector2();
			valueAsVector2.x = ParseDouble(twoValues[0]);
			valueAsVector2.y = ParseDouble(twoValues[1]);
			return valueAsVector2;
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
				string activeValue = ActiveValue(keyValue.Key);
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
					string activeValue = ActiveValue(key);
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
				if (LayerHeight() > GetValue<double>("nozzle_diameter"))
				{
					string error = "'Layer Height' must be less than or equal to the 'Nozzle Diameter'.".Localize();
					string details = string.Format("Layer Height = {0}\nNozzle Diameter = {1}".Localize(), LayerHeight(), GetValue<double>("nozzle_diameter"));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'General' -> 'Layers/Surface'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}
				else if (GetValue<double>("first_layer_height") > GetValue<double>("nozzle_diameter"))
				{
					string error = "'First Layer Height' must be less than or equal to the 'Nozzle Diameter'.".Localize();
					string details = string.Format("First Layer Height = {0}\nNozzle Diameter = {1}".Localize(), GetValue<double>("first_layer_height"), GetValue<double>("nozzle_diameter"));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'General' -> 'Layers/Surface'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				// If we have print leveling turned on then make sure we don't have any leveling commands in the start gcode.
				if (PrinterConnectionAndCommunication.Instance.ActivePrinter.GetValue<bool>("MatterControl.PrintLevelingEnabled"))
				{
					string[] startGCode = ActiveValue("start_gcode").Replace("\\n", "\n").Split('\n');
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

				if (FirstLayerExtrusionWidth() > GetValue<double>("nozzle_diameter") * 4)
				{
					string error = "'First Layer Extrusion Width' must be less than or equal to the 'Nozzle Diameter' * 4.".Localize();
					string details = string.Format("First Layer Extrusion Width = {0}\nNozzle Diameter = {1}".Localize(), ActiveValue("first_layer_extrusion_width"), GetValue<double>("nozzle_diameter"));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Extrusion' -> 'First Layer'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (FirstLayerExtrusionWidth() <= 0)
				{
					string error = "'First Layer Extrusion Width' must be greater than 0.".Localize();
					string details = string.Format("First Layer Extrusion Width = {0}".Localize(), ActiveValue("first_layer_extrusion_width"));
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Extrusion' -> 'First Layer'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (MinFanSpeed() > 100)
				{
					string error = "The Minimum Fan Speed can only go as high as 100%.".Localize();
					string details = string.Format("It is currently set to {0}.".Localize(), MinFanSpeed());
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Cooling'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (MaxFanSpeed() > 100)
				{
					string error = "The Maximum Fan Speed can only go as high as 100%.".Localize();
					string details = string.Format("It is currently set to {0}.".Localize(), MaxFanSpeed());
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Filament' -> 'Cooling'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (ExtruderCount() < 1)
				{
					string error = "The Extruder Count must be at least 1.".Localize();
					string details = string.Format("It is currently set to {0}.".Localize(), ExtruderCount());
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'Printer' -> 'Features'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (FillDensity() < 0 || FillDensity() > 1)
				{
					string error = "The Fill Density must be between 0 and 1.".Localize();
					string details = string.Format("It is currently set to {0}.".Localize(), FillDensity());
					string location = "Location: 'Settings & Controls' -> 'Settings' -> 'General' -> 'Infill'".Localize();
					StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error".Localize());
					return false;
				}

				if (FillDensity() == 1
					&& ActiveValue("infill_type") != "LINES")
				{
					string error = "Solid Infill works best when set to LINES.".Localize();
					string details = string.Format("It is currently set to {0}.".Localize(), ActiveValue("infill_type"));
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
			string actualSpeedValueString = ActiveValue(speedSetting);
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

		public void SetAutoConnect(bool autoConnectPrinter)
		{
			layeredProfile.SetActiveValue("MatterControl.AutoConnect", autoConnectPrinter ? "1" : "0");
		}

		public void SetMarkedForDelete(bool markedForDelete)
		{
			// TODO: It's unfortunate that changes to UI elements drive the SettingsChanged event rather than the data model. As such, we must manually
			// MarkedForDelete and call profile save
			ProfileManager.Instance.ActiveProfile.MarkedForDelete = markedForDelete;
			ProfileManager.Instance.Save();
			SetActiveValue("MatterControl.MarkedForDelete", "1");

			UiThread.RunOnIdle(() => ActiveSliceSettings.Instance = ProfileManager.LoadEmptyProfile());
		}


		public string BaudRate()
		{
			return layeredProfile.GetValue("MatterControl.BaudRate");
		}

		public void SetBaudRate(string baudRate)
		{
			layeredProfile.SetActiveValue("MatterControl.BaudRate", baudRate);
		}

		public string ComPort()
		{
			return layeredProfile.GetValue(string.Format("MatterControl.{0}.ComPort", Environment.MachineName));
		}

		public void SetComPort(string port)
		{
			layeredProfile.SetActiveValue(string.Format("MatterControl.{0}.ComPort", Environment.MachineName), port);
		}

		public void SetComPort(string port, PrinterSettingsLayer layer)
		{
			layeredProfile.SetActiveValue(string.Format("MatterControl.{0}.ComPort", Environment.MachineName), port, layer);
		}

		public string SlicingEngine()
		{
			return layeredProfile.GetValue("MatterControl.SlicingEngine");
		}

		public void SetSlicingEngine(string engine)
		{
			layeredProfile.SetActiveValue("MatterControl.SlicingEngine", engine);
		}

		public string DriverType()
		{
			return layeredProfile.GetValue("MatterControl.DriverType");
		}

		public void SetDriverType(string driver)
		{
			layeredProfile.SetActiveValue("MatterControl.DriverType", driver);
		}

		public string DeviceToken()
		{
			return layeredProfile.GetValue("MatterControl.DeviceToken");
		}

		public void SetDeviceToken(string token)
		{
			if (layeredProfile.GetValue("MatterControl.DeviceToken") != token)
			{
				layeredProfile.SetActiveValue("MatterControl.DeviceToken", token);
			}
		}

		public string DeviceType => layeredProfile.GetValue("MatterControl.DeviceType");

		public string Make => layeredProfile.GetValue("MatterControl.Make");

		// Rename to PrinterName
		public string Name()
		{
			return layeredProfile.GetValue("MatterControl.PrinterName");
		}

		public void SetName(string name)
		{
			layeredProfile.SetActiveValue("MatterControl.PrinterName", name);
		}

		public string Model => layeredProfile.GetValue("MatterControl.Model");

		[JsonIgnore]
		HashSet<string> knownSettings = null;
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

		public string ManualMovementSpeeds()
		{
			return layeredProfile.GetValue("MatterControl.ManualMovementSpeeds");
		}

		public void SetManualMovementSpeeds(string speed)
		{
			layeredProfile.SetActiveValue("MatterControl.ManualMovementSpeeds", speed);
		}

		private List<string> printerDrivers = null;

		public List<string> PrinterDrivers()
		{
			if(printerDrivers == null)
			{
				printerDrivers = GetPrintDrivers();
			}

			return printerDrivers;
		}

		private List<string> GetPrintDrivers()
		{
			var drivers = new List<string>();

			//Determine what if any drivers are needed
			string infFileNames = ActiveValue("windows_driver");
			if (!string.IsNullOrEmpty(infFileNames))
			{
				string[] fileNames = infFileNames.Split(',');
				foreach (string fileName in fileNames)
				{
					switch (OsInformation.OperatingSystem)
					{
						case OSType.Windows:

							string pathForInf = Path.GetFileNameWithoutExtension(fileName);

							// TODO: It's really unexpected that the driver gets copied to the temp folder every time a printer is setup. I'd think this only needs
							// to happen when the infinstaller is run (More specifically - move this to *after* the user clicks Install Driver)

							string infPath = Path.Combine("Drivers", pathForInf);
							string infPathAndFileToInstall = Path.Combine(infPath, fileName);

							if (StaticData.Instance.FileExists(infPathAndFileToInstall))
							{
								// Ensure the output directory exists
								string destTempPath = Path.GetFullPath(Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "temp", "inf", pathForInf));
								if (!Directory.Exists(destTempPath))
								{
									Directory.CreateDirectory(destTempPath);
								}

								string destTempInf = Path.GetFullPath(Path.Combine(destTempPath, fileName));

								// Sync each file from StaticData to the location on disk for serial drivers
								foreach (string file in StaticData.Instance.GetFiles(infPath))
								{
									using (Stream outstream = File.OpenWrite(Path.Combine(destTempPath, Path.GetFileName(file))))
									using (Stream instream = StaticData.Instance.OpenSteam(file))
									{
										instream.CopyTo(outstream);
									}
								}

								drivers.Add(destTempInf);
							}
							break;

						default:
							break;
					}
				}
			}

			return drivers;
		}

		public List<GCodeMacro> Macros => layeredProfile.Macros;
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
				// TODO: Short term hack to silently upgrade existing profiles with missing ID
				string layerKey = ValueOrDefault("MatterControl.LayerID");
				if (string.IsNullOrEmpty(layerKey))
				{
					layerKey = Guid.NewGuid().ToString();
					LayerID = layerKey;
				}

				return layerKey;
			}
			set
			{
				this["MatterControl.LayerID"] = value;
			}
		}

		public string Name
		{
			get
			{
				return ValueOrDefault("MatterControl.LayerName");
			}
			set
			{
				this["MatterControl.LayerName"] = value;
			}
		}

		public string Source
		{
			get
			{
				return ValueOrDefault("MatterControl.LayerSource");
			}
			set
			{
				this["MatterControl.LayerSource"] = value;
			}
		}

		public string ETag
		{
			get
			{
				return ValueOrDefault("MatterControl.LayerETag");
			}
			set
			{
				this["MatterControl.LayerETag"] = value;
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