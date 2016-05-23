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
							   RelativeConfigPath = configIni.FullName.Substring(printerSettingsDirectory.Length + 1),
							   SettingsLayer = SettingsLayer.LoadFromIni(configIni.FullName)
						   }).ToList();
		}

		[Test]
		public void CsvBedSizeExistsAndHasTwoValues()
		{
			ValidateOnAllPrinters(printer =>
			{
				string bedSize = printer.SettingsLayer.ValueOrDefault("bed_size");

				// Must exist in all configs
				Assert.IsNotNullOrEmpty(bedSize, "[bed_size] must exist: " + printer.RelativeConfigPath);

				string[] segments = bedSize.Trim().Split(',');

				// Must be a CSV and have two values
				Assert.AreEqual(2, segments.Length, "[bed_size] should have two values separated by a comma: " + printer.RelativeConfigPath);
			});
		}

		[Test]
		public void CsvPrintCenterExistsAndHasTwoValues()
		{
			ValidateOnAllPrinters(printer =>
			{
				string printCenter = printer.SettingsLayer.ValueOrDefault("print_center");

				// Must exist in all configs
				Assert.IsNotNullOrEmpty(printCenter, "[print_center] must exist: " + printer.RelativeConfigPath);

				string[] segments = printCenter.Trim().Split(',');

				// Must be a CSV and have only two values
				Assert.AreEqual(2, segments.Length, "[print_center] should have two values separated by a comma: " + printer.RelativeConfigPath);
			});
		}

		[Test]
		public void RetractLengthIsLessThanTwenty()
		{
			ValidateOnAllPrinters(printer =>
			{
				string retractLengthString = printer.SettingsLayer.ValueOrDefault("retract_length");
				if (!string.IsNullOrEmpty(retractLengthString))
				{
					float retractLength;
					if (!float.TryParse(retractLengthString, out retractLength))
					{
						Assert.Fail("Invalid [retract_length] value (float parse failed): " + printer.RelativeConfigPath);
					}

					Assert.Less(retractLength, 20, "[retract_length]: " + printer.RelativeConfigPath);
				}
			});
		}

		[Test]
		public void ExtruderCountIsGreaterThanZero()
		{
			ValidateOnAllPrinters(printer =>
			{
				string extruderCountString = printer.SettingsLayer.ValueOrDefault("extruder_count");

				// TODO: Should extruder count be required as originally expected?
				if (string.IsNullOrEmpty(extruderCountString))
				{
					Console.WriteLine("extruder_count missing: " + printer.RelativeConfigPath);
					return;
				}

				// Must exist in all configs
				Assert.IsNotNullOrEmpty(extruderCountString, "[extruder_count] must exist: " + printer.RelativeConfigPath);

				int extruderCount;
				if (!int.TryParse(extruderCountString, out extruderCount))
				{
					Assert.Fail("Invalid [extruder_count] value (int parse failed): " + printer.RelativeConfigPath);
				}

				// Must be greater than zero
				Assert.Greater(extruderCount, 0, "[extruder_count]: " + printer.RelativeConfigPath);
			});
		}

		[Test]
		public void MinFanSpeedOneHundredOrLess()
		{
			ValidateOnAllPrinters(printer =>
			{
				string fanSpeedString = printer.SettingsLayer.ValueOrDefault("min_fan_speed");
				if (!string.IsNullOrEmpty(fanSpeedString))
				{
					// Must be valid int data
					int minFanSpeed;
					if (!int.TryParse(fanSpeedString, out minFanSpeed))
					{
						Assert.Fail("Invalid [min_fan_speed] value (int parse failed): " + printer.RelativeConfigPath);
					}

					// Must be less than or equal to 100
					Assert.LessOrEqual(minFanSpeed, 100, "[min_fan_speed]: " + printer.RelativeConfigPath);
				}
			});
		}

		[Test]
		public void MaxFanSpeedOneHundredOrLess()
		{
			ValidateOnAllPrinters(printer =>
			{
				string fanSpeedString = printer.SettingsLayer.ValueOrDefault("max_fan_speed");
				if (!string.IsNullOrEmpty(fanSpeedString))
				{
					// Must be valid int data
					int maxFanSpeed;
					if (!int.TryParse(fanSpeedString, out maxFanSpeed))
					{
						Assert.Fail("Invalid [max_fan_speed] value (int parse failed): " + printer.RelativeConfigPath);
					}

					// Must be less than or equal to 100
					Assert.LessOrEqual(maxFanSpeed, 100, "[max_fan_speed]: " + printer.RelativeConfigPath);
				}
			});
		}

		[Test]
		public void NoCurlyBracketsInGcode()
		{
			ValidateOnAllPrinters(printer =>
			{
				// TODO: Why aren't we testing all gcode sections?
				string[] keysToTest = { "start_gcode", "end_gcode" };
				foreach (string gcodeKey in keysToTest)
				{
					string gcode = printer.SettingsLayer.ValueOrDefault(gcodeKey);
					if (gcode.Contains("{") || gcode.Contains("}") )
					{
						Assert.Fail(string.Format("[{0}] Curly brackets not allowed: {1}", gcodeKey, printer.RelativeConfigPath));
					}
				}
			});
		}

		[Test]
		public void BottomSolidLayersEqualsOneMM()
		{
			ValidateOnAllPrinters(printer =>
			{
				string bottomSolidLayers = printer.SettingsLayer.ValueOrDefault("bottom_solid_layers");

				// TODO: Remove once validated and resolved
				if (bottomSolidLayers != "1mm")
				{
					printer.RuleViolated = true;
					return;
				}

				Assert.AreEqual("1mm", bottomSolidLayers, "[bottom_solid_layers] must be 1mm: " + printer.RelativeConfigPath);
			});
		}

		[Test]
		public void NoFirstLayerTempInStartGcode()
		{
			ValidateOnAllPrinters(printer =>
			{
				string startGcode = printer.SettingsLayer.ValueOrDefault("start_gcode");
				Assert.False(startGcode.Contains("first_layer_temperature"), "[start_gcode] should not contain [first_layer_temperature]" + printer.RelativeConfigPath);
			});
		}

		[Test]
		public void NoFirstLayerBedTempInStartGcode()
		{
			ValidateOnAllPrinters(printer =>
			{
				string startGcode = printer.SettingsLayer.ValueOrDefault("start_gcode");
				Assert.False(startGcode.Contains("first_layer_bed_temperature"), "[start_gcode] should not contain [first_layer_bed_temperature]" + printer.RelativeConfigPath);
			});
		}

		[Test]
		public void FirstLayerHeightLessThanNozzleDiameter()
		{
			ValidateOnAllPrinters(printer =>
			{
				float nozzleDiameter = float.Parse(printer.SettingsLayer.ValueOrDefault("nozzle_diameter"));
				float layerHeight = float.Parse(printer.SettingsLayer.ValueOrDefault("layer_height"));

				string firstLayerHeightString = printer.SettingsLayer.ValueOrDefault("first_layer_height");
				if (!string.IsNullOrEmpty(firstLayerHeightString))
				{
					float firstLayerHeight = ValueOrPercentageOf(firstLayerHeightString, layerHeight);

					// TODO: Remove once validated and resolved
					if (firstLayerHeight >= nozzleDiameter)
					{
						printer.RuleViolated = true;
						return;
					}

					Assert.Less(firstLayerHeight, nozzleDiameter, "[first_layer_height] must be less than [nozzle_diameter]: " + printer.RelativeConfigPath);
				}
			});
		}

		[Test]
		public void LayerHeightLessThanNozzleDiameter()
		{
			ValidateOnAllPrinters(printer =>
			{
				float nozzleDiameter = float.Parse(printer.SettingsLayer.ValueOrDefault("nozzle_diameter"));
				float layerHeight = float.Parse(printer.SettingsLayer.ValueOrDefault("layer_height"));

				// TODO: Remove once validated and resolved
				if (layerHeight >= nozzleDiameter)
				{
					printer.RuleViolated = true;
					return;
				}

				Assert.Less(layerHeight, nozzleDiameter, "[layer_height] must be less than [nozzle_diameter]: " + printer.RelativeConfigPath);
			});
		}

		// TODO: Requires review
		[Test]
		public void LayerHeightAcceptable()
		{
			ValidateOnAllPrinters(printer =>
			{
				float nozzleDiameter = float.Parse(printer.SettingsLayer.ValueOrDefault("nozzle_diameter"));

				string firstLayerExtrusionWidthString = printer.SettingsLayer.ValueOrDefault("first_layer_extrusion_width");
				if (!string.IsNullOrEmpty(firstLayerExtrusionWidthString))
				{
					float firstLayerExtrusionWidth = ValueOrPercentageOf(firstLayerExtrusionWidthString, nozzleDiameter);

					// TODO: Why are we finding the product of the extrusion width and nozzleDiameter?
					float firstLayerExtrusionWidthToTest = firstLayerExtrusionWidth * nozzleDiameter;
					float firstLayerExtrusionWidthThreshold = nozzleDiameter * 4;

					if (firstLayerExtrusionWidthToTest >= firstLayerExtrusionWidthThreshold ||
						firstLayerExtrusionWidthToTest <= 0 )
					{
						if (firstLayerExtrusionWidthToTest >= firstLayerExtrusionWidthThreshold)
						{
							Console.WriteLine("Extrusion width greater than threshold: " + printer.RelativeConfigPath);
						}
						else if (firstLayerExtrusionWidthToTest <= 0)
						{
							Console.WriteLine("Extrusion width <= 0: " + printer.RelativeConfigPath);
						}

						printer.RuleViolated = true;
						return;
					}

					Assert.Less(firstLayerExtrusionWidthToTest, firstLayerExtrusionWidthThreshold, "[first_layer_extrusion_width] greater than acceptable value: " + printer.RelativeConfigPath);

					// TODO: We're not validating first_layer_extrusion_width as we have the product of nozzleDiameter and firstLayerExtrusionWidth. Seems confusing
					Assert.Greater(firstLayerExtrusionWidthToTest, 0, "First layer extrusion width cannot be zero: " + printer.RelativeConfigPath);
				}
			});
		}

		[Test]
		public void FirstLayerExtrusionWidthGreaterThanZero()
		{
			ValidateOnAllPrinters(printer =>
			{
				float nozzleDiameter = float.Parse(printer.SettingsLayer.ValueOrDefault("nozzle_diameter"));

				string firstLayerExtrusionWidthString = printer.SettingsLayer.ValueOrDefault("first_layer_extrusion_width");
				if (!string.IsNullOrEmpty(firstLayerExtrusionWidthString))
				{
					float firstLayerExtrusionWidth = ValueOrPercentageOf(firstLayerExtrusionWidthString, nozzleDiameter);

					// TODO: Remove once validated and resolved
					if (firstLayerExtrusionWidth <= 0)
					{
						printer.RuleViolated = true;
						return;
					}

					Assert.Greater(firstLayerExtrusionWidth, 0, "[first_layer_extrusion_width] must be greater than zero: " + printer.RelativeConfigPath);
				}
			});
		}

		[Test]
		public void SupportMaterialAssignedToExtruderOne()
		{
			ValidateOnAllPrinters(printer =>
			{
				string supportMaterialExtruder = printer.SettingsLayer.ValueOrDefault("support_material_extruder");
				if (!string.IsNullOrEmpty(supportMaterialExtruder) && printer.Oem != "Esagono")
				{
					Assert.AreEqual("1", supportMaterialExtruder, "[support_material_extruder] must be assigned to extruder 1: " + printer.RelativeConfigPath);
				}
			});
		}

		[Test]
		public void SupportInterfaceMaterialAssignedToExtruderOne()
		{
			ValidateOnAllPrinters(printer =>
			{
				string supportMaterialInterfaceExtruder = printer.SettingsLayer.ValueOrDefault("support_material_interface_extruder");
				if (!string.IsNullOrEmpty(supportMaterialInterfaceExtruder) && printer.Oem != "Esagono")
				{
					Assert.AreEqual("1", supportMaterialInterfaceExtruder, "[support_material_interface_extruder] must be assigned to extruder 1");
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
		private void ValidateOnAllPrinters(Action<PrinterConfig> action)
		{
			var ruleViolations = new List<string>();

			foreach (var printer in allPrinters)
			{
				printer.RuleViolated = false;
				action(printer);

				if (printer.RuleViolated)
				{
					ruleViolations.Add(printer.RelativeConfigPath);
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
			public string RelativeConfigPath { get; set; }
			public SettingsLayer SettingsLayer { get; set; }

			// HACK: short term hack to support a general purpose test rollup function for cases where multiple config files 
			// violate a rule and in the short term we want to report and resolve the issues in batch rather than having a 
			// single test failure. Long term the single test failure better communicates the issue and assist with troubleshooting
			// by using  .AreEqual .LessOrEqual, etc. to communicate intent
			public bool RuleViolated { get; set; } = false;
		}
	}
}
