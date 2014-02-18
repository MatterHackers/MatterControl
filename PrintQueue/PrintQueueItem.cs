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
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.Localizations;

using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.PrintQueue
{
    public class PrintQueueItem : GuiWidget
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
		ExportQueueItemWindow exportingWindow;
		bool exportingWindowIsOpen = false;


        public PrintQueueItem(PrintItemWrapper printItem)
        {
            this.PrintItemWrapper = printItem;
            ConstructPrintQueueItem();
        }

        public PrintQueueItem(string displayName, string fileLocation)
        {
            PrintItem printItem = new PrintItem();
            printItem.Name = displayName;
            printItem.FileLocation = fileLocation;
            this.PrintItemWrapper = new PrintItemWrapper(printItem);
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
                    PartThumbnailWidget thumbnailWidget = new PartThumbnailWidget(PrintItemWrapper, "part_icon_transparent_40x40.png", "building_thumbnail_40x40.png", new Vector2(50, 50));
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
					labelName = new LocalizedString (labelName).Translated;
                    partLabel = new TextWidget(labelName, pointSize: 14);
                    partLabel.TextColor = WidgetTextColor;
                    partLabel.MinimumSize = new Vector2(1, 16);

					string partStatusLblTxt = new LocalizedString ("Status").Translated;     
					string partStatusLblTxtTest = new LocalizedString ("Queued to Print").Translated;
					string partStatusLblTxtFull = string.Format("{0}: {1}", partStatusLblTxt,partStatusLblTxtTest);

					partStatus = new TextWidget(partStatusLblTxtFull, pointSize: 10);
                    partStatus.AutoExpandBoundsToText = true;
                    partStatus.TextColor = WidgetTextColor;
                    partStatus.MinimumSize = new Vector2(50, 12);

					middleColumn.AddChild(partLabel);
                    middleColumn.AddChild(partStatus);
                }

                CreateEditControls();

                topContentsFlowLayout.AddChild(leftColumn);
                topContentsFlowLayout.AddChild(middleColumn);
                topContentsFlowLayout.AddChild(editControls);

                editControls.Visible = false;
            }

            topToBottomLayout.AddChild(topContentsFlowLayout);
            this.AddChild(topToBottomLayout);

            AddHandlers();
        }

		private void OpenExportWindow()
		{
			if(exportingWindowIsOpen == false)
			{
				exportingWindow = new ExportQueueItemWindow (this);
				this.exportingWindowIsOpen = true;
				exportingWindow.Closed += new EventHandler (ExportQueueItemWindow_Closed);
				exportingWindow.ShowAsSystemWindow();
			} 
			else 
			{
				if (exportingWindow != null)
				{
					exportingWindow.BringToFront ();
				}
			}
		}

		void ExportQueueItemWindow_Closed(object sender, EventArgs e)
		{
			this.exportingWindowIsOpen = false;
		}

		
        private void CreateEditControls()
        {
            editControls = new FlowLayoutWidget();
            editControls.Margin = new BorderDouble(right: 10);
            editControls.VAnchor = Agg.UI.VAnchor.FitToChildren | Agg.UI.VAnchor.ParentCenter;
            {
                FlowLayoutWidget layoutLeftToRight = new FlowLayoutWidget();

                linkButtonFactory.margin = new BorderDouble(3);

                // view button
                {
					Button viewLink = linkButtonFactory.Generate(new LocalizedString("View").Translated);
                    viewLink.Click += (sender, e) =>
                    {
                        string pathAndFile = PrintItemWrapper.FileLocation;
                        if (File.Exists(pathAndFile))
                        {
                            new PartPreviewMainWindow(PrintItemWrapper);
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
					Button copyLink = linkButtonFactory.Generate(new LocalizedString("Copy").Translated);
                    copyLink.Click += (sender, e) =>
                    {
                        CreateCopyInQueue();
                    };
                    layoutLeftToRight.AddChild(copyLink);
                }

                // add to library button
                {
                    if (this.PrintItemWrapper.PrintItem.PrintItemCollectionID == PrintLibraryListControl.Instance.LibraryCollection.Id)
                    {
                        //rightColumnOptions.AddChild(new TextWidget("Libary Item"));
                    }
                }

                // the export menu
                {
					Button exportLink = linkButtonFactory.Generate(new LocalizedString("Export").Translated);
                    exportLink.Click += (sender, e) =>
                    {
						OpenExportWindow();
                        
                    };
                    layoutLeftToRight.AddChild(exportLink);
                }

                // spacer
                {
                    layoutLeftToRight.AddChild(new GuiWidget(10, 10));
                }

                // delete button
                {
					Button deleteLink = linkButtonFactory.Generate(new LocalizedString("Remove").Translated);
                    deleteLink.Click += (sender, e) =>
                    {
                        DeletePartFromQueue();
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
                            int thisIndexInQueue = PrintQueueControl.Instance.GetIndex(PrintItemWrapper);
                            PrintQueueControl.Instance.SwapItemsDurringUiAction(thisIndexInQueue, thisIndexInQueue - 1);
                        };
                        topToBottom.AddChild(moveUp);
                    }

                    // move down one button
                    {
                        Button moveDown = linkButtonFactory.Generate(" v ");
                        moveDown.Click += (sender, e) =>
                        {
                            int thisIndexInQueue = PrintQueueControl.Instance.GetIndex(PrintItemWrapper);
                            PrintQueueControl.Instance.SwapItemsDurringUiAction(thisIndexInQueue, thisIndexInQueue + 1);
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
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        event EventHandler unregisterEvents;
        void AddHandlers()
        {
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(onThemeChanged, ref unregisterEvents);

            PrintItemWrapper.SlicingOutputMessage += PrintItem_SlicingOutputMessage;

            MouseEnterBounds += new EventHandler(PrintQueueItem_MouseEnterBounds);
            MouseLeaveBounds += new EventHandler(PrintQueueItem_MouseLeaveBounds);
        }

        void PrintItem_SlicingOutputMessage(object sender, EventArgs e)
        {
            StringEventArgs message = e as StringEventArgs;
            partStatus.Text = string.Format("Status: {0}", message.Data);
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
            PrintQueueItem queueItem = new PrintQueueItem(partToAddToQueue.Name, partToAddToQueue.FileLocation);
            PrintQueueControl.Instance.AddChild(queueItem, partToAddToQueue.insertAfterIndex);
        }

        public void CreateCopyInQueue()
        {
            int thisIndexInQueue = PrintQueueControl.Instance.GetIndex(PrintItemWrapper);
            if (thisIndexInQueue != -1)
            {
                string applicationDataPath = ApplicationDataStorage.Instance.ApplicationUserDataPath;
                string stagingFolder = Path.Combine(applicationDataPath, "data", "temp", "stl");
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
                string[] itemNames = PrintQueueControl.Instance.GetItemNames();
                // figure out if we have a copy already and increment the number if we do
                while (true)
                {
                    if (itemNames.Contains(testName))
                    {
                        testName = string.Format("{0} {1}", newName, copyNumber);
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

        public void DeletePartFromQueue()
        {
            int thisIndexInQueue = PrintQueueControl.Instance.GetIndex(PrintItemWrapper);
            PrintQueueControl.Instance.RemoveIndex(thisIndexInQueue);
        }

        public static void ShowCantFindFileMessage(PrintItemWrapper printItem)
        {
            UiThread.RunOnIdle((state) =>
            {
                string maxLengthName = printItem.FileLocation;
                int maxLength = 43;
                if (maxLengthName.Length > maxLength)
                {
                    string start = maxLengthName.Substring(0, 15) + "...";
                    int amountRemaining = (maxLength - start.Length);
                    string end = maxLengthName.Substring(maxLengthName.Length - amountRemaining, amountRemaining);
                    maxLengthName = start + end;
                }
				string notFoundMessage = new LocalizedString("Oops! Could not find this file").Translated;
				string notFoundMessageEnd = new LocalizedString("Would you like to remove it from the queue").Translated;
				string message = String.Format("{0}:\n'{1}'\n\n{2}?",notFoundMessage, maxLengthName,notFoundMessageEnd);
				string titleLbl = new LocalizedString("Item not Found").Translated;
					if (StyledMessageBox.ShowMessageBox(message, titleLbl, StyledMessageBox.MessageType.YES_NO))
                {
                    PrintQueueControl.Instance.RemoveIndex(PrintQueueControl.Instance.GetIndex(printItem));
                }
            });
        }

        private void onThemeChanged(object sender, EventArgs e)
        {
			if (this.isActivePrint)
			{
				//Set background and text color to new theme
	            this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
	            this.partLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
	            this.partStatus.TextColor = ActiveTheme.Instance.PrimaryTextColor;
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
                SetTextColors(ActiveTheme.Instance.PrimaryTextColor);

                //graphics2D.Render(new Stroke(rectBorder, 4), ActiveTheme.Instance.SecondaryAccentColor);
            }            
            else if (this.isHoverItem)
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
