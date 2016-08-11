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
	[TestFixture, Category("ConfigIni")]
	public class SettingsParseTests
	{
		[Test]
		public void CheckIfShouldBeShownParseTests()
		{

			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "0" };
				var profile = GettProfile(settings);
				Assert.IsFalse(SliceSettingsWidget.ParseShowString("has_heated_bed", profile, null));
				Assert.IsTrue(SliceSettingsWidget.ParseShowString("!has_heated_bed", profile, null));
			}

			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "1" };
				var profile = GettProfile(settings);
				Assert.IsTrue(SliceSettingsWidget.ParseShowString("has_heated_bed", profile, null));
				Assert.IsFalse(SliceSettingsWidget.ParseShowString("!has_heated_bed", profile, null));
			}

			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "0", SettingsKey.auto_connect, "0" };
				var profile = GettProfile(settings);
				Assert.IsTrue(!SliceSettingsWidget.ParseShowString("has_heated_bed&auto_connect", profile, null));
				Assert.IsTrue(!SliceSettingsWidget.ParseShowString("has_heated_bed&!auto_connect", profile, null));
				Assert.IsTrue(!SliceSettingsWidget.ParseShowString("!has_heated_bed&auto_connect", profile, null));
				Assert.IsTrue(SliceSettingsWidget.ParseShowString("!has_heated_bed&!auto_connect", profile, null));
			}
			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "0", SettingsKey.auto_connect, "1" };
				var profile = GettProfile(settings);
				Assert.IsTrue(!SliceSettingsWidget.ParseShowString("has_heated_bed&auto_connect", profile, null));
				Assert.IsTrue(!SliceSettingsWidget.ParseShowString("has_heated_bed&!auto_connect", profile, null));
				Assert.IsTrue(SliceSettingsWidget.ParseShowString("!has_heated_bed&auto_connect", profile, null));
				Assert.IsTrue(!SliceSettingsWidget.ParseShowString("!has_heated_bed&!auto_connect", profile, null));
			}
			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "1", SettingsKey.auto_connect, "0" };
				var profile = GettProfile(settings);
				Assert.IsTrue(!SliceSettingsWidget.ParseShowString("has_heated_bed&auto_connect", profile, null));
				Assert.IsTrue(SliceSettingsWidget.ParseShowString("has_heated_bed&!auto_connect", profile, null));
				Assert.IsTrue(!SliceSettingsWidget.ParseShowString("!has_heated_bed&auto_connect", profile, null));
				Assert.IsTrue(!SliceSettingsWidget.ParseShowString("!has_heated_bed&!auto_connect", profile, null));
			}
			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "1", SettingsKey.auto_connect, "1" };
				var profile = GettProfile(settings);
				Assert.IsTrue(SliceSettingsWidget.ParseShowString("has_heated_bed&auto_connect", profile, null));
				Assert.IsTrue(!SliceSettingsWidget.ParseShowString("has_heated_bed&!auto_connect", profile, null));
				Assert.IsTrue(!SliceSettingsWidget.ParseShowString("!has_heated_bed&auto_connect", profile, null));
				Assert.IsTrue(!SliceSettingsWidget.ParseShowString("!has_heated_bed&!auto_connect", profile, null));
			}

			{
				string[] settings = new string[] { SettingsKey.has_heated_bed, "1", SettingsKey.auto_connect, "1", SettingsKey.has_fan, "1" };
				var profile = GettProfile(settings);
				Assert.IsTrue(SliceSettingsWidget.ParseShowString("has_heated_bed&auto_connect&has_fan", profile, null));
				Assert.IsTrue(!SliceSettingsWidget.ParseShowString("has_heated_bed&auto_connect&!has_fan", profile, null));
				Assert.IsTrue(!SliceSettingsWidget.ParseShowString("has_heated_bed&!auto_connect&has_fan", profile, null));
				Assert.IsTrue(!SliceSettingsWidget.ParseShowString("!has_heated_bed&auto_connect&has_fan", profile, null));
			}
		}

		[Test]
		public void SupportInterfaceMaterialAssignedToExtruderOne()
		{
			// first_layer_extrusion_width
			{
				// percent first layer extrusion width
				{
					string[] settings = new string[] { SettingsKey.first_layer_extrusion_width, "%150", SettingsKey.nozzle_diameter, ".4" };
					Assert.AreEqual(GettProfile(settings).GetValue<double>(SettingsKey.first_layer_extrusion_width), .6, .0001);
				}

				// absolute first layer extrusion width
				{
					string[] settings = new string[] { SettingsKey.first_layer_extrusion_width, ".75", SettingsKey.nozzle_diameter, ".4" };
					Assert.AreEqual(GettProfile(settings).GetValue<double>(SettingsKey.first_layer_extrusion_width), .75, .0001);
				}

				// 0 first layer extrusion width
				{
					string[] settings = new string[] { SettingsKey.first_layer_extrusion_width, "0", SettingsKey.nozzle_diameter, ".4" };
					Assert.AreEqual(GettProfile(settings).GetValue<double>(SettingsKey.first_layer_extrusion_width), .4, .0001);
				}
			}

			// extruder_count
			{
				// normal single
				{
					string[] settings = new string[] { SettingsKey.extruder_count, "1", SettingsKey.extruders_share_temperature, "0" };
					Assert.AreEqual(GettProfile(settings).GetValue<int>(SettingsKey.extruder_count), 1);
				}

				// normal multiple
				{
					string[] settings = new string[] { SettingsKey.extruder_count, "2", SettingsKey.extruders_share_temperature, "0" };
					Assert.AreEqual(GettProfile(settings).GetValue<int>(SettingsKey.extruder_count), 2);
				}

				// shared temp
				{
					string[] settings = new string[] { SettingsKey.extruder_count, "2", SettingsKey.extruders_share_temperature, "1" };
					Assert.AreEqual(GettProfile(settings).GetValue<int>(SettingsKey.extruder_count), 1);
				}
			}
		}

		PrinterSettings GettProfile(string[] settings)
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
