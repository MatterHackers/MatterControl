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
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.MatterControl.Tests.Automation;

namespace MatterControl.Tests.MatterControl
{
	class PrinterChooserUnitTests
	{
		[Test]
		public void PrinterChooserHonorsWhitelist()
		{
			StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData(Path.Combine("..", "..", "..", "..", "StaticData"));

			MatterControlUtilities.OverrideAppDataLocation();

			// Whitelist on non-OEM builds should contain all printers
			var printChooser = new PrinterChooser();
			
			Assert.IsTrue(printChooser.CountOfMakes > 15);

			// Set private member to override settings.json values for this test
			SetPrivatePrinterWhiteListMember(new List<string>() { "3D Stuffmaker" });
			printChooser = new PrinterChooser();
			Assert.IsTrue(printChooser.CountOfMakes == 1);

			SetPrivatePrinterWhiteListMember(new List<string>() { "Airwolf 3D", "3D Stuffmaker" });
			printChooser = new PrinterChooser();
			Assert.IsTrue(printChooser.CountOfMakes == 2);

			SetPrivatePrinterWhiteListMember(new List<string>() { "Esagono" });
			var manufacturerNameMapping = new ManufacturerNameMapping();
			manufacturerNameMapping.NameOnDisk = "Esagono";
			manufacturerNameMapping.NameToDisplay = "Esagonò";
			printChooser = new PrinterChooser();

			string expectedItem = null;
			foreach (var menuItem in printChooser.ManufacturerDropList.MenuItems)
			{
				if(menuItem.Text.StartsWith("Esa"))
				{
					expectedItem = menuItem.Text;
				}
			}
			Assert.IsTrue(!string.IsNullOrEmpty(expectedItem) && expectedItem == "Esagonò");
		}

		private static void SetPrivatePrinterWhiteListMember(List<string> newValue)
		{
			var fieldInfo = typeof(OemSettings).GetField("printerWhiteList", BindingFlags.Instance | BindingFlags.NonPublic);
			fieldInfo.SetValue(OemSettings.Instance, newValue);
		}
	}
}
