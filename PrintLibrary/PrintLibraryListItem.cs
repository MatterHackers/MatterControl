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
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.Localizations;

using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;

namespace MatterHackers.MatterControl.PrintLibrary
{
    public class LibraryThumbnailWidget : ClickWidget
    {
        static Thread thumbNailThread = null;
        private PrintItemWrapper printItem;
        public PrintItemWrapper PrintItem
        {
            get { return printItem; }
            set
            {
                if (printItem != null)
                {
                    printItem.FileHasChanged -= item_FileHasChanged;
                }
                printItem = value;
                thumbNailHasBeenRequested = false;
                if (printItem != null)
                {
                    printItem.FileHasChanged += item_FileHasChanged;
                }
            }
        }

        ImageBuffer buildingThumbnailImage = new Agg.Image.ImageBuffer();
        ImageBuffer noThumbnailImage = new Agg.Image.ImageBuffer();
        ImageBuffer image = new Agg.Image.ImageBuffer();

        // all te color stuff
        protected double borderRadius = 0;
        protected RGBA_Bytes HoverBorderColor = new RGBA_Bytes();

        public RGBA_Bytes FillColor = ActiveTheme.Instance.PrimaryAccentColor;
        public RGBA_Bytes HoverBackgroundColor = new RGBA_Bytes(0, 0, 0, 50);
        RGBA_Bytes normalBackgroundColor = new RGBA_Bytes(255, 255, 255,0);

        bool thumbNailHasBeenRequested = false;

        event EventHandler unregisterEvents;
        public LibraryThumbnailWidget(PrintItemWrapper item, string noThumbnailFileName, string buildingThumbnailFileName, Vector2 size)
        {
            this.PrintItem = item;

            // Set Display Attributes
            this.Margin = new BorderDouble(0);
            this.Padding = new BorderDouble(5);
            this.Width = size.x;
            this.Height = size.y;
            this.MinimumSize = size;
            this.BackgroundColor = normalBackgroundColor;
            this.Cursor = Cursors.Hand;

            // set background images
            if (noThumbnailImage.Width == 0)
            {
                ImageIO.LoadImageData(this.GetImageLocation(noThumbnailFileName), noThumbnailImage);
                ImageIO.LoadImageData(this.GetImageLocation(buildingThumbnailFileName), buildingThumbnailImage);
            }
            this.image = new ImageBuffer(buildingThumbnailImage);

            // Add Handlers
            this.Click += new ButtonEventHandler(onMouseClick);
            this.MouseEnterBounds += new EventHandler(onEnter);
            this.MouseLeaveBounds += new EventHandler(onExit);
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(onThemeChanged, ref unregisterEvents);

            CreateThumNailThreadIfNeeded();
        }

        void item_FileHasChanged(object sender, EventArgs e)
        {
            thumbNailHasBeenRequested = false;
            Invalidate();
        }

        private static void CreateThumNailThreadIfNeeded()
        {
            if (thumbNailThread == null)
            {
                thumbNailThread = new Thread(CreateThumbnailsThread);
                thumbNailThread.Name = "Queue Create Thumbnail";
                thumbNailThread.IsBackground = true;
                thumbNailThread.Start();
            }
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            if (printItem != null)
            {
                printItem.FileHasChanged -= item_FileHasChanged;
            }
            base.OnClosed(e);
        }

