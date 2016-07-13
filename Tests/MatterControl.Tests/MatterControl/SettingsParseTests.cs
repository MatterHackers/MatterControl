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
		public void SupportInterfaceMaterialAssignedToExtruderOne()
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

		SettingsProfile GettProfile(string[] settings)
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			for(int i=0; i<settings.Length; i+=2)
			{
				dictionary.Add(settings[i], settings[i + 1]);
			}
			var profile = new SettingsProfile(new PrinterSettings()
			{
				OemLayer = new PrinterSettingsLayer(dictionary)
			});

			return profile;
		}
	}
}
