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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl
{
	public static class WebCache
	{
		private static string _cachePath = ".";

		private static HashSet<string> savedImages = new HashSet<string>();

		/// <summary>
		/// Download an image from the web into the specified ImageBuffer
		/// </summary>
		/// <param name="uri"></param>
		public static void RetrieveImageAsync(ImageBuffer imageToLoadInto, string uriToLoad, bool scaleToImageX, IRecieveBlenderByte scalingBlender = null)
		{
			var longHash = uriToLoad.GetLongHashCode();
			var imageFileName = ApplicationController.CacheablePath("Images", longHash.ToString() + ".png");

			if (File.Exists(imageFileName))
			{
				try
				{
					LoadImageInto(imageToLoadInto, scaleToImageX, scalingBlender, new StreamReader(imageFileName).BaseStream);
					return;
				}
				catch
				{
				}
			}

			WebClient client = new WebClient();
			client.DownloadDataCompleted += (sender, e) =>
			{
				try // if we get a bad result we can get a target invocation exception. In that case just don't show anything
				{
					Stream stream = new MemoryStream(e.Result);

					LoadImageInto(imageToLoadInto, scaleToImageX, scalingBlender, stream);

					if (imageToLoadInto.Width > 0
						&& imageToLoadInto.Height > 0
						&& !savedImages.Contains(imageFileName))
					{
						savedImages.Add(imageFileName);
						AggContext.ImageIO.SaveImageData(imageFileName, imageToLoadInto);
					}
				}
				catch
				{
				}
			};

			try
			{
				client.DownloadDataAsync(new Uri(uriToLoad));
			}
			catch
			{
			}
		}

		/// <summary>
		/// Download an image from the web into the specified ImageSequence
		/// </summary>
		/// <param name="uri"></param>
		public static void RetrieveImageSquenceAsync(ImageSequence imageSequenceToLoadInto, string uriToLoad)
		{
			var asyncImageSequence = new ImageSequence();

			var longHash = uriToLoad.GetLongHashCode();
			var pngFileName = ApplicationController.CacheablePath("Images", longHash.ToString() + ".png");
			var gifFileName = ApplicationController.CacheablePath("Images", longHash.ToString() + ".gif");

			if (File.Exists(pngFileName))
			{
				try
				{
					Task.Run(() =>
					{
						AggContext.StaticData.LoadImageSequenceData(new StreamReader(pngFileName).BaseStream, asyncImageSequence);
						UiThread.RunOnIdle(() =>
						{
							imageSequenceToLoadInto.Copy(asyncImageSequence);
							imageSequenceToLoadInto.Invalidate();
						});
					});

					return;
				}
				catch
				{
				}
			}
			else if (File.Exists(gifFileName))
			{
				try
				{
					Task.Run(() =>
					{
						AggContext.StaticData.LoadImageSequenceData(new StreamReader(gifFileName).BaseStream, asyncImageSequence);
						UiThread.RunOnIdle(() =>
						{
							imageSequenceToLoadInto.Copy(asyncImageSequence);
							imageSequenceToLoadInto.Invalidate();
						});
					});

					return;
				}
				catch
				{
				}
			}

			WebClient client = new WebClient();
			client.DownloadDataCompleted += (object sender, DownloadDataCompletedEventArgs e) =>
			{
				try // if we get a bad result we can get a target invocation exception. In that case just don't show anything
				{
					Task.Run(() =>
					{
						// scale the loaded image to the size of the target image
						byte[] raw = e.Result;
						Stream stream = new MemoryStream(raw);

						AggContext.StaticData.LoadImageSequenceData(stream, asyncImageSequence);

						if (asyncImageSequence.Frames.Count == 1)
						{
							// save the as png
							AggContext.ImageIO.SaveImageData(pngFileName, asyncImageSequence.Frames[0]);
						}
						else // save original stream as gif
						{
							using (var writter = new FileStream(gifFileName, FileMode.Create))
							{
								stream.Position = 0;
								stream.CopyTo(writter);
							}
						}

						UiThread.RunOnIdle(() =>
						{
							imageSequenceToLoadInto.Copy(asyncImageSequence);
							imageSequenceToLoadInto.Invalidate();
						});
					});
				}
				catch
				{
				}
			};

			try
			{
				client.DownloadDataAsync(new Uri(uriToLoad));
			}
			catch
			{
			}
		}

		public static void RetrieveText(string uriToLoad, Action<string> updateResult)
		{
			var longHash = uriToLoad.GetLongHashCode();

			var textFileName = ApplicationController.CacheablePath("Text", longHash.ToString() + ".txt");

			string fileText = null;
			if (File.Exists(textFileName))
			{
				try
				{
					fileText = File.ReadAllText(textFileName);
					updateResult?.Invoke(fileText);
				}
				catch
				{
				}
			}

			Task.Run(async () =>
			{
				var client = new HttpClient();
				var text = await client.GetStringAsync(uriToLoad);
				if (text != fileText)
				{
					File.WriteAllText(textFileName, text);
					updateResult?.Invoke(text);
				}
			});
		}

		private static void LoadImageInto(ImageBuffer imageToLoadInto, bool scaleToImageX, IRecieveBlenderByte scalingBlender, Stream stream)
		{
			if (scalingBlender == null)
			{
				scalingBlender = new BlenderBGRA();
			}

			ImageBuffer unScaledImage = new ImageBuffer(10, 10);
			if (scaleToImageX)
			{
				// scale the loaded image to the size of the target image
				AggContext.StaticData.LoadImageData(stream, unScaledImage);

				// If the source image (the one we downloaded) is more than twice as big as our dest image.
				while (unScaledImage.Width > imageToLoadInto.Width * 2)
				{
					// The image sampler we use is a 2x2 filter so we need to scale by a max of 1/2 if we want to get good results.
					// So we scale as many times as we need to get the Image to be the right size.
					// If this were going to be a non-uniform scale we could do the x and y separately to get better results.
					ImageBuffer halfImage = new ImageBuffer(unScaledImage.Width / 2, unScaledImage.Height / 2, 32, scalingBlender);
					halfImage.NewGraphics2D().Render(unScaledImage, 0, 0, 0, halfImage.Width / (double)unScaledImage.Width, halfImage.Height / (double)unScaledImage.Height);
					unScaledImage = halfImage;
				}

				double finalScale = imageToLoadInto.Width / (double)unScaledImage.Width;
				imageToLoadInto.Allocate(imageToLoadInto.Width, (int)(unScaledImage.Height * finalScale), imageToLoadInto.Width * (imageToLoadInto.BitDepth / 8), imageToLoadInto.BitDepth);
				imageToLoadInto.NewGraphics2D().Render(unScaledImage, 0, 0, 0, finalScale, finalScale);
				imageToLoadInto.MarkImageChanged();
			}
			else
			{
				AggContext.StaticData.LoadImageData(stream, imageToLoadInto);
				imageToLoadInto.MarkImageChanged();
			}
		}
	}
}