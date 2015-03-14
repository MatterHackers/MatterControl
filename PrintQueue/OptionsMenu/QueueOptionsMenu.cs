/*
Copyright (c) 2014, Kevin Pope
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met: 

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer. 
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution. 

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies, 
either expressed or implied, of the FreeBSD Project.
*/

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
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.Agg.PlatformAbstract;

namespace MatterHackers.MatterControl.PrintQueue
{
    public class QueueOptionsMenu : GuiWidget
    {
        public DropDownMenu MenuDropList;
        private TupleList<string, Func<bool>> menuItems;

		ExportToFolderFeedbackWindow exportingWindow = null;

        public QueueOptionsMenu()
        {
			MenuDropList = new DropDownMenu("Options   ".Localize(), Direction.Up);
            MenuDropList.VAnchor = VAnchor.ParentBottomTop;
            MenuDropList.BorderWidth = 1;
            MenuDropList.MenuAsWideAsItems = false;
            MenuDropList.BorderColor = ActiveTheme.Instance.SecondaryTextColor;
            MenuDropList.Margin = new BorderDouble(4, 0, 1, 0);
			MenuDropList.AlignToRightEdge = true;
            
			SetMenuItems();
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
            menuItems = new TupleList<string, Func<bool>>();
			menuItems.Add(new Tuple<string,Func<bool>>("Design".Localize(), null));
            menuItems.Add(new Tuple<string,Func<bool>>(" Export to Zip".Localize(), exportQueueToZipMenu_Click));
            menuItems.Add(new Tuple<string,Func<bool>>("GCode", null));
			menuItems.Add(new Tuple<string, Func<bool>>(" Export to Folder or SD Card".Localize(), exportGCodeToFolderButton_Click));
            //menuItems.Add(new Tuple<string, Func<bool>>("X3G", null));
			//menuItems.Add(new Tuple<string, Func<bool>>("Export to Folder".Localize(), exportX3GButton_Click));

            if (ActiveSliceSettings.Instance.HasSdCardReader())
            {
                menuItems.Add(new Tuple<string, Func<bool>>("SD Card", null));
				menuItems.Add(new Tuple<string, Func<bool>>(" Load Files".Localize(), loadFilesFromSDButton_Click));
				menuItems.Add(new Tuple<string, Func<bool>>(" Eject SD Card".Localize(), ejectSDCardButton_Click));
            }
            
            // The pdf export library is not working on the mac at the moment so we don't include the 
            // part sheet export option on mac.
			if (OsInformation.OperatingSystem == OSType.Windows)
            {
                // mac cannot export to pdf
				menuItems.Add(new Tuple<string, Func<bool>>("Other".Localize(), null));
				menuItems.Add(new Tuple<string, Func<bool>>(" Create Part Sheet".Localize(), createPartsSheetsButton_Click));
				menuItems.Add(new Tuple<string, Func<bool>>(" Remove All".Localize(), removeAllFromQueueButton_Click));
            }
            else
            {
				menuItems.Add(new Tuple<string, Func<bool>>("Other".Localize(), null));
				menuItems.Add(new Tuple<string, Func<bool>>(" Remove All".Localize(), removeAllFromQueueButton_Click));
            }

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
			#if !__ANDROID__
			List<PrintItem> parts = QueueData.Instance.CreateReadOnlyPartList();
            if (parts.Count > 0)
            {
                FileDialog.SaveFileDialog(
                    new SaveFileDialogParams("Save Parts Sheet|*.pdf")
                    {
                        ActionButtonLabel = "Save Parts Sheet".Localize(),
                        Title = string.Format("{0}: {1}", "MatterControl".Localize(), "Save".Localize())
                    },
                    (saveParams) =>
                    {
                        if (saveParams.FileName != null)
                        {
                            PartsSheet currentPartsInQueue = new PartsSheet(parts, saveParams.FileName);

                            currentPartsInQueue.SaveSheets();

                            SavePartsSheetFeedbackWindow feedbackWindow = new SavePartsSheetFeedbackWindow(parts.Count, parts[0].Name, ActiveTheme.Instance.PrimaryBackgroundColor);
                            currentPartsInQueue.UpdateRemainingItems += feedbackWindow.StartingNextPart;
                            currentPartsInQueue.DoneSaving += feedbackWindow.DoneSaving;

                            feedbackWindow.ShowAsSystemWindow();
                        }
                    });

            }
			#endif
        }

        string pleaseSelectPrinterMessage = "Before you can export printable files, you must select a printer.";
        string pleaseSelectPrinterTitle = "Please select a printer";
        void MustSelectPrinterMessage(object state)
        {
            StyledMessageBox.ShowMessageBox(null, pleaseSelectPrinterMessage, pleaseSelectPrinterTitle);
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

		bool exportX3GButton_Click()
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
			selectParams.ActionButtonLabel = "Export".Localize();
            selectParams.Title = "MatterControl: Select A Folder";

            FileDialog.SelectFolderDialog(selectParams, onSelectFolderDialog);
        }

        private void onSelectFolderDialog(SelectFolderDialogParams openParams)
        {
            string path = openParams.FolderPath;
            if (path != null && path != "")
            {
                List<PrintItem> parts = QueueData.Instance.CreateReadOnlyPartList();
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
            List<PrintItem> partList = QueueData.Instance.CreateReadOnlyPartList();
            ProjectFileHandler project = new ProjectFileHandler(partList);
            project.SaveAs();
        }

        bool loadFilesFromSDButton_Click()
        {
            QueueData.Instance.LoadFilesFromSD();
            return true;
        }

        bool ejectSDCardButton_Click()
        {
            // Remove all the QueueData.SdCardFileName parts from the queue
            QueueData.Instance.RemoveAllSdCardFiles();
            PrinterConnectionAndCommunication.Instance.SendLineToPrinterNow("M22"); // (Release SD card)
            return true;
        }

		bool removeAllFromQueueButton_Click()
		{
			UiThread.RunOnIdle (removeAllPrintsFromQueue);
			return true;
		}

		void removeAllPrintsFromQueue (object state)
		{
			QueueData.Instance.RemoveAll();
		}
    }
}
