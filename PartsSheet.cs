﻿/*
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
using System.Threading;
using System.IO;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.Font;

using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.PolygonMesh.Processors;

using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace MatterHackers.MatterControl
{
    public class PartsSheet
    {
        internal class PartImage
        {
            internal double xOffset = 0;
            internal bool wasDrawn = false;
            internal ImageBuffer image;

            public PartImage(ImageBuffer imageOfPart)
            {
                // TODO: Complete member initialization
                this.image = imageOfPart;
            }
        }

        string pathAndFileToSaveTo;

        public event EventHandler DoneSaving;
        public event EventHandler UpdateRemainingItems;

        List<string> stlFilesToPrint;
        List<PartImage> partImagesToPrint = new List<PartImage>();
        const double inchesPerMm = 0.0393701;

        int countThatHaveBeenSaved;
        public int CountThatHaveBeenSaved
        {
            get
            {
                return countThatHaveBeenSaved;
            }
        }

        public int CountOfParts
        {
            get
            {
                return stlFilesToPrint.Count;
            }
        }

        Vector2 sheetSizeMM;
        public Vector2 SheetSizeMM
        {
            get { return sheetSizeMM; }
            set { sheetSizeMM = value; }
        }

        public Vector2 SheetSizeInches
        {
            get { return SheetSizeMM * inchesPerMm; }
            set { SheetSizeMM = value / inchesPerMm; }
        }

        public double PixelPerMM
        {
            get { return inchesPerMm * SheetDpi; }
        }

        public BorderDouble PageMarginMM
        {
            get { return new BorderDouble(10, 5); }
        }

        public BorderDouble PageMarginPixels
        {
            get { return PageMarginMM * PixelPerMM; }
        }

        public double PartMarginMM
        {
            get { return 2; }
        }

        public double PartMarginPixels
        {
            get { return PartMarginMM * PixelPerMM; }
        }

        public double PartPaddingMM
        {
            get { return 2; }
        }

        public double PartPaddingPixels
        {
            get { return PartPaddingMM * PixelPerMM; }
        }

        public int SheetDpi { get; set; }

        public PartsSheet(List<PrintItem> dataSource, string pathAndFileToSaveTo)
        {
            this.pathAndFileToSaveTo = pathAndFileToSaveTo;
            SheetDpi = 300;
            SheetSizeInches = new Vector2(8.5, 11);
            // make sure we have our own list so it can't get stepped on while we output it.
            stlFilesToPrint = new List<string>();
            foreach(PrintItem stlToCopy in dataSource)
            {
                stlFilesToPrint.Add(stlToCopy.FileLocation);
            }
        }

        void OnDoneSaving()
        {
            if (DoneSaving != null)
            {
                DoneSaving(this, new StringEventArgs(Path.GetFileName("Saving to PDF")));
            }
        }

        public void SaveSheets()
        {
#if false
            SavingFunction();
#else
            Thread saveThread = new Thread(SavingFunction);
            saveThread.Name = "Save Part Sheet";
            saveThread.IsBackground = true;
            saveThread.Start();
#endif
        }

        static bool currentlySaving = false;
        public void SavingFunction()
        {
            currentlySaving = true;
            countThatHaveBeenSaved = 0;
            // first create images for all the parts
            foreach (string stlFileName in stlFilesToPrint)
            {
                Mesh loadedMesh = StlProcessing.Load(stlFileName);
                if (loadedMesh != null)
                {
                    AxisAlignedBoundingBox aabb = loadedMesh.GetAxisAlignedBoundingBox();
                    RectangleDouble bounds2D = new RectangleDouble(aabb.minXYZ.x, aabb.minXYZ.y, aabb.maxXYZ.x, aabb.maxXYZ.y);
                    double widthInMM = bounds2D.Width + PartMarginMM * 2;
                    double textSpaceMM = 5;
                    double heightMM = textSpaceMM + bounds2D.Height + PartMarginMM * 2;

                    string partName = System.IO.Path.GetFileName(System.IO.Path.GetFileName(stlFileName));
                    TypeFacePrinter typeFacePrinter = new TypeFacePrinter(partName, 28, Vector2.Zero, Justification.Center, Baseline.BoundsCenter);
                    double sizeOfNameX = typeFacePrinter.GetSize().x + PartMarginPixels * 2;
                    Vector2 sizeOfRender = new Vector2(widthInMM * PixelPerMM, heightMM * PixelPerMM);

                    ImageBuffer imageOfPart = new ImageBuffer((int)(Math.Max(sizeOfNameX, sizeOfRender.x)), (int)(sizeOfRender.y), 32, new BlenderBGRA());
                    typeFacePrinter.Origin = new Vector2(imageOfPart.Width / 2, (textSpaceMM / 2) * PixelPerMM);

                    Graphics2D partGraphics2D = imageOfPart.NewGraphics2D();

                    RectangleDouble rectBounds = new RectangleDouble(0, 0, imageOfPart.Width, imageOfPart.Height);
                    double strokeWidth = .5 * PixelPerMM;
                    rectBounds.Inflate(-strokeWidth / 2);
                    RoundedRect rect = new RoundedRect(rectBounds, PartMarginMM * PixelPerMM);
                    partGraphics2D.Render(rect, RGBA_Bytes.LightGray);
                    Stroke rectOutline = new Stroke(rect, strokeWidth);
                    partGraphics2D.Render(rectOutline, RGBA_Bytes.DarkGray);

                    PolygonMesh.Rendering.OrthographicZProjection.DrawTo(partGraphics2D, loadedMesh, new Vector2(-bounds2D.Left + PartMarginMM, -bounds2D.Bottom + textSpaceMM + PartMarginMM), PixelPerMM, RGBA_Bytes.Black);
                    partGraphics2D.Render(typeFacePrinter, RGBA_Bytes.Black);

                    partImagesToPrint.Add(new PartImage(imageOfPart));
                }
                countThatHaveBeenSaved++;
                if (UpdateRemainingItems != null)
                {
                    UpdateRemainingItems(this, new StringEventArgs(Path.GetFileName(stlFileName)));
                }
            }

            partImagesToPrint.Sort(BiggestToLittlestImages);

            PdfDocument document = new PdfDocument();
            document.Info.Title = "MatterHackers Parts Sheet";
            document.Info.Author = "MatterHackers Inc.";
            document.Info.Subject = "This is a list of the parts that are in a queue from MatterControl.";
            document.Info.Keywords = "MatterControl, STL, 3D Printing";

            int nextPartToPrintIndex = 0;
            int plateNumber = 1;
            bool done = false;
            while (!done && nextPartToPrintIndex < partImagesToPrint.Count)
            {
                PdfPage pdfPage = document.AddPage();
                CreateOnePage(plateNumber++, ref nextPartToPrintIndex, pdfPage);
            }
			try
			{
            	document.Save(pathAndFileToSaveTo);
            	Process.Start(pathAndFileToSaveTo);
			}
			catch {

			}

            OnDoneSaving();
            currentlySaving = false;
        }

        private static int BiggestToLittlestImages(PartImage one, PartImage two)
        {
            return two.image.Height.CompareTo(one.image.Height);
        }

        void CreateOnePage(int plateNumber, ref int nextPartToPrintIndex, PdfPage pdfPage)
        {
            ImageBuffer plateInventoryImage = new ImageBuffer((int)(300 * 8.5), 300 * 11, 32, new BlenderBGRA());
            Graphics2D plateGraphics = plateInventoryImage.NewGraphics2D();
            double currentlyPrintingHeightPixels = PrintTopOfPage(plateInventoryImage, plateGraphics);

            Vector2 offset = new Vector2(PageMarginPixels.Left, currentlyPrintingHeightPixels);
            double tallestHeight = 0;
            List<PartImage> partsOnLine = new List<PartImage>();
            while (nextPartToPrintIndex < partImagesToPrint.Count)
            {
                ImageBuffer image = partImagesToPrint[nextPartToPrintIndex].image;
                tallestHeight = Math.Max(tallestHeight, image.Height);

                if (partsOnLine.Count > 0 && offset.x + image.Width > plateInventoryImage.Width - PageMarginPixels.Right)
                {
                    if (partsOnLine.Count == 1)
                    {
                        plateGraphics.Render(partsOnLine[0].image, plateInventoryImage.Width / 2 - partsOnLine[0].image.Width / 2, offset.y - tallestHeight);
                    }
                    else
                    {
                        foreach (PartImage partToDraw in partsOnLine)
                        {
                            plateGraphics.Render(partToDraw.image, partToDraw.xOffset, offset.y - tallestHeight);
                        }
                    }

                    offset.x = PageMarginPixels.Left;
                    offset.y -= (tallestHeight + PartPaddingPixels * 2);
                    tallestHeight = 0;
                    partsOnLine.Clear();
                    if (offset.y - image.Height < PageMarginPixels.Bottom)
                    {
                        break;
                    }
                }
                else
                {
                    partImagesToPrint[nextPartToPrintIndex].xOffset = offset.x;
                    partsOnLine.Add(partImagesToPrint[nextPartToPrintIndex]);
                    //plateGraphics.Render(image, offset.x, offset.y - image.Height);
                    offset.x += image.Width + PartPaddingPixels * 2;
                    nextPartToPrintIndex++;
                }
            }

            // print the last line of parts
            foreach (PartImage partToDraw in partsOnLine)
            {
                plateGraphics.Render(partToDraw.image, partToDraw.xOffset, offset.y - tallestHeight);
            }

            TypeFacePrinter printer = new TypeFacePrinter(string.Format("{0}", Path.GetFileNameWithoutExtension(pathAndFileToSaveTo)), 32, justification: Justification.Center);
            printer.Origin = new Vector2(plateGraphics.DestImage.Width/2, 110);
            plateGraphics.Render(printer, RGBA_Bytes.Black);

            printer = new TypeFacePrinter(string.Format("Page {0}", plateNumber), 28, justification: Justification.Center);
            printer.Origin = new Vector2(plateGraphics.DestImage.Width / 2, 60);
            plateGraphics.Render(printer, RGBA_Bytes.Black);

            string applicationUserDataPath = ApplicationDataStorage.Instance.ApplicationUserDataPath;
            string folderToSavePrintsTo = Path.Combine(applicationUserDataPath, "data", "temp", "plateImages");
            string jpegFileName = System.IO.Path.Combine(folderToSavePrintsTo, plateNumber.ToString() + ".jpeg");

            if (!Directory.Exists(folderToSavePrintsTo))
            {
                Directory.CreateDirectory(folderToSavePrintsTo);
            }
            ImageBMPIO.SaveImageData(jpegFileName, plateInventoryImage);

            XGraphics gfx = XGraphics.FromPdfPage(pdfPage);
            XImage jpegImage = XImage.FromFile(jpegFileName);
            //double width = jpegImage.PixelWidth * 72 / jpegImage.HorizontalResolution;
            //double height = jpegImage.PixelHeight * 72 / jpegImage. .HorizontalResolution;

            gfx.DrawImage(jpegImage, 0, 0, pdfPage.Width, pdfPage.Height);
        }

        private double PrintTopOfPage(ImageBuffer plateInventoryImage, Graphics2D plateGraphics)
        {
            plateGraphics.Clear(RGBA_Bytes.White);

            double currentlyPrintingHeightPixels = plateInventoryImage.Height - PageMarginMM.Top * PixelPerMM;

            string logoPathAndFile = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "PartSheetLogo.png");
            if(File.Exists(logoPathAndFile))
            {
                ImageBuffer logoImage = new ImageBuffer();
                ImageBMPIO.LoadImageData(logoPathAndFile, logoImage);
                currentlyPrintingHeightPixels -= logoImage.Height;
                plateGraphics.Render(logoImage, (plateInventoryImage.Width - logoImage.Width) / 2, currentlyPrintingHeightPixels);
            }

            currentlyPrintingHeightPixels -= PartPaddingPixels;

            double underlineHeightMM = 1;
            RectangleDouble lineBounds = new RectangleDouble(0, 0, plateInventoryImage.Width - PageMarginPixels.Left * 2, underlineHeightMM * PixelPerMM);
            lineBounds.Offset(PageMarginPixels.Left, currentlyPrintingHeightPixels - lineBounds.Height);
            plateGraphics.FillRectangle(lineBounds, RGBA_Bytes.Black);

            return currentlyPrintingHeightPixels - (lineBounds.Height + PartPaddingPixels);
        }

        public static bool IsSaving()
        {
            return currentlySaving;
        }
    }
}
