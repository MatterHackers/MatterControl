using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Tests.Automation;
using Newtonsoft.Json;
using NUnit.Framework;
using TestInvoker;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture, Category("OemProfiles")]
	public class OemProfileTests
	{
		private static List<PrinterTestDetails> allPrinters;
		private static string printerSettingsDirectory = Path.Combine(MatterControlUtilities.StaticDataPath, "Profiles");

		private string pauseGCode = @"M76 ; pause print timer
G91
G1 Z10 E-5.0 F1800
G90
G1 X5 F[travel_speed]

M300 S3000 P30   ; Pause Tone
M300 S1500 P30   ; Pause Tone
M300 S3000 P30   ; Pause Tone
M300 S1500 P30   ; Pause Tone
M300 S3000 P30   ; Pause Tone
M300 S1500 P30   ; Pause Tone
M300 S3000 P30   ; Pause Tone
M300 S1500 P30   ; Pause Tone
M300 S750 P30    ; Pause Tone
M300 S1500 P30   ; Pause Tone
M300 S750 P30    ; Pause Tone
M300 S1500 P30   ; Pause Tone
M300 S750 P30    ; Pause Tone
M300 S1500 P30   ; Pause Tone
M300 S750 P30    ; Pause Tone";

		private string resumeGCode = @"M75 ; Start the print job timer
G91
G1 Z-10 F1800
G90

M300 S750 P30    ; Resume Tone
M300 S1500 P30   ; Resume Tone
M300 S750 P30    ; Resume Tone
M300 S1500 P30   ; Resume Tone
M300 S750 P30    ; Resume Tone
M300 S1500 P30   ; Resume Tone
M300 S750 P30    ; Resume Tone
M300 S1500 P30   ; Resume Tone
M300 S3000 P30   ; Resume Tone
M300 S1500 P30   ; Resume Tone
M300 S3000 P30   ; Resume Tone
M300 S1500 P30   ; Resume Tone
M300 S3000 P30   ; Resume Tone
M300 S1500 P30   ; Resume Tone
M300 S3000 P30   ; Resume Tone";

		static OemProfileTests()
		{
			StaticData.RootPath = MatterControlUtilities.StaticDataPath;
			MatterControlUtilities.OverrideAppDataLocation(MatterControlUtilities.RootPath);

			allPrinters = (from printerFile in new DirectoryInfo(printerSettingsDirectory).GetFiles("*.printer", SearchOption.AllDirectories)
						   select new PrinterTestDetails
						   {
							   PrinterName = printerFile.Name,
							   Oem = printerFile.Directory.Name,
							   ConfigPath = printerFile.FullName,
							   RelativeFilePath = printerFile.FullName.Substring(printerSettingsDirectory.Length + 1),
							   PrinterSettings = PrinterSettings.LoadFile(printerFile.FullName)
						   }).ToList();
		}

		[Test, ChildProcessTest]
		public void ModifyPulsePrinterProfilesSettings()
		{
			// This is not really a test. It updaets our profiles with new settings.
			return;

			StaticData.RootPath = MatterControlUtilities.StaticDataPath;
			MatterControlUtilities.OverrideAppDataLocation(MatterControlUtilities.RootPath);

			string profilePath = @"C:\\Users\\LarsBrubaker\\Downloads\\Pulse E Profiles";
			allPrinters = (from printerFile in new DirectoryInfo(profilePath).GetFiles("*.printer", SearchOption.TopDirectoryOnly)
						   select new PrinterTestDetails
						   {
							   PrinterName = printerFile.Name,
							   Oem = printerFile.Directory.Name,
							   ConfigPath = printerFile.FullName,
							   RelativeFilePath = printerFile.FullName.Substring(printerSettingsDirectory.Length + 1),
							   PrinterSettings = PrinterSettings.LoadFile(printerFile.FullName)
						   }).ToList();

			string ConvertString(string input)
            {
				return input.Replace("\n", "\\n").Replace("\r", "");
            }

			void ChangeSettings(PrinterSettings printerSettings)
            {
				var printerModel = printerSettings.GetValue(SettingsKey.model);

				// general
				printerSettings.SetValue(SettingsKey.fill_density, "30%");
				printerSettings.SetValue(SettingsKey.avoid_crossing_perimeters, "1");
				printerSettings.SetValue(SettingsKey.merge_overlapping_lines, "1");
				printerSettings.SetValue(SettingsKey.seam_placement, "Centered In Back");
				printerSettings.SetValue(SettingsKey.expand_thin_walls, "1");
				printerSettings.SetValue(SettingsKey.coast_at_end_distance, "0");
				printerSettings.SetValue(SettingsKey.monotonic_solid_infill, "1");
				printerSettings.SetValue(SettingsKey.infill_overlap_perimeter, "20%");
				printerSettings.SetValue(SettingsKey.avoid_crossing_max_ratio, "3");
				printerSettings.SetValue(SettingsKey.perimeter_start_end_overlap, "35");
				// speed
				printerSettings.SetValue(SettingsKey.external_perimeter_speed, "25");
				printerSettings.SetValue(SettingsKey.perimeter_acceleration, "800");
				printerSettings.SetValue(SettingsKey.default_acceleration, "1300");
				printerSettings.SetValue(SettingsKey.bridge_over_infill, "1");
				// adheasion
				printerSettings.SetValue(SettingsKey.create_skirt, "1");
				// support
				printerSettings.SetValue(SettingsKey.retract_lift, ".6");
				printerSettings.SetValue(SettingsKey.min_extrusion_before_retract, "0");
				printerSettings.SetValue(SettingsKey.retract_before_travel_avoid, "20");
				
				// printer gcode settings
				printerSettings.SetValue(SettingsKey.pause_gcode, ConvertString(pauseGCode));
				printerSettings.SetValue(SettingsKey.resume_gcode, ConvertString(resumeGCode));

				printerSettings.SetValue(SettingsKey.support_material_create_perimeter, "1");

				// e series settings
				if (printerModel.Contains('E'))
				{
					// clear all material settings
					printerSettings.MaterialLayers = new List<PrinterSettingsLayer>();
					printerSettings.ActiveMaterialKey = "";

					printerSettings.SetValue(SettingsKey.has_swappable_bed, "1");
					
					printerSettings.SetValue(SettingsKey.bed_temperature_buildtak, "55");
					printerSettings.SetValue(SettingsKey.bed_temperature_garolite, "75");
					printerSettings.SetValue(SettingsKey.bed_temperature_glass, "75");
					printerSettings.SetValue(SettingsKey.bed_temperature_kapton, "55");
					printerSettings.SetValue(SettingsKey.bed_temperature_pei, "75");
					printerSettings.SetValue(SettingsKey.bed_temperature_pp, "55");

					var zHeight = 215;
					// check the build height based on the extruder type
					switch (printerModel[3])
                    {
						case '3': // Volcano
						case '6': // LGX E3Dv6
							zHeight = 205;
							break;							
					}

					printerSettings.SetValue(SettingsKey.build_height, zHeight.ToString());

					// make sure the start gcode travels fast to priming line
					var startGCode = printerSettings.GetValue(SettingsKey.start_gcode);
					startGCode = startGCode.Replace("G1 Y5 X5 Z0.8 F1800    ; Purge line", "G1 Y5 X5 [travel_speed]    ; Purge line\\nG1 Z0.8 F1800");
					printerSettings.SetValue(SettingsKey.start_gcode, startGCode);
				}

				// 32 bit settings
				if (printerModel.Contains('M') || printerModel.Contains('S'))
				{
					// If the board is 32 bit we cannot update the firmware.
					// make sure it does not show a firmware updater
					printerSettings.SetValue(SettingsKey.include_firmware_updater, "None");
				}
			}

			foreach (var printer in allPrinters)
            {
				ChangeSettings(printer.PrinterSettings);

				printer.PrinterSettings.Save(Path.Combine(Path.GetDirectoryName(printer.ConfigPath), "output", printer.PrinterName), true);
            }

			int a = 0;
		}

		[Test, ChildProcessTest]
		public void AllMaterialsLibraryHasGoodProfiles()
		{
			var materialSettingsDirectory = Path.Combine(MatterControlUtilities.StaticDataPath, "Materials");
			var directoryInfo = new DirectoryInfo(materialSettingsDirectory);
			var files = directoryInfo.GetFiles("*.material", SearchOption.AllDirectories);
			var profiles = files.Select(f => PrinterSettings.LoadFile(f.FullName)).ToList();

			var allMaterialIds = new HashSet<string>();

			var notPresentKeys = new string[]
			{
				"brims",
				"brims_layers",
				"coast_at_end_distance",
				"create_brim",
				"create_skirt",
				"expand_thin_walls",
				"extruder_count",
				"extrusion_multiplier",
				"extrusion_ratio",
				"filament_diameter",
				"fill_angle",
				// "fill_density", // BASF uses this
				"fill_pattern",
				"fill_thin_gaps",
				"first_layer_extrusion_width",
				"first_layer_height",
				"layer_height",
				"monotonic_solid_infill"
			};

			var isPresentKeys = new string[]
			{
				"bed_temperature_blue_tape",
				"bed_temperature_buildtak",
				"bed_temperature_garolite",
				"bed_temperature_glass",
				"bed_temperature_kapton",
				"bed_temperature_pei",
				"bed_temperature_pp",
				"filament_density",
				"layer_id",
				"layer_name",
				"material_sku",
				"temperature",
			};

			for(var i = 0; i < profiles.Count; i++)
            {
				var profile = profiles[i];
				Assert.AreEqual(1, profile.MaterialLayers.Count, "Each material profile should have 1 material in it");
				var material = profile.MaterialLayers[0];
				profile.ActiveMaterialKey = material.LayerID;
				Assert.IsTrue(!string.IsNullOrEmpty(profile.GetValue(SettingsKey.material_sku)), $"All profiles should have a material_sku set {files[i].FullName}");
				Assert.IsTrue(!allMaterialIds.Contains(material.LayerID), $"Every material needs a unique Id {files[i].FullName}");
				foreach(var key in isPresentKeys)
                {
					Assert.IsTrue(material.ContainsKey(key), $"Material {files[i].FullName} should include {key} setting");
				}

				if (profile.GetValue(SettingsKey.layer_name).ToLower().Contains("nylon"))
                {
					// make sure the setting for garolite is greater than 0 and not NC
					double.TryParse(profile.GetValue(SettingsKey.bed_temperature_garolite), out double temp);
					Assert.Greater(temp, 0);
				}

				foreach (var key in notPresentKeys)
                {
					Assert.IsTrue(!material.ContainsKey(key), $"Material {files[i].FullName} should not include {key} setting");
				}
				allMaterialIds.Add(material.LayerID);
			}
		}


		[Test, ChildProcessTest]
		public void LayerGCodeHasExpectedValue()
		{
			// Verifies "layer_gcode" is expected value: "; LAYER:[layer_num]"
			ValidateOnAllPrinters((printer, settings, settingsType) =>
			{
				if (settings.GetValue(SettingsKey.layer_gcode) != "; LAYER:[layer_num]")
				{
					printer.RuleViolated = true;

					// SetSettingInOem(printer, settings, SettingsKey.layer_gcode, "; LAYER:[layer_num]");
				}
			});
		}

		private static void SetSettingInOem(PrinterTestDetails printer, PrinterSettings settings, string key, string value)
		{
			// Fix existing invalid items...
			string layerValue;
			if (settings.OemLayer.TryGetValue(key, out layerValue) && layerValue == "")
			{
				settings.OemLayer.Remove(key);
			}

			if (settings.QualityLayer?.TryGetValue(key, out layerValue) == true && layerValue == "")
			{
				settings.QualityLayer.Remove(key);
			}

			if (settings.MaterialLayer?.TryGetValue(key, out layerValue) == true && layerValue == "")
			{
				settings.MaterialLayer.Remove(key);
			}

			settings.OemLayer[key] = value;

			// Reset to default values
			settings.UserLayer.Remove(SettingsKey.active_quality_key);
			settings.UserLayer.Remove(SettingsKey.active_material_key);
			settings.StagedUserSettings = new PrinterSettingsLayer();

			settings.Save(printer.ConfigPath);
		}

		[Test]
		public void StartGCodeWithExtrudesMustFollowM109Heatup()
		{
			ValidateOnAllPrinters((printer, settings, settingsType) =>
			{
				// Get the start_gcode string
				string startGcode = settings.OemLayer.ValueOrDefault(SettingsKey.start_gcode) ?? string.Empty;

				// Only validate start_gcode configs that have M109 and extrude statements
				if (startGcode.Contains("M109") && startGcode.Contains("G1 E"))
				{
					// Split start_gcode on newlines
					var lines = startGcode.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.ToUpper().Trim()).ToList();

					// Find first instance of M109 or 'G1 E' extrude
					string m109Line = lines.Where(l => l.StartsWith("M109 ")).FirstOrDefault();
					string extrudeLine = lines.Where(l => l.StartsWith("G1 E")).FirstOrDefault();

					if (m109Line == null)
					{
						printer.RuleViolated = true;
						return;
					}

					int m109Pos = lines.IndexOf(m109Line);
					int extrudePos = lines.IndexOf(extrudeLine);

					Assert.IsNotNull(m109Line);
					// Assert.IsNotNull(emptyExtrudeLine);
					// Assert.Greater(emptyExtrudePos, m109Pos);

					if (extrudePos < m109Pos)
					{
						printer.RuleViolated = true;
					}
				}
			});
		}

		[Test]
		public void CsvBedSizeExistsAndHasTwoValues()
		{
			ValidateOnAllPrinters((printer, settings, settingsType) =>
			{
				// Bed size is not required in slice files
				if (printer.RelativeFilePath.IndexOf(".slice", StringComparison.OrdinalIgnoreCase) != -1)
				{
					return;
				}

				string bedSize = settings.GetValue(SettingsKey.bed_size);

				// Must exist in all configs
				Assert.IsTrue(!string.IsNullOrEmpty(bedSize), "[bed_size] must exist: " + printer.RelativeFilePath);

				string[] segments = bedSize.Trim().Split(',');

				// Must be a CSV and have two values
				Assert.AreEqual(2, segments.Length, "[bed_size] should have two values separated by a comma: " + printer.RelativeFilePath);
			});
		}

		[Test]
		public void CsvPrintCenterExistsAndHasTwoValues()
		{
			ValidateOnAllPrinters((printer, settings, settingsType) =>
			{
				// Printer center is not required in slice files
				if (printer.RelativeFilePath.IndexOf(".slice", StringComparison.OrdinalIgnoreCase) != -1)
				{
					return;
				}

				string printCenter = settings.GetValue(SettingsKey.print_center);

				// Must exist in all configs
				Assert.IsTrue(!string.IsNullOrEmpty(printCenter), "[print_center] must exist: " + printer.RelativeFilePath);

				string[] segments = printCenter.Trim().Split(',');

				// Must be a CSV and have only two values
				Assert.AreEqual(2, segments.Length, "[print_center] should have two values separated by a comma: " + printer.RelativeFilePath);

#if false
				// save out the material settings
				foreach(var materialLayer in settings.MaterialLayers)
                {
					// create an empyt profile
					var materialSettings = new PrinterSettings();
					// copy just this material setting to it
					materialSettings.MaterialLayers.Add(materialLayer.Clone());
					// save it
					var fileName = ApplicationController.Instance.SanitizeFileName(materialLayer.Name);
					if (!string.IsNullOrEmpty(fileName))
					{
						fileName = Path.Combine(@"C:\temp", "materials", fileName) + ".material";
						File.WriteAllText(fileName, JsonConvert.SerializeObject(materialSettings, Formatting.Indented));
					}
				}
#endif
			});
		}

		[Test]
		public void RetractLengthIsLessThanTwenty()
		{
			ValidateOnAllPrinters((printer, settings, settingsType) =>
			{
				string retractLengthString = settings.GetValue(SettingsKey.retract_length);
				if (!string.IsNullOrEmpty(retractLengthString))
				{
					float retractLength;
					if (!float.TryParse(retractLengthString, out retractLength))
					{
						Assert.Fail("Invalid [retract_length] value (float parse failed): " + printer.RelativeFilePath);
					}

					Assert.Less(retractLength, 20, "[retract_length]: " + printer.RelativeFilePath);
				}
			});
		}

		[Test]
		public void ExtruderCountIsGreaterThanZero()
		{
			ValidateOnAllPrinters((printer, settings, settingsType) =>
			{
				string extruderCountString = settings.GetValue("extruder_count");
				if (!string.IsNullOrEmpty(extruderCountString))
				{
					int extruderCount;
					if (!int.TryParse(extruderCountString, out extruderCount))
					{
						Assert.Fail("Invalid [extruder_count] value (int parse failed): " + printer.RelativeFilePath);
					}

					// Must be greater than zero
					Assert.Greater(extruderCount, 0, "[extruder_count]: " + printer.RelativeFilePath);
				}
			});
		}

		[Test]
		public void MinFanSpeedOneHundredOrLess()
		{
			ValidateOnAllPrinters((printer, settings, settingsType) =>
			{
				string fanSpeedString = settings.GetValue(SettingsKey.min_fan_speed);
				if (!string.IsNullOrEmpty(fanSpeedString))
				{
					// Must be valid int data
					int minFanSpeed;
					if (!int.TryParse(fanSpeedString, out minFanSpeed))
					{
						Assert.Fail("Invalid [min_fan_speed] value (int parse failed): " + printer.RelativeFilePath);
					}

					// Must be less than or equal to 100
					Assert.LessOrEqual(minFanSpeed, 100, "[min_fan_speed]: " + printer.RelativeFilePath);
				}
			});
		}

		[Test]
		public void PlaAndAbsDensitySetCorrectly()
		{
			ValidateOnAllPrinters((printer, settings, settingsType) =>
			{
				if (settings.OemLayer.ContainsKey(SettingsKey.layer_name))
				{
					if (settings.OemLayer[SettingsKey.layer_name].ToUpper() == "ABS")
					{
						double absDensity = settings.GetValue<double>(SettingsKey.filament_density);
						if (absDensity != 1.04)
						{
							Assert.Fail("[filament_density] value should be set to ABS 1.04: " + printer.RelativeFilePath);
						}
					}
					else if (settings.OemLayer[SettingsKey.layer_name].ToUpper() == "PLA")
					{
						double absDensity = settings.GetValue<double>(SettingsKey.filament_density);
						if (absDensity != 1.24)
						{
							Assert.Fail("[filament_density] value should be set to PLA 1.24: " + printer.RelativeFilePath);
						}
					}
				}
			});
		}

		[Test]
		public void MaxFanSpeedOneHundredOrLess()
		{
			ValidateOnAllPrinters((printer, settings, settingsType) =>
			{
				string fanSpeedString = settings.GetValue(SettingsKey.max_fan_speed);
				if (!string.IsNullOrEmpty(fanSpeedString))
				{
					// Must be valid int data
					int maxFanSpeed;
					if (!int.TryParse(fanSpeedString, out maxFanSpeed))
					{
						Assert.Fail("Invalid [max_fan_speed] value (int parse failed): " + printer.RelativeFilePath);
					}

					// Must be less than or equal to 100
					Assert.LessOrEqual(maxFanSpeed, 100, "[max_fan_speed]: " + printer.RelativeFilePath);
				}
			});
		}

		[Test]
		public void NoCurlyBracketsInGcode()
		{
			ValidateOnAllPrinters((printer, settings, settingsType) =>
			{
				// TODO: Why aren't we testing all gcode sections?
				string[] keysToTest = { SettingsKey.start_gcode, SettingsKey.end_gcode };
				foreach (string gcodeKey in keysToTest)
				{
					string gcode = settings.GetValue(gcodeKey);
					if (gcode.Contains("{") || gcode.Contains("}"))
					{
						Assert.Fail(string.Format("[{0}] Curly brackets not allowed: {1}", gcodeKey, printer.RelativeFilePath));
					}
				}
			});
		}

		[Test]
		public void BottomSolidLayersNotZero()
		{
			ValidateOnAllPrinters((printer, settings, settingsType) =>
			{
				string bottomSolidLayers = settings.GetValue(SettingsKey.bottom_solid_layers);
				if (!string.IsNullOrEmpty(bottomSolidLayers))
				{
					if (bottomSolidLayers == "0")
					{
						printer.RuleViolated = true;
						return;
					}

					// Assert.AreEqual("1mm", bottomSolidLayers, "[bottom_solid_layers] must be 1mm: " + printer.RelativeFilePath);
				}
			});
		}

		[Test]
		public void NoFirstLayerBedTempInStartGcode()
		{
			ValidateOnAllPrinters((printer, settings, settingsType) =>
			{
				string startGcode = settings.GetValue(SettingsKey.start_gcode);
				Assert.False(startGcode.Contains(SettingsKey.first_layer_bed_temperature), "[start_gcode] should not contain [first_layer_bed_temperature]" + printer.RelativeFilePath);
			});
		}

		[Test]
		public void FirstLayerHeightLessThanNozzleDiameterXExtrusionMultiplier()
		{
			ValidateOnAllPrinters((printer, settings, settingsType) =>
			{
				if (settings.GetValue(SettingsKey.output_only_first_layer) == "1")
				{
					return;
				}

				float nozzleDiameter = float.Parse(settings.GetValue(SettingsKey.nozzle_diameter));
				float layerHeight = float.Parse(settings.GetValue(SettingsKey.layer_height));

				float firstLayerExtrusionWidth;

				string firstLayerExtrusionWidthString = settings.GetValue(SettingsKey.first_layer_extrusion_width);
				if (!string.IsNullOrEmpty(firstLayerExtrusionWidthString) && firstLayerExtrusionWidthString.Trim() != "0")
				{
					firstLayerExtrusionWidth = ValueOrPercentageOf(firstLayerExtrusionWidthString, nozzleDiameter);
				}
				else
				{
					firstLayerExtrusionWidth = nozzleDiameter;
				}

				string firstLayerHeightString = settings.GetValue(SettingsKey.first_layer_height);
				if (!string.IsNullOrEmpty(firstLayerHeightString))
				{
					float firstLayerHeight = ValueOrPercentageOf(firstLayerHeightString, layerHeight);

					if (firstLayerHeight > firstLayerExtrusionWidth)
					{
						printer.RuleViolated = true;
						return;
					}
				}
			});
		}

		[Test]
		public void LayerHeightLessThanNozzleDiameter()
		{
			ValidateOnAllPrinters((printer, settings, settingsType) =>
			{
				if (settings.GetValue(SettingsKey.output_only_first_layer) == "1")
				{
					return;
				}

				float nozzleDiameter = float.Parse(settings.GetValue(SettingsKey.nozzle_diameter));
				float layerHeight = float.Parse(settings.GetValue(SettingsKey.layer_height));

				double maximumLayerHeight = nozzleDiameter * 85;

				// TODO: Remove once validated and resolved
				if (layerHeight >= maximumLayerHeight)
				{
					printer.RuleViolated = true;
					return;
				}

				Assert.Less(layerHeight, maximumLayerHeight, "[layer_height] must be less than [minimumLayerHeight]: " + printer.RelativeFilePath);
			});
		}

		[Test]
		public void FirstLayerExtrusionWidthGreaterThanNozzleDiameterIfSet()
		{
			ValidateOnAllPrinters((printer, settings, settingsType) =>
			{
				float nozzleDiameter = float.Parse(settings.GetValue(SettingsKey.nozzle_diameter));

				string firstLayerExtrusionWidthString = settings.GetValue(SettingsKey.first_layer_extrusion_width);
				if (!string.IsNullOrEmpty(firstLayerExtrusionWidthString))
				{
					float firstLayerExtrusionWidth = ValueOrPercentageOf(firstLayerExtrusionWidthString, nozzleDiameter);
					if (firstLayerExtrusionWidth == 0)
					{
						// Ignore zeros
						return;
					}

					Assert.GreaterOrEqual(firstLayerExtrusionWidth, nozzleDiameter, "[first_layer_extrusion_width] must be nozzle diameter or greater: " + printer.RelativeFilePath);
				}
			});
		}

		[Test]
		public void SupportMaterialAssignedToExtruderOne()
		{
			ValidateOnAllPrinters((printer, settings, settingsType) =>
			{
				var supportMaterialExtruder = settings.GetValue<int>(SettingsKey.support_material_extruder);
				var extruderCount = settings.GetValue<int>(SettingsKey.extruder_count);
				// the support extruder should be 0 unless you are on a material setting
				if (supportMaterialExtruder <= 0
					|| (settingsType == SettingsType.Material && extruderCount > 1)
					|| (settingsType == SettingsType.Quality && extruderCount > 1))
				{
					// this is a valid printer profile
				}
				else
				{
					// this needs to be fixed
					printer.RuleViolated = true;
				}
			});
		}

		[Test]
		public void SupportInterfaceMaterialAssignedToExtruderOne()
		{
			ValidateOnAllPrinters((printer, settings, settingsType) =>
			{
				var supportMaterialInterfaceExtruder = settings.GetValue<int>(SettingsKey.support_material_interface_extruder);
				var extruderCount = settings.GetValue<int>(SettingsKey.extruder_count);
				// the support extruder should be 0 unless you are on a material setting
				if (supportMaterialInterfaceExtruder <= 0
					|| (settingsType == SettingsType.Material && extruderCount > 1)
					|| (settingsType == SettingsType.Quality && extruderCount > 1))
				{
					// this is a valid printer profile
				}
				else
				{
					// this needs to be fixed
					printer.RuleViolated = true;
				}
			});
		}

		private static float ValueOrPercentageOf(string valueOrPercent, float baseValue)
		{
			if (valueOrPercent.Contains("%"))
			{
				float percentage = float.Parse(valueOrPercent.Replace("%", "")) / 100;
				return baseValue * percentage;
			}
			else
			{
				return float.Parse(valueOrPercent);
			}
		}

		public enum SettingsType
		{
			All,
			Material,
			Quality
		}

		/// <summary>
		/// Calls the given delegate for each printer as well as each quality/material layer, passing in a PrinterConfig object that has
		/// printer settings loaded into a SettingsLayer as well as state about the printer
		/// </summary>
		/// <param name="action">The action to invoke for each printer</param>
		private void ValidateOnAllPrinters(Action<PrinterTestDetails, PrinterSettings, SettingsType> action)
		{
			var ruleViolations = new List<string>();

			foreach (var printer in allPrinters)
			{
				printer.RuleViolated = false;

				var printerSettings = printer.PrinterSettings;
				printerSettings.AutoSave = false;

				// Disable active material/quality overrides
				printerSettings.ActiveMaterialKey = "";
				printerSettings.ActiveQualityKey = "";

				// Validate just the OemLayer
				action(printer, printerSettings, SettingsType.All);

				if (printer.RuleViolated)
				{
					ruleViolations.Add(printer.RelativeFilePath);
				}

				// Validate material layers
				foreach (var layer in printer.PrinterSettings.MaterialLayers)
				{
					printer.RuleViolated = false;

					printerSettings.ActiveMaterialKey = layer.LayerID;

					// Validate the settings with this material layer active
					action(printer, printerSettings, SettingsType.Material);

					if (printer.RuleViolated)
					{
						ruleViolations.Add(printer.RelativeFilePath + " -> " + layer.Name);
					}
				}

				printerSettings.ActiveMaterialKey = "";

				// Validate quality layers
				foreach (var layer in printer.PrinterSettings.QualityLayers)
				{
					printer.RuleViolated = false;

					printerSettings.ActiveQualityKey = layer.LayerID;

					// Validate the settings with this quality layer active
					action(printer, printerSettings, SettingsType.Quality);

					if (printer.RuleViolated)
					{
						ruleViolations.Add(printer.RelativeFilePath + " -> " + layer.Name);
					}
				}
			}

			Assert.IsTrue(
				ruleViolations.Count == 0, /* Use == instead of Assert.AreEqual to better convey failure details */
				string.Format("One or more printers violate this rule: \r\n\r\n{0}\r\n", string.Join("\r\n", ruleViolations.ToArray())));
		}

		private class PrinterTestDetails
		{
			public string PrinterName { get; set; }

			public string Oem { get; set; }

			public string ConfigPath { get; set; }

			public string RelativeFilePath { get; set; }

			public PrinterSettings PrinterSettings { get; set; }

			// HACK: short term hack to support a general purpose test rollup function for cases where multiple config files
			// violate a rule and in the short term we want to report and resolve the issues in batch rather than having a
			// single test failure. Long term the single test failure better communicates the issue and assist with troubleshooting
			// by using  .AreEqual .LessOrEqual, etc. to communicate intent
			public bool RuleViolated { get; set; } = false;
		}
	}
}
