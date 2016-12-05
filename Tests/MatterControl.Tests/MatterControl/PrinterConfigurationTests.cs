using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Tests.Automation;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture]
	public class PrinterConfigurationTests
	{
		[Test, Category("PrinterConfigurationFiles"), Category("FixNeeded" /* Not Finished/previously ignored */)]
		public void PrinterConfigTests()
		{
			string staticDataPath = TestContext.CurrentContext.ResolveProjectPath(5, "MatterControl", "StaticData");

			StaticData.Instance = new FileSystemStaticData(staticDataPath);

			var profilesDirectory = new DirectoryInfo(Path.Combine(staticDataPath, "Profiles"));

			foreach (FileInfo file in profilesDirectory.GetFiles("*.printer", SearchOption.AllDirectories))
			{
				var printerSettings = PrinterSettings.LoadFile(file.FullName);

				// Assert that no UserLayer values exist in production .printer files
				Assert.IsTrue(printerSettings.UserLayer.Keys.Count == 0);

				var layersToInspect = new List<PrinterSettingsLayer>();
				layersToInspect.Add(printerSettings.OemLayer);
				layersToInspect.AddRange(printerSettings.MaterialLayers);
				layersToInspect.AddRange(printerSettings.QualityLayers);

				// Validate each PrinterSettingLayer in the .printer file
				foreach (var layer in layersToInspect.Where(l => l.Keys.Any()))
				{
					firstLayerSpeedEqualsAcceptableValue(printerSettings, layer, file.FullName);

					firstLayerHeightLessThanNozzleDiameter(printerSettings, layer, file.FullName);

					layerHeightLessThanNozzleDiameter(printerSettings, layer, file.FullName);

					firstLayerExtrusionWidthAcceptableValue(printerSettings, layer, file.FullName);

					firstLayerExtrusionWidthNotZero(layer, file.FullName);

					bedSizeXYSeparatedByComma(layer, file.FullName);

					printCenterFormatSeparatedByComma(layer, file.FullName);

					testRetractLengthLessThanTwenty(layer, file.FullName);

					testExtruderCountGreaterThanZero(layer, file.FullName);

					minimumFanSpeedLessThanOrEqualToOneHundred(layer, file.FullName);

					maxFanSpeedNotGreaterThanOneHundred(layer, file.FullName);

					noCurlyBracketsInStartGcode(layer, file.FullName);

					noCurlyBracketsInEndGcode(layer, file.FullName);

					testBottomSolidLayersOneMM(layer, file.FullName);

					testFirstLayerTempNotInStartGcode(layer, file.FullName);

					testFirstLayerBedTemperatureNotInStartGcode(layer, file.FullName);
				}
			}
		}

		public void firstLayerSpeedEqualsAcceptableValue(PrinterSettings settings, PrinterSettingsLayer layer, string sourceFile)
		{
			string firstLayerSpeedString;
			if (!layer.TryGetValue(SettingsKey.first_layer_speed, out firstLayerSpeedString))
			{
				return;
			}

			double firstLayerSpeed;
			if (firstLayerSpeedString.Contains("%"))
			{
				string infillSpeedString = settings.GetValue("infill_speed");
				double infillSpeed = double.Parse(infillSpeedString);

				firstLayerSpeedString = firstLayerSpeedString.Replace("%", "");

				double FirstLayerSpeedPercent = double.Parse(firstLayerSpeedString);

				firstLayerSpeed = FirstLayerSpeedPercent * infillSpeed / 100.0;
			}
			else
			{
				firstLayerSpeed = double.Parse(firstLayerSpeedString);
			}

			Assert.Greater(firstLayerSpeed, 5, "Unexpected firstLayerSpeedEqualsAcceptableValue value: " + sourceFile);
		}

		public void firstLayerHeightLessThanNozzleDiameter(PrinterSettings printerSettings, PrinterSettingsLayer layer, string sourceFile)
		{
			string firstLayerHeight;
			
			if (!layer.TryGetValue(SettingsKey.first_layer_height, out firstLayerHeight))
			{
				return;
			}

			float convertedFirstLayerHeightValue;

			if (firstLayerHeight.Contains("%"))
			{
				string reFormatLayerHeight = firstLayerHeight.Replace("%", " ");
				convertedFirstLayerHeightValue = float.Parse(reFormatLayerHeight) / 100;
			}
			else
			{
				convertedFirstLayerHeightValue = float.Parse(firstLayerHeight);
			}

			string nozzleDiameter = printerSettings.GetValue(SettingsKey.nozzle_diameter);

			Assert.LessOrEqual(convertedFirstLayerHeightValue, float.Parse(nozzleDiameter), "Unexpected firstLayerHeightLessThanNozzleDiameter value: " + sourceFile);
		}

		public void firstLayerExtrusionWidthAcceptableValue(PrinterSettings printerSettings, PrinterSettingsLayer layer, string sourceFile)
		{
			string firstLayerExtrusionWidth;
			if (!layer.TryGetValue(SettingsKey.first_layer_extrusion_width, out firstLayerExtrusionWidth))
			{
				return;
			}

			float convertedFirstLayerExtrusionWidth;

			string nozzleDiameter = printerSettings.GetValue(SettingsKey.nozzle_diameter);
			float acceptableValue = float.Parse(nozzleDiameter) * 4;

			if (firstLayerExtrusionWidth.Contains("%"))
			{
				string reformatFirstLayerExtrusionWidth = firstLayerExtrusionWidth.Replace("%", " ");
				convertedFirstLayerExtrusionWidth = float.Parse(reformatFirstLayerExtrusionWidth) / 100;
			}
			else
			{
				convertedFirstLayerExtrusionWidth = float.Parse(firstLayerExtrusionWidth);
			}

			Assert.LessOrEqual(convertedFirstLayerExtrusionWidth, acceptableValue, "Unexpected firstLayerExtrusionWidthAcceptableValue value: " + sourceFile);
		}

		public void firstLayerExtrusionWidthNotZero(PrinterSettingsLayer layer, string sourceFile)
		{
			string firstLayerExtrusionWidth;
			if (!layer.TryGetValue(SettingsKey.first_layer_extrusion_width, out firstLayerExtrusionWidth))
			{
				return;
			}

			float convertedFirstLayerExtrusionWidth;

			if(firstLayerExtrusionWidth.Contains("%"))
			{
				string reformatFirstLayerExtrusionWidth = firstLayerExtrusionWidth.Replace("%", " ");
				convertedFirstLayerExtrusionWidth = float.Parse(reformatFirstLayerExtrusionWidth);
			}
			else
			{
				convertedFirstLayerExtrusionWidth = float.Parse(firstLayerExtrusionWidth);
			}

			Assert.AreNotEqual(0, convertedFirstLayerExtrusionWidth, "Unexpected firstLayerExtrusionWidthNotZero value: " + sourceFile);
		}

		public void layerHeightLessThanNozzleDiameter(PrinterSettings printerSettings, PrinterSettingsLayer layer, string sourceFile)
		{
			string layerHeight;
			if (!layer.TryGetValue(SettingsKey.layer_height, out layerHeight))
			{
				return;
			}

			float convertedLayerHeight = float.Parse(layerHeight);

			string nozzleDiameter = printerSettings.GetValue(SettingsKey.nozzle_diameter);
			float convertedNozzleDiameterValue = float.Parse(nozzleDiameter);

			Assert.LessOrEqual(convertedLayerHeight, convertedNozzleDiameterValue, "Unexpected layerHeightLessThanNozzleDiameter value: " + sourceFile);
		}

		public void bedSizeXYSeparatedByComma(PrinterSettingsLayer layer, string sourceFile)
		{
			string settingValue;
			if(!layer.TryGetValue(SettingsKey.bed_size, out settingValue))
			{
				return;
			}

			string[] settingValueToTest = settingValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

			Assert.AreEqual(2, settingValueToTest.Length, "bed_size should have two values separated by a comma: " + sourceFile);
		}

		public void printCenterFormatSeparatedByComma(PrinterSettingsLayer layer, string sourceFile)
		{
			string settingValue;
			
			if (!layer.TryGetValue(SettingsKey.print_center, out settingValue))
			{
				return;
			}

			string[] settingValueToTest = settingValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

			Assert.AreEqual(2, settingValueToTest.Length, "print_center should have two values separated by a comma: " + sourceFile);
		}

		public void testRetractLengthLessThanTwenty(PrinterSettingsLayer layer, string sourceFile)
		{
			string settingValue;
			if (!layer.TryGetValue("retract_length", out settingValue))
			{
				return;
			}

			Assert.Less(float.Parse(settingValue, CultureInfo.InvariantCulture.NumberFormat), 20, "retract_length should be less than 20: " + sourceFile);
		}

		public void testExtruderCountGreaterThanZero(PrinterSettingsLayer layer, string sourceFile)
		{
			string settingValue;
			if (!layer.TryGetValue("extruder_count", out settingValue))
			{
				return;
			}

			Assert.Greater(int.Parse(settingValue), 0, "extruder_count should be greater than zero: " + sourceFile);
		}

		public void minimumFanSpeedLessThanOrEqualToOneHundred(PrinterSettingsLayer layer, string sourceFile)
		{
			string settingValue;
			if (!layer.TryGetValue("min_fan_speed", out settingValue))
			{
				return;
			}

			Assert.LessOrEqual(int.Parse(settingValue), 100, "min_fan_speed should be less than or equal to 100: " + sourceFile);
		}

		public void maxFanSpeedNotGreaterThanOneHundred(PrinterSettingsLayer layer, string sourceFile)
		{
			string settingValue;
			if (!layer.TryGetValue("max_fan_speed", out settingValue))
			{
				return;
			}

			Assert.LessOrEqual(int.Parse(settingValue), 100, "max_fan_speed should be less than or equal to 100: " + sourceFile);
		}

		public void noCurlyBracketsInStartGcode(PrinterSettingsLayer layer, string sourceFile)
		{
			string settingValue;
			if (!layer.TryGetValue("start_gcode", out settingValue))
			{
				return;
			}

			Assert.IsFalse(settingValue.Contains("{"), "start_gcode should not contain braces: " + sourceFile);
		}

		public void noCurlyBracketsInEndGcode(PrinterSettingsLayer layer, string sourceFile)
		{
			string settingValue;
			if (!layer.TryGetValue("end_gcode", out settingValue))
			{
				return;
			}

			Assert.False(settingValue.Contains("{"), "end_gcode should not contain braces: " + sourceFile);
		}

		public void testBottomSolidLayersOneMM(PrinterSettingsLayer layer, string sourceFile)
		{
			string settingValue;
			if (!layer.TryGetValue("bottom_solid_layers", out settingValue))
			{
				return;
			}

			Assert.AreEqual("1mm", settingValue, "bottom_solid_layers should be 1mm: " + sourceFile);
		}

		public void testFirstLayerTempNotInStartGcode(PrinterSettingsLayer layer, string sourceFile)
		{
			string settingValue;
			if (!layer.TryGetValue("start_gcode", out settingValue))
			{
				return;
			}

			Assert.False(settingValue.Contains("first_layer_temperature"), "start_gcode should not contain first_layer_temperature: " + sourceFile);
		}

		public void testFirstLayerBedTemperatureNotInStartGcode(PrinterSettingsLayer layer, string sourceFile)
		{
			string settingValue;
			if (!layer.TryGetValue("start_gcode", out settingValue))
			{
				return;
			}

			Assert.False(settingValue.Contains("first_layer_bed_temperature"), "start_gcode should not contain first_layer_bed_temperature: " + sourceFile);
		}
	}
}
