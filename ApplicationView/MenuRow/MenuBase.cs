using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public abstract class MenuBase : GuiWidget
	{
		public class MenuItemAction
		{
			public MenuItemAction(string title, Action action)
			{
				this.Title = title;
				this.Action = action;
			}

			public string Title { get; set; }
			public Action Action { get; set; }
		}

		public DropDownMenu MenuDropList;
		private List<MenuItemAction> menuItems = null;

		protected abstract IEnumerable<MenuItemAction> GetMenuItems();

		public MenuBase(string menuName)
		{
			MenuDropList = new DropDownMenu(menuName.ToUpper(), Direction.Down, pointSize: 10);
			MenuDropList.MenuItemsPadding = new BorderDouble(0);
			MenuDropList.Margin = new BorderDouble(0);
			MenuDropList.Padding = new BorderDouble(0);

			MenuDropList.DrawDirectionalArrow = false;
			MenuDropList.MenuAsWideAsItems = false;

			menuItems = new List<MenuItemAction>(GetMenuItems());
			BorderDouble padding = MenuDropList.MenuItemsPadding;
			//Add the menu items to the menu itself
			foreach (MenuItemAction item in menuItems)
			{
				MenuDropList.MenuItemsPadding = new BorderDouble(8, 6, 8, 6) * TextWidget.GlobalPointSizeScaleRatio;
				MenuItem newItem = MenuDropList.AddItem(item.Title, pointSize: 11);
				if (item.Action == null)
				{
					newItem.Enabled = false;
				}
			}
			MenuDropList.Padding = padding;

			AddChild(MenuDropList);
			this.Width = GetChildrenBoundsIncludingMargins().Width;
			this.Height = 22 * TextWidget.GlobalPointSizeScaleRatio;
			this.Margin = new BorderDouble(0);
			this.Padding = new BorderDouble(0);
			this.VAnchor = Agg.UI.VAnchor.ParentCenter;
			this.MenuDropList.SelectionChanged += MenuDropList_SelectionChanged;
			this.MenuDropList.OpenOffset = new Vector2(0, 0);
		}

		private void MenuDropList_SelectionChanged(object sender, EventArgs e)
		{
			string menuSelection = ((DropDownMenu)sender).SelectedValue;
			foreach (MenuItemAction item in menuItems)
			{
				if (item.Title == menuSelection && item.Action != null)
				{
					UiThread.RunOnIdle(item.Action);
				}
			}
		}
	}
}