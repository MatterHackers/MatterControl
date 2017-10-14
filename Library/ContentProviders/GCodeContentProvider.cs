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

using System.Threading.Tasks;

namespace MatterHackers.MatterControl
{
	using System;
	using MatterHackers.Agg;
	using MatterHackers.Agg.Font;
	using MatterHackers.Agg.Image;
	using MatterHackers.Agg.VertexSource;
	using MatterHackers.DataConverters3D;
	using MatterHackers.MatterControl.Library;
	using MatterHackers.VectorMath;

	public class GCodeContentProvider : ISceneContentProvider
	{
		public ImageBuffer DefaultImage => throw new NotImplementedException();

		private ImageBuffer thumbnailImage;

		public GCodeContentProvider()
		{
			int width = 60;

			var thumbIcon = new ImageBuffer(width, width);
			var graphics2D = thumbIcon.NewGraphics2D();
			var center = new Vector2(width / 2.0, width / 2.0);

			graphics2D.DrawString("GCode", center.x, center.y, 8 * width / 50, Justification.Center, Baseline.BoundsCenter, color: RGBA_Bytes.White);
			graphics2D.Render(
				new Stroke(
					new Ellipse(center, width / 2 - width / 12),
					width / 12),
				RGBA_Bytes.White);

			thumbnailImage = thumbIcon;
		}

		public Task<IObject3D> CreateItem(ILibraryItem item, Action<double, string> reporter)
		{
			System.Diagnostics.Debugger.Break();
			return null;
		}

		public Task GetThumbnail(ILibraryItem item, int width, int height, Action<ImageBuffer> imageCallback)
		{
			imageCallback(thumbnailImage);

			return Task.CompletedTask;
		}
	}
}
