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
			: base("View".Localize())
		{
			
		}

		override protected TupleList<string, Func<bool>> GetMenuItems()
		{
			return new TupleList<string, Func<bool>>
			{
				{"Settings".Localize(), openPrintingPanel_Click},
				{"Controls".Localize(), openControlsPanel_Click},
				{"Terminal".Localize(), openTermanialPanel_Click},
			};
		}

		private bool openPrintingPanel_Click()
		{
			UiThread.RunOnIdle(() =>
			{
				if (sliceSettingsPopOut != null)
				{
					sliceSettingsPopOut.ShowInWindow();
				}
			});
			return true;
		}

		private bool openControlsPanel_Click()
		{
			UiThread.RunOnIdle(() =>
			{
				if (controlsPopOut != null)
				{
					controlsPopOut.ShowInWindow();
				}
			});
			return true;
		}

		private bool openTermanialPanel_Click()
		{
			UiThread.RunOnIdle(TerminalWindow.Show);
			return true;
		}
	}
}