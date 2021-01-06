/*
Copyright (c) 2019 Lars Brubaker, John Lewin
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
	public class PopupButton : GuiWidget, IIgnoredPopupChild, IMenuCreator
	{
		protected GuiWidget buttonView;
		protected bool menuVisible = false;
		private bool menuVisibileAtMouseDown = false;
		private PopupWidget popupWidget;
		private bool overridePopupHAnchor = false;
		private bool overridePopupVAnchor = false;

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

		public event EventHandler BeforePopup;

		public event EventHandler PopupWindowClosed;

		public bool AlignToRightEdge { get; set; }

		public bool AlwaysKeepOpen { get; set; }

		public override Color BackgroundColor
		{
			get => menuVisible ? this.OpenColor : base.BackgroundColor;
			set => base.BackgroundColor = value;
		}

		public virtual Func<GuiWidget> DynamicPopupContent { get; set; }

		public Color HoverColor { get; set; } = new Color(0, 0, 0, 40);

		public bool KeepMenuOpen => menuVisible || this.AlwaysKeepOpen;

		public bool MakeScrollable { get; set; } = true;

		public Color OpenColor { get; set; } = new Color(0, 0, 0, 40);

		public Direction PopDirection { get; set; } = Direction.Down;

		public Color PopupBorderColor { get; set; } = Color.Transparent;

		public virtual GuiWidget PopupContent { get; set; }

		private HAnchor _popupHAnchor;

		public HAnchor PopupHAnchor
		{
			get => _popupHAnchor;
			set
			{
				overridePopupHAnchor = true;
				_popupHAnchor = value;
			}
		}

		public IPopupLayoutEngine PopupLayoutEngine { get; set; }

		private VAnchor _popupVAnchor;

		public VAnchor PopupVAnchor
		{
			get => _popupVAnchor;
			set
			{
				overridePopupVAnchor = true;
				_popupVAnchor = value;
			}
		}

		public void CloseMenu() => popupWidget?.CloseMenu();

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			if (!menuVisibileAtMouseDown)
			{
				this.ShowPopup();
			}

			base.OnClick(mouseEvent);
		}

		public override void OnClosed(EventArgs e)
		{
			this.PopupContent?.Close();
			base.OnClosed(e);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			// Store the menu state at the time of mousedown
			menuVisibileAtMouseDown = menuVisible;
			base.OnMouseDown(mouseEvent);
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

			if (this.PopupContent == null
				|| this.PopupContent.Children.Count <= 0)
			{
				menuVisible = false;
				return;
			}

			this.OnBeforePopup();

			popupWidget = new PopupWidget(this.PopupContent, PopupLayoutEngine, MakeScrollable)
			{
				BorderWidth = 1,
				BorderColor = this.PopupBorderColor,
			};

			popupWidget.Closed += (s, e) =>
			{
				menuVisible = false;
				popupWidget = null;

				this.PopupWindowClosed?.Invoke(this, null);

				// If DynamicPopupContent is used, we need to close the PopupContent reference as it's constructed every time
				if (this.DynamicPopupContent != null)
				{
					this.PopupContent.Close();
				}
			};

			if (overridePopupHAnchor)
			{
				popupWidget.HAnchor = PopupHAnchor;
			}

			if (overridePopupVAnchor)
			{
				popupWidget.VAnchor = PopupVAnchor;
			}

			popupWidget.Focus();
		}

		protected virtual void OnBeforePopup()
		{
			this.BeforePopup?.Invoke(this, null);
		}
	}
}