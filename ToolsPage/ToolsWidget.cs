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

namespace MatterHackers.MatterControl.ToolsPage
{
    public class ToolsWidget : GuiWidget
    {        
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        TextImageButtonFactory searchButtonFactory = new TextImageButtonFactory();
        Button deleteFromLibraryButton;
        Button addToQueueButton;
        Button searchButton;
        MHTextEditWidget searchInput;

        public ToolsWidget()
        {
            SetDisplayAttributes();
            
            textImageButtonFactory.normalTextColor = RGBA_Bytes.White;
            textImageButtonFactory.hoverTextColor = RGBA_Bytes.White;
            textImageButtonFactory.disabledTextColor = RGBA_Bytes.White;
            textImageButtonFactory.pressedTextColor = RGBA_Bytes.White;
            textImageButtonFactory.borderWidth = 0;

            searchButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            searchButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            searchButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            searchButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            searchButtonFactory.borderWidth = 0;

            FlowLayoutWidget allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);
            {   
                FlowLayoutWidget searchPanel = new FlowLayoutWidget();
                searchPanel.BackgroundColor = new RGBA_Bytes(180, 180, 180);
                searchPanel.HAnchor = HAnchor.ParentLeftRight;
                searchPanel.Padding = new BorderDouble(3, 3);
                {
                    searchInput = new MHTextEditWidget();
                    searchInput.Margin = new BorderDouble(6, 0);
                    searchInput.HAnchor = HAnchor.ParentLeftRight;
                    searchInput.VAnchor = VAnchor.ParentCenter;

                    searchButton = searchButtonFactory.Generate("Search");
                    searchButton.Margin = new BorderDouble(right:9);

                    searchPanel.AddChild(searchInput);
                    searchPanel.AddChild(searchButton);
                }

                FlowLayoutWidget buttonPanel = new FlowLayoutWidget();
                buttonPanel.HAnchor = HAnchor.ParentLeftRight;
                buttonPanel.Padding = new BorderDouble(0, 3);
                {
					Button addToLibrary = textImageButtonFactory.Generate(new LocalizedString("Import").Translated, "icon_import_white_32x32.png");
                    buttonPanel.AddChild(addToLibrary);
                    addToLibrary.Margin = new BorderDouble(0, 0, 3, 0);
                    addToLibrary.Click += new ButtonBase.ButtonEventHandler(loadFile_Click);

					deleteFromLibraryButton = textImageButtonFactory.Generate(new LocalizedString("Delete").Translated);
                    deleteFromLibraryButton.Margin = new BorderDouble(3, 0);
                    deleteFromLibraryButton.Click += new ButtonBase.ButtonEventHandler(deleteFromQueueButton_Click);
                    deleteFromLibraryButton.Visible = false;
                    buttonPanel.AddChild(deleteFromLibraryButton);

					addToQueueButton = textImageButtonFactory.Generate(new LocalizedString("Add to Queue").Translated);
                    addToQueueButton.Margin = new BorderDouble(3, 0);
                    addToQueueButton.Click += new ButtonBase.ButtonEventHandler(addToQueueButton_Click);
                    addToQueueButton.Visible = false;
                    buttonPanel.AddChild(addToQueueButton);

                    GuiWidget spacer = new GuiWidget();
                    spacer.HAnchor = HAnchor.ParentLeftRight;
                    buttonPanel.AddChild(spacer);
                }

                allControls.AddChild(searchPanel);
                allControls.AddChild(ToolsListControl.Instance);
                allControls.AddChild(buttonPanel);
            }
            allControls.AnchorAll();

            this.AddChild(allControls);

            AddHandlers();
        }

        private void AddHandlers()
        {
            ToolsListControl.Instance.SelectedItems.OnAdd += onLibraryItemsSelected;
            ToolsListControl.Instance.SelectedItems.OnRemove += onLibraryItemsSelected;
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
            ToolsListControl.Instance.KeywordFilter = textToSend;
        }

        private void addToQueueButton_Click(object sender, MouseEventArgs e)
        {
            foreach (ToolsListItem item in ToolsListControl.Instance.SelectedItems)
            {
                PrintQueue.PrintQueueItem queueItem = new PrintQueue.PrintQueueItem(item.printItem);
                PrintQueue.PrintQueueControl.Instance.AddChild(queueItem);
            }
            ToolsListControl.Instance.ClearSelectedItems();
            PrintQueue.PrintQueueControl.Instance.EnsureSelection();
        }

        private void onLibraryItemsSelected(object sender, EventArgs e)
        {
            List<ToolsListItem> selectedItemsList = (List<ToolsListItem>)sender;
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
            ToolsListControl.Instance.RemoveSelectedItems();
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
                    printItem.PrintItemCollectionID = ToolsListControl.Instance.LibraryCollection.Id;
                    printItem.Commit();

                    ToolsListItem queueItem = new ToolsListItem(new PrintItemWrapper(printItem));
                    ToolsListControl.Instance.AddChild(queueItem);
                }
                ToolsListControl.Instance.Invalidate();
            }
            ToolsListControl.Instance.SaveLibraryItems();

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
                    printItem.PrintItemCollectionID = ToolsListControl.Instance.LibraryCollection.Id;
                    printItem.Commit();

                    ToolsListItem queueItem = new ToolsListItem(new PrintItemWrapper(printItem));
                    ToolsListControl.Instance.AddChild(queueItem);
                }
                ToolsListControl.Instance.Invalidate();
            }
            ToolsListControl.Instance.SaveLibraryItems();
        }
    }
}
