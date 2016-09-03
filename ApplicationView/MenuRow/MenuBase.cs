using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
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

	public abstract class MenuBase : GuiWidget
	{
		public DropDownMenu MenuDropList;
		private List<MenuItemAction> menuActions = null;

		protected abstract IEnumerable<MenuItemAction> GetMenuActions();

		public MenuBase(string menuName)
		{
			MenuDropList = new DropDownMenu(menuName.ToUpper(), Direction.Down, pointSize: 10);
			MenuDropList.Margin = new BorderDouble(0);
			MenuDropList.Padding = new BorderDouble(4, 4, 0, 4);
			MenuDropList.MenuItemsPadding = new BorderDouble(8, 4);

			MenuDropList.DrawDirectionalArrow = false;
			MenuDropList.MenuAsWideAsItems = false;

			menuActions = new List<MenuItemAction>(GetMenuActions());
			//Add the menu items to the menu itself
			foreach (MenuItemAction item in menuActions)
			{
				if (item.Title.StartsWith("-----"))
				{
					MenuDropList.AddHorizontalLine();
				}
				else
				{
					MenuItem newItem = MenuDropList.AddItem(item.Title, pointSize: 11);
					if (item.Action == null)
					{
						newItem.Enabled = false;
					}
				}
			}

			AddChild(MenuDropList);
			this.Width = GetChildrenBoundsIncludingMargins().Width;
			this.Height = 22 * GuiWidget.DeviceScale;
			this.Margin = new BorderDouble(0);
			this.Padding = new BorderDouble(0);
			this.VAnchor = Agg.UI.VAnchor.ParentCenter;
			this.MenuDropList.SelectionChanged += MenuDropList_SelectionChanged;
			this.MenuDropList.OpenOffset = new Vector2(0, 0);
		}

		private void MenuDropList_SelectionChanged(object sender, EventArgs e)
		{
			string menuSelection = ((DropDownMenu)sender).SelectedValue;
			foreach (MenuItemAction item in menuActions)
			{
				if (item.Title == menuSelection && item.Action != null)
				{
					UiThread.RunOnIdle(item.Action);
				}
			}
		}
	}
}