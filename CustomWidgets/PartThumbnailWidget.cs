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
        ImageBuffer thumbnailImage = new Agg.Image.ImageBuffer();

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
                    this.Width = 50 * TextWidget.GlobalPointSizeScaleRatio;
                    this.Height = 50 * TextWidget.GlobalPointSizeScaleRatio;
                    break;

                case ImageSizes.Size115x115:
                    this.Width = 115 * TextWidget.GlobalPointSizeScaleRatio;
                    this.Height = 115 * TextWidget.GlobalPointSizeScaleRatio;
                    break;

                default:
                    throw new NotImplementedException();
            }
            this.MinimumSize = new Vector2(this.Width, this.Height);

            this.BackgroundColor = normalBackgroundColor;
            this.Cursor = Cursors.Hand;

            // set background images
            if (noThumbnailImage.Width == 0)
            {
                ImageIO.LoadImageData(this.GetImageLocation(noThumbnailFileName), noThumbnailImage);
                ImageIO.LoadImageData(this.GetImageLocation(buildingThumbnailFileName), buildingThumbnailImage);
            }
            this.thumbnailImage = new ImageBuffer(buildingThumbnailImage);

            // Add Handlers
            this.Click += new EventHandler(OnMouseClick);
            this.MouseEnterBounds += new EventHandler(onEnter);
            this.MouseLeaveBounds += new EventHandler(onExit);
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
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
                    thumbnailWidget.thumbnailImage = new ImageBuffer(thumbnailWidget.noThumbnailImage);
                    thumbnailWidget.Invalidate();
                    return;
                }

                if (thumbnailWidget.PrintItem.FileLocation == QueueData.SdCardFileName)
                {
                    switch (thumbnailWidget.Size)
                    {
                        case ImageSizes.Size115x115:
                            {
                                ImageIO.LoadImageData(this.GetImageLocation("icon_sd_card_115x115.png"), thumbnailWidget.thumbnailImage);
                            }
                            break;

                        case ImageSizes.Size50x50:
                            {
                                ImageIO.LoadImageData(this.GetImageLocation("icon_sd_card_50x50.png"), thumbnailWidget.thumbnailImage);
                            }
                            break;

                        default:
                            throw new NotImplementedException();
                    }
                    thumbnailWidget.thumbnailImage.SetRecieveBlender(new BlenderPreMultBGRA());
                    Graphics2D graphics = thumbnailWidget.thumbnailImage.NewGraphics2D();
                    Ellipse outline = new Ellipse(new Vector2(Width / 2.0, Height / 2.0), Width/2 + Width/12);
                    graphics.Render(new Stroke(outline, 4), RGBA_Bytes.White);

                    UiThread.RunOnIdle(thumbnailWidget.EnsureImageUpdated);
                    return;
                }
                
                string stlHashCode = thumbnailWidget.PrintItem.StlFileHashCode.ToString();

                Point2D bigRenderSize = new Point2D(460, 460);
                ImageBuffer bigRender = LoadImageFromDisk(thumbnailWidget, stlHashCode, bigRenderSize);
                if (bigRender == null)
                {
                    List<MeshGroup> loadedMeshGroups = MeshFileIo.Load(thumbnailWidget.PrintItem.FileLocation);

                    thumbnailWidget.thumbnailImage = new ImageBuffer(thumbnailWidget.buildingThumbnailImage);
                    thumbnailWidget.thumbnailImage.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255, 0));
                    bigRender = BuildImageFromMeshGroups(loadedMeshGroups, stlHashCode, bigRenderSize);
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

                            thumbnailWidget.thumbnailImage = new ImageBuffer((int)Width, (int)Height, 32, new BlenderBGRA());
                            thumbnailWidget.thumbnailImage.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255, 0));
                            thumbnailWidget.thumbnailImage.NewGraphics2D().Render(halfWay2, 0, 0, 0, (double)thumbnailWidget.thumbnailImage.Width / halfWay2.Width, (double)thumbnailWidget.thumbnailImage.Height / halfWay2.Height);
                        }
                        break;

                    case ImageSizes.Size115x115:
                        {
                            ImageBuffer halfWay1 = new ImageBuffer(230, 230, 32, new BlenderBGRA());
                            halfWay1.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255, 0));
                            halfWay1.NewGraphics2D().Render(bigRender, 0, 0, 0, (double)halfWay1.Width / bigRender.Width, (double)halfWay1.Height / bigRender.Height);

                            thumbnailWidget.thumbnailImage = new ImageBuffer((int)Width, (int)Height, 32, new BlenderBGRA());
                            thumbnailWidget.thumbnailImage.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255, 0));
                            thumbnailWidget.thumbnailImage.NewGraphics2D().Render(halfWay1, 0, 0, 0, (double)thumbnailWidget.thumbnailImage.Width / halfWay1.Width, (double)thumbnailWidget.thumbnailImage.Height / halfWay1.Height);
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
            thumbnailImage.MarkImageChanged();
            Invalidate();
        }

        public static void CleanUpCacheData()
        {
            //string pngFileName = GetFilenameForSize(stlHashCode, ref size);
            // delete everything that is a tga (we now save pngs).
        }

        private static ImageBuffer LoadImageFromDisk(PartThumbnailWidget thumbnailWidget, string stlHashCode, Point2D size)
        {
            ImageBuffer tempImage = new ImageBuffer(size.x, size.y, 32, new BlenderBGRA());
            string pngFileName = GetFilenameForSize(stlHashCode, ref size);

            if (File.Exists(pngFileName))
            {
                if (ImageIO.LoadImageData(pngFileName, tempImage))
                {
                    return tempImage;
                }
            }

            return null;
        }

        private static string GetFilenameForSize(string stlHashCode, ref Point2D size)
        {
            string folderToSaveThumbnailsTo = ThumbnailPath();
            string pngFileName = Path.Combine(folderToSaveThumbnailsTo, "{0}_{1}x{2}.png".FormatWith(stlHashCode, size.x, size.y));
            return pngFileName;
        }

        private static string ThumbnailPath()
        {
            string applicationUserDataPath = ApplicationDataStorage.Instance.ApplicationUserDataPath;
            string folderToSaveThumbnailsTo = Path.Combine(applicationUserDataPath, "data", "temp", "thumbnails");
            return folderToSaveThumbnailsTo;
        }

        private static ImageBuffer BuildImageFromMeshGroups(List<MeshGroup> loadedMeshGroups, string stlHashCode, Point2D size)
        {
            if (loadedMeshGroups != null 
                && loadedMeshGroups.Count > 0 
                && loadedMeshGroups[0].Meshes != null
                && loadedMeshGroups[0].Meshes[0] != null)
            {
                ImageBuffer tempImage = new ImageBuffer(size.x, size.y, 32, new BlenderBGRA());
                Graphics2D partGraphics2D = tempImage.NewGraphics2D();
                partGraphics2D.Clear(new RGBA_Bytes());

                AxisAlignedBoundingBox aabb = loadedMeshGroups[0].GetAxisAlignedBoundingBox();
                for (int meshGroupIndex = 1; meshGroupIndex < loadedMeshGroups.Count; meshGroupIndex++)
                {
                    aabb = AxisAlignedBoundingBox.Union(aabb, loadedMeshGroups[meshGroupIndex].GetAxisAlignedBoundingBox());
                }
                double maxSize = Math.Max(aabb.XSize, aabb.YSize);
                double scale = size.x / (maxSize * 1.2);
                RectangleDouble bounds2D = new RectangleDouble(aabb.minXYZ.x, aabb.minXYZ.y, aabb.maxXYZ.x, aabb.maxXYZ.y);
                foreach (MeshGroup meshGroup in loadedMeshGroups)
                {
                    foreach (Mesh loadedMesh in meshGroup.Meshes)
                    {
                        PolygonMesh.Rendering.OrthographicZProjection.DrawTo(partGraphics2D, loadedMesh,
                            new Vector2((size.x / scale - bounds2D.Width) / 2 - bounds2D.Left,
                                (size.y / scale - bounds2D.Height) / 2 - bounds2D.Bottom),
                            scale, RGBA_Bytes.White);
                    }
                }

                if (File.Exists("RunUnitTests.txt"))
                {
                    foreach (Mesh loadedMesh in loadedMeshGroups[0].Meshes)
                    {
                        List<MeshEdge> nonManifoldEdges = loadedMesh.GetNonManifoldEdges();
                        if (nonManifoldEdges.Count > 0)
                        {
                            partGraphics2D.Circle(size.x / 4, size.x / 4, size.x / 8, RGBA_Bytes.Red);
                        }
                    }
                }

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

        public void ThemeChanged(object sender, EventArgs e)
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

        private void OnMouseClick(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(DoOnMouseClick);
        }

        private void DoOnMouseClick(object state)
        {
            if (printItem != null)
            {
                string pathAndFile = printItem.FileLocation;
				if (File.Exists(pathAndFile))
                {
                    bool shiftKeyDown = Keyboard.IsKeyDown(Keys.ShiftKey);
                    if (shiftKeyDown)
                    {
                        OpenPartPreviewWindow (View3DWidget.AutoRotate.Disabled);
                    }
                    else
                    {
                        OpenPartPreviewWindow (View3DWidget.AutoRotate.Enabled);
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
            this.partPreviewWindow = null;
		}

		private void OpenPartPreviewWindow(View3DWidget.AutoRotate autoRotate)
		{
            if (partPreviewWindow == null)
			{
                partPreviewWindow = new PartPreviewMainWindow(this.PrintItem, autoRotate);
				partPreviewWindow.Closed += new EventHandler (PartPreviewWindow_Closed);
			}
			else
			{
                partPreviewWindow.BringToFront ();
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
            graphics2D.Render(thumbnailImage, Width / 2 - thumbnailImage.Width / 2, Height / 2 - thumbnailImage.Height / 2);
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
