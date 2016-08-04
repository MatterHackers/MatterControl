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

using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Text;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public static class ProfileMigrations
	{
		public static string MigrateDocument(string filePath, int fromVersion = -1)
		{
			var jObject = JObject.Parse(File.ReadAllText(filePath));

			/*
			if (fromVersion < 201605131)
			{
				var materialLayers = jObject["MaterialLayers"] as JObject;
				if (materialLayers != null)
				{
					foreach (JProperty layer in materialLayers.Properties().ToList())
					{
						layer.Remove();

						string layerID = Guid.NewGuid().ToString();

						var body = layer.Value as JObject;
						body["MatterControl.LayerID"] = layerID;
						body["MatterControl.LayerName"] = layer.Name;

						materialLayers[layerID] = layer.Value;
					}
				}

				var qualityLayers = jObject["QualityLayers"] as JObject;
				if (qualityLayers != null)
				{
					foreach (JProperty layer in qualityLayers.Properties().ToList())
					{
						layer.Remove();

						string layerID = Guid.NewGuid().ToString();

						var body = layer.Value as JObject;
						body["MatterControl.LayerID"] = layerID;
						body["MatterControl.LayerName"] = layer.Name;

						qualityLayers[layerID] = layer.Value;
					}
				}


				jObject["DocumentVersion"] = 201605131;
			}

			if (fromVersion < 201605132)
			{
				string printerID = Guid.NewGuid().ToString();
				jObject.Remove("DocumentPath");
				jObject["ID"] = printerID;
				jObject["DocumentVersion"] = 201605132;

				File.Delete(filePath);
				filePath = Path.Combine(Path.GetDirectoryName(filePath), printerID + ProfileManager.ProfileExtension);
			}
			*/

			if (fromVersion < 201606081)
			{
				JObject materialLayers, qualityLayers;

				materialLayers = jObject["MaterialLayers"] as JObject;
				if (materialLayers != null)
				{
					jObject["MaterialLayers"] = new JArray(materialLayers.Properties().ToList().Select(layer => layer.Value).ToArray());
					qualityLayers = jObject["QualityLayers"] as JObject;
					jObject["QualityLayers"] = new JArray(qualityLayers.Properties().ToList().Select(layer => layer.Value).ToArray());

					var oemProfile = jObject["OemProfile"] as JObject;
					oemProfile.Property("MaterialLayers").Remove();
					oemProfile.Property("QualityLayers").Remove();
				}
				jObject["DocumentVersion"] = 201606081;
			}

			if (fromVersion < 201606161)
			{
				var layersToModify = new List<JObject>();

				layersToModify.Add(jObject["UserLayer"] as JObject);
				layersToModify.Add(jObject["OemProfile"]["OemLayer"] as JObject);
				layersToModify.AddRange(jObject["MaterialLayers"].Cast<JObject>());
				layersToModify.AddRange(jObject["QualityLayers"].Cast<JObject>());

				foreach (var layer in layersToModify)
				{
					var itemsToAdd = new List<KeyValuePair<string, JToken>>();

					foreach (var token in layer)
					{
						if (token.Key.StartsWith("MatterControl."))
						{
							itemsToAdd.Add(token);
						}
					}

					foreach (var item in itemsToAdd)
					{
						layer.Remove(item.Key);

						switch (item.Key)
						{
							case "MatterControl.PrinterName":
								layer.Add(SettingsKey.printer_name, item.Value);
								break;

							case "MatterControl.BaudRate":
								layer.Add(SettingsKey.baud_rate, item.Value);
								break;

							case "MatterControl.Make":
								layer.Add(SettingsKey.make, item.Value);
								break;

							case "MatterControl.Model":
								layer.Add(SettingsKey.model, item.Value);
								break;

							case "MatterControl.ComPort":
								layer.Add(SettingsKey.com_port, item.Value);
								break;

							case "MatterControl.AutoConnect":
								layer.Add(SettingsKey.auto_connect, item.Value);
								break;

							case "MatterControl.DefaultMaterialPresets":
								layer.Add(SettingsKey.default_material_presets, item.Value);
								break;

							case "MatterControl.WindowsDriver":
								layer.Add(SettingsKey.windows_driver, item.Value);
								break;

							case "MatterControl.DeviceToken":
								layer.Add(SettingsKey.device_token, item.Value);
								break;

							case "MatterControl.DriverType":
								layer.Add("driver_type", item.Value);
								break;

							case "MatterControl.DeviceType":
								layer.Add(SettingsKey.device_type, item.Value);
								break;

							case "MatterControl.ActiveThemeIndex":
								layer.Add(SettingsKey.active_theme_index, item.Value);
								break;

							case "MatterControl.PublishBedImage":
								layer.Add(SettingsKey.publish_bed_image, item.Value);
								break;

							case "MatterControl.PrintLevelingData":
								layer.Add("print_leveling_data", item.Value);
								break;

							case "MatterControl.PrintLevelingEnabled":
								layer.Add("print_leveling_enabled", item.Value);
								break;

							case "MatterControl.ManualMovementSpeeds":
								layer.Add("manual_movement_speeds", item.Value);
								break;

							case "MatterControl.SHA1":
								layer.Add("profile_sha1", item.Value);
								break;

							case "MatterControl.SlicingEngine":
								layer.Add("slicing_engine", item.Value);
								break;

							case "MatterControl.LayerName":
								layer.Add("layer_name", item.Value);
								break;

							case "MatterControl.LayerID":
								layer.Add("layer_id", item.Value);
								break;

							case "MatterControl.LayerSource":
								layer.Add("layer_source", item.Value);
								break;

							case "MatterControl.LayerETag":
								layer.Add("layer_etag", item.Value);
								break;

							case "MatterControl.CalibrationFiles":
								layer.Add("calibration_files", item.Value);
								break;

							case "MatterControl.ActiveQualityKey":
								layer.Add("active_quality_key", item.Value);
								break;

							case "MatterControl.ActiveMaterialKey":
							case "MatterControl.PrinterID":
							case "MatterControl.MarkedForDelete":
							case "MatterControl.DefaultQualityPreset":
							case "MatterControl.DefaultMacros":
							case "MatterControl.DefaultSliceEngine":
								// Simply delete abandoned setting
								break;

							default:

								if (item.Key.Contains("ComPort"))
								{
									var segments = item.Key.Split('.');

									if (segments.Length != 3)
									{
										throw new Exception($"Unable to migrate settings: {item.Key}/{item.Value}");
									}
									layer.Add(segments[1] + "_com_port", item.Value);
									break;
								}
								else
								{
									throw new Exception($"Unable to migrate settings: {item.Key}/{item.Value}");
								}
						}
					}
				}

				jObject["DocumentVersion"] = 201606161;
			}

			if (fromVersion < 201606271)
			{
				JObject oemProfile = jObject["OemProfile"] as JObject;
				if (oemProfile != null)
				{
					jObject.Property("OemProfile").Remove();
					jObject["OemLayer"] = oemProfile["OemLayer"];

				}
				jObject["DocumentVersion"] = 201606271;
			}

			File.WriteAllText(
						filePath,
						JsonConvert.SerializeObject(jObject, Formatting.Indented));

			return filePath;
		}
	}
}