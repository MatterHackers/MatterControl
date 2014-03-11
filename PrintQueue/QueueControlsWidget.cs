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
    public class QueueControlsWidget : GuiWidget
    {
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

        public QueueControlsWidget()
        {
            SetDisplayAttributes();
            
            textImageButtonFactory.normalTextColor = RGBA_Bytes.White;
            textImageButtonFactory.hoverTextColor = RGBA_Bytes.White;
            textImageButtonFactory.disabledTextColor = RGBA_Bytes.White;
            textImageButtonFactory.pressedTextColor = RGBA_Bytes.White;
            textImageButtonFactory.borderWidth = 0;

            FlowLayoutWidget allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);

            {
                {                    
                    // Ensure the form opens with no rows selected.
                    //ActiveQueueList.Instance.ClearSelected();

                    allControls.AddChild(PrintQueueControl.Instance);
                }

                FlowLayoutWidget buttonPanel1 = new FlowLayoutWidget();
                buttonPanel1.HAnchor = HAnchor.ParentLeftRight;
                buttonPanel1.Padding = new BorderDouble(0, 3);

                {
					Button addToQueueButton = textImageButtonFactory.Generate(LocalizedString.Get("Add"), "icon_circle_plus.png");
                    buttonPanel1.AddChild(addToQueueButton);
                    addToQueueButton.Margin = new BorderDouble(0, 0, 3, 0);
                    addToQueueButton.Click += new ButtonBase.ButtonEventHandler(loadFile_Click);

					Button deleteAllFromQueueButton = textImageButtonFactory.Generate(LocalizedString.Get("Remove All"));
                    deleteAllFromQueueButton.Margin = new BorderDouble(3, 0);
                    deleteAllFromQueueButton.Click += new ButtonBase.ButtonEventHandler(deleteAllFromQueueButton_Click);
                    buttonPanel1.AddChild(deleteAllFromQueueButton);

                    GuiWidget spacer1 = new GuiWidget();
                    spacer1.HAnchor = HAnchor.ParentLeftRight;
                    buttonPanel1.AddChild(spacer1);

                    GuiWidget spacer2 = new GuiWidget();
                    spacer2.HAnchor = HAnchor.ParentLeftRight;
                    buttonPanel1.AddChild(spacer2);

                    GuiWidget queueMenu = new PrintQueueMenu();
                    queueMenu.VAnchor = VAnchor.ParentTop;
                    buttonPanel1.AddChild(queueMenu);
                }
                allControls.AddChild(buttonPanel1);
            }
            allControls.AnchorAll();
            
            this.AddChild(allControls);
        }

        private void SetDisplayAttributes()
        {
            this.Padding = new BorderDouble(3);
            this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            this.AnchorAll();
        }

        void createPatrsSheetsButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            List<PrintItem> parts = PrintQueueControl.Instance.CreateReadOnlyPartList();

            SaveFileDialogParams saveParams = new SaveFileDialogParams("Save Parts Sheet|*.pdf");

            System.IO.Stream streamToSaveTo = FileDialog.SaveFileDialog(ref saveParams);
            if (streamToSaveTo != null)
            {
				string partFileName = saveParams.FileName;

				if ( !partFileName.StartsWith ("" + Path.DirectorySeparatorChar) )
				{
					partFileName = Path.DirectorySeparatorChar + partFileName;
				}

				PartsSheet currentPartsInQueue = new PartsSheet(parts, partFileName);
                currentPartsInQueue.SaveSheets();
            }
        }

        void exportToSDProcess_UpdateRemainingItems(object sender, EventArgs e)
        {
            ExportToFolderProcess exportToSDProcess = (ExportToFolderProcess)sender;
        }

        void exportQueueButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            List<PrintItem> partList = PrintQueueControl.Instance.CreateReadOnlyPartList();
            ProjectFileHandler project = new ProjectFileHandler(partList);
            project.SaveAs();
        }

        void importQueueButton_Click(object sender, MouseEventArgs mouseEvent)
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

        void deleteAllFromQueueButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrintQueueControl.Instance.RemoveAllChildren();
            PrintQueueControl.Instance.SaveDefaultQueue();
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
                    PrintQueueItem queueItem = new PrintQueueItem(System.IO.Path.GetFileNameWithoutExtension(droppedFileName), System.IO.Path.GetFullPath(droppedFileName));
                    PrintQueueControl.Instance.AddChild(queueItem);
                }
                PrintQueueControl.Instance.EnsureSelection();
                PrintQueueControl.Instance.Invalidate();
            }
            PrintQueueControl.Instance.SaveDefaultQueue();

            base.OnDragDrop(fileDropEventArgs);
        }

        void loadFile_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle(LoadFileOnIdle);
        }

        void LoadFileOnIdle(object state)
        {
            OpenFileDialogParams openParams = new OpenFileDialogParams("Select an STL file, Select a GCODE file|*.stl;*.gcode", multiSelect: true);
			openParams.ActionButtonLabel = "Add to Queue";
			openParams.Title = "MatterControl: Select A File";

            FileDialog.OpenFileDialog(ref openParams);
            if (openParams.FileNames != null)
            {
                foreach (string loadedFileName in openParams.FileNames)
                {
					PrintQueueItem queueItem = new PrintQueueItem(System.IO.Path.GetFileNameWithoutExtension(loadedFileName), System.IO.Path.GetFullPath(loadedFileName));
					PrintQueueControl.Instance.AddChild(queueItem);
                }
                if (PrintQueueControl.Instance.Count > 0)
                {
                    PrintQueueControl.Instance.SelectedIndex = PrintQueueControl.Instance.Count - 1;
                }
                //PrintQueueControl.Instance.EnsureSelection();
                PrintQueueControl.Instance.Invalidate();
            }
            PrintQueueControl.Instance.SaveDefaultQueue();
        }
    }
}
