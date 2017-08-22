using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Tests.Automation;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture, Category("ConfigIni")]
	public class ImportSettingsTests
	{
		[Test]
		public void CheckImportIniToPrinter()
		{
		}

		[Test]
		public void CheckImportPrinterSettingsToPrinter()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var printerSettings = new PrinterSettings();
			printerSettings.SetValue(SettingsKey.cancel_gcode, "cancel gcode");
			printerSettings.SetValue(SettingsKey.start_gcode, "start gcode");

			string newValue = "----- cancel gcode ----";

			string notAnExistingKey = "NotAnExistingKey";

			var toImport = new PrinterSettings();
			toImport.SetValue(SettingsKey.cancel_gcode, newValue);
			toImport.SetValue(notAnExistingKey, "------------------");

			var sourceFilter = new List<PrinterSettingsLayer>()
			{
				toImport.UserLayer
			};

			printerSettings.Merge(printerSettings.UserLayer, toImport, sourceFilter, false);

			Assert.AreEqual(printerSettings.GetValue(SettingsKey.cancel_gcode), newValue, "Imported setting applied");
			Assert.IsEmpty(printerSettings.GetValue(notAnExistingKey), "Invalid settings keys should be skipped");
		}
	}
}
