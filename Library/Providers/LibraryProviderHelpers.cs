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

using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.Library
{
	public static class LibraryProviderHelpers
	{
		public static void RenderCentered(this Graphics2D graphics2D, ImageBuffer imageBuffer, double width, double height)
		{
			int targetWidth = graphics2D.DestImage.Width;
			int targetHeight = graphics2D.DestImage.Height;

			graphics2D.Render(
				imageBuffer,
				width == targetWidth ? 0 : targetWidth / 2 - width / 2,
				height == targetHeight? 0 : targetHeight / 2 - height / 2,
				width,
				height);
		}

		/// <summary>
		/// Generates a resized images matching the target bounds
		/// </summary>
		/// <param name="imageBuffer">The ImageBuffer to resize</param>
		/// <param name="targetWidth">The target width</param>
		/// <param name="targetHeight">The target height</param>
		/// <returns>A resized ImageBuffer contrained to the given bounds and centered on the new surface</returns>
		public static ImageBuffer ResizeImage(ImageBuffer imageBuffer, int targetWidth, int targetHeight)
		{
			var expectedSize = new Vector2((int)(targetWidth * GuiWidget.DeviceScale), (int)(targetHeight * GuiWidget.DeviceScale));

			int width = imageBuffer.Width;
			int height = imageBuffer.Height;

			bool resizeWidth = width >= height;
			bool resizeRequired = (resizeWidth) ? width != expectedSize.X : height != expectedSize.Y;
			if (resizeRequired)
			{
				var scaledImageBuffer = ImageBuffer.CreateScaledImage(imageBuffer, targetWidth, targetHeight);
				scaledImageBuffer.SetRecieveBlender(new BlenderPreMultBGRA());

				return scaledImageBuffer;
			}

			return imageBuffer;
		}
	}
}