        static List<LibraryThumbnailWidget> listOfWidgetsNeedingThumbnails = new List<LibraryThumbnailWidget>();
        static void CreateThumbnailsThread()
        {
            while (true)
            {
                if (listOfWidgetsNeedingThumbnails.Count > 0)
                {
                    LibraryThumbnailWidget thumbnailWidget = listOfWidgetsNeedingThumbnails[0];
                    if (thumbnailWidget.printItem == null)
                    {
                        thumbnailWidget.image = new ImageBuffer(thumbnailWidget.noThumbnailImage);
                    }
                    else // generate the image
                    {
                        Mesh loadedMesh = StlProcessing.Load(thumbnailWidget.printItem.FileLocation);

                        thumbnailWidget.image = new ImageBuffer(thumbnailWidget.buildingThumbnailImage);
                        thumbnailWidget.Invalidate();

                        if (loadedMesh != null)
                        {
                            ImageBuffer tempImage = new ImageBuffer(thumbnailWidget.image.Width, thumbnailWidget.image.Height, 32, new BlenderBGRA());
                            Graphics2D partGraphics2D = tempImage.NewGraphics2D();

                            List<MeshEdge> nonManifoldEdges = loadedMesh.GetNonManifoldEdges();
                            if (nonManifoldEdges.Count > 0)
                            {
                                if (File.Exists("RunUnitTests.txt"))
                                {
                                    partGraphics2D.Circle(4, 4, 4, RGBA_Bytes.Red);
                                }
                            }

                            AxisAlignedBoundingBox aabb = loadedMesh.GetAxisAlignedBoundingBox();
                            double maxSize = Math.Max(aabb.XSize, aabb.YSize);
                            double scale = thumbnailWidget.image.Width / (maxSize * 1.2);
                            RectangleDouble bounds2D = new RectangleDouble(aabb.minXYZ.x, aabb.minXYZ.y, aabb.maxXYZ.x, aabb.maxXYZ.y);
                            PolygonMesh.Rendering.OrthographicZProjection.DrawTo(partGraphics2D, loadedMesh,
                                new Vector2((thumbnailWidget.image.Width / scale - bounds2D.Width) / 2 - bounds2D.Left,
                                    (thumbnailWidget.image.Height / scale - bounds2D.Height) / 2 - bounds2D.Bottom),
                                scale,
                                thumbnailWidget.FillColor);

                            thumbnailWidget.image = new ImageBuffer(tempImage);
                        }
                        else
                        {
                            thumbnailWidget.image = new ImageBuffer(thumbnailWidget.noThumbnailImage);
                        }
                    }
                    thumbnailWidget.Invalidate();

                    using (TimedLock.Lock(listOfWidgetsNeedingThumbnails, "CreateThumbnailsThread()"))
                    {
                        listOfWidgetsNeedingThumbnails.RemoveAt(0);

                        foreach (LibraryThumbnailWidget part in listOfWidgetsNeedingThumbnails)
                        {
                            // mark them so we try to add them again if needed
                            part.thumbNailHasBeenRequested = false;
                        }

                        listOfWidgetsNeedingThumbnails.Clear();
                    }
                }
                Thread.Sleep(100);
            }
        }

        private void onThemeChanged(object sender, EventArgs e)
        {
            //Set background color to new theme
            this.normalBackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
            this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

            //Regenerate thumbnails
            // The thumbnail color is currently white and does not change with this change.
            // If we eventually change the thumbnail color with the theme we will need to change this.
            //this.thumbNailHasBeenRequested = false;
            this.Invalidate();
        }
			
        private void onMouseClick(object sender, MouseEventArgs e)
        {
            if (printItem != null)
            {
                string pathAndFile = printItem.FileLocation;
                if (File.Exists(pathAndFile))
                {
					new PartPreviewMainWindow(printItem);
                }
                else
                {
                    RowItem.ShowCantFindFileMessage(printItem);
                }
            }
        }

        private void onEnter(object sender, EventArgs e)
        {
            HoverBorderColor = new RGBA_Bytes(255, 255, 255);
            this.Invalidate();
        }

        private void onExit(object sender, EventArgs e)
        {
            HoverBorderColor = new RGBA_Bytes();
            this.Invalidate();
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            RoundedRect rectBorder = new RoundedRect(this.LocalBounds, 0);

            //Trigger thumbnail generation if neeeded
            if (!thumbNailHasBeenRequested)
            {
                thumbNailHasBeenRequested = true;
                using (TimedLock.Lock(listOfWidgetsNeedingThumbnails, "PrintQueueItem OnDraw"))
                {
                    //Add to thumbnail generation queue
                    listOfWidgetsNeedingThumbnails.Add(this);
                }
            }
            if (this.FirstWidgetUnderMouse)
            {
                //graphics2D.Render(rectBorder, this.HoverBackgroundColor);
            }
            graphics2D.Render(image, Width / 2 - image.Width / 2, Height / 2 - image.Height / 2);
            base.OnDraw(graphics2D);

            RectangleDouble Bounds = LocalBounds;
            RoundedRect borderRect = new RoundedRect(this.LocalBounds, this.borderRadius);
            Stroke strokeRect = new Stroke(borderRect, BorderWidth);
            graphics2D.Render(strokeRect, HoverBorderColor);
        }

