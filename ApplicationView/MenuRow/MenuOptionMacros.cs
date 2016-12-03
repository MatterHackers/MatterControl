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
	public class MenuOptionMacros : MenuBase
	{
		public MenuOptionMacros() : base("Macros".Localize())
		{
			Name = "Macro Menu";
		}

		protected override IEnumerable<MenuItemAction> GetMenuActions()
		{
			return new List<MenuItemAction>
			{
				new MenuItemAction("Load Filament".Localize(), () => MatterControlApplication.Instance.LaunchBrowser("https://forums.matterhackers.com/category/20/mattercontrol")),
				new MenuItemAction("UnloadFilament".Localize(), () => MatterControlApplication.Instance.LaunchBrowser("http://wiki.mattercontrol.com")),
			};
		}
	}
}