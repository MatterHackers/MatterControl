using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;

namespace MatterHackers.MatterControl
{
	public class MenuOptionSettings : MenuBase
	{
		static public PopOutTextTabWidget sliceSettingsPopOut = null;
		static public PopOutTextTabWidget controlsPopOut = null;

		public MenuOptionSettings() : base("View".Localize())
		{
		}

		protected override IEnumerable<MenuItemAction> GetMenuActions()
		{
			return new List<MenuItemAction>
			{
				new MenuItemAction("Settings".Localize(), () => sliceSettingsPopOut?.ShowInWindow()),
				new MenuItemAction("Controls".Localize(), () => controlsPopOut?.ShowInWindow()),
				new MenuItemAction("Terminal".Localize(), () => UiThread.RunOnIdle(TerminalWindow.Show)),
			};
		}
	}
}