        private string GetImageLocation(string imageName)
        {
            return Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, imageName);
        }
    }
    
    public class PrintLibraryListItem : ClickWidget
    {
        public PrintItemWrapper printItem;
        public RGBA_Bytes WidgetTextColor;
        public RGBA_Bytes WidgetBackgroundColor;

        public bool isActivePrint = false;
        public bool isSelectedItem = false;
        public bool isHoverItem = false;
        TextWidget partLabel;
        Button viewLink;
        Button removeLink;
        Button exportLink;
        Button addToQueueLink;
        public CheckBox selectionCheckBox;
        FlowLayoutWidget buttonContainer;
        LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
		bool exportWindowIsOpen = false;
		bool viewWindowIsOpen = false;
		PartPreviewMainWindow viewingWindow;
		ExportLibraryItemWindow exportingWindow;

		private void OpenExportWindow()
		{
			if (exportWindowIsOpen == false)
			{
				exportingWindow = new ExportLibraryItemWindow(this);
				this.exportWindowIsOpen = true;
				exportingWindow.Closed += new EventHandler(ExportLibraryItemWindow_Closed);
				exportingWindow.ShowAsSystemWindow ();
			}
			else 
			{
				if (exportingWindow != null)
				{
					exportingWindow.BringToFront ();
				}
			}

		}

		void ExportLibraryItemWindow_Closed(object sender, EventArgs e)
		{
			this.exportWindowIsOpen = false;
		}

        public PrintLibraryListItem(PrintItemWrapper printItem)
        {
            this.printItem = printItem;
            linkButtonFactory.fontSize = 10;
            linkButtonFactory.textColor = RGBA_Bytes.White;

            WidgetTextColor = RGBA_Bytes.Black;
            WidgetBackgroundColor = RGBA_Bytes.White;

            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

            SetDisplayAttributes();

            FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            mainContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            {
                GuiWidget selectionCheckBoxContainer = new GuiWidget();
                selectionCheckBoxContainer.VAnchor = VAnchor.Max_FitToChildren_ParentHeight;
                selectionCheckBoxContainer.HAnchor = Agg.UI.HAnchor.FitToChildren;
                selectionCheckBoxContainer.Margin = new BorderDouble(left: 6);
                selectionCheckBox = new CheckBox("");
                selectionCheckBox.VAnchor = VAnchor.ParentCenter;
                selectionCheckBox.HAnchor = HAnchor.ParentCenter;
                selectionCheckBoxContainer.AddChild(selectionCheckBox);
                
                FlowLayoutWidget leftColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
                leftColumn.VAnchor |= VAnchor.ParentTop;


                FlowLayoutWidget middleColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
                middleColumn.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                middleColumn.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
                middleColumn.Padding = new BorderDouble(0,6);
                middleColumn.Margin = new BorderDouble(10,0);
                {
                    string labelName = textInfo.ToTitleCase(printItem.Name);
                    labelName = labelName.Replace('_', ' ');
                    partLabel = new TextWidget(labelName, pointSize: 12);
                    partLabel.TextColor = WidgetTextColor;
                    partLabel.MinimumSize = new Vector2(1, 16);
                    middleColumn.AddChild(partLabel);
                }

                FlowLayoutWidget rightColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
                rightColumn.VAnchor = Agg.UI.VAnchor.ParentBottomTop;                

                buttonContainer = new FlowLayoutWidget();
                buttonContainer.Margin = new BorderDouble(0,6);
                buttonContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight; 
                {
					addToQueueLink = linkButtonFactory.Generate(LocalizedString.Get("Add to Queue"));
                    addToQueueLink.Margin = new BorderDouble(left: 0, right: 10);
                    addToQueueLink.VAnchor = VAnchor.ParentCenter;

                    addToQueueLink.Click += (sender, e) =>
                    {
                        QueueData.Instance.AddItem(new PrintItem(this.printItem.Name, this.printItem.FileLocation));
                    };

					viewLink = linkButtonFactory.Generate(LocalizedString.Get("View"));
                    viewLink.Margin = new BorderDouble(left: 0, right:10);
                    viewLink.VAnchor = VAnchor.ParentCenter;                    

					exportLink = linkButtonFactory.Generate(LocalizedString.Get("Export"));
                    exportLink.Margin = new BorderDouble(left: 0, right: 10);
                    exportLink.VAnchor = VAnchor.ParentCenter;

                    exportLink.Click += (sender, e) =>
                    {
						OpenExportWindow();
                    };

					removeLink = linkButtonFactory.Generate(LocalizedString.Get("Remove"));
                    removeLink.Margin = new BorderDouble(left: 10, right: 10);
                    removeLink.VAnchor = VAnchor.ParentCenter;

                    buttonContainer.AddChild(addToQueueLink);
                    buttonContainer.AddChild(viewLink);
                    buttonContainer.AddChild(exportLink);
                    buttonContainer.AddChild(removeLink);
                }
                middleColumn.AddChild(buttonContainer);
                //rightColumn.AddChild(buttonContainer);

                mainContainer.AddChild(selectionCheckBoxContainer);
                {
                    PartThumbnailWidget thumbnailWidget = new PartThumbnailWidget(printItem, "part_icon_transparent_40x40.png", "building_thumbnail_40x40.png", new Vector2(50, 50));                    
                    mainContainer.AddChild(thumbnailWidget);
                }
                mainContainer.AddChild(leftColumn);
                mainContainer.AddChild(middleColumn);                
                mainContainer.AddChild(rightColumn);
            }
            this.AddChild(mainContainer);
            AddHandlers();
        }

        void SetDisplayAttributes()
        {
            this.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;
            this.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            this.Height = 28;
			this.BackgroundColor = this.WidgetBackgroundColor;
            this.Padding = new BorderDouble(0);
            this.Margin = new BorderDouble(6,0,6,6);
        }

        event EventHandler unregisterEvents;
        void AddHandlers()
        {
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(onThemeChanged, ref unregisterEvents);
            //this.Click += new ButtonEventHandler(PrintLibraryListItem_Click);
            viewLink.Click += new ButtonBase.ButtonEventHandler(onViewLinkClick);
            removeLink.Click += new ButtonBase.ButtonEventHandler(onRemoveLinkClick);
            selectionCheckBox.CheckedStateChanged += selectionCheckBox_CheckedStateChanged;
        }

        void PrintLibraryListItem_Click(object sender, EventArgs e)
        {
            selectionCheckBox.Checked = !selectionCheckBox.Checked;
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        private void onClick(object sender, MouseEventArgs e)
        {
            if (this.isSelectedItem == false)
            {
                this.isSelectedItem = true;
                PrintLibraryListControl.Instance.SelectedItems.Add(this);
            }
        }

        private void selectionCheckBox_CheckedStateChanged(object sender, EventArgs e)
        {            
            if (selectionCheckBox.Checked == true)
            {
                this.isSelectedItem = true;
                PrintLibraryListControl.Instance.SelectedItems.Add(this);
            }
            else
            {
                this.isSelectedItem = false;
                PrintLibraryListControl.Instance.SelectedItems.Remove(this);
            }
        }

        private void onAddLinkClick(object sender, MouseEventArgs e)
        {
        }

        void RemoveThisFromPrintLibrary(object state)
        {
            PrintLibraryListControl.Instance.RemoveChild(this);
            this.printItem.Delete();
        }

        private void onRemoveLinkClick(object sender, MouseEventArgs e)
        {
            UiThread.RunOnIdle(RemoveThisFromPrintLibrary);
        }

        private void onViewLinkClick(object sender, MouseEventArgs e)
        {
            UiThread.RunOnIdle(onViewLinkClick);
        }


		private void OpenPartViewWindow()
		{
			if (viewWindowIsOpen == false)
			{
				viewingWindow =  new PartPreviewMainWindow(this.printItem);
				this.viewWindowIsOpen = true;
				viewingWindow.Closed += new EventHandler(PartPreviewMainWindow_Closed); 
			}
			else
			{
				if(viewingWindow != null)
				{
					viewingWindow.BringToFront();
				}
			}

		}

		void PartPreviewMainWindow_Closed(object sender, EventArgs e)
		{
			viewWindowIsOpen = false;
		}


        private void onViewLinkClick(object state)
        {
            string pathAndFile = this.printItem.FileLocation;
            Console.WriteLine(pathAndFile);
            if (File.Exists(pathAndFile))
            {
				OpenPartViewWindow ();
            }
            else
            {
                string message = String.Format("Cannot find\n'{0}'.\nWould you like to remove it from the queue?", pathAndFile);
                if (StyledMessageBox.ShowMessageBox(message, "Item not found", StyledMessageBox.MessageType.YES_NO))
                {
                    PrintLibraryListControl.Instance.RemoveChild(this);
                }
            }
        }

        private void onThemeChanged(object sender, EventArgs e)
        {
            //Set background and text color to new theme
            this.Invalidate();
        }

        public override void OnDraw(Graphics2D graphics2D)
        {

            if (this.isHoverItem)
            {
                buttonContainer.Visible = true;
            }
            else
            {
                buttonContainer.Visible = false;
            }
            
            base.OnDraw(graphics2D);

            if (this.isSelectedItem)
            {
                this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
                this.partLabel.TextColor = RGBA_Bytes.White;
                this.selectionCheckBox.TextColor = RGBA_Bytes.White;

                //RectangleDouble Bounds = LocalBounds;
                //RoundedRect rectBorder = new RoundedRect(Bounds, 0);
                //graphics2D.Render(new Stroke(rectBorder, 3), RGBA_Bytes.White);
            }

            else if (this.isHoverItem)
            {
                RectangleDouble Bounds = LocalBounds;
                RoundedRect rectBorder = new RoundedRect(Bounds, 0);

                this.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
                this.partLabel.TextColor = RGBA_Bytes.White;
                this.selectionCheckBox.TextColor = RGBA_Bytes.White;

                graphics2D.Render(new Stroke(rectBorder, 3), ActiveTheme.Instance.PrimaryAccentColor);
            }
            else
            {
                this.BackgroundColor = new RGBA_Bytes(255, 255, 255, 255);
                this.partLabel.TextColor = RGBA_Bytes.Black;
                this.selectionCheckBox.TextColor = RGBA_Bytes.Black;
            }

        }
    }
}
