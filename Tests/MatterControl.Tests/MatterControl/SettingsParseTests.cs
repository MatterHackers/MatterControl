using System.Collections.Generic;
using System.IO;
using System.Linq;
using MatterControl.Printing.PrintLeveling;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;
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

			var printer = new PrinterConfig(new PrinterSettings());
			var levelingSolution = new LevelWizard3Point(printer.Shim());
			var printerSettings = printer.Settings;

			{
				var samples = levelingSolution.GetPrintLevelPositionToSample().ToList();
				Assert.AreEqual("200,200", printerSettings.GetValue(SettingsKey.bed_size));
				Assert.AreEqual("100,100", printerSettings.GetValue(SettingsKey.print_center));
				Assert.AreEqual("rectangular", printerSettings.GetValue(SettingsKey.bed_shape));
				Assert.AreEqual(new Vector2(20, 20), samples[0]);
				Assert.AreEqual(new Vector2(180, 20), samples[1]);
				Assert.AreEqual(new Vector2(100, 180), samples[2]);
			}
		}

		[Test]
		public void CheckIfShouldBeShownParseTests()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			// test single if set to 0
			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "0" };
				var profile = GetProfile(settings);
				Assert.IsFalse(profile.ParseShowString("has_heated_bed"));
				Assert.IsTrue(profile.ParseShowString("!has_heated_bed"));
			}

			// test single >
			{
				string[] settings = new string[] { SettingsKey.extruder_count, "1" };
				var profile = GetProfile(settings);
				Assert.IsFalse(profile.ParseShowString("extruder_count>1"));
				Assert.IsTrue(profile.ParseShowString("extruder_count>0"));
			}

			// test single >
			{
				string[] settings = new string[] { SettingsKey.extruder_count, "2" };
				var profile = GetProfile(settings);
				Assert.IsFalse(profile.ParseShowString("extruder_count>3"));
				Assert.IsFalse(profile.ParseShowString("extruder_count>2"));
				Assert.IsTrue(profile.ParseShowString("extruder_count>1"));
				Assert.IsTrue(profile.ParseShowString("extruder_count>0"));
			}

			// test single if set to 1
			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "1" };
				var profile = GetProfile(settings);
				Assert.IsTrue(profile.ParseShowString("has_heated_bed"));
				Assert.IsFalse(profile.ParseShowString("!has_heated_bed"));
			}

			// test & with set to 0 and 0
			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "0", SettingsKey.auto_connect, "0" };
				var profile = GetProfile(settings);
				Assert.IsFalse(profile.ParseShowString("has_heated_bed&auto_connect"));
				Assert.IsFalse(profile.ParseShowString("has_heated_bed&!auto_connect"));
				Assert.IsFalse(profile.ParseShowString("!has_heated_bed&auto_connect"));
				Assert.IsTrue(profile.ParseShowString("!has_heated_bed&!auto_connect"));
			}

			// test & with 0 and 1
			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "0", SettingsKey.auto_connect, "1" };
				var profile = GetProfile(settings);
				Assert.IsFalse(profile.ParseShowString("has_heated_bed&auto_connect"));
				Assert.IsFalse(profile.ParseShowString("has_heated_bed&!auto_connect"));
				Assert.IsTrue(profile.ParseShowString("!has_heated_bed&auto_connect"));
				Assert.IsFalse(profile.ParseShowString("!has_heated_bed&!auto_connect"));
			}

			// test & with 1 and 0
			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "1", SettingsKey.auto_connect, "0" };
				var profile = GetProfile(settings);
				Assert.IsFalse(profile.ParseShowString("has_heated_bed&auto_connect"));
				Assert.IsTrue(profile.ParseShowString("has_heated_bed&!auto_connect"));
				Assert.IsFalse(profile.ParseShowString("!has_heated_bed&auto_connect"));
				Assert.IsFalse(profile.ParseShowString("!has_heated_bed&!auto_connect"));
			}

			// test & with 1 and 1
			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "1", SettingsKey.auto_connect, "1" };
				var profile = GetProfile(settings);
				Assert.IsTrue(profile.ParseShowString("has_heated_bed&auto_connect"));
				Assert.IsFalse(profile.ParseShowString("has_heated_bed&!auto_connect"));
				Assert.IsFalse(profile.ParseShowString("!has_heated_bed&auto_connect"));
				Assert.IsFalse(profile.ParseShowString("!has_heated_bed&!auto_connect"));
			}

			// test 3 &s
			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "1", SettingsKey.auto_connect, "1", SettingsKey.has_fan, "1" };
				var profile = GetProfile(settings);
				Assert.IsTrue(profile.ParseShowString("has_heated_bed&auto_connect&has_fan"));
				Assert.IsTrue(profile.ParseShowString("has_heated_bed&auto_connect&has_fan|!has_sdcard"));
				Assert.IsTrue(profile.ParseShowString("has_heated_bed&auto_connect&!has_sdcard|has_fan"));
				Assert.IsTrue(profile.ParseShowString("has_heated_bed&auto_connect&has_sdcard|has_fan"));
				Assert.IsFalse(profile.ParseShowString("has_heated_bed&auto_connect&!has_fan"));
				Assert.IsFalse(profile.ParseShowString("has_heated_bed&!auto_connect&has_fan"));
				Assert.IsFalse(profile.ParseShowString("!has_heated_bed&auto_connect&has_fan"));
			}

			// test list setting value
			{
				string[] settings = new string[] { SettingsKey.has_hardware_leveling, "0",
					SettingsKey.print_leveling_solution, "3 Point Plane",
					SettingsKey.extruder_count, "2"};
				var profile = GetProfile(settings);
				Assert.IsTrue(profile.ParseShowString("!has_hardware_leveling&print_leveling_solution=3 Point Plane"));
				Assert.IsTrue(profile.ParseShowString("!has_hardware_leveling&print_leveling_solution=3 Point Plane|print_leveling_solution=3x3 Mesh"));
				Assert.IsTrue(profile.ParseShowString("!has_hardware_leveling&print_leveling_solution=3x3 Mesh|print_leveling_solution=3 Point Plane"));
				Assert.IsTrue(profile.ParseShowString("!has_hardware_leveling&!print_leveling_solution=7 Point Disk"));
				Assert.IsFalse(profile.ParseShowString("has_hardware_leveling&print_leveling_solution=3 Point Plane"));
				Assert.IsFalse(profile.ParseShowString("!has_hardware_leveling&!print_leveling_solution=3 Point Plane"));
				Assert.IsFalse(profile.ParseShowString("!has_hardware_leveling&print_leveling_solution=7 Point Disk"));

				Assert.IsFalse(profile.ParseShowString("!has_hardware_leveling&print_leveling_solution=3 Point Plane&extruder_count>2"));
				Assert.IsFalse(profile.ParseShowString("!has_hardware_leveling&print_leveling_solution=3 Point Plane&extruder_count>2"));
				Assert.IsTrue(profile.ParseShowString("!has_hardware_leveling&print_leveling_solution=3 Point Plane&extruder_count>1"));
				Assert.IsTrue(profile.ParseShowString("!has_hardware_leveling&print_leveling_solution=3 Point Plane&extruder_count>0"));
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
					Assert.AreEqual(GetProfile(settings).Helpers.HotendCount(), 1);
				}
			}
		}

		[Test]
		// Validates that all SetSettingsOnChange linked fields exist and have their required TargetSetting and Value definitions
		public void LinkedSettingsExist()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var settingsByName = PrinterSettings.SettingsData;
			var allSettings = settingsByName.Values;

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

			var allSettings = PrinterSettings.SettingsData.Values;

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
