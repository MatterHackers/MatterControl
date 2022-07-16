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
		public void CheckImportPrinterSettingsToPrinter()
		{
			StaticData.RootPath = MatterControlUtilities.StaticDataPath;
			MatterControlUtilities.OverrideAppDataLocation(MatterControlUtilities.RootPath);

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

		[Test]
		public void MergeDropsFieldsIfValueAlreadySet()
		{
			// Validates that field are dropped during import if they are already set in a base layer
			//
			StaticData.RootPath = MatterControlUtilities.StaticDataPath;
			MatterControlUtilities.OverrideAppDataLocation(MatterControlUtilities.RootPath);

			var printerSettings = new PrinterSettings();
			printerSettings.SetValue(SettingsKey.cancel_gcode, "cancel gcode");
			printerSettings.SetValue(SettingsKey.start_gcode, "start gcode");

			// Ensure layer_height of a given value
			printerSettings.BaseLayer[SettingsKey.layer_height] = "0.25";

			string newValue = "----- cancel gcode ----";
			string notAnExistingKey = "NotAnExistingKey";

			var toImport = new PrinterSettings();
			toImport.SetValue(SettingsKey.cancel_gcode, newValue);
			toImport.SetValue(SettingsKey.layer_height, "0.25");
			toImport.SetValue(notAnExistingKey, "------------------");

			var sourceFilter = new List<PrinterSettingsLayer>()
			{
				toImport.UserLayer
			};

			printerSettings.Merge(printerSettings.UserLayer, toImport, sourceFilter, false);

			Assert.AreEqual(printerSettings.GetValue(SettingsKey.cancel_gcode), newValue, "Imported setting applied");
			Assert.IsEmpty(printerSettings.GetValue(notAnExistingKey), "Invalid settings keys should be skipped");
			Assert.IsFalse(printerSettings.UserLayer.ContainsKey(SettingsKey.layer_height), "User layer should not contain layer_height after merge");
			Assert.AreEqual(2, printerSettings.UserLayer.Count, "User layer should contain two items after import (start_gcode, cancel_gcode)");
		}
	}
}
