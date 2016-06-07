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
						   select new PrinterConfig
						   {
							   PrinterName = configIni.Directory.Name,
							   Oem = configIni.Directory.Parent.Name,
							   ConfigPath = configIni.FullName,
							   ConfigIni = new LayerInfo()
							   {
								   RelativePath = configIni.FullName.Substring(printerSettingsDirectory.Length + 1),
								   Settings = SettingsLayer.LoadFromIni(configIni.FullName),
							   },
							   MatterialLayers = LoadLayers(Path.Combine(configIni.Directory.FullName, "material")),
							   QualityLayers = LoadLayers(Path.Combine(configIni.Directory.FullName, "quality"))
						   }).ToList();
		}

		private static List<LayerInfo> LoadLayers(string layersDirectory)
		{
			return Directory.Exists(layersDirectory) ?
					Directory.GetFiles(layersDirectory, "*.slice").Select(file => new LayerInfo()
					{
						RelativePath = file.Substring(printerSettingsDirectory.Length + 1),
						Settings = SettingsLayer.LoadFromIni(file)
					}).ToList()
					: new List<LayerInfo>();
		}

		[Test]
		public void CsvBedSizeExistsAndHasTwoValues()
		{
			ValidateOnAllPrinters((printer, layer) =>
			{
				// Bed size is not required in slice files
				if (layer.RelativePath.IndexOf(".slice", StringComparison.OrdinalIgnoreCase) != -1)
				{
					return;
				}

				string bedSize = layer.Settings.ValueOrDefault("bed_size");

				// Must exist in all configs
				Assert.IsNotNullOrEmpty(bedSize, "[bed_size] must exist: " + layer.RelativePath);

				string[] segments = bedSize.Trim().Split(',');

				// Must be a CSV and have two values
				Assert.AreEqual(2, segments.Length, "[bed_size] should have two values separated by a comma: " + layer.RelativePath);
			});
		}

		[Test]
		public void CsvPrintCenterExistsAndHasTwoValues()
		{
			ValidateOnAllPrinters((printer, layer) =>
			{
				// Printer center is not required in slice files
				if (layer.RelativePath.IndexOf(".slice", StringComparison.OrdinalIgnoreCase) != -1)
				{
					return;
				}

				string printCenter = layer.Settings.ValueOrDefault("print_center");

				// Must exist in all configs
				Assert.IsNotNullOrEmpty(printCenter, "[print_center] must exist: " + layer.RelativePath);

				string[] segments = printCenter.Trim().Split(',');

				// Must be a CSV and have only two values
				Assert.AreEqual(2, segments.Length, "[print_center] should have two values separated by a comma: " + layer.RelativePath);
			});
		}

		[Test]
		public void RetractLengthIsLessThanTwenty()
		{
			ValidateOnAllPrinters((printer, layer) =>
			{
				string retractLengthString = layer.Settings.ValueOrDefault("retract_length");
				if (!string.IsNullOrEmpty(retractLengthString))
				{
					float retractLength;
					if (!float.TryParse(retractLengthString, out retractLength))
					{
						Assert.Fail("Invalid [retract_length] value (float parse failed): " + layer.RelativePath);
					}

					Assert.Less(retractLength, 20, "[retract_length]: " + layer.RelativePath);
				}
			});
		}

		[Test]
		public void ExtruderCountIsGreaterThanZero()
		{
			ValidateOnAllPrinters((printer, layer) =>
			{
				string extruderCountString = layer.Settings.ValueOrDefault("extruder_count");
				if (!string.IsNullOrEmpty(extruderCountString))
				{
					int extruderCount;
					if (!int.TryParse(extruderCountString, out extruderCount))
					{
						Assert.Fail("Invalid [extruder_count] value (int parse failed): " + layer.RelativePath);
					}

					// Must be greater than zero
					Assert.Greater(extruderCount, 0, "[extruder_count]: " + layer.RelativePath);
				}
			});
		}

		[Test]
		public void MinFanSpeedOneHundredOrLess()
		{
			ValidateOnAllPrinters((printer, layer) =>
			{
				string fanSpeedString = layer.Settings.ValueOrDefault("min_fan_speed");
				if (!string.IsNullOrEmpty(fanSpeedString))
				{
					// Must be valid int data
					int minFanSpeed;
					if (!int.TryParse(fanSpeedString, out minFanSpeed))
					{
						Assert.Fail("Invalid [min_fan_speed] value (int parse failed): " + layer.RelativePath);
					}

					// Must be less than or equal to 100
					Assert.LessOrEqual(minFanSpeed, 100, "[min_fan_speed]: " + layer.RelativePath);
				}
			});
		}

		[Test]
		public void MaxFanSpeedOneHundredOrLess()
		{
			ValidateOnAllPrinters((printer, layer) =>
			{
				string fanSpeedString = layer.Settings.ValueOrDefault("max_fan_speed");
				if (!string.IsNullOrEmpty(fanSpeedString))
				{
					// Must be valid int data
					int maxFanSpeed;
					if (!int.TryParse(fanSpeedString, out maxFanSpeed))
					{
						Assert.Fail("Invalid [max_fan_speed] value (int parse failed): " + layer.RelativePath);
					}

					// Must be less than or equal to 100
					Assert.LessOrEqual(maxFanSpeed, 100, "[max_fan_speed]: " + layer.RelativePath);
				}
			});
		}

		[Test]
		public void NoCurlyBracketsInGcode()
		{
			ValidateOnAllPrinters((printer, layer) =>
			{
				// TODO: Why aren't we testing all gcode sections?
				string[] keysToTest = { "start_gcode", "end_gcode" };
				foreach (string gcodeKey in keysToTest)
				{
					string gcode = layer.Settings.ValueOrDefault(gcodeKey);
					if (gcode.Contains("{") || gcode.Contains("}") )
					{
						Assert.Fail(string.Format("[{0}] Curly brackets not allowed: {1}", gcodeKey, layer.RelativePath));
					}
				}
			});
		}

		[Test]
		public void BottomSolidLayersEqualsOneMM()
		{
			ValidateOnAllPrinters((printer, layer) =>
			{
				string bottomSolidLayers = layer.Settings.ValueOrDefault("bottom_solid_layers");
				if (!string.IsNullOrEmpty(bottomSolidLayers))
				{
					if (bottomSolidLayers != "1mm")
					{
						printer.RuleViolated = true;
						return;
					}

					Assert.AreEqual("1mm", bottomSolidLayers, "[bottom_solid_layers] must be 1mm: " + layer.RelativePath);
				}
			});
		}

		[Test]
		public void NoFirstLayerTempInStartGcode()
		{
			ValidateOnAllPrinters((printer, layer) =>
			{
				string startGcode = layer.Settings.ValueOrDefault("start_gcode");
				Assert.False(startGcode.Contains("first_layer_temperature"), "[start_gcode] should not contain [first_layer_temperature]" + layer.RelativePath);
			});
		}

		[Test]
		public void NoFirstLayerBedTempInStartGcode()
		{
			ValidateOnAllPrinters((printer, layer) =>
			{
				string startGcode = layer.Settings.ValueOrDefault("start_gcode");
				Assert.False(startGcode.Contains("first_layer_bed_temperature"), "[start_gcode] should not contain [first_layer_bed_temperature]" + layer.RelativePath);
			});
		}

		[Test]
		public void FirstLayerHeightLessThanNozzleDiameterXExtrusionMultiplier()
		{
			ValidateOnAllPrinters((printer, layer) =>
			{
				if (layer.Settings.ValueOrDefault("output_only_first_layer") == "1")
				{
					return;
				}

				float nozzleDiameter = float.Parse(layer.Settings.ValueOrDefault("nozzle_diameter"));
				float layerHeight = float.Parse(layer.Settings.ValueOrDefault("layer_height"));


				float firstLayerExtrusionWidth;

				string firstLayerExtrusionWidthString = layer.Settings.ValueOrDefault("first_layer_extrusion_width");
				if (!string.IsNullOrEmpty(firstLayerExtrusionWidthString) && firstLayerExtrusionWidthString.Trim() != "0")
				{
					firstLayerExtrusionWidth = ValueOrPercentageOf(firstLayerExtrusionWidthString, nozzleDiameter);
				}
				else
				{
					firstLayerExtrusionWidth = nozzleDiameter;
				}

				string firstLayerHeightString = layer.Settings.ValueOrDefault("first_layer_height");
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

					Assert.Less(firstLayerHeight, minimumLayerHeight, "[first_layer_height] must be less than [firstLayerExtrusionWidth]: " + layer.RelativePath);
				}
				
			});
		}

		[Test]
		public void LayerHeightLessThanNozzleDiameter()
		{
			ValidateOnAllPrinters((printer, layer) =>
			{
				if (layer.Settings.ValueOrDefault("output_only_first_layer") == "1")
				{
					return;
				}

				float nozzleDiameter = float.Parse(layer.Settings.ValueOrDefault("nozzle_diameter"));
				float layerHeight = float.Parse(layer.Settings.ValueOrDefault("layer_height"));

				double minimumLayerHeight = nozzleDiameter * 0.85;

				// TODO: Remove once validated and resolved
				if (layerHeight >= minimumLayerHeight)
				{
					printer.RuleViolated = true;
					return;
				}

				Assert.Less(layerHeight, minimumLayerHeight, "[layer_height] must be less than [minimumLayerHeight]: " + layer.RelativePath);
			});
		}

		[Test]
		public void FirstLayerExtrusionWidthGreaterThanNozzleDiameterIfSet()
		{
			ValidateOnAllPrinters((printer, layer) =>
			{
				float nozzleDiameter = float.Parse(layer.Settings.ValueOrDefault("nozzle_diameter"));

				string firstLayerExtrusionWidthString = layer.Settings.ValueOrDefault("first_layer_extrusion_width");
				if (!string.IsNullOrEmpty(firstLayerExtrusionWidthString))
				{
					float firstLayerExtrusionWidth = ValueOrPercentageOf(firstLayerExtrusionWidthString, nozzleDiameter);
					if (firstLayerExtrusionWidth == 0)
					{
						// Ignore zeros
						return;
					}

					Assert.GreaterOrEqual(firstLayerExtrusionWidth, nozzleDiameter, "[first_layer_extrusion_width] must be nozzle diameter or greater: " + layer.RelativePath);
				}
			});
		}

		[Test]
		public void SupportMaterialAssignedToExtruderOne()
		{
			ValidateOnAllPrinters((printer, layer) =>
			{
				string supportMaterialExtruder = layer.Settings.ValueOrDefault("support_material_extruder");
				if (!string.IsNullOrEmpty(supportMaterialExtruder) && printer.Oem != "Esagono")
				{
					Assert.AreEqual("1", supportMaterialExtruder, "[support_material_extruder] must be assigned to extruder 1: " + layer.RelativePath);
				}
			});
		}

		[Test]
		public void SupportInterfaceMaterialAssignedToExtruderOne()
		{
			ValidateOnAllPrinters((printer, layer) =>
			{
				string supportMaterialInterfaceExtruder = layer.Settings.ValueOrDefault("support_material_interface_extruder");
				if (!string.IsNullOrEmpty(supportMaterialInterfaceExtruder) && printer.Oem != "Esagono")
				{
					Assert.AreEqual("1", supportMaterialInterfaceExtruder, "[support_material_interface_extruder] must be assigned to extruder 1: " + layer.RelativePath);
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
					ruleViolations.Add(printer.ConfigIni.RelativePath);
				}

				foreach (var layer in printer.MatterialLayers)
				{
					printer.RuleViolated = false;

					action(printer, layer);

					if (printer.RuleViolated)
					{
						ruleViolations.Add(layer.RelativePath);
					}
				}

				foreach (var layer in printer.QualityLayers)
				{
					printer.RuleViolated = false;

					action(printer, layer);

					if (printer.RuleViolated)
					{
						ruleViolations.Add(layer.RelativePath);
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
			public string RelativePath { get; set; }
			public SettingsLayer Settings { get; set; }
		}
	}
}
