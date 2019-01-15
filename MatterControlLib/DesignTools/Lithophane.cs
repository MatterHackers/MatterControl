/*
Copyright (c) 2018, John Lewin
 */

using System;
using System.Diagnostics;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.Plugins.Lithophane
{
	public static class Lithophane
	{
		class PixelInfo
		{
			public Vector3 Top { get; set; }
			public Vector3 Bottom { get; set; }
		}

		public static Mesh Generate(IImageData resizedImage, double maxZ, double nozzleWidth, double pixelsPerMM, bool invert, IProgress<ProgressStatus> reporter)
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

			var progressStatus = new ProgressStatus();



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
					mesh.CreateFace(new [] { pixels[k].Top, pixels[j].Top, pixels[nextJ].Top, pixels[nextK].Top });
					mesh.CreateFace(new [] { pixels[j].Bottom, pixels[k].Bottom, pixels[nextK].Bottom, pixels[nextJ].Bottom });
					workIndex++;

					if (workIndex % notificationInterval == 0)
					{
						progressStatus.Progress0To1 = workIndex / workCount;
						reporter.Report(progressStatus);
					}
				}
			}

			// Side faces: East/West
			for (int j = 0; j < height - 1; ++j)
			{
				//Next row
				k = j + 1;

				// Create east, then west face
				mesh.CreateFace(new [] { leftRow[k].Top, leftRow[j].Top, leftRow[j].Bottom, leftRow[k].Bottom });
				mesh.CreateFace(new [] { rightRow[k].Top, rightRow[j].Top, rightRow[j].Bottom, rightRow[k].Bottom });
				workIndex++;

				if (workIndex % notificationInterval == 0)
				{
					progressStatus.Progress0To1 = workIndex / workCount;
					reporter.Report(progressStatus);
				}
			}

			// Side faces: North/South
			for (int j = 0; j < width - 1; ++j)
			{
				// Next row
				k = j + 1;

				// Create north, then south face
				mesh.CreateFace(new [] { frontRow[k].Top, frontRow[j].Top, frontRow[j].Bottom, frontRow[k].Bottom });
				mesh.CreateFace(new [] { backRow[k].Top, backRow[j].Top, backRow[j].Bottom, backRow[k].Bottom });
				workIndex++;

				if (workIndex % notificationInterval == 0)
				{
					progressStatus.Progress0To1 = workIndex / workCount;
					reporter.Report(progressStatus);
				}
			}

			Console.WriteLine("ElapsedTime - Face Generation: {0}", stopwatch.ElapsedMilliseconds);

			return mesh;
		}

		public interface IImageData
		{
			byte[] Pixels { get; }

			int Width { get; }
			int Height { get; }
		}

		public class ImageBufferImageData : IImageData
		{
			ImageBuffer resizedImage;

			public ImageBufferImageData(ImageBuffer image, double pixelWidth)
			{
				resizedImage = this.ToResizedGrayscale(image, pixelWidth);
				resizedImage.FlipY();
			}

			public int Width => resizedImage.Width;
			public int Height => resizedImage.Height;

			private ImageBuffer ToResizedGrayscale(ImageBuffer image, double onPlateWidth = 0)
			{
				var ratio = onPlateWidth / image.Width;

				var resizedImage = image.CreateScaledImage(ratio);

				var grayImage = resizedImage.ToGrayscale();

				// Render grayscale pixels onto resized image with larger pixel format needed by caller
				resizedImage.NewGraphics2D().Render(grayImage, 0, 0);

				return resizedImage;
			}

			public byte[] Pixels => resizedImage.GetBuffer();
		}
	}
}
