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

		public MenuOptionSettings()
			: base("View".Localize())
		{
			
		}

		override protected IEnumerable<MenuItemAction> GetMenuItems()
		{
			return new List<MenuItemAction>
            {
				new MenuItemAction("Settings".Localize(), openPrintingPannel_Click),
				new MenuItemAction("Controls".Localize(), openControlsPannel_Click),
				new MenuItemAction("Terminal".Localize(), openTermanialPannel_Click),
            };
		}

		private void openPrintingPannel_Click()
		{
			UiThread.RunOnIdle(() =>
			{
				if (sliceSettingsPopOut != null)
				{
					sliceSettingsPopOut.ShowInWindow();
				}
			});
		}

		private void openControlsPannel_Click()
		{
			UiThread.RunOnIdle(() =>
			{
				if (controlsPopOut != null)
				{
					controlsPopOut.ShowInWindow();
				}
			});
		}

		private void openTermanialPannel_Click()
		{
			UiThread.RunOnIdle(TerminalWindow.Show);
		}
	}
}