using System.Collections.Generic;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture, Category("ConfigIni")]
	public class SettingsParseTests
	{
		[Test]
		public void Check3PointLevelingPositions()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));
			var printerSettings = ActiveSliceSettings.Instance;
			{
				var sample0 = LevelWizardBase.GetPrintLevelPositionToSample(printerSettings, 0);
				var sample1 = LevelWizardBase.GetPrintLevelPositionToSample(printerSettings, 1);
				var sample2 = LevelWizardBase.GetPrintLevelPositionToSample(printerSettings, 2);
				Assert.AreEqual("200,200", ActiveSliceSettings.Instance.GetValue(SettingsKey.bed_size));
				Assert.AreEqual("100,100", ActiveSliceSettings.Instance.GetValue(SettingsKey.print_center));
				Assert.AreEqual("rectangular", ActiveSliceSettings.Instance.GetValue(SettingsKey.bed_shape));
				Assert.AreEqual("", ActiveSliceSettings.Instance.GetValue(SettingsKey.leveling_manual_positions));
				Assert.AreEqual(new Vector2(100, 180), sample0);
				Assert.AreEqual(new Vector2(20, 20), sample1);
				Assert.AreEqual(new Vector2(180, 20), sample2);
			}

			{
				// nothing set
				var manualPositions = LevelWizardBase.GetManualPositions("", 3);
				Assert.IsNull(manualPositions);

				// not enough points
				manualPositions = LevelWizardBase.GetManualPositions("0,0:100,50", 3);
				Assert.IsNull(manualPositions);

				// too many points
				manualPositions = LevelWizardBase.GetManualPositions("0,0:100,0:200,200:50,3", 3);
				Assert.IsNull(manualPositions);

				// bad data
				manualPositions = LevelWizardBase.GetManualPositions("0,oe:100,0:200,200", 3);
				Assert.IsNull(manualPositions);

				// good data
				manualPositions = LevelWizardBase.GetManualPositions("0,1:100,2:50,101", 3);
				Assert.IsTrue(manualPositions.Count == 3);
				Assert.IsTrue(manualPositions[0] == new Vector2(0, 1));
				Assert.IsTrue(manualPositions[1] == new Vector2(100, 2));
				Assert.IsTrue(manualPositions[2] == new Vector2(50, 101));

				// good data
				manualPositions = LevelWizardBase.GetManualPositions("0,1:100,2:50,103:0,4:100,5:50,106:0,7:100,8:50,109", 9);
				Assert.IsTrue(manualPositions.Count == 9);
				Assert.IsTrue(manualPositions[0] == new Vector2(0, 1));
				Assert.IsTrue(manualPositions[1] == new Vector2(100, 2));
				Assert.IsTrue(manualPositions[2] == new Vector2(50, 103));
				Assert.IsTrue(manualPositions[3] == new Vector2(0, 4));
				Assert.IsTrue(manualPositions[4] == new Vector2(100, 5));
				Assert.IsTrue(manualPositions[5] == new Vector2(50, 106));
				Assert.IsTrue(manualPositions[6] == new Vector2(0, 7));
				Assert.IsTrue(manualPositions[7] == new Vector2(100, 8));
				Assert.IsTrue(manualPositions[8] == new Vector2(50, 109));
			}

			{
				ActiveSliceSettings.Instance.SetValue(SettingsKey.leveling_manual_positions, "1,2:211,3:113,104");
				var sample0 = LevelWizardBase.GetPrintLevelPositionToSample(printerSettings, 0);
				var sample1 = LevelWizardBase.GetPrintLevelPositionToSample(printerSettings, 1);
				var sample2 = LevelWizardBase.GetPrintLevelPositionToSample(printerSettings, 2);
				Assert.IsTrue(sample0 == new Vector2(1, 2));
				Assert.IsTrue(sample1 == new Vector2(211, 3));
				Assert.IsTrue(sample2 == new Vector2(113, 104));
			}
		}

		[Test]
		public void CheckIfShouldBeShownParseTests()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "0" };
				var profile = GetProfile(settings);
				Assert.IsFalse(profile.ParseShowString("has_heated_bed", null));
				Assert.IsTrue(profile.ParseShowString("!has_heated_bed", null));
			}

			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "1" };
				var profile = GetProfile(settings);
				Assert.IsTrue(profile.ParseShowString("has_heated_bed", null));
				Assert.IsFalse(profile.ParseShowString("!has_heated_bed", null));
			}

			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "0", SettingsKey.auto_connect, "0" };
				var profile = GetProfile(settings);
				Assert.IsFalse(profile.ParseShowString("has_heated_bed&auto_connect", null));
				Assert.IsFalse(profile.ParseShowString("has_heated_bed&!auto_connect", null));
				Assert.IsFalse(profile.ParseShowString("!has_heated_bed&auto_connect", null));
				Assert.IsTrue(profile.ParseShowString("!has_heated_bed&!auto_connect", null));
			}
			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "0", SettingsKey.auto_connect, "1" };
				var profile = GetProfile(settings);
				Assert.IsFalse(profile.ParseShowString("has_heated_bed&auto_connect", null));
				Assert.IsFalse(profile.ParseShowString("has_heated_bed&!auto_connect", null));
				Assert.IsTrue(profile.ParseShowString("!has_heated_bed&auto_connect", null));
				Assert.IsFalse(profile.ParseShowString("!has_heated_bed&!auto_connect", null));
			}
			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "1", SettingsKey.auto_connect, "0" };
				var profile = GetProfile(settings);
				Assert.IsFalse(profile.ParseShowString("has_heated_bed&auto_connect", null));
				Assert.IsTrue(profile.ParseShowString("has_heated_bed&!auto_connect", null));
				Assert.IsFalse(profile.ParseShowString("!has_heated_bed&auto_connect", null));
				Assert.IsFalse(profile.ParseShowString("!has_heated_bed&!auto_connect", null));
			}
			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "1", SettingsKey.auto_connect, "1" };
				var profile = GetProfile(settings);
				Assert.IsTrue(profile.ParseShowString("has_heated_bed&auto_connect", null));
				Assert.IsFalse(profile.ParseShowString("has_heated_bed&!auto_connect", null));
				Assert.IsFalse(profile.ParseShowString("!has_heated_bed&auto_connect", null));
				Assert.IsFalse(profile.ParseShowString("!has_heated_bed&!auto_connect", null));
			}

			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "1", SettingsKey.auto_connect, "1", SettingsKey.has_fan, "1" };
				var profile = GetProfile(settings);
				Assert.IsTrue(profile.ParseShowString("has_heated_bed&auto_connect&has_fan", null));
				Assert.IsTrue(profile.ParseShowString("has_heated_bed&auto_connect&has_fan|!has_sdcard", null));
				Assert.IsTrue(profile.ParseShowString("has_heated_bed&auto_connect&!has_sdcard|has_fan", null));
				Assert.IsTrue(profile.ParseShowString("has_heated_bed&auto_connect&has_sdcard|has_fan", null));
				Assert.IsFalse(profile.ParseShowString("has_heated_bed&auto_connect&!has_fan", null));
				Assert.IsFalse(profile.ParseShowString("has_heated_bed&!auto_connect&has_fan", null));
				Assert.IsFalse(profile.ParseShowString("!has_heated_bed&auto_connect&has_fan", null));
			}

			// test list setting value
			{
				string[] settings = new string[] { SettingsKey.has_hardware_leveling, "0", SettingsKey.print_leveling_solution, "3 Point Plane" };
				var profile = GetProfile(settings);
				Assert.IsTrue(profile.ParseShowString("!has_hardware_leveling&print_leveling_solution=3 Point Plane", null));
				Assert.IsTrue(profile.ParseShowString("!has_hardware_leveling&print_leveling_solution=3 Point Plane|print_leveling_solution=3x3 Mesh", null));
				Assert.IsTrue(profile.ParseShowString("!has_hardware_leveling&print_leveling_solution=3x3 Mesh|print_leveling_solution=3 Point Plane", null));
				Assert.IsTrue(profile.ParseShowString("!has_hardware_leveling&!print_leveling_solution=7 Point Disk", null));
				Assert.IsFalse(profile.ParseShowString("has_hardware_leveling&print_leveling_solution=3 Point Plane", null));
				Assert.IsFalse(profile.ParseShowString("!has_hardware_leveling&!print_leveling_solution=3 Point Plane", null));
				Assert.IsFalse(profile.ParseShowString("!has_hardware_leveling&print_leveling_solution=7 Point Disk", null));
			}
		}

		[Test]
		public void SupportInterfaceMaterialAssignedToExtruderOne()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));

			// first_layer_extrusion_width
			{
				// percent first layer extrusion width
				{
					string[] settings = new string[] { SettingsKey.first_layer_extrusion_width, "%150", SettingsKey.nozzle_diameter, ".4" };
					Assert.AreEqual(GetProfile(settings).GetValue<double>(SettingsKey.first_layer_extrusion_width), .6, .0001);
				}

				// absolute first layer extrusion width
				{
					string[] settings = new string[] { SettingsKey.first_layer_extrusion_width, ".75", SettingsKey.nozzle_diameter, ".4" };
					Assert.AreEqual(GetProfile(settings).GetValue<double>(SettingsKey.first_layer_extrusion_width), .75, .0001);
				}

				// 0 first layer extrusion width
				{
					string[] settings = new string[] { SettingsKey.first_layer_extrusion_width, "0", SettingsKey.nozzle_diameter, ".4" };
					Assert.AreEqual(GetProfile(settings).GetValue<double>(SettingsKey.first_layer_extrusion_width), .4, .0001);
				}
			}

			// extruder_count
			{
				// normal single
				{
					string[] settings = new string[] { SettingsKey.extruder_count, "1", SettingsKey.extruders_share_temperature, "0" };
					Assert.AreEqual(GetProfile(settings).GetValue<int>(SettingsKey.extruder_count), 1);
				}

				// normal multiple
				{
					string[] settings = new string[] { SettingsKey.extruder_count, "2", SettingsKey.extruders_share_temperature, "0" };
					Assert.AreEqual(GetProfile(settings).GetValue<int>(SettingsKey.extruder_count), 2);
				}

				// shared temp
				{
					string[] settings = new string[] { SettingsKey.extruder_count, "2", SettingsKey.extruders_share_temperature, "1" };
					Assert.AreEqual(GetProfile(settings).Helpers.NumberOfHotEnds(), 1);
				}
			}
		}

		[Test]
		// Validates that all SetSettingsOnChange linked fields exist and have their required TargetSetting and Value definitions
		public void LinkedSettingsExist()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			string propertiesFileContents = AggContext.StaticData.ReadAllText(Path.Combine("SliceSettings", "Properties.json"));
			var allSettings = JsonConvert.DeserializeObject<List<SliceSettingData>>(propertiesFileContents);

			var settingsByName = new Dictionary<string, SliceSettingData>();
			foreach (var settingsData in allSettings)
			{
				settingsByName.Add(settingsData.SlicerConfigName, settingsData);
			}

			foreach(var boundSetting in allSettings.Where(s => s.SetSettingsOnChange.Count > 0))
			{
				foreach(var linkedSetting in boundSetting.SetSettingsOnChange)
				{
					// TargetSetting definition must exist
					Assert.IsTrue(linkedSetting.TryGetValue("TargetSetting", out string targetSettingSource), "TargetSetting field should exist");

					// TargetSetting source field must be defined/known
					Assert.IsTrue(settingsByName.ContainsKey(targetSettingSource), "Linked field should exist: " + targetSettingSource);
				}
			}
		}

		[Test]
		public void PresentationNamesLackColon()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			string propertiesFileContents = AggContext.StaticData.ReadAllText(Path.Combine("SliceSettings", "Properties.json"));
			var allSettings = JsonConvert.DeserializeObject<List<SliceSettingData>>(propertiesFileContents);

			foreach (var setting in allSettings)
			{
				// TargetSetting source field must be defined/known
				Assert.IsFalse(setting.PresentationName.Trim().EndsWith(":"), $"Presentation name should not end with trailing colon: '{setting.PresentationName}'");
			}
		}

		PrinterSettings GetProfile(string[] settings)
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			for(int i=0; i<settings.Length; i+=2)
			{
				dictionary.Add(settings[i], settings[i + 1]);
			}
			var profile = new PrinterSettings()
			{
				OemLayer = new PrinterSettingsLayer(dictionary)
			};

			return profile;
		}
	}
}
