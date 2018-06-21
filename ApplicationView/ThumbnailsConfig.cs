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

namespace MatterHackers.MatterControl
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using MatterHackers.Agg.Image;
	using MatterHackers.Agg.Platform;
	using MatterHackers.MatterControl.Library;

	public class ThumbnailsConfig
	{
		private readonly static object thumbsLock = new object();

		private Queue<Func<Task>> queuedThumbCallbacks = new Queue<Func<Task>>();

		public Dictionary<Type, ImageBuffer> OperationIcons { get; internal set; }

		public ImageBuffer DefaultThumbnail { get; } = AggContext.StaticData.LoadIcon("cube.png");

		public ImageBuffer LoadCachedImage(string cacheId, int width, int height)
		{
			ImageBuffer cachedItem = LoadImage(this.CachePath(cacheId, width, height));
			if (cachedItem != null)
			{
				return cachedItem;
			}

			if (width < 100
				&& height < 100)
			{
				// check for a 100x100 image
				var cachedAt100x100 = LoadImage(this.CachePath(cacheId, 100, 100));
				if (cachedAt100x100 != null)
				{
					return cachedAt100x100.CreateScaledImage(width, height);
				}
			}

			return null;
		}

		public string CachePath(string cacheId)
		{
			// TODO: Use content SHA
			return ApplicationController.CacheablePath("ItemThumbnails", $"{cacheId}.png");
		}

		public string CachePath(string id, int width, int height)
		{
			return ApplicationController.CacheablePath("ItemThumbnails", $"{id}-{width}x{height}.png");
		}

		private AutoResetEvent thumbGenResetEvent = new AutoResetEvent(false);

		private Task thumbnailGenerator = null;

		internal void QueueForGeneration(Func<Task> func)
		{
			lock (thumbsLock)
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
						lock (thumbsLock)
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
					return AggContext.ImageIO.LoadImage(filePath).SetPreMultiply();
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
	}
}