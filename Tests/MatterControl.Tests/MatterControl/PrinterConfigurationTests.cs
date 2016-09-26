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

	[TestFixture]
	public class PrinterConfigurationTests
	{
		[Test, Category("PrinterConfigurationFiles"), Ignore("Not Finished")]
		public void PrinterConfigTests()
		{

			DirectoryInfo currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
			var allConfigFile = currentDirectory.Parent.Parent.Parent.Parent.FullName;
			string pathToPrinterSettings = @"StaticData\PrinterSettings";
			var fullPathToPrinterSettings = Path.Combine(allConfigFile, pathToPrinterSettings);
			Dictionary<string, string> currentProfile = new Dictionary<string, string>();
			DirectoryInfo test = new DirectoryInfo(fullPathToPrinterSettings);
			IEnumerable<FileInfo> fileList = test.GetFiles(".", System.IO.SearchOption.AllDirectories);
			var allPrinterConfigs = fileList.Where(file => file.Name == "config.ini");

			foreach (FileInfo file in allPrinterConfigs)
			{
				//Iterate over each line in the config file, and load the setting and value into a dictionary
				foreach (string line in File.ReadLines(file.FullName))
				{
					string[] settingNameAndValue = line.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
					string settingName = settingNameAndValue[0].Trim();
					string settingValue = string.Empty;

					if (settingNameAndValue.Length == 2)
					{
						settingValue = settingNameAndValue[1].Trim();
					}
					currentProfile[settingName] = settingValue;
				}

				Assert.True(
					firstLayerSpeedEqualsAcceptableValue(currentProfile),
					"Unexpected firstLayerSpeedEqualsAcceptableValue value: " + file.FullName);


				Assert.True(
					firstLayerHeightLessThanNozzleDiameter(currentProfile),
					"Unexpected firstLayerHeightLessThanNozzleDiameter value: " + file.FullName);

				Assert.True(
					layerHeightLessThanNozzleDiameter(currentProfile),
					"Unexpected layerHeightLessThanNozzleDiameter value: " + file.FullName);

				Assert.True(
					firstLayerExtrusionWidthAcceptableValue(currentProfile),
					"Unexpected firstLayerExtrusionWidthAcceptableValue value: " + file.FullName
					);

				Assert.True(firstLayerExtrusionWidthNotZero(currentProfile),
					"Unexpected firstLayerExtrusionWidthNotZero value: " + file.FullName);

				Assert.True(
					bedSizeXYSeparatedByComma(currentProfile), 
					"Unexpected bedSizeXYSeparatedByComma value: " + file.FullName);

				Assert.True(
					printCenterFormatSeparatedByComma(currentProfile),
					"Unexpected printCenterFormatSeparatedByComma value: " + file.FullName);

				Assert.True(
					testRetractLengthLessThanTwenty(currentProfile),
					"Unexpected testRetractLengthLessThanTwenty value: " + file.FullName);

				Assert.True(
					testExtruderCountGreaterThanZero(currentProfile),
					"Unexpected testExtruderCountGreaterThanZero value: " + file.FullName);

				Assert.True(
					minimumFanSpeedLessThanOneHundred(currentProfile),
					"Unexpected minimumFanSpeedLessThanOneHundred value: " + file.FullName);

				Assert.True(
					maxFanSpeedNotGreaterThanOneHundred(currentProfile),
					"Unexpected maxFanSpeedNotGreaterThanOneHundred value: " + file.FullName);

				Assert.True(
					noCurlyBracketsInStartGcode(currentProfile),
					"Unexpected noCurlyBracketsInStartGcode value: " + file.FullName);

				Assert.True(
					noCurlyBracketsInEndGcode(currentProfile),
					"Unexpected noCurlyBracketsInEndGcode value: " + file.FullName);

				Assert.True(
					testBottomSolidLayersOneMM(currentProfile),
					"Unexpected testBottomSolidLayersOneMM value: " + file.FullName);

				Assert.True(
					testFirstLayerTempNotInStartGcode(currentProfile),
					"Unexpected testFirstLayerTempNotInStartGcode value: " + file.FullName);

				Assert.True(
					testFirstLayerBedTemperatureNotInStartGcode(currentProfile),
					"Unexpected testFirstLayerBedTemperatureNotInStartGcode value: " + file.FullName);
			}
		}

		public bool firstLayerSpeedEqualsAcceptableValue(Dictionary<string, string> currentFile)
		{
			string firstLayerSpeedString = currentFile["first_layer_speed"];
			double firstLayerSpeed;

			if (firstLayerSpeedString.Contains("%"))
			{
				string infillSpeedString = currentFile["infill_speed"];
				double infillSpeed = double.Parse(infillSpeedString);

				firstLayerSpeedString = firstLayerSpeedString.Replace("%", "");

				double FirstLayerSpeedPercent = double.Parse(firstLayerSpeedString);

				firstLayerSpeed = FirstLayerSpeedPercent * infillSpeed / 100.0;
			}
			else
			{
				firstLayerSpeed = double.Parse(firstLayerSpeedString);
			}

			return firstLayerSpeed > 5;
		}

		public bool firstLayerHeightLessThanNozzleDiameter(Dictionary<string, string> currentFile)
		{
			string firstLayerHeight = currentFile[SettingsKey.first_layer_height];
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

			string nozzleDiameter = currentFile[SettingsKey.nozzle_diameter];
			float convertedNozzleDiameterValue = float.Parse(nozzleDiameter);

			return convertedFirstLayerHeightValue <= convertedNozzleDiameterValue;
		}

		public bool firstLayerExtrusionWidthAcceptableValue(Dictionary<string, string> currentFile)
		{
			string firstLayerExtrusionWidth = currentFile[SettingsKey.first_layer_extrusion_width];
			float convertedFirstLayerExtrusionWidth;

			string nozzleDiameter = currentFile[SettingsKey.nozzle_diameter];
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

			return convertedFirstLayerExtrusionWidth <= acceptableValue;
		}

		public bool firstLayerExtrusionWidthNotZero(Dictionary<string,string> currentFile)
		{
			string firstLayerExtrusionWidth = currentFile[SettingsKey.first_layer_extrusion_width];
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

			return convertedFirstLayerExtrusionWidth != 0;
		}

		public bool layerHeightLessThanNozzleDiameter(Dictionary<string,string> currentFile)
		{
			string layerHeight = currentFile[SettingsKey.layer_height];
			float convertedLayerHeight = float.Parse(layerHeight);

			string nozzleDiameter = currentFile[SettingsKey.nozzle_diameter];
			float convertedNozzleDiameterValue = float.Parse(nozzleDiameter);

			return convertedLayerHeight <= convertedNozzleDiameterValue;
		}

		public bool bedSizeXYSeparatedByComma(Dictionary<string, string> currentFile)
		{
			string settingValue = currentFile[SettingsKey.bed_size];
			string[] settingValueToTest = settingValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

			return settingValueToTest.Length == 2;
		}

		public bool printCenterFormatSeparatedByComma(Dictionary<string, string> currentFile)
		{
			string settingValue = currentFile[SettingsKey.print_center];
			string[] settingValueToTest = settingValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

			return settingValueToTest.Length == 2;
		}

		public bool testRetractLengthLessThanTwenty(Dictionary<string, string> currentFile)
		{
			string settingValue = currentFile["retract_length"];
			float convertedSettingValue = float.Parse(settingValue, CultureInfo.InvariantCulture.NumberFormat);

			return convertedSettingValue < 20;
		}

        public bool testExtruderCountGreaterThanZero(Dictionary<string,string> currentFile)
        {
			string settingValue = currentFile["extruder_count"];
			int convertedExtruderCount =    Int32.Parse(settingValue);

			return convertedExtruderCount > 0;
		}

		public bool minimumFanSpeedLessThanOneHundred(Dictionary<string, string> currentFile)
		{
			string settingValue = currentFile["min_fan_speed"];
			int convertedFanSpeed = Int32.Parse(settingValue);

			return convertedFanSpeed < 100;
		}

		public bool maxFanSpeedNotGreaterThanOneHundred(Dictionary<string, string> currentFile)
		{
			string settingValue = currentFile["max_fan_speed"];
			int convertedFanSpeed = Int32.Parse(settingValue);

			return convertedFanSpeed <= 100;
		}


		public bool noCurlyBracketsInStartGcode(Dictionary<string, string> currentFile)
		{
			string settingValue = currentFile["start_gcode"];

			return !settingValue.Contains("{");
		}

		public bool noCurlyBracketsInEndGcode(Dictionary<string, string> currentFile)
		{
			string settingValue = currentFile["end_gcode"];

			return !settingValue.Contains("{");
		}


		public bool testBottomSolidLayersOneMM(Dictionary<string,string> currentFile)
		{
			string settingValue = currentFile["bottom_solid_layers"];

			return settingValue == "1mm";
		}

		public bool testFirstLayerTempNotInStartGcode(Dictionary<string,string> currentFile)
		{
			string settingValue = currentFile["start_gcode"];

			return !settingValue.Contains("first_layer_temperature");
		}

		public bool  testFirstLayerBedTemperatureNotInStartGcode(Dictionary<string,string> currentFile)
		{
			string settingValue = currentFile["start_gcode"];
			return !settingValue.Contains("first_layer_bed_temperature");
		}
	}
}
