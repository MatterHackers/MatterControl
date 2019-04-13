/*
Copyright (c) 2019, John Lewin
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

using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class RoundedRectShape : FlattenCurves
	{ 
		public RoundedRectShape(RectangleDouble rect, double radius)
			: this (rect, radius, radius, radius, radius)
		{
		}

		public RoundedRectShape(RectangleDouble rect, double topLeftRadius = 0, double topRightRadius = 0, double bottomRightRadius = 0, double bottomLeftRadius = 0)
			: base(BuildShape(rect, topLeftRadius, topRightRadius, bottomRightRadius, bottomLeftRadius))
		{
		}

		private static IVertexSource BuildShape(RectangleDouble rect, double topLeftRadius, double topRightRadius, double bottomRightRadius, double bottomLeftRadius)
		{
			// See https://photos.app.goo.gl/YdTiehf6ih7fSoDA9 for point diagram

			var centerY = rect.YCenter;

			double radius;
			var tabShape2 = new VertexStorage();

			// A -> B
			radius = bottomLeftRadius;

			tabShape2.MoveTo(rect.Left + radius, rect.Bottom);
			if (radius > 0)
			{
				tabShape2.curve3(rect.Left, rect.Bottom, rect.Left, rect.Bottom + radius);
			}

			// C -> D
			radius = topLeftRadius;
			tabShape2.LineTo(rect.Left, rect.Top - radius);
			if (radius > 0)
			{
				tabShape2.curve3(rect.Left, rect.Top, rect.Left + radius, rect.Top);
			}

			// E -> F
			radius = topRightRadius;
			tabShape2.LineTo(rect.Right - radius, rect.Top);
			if (radius > 0)
			{
				tabShape2.curve3(rect.Right, rect.Top, rect.Right, rect.Top - radius);
			}

			// G -> H
			radius = bottomRightRadius;
			tabShape2.LineTo(rect.Right, rect.Bottom + radius);
			if (radius > 0)
			{
				tabShape2.curve3(rect.Right, rect.Bottom, rect.Right - radius, rect.Bottom);
			}

			// H -> A
			radius = bottomLeftRadius;
			tabShape2.LineTo(rect.Left - radius, rect.Bottom);

			return new FlattenCurves(tabShape2);
		}
	}
}
