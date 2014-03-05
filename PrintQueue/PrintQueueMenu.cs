using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Threading;

using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.PrintQueue
{
    public class PrintQueueMenu : GuiWidget
    {
        public DropDownMenu MenuDropList;
        private TupleList<string, Func<bool>> menuItems;

		ExportToFolderFeedbackWindow exportingWindow = null;

        public PrintQueueMenu()
        {
			MenuDropList = new DropDownMenu(new LocalizedString("Queue Options").Translated, Direction.Up);
            MenuDropList.HAnchor |= HAnchor.ParentLeft;
            MenuDropList.VAnchor |= VAnchor.ParentTop;
            SetMenuItems();

            AddChild(MenuDropList);
            this.Width = MenuDropList.Width;
            this.Height = MenuDropList.Height;
            this.Margin = new BorderDouble(4,0);
            this.Padding = new BorderDouble(0);
            this.MenuDropList.SelectionChanged += new EventHandler(MenuDropList_SelectionChanged);
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
            //Set the name and callback function of the menu items
            menuItems = new TupleList<string, Func<bool>> 
            {
				{"STL", null},
				{new LocalizedString(" Import from Zip").Translated, importQueueFromZipMenu_Click},
				{new LocalizedString(" Export to Zip").Translated, exportQueueToZipMenu_Click},
				{"GCode", null},
				{new LocalizedString(" Export to Folder").Translated, exportGCodeToFolderButton_Click},
				{new LocalizedString("Extra").Translated, null},
				{new LocalizedString(" Create Part Sheet").Translated, createPartsSheetsButton_Click},
            };

            BorderDouble padding = MenuDropList.MenuItemsPadding;
            //Add the menu items to the menu itself
            foreach (Tuple<string, Func<bool>> item in menuItems)
            {
                if (item.Item2 == null)
                {
                    MenuDropList.MenuItemsPadding = new BorderDouble(5, 0, padding.Right, 3);
                }
                else
                {
                    MenuDropList.MenuItemsPadding = new BorderDouble(10, 5, padding.Right, 5);
                }

                MenuDropList.AddItem(item.Item1);
            }

            MenuDropList.Padding = padding;
        }

        bool createPartsSheetsButton_Click()
        {
            UiThread.RunOnIdle(PartSheetClickOnIdle);
            return true;
        }

        void PartSheetClickOnIdle(object state)
        {
            List<PrintItem> parts = PrintQueueControl.Instance.CreateReadOnlyPartList();
            if (parts.Count > 0)
            {
                SaveFileDialogParams saveParams = new SaveFileDialogParams("Save Parts Sheet|*.pdf");
				saveParams.ActionButtonLabel = new LocalizedString("Save Parts Sheet").Translated;
				string saveParamsTitleLbl = new LocalizedString("MatterContol").Translated;
				string saveParamsTitleLblFull = new LocalizedString ("Save").Translated;
				saveParams.Title = string.Format("{0}: {1}",saveParamsTitleLbl,saveParamsTitleLblFull);

                System.IO.Stream streamToSaveTo = FileDialog.SaveFileDialog(ref saveParams);
                if (streamToSaveTo != null)
                {
                    PartsSheet currentPartsInQueue = new PartsSheet(parts, saveParams.FileName);

                    currentPartsInQueue.SaveSheets();

                    SavePartsSheetFeedbackWindow feedbackWindow = new SavePartsSheetFeedbackWindow(parts.Count, parts[0].Name, ActiveTheme.Instance.PrimaryBackgroundColor);
                    currentPartsInQueue.UpdateRemainingItems += feedbackWindow.StartingNextPart;
                    currentPartsInQueue.DoneSaving += feedbackWindow.DoneSaving;

                    feedbackWindow.ShowAsSystemWindow();
                }
            }
        }

        void MustSelectPrinterMessage(object state)
        {
            StyledMessageBox.ShowMessageBox("You must select a printer before you can export printable files.", "You must select a printer");
        }

        bool exportGCodeToFolderButton_Click()
        {
            if (ActivePrinterProfile.Instance.ActivePrinter == null)
            {
                UiThread.RunOnIdle(MustSelectPrinterMessage);
            }
            else
            {
                UiThread.RunOnIdle(SelectLocationToExportGCode);
            }

            return true;
        }

		void ExportToFolderFeedbackWindow_Closed(object sender, EventArgs e)
		{
			this.exportingWindow = null;
		}

		private void SelectLocationToExportGCode(object state)
        {
            SelectFolderDialogParams selectParams = new SelectFolderDialogParams("Select Location To Save Files");
			selectParams.ActionButtonLabel = new LocalizedString("Export").Translated;
            selectParams.Title = "MatterControl: Select A Folder";

            string path = FileDialog.SelectFolderDialog(ref selectParams);
            if (path != null && path != "")
            {
                List<PrintItem> parts = PrintQueueControl.Instance.CreateReadOnlyPartList();
                if (parts.Count > 0)
                {
                    if (exportingWindow == null)
                    {
                        exportingWindow = new ExportToFolderFeedbackWindow(parts.Count, parts[0].Name, ActiveTheme.Instance.PrimaryBackgroundColor);
                        exportingWindow.Closed += new EventHandler(ExportToFolderFeedbackWindow_Closed);
                        exportingWindow.ShowAsSystemWindow();
                    }
                    else
                    {
                        exportingWindow.BringToFront();
                    }

                    ExportToFolderProcess exportToFolderProcess = new ExportToFolderProcess(parts, path);
                    exportToFolderProcess.StartingNextPart += exportingWindow.StartingNextPart;
                    exportToFolderProcess.UpdatePartStatus += exportingWindow.UpdatePartStatus;
                    exportToFolderProcess.DoneSaving += exportingWindow.DoneSaving;
                    exportToFolderProcess.Start();
                }
            }
        }

        bool exportQueueToZipMenu_Click()
        {
            UiThread.RunOnIdle(ExportQueueToZipOnIdle);
            return true;
        }

        void ExportQueueToZipOnIdle(object state)
        {
            List<PrintItem> partList = PrintQueueControl.Instance.CreateReadOnlyPartList();
            ProjectFileHandler project = new ProjectFileHandler(partList);
            project.SaveAs();
        }

        bool importQueueFromZipMenu_Click()
        {
            UiThread.RunOnIdle(ImportQueueFromZipMenuOnIdle);
            return true;
        }
        
        void ImportQueueFromZipMenuOnIdle(object state)
        {
            ProjectFileHandler project = new ProjectFileHandler(null);
            List<PrintItem> partFiles = project.OpenFromDialog();
            if (partFiles != null)
            {
                PrintQueueControl.Instance.RemoveAllChildren();
                foreach (PrintItem part in partFiles)
                {
                    PrintQueueControl.Instance.AddChild(new PrintQueueItem(part.Name, part.FileLocation));
                }
                PrintQueueControl.Instance.EnsureSelection();
                PrintQueueControl.Instance.Invalidate();
                PrintQueueControl.Instance.SaveDefaultQueue();
            }
        }

        void deleteFromQueueButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrintQueueControl.Instance.RemoveIndex(PrintQueueControl.Instance.SelectedIndex);
            PrintQueueControl.Instance.SaveDefaultQueue();
        }

        void deleteAllFromQueueButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrintQueueControl.Instance.RemoveAllChildren();
            PrintQueueControl.Instance.SaveDefaultQueue();
        }
    }
}
