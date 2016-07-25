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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using System;

namespace MatterHackers.MatterControl
{
	public class SolidProgressControl : GuiWidget
	{
		public EventHandler ProgressChanged;
		private int percentComplete;
		public RGBA_Bytes fillColor;
		public RGBA_Bytes borderColor;

		public int PercentComplete
		{
			get { return percentComplete; }
			set
			{
				if (value != percentComplete)
				{
					if (ProgressChanged != null)
					{
						ProgressChanged(this, null);
					}
					percentComplete = value;
					Invalidate();
				}
			}
		}

		public SolidProgressControl(int width = 80, int height = 15)
			: base(width, height)
		{
			this.fillColor = ActiveTheme.Instance.PrimaryAccentColor;
			this.borderColor = ActiveTheme.Instance.PrimaryTextColor;

			this.AfterDraw += new DrawEventHandler(bar_Draw);
		}

		private void bar_Draw(GuiWidget drawingWidget, DrawEventArgs drawEvent)
		{
			if (drawingWidget != null && drawEvent != null && drawEvent.graphics2D != null)
			{
				drawEvent.graphics2D.FillRectangle(0, 0, drawingWidget.Width * PercentComplete / 100.0, drawingWidget.Height, fillColor);
				drawEvent.graphics2D.Rectangle(drawingWidget.LocalBounds, borderColor);
			}
		}
	}
}