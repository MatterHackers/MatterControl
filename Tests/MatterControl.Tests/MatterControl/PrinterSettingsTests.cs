using System;
using System.Collections.Generic;
using MatterControl.Printing.PrintLeveling;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Tests.Automation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture, Category("PrinterSettings")]
	public class PrinterSettingsTests
	{
		[Test]
		public void StartGCodeHasHeating()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var printer = new PrinterConfig(new PrinterSettings());
			printer.Settings.Slicer = new EngineMappingsMatterSlice();

			Slicer.ExtrudersUsed = new List<bool> { true };

			var extruderTemp = printer.Settings.GetValue<double>(SettingsKey.temperature);
			Assert.IsTrue(extruderTemp > 0);

			var bedTemp = printer.Settings.GetValue<double>(SettingsKey.bed_temperature);
			Assert.IsTrue(bedTemp > 0);

			string result = printer.Settings.ResolveValue(SettingsKey.start_gcode);

			// Pass start_gcode through exportField converter
			var exportField = printer.Settings.Slicer.Exports[SettingsKey.start_gcode];
			result = exportField.Converter(result, printer.Settings);

			var beforeAndAfter = result.Split(new string[] { "; settings from start_gcode" }, StringSplitOptions.None);

			Assert.AreEqual(2, beforeAndAfter.Length);
			Assert.IsTrue(beforeAndAfter[0].Contains($"M104 T0 S{extruderTemp}"));
			Assert.IsTrue(beforeAndAfter[0].Contains($"M140 S{bedTemp}"));
			Assert.IsFalse(beforeAndAfter[0].Contains($"M109 T0 S{extruderTemp}"));
			Assert.IsFalse(beforeAndAfter[0].Contains($"M190 S{bedTemp}"));
			Assert.IsTrue(beforeAndAfter[1].Contains($"M109 T0 S{extruderTemp}"));
			Assert.IsTrue(beforeAndAfter[1].Contains($"M190 S{bedTemp}"));

			// set mapping when there is an M109 in the start code
			printer.Settings.SetValue(SettingsKey.start_gcode, "G28\\nM109 S205");

			string result2 = printer.Settings.ResolveValue(SettingsKey.start_gcode);

			// Pass start_gcode through exportField converter
			result2 = exportField.Converter(result2, printer.Settings);

			beforeAndAfter = result2.Split(new string[] { "; settings from start_gcode" }, StringSplitOptions.None);

			// the main change is there should be an M190 before and not after the start code
			Assert.AreEqual(2, beforeAndAfter.Length);
			Assert.IsTrue(beforeAndAfter[0].Contains($"M104 T0 S{extruderTemp}"));
			Assert.IsTrue(beforeAndAfter[0].Contains($"M140 S{bedTemp}"));
			Assert.IsFalse(beforeAndAfter[0].Contains($"M109 T0 S{extruderTemp}"));
			Assert.IsTrue(beforeAndAfter[0].Contains($"M190 S{bedTemp}"));
			Assert.IsFalse(beforeAndAfter[1].Contains($"M109 T0 S{extruderTemp}"), "M109 already in gcode, should not be in after.");
			Assert.IsFalse(beforeAndAfter[1].Contains($"M190 S{bedTemp}"));
		}

		[Test]
		public void ExpectedPropertiesOnlyTest()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var expectedProperties = new HashSet<string>(
				new[]
				{
					"DocumentVersion",
					"ID",
					"StagedUserSettings",
					"Macros",
					"OemLayer",
					"UserLayer",
					"MaterialLayers",
					"QualityLayers"
				});

			var printer = new PrinterConfig(new PrinterSettings());
			var levelingSolution = new LevelWizard3Point(printer.Shim());
			var printerSettings = printer.Settings;

			var json = printer.Settings.ToJson();
			var jObject = JObject.Parse(json);

			foreach (var item in jObject)
			{
				Assert.IsTrue(expectedProperties.Contains(item.Key), $"Unexpected property ({item.Key}) in PrinterSettings - add to list or use @JsonIgnore");
			}
		}
	}
}
