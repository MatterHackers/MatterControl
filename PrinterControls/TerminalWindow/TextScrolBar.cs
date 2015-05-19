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

namespace MatterHackers.MatterControl
{
	public class TextScrollBar : GuiWidget
	{
		private TextScrollWidget textScrollWidget;
		private bool downOnBar = false;

		public TextScrollBar(TextScrollWidget textScrollWidget, int width)
			: base(width, ScrollBar.ScrollBarWidth)
		{
			this.textScrollWidget = textScrollWidget;
			Margin = new BorderDouble(0, 5);
			VAnchor = Agg.UI.VAnchor.ParentBottomTop;
			BackgroundColor = RGBA_Bytes.LightGray;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			int thumbHeight = 10;
			//graphics2D.Rectangle(LocalBounds, RGBA_Bytes.Black);
			double bottom = textScrollWidget.Position0To1 * (Height - thumbHeight);// the 2 is the border
			RectangleDouble thumb = new RectangleDouble(0, bottom, Width, bottom + thumbHeight);// the 1 is the border
			graphics2D.FillRectangle(thumb, RGBA_Bytes.DarkGray);
			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			downOnBar = true;
			textScrollWidget.Position0To1 = mouseEvent.Y / Height;
			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (downOnBar)
			{
				textScrollWidget.Position0To1 = mouseEvent.Y / Height;
			}
			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			downOnBar = false;
			base.OnMouseUp(mouseEvent);
		}

        public override void OnMouseWheel(MouseEventArgs mouseEvent)
        {
            double scrolledPos = mouseEvent.WheelDelta / Height + textScrollWidget.Position0To1;
            if (scrolledPos > 1)
            {
                scrolledPos = 1;
            }
            else if (scrolledPos < 0)
            {
                scrolledPos = 0;
            }
            textScrollWidget.Position0To1 = scrolledPos;
            base.OnMouseWheel(mouseEvent);
        }
	}
}