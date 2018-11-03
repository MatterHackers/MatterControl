using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.Tests.Automation;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	class BoundDropListTests
	{
		[Test]
		public void BoundDropListHonorsWhitelist()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var manufacturers = new string[] { "3D Factory", "3D Stuffmaker", "Airwolf 3D", "BCN", "BeeVeryCreative", "Blue Eagle Labs", "Deezmaker", "FlashForge", "gCreate", "IRA3D", "JumpStart", "Leapfrog", "Lulzbot", "MAKEiT", "Maker's Tool Works", "MakerBot", "MakerGear", "Me3D", "OpenBeam", "Organic Thinking System", "Other", "Portabee", "Printrbot", "PrintSpace", "Revolution 3D Printers", "ROBO 3D", "SeeMeCNC", "Solidoodle", "Tosingraf", "Type A Machines", "Ultimaker", "Velleman", "Wanhao" };

			var allManufacturers = manufacturers.Select(m => new KeyValuePair<string, string>(m, m)).ToList();

			BoundDropList dropList;

			var theme = new ThemeConfig();

			// Whitelist on non-OEM builds should contain all printers
			dropList = new BoundDropList("Test", theme);
			dropList.ListSource = allManufacturers;
			Assert.Greater(dropList.MenuItems.Count, 20);

			var whitelist = new List<string> { "3D Stuffmaker" };

			OemSettings.Instance.SetManufacturers(allManufacturers, whitelist);

			dropList = new BoundDropList("Test", theme);
			dropList.ListSource = OemSettings.Instance.AllOems;
			Assert.AreEqual(1, dropList.MenuItems.Count);

			whitelist.Add("Airwolf 3D");
			OemSettings.Instance.SetManufacturers(allManufacturers, whitelist);

			dropList = new BoundDropList("Test", theme);
			dropList.ListSource = OemSettings.Instance.AllOems;
			Assert.AreEqual(2, dropList.MenuItems.Count);
		}
	}
}
