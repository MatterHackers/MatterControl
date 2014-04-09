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
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.PrintLibrary
{
    public class PrintLibraryWidget : GuiWidget
    {        
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        TextImageButtonFactory searchButtonFactory = new TextImageButtonFactory();
        Button deleteFromLibraryButton;
        Button addToQueueButton;
        Button searchButton;
        MHTextEditWidget searchInput;        

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

            FlowLayoutWidget allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);
            {   
                FlowLayoutWidget searchPanel = new FlowLayoutWidget();
                searchPanel.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
                searchPanel.HAnchor = HAnchor.ParentLeftRight;
                searchPanel.Padding = new BorderDouble(0);                
                {
                    searchInput = new MHTextEditWidget();
                    searchInput.Margin = new BorderDouble(6, 3, 0, 0);
                    searchInput.HAnchor = HAnchor.ParentLeftRight;
                    searchInput.VAnchor = VAnchor.ParentCenter;

					searchButton = searchButtonFactory.Generate(LocalizedString.Get("Search"),centerText:true);

                    searchPanel.AddChild(searchInput);
                    searchPanel.AddChild(searchButton);
                }

                FlowLayoutWidget buttonPanel = new FlowLayoutWidget();
                buttonPanel.HAnchor = HAnchor.ParentLeftRight;
                buttonPanel.Padding = new BorderDouble(0, 3);
                {
					Button addToLibrary = textImageButtonFactory.Generate(LocalizedString.Get("Import"), "icon_import_white_32x32.png");
                    buttonPanel.AddChild(addToLibrary);
                    addToLibrary.Margin = new BorderDouble(0, 0, 3, 0);
                    addToLibrary.Click += new ButtonBase.ButtonEventHandler(loadFile_Click);

					addToQueueButton = textImageButtonFactory.Generate("Add to Queue");
                    addToQueueButton.Margin = new BorderDouble(3, 0);
                    addToQueueButton.Click += new ButtonBase.ButtonEventHandler(addToQueueButton_Click);
                    addToQueueButton.Visible = false;
                    buttonPanel.AddChild(addToQueueButton);

					deleteFromLibraryButton = textImageButtonFactory.Generate("Remove");
                    deleteFromLibraryButton.Margin = new BorderDouble(3, 0);
                    deleteFromLibraryButton.Click += new ButtonBase.ButtonEventHandler(deleteFromQueueButton_Click);
                    deleteFromLibraryButton.Visible = false;
                    buttonPanel.AddChild(deleteFromLibraryButton);

                    GuiWidget spacer = new GuiWidget();
                    spacer.HAnchor = HAnchor.ParentLeftRight;
                    buttonPanel.AddChild(spacer);
                }

                allControls.AddChild(searchPanel);
                allControls.AddChild(PrintLibraryListControl.Instance);
                allControls.AddChild(buttonPanel);
            }
            allControls.AnchorAll();

            this.AddChild(allControls);

            AddHandlers();
        }


        private void AddHandlers()
        {
            PrintLibraryListControl.Instance.SelectedItems.OnAdd += onLibraryItemsSelected;
            PrintLibraryListControl.Instance.SelectedItems.OnRemove += onLibraryItemsSelected;
            searchInput.ActualTextEditWidget.EnterPressed += new KeyEventHandler(searchInputEnterPressed);
            searchButton.Click += searchButtonClick;
            searchInput.ActualTextEditWidget.KeyUp += searchInputKeyUp;
        }

        void searchInputKeyUp(object sender, KeyEventArgs keyEvent)
        {
            searchButtonClick(null, null);
        }

        void searchInputEnterPressed(object sender, KeyEventArgs keyEvent)
        {
            searchButtonClick(null, null);
        }

        void searchButtonClick(object sender, MouseEventArgs mouseEvent)
        {
            string textToSend = searchInput.Text.Trim();
            PrintLibraryListControl.Instance.KeywordFilter = textToSend;
        }

        private void addToQueueButton_Click(object sender, MouseEventArgs e)
        {
            foreach (PrintLibraryListItem item in PrintLibraryListControl.Instance.SelectedItems)
            {
                PrintQueue.PrintQueueItem queueItem = new PrintQueue.PrintQueueItem(item.printItem);
                PrintQueue.PrintQueueControl.Instance.AddChild(queueItem);
            }
            PrintLibraryListControl.Instance.ClearSelectedItems();
            PrintQueue.PrintQueueControl.Instance.EnsureSelection();
            PrintQueueControl.Instance.SaveDefaultQueue();
        }

        private void onLibraryItemsSelected(object sender, EventArgs e)
        {
            List<PrintLibraryListItem> selectedItemsList = (List<PrintLibraryListItem>)sender;
            if (selectedItemsList.Count > 0)
            {
                addToQueueButton.Visible = true;
                deleteFromLibraryButton.Visible = true;
            }
            else
            {
                addToQueueButton.Visible = false;
                deleteFromLibraryButton.Visible = false;
            }
        }

        private void SetDisplayAttributes()
        {
            this.Padding = new BorderDouble(3);
            this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            this.AnchorAll();
        }

        void deleteFromQueueButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrintLibraryListControl.Instance.RemoveSelectedItems();
        }

        public override void OnDragEnter(FileDropEventArgs fileDropEventArgs)
        {
            foreach (string file in fileDropEventArgs.DroppedFiles)
            {
                string extension = Path.GetExtension(file).ToUpper();
                if (extension == ".STL" || extension == ".GCODE")
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
                if (extension == ".STL" || extension == ".GCODE")
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
                if (extension == ".STL" || extension == ".GCODE")
                {
                    PrintItem printItem = new PrintItem();
                    printItem.Name = System.IO.Path.GetFileNameWithoutExtension(droppedFileName);
                    printItem.FileLocation = System.IO.Path.GetFullPath(droppedFileName);
                    printItem.PrintItemCollectionID = PrintLibraryListControl.Instance.LibraryCollection.Id;
                    printItem.Commit();

                    PrintLibraryListItem queueItem = new PrintLibraryListItem(new PrintItemWrapper(printItem));
                    PrintLibraryListControl.Instance.AddChild(queueItem);
                }
                PrintLibraryListControl.Instance.Invalidate();
            }
            PrintLibraryListControl.Instance.SaveLibraryItems();

            base.OnDragDrop(fileDropEventArgs);
        }

        void loadFile_Click(object sender, MouseEventArgs mouseEvent)
        {
            OpenFileDialogParams openParams = new OpenFileDialogParams("Select an STL file, Select a GCODE file|*.stl;*.gcode", multiSelect: true);
            FileDialog.OpenFileDialog(ref openParams);
            if (openParams.FileNames != null)
            {
                foreach (string loadedFileName in openParams.FileNames)
                {
                    PrintItem printItem = new PrintItem();
                    printItem.Name = System.IO.Path.GetFileNameWithoutExtension(loadedFileName);
                    printItem.FileLocation = System.IO.Path.GetFullPath(loadedFileName);
                    printItem.PrintItemCollectionID = PrintLibraryListControl.Instance.LibraryCollection.Id;
                    printItem.Commit();

                    PrintLibraryListItem queueItem = new PrintLibraryListItem(new PrintItemWrapper(printItem));
                    PrintLibraryListControl.Instance.AddChild(queueItem);
                }
                PrintLibraryListControl.Instance.Invalidate();
            }
            PrintLibraryListControl.Instance.SaveLibraryItems();
        }
    }
}
