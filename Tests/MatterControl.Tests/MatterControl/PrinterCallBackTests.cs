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
    public class PrinterCallbackTests
    {

        Dictionary<string, float> settingsComparison;


        /*[Test]
        public void Blah()
        {
            Assert.True("B" == "B");
        }*/

       [Test, Category("PrinterConfigurationFiles")]
       public void PrinterConfigTests()
        {

           //Do the work to setup the expected failure case
           //ActiveSliceSettings.Instance.FirstLayerExtrusionWidth = 0.2;

           //Assert.True(ActiveSliceSettings.Instance.IsValid());
           //Assert.True("A" == "A");

           //Do the work to setup the expected failure case
           //Assert.False(ActiveSliceSettings.Instance.IsValid());           

           DirectoryInfo currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
           var allConfigFile = currentDirectory.Parent.Parent.Parent.Parent.FullName;
           string pathToPrinterSettings = @"StaticData\PrinterSettings";
           var fullPathToPrinterSettings = Path.Combine(allConfigFile, pathToPrinterSettings);

           DirectoryInfo test = new DirectoryInfo(fullPathToPrinterSettings);

           IEnumerable<FileInfo> fileList = test.GetFiles(".", System.IO.SearchOption.AllDirectories);

           var allPrinterConfigs = fileList.Where(file => file.Name == "config.ini");
           
           foreach (FileInfo files in allPrinterConfigs)
           {
               Console.WriteLine(files.FullName);

               settingsComparison = new Dictionary<string, float>();
               foreach(string line in File.ReadLines(files.FullName))
               {
                   
                   string[] settingNameAndValue = line.Split(new []{'='}, StringSplitOptions.RemoveEmptyEntries);
                   string settingName = settingNameAndValue[0].Trim();
                   string settingValue = string.Empty;

                   if (settingNameAndValue.Length == 2)
                   {
                        settingValue = settingNameAndValue[1].Trim();
                   }
      
                   createComparisonDictionary(settingName, settingValue);
                   bedSizeXYSeparatedByComma(settingName, settingValue);
                   printCenterFormatSeparatedByComma(settingName, settingValue);
                   testRetractLengthLessThanTwenty(settingName, settingValue);
                   testExtruderCountGreaterThanZero(settingName, settingValue);
                   maxFanSpeedNotGreaterThanOneHundred(settingName, settingValue);
                   minimumFanSpeedLessThanOneHundred(settingName, settingValue);
                   noCurlyBracketsInStartGcode(settingName, settingValue);
                   noCurlyBracketsInEndGcode(settingName, settingValue);
                   testBottomSolidLayersOneMM(settingName, settingValue);
                   testFirstLayerTempNotInStartGcode(settingName, settingValue);
                   testFirstLayerBedTemperatureNotInStartGcode(settingName, settingValue);

               }

               compareDictionarySettings();
           }
        }

        public void createComparisonDictionary (string settingName, string settingValue)
       {

            if (settingName == "nozzle_diameter")
            {
                settingsComparison[settingName] = float.Parse(settingValue);
                
            }
            else if(settingName == "layer_height")
            {
                settingsComparison[settingName] = float.Parse(settingValue);

            }
            else if (settingName =="first_layer_height")
            {
                if (settingValue.Contains("%"))
                {
                    string newVal = settingValue.Replace("%", " ");
                    settingsComparison[settingName] = float.Parse(newVal) / 100;
                }
                else
                {
                    settingsComparison[settingName] = float.Parse(settingValue);
                }
                
            }
            else if (settingName == "first_layer_extrusion_width")
            {

                if (settingValue.Contains("%"))
                {
                    string newVal = settingValue.Replace("%", " ");
                    settingsComparison[settingName] = float.Parse(newVal) / 100;
                }
                else
                {
                    settingsComparison[settingName] = float.Parse(settingValue);
                }
            }

       }

        public void compareDictionarySettings()
        {

            float firstLayerHeight = settingsComparison["first_layer_height"];
            float nozzleDiameter = settingsComparison["nozzle_diameter"];
            float layerHeight = settingsComparison["layer_height"];
            float firstLayerExtrusionWidth = settingsComparison["first_layer_extrusion_width"];
            float firstLayerExtrusionWidthToTest = firstLayerExtrusionWidth * nozzleDiameter;
            float firstLayerExtrusionWidthThreshold =   nozzleDiameter * 4;

            if (firstLayerHeight > nozzleDiameter)
            {
                Console.WriteLine("first layer height greater than nozzle diameter");
            }

            if (layerHeight > nozzleDiameter)
            {
                Console.WriteLine("layer height greater than nozzle diameter");
            }

            if(firstLayerExtrusionWidthToTest > firstLayerExtrusionWidthThreshold)
            {
                Console.WriteLine("First Layer extrusion width greater than acceptable value");
            }

            if(firstLayerExtrusionWidthToTest <= 0)
            {
                Console.WriteLine("First layer extrusion width cannot be zero");
            }

        }


        public void bedSizeXYSeparatedByComma(string settingName, string settingValue)
        {

            string[] settingValueToTest = settingValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (settingName == "bed_size" && settingValueToTest.Length != 2)
            {        
                    string test = String.Format("Name: {0}     ::  Value: {1} ", settingValue, settingValue.Length.ToString());
                    Console.WriteLine(test);
            }
        }

        public void printCenterFormatSeparatedByComma(string settingName, string settingValue)
        {
            string[] settingValueToTest = settingValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (settingName == "print_center" && settingValueToTest.Length != 2)
            {
                string test = String.Format("Name: {0}     ::  Value: {1} ", settingValue, settingValue.Length.ToString());
                Console.WriteLine(test);
            }
        }

        public void testRetractLengthLessThanTwenty(string settingName, string settingValue)
        {
            
            if(settingName == "retract_length") 
            {
                float convertedSettingValue = float.Parse(settingValue, CultureInfo.InvariantCulture.NumberFormat);
                if (convertedSettingValue > 20)
                {
                    string test = String.Format("{0} :: {1}", settingName, convertedSettingValue.ToString());
                    Console.WriteLine(test);
                }
            }
        }

        public void testExtruderCountGreaterThanZero(string settingName, string settingValue)
        {
            if (settingName =="extruder_count")
            {
                int convertedExtruderCount =    Int32.Parse(settingValue);
                if(convertedExtruderCount < 1)
                {
                    string test = String.Format("{0} :: {1}", settingName, convertedExtruderCount.ToString());
                    Console.WriteLine(test);
                }
            }
        }

        public void minimumFanSpeedLessThanOneHundred(string settingName, string settingValue)
        {
            if (settingName == "min_fan_speed")
            {
                int convertedFanSpeed = Int32.Parse(settingValue);
                
                if(convertedFanSpeed > 100)
                {
                    string test = String.Format("{0} :: {1}", settingName, convertedFanSpeed.ToString());
                    Console.WriteLine(test);
                }
            }
        }

        public void maxFanSpeedNotGreaterThanOneHundred(string settingName, string settingValue)
        {

            if (settingName == "max_fan_speed")
            {
                int convertedFanSpeed = Int32.Parse(settingValue);

                if (convertedFanSpeed > 100)
                {
                    string test = String.Format("{0} :: {1}", settingName, convertedFanSpeed.ToString());
                    Console.WriteLine(test);
                }
            }
        }

        public void noCurlyBracketsInStartGcode(string settingName, string settingValue)
        {

            if (settingName == "start_gcode" && settingValue.Contains("}"))
            {
                Console.WriteLine("CURLY BRACKETS IN THERE");
            }

        }

        public void noCurlyBracketsInEndGcode(string settingName, string settingValue)
        {
            if (settingName == "end_gcode" && settingValue.Contains("}"))
            {
                Console.WriteLine("Curly brakcet in end gcode");
            }

        }


        public void testBottomSolidLayersOneMM(string settingName, string settingValue)
        {

            if (settingName == "bottom_solid_layers" && settingValue != "1mm")
            {
                Console.WriteLine("Bottom solid layer test fail");
            }

        }

        public void testFirstLayerTempNotInStartGcode(string settingName, string settingValue)
        {

            if(settingName == "start_gcode" && settingValue.Contains("first_layer_temperature"))
            {
                Console.WriteLine("FIRST Layer temp fail");
            }

        }

        public void testFirstLayerBedTemperatureNotInStartGcode(string settingName, string settingValue)
        {

            if(settingName == "start_gcode" && settingValue.Contains("first_layer_bed_temperature"))
            {
                Console.WriteLine("FIRST LAYER BED TEMP FAIL");

            }
        }
    }
}
