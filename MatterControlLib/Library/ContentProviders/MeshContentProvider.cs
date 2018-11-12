/*
Copyright (c) 2018, John Lewin
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

using System.Threading.Tasks;

namespace MatterHackers.MatterControl
{
	using System;
	using System.IO;
	using System.Threading;
	using MatterHackers.Agg.Image;
	using MatterHackers.Agg.Platform;
	using MatterHackers.DataConverters3D;
	using MatterHackers.MatterControl.DesignTools.Operations;
	using MatterHackers.MatterControl.Library;
	using MatterHackers.RayTracer;

	/// <summary>
	/// Loads IObject3D objects for mesh based ILibraryItems
	/// </summary>
	public class MeshContentProvider : ISceneContentProvider
	{
		private static readonly bool Is32Bit = IntPtr.Size == 4;

		// For 32 bit max size to ray trace is 8 MB mesh for 64 bit the max size is 40 MB.
		private long MaxFileSizeForTracing => Is32Bit ? 8 * 1000 * 1000 : 16 * 1000 * 1000;

		private long MaxFileSizeForThumbnail => Is32Bit ? 16 * 1000 * 1000 : 32 * 1000 * 1000;

		public Task<IObject3D> CreateItem(ILibraryItem item, Action<double, string> progressReporter)
		{
			return Task.Run(async () =>
			{
				IObject3D loadedItem = null;

				try
				{
					var streamInterface = item as ILibraryAssetStream;
					if (streamInterface != null)
					{
						using (var contentStream = await streamInterface.GetStream(progressReporter))
						{
							if (contentStream != null)
							{
								// TODO: Wire up caching
								loadedItem = Object3D.Load(contentStream.Stream, Path.GetExtension(streamInterface.FileName), CancellationToken.None, null /*itemCache*/, progressReporter);

								// Set MeshPath for non-mcx content. Avoid on mcx to ensure serialization of children
								if (item is FileSystemFileItem fileItem
									&& !string.Equals(Path.GetExtension(fileItem.FileName), ".mcx", StringComparison.OrdinalIgnoreCase))
								{
									loadedItem.MeshPath = fileItem.Path;
								}
							}
						}
					}
					else
					{
						var contentInterface = item as ILibraryObject3D;
						loadedItem = await contentInterface?.GetObject3D(progressReporter);
					}
				}
				catch { }

				if (loadedItem != null)
				{
					// Push the original Library item name through to the IObject3D
					loadedItem.Name = item.Name;
				}

				// Notification should force invalidate and redraw
				progressReporter?.Invoke(1, "");

				return loadedItem;
			});
		}

		public async Task<ImageBuffer> GetThumbnail(ILibraryItem libraryItem, int width, int height)
		{
			IObject3D object3D = null;

			long fileSize = 0;
			if (libraryItem is ILibraryAssetStream contentModel
				// Only load the stream if it's available - prevents download of Internet content simply for thumbnails
				&& contentModel.LocalContentExists
				&& contentModel.FileSize < MaxFileSizeForThumbnail)
			{
				fileSize = contentModel.FileSize;
				// TODO: Wire up limits for thumbnail generation. If content is too big, return null allowing the thumbnail to fall back to content default
				object3D = await contentModel.CreateContent();
			}
			else if (libraryItem is ILibraryObject3D)
			{
				object3D = await (libraryItem as ILibraryObject3D)?.GetObject3D(null);
			}

			if (object3D == null)
			{
				return DefaultImage;
			}

			string thumbnailId = libraryItem.ID;

			var thumbnail = GetThumbnail(object3D, thumbnailId, width, height);
			if (thumbnail != null)
			{
				// Cache content thumbnail
				AggContext.ImageIO.SaveImageData(
					ApplicationController.Instance.Thumbnails.CachePath(object3D.MeshRenderId().ToString(), width, height),
					thumbnail);

				// Cache library thumbnail
				AggContext.ImageIO.SaveImageData(
					ApplicationController.Instance.Thumbnails.CachePath(libraryItem, width, height),
					thumbnail);
			}

			return thumbnail ?? DefaultImage;
		}

		// Limit to private scope until need returns
		private ImageBuffer GetThumbnail(IObject3D item, string thumbnailId, int width, int height)
		{
			if (item == null)
			{
				return DefaultImage;
			}

			int estimatedMemorySize = item.EstimatedMemory();
			if (estimatedMemorySize > MaxFileSizeForThumbnail)
			{
				return DefaultImage;
			}

			bool forceOrthographic = false;
			if (estimatedMemorySize > MaxFileSizeForTracing)
			{
				forceOrthographic = true;
			}

			bool RenderOrthographic = (forceOrthographic) ? true : UserSettings.Instance.ThumbnailRenderingMode == "orthographic";

			return ThumbnailEngine.Generate(
				item,
				RenderOrthographic ? RenderType.ORTHOGROPHIC : RenderType.RAY_TRACE,
				width,
				height,
				allowMultiThreading: !ApplicationController.Instance.AnyPrintTaskRunning);
		}

		public ImageBuffer DefaultImage { get; } = AggContext.StaticData.LoadIcon("mesh.png");
	}
}