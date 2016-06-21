using MatterHackers.MatterControl;
using NUnit.Framework;
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Globalization;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture, Category("ConfigIni")]
	public class ConfigIniTests
	{
		private static List<PrinterConfig> allPrinters;
		private static string matterControlDirectory = Path.GetFullPath(Path.Combine("..", "..", "..", ".."));
		private static string printerSettingsDirectory = Path.GetFullPath(Path.Combine(matterControlDirectory, "StaticData", "PrinterSettings"));

		static ConfigIniTests()
		{
			allPrinters = (from configIni in new DirectoryInfo(printerSettingsDirectory).GetFiles("config.ini", System.IO.SearchOption.AllDirectories)
						   let oemProfile = new OemProfile(PrinterSettingsLayer.LoadFromIni(configIni.FullName))
						   select new PrinterConfig
						   {
							   PrinterName = configIni.Directory.Name,
							   Oem = configIni.Directory.Parent.Name,
							   ConfigPath = configIni.FullName,
							   ConfigIni = new LayerInfo()
							   {
								   RelativeFilePath = configIni.FullName.Substring(printerSettingsDirectory.Length + 1),

								   // The config.ini layer cascade contains only itself
								   LayerCascade = new PrinterSettings(oemProfile, new PrinterSettingsLayer()),
							   },
							   MatterialLayers = LoadLayers(Path.Combine(configIni.Directory.FullName, "material"), oemProfile),
							   QualityLayers = LoadLayers(Path.Combine(configIni.Directory.FullName, "quality"), oemProfile)
						   }).ToList();
		}

		private static List<LayerInfo> LoadLayers(string layersDirectory, OemProfile oemProfile)
		{
			// The slice presets layer cascade contains the preset layer, with config.ini data as a parent
			return Directory.Exists(layersDirectory) ?
					Directory.GetFiles(layersDirectory, "*.slice").Select(file => new LayerInfo()
					{
						RelativeFilePath = file.Substring(printerSettingsDirectory.Length + 1),
						LayerCascade = new PrinterSettings(new OemProfile(PrinterSettingsLayer.LoadFromIni(file)), oemProfile.OemLayer)
					}).ToList()
					: new List<LayerInfo>();
		}

		[Test]
		public void CsvBedSizeExistsAndHasTwoValues()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				// Bed size is not required in slice files
				if (settings.RelativeFilePath.IndexOf(".slice", StringComparison.OrdinalIgnoreCase) != -1)
				{
					return;
				}

				string bedSize = settings.LayerCascade.GetValue(SettingsKey.bed_size);

				// Must exist in all configs
				Assert.IsNotNullOrEmpty(bedSize, "[bed_size] must exist: " + settings.RelativeFilePath);

				string[] segments = bedSize.Trim().Split(',');

				// Must be a CSV and have two values
				Assert.AreEqual(2, segments.Length, "[bed_size] should have two values separated by a comma: " + settings.RelativeFilePath);
			});
		}

		[Test]
		public void CsvPrintCenterExistsAndHasTwoValues()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				// Printer center is not required in slice files
				if (settings.RelativeFilePath.IndexOf(".slice", StringComparison.OrdinalIgnoreCase) != -1)
				{
					return;
				}

				string printCenter = settings.LayerCascade.GetValue(SettingsKey.print_center);

				// Must exist in all configs
				Assert.IsNotNullOrEmpty(printCenter, "[print_center] must exist: " + settings.RelativeFilePath);

				string[] segments = printCenter.Trim().Split(',');

				// Must be a CSV and have only two values
				Assert.AreEqual(2, segments.Length, "[print_center] should have two values separated by a comma: " + settings.RelativeFilePath);
			});
		}

		[Test]
		public void RetractLengthIsLessThanTwenty()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				string retractLengthString = settings.LayerCascade.GetValue("retract_length");
				if (!string.IsNullOrEmpty(retractLengthString))
				{
					float retractLength;
					if (!float.TryParse(retractLengthString, out retractLength))
					{
						Assert.Fail("Invalid [retract_length] value (float parse failed): " + settings.RelativeFilePath);
					}

					Assert.Less(retractLength, 20, "[retract_length]: " + settings.RelativeFilePath);
				}
			});
		}

		[Test]
		public void ExtruderCountIsGreaterThanZero()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				string extruderCountString = settings.LayerCascade.GetValue("extruder_count");
				if (!string.IsNullOrEmpty(extruderCountString))
				{
					int extruderCount;
					if (!int.TryParse(extruderCountString, out extruderCount))
					{
						Assert.Fail("Invalid [extruder_count] value (int parse failed): " + settings.RelativeFilePath);
					}

					// Must be greater than zero
					Assert.Greater(extruderCount, 0, "[extruder_count]: " + settings.RelativeFilePath);
				}
			});
		}

		[Test]
		public void MinFanSpeedOneHundredOrLess()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				string fanSpeedString = settings.LayerCascade.GetValue("min_fan_speed");
				if (!string.IsNullOrEmpty(fanSpeedString))
				{
					// Must be valid int data
					int minFanSpeed;
					if (!int.TryParse(fanSpeedString, out minFanSpeed))
					{
						Assert.Fail("Invalid [min_fan_speed] value (int parse failed): " + settings.RelativeFilePath);
					}

					// Must be less than or equal to 100
					Assert.LessOrEqual(minFanSpeed, 100, "[min_fan_speed]: " + settings.RelativeFilePath);
				}
			});
		}

		[Test]
		public void MaxFanSpeedOneHundredOrLess()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				string fanSpeedString = settings.LayerCascade.GetValue("max_fan_speed");
				if (!string.IsNullOrEmpty(fanSpeedString))
				{
					// Must be valid int data
					int maxFanSpeed;
					if (!int.TryParse(fanSpeedString, out maxFanSpeed))
					{
						Assert.Fail("Invalid [max_fan_speed] value (int parse failed): " + settings.RelativeFilePath);
					}

					// Must be less than or equal to 100
					Assert.LessOrEqual(maxFanSpeed, 100, "[max_fan_speed]: " + settings.RelativeFilePath);
				}
			});
		}

		[Test]
		public void NoCurlyBracketsInGcode()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				// TODO: Why aren't we testing all gcode sections?
				string[] keysToTest = { "start_gcode", "end_gcode" };
				foreach (string gcodeKey in keysToTest)
				{
					string gcode = settings.LayerCascade.GetValue(gcodeKey);
					if (gcode.Contains("{") || gcode.Contains("}") )
					{
						Assert.Fail(string.Format("[{0}] Curly brackets not allowed: {1}", gcodeKey, settings.RelativeFilePath));
					}
				}
			});
		}

		[Test, Category("FixNeeded")]
		public void BottomSolidLayersEqualsOneMM()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				string bottomSolidLayers = settings.LayerCascade.GetValue("bottom_solid_layers");
				if (!string.IsNullOrEmpty(bottomSolidLayers))
				{
					if (bottomSolidLayers != "1mm")
					{
						printer.RuleViolated = true;
						return;
					}

					Assert.AreEqual("1mm", bottomSolidLayers, "[bottom_solid_layers] must be 1mm: " + settings.RelativeFilePath);
				}
			});
		}

		[Test]
		public void NoFirstLayerTempInStartGcode()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				string startGcode = settings.LayerCascade.GetValue("start_gcode");
				Assert.False(startGcode.Contains("first_layer_temperature"), "[start_gcode] should not contain [first_layer_temperature]" + settings.RelativeFilePath);
			});
		}

		[Test]
		public void NoFirstLayerBedTempInStartGcode()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				string startGcode = settings.LayerCascade.GetValue("start_gcode");
				Assert.False(startGcode.Contains("first_layer_bed_temperature"), "[start_gcode] should not contain [first_layer_bed_temperature]" + settings.RelativeFilePath);
			});
		}

		[Test, Category("FixNeeded")]
		public void FirstLayerHeightLessThanNozzleDiameterXExtrusionMultiplier()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				if (settings.LayerCascade.GetValue("output_only_first_layer") == "1")
				{
					return;
				}

				float nozzleDiameter = float.Parse(settings.LayerCascade.GetValue(SettingsKey.nozzle_diameter));
				float layerHeight = float.Parse(settings.LayerCascade.GetValue(SettingsKey.layer_height));


				float firstLayerExtrusionWidth;

				string firstLayerExtrusionWidthString = settings.LayerCascade.GetValue("first_layer_extrusion_width");
				if (!string.IsNullOrEmpty(firstLayerExtrusionWidthString) && firstLayerExtrusionWidthString.Trim() != "0")
				{
					firstLayerExtrusionWidth = ValueOrPercentageOf(firstLayerExtrusionWidthString, nozzleDiameter);
				}
				else
				{
					firstLayerExtrusionWidth = nozzleDiameter;
				}

				string firstLayerHeightString = settings.LayerCascade.GetValue(SettingsKey.first_layer_height);
				if (!string.IsNullOrEmpty(firstLayerHeightString))
				{
					float firstLayerHeight = ValueOrPercentageOf(firstLayerHeightString, layerHeight);

					double minimumLayerHeight = firstLayerExtrusionWidth * 0.85;

					// TODO: Remove once validated and resolved
					if (firstLayerHeight >= minimumLayerHeight)
					{
						printer.RuleViolated = true;
						return;
					}

					Assert.Less(firstLayerHeight, minimumLayerHeight, "[first_layer_height] must be less than [firstLayerExtrusionWidth]: " + settings.RelativeFilePath);
				}
				
			});
		}

		[Test, Category("FixNeeded")]
		public void LayerHeightLessThanNozzleDiameter()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				if (settings.LayerCascade.GetValue("output_only_first_layer") == "1")
				{
					return;
				}

				float nozzleDiameter = float.Parse(settings.LayerCascade.GetValue(SettingsKey.nozzle_diameter));
				float layerHeight = float.Parse(settings.LayerCascade.GetValue(SettingsKey.layer_height));

				double minimumLayerHeight = nozzleDiameter * 0.85;

				// TODO: Remove once validated and resolved
				if (layerHeight >= minimumLayerHeight)
				{
					printer.RuleViolated = true;
					return;
				}

				Assert.Less(layerHeight, minimumLayerHeight, "[layer_height] must be less than [minimumLayerHeight]: " + settings.RelativeFilePath);
			});
		}

		[Test]
		public void FirstLayerExtrusionWidthGreaterThanNozzleDiameterIfSet()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				float nozzleDiameter = float.Parse(settings.LayerCascade.GetValue(SettingsKey.nozzle_diameter));

				string firstLayerExtrusionWidthString = settings.LayerCascade.GetValue("first_layer_extrusion_width");
				if (!string.IsNullOrEmpty(firstLayerExtrusionWidthString))
				{
					float firstLayerExtrusionWidth = ValueOrPercentageOf(firstLayerExtrusionWidthString, nozzleDiameter);
					if (firstLayerExtrusionWidth == 0)
					{
						// Ignore zeros
						return;
					}

					Assert.GreaterOrEqual(firstLayerExtrusionWidth, nozzleDiameter, "[first_layer_extrusion_width] must be nozzle diameter or greater: " + settings.RelativeFilePath);
				}
			});
		}

		[Test]
		public void SupportMaterialAssignedToExtruderOne()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				string supportMaterialExtruder = settings.LayerCascade.GetValue("support_material_extruder");
				if (!string.IsNullOrEmpty(supportMaterialExtruder) && printer.Oem != "Esagono")
				{
					Assert.AreEqual("1", supportMaterialExtruder, "[support_material_extruder] must be assigned to extruder 1: " + settings.RelativeFilePath);
				}
			});
		}

		[Test]
		public void SupportInterfaceMaterialAssignedToExtruderOne()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				// Make exception for extruder assignment on 3D Stuffmaker slice files
				if (printer.Oem == "3D Stuffmaker" && settings.RelativeFilePath.IndexOf(".slice", StringComparison.OrdinalIgnoreCase) != -1)
				{
					return;
				}

				string supportMaterialInterfaceExtruder = settings.LayerCascade.GetValue("support_material_interface_extruder");
				if (!string.IsNullOrEmpty(supportMaterialInterfaceExtruder) && printer.Oem != "Esagono")
				{
					Assert.AreEqual("1", supportMaterialInterfaceExtruder, "[support_material_interface_extruder] must be assigned to extruder 1: " + settings.RelativeFilePath);
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

		/// <summary>
		/// Calls the given delegate for each known printer, passing in a PrinterConfig object that has 
		/// config.ini loaded into a SettingsLayer as well as state about the printer
		/// </summary>
		/// <param name="action">The action to invoke for each printer</param>
		private void ValidateOnAllPrinters(Action<PrinterConfig, LayerInfo> action)
		{
			var ruleViolations = new List<string>();

			foreach (var printer in allPrinters)
			{
				printer.RuleViolated = false;

				action(printer, printer.ConfigIni);

				if (printer.RuleViolated)
				{
					ruleViolations.Add(printer.ConfigIni.RelativeFilePath);
				}

				foreach (var layer in printer.MatterialLayers)
				{
					printer.RuleViolated = false;

					action(printer, layer);

					if (printer.RuleViolated)
					{
						ruleViolations.Add(layer.RelativeFilePath);
					}
				}

				foreach (var layer in printer.QualityLayers)
				{
					printer.RuleViolated = false;

					action(printer, layer);

					if (printer.RuleViolated)
					{
						ruleViolations.Add(layer.RelativeFilePath);
					}
				}
			}

			Assert.IsTrue(
				ruleViolations.Count == 0, /* Use == instead of Assert.AreEqual to better convey failure details */
				string.Format("One or more printers violate this rule: \r\n\r\n{0}\r\n", string.Join("\r\n", ruleViolations.ToArray())));
		}

		private class PrinterConfig
		{
			public string PrinterName { get; set; }
			public string Oem { get; set; }
			public string ConfigPath { get; set; }
			public LayerInfo ConfigIni { get; set; }

			// HACK: short term hack to support a general purpose test rollup function for cases where multiple config files 
			// violate a rule and in the short term we want to report and resolve the issues in batch rather than having a 
			// single test failure. Long term the single test failure better communicates the issue and assist with troubleshooting
			// by using  .AreEqual .LessOrEqual, etc. to communicate intent
			public bool RuleViolated { get; set; } = false;
			public List<LayerInfo> MatterialLayers { get; internal set; }
			public List<LayerInfo> QualityLayers { get; internal set; }
		}

		private class LayerInfo
		{
			public string RelativeFilePath { get; set; }
			public PrinterSettings LayerCascade { get; set; }
		}
	}
}
