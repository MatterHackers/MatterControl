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
using MatterHackers.MatterControl.CreatorPlugins;

namespace MatterHackers.MatterControl.PrintQueue
{
    public class BottomToolbar : GuiWidget
    {
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        PluginChooserWindow pluginChooserWindow;
        QueueDataView queueDataView;

        public BottomToolbar(QueueDataView queueDataView)
        {
            this.queueDataView = queueDataView;

            SetDisplayAttributes();

            textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.borderWidth = 0;

            FlowLayoutWidget allControls = new FlowLayoutWidget(FlowDirection.TopToBottom);

            {
                {                    
                    // Ensure the form opens with no rows selected.
                    //ActiveQueueList.Instance.ClearSelected();

                    allControls.AddChild(queueDataView);
                }

                FlowLayoutWidget buttonPanel1 = new FlowLayoutWidget();
                buttonPanel1.HAnchor = HAnchor.ParentLeftRight;
                buttonPanel1.Padding = new BorderDouble(0, 3);

                {
					Button addToQueueButton = textImageButtonFactory.Generate(LocalizedString.Get("Add"), "icon_circle_plus.png");
                    buttonPanel1.AddChild(addToQueueButton);
                    addToQueueButton.Margin = new BorderDouble(0, 0, 3, 0);
                    addToQueueButton.Click += new ButtonBase.ButtonEventHandler(addToQueueButton_Click);

                    Button runCreator = textImageButtonFactory.Generate(LocalizedString.Get("Create"), "icon_creator_white_32x32.png");
                    buttonPanel1.AddChild(runCreator);
                    runCreator.Margin = new BorderDouble(0, 0, 3, 0);
                    runCreator.Click += (sender, e) =>
                    {
                        OpenPluginChooserWindow();
                    };

					Button deleteAllFromQueueButton = textImageButtonFactory.Generate(LocalizedString.Get("Remove All"));
                    deleteAllFromQueueButton.Margin = new BorderDouble(3, 0);
                    deleteAllFromQueueButton.Click += new ButtonBase.ButtonEventHandler(deleteAllFromQueueButton_Click);
					//buttonPanel1.AddChild(deleteAllFromQueueButton);

                    GuiWidget spacer1 = new GuiWidget();
                    spacer1.HAnchor = HAnchor.ParentLeftRight;
                    buttonPanel1.AddChild(spacer1);

                    GuiWidget spacer2 = new GuiWidget();
                    spacer2.HAnchor = HAnchor.ParentLeftRight;
                    buttonPanel1.AddChild(spacer2);

                    GuiWidget queueMenu = new QueueOptionsMenu();
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

        void createPartsSheetsButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            List<PrintItem> parts = QueueData.Instance.CreateReadOnlyPartList();

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
            List<PrintItem> partList = QueueData.Instance.CreateReadOnlyPartList();
            ProjectFileHandler project = new ProjectFileHandler(partList);
            project.SaveAs();
        }

        void importQueueButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            ProjectFileHandler project = new ProjectFileHandler(null);
            List<PrintItem> partFiles = project.OpenFromDialog();
            if (partFiles != null)
            {
                QueueData.Instance.RemoveAll();
                foreach (PrintItem part in partFiles)
                {
                    QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(part.Name, part.FileLocation)));
                }
            }
        }

        void deleteAllFromQueueButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            QueueData.Instance.RemoveAll();
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
                    QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileNameWithoutExtension(droppedFileName), Path.GetFullPath(droppedFileName))));
                }
            }

            base.OnDragDrop(fileDropEventArgs);
        }

        void addToQueueButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle(AddItemsToQueue);
        }

        void AddItemsToQueue(object state)
        {
            OpenFileDialogParams openParams = new OpenFileDialogParams("Select an STL file, Select a GCODE file|*.stl;*.gcode", multiSelect: true);
			openParams.ActionButtonLabel = "Add to Queue";
			openParams.Title = "MatterControl: Select A File";

            FileDialog.OpenFileDialog(ref openParams);
            if (openParams.FileNames != null)
            {
                foreach (string loadedFileName in openParams.FileNames)
                {
                    QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(Path.GetFileNameWithoutExtension(loadedFileName), Path.GetFullPath(loadedFileName))));
                }
            }
        }
    }
}
