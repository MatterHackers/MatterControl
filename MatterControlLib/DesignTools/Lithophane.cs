/*
Copyright (c) 2023, Lars Brubaker, John Lewin
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

using MatterHackers.Agg.Image;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System;
using System.Diagnostics;
using System.Linq;

namespace MatterHackers.MatterControl.Plugins.Lithophane
{
    public static class Lithophane
    {
        public interface IImageData
        {
            int Height { get; }
            byte[] Pixels { get; }

            int Width { get; }
        }

        public static Mesh Generate(IImageData resizedImage, double maxZ, double nozzleWidth, double pixelsPerMM, bool invert, Action<double, string> reporter)
        {
            // TODO: Move this to a user supplied value
            double baseThickness = nozzleWidth;     // base thickness (in mm)
            double zRange = maxZ - baseThickness;

            // Dimensions of image
            var width = resizedImage.Width;
            var height = resizedImage.Height;

            var zScale = zRange / 255;

            var pixelData = resizedImage.Pixels;

            Stopwatch stopwatch = Stopwatch.StartNew();

            var mesh = new Mesh();

            //var rescale = (double)onPlateWidth / imageData.Width;
            var rescale = 1;

            // Build an array of PixelInfo objects from each pixel
            // Collapse from 4 bytes per pixel to one - makes subsequent processing more logical and has minimal cost
            var pixels = pixelData.Where((x, i) => i % 4 == 0)

                // Interpolate the pixel color to zheight
                .Select(b => baseThickness + (invert ? 255 - b : b) * zScale)

                // Project to Vector3 for each pixel at the computed x/y/z
                .Select((z, i) => new Vector3(
                        i % width * rescale,
                        (i - i % width) / width * rescale * -1,
                        z))
                // Project to PixelInfo, creating a mirrored Vector3 at z0, paired together and added to the mesh
                .Select(vec =>
                {
                    var pixelInfo = new PixelInfo()
                    {
                        Top = vec,
                        Bottom = new Vector3(vec.X, vec.Y, 0)
                    };

                    mesh.Vertices.Add(pixelInfo.Top);
                    mesh.Vertices.Add(pixelInfo.Bottom);

                    return pixelInfo;
                }).ToArray();

            Console.WriteLine("ElapsedTime - PixelInfo Linq Generation: {0}", stopwatch.ElapsedMilliseconds);
            stopwatch.Restart();

            // Select pixels along image edges
            var backRow = pixels.Take(width).Reverse().ToArray();
            var frontRow = pixels.Skip((height - 1) * width).Take(width).ToArray();
            var leftRow = pixels.Where((x, i) => i % width == 0).ToArray();
            var rightRow = pixels.Where((x, i) => (i + 1) % width == 0).Reverse().ToArray();

            int k,
                nextJ,
                nextK;

            var notificationInterval = 100;

            var workCount = (resizedImage.Width - 1) * (resizedImage.Height - 1) +
                            (height - 1) +
                            (width - 1);

            double workIndex = 0;

            // Vertical faces: process each row and column, creating the top and bottom faces as appropriate
            for (int i = 0; i < resizedImage.Height - 1; ++i)
            {
                var startAt = i * width;

                // Process each column
                for (int j = startAt; j < startAt + resizedImage.Width - 1; ++j)
                {
                    k = j + 1;
                    nextJ = j + resizedImage.Width;
                    nextK = nextJ + 1;

                    // Create north, then south face
                    mesh.CreateFace(pixels[k].Top, pixels[j].Top, pixels[nextJ].Top, pixels[nextK].Top);
                    mesh.CreateFace(pixels[j].Bottom, pixels[k].Bottom, pixels[nextK].Bottom, pixels[nextJ].Bottom);
                    workIndex++;

                    if (workIndex % notificationInterval == 0)
                    {
                        reporter?.Invoke(workIndex / workCount, null);
                    }
                }
            }

            // Side faces: East/West
            for (int j = 0; j < height - 1; ++j)
            {
                //Next row
                k = j + 1;

                // Create east, then west face
                mesh.CreateFace(leftRow[k].Top, leftRow[j].Top, leftRow[j].Bottom, leftRow[k].Bottom);
                mesh.CreateFace(rightRow[k].Top, rightRow[j].Top, rightRow[j].Bottom, rightRow[k].Bottom);
                workIndex++;

                if (workIndex % notificationInterval == 0)
                {
                    reporter?.Invoke(workIndex / workCount, null);
                }
            }

            // Side faces: North/South
            for (int j = 0; j < width - 1; ++j)
            {
                // Next row
                k = j + 1;

                // Create north, then south face
                mesh.CreateFace(frontRow[k].Top, frontRow[j].Top, frontRow[j].Bottom, frontRow[k].Bottom);
                mesh.CreateFace(backRow[k].Top, backRow[j].Top, backRow[j].Bottom, backRow[k].Bottom);
                workIndex++;

                if (workIndex % notificationInterval == 0)
                {
                    reporter?.Invoke(workIndex / workCount, null);
                }
            }

            Console.WriteLine("ElapsedTime - Face Generation: {0}", stopwatch.ElapsedMilliseconds);

            return mesh;
        }

        public class ImageBufferImageData : IImageData
        {
            private ImageBuffer resizedImage;

            public ImageBufferImageData(ImageBuffer image, double pixelWidth)
            {
                resizedImage = this.ToResizedGrayscale(image, pixelWidth).MirrorY();
            }

            public int Height => resizedImage.Height;
            public byte[] Pixels => resizedImage.GetBuffer();
            public int Width => resizedImage.Width;

            private ImageBuffer ToResizedGrayscale(ImageBuffer image, double onPlateWidth = 0)
            {
                var ratio = onPlateWidth / image.Width;

                var resizedImage = image.CreateScaledImage(ratio);

                var grayImage = resizedImage.ToGrayscale();

                // Render grayscale pixels onto resized image with larger pixel format needed by caller
                resizedImage.NewGraphics2D().Render(grayImage, 0, 0);

                return resizedImage;
            }
        }

        private class PixelInfo
        {
            public Vector3 Bottom { get; set; }
            public Vector3 Top { get; set; }
        }
    }
}