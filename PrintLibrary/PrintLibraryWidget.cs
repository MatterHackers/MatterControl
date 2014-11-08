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

using MatterHackers.PolygonMesh.Processors;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.PrintLibrary
{
    public class PrintLibraryWidget : GuiWidget
    {        
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        TextImageButtonFactory editButtonFactory = new TextImageButtonFactory();
        TextImageButtonFactory searchButtonFactory = new TextImageButtonFactory();
        TextWidget navigationLabel;
        Button removeFromLibraryButton;
        Button addToQueueButton;
        Button searchButton;
        Button exportItemButton;
        Button editItemButton;
        Button addToLibraryButton;
        Button enterEditModeButton;
        Button leaveEditModeButton;
        MHTextEditWidget searchInput;
        LibraryDataView libraryDataView;

        public PrintLibraryWidget()
        {
            SetDisplayAttributes();

            textImageButtonFactory.borderWidth = 0;

            searchButtonFactory.normalTextColor = RGBA_Bytes.White;
            searchButtonFactory.hoverTextColor = RGBA_Bytes.White;
            searchButtonFactory.disabledTextColor = RGBA_Bytes.White;
            searchButtonFactory.pressedTextColor = RGBA_Bytes.White;
            searchButtonFactory.borderWidth = 0;
            searchButtonFactory.FixedWidth = 80;

            editButtonFactory.normalTextColor = ActiveTheme.Instance.SecondaryAccentColor;
            editButtonFactory.hoverTextColor = RGBA_Bytes.White;
            editButtonFactory.disabledTextColor = ActiveTheme.Instance.SecondaryAccentColor;
            editButtonFactory.pressedTextColor = RGBA_Bytes.White;
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
                {
                    searchInput = new MHTextEditWidget(messageWhenEmptyAndNotSelected:"Search Library".Localize());
                    searchInput.Margin = new BorderDouble(0, 3, 0, 0);
                    searchInput.HAnchor = HAnchor.ParentLeftRight;
                    searchInput.VAnchor = VAnchor.ParentCenter;
                    

					searchButton = searchButtonFactory.Generate(LocalizedString.Get("Search"),centerText:true);

                    searchPanel.AddChild(enterEditModeButton);
                    searchPanel.AddChild(leaveEditModeButton);

                    searchPanel.AddChild(searchInput);
                    searchPanel.AddChild(searchButton);
                }

                FlowLayoutWidget navigationPanel = new FlowLayoutWidget();
                navigationPanel.HAnchor = HAnchor.ParentLeftRight;
                navigationPanel.Padding = new BorderDouble(0);
                navigationPanel.BackgroundColor = ActiveTheme.Instance.TransparentLightOverlay;

                navigationLabel = new TextWidget("My Library".Localize(), pointSize: 14);
                navigationLabel.VAnchor = Agg.UI.VAnchor.ParentCenter;
                navigationLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;

                


                navigationPanel.AddChild(new GuiWidget(50,0)); //Add this as temporary balance to edit buttons
                navigationPanel.AddChild(new HorizontalSpacer());
                navigationPanel.AddChild(navigationLabel);
                navigationPanel.AddChild(new HorizontalSpacer());
                //navigationPanel.AddChild(enterEditModeButton);
                //navigationPanel.AddChild(leaveEditModeButton);

                


                FlowLayoutWidget buttonPanel = new FlowLayoutWidget();
                buttonPanel.HAnchor = HAnchor.ParentLeftRight;
                buttonPanel.Padding = new BorderDouble(0, 3);
                buttonPanel.MinimumSize = new Vector2(0, 46);
                {
					addToLibraryButton = textImageButtonFactory.Generate(LocalizedString.Get("Import"), "icon_import_white_32x32.png");
                    buttonPanel.AddChild(addToLibraryButton);
                    addToLibraryButton.Margin = new BorderDouble(0, 0, 3, 0);
                    addToLibraryButton.Click += new EventHandler(importToLibraryloadFile_Click);

                    addToQueueButton = textImageButtonFactory.Generate("Add to Queue".Localize());
                    addToQueueButton.Margin = new BorderDouble(3, 0);
                    addToQueueButton.Click += new EventHandler(addToQueueButton_Click);
                    addToQueueButton.Visible = false;
                    buttonPanel.AddChild(addToQueueButton);

                    exportItemButton = textImageButtonFactory.Generate("Export".Localize());
                    exportItemButton.Margin = new BorderDouble(3, 0);
                    exportItemButton.Click += new EventHandler(exportButton_Click);
                    exportItemButton.Visible = false;
                    buttonPanel.AddChild(exportItemButton);

                    editItemButton = textImageButtonFactory.Generate("Edit".Localize());
                    editItemButton.Margin = new BorderDouble(3, 0);
                    editItemButton.Click += new EventHandler(editButton_Click);
                    editItemButton.Visible = false;
                    buttonPanel.AddChild(editItemButton);

					removeFromLibraryButton = textImageButtonFactory.Generate("Remove".Localize());
                    removeFromLibraryButton.Margin = new BorderDouble(3, 0);
                    removeFromLibraryButton.Click += new EventHandler(deleteFromQueueButton_Click);
                    removeFromLibraryButton.Visible = false;
                    buttonPanel.AddChild(removeFromLibraryButton);

                    GuiWidget spacer = new GuiWidget();
                    spacer.HAnchor = HAnchor.ParentLeftRight;
                    buttonPanel.AddChild(spacer);
                }
                //allControls.AddChild(navigationPanel);
                allControls.AddChild(searchPanel);                                
                libraryDataView = new LibraryDataView();
                allControls.AddChild(libraryDataView);
                allControls.AddChild(buttonPanel);
            }
            allControls.AnchorAll();

            this.AddChild(allControls);

            AddHandlers();
        }


        private void AddHandlers()
        {
            libraryDataView.SelectedItems.OnAdd += onLibraryItemsSelected;
            libraryDataView.SelectedItems.OnRemove += onLibraryItemsSelected;
            searchInput.ActualTextEditWidget.EnterPressed += new KeyEventHandler(searchInputEnterPressed);
            searchButton.Click += searchButtonClick;
            searchInput.ActualTextEditWidget.KeyUp += searchInputKeyUp;
            enterEditModeButton.Click += enterEditModeButtonClick;
            leaveEditModeButton.Click += leaveEditModeButtonClick;
        }

        void searchInputKeyUp(object sender, KeyEventArgs keyEvent)
        {
            searchButtonClick(null, null);
        }

        void searchInputEnterPressed(object sender, KeyEventArgs keyEvent)
        {
            searchButtonClick(null, null);
        }

        void enterEditModeButtonClick(object sender, EventArgs mouseEvent)
        {
            enterEditModeButton.Visible = false;
            leaveEditModeButton.Visible = true;
            libraryDataView.EditMode = true;
            addToLibraryButton.Visible = false;
            SetVisibleButtons();
        }

        void leaveEditModeButtonClick(object sender, EventArgs mouseEvent)
        {
            enterEditModeButton.Visible = true;
            leaveEditModeButton.Visible = false;
            libraryDataView.EditMode = false;
            addToLibraryButton.Visible = true;
            SetVisibleButtons();
            
        }

        void searchButtonClick(object sender, EventArgs mouseEvent)
        {
            string textToSend = searchInput.Text.Trim();
            LibraryData.Instance.KeywordFilter = textToSend;
        }

        private void addToQueueButton_Click(object sender, EventArgs e)
        {
            foreach (LibraryRowItem item in libraryDataView.SelectedItems)
            {
                QueueData.Instance.AddItem(item.printItemWrapper);
            }
            libraryDataView.ClearSelectedItems();
        }

        private void onLibraryItemsSelected(object sender, EventArgs e)
        {
            SetVisibleButtons();
            
        }

        private void SetVisibleButtons()
        {            
            List<LibraryRowItem> selectedItemsList = libraryDataView.SelectedItems;
            if (selectedItemsList.Count > 0)
            {
                if (selectedItemsList.Count == 1)
                {
                    exportItemButton.Visible = true;
                    editItemButton.Visible = true;
                }
                else
                {
                    exportItemButton.Visible = false;
                    editItemButton.Visible = false;
                }
                
                addToQueueButton.Visible = true;
                removeFromLibraryButton.Visible = true;
            }
            else
            {
                addToQueueButton.Visible = false;
                removeFromLibraryButton.Visible = false;
                exportItemButton.Visible = false;
                editItemButton.Visible = false;
                
            }
        }

        private void SetDisplayAttributes()
        {
            this.Padding = new BorderDouble(3);
            this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            this.AnchorAll();
        }

        void deleteFromQueueButton_Click(object sender, EventArgs mouseEvent)
        {
			foreach (LibraryRowItem item in libraryDataView.SelectedItems)
			{
				LibraryData.Instance.RemoveItem(item.printItemWrapper);
			}

			libraryDataView.ClearSelectedItems();
        }


        ExportPrintItemWindow exportingWindow;
        bool exportingWindowIsOpen = false;
        void exportButton_Click(object sender, EventArgs mouseEvent)
        {
            //Open export options
            if (libraryDataView.SelectedItems.Count == 1)
            {
                LibraryRowItem libraryItem = libraryDataView.SelectedItems[0];
                OpenExportWindow(libraryItem.printItemWrapper);
            }
        }

        void editButton_Click(object sender, EventArgs mouseEvent)
        {
            //Open export options
            if (libraryDataView.SelectedItems.Count == 1)
            {
                LibraryRowItem libraryItem = libraryDataView.SelectedItems[0];
                libraryItem.OpenPartViewWindow(openInEditMode:true);
            }

        }

        private void OpenExportWindow(PrintItemWrapper printItem)
        {
            if (exportingWindowIsOpen == false)
            {
                exportingWindow = new ExportPrintItemWindow(printItem);
                this.exportingWindowIsOpen = true;
                exportingWindow.Closed += new EventHandler(ExportQueueItemWindow_Closed);
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

        void ExportQueueItemWindow_Closed(object sender, EventArgs e)
        {
            this.exportingWindowIsOpen = false;
        }

        public override void OnDragEnter(FileDropEventArgs fileDropEventArgs)
        {
            foreach (string file in fileDropEventArgs.DroppedFiles)
            {
                string extension = Path.GetExtension(file).ToUpper();
                if (MeshFileIo.ValidFileExtensions().Contains(extension)
                    || extension == ".GCODE")
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
                    || extension == ".GCODE")
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
                    PrintItem printItem = new PrintItem();
                    printItem.Name = Path.GetFileNameWithoutExtension(droppedFileName);
                    printItem.FileLocation = Path.GetFullPath(droppedFileName);
                    printItem.PrintItemCollectionID = LibraryData.Instance.LibraryCollection.Id;
                    printItem.Commit();

                    LibraryData.Instance.AddItem(new PrintItemWrapper(printItem));
                }
            }

            base.OnDragDrop(fileDropEventArgs);
        }

        void importToLibraryloadFile_Click(object sender, EventArgs mouseEvent)
        {
            UiThread.RunOnIdle(importToLibraryloadFile_ClickOnIdle);
        }

        void importToLibraryloadFile_ClickOnIdle(object state)
        {
            OpenFileDialogParams openParams = new OpenFileDialogParams(ApplicationSettings.OpenPrintableFileParams, multiSelect: true);
			FileDialog.OpenFileDialog(openParams, onLibraryLoadFileSelected);            
        }

		void onLibraryLoadFileSelected(OpenFileDialogParams openParams)
		{
			if (openParams.FileNames != null)
			{
                LibraryData.Instance.LoadFilesIntoLibrary(openParams.FileNames);
			}
		}
    }
}
