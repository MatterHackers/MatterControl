using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.SlicerConfiguration.MappingClasses;
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

			var gcodeMapping = new MapStartGCode(printer, SettingsKey.start_gcode, "startCode", true);

			Slicer.ExtrudersUsed = new List<bool> { true };

			var startGCode = gcodeMapping.Value;
			var extruderTemp = printer.Settings.GetValue<double>(SettingsKey.temperature);
			Assert.IsTrue(extruderTemp > 0);
			var bedTemp = printer.Settings.GetValue<double>(SettingsKey.bed_temperature);
			Assert.IsTrue(bedTemp > 0);
			Assert.IsTrue(startGCode.Contains($"M104 T0 S{extruderTemp}"));
			Assert.IsTrue(startGCode.Contains($"M109 T0 S{extruderTemp}"));
			Assert.IsTrue(startGCode.Contains($"M140 S{bedTemp}"));
			Assert.IsTrue(startGCode.Contains($"M190 S{bedTemp}"));
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
			var levelingSolution = new LevelWizard3Point(printer);
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
