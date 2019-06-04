/*
Copyright (c) 2018, John Lewin
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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class DropButton : SimpleButton
	{
		public bool MenuVisible { get; private set; }

		public DropButton(ThemeConfig theme)
			: base(theme)
		{
			this.AnchorMate = new MatePoint(this)
			{
				Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
				AltMate = new MateOptions(MateEdge.Left, MateEdge.Top)
			};

			this.PopupMate = new MatePoint()
			{
				Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
				AltMate = new MateOptions(MateEdge.Right, MateEdge.Top)
			};

			this.AltPopupBounds = default(RectangleDouble);
		}

		public Func<GuiWidget> PopupContent { get; set; }

		public MatePoint AnchorMate { get; set; }

		public MatePoint PopupMate { get; set; }

		public RectangleDouble AltPopupBounds { get; }

		public override Color BackgroundColor
		{
			get => MenuVisible ? theme.SlightShade : base.BackgroundColor;
			set => base.BackgroundColor = value;
		}

		private bool downWhileOpen = false;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			downWhileOpen = MenuVisible;

			base.OnMouseDown(mouseEvent);
		}

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			if (!MenuVisible
				&& !downWhileOpen)
			{
				if (this.AnchorMate.Widget == null)
				{
					this.AnchorMate.Widget = this;
				};

				var popupContent = this.PopupContent();
				if (popupContent == null
					|| popupContent.Children.Count <= 0)
				{
					return;
				}

				this.PopupMate.Widget = popupContent;

				if (this.Parents<SystemWindow>().FirstOrDefault() is SystemWindow systemWindow)
				{
					MenuVisible = true;

					void popupContent_Closed(object sender, EventArgs e)
					{
						// Reset menuVisible
						MenuVisible = false;
						popupContent.Closed -= popupContent_Closed;
					}

					popupContent.Closed += popupContent_Closed;

					systemWindow.ShowPopup(
						this.AnchorMate,
						this.PopupMate,
						this.AltPopupBounds);
				}
			}

			base.OnClick(mouseEvent);
		}
	}
}