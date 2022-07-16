/*
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.MatterControl.Library;

namespace MatterHackers.MatterControl
{
	public class ThumbnailsConfig
	{
		private static int[] cacheSizes = new int[]
		{
			18, 22, 50, 70, 100, 256
		};

		private static readonly object ThumbsLock = new object();

		private Queue<Func<Task>> queuedThumbCallbacks = new Queue<Func<Task>>();

		private AutoResetEvent thumbGenResetEvent = new AutoResetEvent(false);

		private Task thumbnailGenerator = null;

		private ThemeConfig Theme => ApplicationController.Instance.Theme;

		public ThumbnailsConfig()
		{
		}

		public ImageBuffer DefaultThumbnail() => StaticData.Instance.LoadIcon("cube.png", 16, 16).SetToColor(Theme.TextColor);

		public ImageBuffer LoadCachedImage(string cacheId, int width, int height)
		{
			var expectedCachePath = this.CachePath(cacheId, width, height);
			ImageBuffer cachedItem = LoadImage(expectedCachePath);
			if (cachedItem != null)
			{
				return cachedItem;
			}

			// if we don't find it see if it in the cache at a bigger size
			foreach (var cacheSize in cacheSizes.Where(s => s > width))
			{
				cachedItem = LoadImage(this.CachePath(cacheId, cacheSize, cacheSize));
				if (cachedItem != null
					&& cachedItem.Width > 0 && cachedItem.Height > 0)
				{
					cachedItem = cachedItem.CreateScaledImage(width, height);
					cachedItem.SetRecieveBlender(new BlenderPreMultBGRA());

					ImageIO.SaveImageData(expectedCachePath, cachedItem);

					return cachedItem;
				}
			}

			// could not find it in the user cache, try to load it from static data
			var staticDataFilename = Path.Combine("Images", "Thumbnails", $"{cacheId}-{256}x{256}.png");
			if (StaticData.Instance.FileExists(staticDataFilename))
			{
				cachedItem = StaticData.Instance.LoadImage(staticDataFilename);
				cachedItem.SetRecieveBlender(new BlenderPreMultBGRA());

				cachedItem = cachedItem.CreateScaledImage(width, height);

				ImageIO.SaveImageData(expectedCachePath, cachedItem);

				return cachedItem;
			}

			return null;
		}

		public ImageBuffer LoadCachedImage(ILibraryItem libraryItem, int width, int height)
		{
			// try to load it from the users cache
			var expectedCachePath = this.CachePath(libraryItem, width, height);

			ImageBuffer cachedItem = LoadImage(expectedCachePath);
			if (cachedItem != null
				&& cachedItem.Width > 0 && cachedItem.Height > 0)
			{
				cachedItem.SetRecieveBlender(new BlenderPreMultBGRA());
				return cachedItem;
			}

			// if we don't find it see if it in the cache at a bigger size
			foreach (var cacheSize in cacheSizes.Where(s => s > width))
			{
				cachedItem = LoadImage(this.CachePath(libraryItem, cacheSize, cacheSize));
				if (cachedItem != null
					&& cachedItem.Width > 0 && cachedItem.Height > 0)
				{
					cachedItem = cachedItem.CreateScaledImage(width, height);
					cachedItem.SetRecieveBlender(new BlenderPreMultBGRA());

					ImageIO.SaveImageData(expectedCachePath, cachedItem);

					return cachedItem;
				}
			}

			// could not find it in the user cache, try to load it from static data
			var staticDataFilename = Path.Combine("Images", "Thumbnails", CacheFilename(libraryItem, 256, 256));
			if (StaticData.Instance.FileExists(staticDataFilename))
			{
				cachedItem = StaticData.Instance.LoadImage(staticDataFilename);
				cachedItem.SetRecieveBlender(new BlenderPreMultBGRA());

				cachedItem = cachedItem.CreateScaledImage(width, height);

				ImageIO.SaveImageData(expectedCachePath, cachedItem);

				return cachedItem;
			}

			return null;
		}

		public string CachePath(string cacheId, int width, int height)
		{
			return ApplicationController.CacheablePath(
				Path.Combine("Thumbnails", "Content"),
				$"{cacheId}-{width}x{height}.png");
		}

		public string CachePath(ILibraryItem libraryItem)
		{
			return ApplicationController.CacheablePath(
				Path.Combine("Thumbnails", "Library"),
				$"{libraryItem.ID}.png");
		}

		public string CacheFilename(ILibraryItem libraryItem, int width, int height)
		{
			return $"{libraryItem.ID}-{width}x{height}.png";
		}

		public string CachePath(ILibraryItem libraryItem, int width, int height)
		{
			return ApplicationController.CacheablePath(
				Path.Combine("Thumbnails", "Library"),
				CacheFilename(libraryItem, width, height));
		}

		internal void QueueForGeneration(Func<Task> func)
		{
			lock (ThumbsLock)
			{
				if (thumbnailGenerator == null)
				{
					// Spin up a new thread once needed
					thumbnailGenerator = Task.Run((Action)ThumbGeneration);
				}

				queuedThumbCallbacks.Enqueue(func);
				thumbGenResetEvent.Set();
			}
		}

		private async void ThumbGeneration()
		{
			Thread.CurrentThread.Name = $"ThumbnailGeneration";

			while (!ApplicationController.Instance.ApplicationExiting)
			{
				Thread.Sleep(100);

				try
				{
					if (queuedThumbCallbacks.Count > 0)
					{
						Func<Task> callback;
						lock (ThumbsLock)
						{
							callback = queuedThumbCallbacks.Dequeue();
						}

						await callback();
					}
					else
					{
						// Process until queuedThumbCallbacks is empty then wait for new tasks via QueueForGeneration
						thumbGenResetEvent.WaitOne();
					}
				}
				catch (AppDomainUnloadedException)
				{
					return;
				}
				catch (ThreadAbortException)
				{
					return;
				}
				catch (Exception ex)
				{
					Console.WriteLine("Error generating thumbnail: " + ex.Message);
				}
			}

			// Null task reference on exit
			thumbnailGenerator = null;
		}

		private static ImageBuffer LoadImage(string filePath)
		{
			try
			{
				if (File.Exists(filePath))
				{
					return ImageIO.LoadImage(filePath).SetPreMultiply();
				}
			}
			catch { } // Suppress exceptions, return null on any errors

			return null;
		}

		public void Shutdown()
		{
			// Release the waiting ThumbnailGeneration task so it can shutdown gracefully
			thumbGenResetEvent?.Set();
		}

		public void DeleteCache(ILibraryItem sourceItem)
		{
			var thumbnailPath = ApplicationController.Instance.Thumbnails.CachePath(sourceItem);
			if (File.Exists(thumbnailPath))
			{
				File.Delete(thumbnailPath);
			}

			// Purge any specifically sized thumbnails
			foreach (var sizedThumbnail in Directory.GetFiles(Path.GetDirectoryName(thumbnailPath), Path.GetFileNameWithoutExtension(thumbnailPath) + "-*.png"))
			{
				File.Delete(sizedThumbnail);
			}
		}
	}
}