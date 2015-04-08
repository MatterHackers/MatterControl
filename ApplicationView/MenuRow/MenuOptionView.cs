using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl
{
	public class MenuOptionView : GuiWidget
	{
		public DropDownMenu MenuDropList;
		private TupleList<string, Func<bool>> menuItems;

		public MenuOptionView()
		{
			MenuDropList = new DropDownMenu("View".Localize().ToUpper(), Direction.Down, pointSize: 10);
			MenuDropList.MenuItemsPadding = new BorderDouble(0);
			MenuDropList.Margin = new BorderDouble(0);
			MenuDropList.Padding = new BorderDouble(0);

			SetMenuItems();

			AddChild(MenuDropList);
			this.Width = 44 * TextWidget.GlobalPointSizeScaleRatio; ;
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
                {LocalizedString.Get("Layout 1"), layout1_Click},
                {LocalizedString.Get("Layout 2"), layout2_Click},
				//{LocalizedString.Get("Layout 3"), layout3_Click},
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

		private int widthAdjust = -14;
		private int heightAdjust = -35;

		private bool layout1_Click()
		{
			//			UiThread.RunOnIdle((state) =>
			//			{
			//				double width = System.Windows.SystemParameters.FullPrimaryScreenWidth;
			//				double height = System.Windows.SystemParameters.FullPrimaryScreenHeight;
			//
			//				MatterControlApplication.Instance.DesktopPosition = new Point2D(0, 0);
			//				MatterControlApplication.Instance.Width = width / 3;
			//				MatterControlApplication.Instance.Height = height;
			//
			//				PopOutManager.SetStates(ThirdPanelTabView.SliceSettingsTabName, true, width / 3, height + heightAdjust, width / 3 * 2 + widthAdjust, 0);
			//				PopOutManager.SetStates(ThirdPanelTabView.ControlsTabName, false, width / 3, height / 2 + heightAdjust, width / 3 * 2 + widthAdjust, height / 2);
			//				ApplicationController.Instance.ReloadAll(null, null);
			//
			//			});
			return true;
		}

		private bool layout2_Click()
		{
			//			UiThread.RunOnIdle((state) =>
			//			{
			//				double width = System.Windows.SystemParameters.PrimaryScreenWidth;
			//				double height = System.Windows.SystemParameters.PrimaryScreenHeight;
			//
			//				MatterControlApplication.Instance.DesktopPosition = new Point2D(0, 0);
			//				MatterControlApplication.Instance.Width = width / 3;
			//				MatterControlApplication.Instance.Height = height;
			//
			//				PopOutManager.SetStates(ThirdPanelTabView.SliceSettingsTabName, true, width / 3, height / 2 + heightAdjust, width / 3 * 2 + widthAdjust, 0);
			//				PopOutManager.SetStates(ThirdPanelTabView.ControlsTabName, true, width / 3, height / 2 + heightAdjust * 2, width / 3 * 2 + widthAdjust, height / 2);
			//				ApplicationController.Instance.ReloadAll(null, null);
			//			});
			return true;
		}
	}
}