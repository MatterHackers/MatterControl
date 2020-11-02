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
using System.IO;
using System.Linq;
using System.Xml;
using SettingsDictionary = System.Collections.Generic.Dictionary<string, string>;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class PrinterSettingsLayer : SettingsDictionary
	{
		public PrinterSettingsLayer() { }

		public PrinterSettingsLayer(IDictionary<string, string> settingsDictionary)
		{
			foreach (var keyValue in settingsDictionary)
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

			set => this["layer_id"] = value;
		}

		public string Name
		{
			get => ValueOrDefault(SettingsKey.layer_name);
			set => this[SettingsKey.layer_name] = value;
		}

		public string Source
		{
			get => ValueOrDefault("layer_source");
			set => this["layer_source"] = value;
		}

		public string ETag
		{
			get => ValueOrDefault("layer_etag");
			set => this["layer_etag"] = value;
		}

		public string ValueOrDefault(string key, string defaultValue = "")
		{
			this.TryGetValue(key, out string foundValue);
			return foundValue ?? defaultValue;
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
			return new PrinterSettingsLayer(this)
			{
				LayerID = id,
				Name = this.Name,
				ETag = this.ETag,
				Source = this.Source
			};
		}

		public static Dictionary<string, PrinterSettingsLayer> LoadMaterialSettingsFromFff(string settingsFilePath)
		{
			return LoadNamedSettigs(settingsFilePath, "autoConfigureMaterial");
		}

		private static Dictionary<string, PrinterSettingsLayer> LoadNamedSettigs(string settingsFilePath, string xmlNodes)
		{
			var xmlDoc = new XmlDocument();
			xmlDoc.Load(settingsFilePath);

			var profile = xmlDoc.SelectSingleNode("profile");

			var materials = new Dictionary<string, PrinterSettingsLayer>();

			var materialNodes = profile.SelectNodes(xmlNodes);
			for (var i = 0; i < materialNodes.Count; i++)
			{
				var materialNode = materialNodes[i];
				var material = new PrinterSettingsLayer();
				materialNode.ReadSettings(material, false);
				var materialName = materialNode.Attributes["name"].InnerText;
				material[SettingsKey.layer_name] = materialName;
				materials.Add(materialName, material);
			}

			return materials;
		}

		public static Dictionary<string, PrinterSettingsLayer> LoadQualitySettingsFromFff(string settingsFilePath)
		{
			return LoadNamedSettigs(settingsFilePath, "autoConfigureQuality");
		}

		public static PrinterSettingsLayer LoadFromFff(string settingsFilePath)
		{
			var xmlDoc = new XmlDocument();
			xmlDoc.Load(settingsFilePath);

			var profile = xmlDoc.SelectSingleNode("profile");

			var layer = new PrinterSettingsLayer();

			layer[SettingsKey.printer_name] = profile.Attributes["name"].InnerText;
			profile.ReadSettings(layer, true);

			return layer;
		}
	}

	public static class XmlDocSettingsReader
	{
		public static void ReadSettings(this XmlNode profile, PrinterSettingsLayer layer, bool inculdePrinterSettings)
		{
			if (inculdePrinterSettings)
			{
				// printer hardware settings
				profile.SetNumber(layer, "toolheadNumber", SettingsKey.extruder_count);
				profile.SetNumber(layer, "baudRateOverride", SettingsKey.baud_rate);
				profile.SetNumber(layer, "filamentDiameters", SettingsKey.filament_diameter);

				// bed setting
				profile.ReadNumber("strokeXoverride", out double xSize);
				profile.ReadNumber("strokeYoverride", out double ySize);
				layer[SettingsKey.bed_size] = $"{xSize},{ySize}";
				profile.SetNumber(layer, "strokeZoverride", SettingsKey.build_height);
				profile.ReadNumber("originOffsetXoverride", out double xOrigin);
				profile.ReadNumber("originOffsetYoverride", out double yOrigin);
				layer[SettingsKey.print_center] = $"{xSize / 2 - xOrigin},{ySize / 2 - yOrigin}";
				profile.ReadNumber("machineTypeOverride", out double machineType);
				if (machineType == 1)
				{
					layer[SettingsKey.bed_shape] = "circular";
				}

				// homing info
				profile.ReadNumber("homeZdirOverride", out double zDir);
				if (zDir == 1)
				{
					layer[SettingsKey.z_homes_to_max] = "1";
				}
			}

			profile.SetCommaString(layer, "startingGcode", SettingsKey.start_gcode);
			if (inculdePrinterSettings
				&& layer.ContainsKey(SettingsKey.start_gcode)
				&& layer[SettingsKey.start_gcode].Contains("G29") == true)
			{
				layer[SettingsKey.has_hardware_leveling] = "1";
			}

			profile.SetCommaString(layer, "endingGcode", SettingsKey.end_gcode);

			// default material settings
			profile.Set(layer, "printMaterial", "printMaterial");
			profile.Set(layer, "printQuality", "printQuality");
			profile.SetNumber(layer, "filamentDensities", SettingsKey.filament_density);
			profile.SetNumber(layer, "filamentPricesPerKg", SettingsKey.filament_cost);
			// layers
			profile.SetNumber(layer, "layerHeight", SettingsKey.layer_height);
			profile.SetNumber(layer, "topSolidLayers", SettingsKey.top_solid_layers);
			profile.SetNumber(layer, "bottomSolidLayers", SettingsKey.bottom_solid_layers);
			profile.SetNumber(layer, "externalInfillAngles", SettingsKey.fill_angle);
			profile.SetPercent(layer, "infillPercentage", SettingsKey.fill_density);
			profile.SetPercent(layer, "firstLayerUnderspeed", SettingsKey.first_layer_speed, (value) => value * 100);
			// perimeters
			profile.SetNumber(layer, "perimeterOutlines", SettingsKey.perimeters);
			profile.SetBool(layer, "printPerimetersInsideOut", SettingsKey.external_perimeters_first, true);
			// support
			profile.SetNumber(layer, "supportGridSpacing", SettingsKey.support_material_spacing);
			profile.SetNumber(layer, "supportAngles", SettingsKey.support_material_infill_angle);
			profile.SetNumber(layer, "maxOverhangAngle", SettingsKey.fill_angle);
			// raft settings
			profile.SetBool(layer, "useRaft", SettingsKey.create_raft);
			profile.SetNumber(layer, "raftExtruder", SettingsKey.raft_extruder);
			profile.SetNumber(layer, "raftOffset", SettingsKey.raft_extra_distance_around_part);
			profile.SetNumber(layer, "raftSeparationDistance", SettingsKey.raft_air_gap);
			// skirt settings
			profile.SetBool(layer, "useSkirt", SettingsKey.create_skirt);
			profile.SetNumber(layer, "skirtOffset", SettingsKey.skirt_distance);
			profile.SetNumber(layer, "skirtOutlines", SettingsKey.skirts);
			// fan settings

			// extruder
			var extruder = profile.SelectSingleNode("extruder");
			if (extruder != null)
			{
				if (inculdePrinterSettings)
				{
					extruder.SetNumber(layer, "diameter", SettingsKey.nozzle_diameter);
				}

				extruder.SetNumber(layer, "extrusionMultiplier", SettingsKey.extrusion_multiplier);
				extruder.SetNumber(layer, "retractionDistance", SettingsKey.retract_length);
				extruder.SetNumber(layer, "extraRestartDistance", SettingsKey.retract_restart_extra);
				extruder.SetNumber(layer, "retractionZLift", SettingsKey.retract_lift);
				extruder.SetNumber(layer, "coastingDistance", SettingsKey.coast_at_end_distance, (rawValue) =>
				{
					if (extruder.IsSet("useCoasting"))
					{
						return rawValue;
					}

					return 0;
				});
			}

			var tempControlers = profile.SelectNodes("temperatureController");
			for (var i = 0; i < tempControlers.Count; i++)
			{
				var tempControler = tempControlers[i];
				var innerText = tempControler.InnerText;
				var bed = tempControler.SelectNodes("isHeatedBed")[0].InnerText;
				if (bed == "1")
				{
					layer[SettingsKey.bed_temperature] = tempControler.SelectNodes("setpoint")[0].Attributes["temperature"].InnerText;
				}
				else
				{
					layer[SettingsKey.temperature] = tempControler.SelectNodes("setpoint")[0].Attributes["temperature"].InnerText;
				}
			}
		}

		public static void SetNumber(this XmlNode node,
			PrinterSettingsLayer layer,
			string fffSetting,
			string mcSetting,
			Func<double, double> convert = null)
		{
			// read valid settings and map
			if (node.ReadNumber(fffSetting, out double result, convert))
			{
				layer[mcSetting] = result.ToString();
			}
		}

		public static void SetPercent(this XmlNode node,
			PrinterSettingsLayer layer,
			string fffSetting,
			string mcSetting,
			Func<double, double> convert = null)
		{
			// read valid settings and map
			if (node.ReadNumber(fffSetting, out double result, convert))
			{
				layer[mcSetting] = result.ToString() + "%";
			}
		}

		public static bool ReadNumber(this XmlNode node, string fffSetting, out double value, Func<double, double> convert = null)
		{
			value = double.MinValue;
			// read valid settings and map
			var settings = node.SelectNodes(fffSetting);
			if (settings.Count > 0)
			{
				var innerText = settings[0].InnerText;
				if (innerText.Contains('|'))
				{
					innerText = innerText.Split('|')[0];
				}

				if (double.TryParse(innerText, out double result))
				{
					if (convert != null)
					{
						result = convert(result);
					}

					value = result;
					return true;
				}
			}

			return false;
		}

		public static void SetBool(this XmlNode node, PrinterSettingsLayer layer, string fffSetting, string mcSetting, bool invert = false)
		{
			var settings = node.SelectNodes(fffSetting);
			if (settings.Count > 0)
			{
				if (double.TryParse(settings[0].InnerText, out double result))
				{
					var value = result == 1 ? true : false;
					value = invert ? !value : value;
					layer[mcSetting] = value ? "1" : "0";
				}
			}
		}

		public static void Set(this XmlNode node, PrinterSettingsLayer layer, string fffSetting, string mcSetting)
		{
			// read valid settings and map
			var settings = node.SelectNodes(fffSetting);
			if (settings.Count > 0)
			{
				layer[mcSetting] = settings[0].InnerText;
			}
		}

		public static bool IsSet(this XmlNode node, string fffSetting)
		{
			// read valid settings and map
			var settings = node.SelectNodes(fffSetting);
			if (settings.Count > 0)
			{
				if (settings[0].InnerText == "1")
				{
					return true;
				}
			}

			return false;
		}

		public static void SetCommaString(this XmlNode node, PrinterSettingsLayer layer, string fffSetting, string mcSetting)
		{
			// read valid settings and map
			var settings = node.SelectNodes(fffSetting);
			if (settings.Count > 0)
			{
				var gCode = settings[0].InnerText;
				// do some string replacements
				gCode = gCode.Replace("bed0_temperature", "bed_temperature");
				gCode = gCode.Replace("extruder0_temperature", "temperature");

				layer[mcSetting] = gCode.Replace(",", "\\n");
			}
		}
	}
}