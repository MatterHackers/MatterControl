using System.Collections.Generic;
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
