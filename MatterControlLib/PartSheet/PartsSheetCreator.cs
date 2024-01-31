﻿/*
Copyright (c) 2023, Lars Brubaker, Kevin Pope, John Lewin
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

using Matter_CAD_Lib.DesignTools.Objects3D;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.Library;
using MatterHackers.VectorMath;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl
{
    public class PartsSheet
    {
        private const double inchesPerMm = 0.0393701;

        private static bool currentlySaving = false;

        private List<ILibraryAssetStream> itemSource;

        private List<PartImage> partImagesToPrint = new List<PartImage>();

        private string pathAndFileToSaveTo;
        private bool openAfterSave;

        public PartsSheet(IEnumerable<ILibraryAssetStream> itemSource, string pathAndFileToSaveTo, bool openAfterSave = true)
        {
            this.pathAndFileToSaveTo = pathAndFileToSaveTo;
            this.openAfterSave = openAfterSave;
            SheetDpi = 300;
            SheetSizeInches = new Vector2(8.5, 11);

            this.itemSource = itemSource.ToList();
        }

        public BorderDouble PageMarginMM { get; } = new BorderDouble(10, 25, 10, 5);

        public BorderDouble PageMarginPixels => PageMarginMM * PixelsPerMM;

        public double PartMarginMM { get; } = 2;

        public double PartMarginPixels => PartMarginMM * PixelsPerMM;

        public double PartPaddingMM { get; } = 2;

        public double PartPaddingPixels => PartPaddingMM * PixelsPerMM;

        public double PixelsPerMM => inchesPerMm * SheetDpi;

        public int SheetDpi { get; set; }

        public Vector2 SheetSizeInches
        {
            get { return SheetSizeMM * inchesPerMm; }
            set { SheetSizeMM = value / inchesPerMm; }
        }

        public Vector2 SheetSizeMM { get; set; }

        public Vector2 SheetSizePixels => SheetSizeMM * PixelsPerMM;

        public static bool IsSaving()
        {
            return currentlySaving;
        }

        private async Task ExportTask(Action<double, string> reporter, CancellationTokenSource cancellationToken)
        {
            var processCount = 0.0;
            currentlySaving = true;
            // first create images for all the parts
            foreach (var item in itemSource)
            {
                reporter?.Invoke(0, item.Name);

                var xxx = itemSource.Count();
                var yyy = itemSource.FirstOrDefault()?.Name;

                var object3D = await item.CreateContent();

                if (object3D == null)
                {
                    continue;
                }

                var loadedMeshGroups = object3D.VisibleMeshes().ToList();
                if (loadedMeshGroups?.Count > 0)
                {
                    AxisAlignedBoundingBox aabb = loadedMeshGroups[0].Mesh.GetAxisAlignedBoundingBox(loadedMeshGroups[0].WorldMatrix());

                    for (int i = 1; i < loadedMeshGroups.Count; i++)
                    {
                        aabb = AxisAlignedBoundingBox.Union(aabb, loadedMeshGroups[i].Mesh.GetAxisAlignedBoundingBox(loadedMeshGroups[i].WorldMatrix()));
                    }

                    RectangleDouble bounds2D = new RectangleDouble(aabb.MinXYZ.X, aabb.MinXYZ.Y, aabb.MaxXYZ.X, aabb.MaxXYZ.Y);
                    double widthInMM = bounds2D.Width + PartMarginMM * 2;
                    double textSpaceMM = 5;
                    double heightMM = textSpaceMM + bounds2D.Height + PartMarginMM * 2;

                    TypeFacePrinter typeFacePrinter = new TypeFacePrinter(item.Name, 28, Vector2.Zero, Justification.Center, Baseline.BoundsCenter);
                    double sizeOfNameX = typeFacePrinter.GetSize().X + PartMarginPixels * 2;
                    Vector2 sizeOfRender = new Vector2(widthInMM * PixelsPerMM, heightMM * PixelsPerMM);

                    ImageBuffer imageOfPart = new ImageBuffer((int)(Math.Max(sizeOfNameX, sizeOfRender.X)), (int)(sizeOfRender.Y));
                    typeFacePrinter.Origin = new Vector2(imageOfPart.Width / 2, (textSpaceMM / 2) * PixelsPerMM);

                    Graphics2D partGraphics2D = imageOfPart.NewGraphics2D();

                    RectangleDouble rectBounds = new RectangleDouble(0, 0, imageOfPart.Width, imageOfPart.Height);
                    double strokeWidth = .5 * PixelsPerMM;
                    rectBounds.Inflate(-strokeWidth / 2);
                    RoundedRect rect = new RoundedRect(rectBounds, PartMarginMM * PixelsPerMM);
                    partGraphics2D.Render(rect, Color.LightGray);
                    Stroke rectOutline = new Stroke(rect, strokeWidth);
                    partGraphics2D.Render(rectOutline, Color.DarkGray);

                    foreach (var meshGroup in loadedMeshGroups)
                    {
                        PolygonMesh.Rendering.OrthographicZProjection.DrawTo(partGraphics2D, meshGroup.Mesh, meshGroup.WorldMatrix(), new Vector2(-bounds2D.Left + PartMarginMM, -bounds2D.Bottom + textSpaceMM + PartMarginMM), PixelsPerMM, Color.Black);
                    }
                    partGraphics2D.Render(typeFacePrinter, Color.Black);

                    partImagesToPrint.Add(new PartImage(imageOfPart));
                }

                reporter?.Invoke(Math.Min(processCount / itemSource.Count, .95), null);
                processCount++;
            }

            reporter?.Invoke(0, "Saving".Localize());

            partImagesToPrint.Sort(BiggestToLittlestImages);

            PdfDocument document = new PdfDocument();
            document.Info.Title = "MatterHackers Parts Sheet";
            document.Info.Author = "MatterHackers Inc.";
            document.Info.Subject = "This is a list of the parts that are in a queue from MatterControl.";
            document.Info.Keywords = "MatterControl, STL, 3D Printing";

            int nextPartToPrintIndex = 0;
            int plateNumber = 1;

            while (nextPartToPrintIndex < partImagesToPrint.Count)
            {
                PdfPage pdfPage = document.AddPage();
                CreateOnePage(plateNumber++, ref nextPartToPrintIndex, pdfPage);
            }

            try
            {
                // save the final document
                document.Save(pathAndFileToSaveTo);

                if (openAfterSave)
                {
                    // Now try and open the document. This will launch whatever PDF viewer is on the system and ask it
                    // to show the file (at least on Windows).
                    ApplicationController.ProcessStart(pathAndFileToSaveTo);
                }
            }
            catch (Exception)
            {
            }

            currentlySaving = false;

            reporter?.Invoke(1, null);
        }
        
        public async Task SaveSheets(Action<double, string> reporter = null)
        {
            if (reporter == null)
            {
                await ApplicationController.Instance.Tasks.Execute("Export Part Sheet".Localize(), null, ExportTask);
            }
            else
            {
                await ExportTask(reporter, new CancellationTokenSource());
            }
        }

        private static int BiggestToLittlestImages(PartImage one, PartImage two)
        {
            return two.image.Height.CompareTo(one.image.Height);
        }

        private void CreateOnePage(int plateNumber, ref int nextPartToPrintIndex, PdfPage pdfPage)
        {
            ImageBuffer plateInventoryImage = new ImageBuffer((int)(SheetSizePixels.X), (int)(SheetSizePixels.Y));
            Graphics2D plateGraphics = plateInventoryImage.NewGraphics2D();
            double currentlyPrintingHeightPixels = PrintTopOfPage(plateInventoryImage, plateGraphics);

            Vector2 offset = new Vector2(PageMarginPixels.Left, currentlyPrintingHeightPixels);
            double tallestHeight = 0;
            List<PartImage> partsOnLine = new List<PartImage>();
            while (nextPartToPrintIndex < partImagesToPrint.Count)
            {
                ImageBuffer image = partImagesToPrint[nextPartToPrintIndex].image;
                tallestHeight = Math.Max(tallestHeight, image.Height);

                if (partsOnLine.Count > 0 && offset.X + image.Width > plateInventoryImage.Width - PageMarginPixels.Right)
                {
                    if (partsOnLine.Count == 1)
                    {
                        plateGraphics.Render(partsOnLine[0].image, plateInventoryImage.Width / 2 - partsOnLine[0].image.Width / 2, offset.Y - tallestHeight);
                    }
                    else
                    {
                        foreach (PartImage partToDraw in partsOnLine)
                        {
                            plateGraphics.Render(partToDraw.image, partToDraw.xOffset, offset.Y - tallestHeight);
                        }
                    }

                    offset.X = PageMarginPixels.Left;
                    offset.Y -= (tallestHeight + PartPaddingPixels * 2);
                    tallestHeight = 0;
                    partsOnLine.Clear();
                    if (offset.Y - image.Height < PageMarginPixels.Bottom)
                    {
                        break;
                    }
                }
                else
                {
                    partImagesToPrint[nextPartToPrintIndex].xOffset = offset.X;
                    partsOnLine.Add(partImagesToPrint[nextPartToPrintIndex]);
                    //plateGraphics.Render(image, offset.x, offset.y - image.Height);
                    offset.X += image.Width + PartPaddingPixels * 2;
                    nextPartToPrintIndex++;
                }
            }

            // print the last line of parts
            foreach (PartImage partToDraw in partsOnLine)
            {
                plateGraphics.Render(partToDraw.image, partToDraw.xOffset, offset.Y - tallestHeight);
            }

            TypeFacePrinter printer = new TypeFacePrinter(string.Format("{0}", Path.GetFileNameWithoutExtension(pathAndFileToSaveTo)), 32, justification: Justification.Center);
            printer.Origin = new Vector2(plateGraphics.DestImage.Width / 2, 110);
            plateGraphics.Render(printer, Color.Black);

            printer = new TypeFacePrinter(string.Format("Page {0}", plateNumber), 28, justification: Justification.Center);
            printer.Origin = new Vector2(plateGraphics.DestImage.Width / 2, 60);
            plateGraphics.Render(printer, Color.Black);

            MemoryStream jpegStream = new MemoryStream();
            ImageIO.SaveImageData(jpegStream, ".jpeg", plateInventoryImage);

            XGraphics gfx = XGraphics.FromPdfPage(pdfPage);
            jpegStream.Seek(0, SeekOrigin.Begin);
            XImage jpegImage = XImage.FromStream(jpegStream);
            //double width = jpegImage.PixelWidth * 72 / jpegImage.HorizontalResolution;
            //double height = jpegImage.PixelHeight * 72 / jpegImage. .HorizontalResolution;

            gfx.DrawImage(jpegImage, 0, 0, pdfPage.Width, pdfPage.Height);
        }

        private double PrintTopOfPage(ImageBuffer plateInventoryImage, Graphics2D plateGraphics)
        {
            plateGraphics.Clear(Color.White);

            double currentlyPrintingHeightPixels = plateInventoryImage.Height - PageMarginPixels.Top;

            string logoPathAndFile = Path.Combine("Images", "PartSheetLogo.png");
            if (StaticData.Instance.FileExists(logoPathAndFile))
            {
                ImageBuffer logoImage = StaticData.Instance.LoadImage(logoPathAndFile);
                currentlyPrintingHeightPixels -= logoImage.Height;
                plateGraphics.Render(logoImage, (plateInventoryImage.Width - logoImage.Width) / 2, currentlyPrintingHeightPixels);
            }

            currentlyPrintingHeightPixels -= PartPaddingPixels;

            double underlineHeightMM = 1;

            var lineBounds = new RectangleDouble(0, 0, plateInventoryImage.Width - PageMarginPixels.Left * 2, underlineHeightMM * PixelsPerMM);
            lineBounds.Offset(PageMarginPixels.Left, currentlyPrintingHeightPixels - lineBounds.Height);
            plateGraphics.FillRectangle(lineBounds, Color.Black);

            return currentlyPrintingHeightPixels - (lineBounds.Height + PartPaddingPixels);
        }

        public class FileNameAndPresentationName
        {
            public string fileName;
            public string presentationName;

            public FileNameAndPresentationName(string fileName, string presentationName)
            {
                this.fileName = fileName;
                this.presentationName = presentationName;
            }
        }

        internal class PartImage
        {
            internal ImageBuffer image;
            internal bool wasDrawn = false;
            internal double xOffset = 0;

            public PartImage(ImageBuffer imageOfPart)
            {
                this.image = imageOfPart;
            }
        }
    }
}