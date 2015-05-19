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
	public class MenuOptionSettings : GuiWidget
	{
		public DropDownMenu MenuDropList;
		private TupleList<string, Func<bool>> menuItems;

		static public PopOutTextTabWidget sliceSettingsPopOut = null;
		static public PopOutTextTabWidget controlsPopOut = null;

		public MenuOptionSettings()
		{
			MenuDropList = new DropDownMenu("Settings".Localize().ToUpper(), Direction.Down, pointSize: 10);
			MenuDropList.MenuItemsPadding = new BorderDouble(0);
			MenuDropList.Margin = new BorderDouble(0);
			MenuDropList.Padding = new BorderDouble(0);

			SetMenuItems();

			AddChild(MenuDropList);
			this.Width = 84 * TextWidget.GlobalPointSizeScaleRatio; ;
			this.Height = 22 * TextWidget.GlobalPointSizeScaleRatio; ;
			this.Margin = new BorderDouble(0);
			this.Padding = new BorderDouble(0);
			this.VAnchor = Agg.UI.VAnchor.ParentCenter;
			this.MenuDropList.SelectionChanged += new EventHandler(MenuDropList_SelectionChanged);
			this.MenuDropList.OpenOffset = new Vector2(0, 0);
		}

		private void MenuDropList_SelectionChanged(object sender, EventArgs e)
		{
			string menuSelection = ((DropDownMenu)sender).SelectedValue;
			foreach (Tuple<string, Func<bool>> item in menuItems)
			{
				if (item.Item1 == menuSelection)
				{
					if (item.Item2 != null)
					{
						item.Item2();
					}
				}
			}
		}

		private void SetMenuItems()
		{
			menuItems = new TupleList<string, Func<bool>>
            {
                {LocalizedString.Get("Printing"), openPrintingPannel_Click},
                {LocalizedString.Get("Controls"), openControlsPannel_Click},
				{LocalizedString.Get("Show Terminal"), openTermanialPannel_Click},
            };

			BorderDouble padding = MenuDropList.MenuItemsPadding;
			//Add the menu items to the menu itself
			foreach (Tuple<string, Func<bool>> item in menuItems)
			{
				MenuDropList.MenuItemsPadding = new BorderDouble(8, 4, 8, 4) * TextWidget.GlobalPointSizeScaleRatio;
				MenuDropList.AddItem(item.Item1, pointSize: 10);
			}
			MenuDropList.Padding = padding;
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