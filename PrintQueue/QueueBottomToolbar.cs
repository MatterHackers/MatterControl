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
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CreatorPlugins;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.SettingsManagement;

namespace MatterHackers.MatterControl.PrintQueue
{
    public class QueueBottomToolbar : GuiWidget
    {
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        PluginChooserWindow pluginChooserWindow;
        QueueDataView queueDataView;

        static Button shopButton;

        public QueueBottomToolbar(QueueDataView queueDataView)
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

                    // put in the creator button
                    {
                        Button runCreator = textImageButtonFactory.Generate(LocalizedString.Get("Create"), "icon_creator_white_32x32.png");
                        buttonPanel1.AddChild(runCreator);
                        runCreator.Margin = new BorderDouble(0, 0, 3, 0);
                        runCreator.Click += (sender, e) =>
                        {
                            OpenPluginChooserWindow();
                        };
                    }

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
