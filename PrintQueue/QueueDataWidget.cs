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

using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CreatorPlugins;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrintQueue
{
    public class QueueDataWidget : GuiWidget
    {
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        TextImageButtonFactory editButtonFactory = new TextImageButtonFactory();
        PluginChooserWindow pluginChooserWindow;
        QueueDataView queueDataView;
        Button exportItemButton;
		Button sendItemButton;
        Button copyItemButton;
        Button removeItemButton;
        Button enterEditModeButton;
        Button leaveEditModeButton;

        Button addToQueueButton;
        Button createButton;

        static Button shopButton;
        event EventHandler unregisterEvents;

		public delegate void SendButtonAction(object state, List<PrintItemWrapper> sendItems);
		public static SendButtonAction sendButtonFunction = null;

        public QueueDataWidget(QueueDataView queueDataView)
        {
            this.queueDataView = queueDataView;

            SetDisplayAttributes();

            textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.borderWidth = 0;


            editButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
            editButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
            editButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
            editButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
            editButtonFactory.borderWidth = 0;
            editButtonFactory.FixedWidth = 70;

            FlowLayoutWidget allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);
            {
                enterEditModeButton = editButtonFactory.Generate("Edit".Localize(), centerText: true);
                leaveEditModeButton = editButtonFactory.Generate("Done".Localize(), centerText: true);
                leaveEditModeButton.Visible = false;
                
                FlowLayoutWidget searchPanel = new FlowLayoutWidget();
                searchPanel.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
                searchPanel.HAnchor = HAnchor.ParentLeftRight;
                searchPanel.Padding = new BorderDouble(0);

                searchPanel.AddChild(enterEditModeButton);
                searchPanel.AddChild(leaveEditModeButton);

                searchPanel.AddChild(new HorizontalSpacer());
                
                allControls.AddChild(searchPanel);
                
                {                    
                    // Ensure the form opens with no rows selected.
                    //ActiveQueueList.Instance.ClearSelected();

                    allControls.AddChild(queueDataView);
                }

                FlowLayoutWidget buttonPanel1 = new FlowLayoutWidget();
                buttonPanel1.HAnchor = HAnchor.ParentLeftRight;
                buttonPanel1.Padding = new BorderDouble(0, 3);
                buttonPanel1.MinimumSize = new Vector2(0, 46);
                {
                    addToQueueButton = textImageButtonFactory.Generate(LocalizedString.Get("Add"), "icon_circle_plus.png");
                    buttonPanel1.AddChild(addToQueueButton);
                    addToQueueButton.Margin = new BorderDouble(0, 0, 3, 0);
                    addToQueueButton.Click += new EventHandler(addToQueueButton_Click);

                    // put in the creator button
                    {
                        createButton = textImageButtonFactory.Generate(LocalizedString.Get("Create"), "icon_creator_white_32x32.png");
                        buttonPanel1.AddChild(createButton);
                        createButton.Margin = new BorderDouble(0, 0, 3, 0);
                        createButton.Click += (sender, e) =>
                        {
                            OpenPluginChooserWindow();
                        };
                    }

					sendItemButton = textImageButtonFactory.Generate("Send".Localize());
					sendItemButton.Margin = new BorderDouble(0, 0, 3, 0);
					sendItemButton.Click += new EventHandler(sendButton_Click);
					sendItemButton.Visible = false;
					buttonPanel1.AddChild(sendItemButton);

                    exportItemButton = textImageButtonFactory.Generate("Export".Localize());
                    exportItemButton.Margin = new BorderDouble(3, 0);
                    exportItemButton.Click += new EventHandler(exportButton_Click);
                    exportItemButton.Visible = false;
                    buttonPanel1.AddChild(exportItemButton);

                    copyItemButton = textImageButtonFactory.Generate("Copy".Localize());
                    copyItemButton.Margin = new BorderDouble(3, 0);
                    copyItemButton.Click += new EventHandler(copy_Button_Click);
                    copyItemButton.Visible = false;
                    buttonPanel1.AddChild(copyItemButton);

                    removeItemButton = textImageButtonFactory.Generate("Remove".Localize());
                    removeItemButton.Margin = new BorderDouble(3, 0);
                    removeItemButton.Click += new EventHandler(removeButton_Click);
                    removeItemButton.Visible = false;
                    buttonPanel1.AddChild(removeItemButton);

                    if(OemSettings.Instance.ShowShopButton)
                    {
                        shopButton = textImageButtonFactory.Generate(LocalizedString.Get("Buy Materials"), "icon_shopping_cart_32x32.png");
                        buttonPanel1.AddChild(shopButton);
                        shopButton.Margin = new BorderDouble(0, 0, 3, 0);
                        shopButton.Click += (sender, e) =>
                        {
                            double activeFilamentDiameter = 0;
                            if(ActivePrinterProfile.Instance.ActivePrinter != null)
                            {
                                activeFilamentDiameter = 3;
                                if (ActiveSliceSettings.Instance.FilamentDiameter < 2)
                                {
                                    activeFilamentDiameter = 1.75;
                                }
                            }

                            System.Diagnostics.Process.Start("http://www.matterhackers.com/mc/store/redirect?d={0}&clk=mcs&a={1}".FormatWith(activeFilamentDiameter, OemSettings.Instance.AffiliateCode));
                        };
                    }

                    Button deleteAllFromQueueButton = textImageButtonFactory.Generate(LocalizedString.Get("Remove All"));
                    deleteAllFromQueueButton.Margin = new BorderDouble(3, 0);
                    deleteAllFromQueueButton.Click += new EventHandler(deleteAllFromQueueButton_Click);
                    //buttonPanel1.AddChild(deleteAllFromQueueButton);
                    
                    buttonPanel1.AddChild(new HorizontalSpacer());

                    queueMenuContainer = new FlowLayoutWidget();
                    queueMenuContainer.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
                    queueMenu = new QueueOptionsMenu();
                    queueMenuContainer.AddChild(queueMenu.MenuDropList);
                    buttonPanel1.AddChild(queueMenuContainer);

                    ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent((object sender, EventArgs e) =>
                    {
                        queueMenuContainer.RemoveAllChildren();
                        // the printer changed reload the queueMenue
                        queueMenu = new QueueOptionsMenu();
                        queueMenuContainer.AddChild(queueMenu.MenuDropList);
                    }, ref unregisterEvents);
                }
                allControls.AddChild(buttonPanel1);
            }
            allControls.AnchorAll();
            
            this.AddChild(allControls);
            AddHandlers();
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        QueueOptionsMenu queueMenu;
        FlowLayoutWidget queueMenuContainer;

        void AddHandlers()
        {
            queueDataView.SelectedItems.OnAdd += onLibraryItemsSelectChanged;
            queueDataView.SelectedItems.OnRemove += onLibraryItemsSelectChanged;
            
            enterEditModeButton.Click += enterEditModeButtonClick;
            leaveEditModeButton.Click += leaveEditModeButtonClick;
        }

        void enterEditModeButtonClick(object sender, EventArgs mouseEvent)
        {
            enterEditModeButton.Visible = false;
            leaveEditModeButton.Visible = true;
            queueDataView.EditMode = true;
            addToQueueButton.Visible = false;
            createButton.Visible = false;

            // Avoid setting properties on shopButton when the object is null due to app configuration
            if (OemSettings.Instance.ShowShopButton)
            {
                shopButton.Visible = false;
            }

            queueMenuContainer.Visible = false;
            SetVisibleButtons();
        }

        void leaveEditModeButtonClick(object sender, EventArgs mouseEvent)
        {
            enterEditModeButton.Visible = true;
            leaveEditModeButton.Visible = false;
            queueDataView.EditMode = false;
            addToQueueButton.Visible = true;
            createButton.Visible = true;

            // Avoid setting properties on shopButton when the object is null due to app configuration
            if (OemSettings.Instance.ShowShopButton)
            {
                shopButton.Visible = true;
            }

            queueMenuContainer.Visible = true;
            SetVisibleButtons();
        }


        private void SetDisplayAttributes()
        {
            this.Padding = new BorderDouble(3);
            this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            this.AnchorAll();
        }

        void exportButton_Click(object sender, EventArgs mouseEvent)
        {
            //Open export options
            if (queueDataView.SelectedItems.Count == 1)
            {
                QueueRowItem libraryItem = queueDataView.SelectedItems[0];
                OpenExportWindow(libraryItem.PrintItemWrapper);
            }
        }

		void sendButton_Click(object sender, EventArgs mouseEvent)
		{
			//Open export options
			List<PrintItemWrapper> itemList = this.queueDataView.SelectedItems.Select(item => item.PrintItemWrapper).ToList();
			if (sendButtonFunction != null)
			{
				UiThread.RunOnIdle((state) =>
				{
					sendButtonFunction(null, itemList);
				});
			}
			else
			{
				UiThread.RunOnIdle((state) =>
				{
					StyledMessageBox.ShowMessageBox(null,"Oops! Send is currently disabled.", "Send Print");
				});
			}
		}

        void removeButton_Click(object sender, EventArgs mouseEvent)
        {
            // Sort by index in the QueueData list to prevent positions shifting due to removes
            var sortedByIndexPos = this.queueDataView.SelectedItems.OrderByDescending(rowItem => QueueData.Instance.GetIndex(rowItem.PrintItemWrapper));

            // Once sorted, remove each selected item
            foreach (var item in sortedByIndexPos)
            {
                item.DeletePartFromQueue(null);
            }

            this.queueDataView.SelectedItems.Clear();
        }

        void copy_Button_Click(object sender, EventArgs mouseEvent)
        {
            CreateCopyInQueue();
        }

        public void CreateCopyInQueue()
        {
            // Guard for single item selection
            if (this.queueDataView.SelectedItems.Count != 1) return;

            var queueRowItem = this.queueDataView.SelectedItems[0];

            var printItem = queueRowItem.PrintItemWrapper;

            int thisIndexInQueue = QueueData.Instance.GetIndex(printItem);
            if (thisIndexInQueue != -1 && File.Exists(printItem.FileLocation))
            {
                string libraryDataPath = ApplicationDataStorage.Instance.ApplicationLibraryDataPath;
                if (!Directory.Exists(libraryDataPath))
                {
                    Directory.CreateDirectory(libraryDataPath);
                }

                string newCopyFilename;
                int infiniteBlocker = 0;
                do
                {
                    newCopyFilename = Path.Combine(libraryDataPath, Path.ChangeExtension(Path.GetRandomFileName(), Path.GetExtension(printItem.FileLocation)));
                    newCopyFilename = Path.GetFullPath(newCopyFilename);
                    infiniteBlocker++;
                } while (File.Exists(newCopyFilename) && infiniteBlocker < 100);

                File.Copy(printItem.FileLocation, newCopyFilename);

                string newName = printItem.Name;

                if (!newName.Contains(" - copy"))
                {
                    newName += " - copy";
                }
                else
                {
                    int index = newName.LastIndexOf(" - copy");
                    newName = newName.Substring(0, index) + " - copy";
                }

                int copyNumber = 2;
                string testName = newName;
                string[] itemNames = QueueData.Instance.GetItemNames();
                // figure out if we have a copy already and increment the number if we do
                while (true)
                {
                    if (itemNames.Contains(testName))
                    {
                        testName = "{0} {1}".FormatWith(newName, copyNumber);
                        copyNumber++;
                    }
                    else
                    {
                        break;
                    }
                }
                newName = testName;

                UiThread.RunOnIdle(AddPartCopyToQueue, new PartToAddToQueue()
                {
                    Name = newName,
                    FileLocation = newCopyFilename,
                    InsertAfterIndex = thisIndexInQueue + 1
                });
            }
        }

        class PartToAddToQueue
        {
            internal string Name { get; set; }
            internal string FileLocation { get; set; }
            internal int InsertAfterIndex { get; set; }
        }

        ExportPrintItemWindow exportingWindow;
        bool exportingWindowIsOpen = false;
        private void OpenExportWindow(PrintItemWrapper printItem)
        {
            if (exportingWindowIsOpen == false)
            {
                exportingWindow = new ExportPrintItemWindow(printItem);
                this.exportingWindowIsOpen = true;
                exportingWindow.Closed += (source, e) => this.exportingWindowIsOpen = false;
                exportingWindow.ShowAsSystemWindow();
            }
            else
            {
                if (exportingWindow != null)
                {
                    exportingWindow.BringToFront();
                }
            }
        }


        private void OpenPluginChooserWindow()
        {
            if (pluginChooserWindow == null)
            {
                pluginChooserWindow = new PluginChooserWindow();
                pluginChooserWindow.Closed += (sender, e) =>
                {
                    pluginChooserWindow = null;
                };
            }
            else
            {
                pluginChooserWindow.BringToFront();
            }
        }

        void createPartsSheetsButton_Click(object sender, EventArgs mouseEvent)
        {
			#if !__ANDROID__          
			List<PrintItem> parts = QueueData.Instance.CreateReadOnlyPartList();

            FileDialog.SaveFileDialog(
                new SaveFileDialogParams("Save Parts Sheet|*.pdf"),
                (saveParams) =>
                {
                    string partFileName = saveParams.FileName;

                    if (!partFileName.StartsWith("" + Path.DirectorySeparatorChar))
                    {
                        partFileName = Path.DirectorySeparatorChar + partFileName;
                    }

                    PartsSheet currentPartsInQueue = new PartsSheet(parts, partFileName);
                    currentPartsInQueue.SaveSheets();
                });

			#endif
        }

        private void onLibraryItemsSelectChanged(object sender, EventArgs e)
        {
            SetVisibleButtons();
        }

        private void SetVisibleButtons()
        {
            int selectedCount = queueDataView.SelectedItems.Count;
            if (selectedCount > 0 && queueDataView.EditMode)
            {
				sendItemButton.Visible = true;
				if (selectedCount == 1)
                {
                    exportItemButton.Visible = true;
                    copyItemButton.Visible = true;
                    removeItemButton.Visible = true;
                }
                else
                {
                    exportItemButton.Visible = false;
                    copyItemButton.Visible = false;
                    removeItemButton.Visible = true;
                }

                //addToQueueButton.Visible = false;
                //createButton.Visible = false;
            }
            else
            {
                //addToQueueButton.Visible = true;
                //createButton.Visible = true;
				sendItemButton.Visible = false;
                exportItemButton.Visible = false;
                copyItemButton.Visible = false;
                removeItemButton.Visible = false;
            }
        }


        void exportToSDProcess_UpdateRemainingItems(object sender, EventArgs e)
        {
            ExportToFolderProcess exportToSDProcess = (ExportToFolderProcess)sender;
        }

        void exportQueueButton_Click(object sender, EventArgs mouseEvent)
        {
            List<PrintItem> partList = QueueData.Instance.CreateReadOnlyPartList();
            ProjectFileHandler project = new ProjectFileHandler(partList);
            project.SaveAs();
        }

        void importQueueButton_Click(object sender, EventArgs mouseEvent)
        {
            ProjectFileHandler project = new ProjectFileHandler(null);
            throw new NotImplementedException();
#if false
            List<PrintItem> partFiles = project.OpenFromDialog();
            if (partFiles != null)
            {                
                foreach (PrintItem part in partFiles)
                {
                    QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(part.Name, part.FileLocation)));
                }
            }
#endif
        }

        void deleteAllFromQueueButton_Click(object sender, EventArgs mouseEvent)
        {
            QueueData.Instance.RemoveAll();
        }

        public override void OnDragEnter(FileDropEventArgs fileDropEventArgs)
        {
            foreach (string file in fileDropEventArgs.DroppedFiles)
            {
                string extension = Path.GetExtension(file).ToUpper();
                if (MeshFileIo.ValidFileExtensions().Contains(extension)
                    || extension == ".GCODE" 
                    || extension == ".ZIP")
                {
                    fileDropEventArgs.AcceptDrop = true;
                }
            }
            base.OnDragEnter(fileDropEventArgs);
        }

        public override void OnDragOver(FileDropEventArgs fileDropEventArgs)
        {
            foreach (string file in fileDropEventArgs.DroppedFiles)
            {
                string extension = Path.GetExtension(file).ToUpper();
                if (MeshFileIo.ValidFileExtensions().Contains(extension)
                    || extension == ".GCODE" 
                    || extension == ".ZIP")
                {
                    fileDropEventArgs.AcceptDrop = true;
                }
            }
            base.OnDragOver(fileDropEventArgs);
        }

        public override void OnDragDrop(FileDropEventArgs fileDropEventArgs)
        {
            foreach (string droppedFileName in fileDropEventArgs.DroppedFiles)
            {
                string extension = Path.GetExtension(droppedFileName).ToUpper();
                if (MeshFileIo.ValidFileExtensions().Contains(extension)
                    || extension == ".GCODE")
                {
                    QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileNameWithoutExtension(droppedFileName), Path.GetFullPath(droppedFileName))));
                }
                else if (extension == ".ZIP")
                {
                    ProjectFileHandler project = new ProjectFileHandler(null);
                    List<PrintItem> partFiles = project.ImportFromProjectArchive(droppedFileName);
                    if (partFiles != null)
                    {
                        foreach (PrintItem part in partFiles)
                        {
                            QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(part.Name, part.FileLocation)));
                        }
                    }
                }
            }

            base.OnDragDrop(fileDropEventArgs);
        }

        void addToQueueButton_Click(object sender, EventArgs mouseEvent)
        {
            UiThread.RunOnIdle(AddItemsToQueue);
        }

        void AddPartCopyToQueue(object state)
        {
            var partInfo = state as PartToAddToQueue;
            QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(partInfo.Name, partInfo.FileLocation)), QueueData.ValidateSizeOn32BitSystems.Skip, partInfo.InsertAfterIndex);
        }

        void AddItemsToQueue(object state)
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
                        foreach (string fileNameToLoad in openParams.FileNames)
                        {
                            if (Path.GetExtension(fileNameToLoad).ToUpper() == ".ZIP")
                            {
                                ProjectFileHandler project = new ProjectFileHandler(null);
                                List<PrintItem> partFiles = project.ImportFromProjectArchive(fileNameToLoad);
                                if (partFiles != null)
                                {
                                    foreach (PrintItem part in partFiles)
                                    {
                                        QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(part.Name, part.FileLocation)));
                                    }
                                }
                            }
                            else
                            {
                                QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileNameWithoutExtension(fileNameToLoad), Path.GetFullPath(fileNameToLoad))));
                            }
                        }
                    }
                });

        }
    }
}
