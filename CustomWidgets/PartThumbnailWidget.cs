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

using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.MatterControl.PrintQueue;

namespace MatterHackers.MatterControl
{
    public class PartThumbnailWidget : ClickWidget
    {
        static Thread thumbNailThread = null;

        PrintItemWrapper printItem;
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
        public double BorderWidth = 0;
        protected double borderRadius = 0;
        protected RGBA_Bytes HoverBorderColor = new RGBA_Bytes();

        public RGBA_Bytes FillColor = new RGBA_Bytes(255, 255, 255);
        public RGBA_Bytes HoverBackgroundColor = new RGBA_Bytes(0, 0, 0, 50);
        RGBA_Bytes normalBackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

        bool thumbNailHasBeenRequested = false;

        event EventHandler unregisterEvents;
        public PartThumbnailWidget(PrintItemWrapper item, string noThumbnailFileName, string buildingThumbnailFileName, Vector2 size)
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
                ImageBMPIO.LoadImageData(this.GetImageLocation(noThumbnailFileName), noThumbnailImage);
                ImageBMPIO.LoadImageData(this.GetImageLocation(buildingThumbnailFileName), buildingThumbnailImage);
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

        static List<PartThumbnailWidget> listOfWidgetsNeedingThumbnails = new List<PartThumbnailWidget>();
        static void CreateThumbnailsThread()
        {
            while (true)
            {
                if (listOfWidgetsNeedingThumbnails.Count > 0)
                {
                    PartThumbnailWidget thumbnailWidget = listOfWidgetsNeedingThumbnails[0];
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
                            PolygonMesh.Processors.OrthographicZProjection.DrawTo(partGraphics2D, loadedMesh,
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

                        foreach (PartThumbnailWidget part in listOfWidgetsNeedingThumbnails)
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
                    PrintQueueItem.ShowCantFindFileMessage(printItem);
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
                graphics2D.Render(rectBorder, this.HoverBackgroundColor);
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
}
