/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class BedMeshGenerator
	{
		private static ImageBuffer watermarkImage = null;

		private Color bedBaseColor = new Color(245, 245, 255);
		private Color bedMarkingsColor = Color.Black;

		public (Mesh bed, Mesh volume) CreatePrintBedAndVolume(PrinterConfig printer)
		{
			Mesh printerBed = null;
			Mesh buildVolume = null;

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

			ImageBuffer bedplateImage;

			switch (printer.Bed.BedShape)
			{
				case BedShape.Rectangular:
					if (displayVolumeToBuild.Z > 0)
					{
						buildVolume = PlatonicSolids.CreateCube(displayVolumeToBuild);
						foreach (Vertex vertex in buildVolume.Vertices)
						{
							vertex.Position = vertex.Position + new Vector3(0, 0, displayVolumeToBuild.Z / 2);
						}
					}

					bedplateImage = CreateRectangularBedGridImage(printer, displayVolumeToBuild, divisor, skip);

					ApplyOemBedImage(bedplateImage);

					printerBed = PlatonicSolids.CreateCube(displayVolumeToBuild.X, displayVolumeToBuild.Y, 1.8);
					{
						Face face = printerBed.Faces[0];
						MeshHelper.PlaceTextureOnFace(face, bedplateImage);
					}
					break;

				case BedShape.Circular:
					{
						if (displayVolumeToBuild.Z > 0)
						{
							buildVolume = VertexSourceToMesh.Extrude(new Ellipse(new Vector2(), displayVolumeToBuild.X / 2, displayVolumeToBuild.Y / 2), displayVolumeToBuild.Z);
							foreach (Vertex vertex in buildVolume.Vertices)
							{
								vertex.Position = vertex.Position + new Vector3(0, 0, .2);
							}
						}

						bedplateImage = CreateCircularBedGridImage((int)(displayVolumeToBuild.X / divisor), (int)(displayVolumeToBuild.Y / divisor), skip);

						ApplyOemBedImage(bedplateImage);

						printerBed = VertexSourceToMesh.Extrude(new Ellipse(new Vector2(), displayVolumeToBuild.X / 2, displayVolumeToBuild.Y / 2), 1.8);
						{
							foreach (Face face in printerBed.Faces)
							{
								if (face.Normal.Z > 0)
								{
									face.SetTexture(0, bedplateImage);
									foreach (FaceEdge faceEdge in face.FaceEdges())
									{
										faceEdge.SetUv(0, new Vector2((displayVolumeToBuild.X / 2 + faceEdge.FirstVertex.Position.X) / displayVolumeToBuild.X,
											(displayVolumeToBuild.Y / 2 + faceEdge.FirstVertex.Position.Y) / displayVolumeToBuild.Y));
									}
								}
							}
						}
					}
					break;

				default:
					throw new NotImplementedException();
			}

			var zTop = printerBed.GetAxisAlignedBoundingBox().maxXYZ.Z;
			foreach (Vertex vertex in printerBed.Vertices)
			{
				vertex.Position = vertex.Position - new Vector3(-printer.Bed.BedCenter, zTop + .02);
			}

			if (buildVolume != null)
			{
				foreach (Vertex vertex in buildVolume.Vertices)
				{
					vertex.Position = vertex.Position - new Vector3(-printer.Bed.BedCenter, 2.2);
				}
			}
			
			return (printerBed, buildVolume);
		}

		private ImageBuffer CreateCircularBedGridImage(int linesInX, int linesInY, int increment = 1)
		{
			Vector2 bedImageCentimeters = new Vector2(linesInX, linesInY);

			var bedplateImage = new ImageBuffer(1024, 1024);
			Graphics2D graphics2D = bedplateImage.NewGraphics2D();
			graphics2D.Clear(bedBaseColor);
			{
				double lineDist = bedplateImage.Width / (double)linesInX;

				int count = 1;
				int pointSize = 16;
				graphics2D.DrawString(count.ToString(), 4, 4, pointSize, color: bedMarkingsColor);
				double currentRadius = lineDist;
				Vector2 bedCenter = new Vector2(bedplateImage.Width / 2, bedplateImage.Height / 2);
				for (double linePos = lineDist + bedplateImage.Width / 2; linePos < bedplateImage.Width; linePos += lineDist)
				{
					int linePosInt = (int)linePos;
					graphics2D.DrawString((count * increment).ToString(), linePos + 2, bedplateImage.Height / 2, pointSize, color: bedMarkingsColor);

					Ellipse circle = new Ellipse(bedCenter, currentRadius);
					Stroke outline = new Stroke(circle);
					graphics2D.Render(outline, bedMarkingsColor);
					currentRadius += lineDist;
					count++;
				}

				graphics2D.Line(0, bedplateImage.Height / 2, bedplateImage.Width, bedplateImage.Height / 2, bedMarkingsColor);
				graphics2D.Line(bedplateImage.Width / 2, 0, bedplateImage.Width / 2, bedplateImage.Height, bedMarkingsColor);
			}

			return bedplateImage;
		}

		private ImageBuffer CreateRectangularBedGridImage(PrinterConfig printer, Vector3 displayVolumeToBuild, double divisor, double skip)
		{
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

			return bedplateImage;
		}

		private void ApplyOemBedImage(ImageBuffer bedImage)
		{
			// Add an oem/watermark image to the bedplate grid
			string imagePathAndFile = Path.Combine("OEMSettings", "bedimage.png");
			if (AggContext.StaticData.FileExists(imagePathAndFile))
			{
				if (watermarkImage == null)
				{
					watermarkImage = AggContext.StaticData.LoadImage(imagePathAndFile);
				}

				Graphics2D bedGraphics = bedImage.NewGraphics2D();
				bedGraphics.Render(
					watermarkImage, 
					new Vector2(
						(bedImage.Width - watermarkImage.Width) / 2, 
						(bedImage.Height - watermarkImage.Height) / 2));
			}
		}
	}
}