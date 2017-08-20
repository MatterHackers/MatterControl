using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Tests.Automation;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture, Category("OemProfiles")]
	public class OemProfileTests
	{
		private static List<PrinterConfig> allPrinters;
		private static string printerSettingsDirectory = TestContext.CurrentContext.ResolveProjectPath(4, "StaticData", "Profiles");

		static OemProfileTests()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			allPrinters = (from printerFile in new DirectoryInfo(printerSettingsDirectory).GetFiles("*.printer", SearchOption.AllDirectories)
						   select new PrinterConfig
						   {
							   PrinterName = printerFile.Name,
							   Oem = printerFile.Directory.Name,
							   ConfigPath = printerFile.FullName,
							   RelativeFilePath = printerFile.FullName.Substring(printerSettingsDirectory.Length + 1),
							   PrinterSettings = PrinterSettings.LoadFile(printerFile.FullName)
						   }).ToList();
		}

		[Test, RunInApplicationDomain]
		public void LayerGCodeHasExpectedValue()
		{
			// Verifies "layer_gcode" is expected value: "; LAYER:[layer_num]"
			ValidateOnAllPrinters((printer, settings) =>
			{
				if (settings.GetValue(SettingsKey.layer_gcode) != "; LAYER:[layer_num]")
				{
					printer.RuleViolated = true;

					/* Fix existing invalid items...
					string layerValue;
					if (settings.OemLayer.TryGetValue(SettingsKey.layer_gcode, out layerValue) && layerValue == "")
					{
						settings.OemLayer.Remove(SettingsKey.layer_gcode);
					}

					if (settings.QualityLayer?.TryGetValue(SettingsKey.layer_gcode, out layerValue) == true && layerValue == "")
					{
						settings.QualityLayer.Remove(SettingsKey.layer_gcode);
					}

					if (settings.MaterialLayer?.TryGetValue(SettingsKey.layer_gcode, out layerValue) == true && layerValue == "")
					{
						settings.MaterialLayer.Remove(SettingsKey.layer_gcode);
					}

					// Reset to default values
					settings.UserLayer.Remove(SettingsKey.active_quality_key);
					settings.MaterialSettingsKeys = new List<string>();
					settings.StagedUserSettings = new PrinterSettingsLayer();

					settings.Save(printer.ConfigPath); */
				}
			});
		}

		[Test]
		public void StartGCodeWithExtrudesMustFollowM109Heatup()
		{
			ValidateOnAllPrinters((printer, settings) =>
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

					if(m109Line == null)
					{
						printer.RuleViolated = true;
						return;
					}

					int m109Pos = lines.IndexOf(m109Line);
					int extrudePos = lines.IndexOf(extrudeLine);

					Assert.IsNotNull(m109Line);
					//Assert.IsNotNull(emptyExtrudeLine);
					//Assert.Greater(emptyExtrudePos, m109Pos);

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
			ValidateOnAllPrinters((printer, settings) =>
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
			ValidateOnAllPrinters((printer, settings) =>
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
			});
		}

		[Test]
		public void RetractLengthIsLessThanTwenty()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				string retractLengthString = settings.GetValue("retract_length");
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
			ValidateOnAllPrinters((printer, settings) =>
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
			ValidateOnAllPrinters((printer, settings) =>
			{
				string fanSpeedString = settings.GetValue("min_fan_speed");
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
			ValidateOnAllPrinters((printer, settings) =>
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
			ValidateOnAllPrinters((printer, settings) =>
			{
				string fanSpeedString = settings.GetValue("max_fan_speed");
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
			ValidateOnAllPrinters((printer, settings) =>
			{
				// TODO: Why aren't we testing all gcode sections?
				string[] keysToTest = { SettingsKey.start_gcode, SettingsKey.end_gcode };
				foreach (string gcodeKey in keysToTest)
				{
					string gcode = settings.GetValue(gcodeKey);
					if (gcode.Contains("{") || gcode.Contains("}") )
					{
						Assert.Fail(string.Format("[{0}] Curly brackets not allowed: {1}", gcodeKey, printer.RelativeFilePath));
					}
				}
			});
		}

		[Test]
		public void BottomSolidLayersNotZero()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				string bottomSolidLayers = settings.GetValue("bottom_solid_layers");
				if (!string.IsNullOrEmpty(bottomSolidLayers))
				{
					if (bottomSolidLayers == "0")
					{
						printer.RuleViolated = true;
						return;
					}

					//Assert.AreEqual("1mm", bottomSolidLayers, "[bottom_solid_layers] must be 1mm: " + printer.RelativeFilePath);
				}
			});
		}

		[Test]
		public void NoFirstLayerBedTempInStartGcode()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				string startGcode = settings.GetValue(SettingsKey.start_gcode);
				Assert.False(startGcode.Contains("first_layer_bed_temperature"), "[start_gcode] should not contain [first_layer_bed_temperature]" + printer.RelativeFilePath);
			});
		}

		[Test]
		public void FirstLayerHeightLessThanNozzleDiameterXExtrusionMultiplier()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				if (settings.GetValue("output_only_first_layer") == "1")
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

					double maximumLayerHeight = firstLayerExtrusionWidth * 0.85;

					// TODO: Remove once validated and resolved
					if (firstLayerHeight >= maximumLayerHeight)
					{
						printer.RuleViolated = true;
						return;
					}

					Assert.Less(firstLayerHeight, maximumLayerHeight, "[first_layer_height] must be less than [firstLayerExtrusionWidth]: " + printer.RelativeFilePath);
				}
				
			});
		}

		[Test]
		public void LayerHeightLessThanNozzleDiameter()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				if (settings.GetValue("output_only_first_layer") == "1")
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
			ValidateOnAllPrinters((printer, settings) =>
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
			ValidateOnAllPrinters((printer, settings) =>
			{
				string supportMaterialExtruder = settings.GetValue("support_material_extruder");
				if (!string.IsNullOrEmpty(supportMaterialExtruder) && printer.Oem != "Esagono")
				{
					Assert.AreEqual("1", supportMaterialExtruder, "[support_material_extruder] must be assigned to extruder 1: " + printer.RelativeFilePath);
				}
			});
		}

		[Test]
		public void SupportInterfaceMaterialAssignedToExtruderOne()
		{
			ValidateOnAllPrinters((printer, settings) =>
			{
				// Make exception for extruder assignment on 3D Stuffmaker slice files
				if (printer.Oem == "3D Stuffmaker")
				{
					return;
				}

				string supportMaterialInterfaceExtruder = settings.GetValue("support_material_interface_extruder");
				if (!string.IsNullOrEmpty(supportMaterialInterfaceExtruder) && printer.Oem != "Esagono")
				{
					Assert.AreEqual("1", supportMaterialInterfaceExtruder, "[support_material_interface_extruder] must be assigned to extruder 1: " + printer.RelativeFilePath);
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
		/// Calls the given delegate for each printer as well as each quality/material layer, passing in a PrinterConfig object that has 
		/// printer settings loaded into a SettingsLayer as well as state about the printer
		/// </summary>
		/// <param name="action">The action to invoke for each printer</param>
		private void ValidateOnAllPrinters(Action<PrinterConfig, PrinterSettings> action)
		{
			var ruleViolations = new List<string>();

			foreach (var printer in allPrinters)
			{
				printer.RuleViolated = false;

				var printerSettings = printer.PrinterSettings;
				printerSettings.AutoSave = false;

				// Disable active material/quality overrides
				printerSettings.SetMaterialPreset(0, "");
				printerSettings.ActiveQualityKey = "";

				// Validate just the OemLayer
				action(printer, printerSettings);

				if (printer.RuleViolated)
				{
					ruleViolations.Add(printer.RelativeFilePath);
				}

				// Validate material layers
				foreach (var layer in printer.PrinterSettings.MaterialLayers)
				{
					printer.RuleViolated = false;

					printerSettings.SetMaterialPreset(0, layer.LayerID);

					// Validate the settings with this material layer active
					action(printer, printerSettings);

					if (printer.RuleViolated)
					{
						ruleViolations.Add(printer.RelativeFilePath + " -> " + layer.Name);
					}
				}

				printerSettings.SetMaterialPreset(0, "");

				// Validate quality layers
				foreach (var layer in printer.PrinterSettings.QualityLayers)
				{
					printer.RuleViolated = false;

					printerSettings.ActiveQualityKey = layer.LayerID;

					// Validate the settings with this quality layer active
					action(printer, printerSettings);

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

		private class PrinterConfig
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
