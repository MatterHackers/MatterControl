using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.AboutPage;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public class MenuOptionHelp : MenuBase
	{
		public MenuOptionHelp() : base("Help".Localize())
		{
			Name = "Help Menu";
		}

		protected override IEnumerable<MenuItemAction> GetMenuActions()
		{
			return new List<MenuItemAction>
			{
				new MenuItemAction("Forums".Localize(), () => MatterControlApplication.Instance.LaunchBrowser("https://forums.matterhackers.com/category/20/mattercontrol")),
				new MenuItemAction("Wiki".Localize(), () => MatterControlApplication.Instance.LaunchBrowser("http://wiki.mattercontrol.com")),
				new MenuItemAction("Guides and Articles".Localize(), () => MatterControlApplication.Instance.LaunchBrowser("http://www.matterhackers.com/topic/mattercontrol")),
				new MenuItemAction("Release Notes".Localize(), () => MatterControlApplication.Instance.LaunchBrowser("http://wiki.mattercontrol.com/Release_Notes")),
				new MenuItemAction("------------------------", null),
				new MenuItemAction("Report a Bug".Localize(), () => MatterControlApplication.Instance.LaunchBrowser("https://github.com/MatterHackers/MatterControl/issues")),
				new MenuItemAction("Check For Update".Localize(), () =>
				{
					ApplicationMenuRow.AlwaysShowUpdateStatus = true;
					UpdateControlData.Instance.CheckForUpdateUserRequested();
					CheckForUpdateWindow.Show();
				}),
				new MenuItemAction("------------------------", null),
				new MenuItemAction("About MatterControl".Localize(), () => AboutWindow.Show()),
			};
		}
	}
}