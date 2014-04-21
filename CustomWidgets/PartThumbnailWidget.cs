/*
Copyright (c) 2014, Lars Brubaker
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
using System.ComponentModel;

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
        static BackgroundWorker createThumbnailWorker = null;

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

        // all the color stuff
        public double BorderWidth = 0; //Don't delete this - required for OnDraw
        protected double borderRadius = 0;
        protected RGBA_Bytes HoverBorderColor = new RGBA_Bytes();

        public RGBA_Bytes FillColor = new RGBA_Bytes(255, 255, 255);
        public RGBA_Bytes HoverBackgroundColor = new RGBA_Bytes(0, 0, 0, 50);
        RGBA_Bytes normalBackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

        bool thumbNailHasBeenRequested = false;

        public enum ImageSizes { Size50x50, Size115x115 };

        public ImageSizes Size { get; set; }

        event EventHandler unregisterEvents;
        public PartThumbnailWidget(PrintItemWrapper item, string noThumbnailFileName, string buildingThumbnailFileName, ImageSizes size)
        {
            this.PrintItem = item;

            // Set Display Attributes
            this.Margin = new BorderDouble(0);
            this.Padding = new BorderDouble(5);
            Size = size;
            switch(size)
            {
                case ImageSizes.Size50x50:
                    this.Width = 50;
                    this.Height = 50;
                    this.MinimumSize = new Vector2(50, 50);
                    break;

                case ImageSizes.Size115x115:
                    this.Width = 115;
                    this.Height = 115;
                    this.MinimumSize = new Vector2(115, 115);
                    break;

                default:
                    throw new NotImplementedException();
            }

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
        }

        void item_FileHasChanged(object sender, EventArgs e)
        {
            thumbNailHasBeenRequested = false;
            Invalidate();
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

        void createThumbnailWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            PartThumbnailWidget thumbnailWidget = e.Argument as PartThumbnailWidget;
            if (thumbnailWidget != null)
            {
                ImageBuffer image50x50;
                ImageBuffer image115x115;

                if (thumbnailWidget.printItem == null)
                {
                    thumbnailWidget.image = new ImageBuffer(thumbnailWidget.noThumbnailImage);
                    thumbnailWidget.Invalidate();
                    return;
                }
                
                string stlHashCode = thumbnailWidget.PrintItem.StlFileHashCode.ToString();

                image50x50 = LoadImageFromDisk(thumbnailWidget, stlHashCode, new Point2D(50, 50));
                image115x115 = LoadImageFromDisk(thumbnailWidget, stlHashCode, new Point2D(115, 115));
                if (image50x50 == null || image115x115 == null)
                {
                    Mesh loadedMesh = StlProcessing.Load(thumbnailWidget.PrintItem.FileLocation);

                    if (image50x50 == null)
                    {
                        thumbnailWidget.image = new ImageBuffer(thumbnailWidget.buildingThumbnailImage);
                        image50x50 = BuildImageFromSTL(loadedMesh, stlHashCode, new Point2D(50, 50));
                        if (image50x50 == null)
                        {
                            thumbnailWidget.image = new ImageBuffer(thumbnailWidget.noThumbnailImage);
                        }
                    }
                    if (image115x115 == null)
                    {
                        thumbnailWidget.image = new ImageBuffer(thumbnailWidget.buildingThumbnailImage);
                        image115x115 = BuildImageFromSTL(loadedMesh, stlHashCode, new Point2D(115, 115));
                        if (image115x115 == null)
                        {
                            thumbnailWidget.image = new ImageBuffer(thumbnailWidget.noThumbnailImage);
                        }
                    }
                }

                switch (thumbnailWidget.Size)
                {
                    case ImageSizes.Size50x50:
                        if (image50x50 != null)
                        {
                            thumbnailWidget.image = new ImageBuffer(image50x50);
                        }
                        break;

                    case ImageSizes.Size115x115:
                        if (image115x115 != null)
                        {
                            thumbnailWidget.image = new ImageBuffer(image115x115);
                        }
                        break;

                    default:
                        throw new NotImplementedException();
                }

                thumbnailWidget.Invalidate();
            }
        }

        private static ImageBuffer LoadImageFromDisk(PartThumbnailWidget thumbnailWidget, string stlHashCode, Point2D size)
        {
            ImageBuffer tempImage = new ImageBuffer(size.x, size.y, 32, new BlenderBGRA());
            string applicationUserDataPath = ApplicationDataStorage.Instance.ApplicationUserDataPath;
            string folderToSavePrintsTo = Path.Combine(applicationUserDataPath, "data", "temp", "thumbnails");
            string pngFileName = Path.Combine(folderToSavePrintsTo, "{0}_{1}x{2}.png".FormatWith(stlHashCode, size.x, size.y));

            if (File.Exists(pngFileName))
            {
                if (ImageIO.LoadImageData(pngFileName, tempImage))
                {
                    return tempImage;
                }
            }

            return null;
        }

        private static ImageBuffer BuildImageFromSTL(Mesh loadedMesh, string stlHashCode, Point2D size)
        {
            if(loadedMesh != null)
            {
                ImageBuffer tempImage = new ImageBuffer(size.x, size.y, 32, new BlenderBGRA());
                Graphics2D partGraphics2D = tempImage.NewGraphics2D();
                partGraphics2D.Clear(new RGBA_Bytes());

                List<MeshEdge> nonManifoldEdges = loadedMesh.GetNonManifoldEdges();
                if (nonManifoldEdges.Count > 0)
                {
                    if (File.Exists("RunUnitTests.txt"))
                    {
                        partGraphics2D.Circle(4, 4, 4, RGBA_Bytes.Red);
                    }
                }
                nonManifoldEdges = null;

                AxisAlignedBoundingBox aabb = loadedMesh.GetAxisAlignedBoundingBox();
                double maxSize = Math.Max(aabb.XSize, aabb.YSize);
                double scale = size.x / (maxSize * 1.2);
                RectangleDouble bounds2D = new RectangleDouble(aabb.minXYZ.x, aabb.minXYZ.y, aabb.maxXYZ.x, aabb.maxXYZ.y);
                PolygonMesh.Rendering.OrthographicZProjection.DrawTo(partGraphics2D, loadedMesh,
                    new Vector2((size.x / scale - bounds2D.Width) / 2 - bounds2D.Left,
                        (size.y / scale - bounds2D.Height) / 2 - bounds2D.Bottom),
                    scale, RGBA_Bytes.White);

                // and save it to disk
                string applicationUserDataPath = ApplicationDataStorage.Instance.ApplicationUserDataPath;
                string folderToSavePrintsTo = Path.Combine(applicationUserDataPath, "data", "temp", "thumbnails");
                string pngFileName = Path.Combine(folderToSavePrintsTo, "{0}_{1}x{2}.png".FormatWith(stlHashCode, size.x, size.y));

                if (!Directory.Exists(folderToSavePrintsTo))
                {
                    Directory.CreateDirectory(folderToSavePrintsTo);
                }
                ImageIO.SaveImageData(pngFileName, tempImage);

                // and give it back
                return tempImage;
            }

            return null;
        }

        void createThumbnailWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            createThumbnailWorker = null;
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
                    QueueRowItem.ShowCantFindFileMessage(printItem);
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
                if (createThumbnailWorker == null)
                {
                    createThumbnailWorker = new BackgroundWorker();
                    createThumbnailWorker.DoWork += new DoWorkEventHandler(createThumbnailWorker_DoWork);
                    createThumbnailWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(createThumbnailWorker_RunWorkerCompleted);
                    createThumbnailWorker.RunWorkerAsync(this);
                    thumbNailHasBeenRequested = true;
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

        string GetImageLocation(string imageName)
        {
            return Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, imageName);
        }
    }
}
