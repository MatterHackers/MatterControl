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
    public class MenuOptionFile : GuiWidget
    {
        public DropDownMenu MenuDropList;
        private TupleList<string, Func<bool>> menuItems;

        public MenuOptionFile()
        {
            MenuDropList = new DropDownMenu("File".Localize().ToUpper(), Direction.Down,pointSize:10);        
            MenuDropList.MenuItemsPadding = new BorderDouble(0);
            MenuDropList.Margin = new BorderDouble(0);
            MenuDropList.Padding = new BorderDouble(0);

            SetMenuItems();

            AddChild(MenuDropList);
            this.Width = 44 * TextWidget.GlobalPointSizeScaleRatio;;
            this.Height = 22 * TextWidget.GlobalPointSizeScaleRatio;;
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
                {LocalizedString.Get("Add Printer"), addPrinter_Click},
                {LocalizedString.Get("Add File"), importFile_Click},
				{LocalizedString.Get("Exit"), exit_Click},
            };

            BorderDouble padding = MenuDropList.MenuItemsPadding;
            //Add the menu items to the menu itself
            foreach (Tuple<string, Func<bool>> item in menuItems)
            {
                MenuDropList.MenuItemsPadding = new BorderDouble(8,4,8,4) * TextWidget.GlobalPointSizeScaleRatio;
                MenuDropList.AddItem(item.Item1,pointSize:10);
            }            
            MenuDropList.Padding = padding;
        }


        bool addPrinter_Click()
        {
            UiThread.RunOnIdle((state) =>
            { 
                ConnectionWindow.Show();
            });
            return true;
        }
			
        bool importFile_Click()
        {
            UiThread.RunOnIdle((state) =>
            {
                FileDialog.OpenFileDialog(
                    new OpenFileDialogParams(ApplicationSettings.OpenPrintableFileParams)
                    {
                        MultiSelect = true,
                        ActionButtonLabel = "Add to Queue",
                        Title = "MatterControl: Select A File"
                    },
                    (openParams) =>
                    {
                        if (openParams.FileNames != null)
                        {
                            foreach (string loadedFileName in openParams.FileNames)
                            {
                                QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileNameWithoutExtension(loadedFileName), Path.GetFullPath(loadedFileName))));
                            }
                        }

                    });

            });
            return true;
        }

        string cannotExitWhileActiveMessage = "Oops! You cannot exit while a print is active.".Localize();
        string cannotExitWhileActiveTitle = "Unable to Exit";
		bool exit_Click()
        {
            UiThread.RunOnIdle((state) =>
            {                
                GuiWidget parent = this;
                while (parent as MatterControlApplication == null)
                {
                    parent = parent.Parent;
                }

				if(PrinterConnectionAndCommunication.Instance.PrinterIsPrinting)
				{
						StyledMessageBox.ShowMessageBox(null, cannotExitWhileActiveMessage, cannotExitWhileActiveTitle);
				}
				else
				{

                	MatterControlApplication app = parent as MatterControlApplication;
               	 	app.RestartOnClose = false;
                	app.Close();

				}

            });
			return true;
        }    
    }
}
