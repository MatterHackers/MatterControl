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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class LeftClipFlowLayoutWidget : FlowLayoutWidget
	{
		public int GradientDistance { get; set; } = 5;

		private ImageBuffer gradientBackground;

		public override void OnDrawBackground(Graphics2D graphics2D)
		{
			if (gradientBackground != null)
			{
				graphics2D.Render(gradientBackground, this.LocalBounds.Left, 0);

				var bounds = this.LocalBounds;

				if (bounds.Width > gradientBackground.Width)
				{
					// Fill anything outside of the gradient region with the opaque background
					graphics2D.FillRectangle(
						new RectangleDouble(gradientBackground.Width, bounds.Bottom, bounds.Right, bounds.Top),
						this.BackgroundColor);
				}
			}
			else
			{
				base.OnDrawBackground(graphics2D);
			}
		}

		public override void OnLoad(EventArgs args)
		{
			base.OnLoad(args);

			if (this.GradientDistance > 0)
			{
				gradientBackground = agg_basics.TrasparentToColorGradientX(
						(int)this.LocalBounds.Width + this.GradientDistance,
						(int)this.LocalBounds.Height,
						this.BackgroundColor,
						this.GradientDistance);

				gradientBackground.SetRecieveBlender(new BlenderPreMultBGRA());
			}
		}
	}
}