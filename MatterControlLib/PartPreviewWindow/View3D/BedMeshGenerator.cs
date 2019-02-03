﻿/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public static class BedMeshGenerator
	{
		private static ImageBuffer watermarkImage = null;

		private static Color bedBaseColor = new Color(245, 245, 255);
		private static Color bedMarkingsColor = Color.Black;

		public static ImageBuffer CreatePrintBedImage(PrinterConfig printer)
		{
			ImageBuffer bedImage;

			switch (printer.Bed.BedShape)
			{
				case BedShape.Rectangular:
					bedImage = CreateRectangularBedGridImage(printer);
					break;

				case BedShape.Circular:
					bedImage = CreateCircularBedGridImage(printer);
					break;

				default:
					throw new NotImplementedException();
			}

			return bedImage;
		}

		public static (Mesh bed, Mesh volume) CreatePrintBedAndVolume(PrinterConfig printer)
		{
			Mesh printerBed = null;
			Mesh buildVolume = null;

			Vector3 displayVolumeToBuild = Vector3.ComponentMax(printer.Bed.ViewerVolume, new Vector3(1, 1, 1));

			ImageBuffer bedplateImage = CreatePrintBedImage(printer);

			switch (printer.Bed.BedShape)
			{
				case BedShape.Rectangular:
					if (displayVolumeToBuild.Z > 0)
					{
						buildVolume = PlatonicSolids.CreateCube(displayVolumeToBuild);
						for(int i=0; i< buildVolume.Vertices.Count; i++)
						{
							buildVolume.Vertices[i] = buildVolume.Vertices[i] + new Vector3Float(0, 0, displayVolumeToBuild.Z / 2);
						}
						var bspTree = FaceBspTree.Create(buildVolume);
						buildVolume.FaceBspTree = bspTree;
					}

					printerBed = PlatonicSolids.CreateCube(displayVolumeToBuild.X, displayVolumeToBuild.Y, 1.8);
					{
						printerBed.PlaceTextureOnFaces(0, bedplateImage);
					}
					break;

				case BedShape.Circular:
					{
						if (displayVolumeToBuild.Z > 0)
						{
							buildVolume = VertexSourceToMesh.Extrude(new Ellipse(new Vector2(), displayVolumeToBuild.X / 2, displayVolumeToBuild.Y / 2), displayVolumeToBuild.Z);
						}

						printerBed = VertexSourceToMesh.Extrude(new Ellipse(new Vector2(), displayVolumeToBuild.X / 2, displayVolumeToBuild.Y / 2), 1.8);
						printerBed.PlaceTextureOnFaces(0, bedplateImage);
					}
					break;

				default:
					throw new NotImplementedException();
			}

			var zTop = printerBed.GetAxisAlignedBoundingBox().MaxXYZ.Z;
			for (int i = 0; i < printerBed.Vertices.Count; i++)
			{
				printerBed.Vertices[i] = printerBed.Vertices[i] - new Vector3Float(-printer.Bed.BedCenter, zTop + .02);
			}

			if (buildVolume != null)
			{
				for (int i = 0; i < buildVolume.Vertices.Count; i++)
				{
					buildVolume.Vertices[i] = buildVolume.Vertices[i] - new Vector3Float(-printer.Bed.BedCenter, zTop + .02);
				}
			}

			return (printerBed, buildVolume);
		}

		private static ImageBuffer CreateCircularBedGridImage(PrinterConfig printer)
		{
			Vector3 displayVolumeToBuild = Vector3.ComponentMax(printer.Bed.ViewerVolume, new Vector3(1, 1, 1));
			double sizeForMarking = Math.Max(displayVolumeToBuild.X, displayVolumeToBuild.Y);
			double cmPerLine = 10;
			int skip = 1;
			if (sizeForMarking > 1000)
			{
				cmPerLine = 100;
				skip = 10;
			}
			else if (sizeForMarking > 300)
			{
				cmPerLine = 50;
				skip = 5;
			}

			var bedplateImage = new ImageBuffer(1024, 1024);
			Graphics2D graphics2D = bedplateImage.NewGraphics2D();
			graphics2D.Clear(bedBaseColor);

			var originPixels = new Vector2();
			{
				double lineSpacePixels = bedplateImage.Width / (displayVolumeToBuild.X / cmPerLine);

				double xPositionLines = (-(printer.Bed.ViewerVolume.X / 2.0) + printer.Bed.BedCenter.X) / cmPerLine;
				int xPositionCmInt = (int)Math.Round(xPositionLines);
				double fraction = xPositionLines - xPositionCmInt;
				for (double linePos = lineSpacePixels * (1 - fraction); linePos < bedplateImage.Width; linePos += lineSpacePixels)
				{
					xPositionCmInt++;
					if (xPositionCmInt == 0)
					{
						originPixels.X = linePos;
					}
				}
			}

			{
				double lineDist = bedplateImage.Height / (displayVolumeToBuild.Y / cmPerLine);

				double yPositionCm = (-(printer.Bed.ViewerVolume.Y / 2.0) + printer.Bed.BedCenter.Y) / cmPerLine;
				int yPositionCmInt = (int)Math.Round(yPositionCm);
				double fraction = yPositionCm - yPositionCmInt;
				for (double linePos = lineDist * (1 - fraction); linePos < bedplateImage.Height; linePos += lineDist)
				{
					yPositionCmInt++;
					int linePosInt = (int)linePos;
					if (yPositionCmInt == 0)
					{
						originPixels.Y = linePos;
					}
				}
			}

			Ellipse bedCircle = new Ellipse(bedplateImage.Width/2, bedplateImage.Height/2, bedplateImage.Width/2, bedplateImage.Height/2);
			graphics2D.Render(bedCircle, bedBaseColor);
			//graphics2D.Clear(bedBaseColor);

			{
				double lineSpacePixels = bedplateImage.Width / (displayVolumeToBuild.X / cmPerLine);

				double xPositionLines = (-(printer.Bed.ViewerVolume.X / 2.0) + printer.Bed.BedCenter.X) / cmPerLine;
				int xPositionCmInt = (int)Math.Round(xPositionLines);
				double fraction = xPositionLines - xPositionCmInt;
				int pointSize = 20;
				graphics2D.DrawString((xPositionCmInt * skip).ToString(), 4, originPixels.Y + 4, pointSize, color: bedMarkingsColor);
				for (double linePos = lineSpacePixels * (1 - fraction); linePos < bedplateImage.Width; linePos += lineSpacePixels)
				{
					xPositionCmInt++;
					int linePosInt = (int)linePos;
					int lineWidth = 1;
					if (xPositionCmInt == 0)
					{
						lineWidth = 2;
						graphics2D.Line(linePosInt, 0, linePosInt, bedplateImage.Height, bedMarkingsColor, lineWidth);
					}
					graphics2D.DrawString((xPositionCmInt * skip).ToString(), linePos + 4, originPixels.Y + 4, pointSize, color: bedMarkingsColor);

					Ellipse circle = new Ellipse(originPixels, linePos - originPixels.X);
					Stroke outline = new Stroke(circle);
					graphics2D.Render(outline, bedMarkingsColor);
				}
			}

			{
				double lineDist = bedplateImage.Height / (displayVolumeToBuild.Y / cmPerLine);

				double yPositionCm = (-(printer.Bed.ViewerVolume.Y / 2.0) + printer.Bed.BedCenter.Y) / cmPerLine;
				int yPositionCmInt = (int)Math.Round(yPositionCm);
				double fraction = yPositionCm - yPositionCmInt;
				int pointSize = 20;
				for (double linePos = lineDist * (1 - fraction); linePos < bedplateImage.Height; linePos += lineDist)
				{
					yPositionCmInt++;
					int linePosInt = (int)linePos;
					int lineWidth = 1;
					if (yPositionCmInt == 0)
					{
						lineWidth = 2;
						originPixels.Y = linePos;
						graphics2D.Line(0, linePosInt, bedplateImage.Height, linePosInt, bedMarkingsColor, lineWidth);
					}

					graphics2D.DrawString((yPositionCmInt * skip).ToString(), originPixels.X + 4, linePos + 4, pointSize, color: bedMarkingsColor);
				}
			}

			ApplyOemBedImage(bedplateImage, printer);

			return bedplateImage;
		}

		private static ImageBuffer CreateRectangularBedGridImage(PrinterConfig printer)
		{
			Vector3 displayVolumeToBuild = Vector3.ComponentMax(printer.Bed.ViewerVolume, new Vector3(1, 1, 1));
			double sizeForMarking = Math.Max(displayVolumeToBuild.X, displayVolumeToBuild.Y);
			double divisor = 10;
			int skip = 1;
			if (sizeForMarking > 1000)
			{
				divisor = 100;
				skip = 10;
			}
			else if (sizeForMarking > 300)
			{
				divisor = 50;
				skip = 5;
			}

			var bedplateImage = new ImageBuffer(1024, 1024);
			Graphics2D graphics2D = bedplateImage.NewGraphics2D();
			graphics2D.Clear(bedBaseColor);

			{
				double lineDist = bedplateImage.Width / (displayVolumeToBuild.X / divisor);

				double xPositionCm = (-(printer.Bed.ViewerVolume.X / 2.0) + printer.Bed.BedCenter.X) / divisor;
				int xPositionCmInt = (int)Math.Round(xPositionCm);
				double fraction = xPositionCm - xPositionCmInt;
				int pointSize = 20;
				graphics2D.DrawString((xPositionCmInt * skip).ToString(), 4, 4, pointSize, color: bedMarkingsColor);
				for (double linePos = lineDist * (1 - fraction); linePos < bedplateImage.Width; linePos += lineDist)
				{
					xPositionCmInt++;
					int linePosInt = (int)linePos;
					int lineWidth = 1;
					if (xPositionCmInt == 0)
					{
						lineWidth = 2;
					}
					graphics2D.Line(linePosInt, 0, linePosInt, bedplateImage.Height, bedMarkingsColor, lineWidth);
					graphics2D.DrawString((xPositionCmInt * skip).ToString(), linePos + 4, 4, pointSize, color: bedMarkingsColor);
				}
			}

			{
				double lineDist = bedplateImage.Height / (displayVolumeToBuild.Y / divisor);

				double yPositionCm = (-(printer.Bed.ViewerVolume.Y / 2.0) + printer.Bed.BedCenter.Y) / divisor;
				int yPositionCmInt = (int)Math.Round(yPositionCm);
				double fraction = yPositionCm - yPositionCmInt;
				int pointSize = 20;
				for (double linePos = lineDist * (1 - fraction); linePos < bedplateImage.Height; linePos += lineDist)
				{
					yPositionCmInt++;
					int linePosInt = (int)linePos;
					int lineWidth = 1;
					if (yPositionCmInt == 0)
					{
						lineWidth = 2;
					}
					graphics2D.Line(0, linePosInt, bedplateImage.Height, linePosInt, bedMarkingsColor, lineWidth);

					graphics2D.DrawString((yPositionCmInt * skip).ToString(), 4, linePos + 4, pointSize, color: bedMarkingsColor);
				}
			}

			ApplyOemBedImage(bedplateImage, printer);

			return bedplateImage;
		}

		private static void ApplyOemBedImage(ImageBuffer bedImage, PrinterConfig printer)
		{
			// Add an oem/watermark image to the bedplate grid
			string imagePathAndFile = Path.Combine("OEMSettings", "bedimage.png");
			if (AggContext.StaticData.FileExists(imagePathAndFile))
			{
				if (watermarkImage == null)
				{
					watermarkImage = AggContext.StaticData.LoadImage(imagePathAndFile);
				}

				var xYRatio = printer.Bed.ViewerVolume.X / (double)printer.Bed.ViewerVolume.Y;
				var scaledWidth = watermarkImage.Width;
				var scaledHeight = watermarkImage.Height * xYRatio;
				Graphics2D bedGraphics = bedImage.NewGraphics2D();
				bedGraphics.Render(
					watermarkImage,
					new Vector2(
						(bedImage.Width - scaledWidth) / 2,
						(bedImage.Height - scaledHeight) / 2),
					scaledWidth, scaledHeight);
			}
		}
	}
}