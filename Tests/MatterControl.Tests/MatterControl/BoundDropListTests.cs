﻿using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.Tests.Automation;
using Xunit;

namespace MatterControl.Tests.MatterControl
{
	public class BoundDropListTests
	{
		[StaFact]
		public void BoundDropListHonorsWhitelist()
		{
			StaticData.RootPath = MatterControlUtilities.StaticDataPath;
			MatterControlUtilities.OverrideAppDataLocation(MatterControlUtilities.RootPath);

			var manufacturers = new string[] { "3D Factory", "3D Stuffmaker", "Airwolf 3D", "BCN3D", "BeeVeryCreative", "Blue Eagle Labs", 
				"Deezmaker", "FlashForge", "gCreate", "IRA3D", "JumpStart", "Leapfrog", "Lulzbot", "MAKEiT", "Maker's Tool Works",
				"MakerBot", "MakerGear", "Me3D", "OpenBeam", "Organic Thinking System", "Other", "Portabee", "Printrbot", "PrintSpace",
				"Revolution 3D Printers", "ROBO 3D", "SeeMeCNC", "Solidoodle", "Tosingraf", "Type A Machines", "Ultimaker", "Velleman",
				"Wanhao" };

			var allManufacturers = manufacturers.Select(m => new KeyValuePair<string, string>(m, m)).ToList();

			BoundDropList dropList;

			var theme = new ThemeConfig();

			// Whitelist on non-OEM builds should contain all printers
			dropList = new BoundDropList("Test", theme);
			dropList.ListSource = allManufacturers;
			Assert.True(dropList.MenuItems.Count > 20);

			var whitelist = new List<string> { "3D Stuffmaker" };

			OemSettings.Instance.SetManufacturers(allManufacturers, whitelist);

			dropList = new BoundDropList("Test", theme);
			dropList.ListSource = OemSettings.Instance.AllOems;
			Assert.Single(dropList.MenuItems);

			whitelist.Add("Airwolf 3D");
			OemSettings.Instance.SetManufacturers(allManufacturers, whitelist);

			dropList = new BoundDropList("Test", theme);
			dropList.ListSource = OemSettings.Instance.AllOems;
			Assert.Equal(2, dropList.MenuItems.Count);
		}
	}
}
