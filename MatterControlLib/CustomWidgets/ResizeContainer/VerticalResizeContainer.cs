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
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public enum GrabBarSide { Left, Right }

	public class VerticalResizeContainer : FlowLayoutWidget
	{
		public event EventHandler Resized;

		private double downWidth = 0;
		private bool mouseDownOnBar = false;
		private double mouseDownX;

		private int _splitterWidth;
		private GrabBarSide grabSide;
		private bool mouseOverBar;
		protected ThemeConfig theme;

		internal VerticalResizeContainer(ThemeConfig theme, GrabBarSide grabSide)
			: base (FlowDirection.TopToBottom)
		{
			this.grabSide = grabSide;
			this.HAnchor = HAnchor.Absolute;
			this.SplitterWidth = theme.SplitterWidth;
			this.SplitterBarColor = theme.SplitterBackground;
			this.theme = theme;
		}

		public override Cursors Cursor
		{
			get
			{
				if (mouseOverBar)
				{
					return Cursors.VSplit;
				}

				return Cursors.Default;
			}

			set => base.Cursor = value;
		}

		public Color SplitterBarColor { get; set; }

		public int SplitterWidth
		{
			get => _splitterWidth;
			set
			{
				if (_splitterWidth != value)
				{
					_splitterWidth = value;

					if (grabSide == GrabBarSide.Left)
					{
						this.Padding = new BorderDouble(_splitterWidth, 0, 0, 0);
					}
					else
					{
						this.Padding = new BorderDouble(0, 0, _splitterWidth, 0);
					}

					this.MinimumSize = new VectorMath.Vector2(_splitterWidth, 0);
				}
			}
		}

		protected virtual void OnResized(EventArgs e)
		{
			this.Resized?.Invoke(this, e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (grabSide == GrabBarSide.Left)
			{
				graphics2D.FillRectangle(LocalBounds.Left, LocalBounds.Bottom, LocalBounds.Left + this.SplitterWidth, LocalBounds.Top, this.SplitterBarColor);
			}
			else
			{
				graphics2D.FillRectangle(LocalBounds.Right - this.SplitterWidth, LocalBounds.Bottom, LocalBounds.Right, LocalBounds.Top, this.SplitterBarColor);
			}

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if ((grabSide == GrabBarSide.Left && mouseEvent.Position.X < LocalBounds.Left + this.SplitterWidth)
				|| (grabSide == GrabBarSide.Right && mouseEvent.Position.X > LocalBounds.Right - this.SplitterWidth))
			{
				mouseDownOnBar = true;
				mouseDownX = TransformToScreenSpace(mouseEvent.Position).X;
				downWidth = Width;
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if ((grabSide == GrabBarSide.Left && mouseEvent.Position.X < LocalBounds.Left + this.SplitterWidth)
				|| (grabSide == GrabBarSide.Right && mouseEvent.Position.X > LocalBounds.Right - this.SplitterWidth))
			{
				mouseOverBar = true;
			}
			else
			{
				mouseOverBar = false;
			}

			if (mouseDownOnBar)
			{
				int currentMouseX = (int)TransformToScreenSpace(mouseEvent.Position).X;
				UiThread.RunOnIdle(() =>
				{
					if (grabSide == GrabBarSide.Left)
					{
						Width = downWidth + mouseDownX - currentMouseX;
					}
					else
					{
						Width = downWidth + currentMouseX- mouseDownX;
					}
				});
			}
			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			var mouseUpX = TransformToScreenSpace(mouseEvent.Position).X;
			if(mouseDownOnBar
				&& mouseUpX != mouseDownX)
			{
				OnResized(null);
			}

			mouseDownOnBar = false;

			base.OnMouseUp(mouseEvent);
		}
	}
}