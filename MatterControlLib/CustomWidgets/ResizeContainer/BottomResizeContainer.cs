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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using System;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class BottomResizeContainer : FlowLayoutWidget
	{
		private double downHeight = 0;
		private bool mouseDownOnBar = false;
		private double mouseDownY;

		private int _splitterHeight;

		internal BottomResizeContainer(ThemeConfig theme)
			: base (FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Absolute;
			this.SplitterHeight = theme.SplitterWidth;
			this.SpliterBarColor = theme.SplitterBackground;
		}

		public event EventHandler Resized;
		public Color SpliterBarColor { get; set; }

		public int SplitterHeight
		{
			get => _splitterHeight;
			set
			{
				if (_splitterHeight != value)
				{
					_splitterHeight = value;
					this.Padding = new BorderDouble(0, _splitterHeight, 0, 0);
					this.MinimumSize = new VectorMath.Vector2(0, _splitterHeight);
				}
			}
		}

		protected virtual void OnResized(EventArgs e)
		{
			this.Resized?.Invoke(this, e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			graphics2D.FillRectangle(LocalBounds.Left, LocalBounds.Bottom, LocalBounds.Right, LocalBounds.Bottom + this.SplitterHeight, this.SpliterBarColor);
			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Position.Y <= this.LocalBounds.Bottom + this.SplitterHeight
				&& mouseEvent.Position.Y >= this.LocalBounds.Bottom)
			{
				mouseDownOnBar = true;
				mouseDownY = TransformToScreenSpace(mouseEvent.Position).Y;
				downHeight = Height;
			}
			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (mouseDownOnBar)
			{
				int currentMouseY = (int)TransformToScreenSpace(mouseEvent.Position).Y;
				UiThread.RunOnIdle(() =>
				{
					Height = downHeight + mouseDownY - currentMouseY;
				});
			}

			var currentCursor = this.Cursor;

			if (mouseEvent.Position.Y <= this.LocalBounds.Bottom + this.SplitterHeight
				&& mouseEvent.Position.Y >= this.LocalBounds.Bottom)
			{
				this.Cursor = Cursors.HSplit;

			}
			else
			{
				this.Cursor = Cursors.Default;
			}

			if (this.FirstWidgetUnderMouse
				&& this.Cursor != currentCursor)
			{
				this.SetCursor(this.Cursor);
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			if(mouseDownOnBar)
			{
				mouseEvent.WheelDelta = 0;
			}
			base.OnMouseWheel(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			var mouseUpY = TransformToScreenSpace(mouseEvent.Position).Y;
			if (mouseDownOnBar
				&& mouseUpY != mouseDownY)
			{
				OnResized(null);
			}

			mouseDownOnBar = false;
			base.OnMouseUp(mouseEvent);
		}
	}
}