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

		protected override IEnumerable<MenuItemAction> GetMenuItems()
		{
			return new List<MenuItemAction>
			{
				new MenuItemAction("Getting Started".Localize(), () => MatterControlApplication.Instance.LaunchBrowser("http://www.mattercontrol.com/articles/mattercontrol-getting-started")),
				new MenuItemAction("View Help".Localize(), () => MatterControlApplication.Instance.LaunchBrowser("http://www.mattercontrol.com/articles")),
				new MenuItemAction("Release Notes".Localize(), () => MatterControlApplication.Instance.LaunchBrowser("http://wiki.mattercontrol.com/Release_Notes")),
				new MenuItemAction("User Manual".Localize(), () => MatterControlApplication.Instance.LaunchBrowser("http://wiki.mattercontrol.com")),
				new MenuItemAction("------------------------", null),
				new MenuItemAction("Report a Bug".Localize(), () => ContactFormWindow.Open()),
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