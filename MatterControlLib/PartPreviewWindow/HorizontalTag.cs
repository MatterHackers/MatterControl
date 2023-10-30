/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{

	public class HorizontalTag : GuiWidget
	{
		private IVertexSource tabShape = null;

		public Color TagColor { get; set; }

		public override void OnBoundsChanged(EventArgs e)
		{
			base.OnBoundsChanged(e);

			var rect = this.LocalBounds;
			var centerY = rect.YCenter;

			// Tab - core
			var radius = 3.0;
			var tabShape2 = new VertexStorage();
			tabShape2.MoveTo(rect.Left + radius, rect.Bottom);
			tabShape2.Curve3(rect.Left, rect.Bottom, rect.Left, rect.Bottom + radius);
			tabShape2.LineTo(rect.Left, rect.Top - radius);
			tabShape2.Curve3(rect.Left, rect.Top, rect.Left + radius, rect.Top);
			tabShape2.LineTo(rect.Right - 8, rect.Top);
			tabShape2.LineTo(rect.Right, centerY);
			tabShape2.LineTo(rect.Right - 8, rect.Bottom);
			tabShape2.LineTo(rect.Left, rect.Bottom);

			tabShape = new FlattenCurves(tabShape2);
		}

		public override void OnDrawBackground(Graphics2D graphics2D)
		{
			base.OnDrawBackground(graphics2D);

			if (tabShape != null)
			{
				graphics2D.Render(tabShape, this.TagColor);
			}
		}
	}
}
