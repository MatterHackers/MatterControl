using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl
{
	public abstract class MenuBase : GuiWidget
	{
		public DropDownMenu MenuDropList;
		private TupleList<string, Func<bool>> menuItems = null;

		public MenuBase(string menuName)
		{
			MenuDropList = new DropDownMenu(menuName.ToUpper(), Direction.Down, pointSize: 10);
			MenuDropList.MenuItemsPadding = new BorderDouble(0);
			MenuDropList.Margin = new BorderDouble(0);
			MenuDropList.Padding = new BorderDouble(0);

			MenuDropList.DrawDirectionalArrow = false;
			MenuDropList.MenuAsWideAsItems = false;

			menuItems = GetMenuItems();
			BorderDouble padding = MenuDropList.MenuItemsPadding;
			//Add the menu items to the menu itself
			foreach (Tuple<string, Func<bool>> item in menuItems)
			{
				MenuDropList.MenuItemsPadding = new BorderDouble(8, 4, 8, 4) * TextWidget.GlobalPointSizeScaleRatio;
				MenuDropList.AddItem(item.Item1, pointSize: 10);
			}
			MenuDropList.Padding = padding;


			AddChild(MenuDropList);
			this.Width = GetChildrenBoundsIncludingMargins().Width;
			this.Height = 22 * TextWidget.GlobalPointSizeScaleRatio;
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

		abstract protected TupleList<string, Func<bool>> GetMenuItems();
	}
}