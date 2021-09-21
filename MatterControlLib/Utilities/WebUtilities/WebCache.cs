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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl
{
	public static class WebCache
	{
		private static HashSet<string> savedImages = new HashSet<string>();

		private static object locker = new object();

		// Download an image from the web into the specified ImageBuffer
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
						ImageIO.SaveImageData(imageFileName, imageToLoadInto);
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

		// Download an image from the web into the specified ImageSequence
		public static void RetrieveImageSquenceAsync(ImageSequence imageSequenceToLoadInto,
			string uriToLoad,
			Action doneLoading = null)
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
						lock (locker)
						{
							StaticData.Instance.LoadImageSequenceData(new StreamReader(pngFileName).BaseStream, asyncImageSequence);
						}

						UiThread.RunOnIdle(() =>
						{
							imageSequenceToLoadInto.Copy(asyncImageSequence);
							imageSequenceToLoadInto.Invalidate();
							doneLoading?.Invoke();
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

				Task.Run(() =>
				{
					try
					{
						lock (locker)
						{
							StaticData.Instance.LoadImageSequenceData(new StreamReader(gifFileName).BaseStream, asyncImageSequence);
						}

						if (asyncImageSequence.NumFrames > 0)
						{
							UiThread.RunOnIdle(() =>
							{
								imageSequenceToLoadInto.Copy(asyncImageSequence);
								imageSequenceToLoadInto.Invalidate();
								doneLoading?.Invoke();
							});
						}
						else
						{
							DownloadImageAsync(imageSequenceToLoadInto, uriToLoad, doneLoading, asyncImageSequence, pngFileName, gifFileName);
						}
					}
					catch
					{
						DownloadImageAsync(imageSequenceToLoadInto, uriToLoad, doneLoading, asyncImageSequence, pngFileName, gifFileName);
					}
				});

				return;
			}

			DownloadImageAsync(imageSequenceToLoadInto, uriToLoad, doneLoading, asyncImageSequence, pngFileName, gifFileName);
		}

		private static void DownloadImageAsync(ImageSequence imageSequenceToLoadInto, string uriToLoad, Action doneLoading, ImageSequence asyncImageSequence, string pngFileName, string gifFileName)
		{
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

						lock (locker)
						{
							StaticData.Instance.LoadImageSequenceData(stream, asyncImageSequence);
						}

						if (asyncImageSequence.Frames.Count == 1)
						{
							// save the as png
							lock (locker)
							{
								if (!File.Exists(pngFileName))
								{
									ImageIO.SaveImageData(pngFileName, asyncImageSequence.Frames[0]);
								}
							}
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
							doneLoading?.Invoke();
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

		/// <summary>
		/// Return the first result that can be found (usually the cache). Wait up to 5 seconds to populate the cache
		/// if it does not exist.
		/// </summary>
		/// <param name="uriToLoad"></param>
		/// <param name="addToAppCache"></param>
		/// <param name="addHeaders"></param>
		/// <returns></returns>
		public static string GetCachedText(string uriToLoad,
			bool addToAppCache = true,
			Action<HttpRequestMessage> addHeaders = null)
		{
			string results = null;
			WebCache.RetrieveText(uriToLoad,
				(content) =>
				{
					results = content;
				},
				false,
				addHeaders);

			var startTime = UiThread.CurrentTimerMs;
			// wait up to 5 seconds for a response
			while (results == null
				&& UiThread.CurrentTimerMs < startTime + 5000)
			{
				Thread.Sleep(10);
			}

			return results;
		}

		/// <summary>
		/// Retrieve text from a url async, but first return any existing cache of the text synchronously
		/// </summary>
		/// <param name="uriToLoad">The web path to find the text, will also be used as the cache key</param>
		/// <param name="updateResult">A function to call when the text is received if it is different than the cache.</param>
		/// <param name="addToAppCache">Add the results to a directory that can be copied into the main distribution,
		/// or add them to a directory that is only for the local machine.</param>
		public static void RetrieveText(string uriToLoad,
			Action<string> updateResult,
			bool addToAppCache = true,
			Action<HttpRequestMessage> addHeaders = null)
		{
			if (addToAppCache)
			{
				RetrieveText(uriToLoad, "TextWebCache", updateResult, addHeaders);
			}
			else
			{
				RetrieveText(uriToLoad, "Text", updateResult, addHeaders);
			}
		}

		private static void RetrieveText(string uriToLoad,
			string cacheFolder,
			Action<string> updateResult,
			Action<HttpRequestMessage> addHeaders = null)
		{
			var longHash = uriToLoad.GetLongHashCode();

			var appDataFileName = ApplicationController.CacheablePath(cacheFolder, longHash.ToString() + ".txt");

			string fileText = null;
			// first try the cache in the users applications folder
			if (File.Exists(appDataFileName))
			{
				try
				{
					lock (locker)
					{
						fileText = File.ReadAllText(appDataFileName);
					}

					updateResult?.Invoke(fileText);
				}
				catch
				{
				}
			}
			else // We could not find it in the application cache. Check if it is in static data.
			{
				var staticDataPath = Path.Combine(cacheFolder, longHash.ToString() + ".txt");

				if (StaticData.Instance.FileExists(staticDataPath))
				{
					try
					{
						lock (locker)
						{
							fileText = StaticData.Instance.ReadAllText(staticDataPath);
						}

						updateResult?.Invoke(fileText);
					}
					catch
					{
					}
				}
			}

			// whether we find it or not check the web for the latest version
			Task.Run(async () =>
			{
				var requestMessage = new HttpRequestMessage(HttpMethod.Get, uriToLoad);
				addHeaders?.Invoke(requestMessage);
				using (var client = new HttpClient())
				{
					using (HttpResponseMessage response = await client.SendAsync(requestMessage))
					{
						var text = await response.Content.ReadAsStringAsync();
						if (!string.IsNullOrEmpty(text)
							&& text != fileText)
						{
							File.WriteAllText(appDataFileName, text);
							updateResult?.Invoke(text);
						}
					}
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
				lock (locker)
				{
					// scale the loaded image to the size of the target image
					StaticData.Instance.LoadImageData(stream, unScaledImage);
				}

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
			}
			else
			{
				StaticData.Instance.LoadImageData(stream, imageToLoadInto);
			}
		}
	}
}