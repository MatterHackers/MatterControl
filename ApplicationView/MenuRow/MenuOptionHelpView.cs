using System;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.VectorMath;

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
            this.Width = 48 * TextWidget.GlobalPointSizeScaleRatio;
            this.Height = 22 * TextWidget.GlobalPointSizeScaleRatio;
            this.Margin = new BorderDouble(0);
            this.Padding = new BorderDouble(0);
            this.VAnchor = Agg.UI.VAnchor.ParentCenter;
            this.MenuDropList.SelectionChanged += new EventHandler(MenuDropList_SelectionChanged);
            this.MenuDropList.OpenOffset = new Vector2(0, 0);
        }

        void MenuDropList_SelectionChanged(object sender, EventArgs e)
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

        void SetMenuItems()
        {
            menuItems = new TupleList<string, Func<bool>> 
            {                
                {LocalizedString.Get("Simple"), simple_Click},
                {LocalizedString.Get("Intermediat"), intermediate_Click},
                {LocalizedString.Get("Advanced"), advanced_Click},
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


        bool advanced_Click()
        {
            return true;
        }

        bool intermediate_Click()
        {
            return true;
        }

        bool simple_Click()
        {
            return true;
        }
    }
}
