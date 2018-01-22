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

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PopupButton : GuiWidget, IIgnoredPopupChild
	{
		private static readonly Color slightShade = new Color(0, 0, 0, 40);

		public event EventHandler PopupWindowClosed;
		public event EventHandler BeforePopup;

		private GuiWidget buttonView;
		private bool menuVisibileAtMouseDown = false;
		private bool menuVisible = false;
		private PopupWidget popupWidget;

		public PopupButton()
		{
		}

		public PopupButton(GuiWidget buttonView)
		{
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit | VAnchor.Center;
			this.buttonView = buttonView;
			this.buttonView.Selectable = false;

			this.AddChild(buttonView);
		}

		public bool AlignToRightEdge { get; set; }
		public virtual Func<GuiWidget> DynamicPopupContent { get; set; }
		public IPopupLayoutEngine PopupLayoutEngine { get; set; }
		public Direction PopDirection { get; set; } = Direction.Down;
		public bool MakeScrollable { get; set; } = true;
		public virtual GuiWidget PopupContent { get; set; }

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			// Store the menu state at the time of mousedown
			menuVisibileAtMouseDown = menuVisible;
			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			// HACK: Child controls seem to be interfering with this.MouseCaptured - this short term workaround ensure we get clicks but likely mean mouse down outside of the control will fire the popup
			bool mouseUpInBounds = this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y);

			// Only show the popup if the menu was hidden as the mouse events started
			if ((mouseUpInBounds || buttonView?.MouseCaptured == true)
				&& !menuVisibileAtMouseDown)
			{
				ShowPopup();
			}

			base.OnMouseUp(mouseEvent);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (menuVisible)
			{
				graphics2D.FillRectangle(this.LocalBounds, slightShade);
			}

			base.OnDraw(graphics2D);
		}

		public void ShowPopup()
		{
			if (PopupLayoutEngine == null)
			{
				PopupLayoutEngine = new PopupLayoutEngine(this.PopupContent, this, this.PopDirection, 0, this.AlignToRightEdge);
			}
			menuVisible = true;

			this.PopupContent?.ClearRemovedFlag();

			if (this.DynamicPopupContent != null)
			{
				this.PopupContent = this.DynamicPopupContent();
			}

			if (this.PopupContent == null)
			{
				return;
			}

			this.OnBeforePopup();

			popupWidget = new PopupWidget(this.PopupContent, PopupLayoutEngine, MakeScrollable)
			{
				BorderWidth = 1,
				BorderColor = this.BorderColor,
			};

			popupWidget.Closed += (s, e) =>
			{
				menuVisible = false;
				popupWidget = null;

				this.PopupWindowClosed?.Invoke(this, null);
			};
			popupWidget.Focus();
		}

		protected virtual void OnBeforePopup()
		{
			this.BeforePopup?.Invoke(this, null);
		}
	}
}