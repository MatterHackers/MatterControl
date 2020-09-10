/*
Copyright (c) 2017, John Lewin
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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.Library;

namespace MatterHackers.MatterControl.DesignTools
{
	/// <summary>
	/// Loads IObject3D and thumbnails for ImageBuffer based ILibraryItem objects
	/// </summary>
	public class ImageContentProvider : ISceneContentProvider
	{
		public Task<IObject3D> CreateItem(ILibraryItem item, Action<double, string> reporter)
		{
			return Task.Run<IObject3D>(async () =>
			{
				var imageBuffer = await this.LoadImage(item);
				if (imageBuffer != null)
				{
					string assetPath = "";

					if (item is FileSystemFileItem fileItem)
					{
						assetPath = fileItem.Path;
					}
					else if (item is ILibraryAssetStream streamInterface)
					{
						using (var streamAndLength = await streamInterface.GetStream(null))
						{
							assetPath = AssetObject3D.AssetManager.StoreStream(streamAndLength.Stream, Path.GetExtension(streamInterface.FileName));
						}
					}

					return new ImageObject3D()
					{
						AssetPath = assetPath,
						Name = "Image"
					};
				}

				return null;
			});
		}

		private async Task<ImageBuffer> LoadImage(ILibraryItem item)
		{
			// Load the image at its native size, let the caller scale or resize
			if (item is ILibraryAssetStream streamInterface)
			{
				using (var streamAndLength = await streamInterface.GetStream(null))
				{
					var imageBuffer = new ImageBuffer();
					if (AggContext.ImageIO.LoadImageData(streamAndLength.Stream, imageBuffer))
					{
						imageBuffer.SetRecieveBlender(new BlenderPreMultBGRA());
						return imageBuffer;
					}
				}
			}

			return null;
		}

		public Task<ImageBuffer> GetThumbnail(ILibraryItem item, int width, int height)
		{
			return Task.Run<ImageBuffer>(async () =>
			{
				var thumbnail = await LoadImage(item);
				if (thumbnail != null)
				{
					thumbnail = LibraryProviderHelpers.ResizeImage(thumbnail, width, height);

					// Cache library thumbnail
					AggContext.ImageIO.SaveImageData(
						ApplicationController.Instance.Thumbnails.CachePath(item, width, height),
						thumbnail);
				}

				return thumbnail;
			});
		}

		public ImageBuffer DefaultImage => AggContext.StaticData.LoadIcon("140.png", 16, 16);
	}
}