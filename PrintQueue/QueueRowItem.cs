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
using System.Globalization;
using System.Threading;
using System.IO;

using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.Localizations;

using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.PrintQueue
{
    public class QueueRowItem : GuiWidget
    {
        public PrintItemWrapper PrintItemWrapper { get; set; }
        public RGBA_Bytes WidgetTextColor;
        public RGBA_Bytes WidgetBackgroundColor;
        public bool isActivePrint = false;
        public bool isSelectedItem = false;
        public bool isHoverItem = false;
        TextWidget partLabel;
        TextWidget partStatus;
        FlowLayoutWidget editControls;
        LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
		ExportPrintItemWindow exportingWindow;
		PartPreviewMainWindow viewingWindow;
		bool exportingWindowIsOpen = false;
		bool viewWindowIsOpen = false;
        QueueDataView queueDataView;
        SlideWidget actionButtonContainer;

        public bool IsHoverItem
        {
            get { return isHoverItem; }
            set
            {
                if (this.isHoverItem != value)
                {
                    this.isHoverItem = value;
                    if (value == true && !this.queueDataView.EditMode)
                    {
                        this.actionButtonContainer.SlideIn();
                    }
                    else
                    {
                        this.actionButtonContainer.SlideOut();
                    }
                }
            }
        }

        public QueueRowItem(PrintItemWrapper printItemWrapper, QueueDataView queueDataView)
        {
            this.queueDataView = queueDataView;
            this.PrintItemWrapper = printItemWrapper;
            ConstructPrintQueueItem();
        }

        public void ConstructPrintQueueItem()
        {
            linkButtonFactory.fontSize = 10;
            linkButtonFactory.textColor = RGBA_Bytes.Black;

            WidgetTextColor = RGBA_Bytes.Black;
            WidgetBackgroundColor = RGBA_Bytes.White;

            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

            SetDisplayAttributes();

            FlowLayoutWidget topToBottomLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottomLayout.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;

            FlowLayoutWidget topContentsFlowLayout = new FlowLayoutWidget(FlowDirection.LeftToRight);
            topContentsFlowLayout.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
            {
                FlowLayoutWidget leftColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
                leftColumn.VAnchor = VAnchor.ParentTop | Agg.UI.VAnchor.FitToChildren;
                {
                    PartThumbnailWidget thumbnailWidget = new PartThumbnailWidget(PrintItemWrapper, "part_icon_transparent_40x40.png", "building_thumbnail_40x40.png", PartThumbnailWidget.ImageSizes.Size50x50);
                    leftColumn.AddChild(thumbnailWidget);
                }

                FlowLayoutWidget middleColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
                middleColumn.VAnchor = VAnchor.ParentTop | Agg.UI.VAnchor.FitToChildren;
                middleColumn.HAnchor = HAnchor.ParentLeftRight;// | Agg.UI.HAnchor.FitToChildren;
                middleColumn.Padding = new BorderDouble(8);
                middleColumn.Margin = new BorderDouble(10,0);
                {
                    string labelName = textInfo.ToTitleCase(PrintItemWrapper.Name);
                    labelName = labelName.Replace('_', ' ');
                    partLabel = new TextWidget(labelName, pointSize: 14);
                    partLabel.TextColor = WidgetTextColor;
                    partLabel.MinimumSize = new Vector2(1, 16);

					string partStatusLabelTxt = LocalizedString.Get ("Status").ToUpper();     
					string partStatusLabelTxtTest = LocalizedString.Get ("Queued to Print");
					string partStatusLabelTxtFull = "{0}: {1}".FormatWith(partStatusLabelTxt,partStatusLabelTxtTest);

					partStatus = new TextWidget(partStatusLabelTxtFull, pointSize: 10);
                    partStatus.AutoExpandBoundsToText = true;
                    partStatus.TextColor = WidgetTextColor;
                    partStatus.MinimumSize = new Vector2(50, 12);

					middleColumn.AddChild(partLabel);
                    middleColumn.AddChild(partStatus);
                }

                CreateEditControls();

                topContentsFlowLayout.AddChild(leftColumn);
                topContentsFlowLayout.AddChild(middleColumn);
                //topContentsFlowLayout.AddChild(editControls);

                editControls.Visible = false;
            }

            topToBottomLayout.AddChild(topContentsFlowLayout);
            this.AddChild(topToBottomLayout);

            actionButtonContainer = getItemActionButtons();
            actionButtonContainer.Visible = false;

            this.AddChild(actionButtonContainer);


            AddHandlers();
        }

        SlideWidget getItemActionButtons()
        {
            SlideWidget buttonContainer = new SlideWidget();
            buttonContainer.VAnchor = VAnchor.ParentBottomTop;
            buttonContainer.HAnchor = HAnchor.ParentRight;

            FlowLayoutWidget buttonFlowContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            buttonFlowContainer.VAnchor = VAnchor.ParentBottomTop;

            ClickWidget printButton = new ClickWidget();
            printButton.VAnchor = VAnchor.ParentBottomTop;
            printButton.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
            printButton.Width = 80;

            TextWidget printLabel = new TextWidget("Remove".Localize());
            printLabel.TextColor = RGBA_Bytes.White;
            printLabel.VAnchor = VAnchor.ParentCenter;
            printLabel.HAnchor = HAnchor.ParentCenter;

            printButton.AddChild(printLabel);
            printButton.Click += (sender, e) =>
            {
                UiThread.RunOnIdle(DeletePartFromQueue);
            }; ;

            ClickWidget editButton = new ClickWidget();
            editButton.VAnchor = VAnchor.ParentBottomTop;
            editButton.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
            editButton.Width = 80;

            TextWidget editLabel = new TextWidget("Edit".Localize());
            editLabel.TextColor = RGBA_Bytes.White;
            editLabel.VAnchor = VAnchor.ParentCenter;
            editLabel.HAnchor = HAnchor.ParentCenter;

            editButton.AddChild(editLabel);
            editButton.Click += onEditPartClick;

            //buttonFlowContainer.AddChild(editButton);
            buttonFlowContainer.AddChild(printButton);

            buttonContainer.AddChild(buttonFlowContainer);
            //buttonContainer.Width = 160;
            buttonContainer.Width = 80;

            return buttonContainer;
        }

        private void onEditPartClick(object sender, MouseEventArgs e)
        {
            UiThread.RunOnIdle((state) =>
            {
                OpenPartViewWindow(true);
            });
        }


        

        private void OpenExportWindow()
        {
            if (exportingWindowIsOpen == false)
            {
                exportingWindow = new ExportPrintItemWindow(this.PrintItemWrapper);
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

        public void OpenPartViewWindow(bool openInEditMode = false)
        {
            if (viewWindowIsOpen == false)
            {
                viewingWindow = new PartPreviewMainWindow(this.PrintItemWrapper, View3DTransformPart.AutoRotate.Enabled, openInEditMode);
                this.viewWindowIsOpen = true;
                viewingWindow.Closed += new EventHandler(PartPreviewWindow_Closed);
            }
            else
            {
                if (viewingWindow != null)
                {
                    viewingWindow.BringToFront();
                }
            }
        }

		void PartPreviewWindow_Closed(object sender, EventArgs e)
		{
			this.viewWindowIsOpen = false;
		}

        private void CreateEditControls()
        {
            editControls = new FlowLayoutWidget();
            editControls.Margin = new BorderDouble(right: 10);
            editControls.VAnchor = Agg.UI.VAnchor.FitToChildren | Agg.UI.VAnchor.ParentCenter;
            {
                FlowLayoutWidget layoutLeftToRight = new FlowLayoutWidget();

                linkButtonFactory.margin = new BorderDouble(3);

                bool fileIsOnPrinterSdCard = PrintItemWrapper.PrintItem.FileLocation == QueueData.SdCardFileName;
                if (!fileIsOnPrinterSdCard)
                {
                    // view button
                    {
                        Button viewLink = linkButtonFactory.Generate(LocalizedString.Get("View"));
                        viewLink.Click += (sender, e) =>
                        {

                            string pathAndFile = PrintItemWrapper.FileLocation;
                            if (File.Exists(pathAndFile))
                            {
                                OpenPartViewWindow();
                            }
                            else
                            {
                                ShowCantFindFileMessage(PrintItemWrapper);
                            }
                        };
                        layoutLeftToRight.AddChild(viewLink);
                    }

                    // copy button
                    {
                        Button copyLink = linkButtonFactory.Generate(LocalizedString.Get("Copy"));
                        copyLink.Click += (sender, e) =>
                        {
                            CreateCopyInQueue();
                        };
                        layoutLeftToRight.AddChild(copyLink);
                    }

                    // add to library button
                    {
                        if (this.PrintItemWrapper.PrintItem.PrintItemCollectionID == LibraryData.Instance.LibraryCollection.Id)
                        {
                            //rightColumnOptions.AddChild(new TextWidget("Libary Item"));
                        }
                    }

                    // the export menu
                    {
                        Button exportLink = linkButtonFactory.Generate(LocalizedString.Get("Export"));
                        exportLink.Click += (sender, e) =>
                        {
                            OpenExportWindow();

                        };
                        layoutLeftToRight.AddChild(exportLink);
                    }
                }

                // spacer
                {
                    layoutLeftToRight.AddChild(new GuiWidget(10, 10));
                }

                // delete button
                {
					Button deleteLink = linkButtonFactory.Generate(LocalizedString.Get("Remove"));
                    deleteLink.Click += (sender, e) =>
                    {
                        UiThread.RunOnIdle(DeletePartFromQueue);
                    };
                    layoutLeftToRight.AddChild(deleteLink);
                }

                // push off to the right the rest spacer
                {
                    GuiWidget spaceFiller = new GuiWidget(10, 10);
                    //layoutLeftToRight.AddChild(spaceFiller);
                }

                // up and down buttons
                {
                    FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
                    // move up one button
                    {
                        Button moveUp = linkButtonFactory.Generate(" ^ ");
                        moveUp.Click += (sender, e) =>
                        {
                            int thisIndexInQueue = QueueData.Instance.GetIndex(PrintItemWrapper);
                            QueueData.Instance.SwapItemsOnIdle(thisIndexInQueue, thisIndexInQueue - 1);
                        };
                        topToBottom.AddChild(moveUp);
                    }

                    // move down one button
                    {
                        Button moveDown = linkButtonFactory.Generate(" v ");
                        moveDown.Click += (sender, e) =>
                        {
                            int thisIndexInQueue = QueueData.Instance.GetIndex(PrintItemWrapper);
                            QueueData.Instance.SwapItemsOnIdle(thisIndexInQueue, thisIndexInQueue + 1);
                        };
                        topToBottom.AddChild(moveDown);
                    }
                    
                    // don't add this yet as we don't have icons for it and it should probably be drag and drop anyway
                    //layoutLeftToRight.AddChild(topToBottom);
                }


                // now add the layout to the edit controls bar
                editControls.AddChild(layoutLeftToRight);
            }
        }

        public override void OnClosed(EventArgs e)
        {
            PrintItemWrapper.SlicingOutputMessage.UnregisterEvent(PrintItem_SlicingOutputMessage, ref unregisterEvents);
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        event EventHandler unregisterEvents;
        void AddHandlers()
        {
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);

            PrintItemWrapper.SlicingOutputMessage.RegisterEvent(PrintItem_SlicingOutputMessage, ref unregisterEvents);

            MouseEnterBounds += new EventHandler(PrintQueueItem_MouseEnterBounds);
            MouseLeaveBounds += new EventHandler(PrintQueueItem_MouseLeaveBounds);
        }

        void PrintItem_SlicingOutputMessage(object sender, EventArgs e)
        {
            StringEventArgs message = e as StringEventArgs;
            partStatus.Text = "Status: {0}".FormatWith(message.Data);
        }

        void SetDisplayAttributes()
        {
            this.VAnchor = Agg.UI.VAnchor.FitToChildren;
            this.HAnchor = Agg.UI.HAnchor.ParentLeftRight | Agg.UI.HAnchor.FitToChildren;
            this.Height = 50;
			this.BackgroundColor = this.WidgetBackgroundColor;
            this.Padding = new BorderDouble(0);
            this.Margin = new BorderDouble(6,0,6,6);
        }

        void PrintQueueItem_MouseLeaveBounds(object sender, EventArgs e)
        {
            editControls.Visible = false;
        }

        void PrintQueueItem_MouseEnterBounds(object sender, EventArgs e)
        {
            editControls.Visible = true;
        }

        class PartToAddToQueue
        {
            internal string Name;
            internal string FileLocation;
            internal int insertAfterIndex;

            internal PartToAddToQueue(string name, string fileLocation, int insertAfterIndex)
            {
                this.Name = name;
                this.FileLocation = fileLocation;
                this.insertAfterIndex = insertAfterIndex;
            }
        }

        void AddPartToQueue(object state)
        {
            PartToAddToQueue partToAddToQueue = (PartToAddToQueue)state;
            QueueData.Instance.AddItem(new PrintItemWrapper( new PrintItem(partToAddToQueue.Name, partToAddToQueue.FileLocation)), partToAddToQueue.insertAfterIndex);
        }

        public void CreateCopyInQueue()
        {
            int thisIndexInQueue = QueueData.Instance.GetIndex(PrintItemWrapper);
            if (thisIndexInQueue != -1 && File.Exists(PrintItemWrapper.FileLocation))
            {
                string applicationDataPath = ApplicationDataStorage.Instance.ApplicationUserDataPath;
                string stagingFolder = Path.Combine(applicationDataPath, "data", "temp", "design");
                if (!Directory.Exists(stagingFolder))
                {
                    Directory.CreateDirectory(stagingFolder);
                }

                string newCopyFilename;
                int infiniteBlocker = 0;
                do
                {
                    newCopyFilename = Path.Combine(stagingFolder, Path.ChangeExtension(Path.GetRandomFileName(), "stl"));
                    newCopyFilename = Path.GetFullPath(newCopyFilename);
                    infiniteBlocker++;
                } while (File.Exists(newCopyFilename) && infiniteBlocker < 100);

                File.Copy(PrintItemWrapper.FileLocation, newCopyFilename);

                string newName = PrintItemWrapper.Name;

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

                UiThread.RunOnIdle(AddPartToQueue, new PartToAddToQueue(newName, newCopyFilename, thisIndexInQueue + 1));
            }
        }

        string alsoRemoveFromSdCardMessage = "Would you also like to remove this file from the Printer's SD Card?".Localize();
        string alsoRemoveFromSdCardTitle = "Remove From Printer's SD Card?";
        void DeletePartFromQueue(object state)
        {
            if (PrintItemWrapper.PrintItem.FileLocation == QueueData.SdCardFileName)
            {
                if (StyledMessageBox.ShowMessageBox(alsoRemoveFromSdCardMessage, alsoRemoveFromSdCardTitle, StyledMessageBox.MessageType.YES_NO))
                {
                    // The firmware only understands the names when lowercase.
                    PrinterConnectionAndCommunication.Instance.DeleteFileFromSdCard(PrintItemWrapper.PrintItem.Name);
                }
            }

            int thisIndexInQueue = QueueData.Instance.GetIndex(PrintItemWrapper);
            QueueData.Instance.RemoveIndexOnIdle(thisIndexInQueue);
        }

        public static void ShowCantFindFileMessage(PrintItemWrapper printItemWrapper)
        {
            UiThread.RunOnIdle((state) =>
            {
                string maxLengthName = printItemWrapper.FileLocation;
                int maxLength = 43;
                if (maxLengthName.Length > maxLength)
                {
                    string start = maxLengthName.Substring(0, 15) + "...";
                    int amountRemaining = (maxLength - start.Length);
                    string end = maxLengthName.Substring(maxLengthName.Length - amountRemaining, amountRemaining);
                    maxLengthName = start + end;
                }
				string notFoundMessage = LocalizedString.Get("Oops! Could not find this file");
				string notFoundMessageEnd = LocalizedString.Get("Would you like to remove it from the queue");
				string message = "{0}:\n'{1}'\n\n{2}?".FormatWith(notFoundMessage, maxLengthName,notFoundMessageEnd);
				string titleLabel = LocalizedString.Get("Item not Found");
					if (StyledMessageBox.ShowMessageBox(message, titleLabel, StyledMessageBox.MessageType.YES_NO))
                {
                    QueueData.Instance.RemoveIndexOnIdle(QueueData.Instance.GetIndex(printItemWrapper));
                }
            });
        }

        public void ThemeChanged(object sender, EventArgs e)
        {
            if (this.isActivePrint)
            {
                //Set background and text color to new theme
                this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
                this.partLabel.TextColor = RGBA_Bytes.White;
                this.partStatus.TextColor = RGBA_Bytes.White;
                this.Invalidate();
            }
        }

        public void SetTextColors(RGBA_Bytes color)
        {
            this.partLabel.TextColor = color;
            this.partStatus.TextColor = color;

            editControls.SendToChildren(new ChangeTextColorEventArgs(color));
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            base.OnDraw(graphics2D);
            
            if (this.isActivePrint)
            {
                //RectangleDouble Bounds = LocalBounds;
                //RoundedRect rectBorder = new RoundedRect(Bounds, 0);

                this.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
                SetTextColors(RGBA_Bytes.White);

                //graphics2D.Render(new Stroke(rectBorder, 4), ActiveTheme.Instance.SecondaryAccentColor);
            }            
            else if (this.IsHoverItem)
            {
                RectangleDouble Bounds = LocalBounds;
                RoundedRect rectBorder = new RoundedRect(Bounds, 0);

                this.BackgroundColor = RGBA_Bytes.White;
                SetTextColors(RGBA_Bytes.Black);

                graphics2D.Render(new Stroke(rectBorder, 3), ActiveTheme.Instance.SecondaryAccentColor);
            }
			else
			{	
				this.BackgroundColor = RGBA_Bytes.White;
                SetTextColors(RGBA_Bytes.Black);                
			}
        }
    }
}
