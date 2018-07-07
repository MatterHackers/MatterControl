using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.Tests.Automation;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	class PrinterChooserUnitTests
	{
		[Test]
		public void PrinterChooserHonorsWhitelist()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var manufacturers = new string[] { "3D Factory", "3D Stuffmaker", "Airwolf 3D", "BCN", "BeeVeryCreative", "Blue Eagle Labs", "Deezmaker", "FlashForge", "gCreate", "IRA3D", "JumpStart", "Leapfrog", "Lulzbot", "MAKEiT", "Maker's Tool Works", "MakerBot", "MakerGear", "Me3D", "OpenBeam", "Organic Thinking System", "Other", "Portabee", "Printrbot", "PrintSpace", "Revolution 3D Printers", "ROBO 3D", "SeeMeCNC", "Solidoodle", "Tosingraf", "Type A Machines", "Ultimaker", "Velleman", "Wanhao" };

			var allManufacturers = manufacturers.Select(m => new KeyValuePair<string, string>(m, m)).ToList();

			BoundDropList dropList;

			// Whitelist on non-OEM builds should contain all printers
			dropList = new BoundDropList("Test");
			dropList.ListSource = allManufacturers;
			Assert.Greater(dropList.MenuItems.Count, 20);

			var whitelist = new List<string> { "3D Stuffmaker" };

			OemSettings.Instance.SetManufacturers(allManufacturers, whitelist);

			dropList = new BoundDropList("Test");
			dropList.ListSource = OemSettings.Instance.AllOems;
			Assert.AreEqual(1, dropList.MenuItems.Count);

			whitelist.Add("Airwolf 3D");
			OemSettings.Instance.SetManufacturers(allManufacturers, whitelist);

			dropList = new BoundDropList("Test");
			dropList.ListSource = OemSettings.Instance.AllOems;
			Assert.AreEqual(2, dropList.MenuItems.Count);

			/* 
			 * Disable Esagono tests
			 * 
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
			*/
		}
	}
}
