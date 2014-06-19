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
using System.ComponentModel;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
    public class PartThumbnailWidget : ClickWidget
    {
        static BackgroundWorker createThumbnailWorker = null;

        PrintItemWrapper printItem;
		PartPreviewMainWindow partPreviewWindow;
		bool partPreviewWindowIsOpen = false;

        public PrintItemWrapper PrintItem
        {
            get { return printItem; }
            set
            {
                if (printItem != null)
                {
                    printItem.FileHasChanged.UnregisterEvent(item_FileHasChanged, ref unregisterEvents);
                }
                printItem = value;
                thumbNailHasBeenRequested = false;
                if (printItem != null)
                {
                    printItem.FileHasChanged.RegisterEvent(item_FileHasChanged, ref unregisterEvents);
                }
            }
        }

        ImageBuffer buildingThumbnailImage = new Agg.Image.ImageBuffer();
        ImageBuffer noThumbnailImage = new Agg.Image.ImageBuffer();
        ImageBuffer tumbnailImage = new Agg.Image.ImageBuffer();

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
            this.tumbnailImage = new ImageBuffer(buildingThumbnailImage);

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
                printItem.FileHasChanged.UnregisterEvent(item_FileHasChanged, ref unregisterEvents);
            }
            base.OnClosed(e);
        }

        void createThumbnailWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            PartThumbnailWidget thumbnailWidget = e.Argument as PartThumbnailWidget;
            if (thumbnailWidget != null)
            {
                if (thumbnailWidget.printItem == null)
                {
                    thumbnailWidget.tumbnailImage = new ImageBuffer(thumbnailWidget.noThumbnailImage);
                    thumbnailWidget.Invalidate();
                    return;
                }

                if (thumbnailWidget.PrintItem.FileLocation == QueueData.SdCardFileName)
                {
                    switch (thumbnailWidget.Size)
                    {
                        case ImageSizes.Size115x115:
                            {
                                ImageIO.LoadImageData(this.GetImageLocation("icon_sd_card_115x115.png"), thumbnailWidget.tumbnailImage);
                                thumbnailWidget.tumbnailImage.SetRecieveBlender(new BlenderPreMultBGRA());
                                Graphics2D graphics = thumbnailWidget.tumbnailImage.NewGraphics2D();
                                Ellipse outline = new Ellipse(new Vector2(115 / 2.0, 115 / 2.0), 50);
                                graphics.Render(new Stroke(outline, 4), RGBA_Bytes.White);
                            }
                            break;

                        case ImageSizes.Size50x50:
                            {
                                ImageIO.LoadImageData(this.GetImageLocation("icon_sd_card_50x50.png"), thumbnailWidget.tumbnailImage);
                                thumbnailWidget.tumbnailImage.SetRecieveBlender(new BlenderPreMultBGRA());
                                Graphics2D graphics = thumbnailWidget.tumbnailImage.NewGraphics2D();
                                Ellipse outline = new Ellipse(new Vector2(50 / 2.0, 50 / 2.0), 22);
                                graphics.Render(new Stroke(outline, 1.5), RGBA_Bytes.White);
                            }
                            break;

                        default:
                            throw new NotImplementedException();
                    }

                    UiThread.RunOnIdle(thumbnailWidget.EnsureImageUpdated);
                    return;
                }
                
                string stlHashCode = thumbnailWidget.PrintItem.StlFileHashCode.ToString();

                Point2D bigRenderSize = new Point2D(460, 460);
                ImageBuffer bigRender = LoadImageFromDisk(thumbnailWidget, stlHashCode, bigRenderSize);
                if (bigRender == null)
                {
                    Mesh loadedMesh = StlProcessing.Load(thumbnailWidget.PrintItem.FileLocation);

                    thumbnailWidget.tumbnailImage = new ImageBuffer(thumbnailWidget.buildingThumbnailImage);
                    thumbnailWidget.tumbnailImage.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255, 0));
                    bigRender = BuildImageFromSTL(loadedMesh, stlHashCode, bigRenderSize);
                    if (bigRender == null)
                    {
                        bigRender = new ImageBuffer(thumbnailWidget.noThumbnailImage);
                    }
                }

                switch (thumbnailWidget.Size)
                {
                    case ImageSizes.Size50x50:
                        {
                            ImageBuffer halfWay1 = new ImageBuffer(200, 200, 32, new BlenderBGRA());
                            halfWay1.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255, 0));
                            halfWay1.NewGraphics2D().Render(bigRender, 0, 0, 0, (double)halfWay1.Width / bigRender.Width, (double)halfWay1.Height / bigRender.Height);

                            ImageBuffer halfWay2 = new ImageBuffer(100, 100, 32, new BlenderBGRA());
                            halfWay2.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255, 0));
                            halfWay2.NewGraphics2D().Render(halfWay1, 0, 0, 0, (double)halfWay2.Width / halfWay1.Width, (double)halfWay2.Height / halfWay1.Height);

                            thumbnailWidget.tumbnailImage = new ImageBuffer(50, 50, 32, new BlenderBGRA());
                            thumbnailWidget.tumbnailImage.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255, 0));
                            thumbnailWidget.tumbnailImage.NewGraphics2D().Render(halfWay2, 0, 0, 0, (double)thumbnailWidget.tumbnailImage.Width / halfWay2.Width, (double)thumbnailWidget.tumbnailImage.Height / halfWay2.Height);
                        }
                        break;

                    case ImageSizes.Size115x115:
                        {
                            ImageBuffer halfWay1 = new ImageBuffer(230, 230, 32, new BlenderBGRA());
                            halfWay1.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255, 0));
                            halfWay1.NewGraphics2D().Render(bigRender, 0, 0, 0, (double)halfWay1.Width / bigRender.Width, (double)halfWay1.Height / bigRender.Height);

                            thumbnailWidget.tumbnailImage = new ImageBuffer(115, 115, 32, new BlenderBGRA());
                            thumbnailWidget.tumbnailImage.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255, 0));
                            thumbnailWidget.tumbnailImage.NewGraphics2D().Render(halfWay1, 0, 0, 0, (double)thumbnailWidget.tumbnailImage.Width / halfWay1.Width, (double)thumbnailWidget.tumbnailImage.Height / halfWay1.Height);
                        }
                        break;

                    default:
                        throw new NotImplementedException();
                }

                UiThread.RunOnIdle(thumbnailWidget.EnsureImageUpdated);
            }
        }

        void EnsureImageUpdated(object state)
        {
            tumbnailImage.MarkImageChanged();
            Invalidate();
        }

        private static ImageBuffer LoadImageFromDisk(PartThumbnailWidget thumbnailWidget, string stlHashCode, Point2D size)
        {
            ImageBuffer tempImage = new ImageBuffer(size.x, size.y, 32, new BlenderBGRA());
            string applicationUserDataPath = ApplicationDataStorage.Instance.ApplicationUserDataPath;
            string folderToSavePrintsTo = Path.Combine(applicationUserDataPath, "data", "temp", "thumbnails");
            string tgaFileName = Path.Combine(folderToSavePrintsTo, "{0}_{1}x{2}.tga".FormatWith(stlHashCode, size.x, size.y));

            if (File.Exists(tgaFileName))
            {
                if (ImageTgaIO.LoadImageData(tgaFileName, tempImage))
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
                string tgaFileName = Path.Combine(folderToSavePrintsTo, "{0}_{1}x{2}.tga".FormatWith(stlHashCode, size.x, size.y));

                if (!Directory.Exists(folderToSavePrintsTo))
                {
                    Directory.CreateDirectory(folderToSavePrintsTo);
                }
                ImageTgaIO.SaveImageData(tgaFileName, tempImage);

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
                    bool shiftKeyDown = Keyboard.IsKeyDown(Keys.ShiftKey);
                    if (shiftKeyDown)
                    {
                        OpenPartPreviewWindow (View3DTransformPart.AutoRotate.Disabled);
                    }
                    else
                    {
                        OpenPartPreviewWindow (View3DTransformPart.AutoRotate.Enabled);
                    }
                }
                else
                {
                    QueueRowItem.ShowCantFindFileMessage(printItem);
                }
            }
        }

		void PartPreviewWindow_Closed(object sender, EventArgs e)
		{
			this.partPreviewWindowIsOpen = false;
		}

		private void OpenPartPreviewWindow(View3DTransformPart.AutoRotate autoRotate)
		{
			if (partPreviewWindowIsOpen == false)
			{
                partPreviewWindow = new PartPreviewMainWindow(this.PrintItem, autoRotate);
				this.partPreviewWindowIsOpen = true;
				partPreviewWindow.Closed += new EventHandler (PartPreviewWindow_Closed);
			}
			else
			{
				if (partPreviewWindow != null)
				{
					partPreviewWindow.BringToFront ();
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
            graphics2D.Render(tumbnailImage, Width / 2 - tumbnailImage.Width / 2, Height / 2 - tumbnailImage.Height / 2);
            base.OnDraw(graphics2D);

            RectangleDouble Bounds = LocalBounds;
            RoundedRect borderRect = new RoundedRect(this.LocalBounds, this.borderRadius);
            Stroke strokeRect = new Stroke(borderRect, BorderWidth);
            graphics2D.Render(strokeRect, HoverBorderColor);
        }

        string GetImageLocation(string imageName)
        {
            return Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "Icons", imageName);
        }
    }
}
