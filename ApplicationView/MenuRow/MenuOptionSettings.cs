using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.VectorMath;
using System;
using System.IO;

namespace MatterHackers.MatterControl
{
	public class MenuOptionSettings : MenuBase
	{
		static public PopOutTextTabWidget sliceSettingsPopOut = null;
		static public PopOutTextTabWidget controlsPopOut = null;

		public MenuOptionSettings()
			: base("Settings".Localize())
		{
		}

		override protected TupleList<string, Func<bool>> GetMenuItems()
		{
			return new TupleList<string, Func<bool>>
            {
                {LocalizedString.Get("Settings"), openPrintingPannel_Click},
                {LocalizedString.Get("Controls"), openControlsPannel_Click},
				{LocalizedString.Get("Show Terminal"), openTermanialPannel_Click},
            };
		}

		private bool openPrintingPannel_Click()
		{
			UiThread.RunOnIdle((state) =>
			{
				if (sliceSettingsPopOut != null)
				{
					sliceSettingsPopOut.ShowInWindow();
				}
			});
			return true;
		}

		private bool openControlsPannel_Click()
		{
			UiThread.RunOnIdle((state) =>
			{
				if (controlsPopOut != null)
				{
					controlsPopOut.ShowInWindow();
				}
			});
			return true;
		}

		private bool openTermanialPannel_Click()
		{
			UiThread.RunOnIdle((state) =>
			{
				TerminalWindow.Show();
			});
			return true;
		}
	}
}