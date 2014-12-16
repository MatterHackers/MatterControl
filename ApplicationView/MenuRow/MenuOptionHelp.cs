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
    public class MenuOptionHelp : GuiWidget
    {
        public DropDownMenu MenuDropList;
        private TupleList<string, Func<bool>> menuItems;

        public MenuOptionHelp()
        {
            MenuDropList = new DropDownMenu("Help".Localize().ToUpper(), Direction.Down, pointSize: 10);
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
                {LocalizedString.Get("Getting Started"), gettingStarted_Click},
                {LocalizedString.Get("View Help"), help_Click},
                {LocalizedString.Get("Report a Bug"), bug_Click},
				{LocalizedString.Get("Release Notes"), notes_Click},
                {LocalizedString.Get("About MatterControl"), about_Click},
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


        bool bug_Click()
        {
            
            UiThread.RunOnIdle((state) =>
            {
                ContactFormWindow.Open();
            });
            return true;
        }

        bool help_Click()
        {
            UiThread.RunOnIdle((state) =>
            {   
                System.Diagnostics.Process.Start("http://www.mattercontrol.com/articles");
            });
            return true;
        }

        bool about_Click()
        {
            UiThread.RunOnIdle((state) =>
            {
                AboutWindow.Show();
            });            
            return true;
        }

		bool notes_Click()
		{
			UiThread.RunOnIdle((state) =>
				{
					System.Diagnostics.Process.Start("http://wiki.mattercontrol.com/Release_Notes");
				});            
			return true;
		}

        bool gettingStarted_Click()
        {
            UiThread.RunOnIdle((state) =>
            {
                System.Diagnostics.Process.Start("http://www.mattercontrol.com/articles/mattercontrol-getting-started");
            });
            
            return true;
        }
    }
}
