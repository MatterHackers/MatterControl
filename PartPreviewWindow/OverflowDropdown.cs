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
using MatterHackers.VectorMath;
using System.IO;
using MatterHackers.Localizations;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using System;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class OverflowDropdown : ImageWidget
	{
		private static readonly RGBA_Bytes slightShade = new RGBA_Bytes(0, 0, 0, 40);

		private bool menuInitiallyActive = false;
		private bool overflowMenuActive = false;

		public OverflowDropdown(bool allowLightnessInvert)
			: base(LoadThemedIcon(allowLightnessInvert))
		{
			this.ToolTipText = "More...".Localize();
			this.Margin = 3;
		}

		public static ImageBuffer LoadThemedIcon(bool allowLightnessInvert)
		{
			var imageBuffer = StaticData.Instance.LoadIcon(Path.Combine("ViewTransformControls", "overflow.png"), 32, 32);
			if (!ActiveTheme.Instance.IsDarkTheme && allowLightnessInvert)
			{
				imageBuffer.InvertLightness();
			}

			return imageBuffer;
		}

		public GuiWidget PopupContent { get; set; }

		public Func<GuiWidget> DynamicPopupContent { get; set; }

		public bool AlignToRightEdge { get; set; }

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			menuInitiallyActive = overflowMenuActive;
			base.OnMouseDown(mouseEvent);
		}

		public override void OnClick(MouseEventArgs mouseEvent)
		{
			if (!menuInitiallyActive)
			{
				ShowPopup();
				this.BackgroundColor = slightShade;
			}

			base.OnClick(mouseEvent);
		}

		public void ShowPopup()
		{
			overflowMenuActive = true;

			this.PopupContent?.ClearRemovedFlag();

			if (this.DynamicPopupContent != null)
			{
				this.PopupContent = this.DynamicPopupContent();
			}

			if (this.PopupContent == null)
			{
				return;
			}

			var popupWidget = new PopupWidget(this.PopupContent, this, Vector2.Zero, Direction.Down, 0, this.AlignToRightEdge)
			{
				BorderWidth = 1,
				BorderColor = RGBA_Bytes.Gray,
				BackgroundColor = RGBA_Bytes.White,
			};
			popupWidget.Closed += (s, e) =>
			{
				this.BackgroundColor = RGBA_Bytes.Transparent;
				overflowMenuActive = false;
			};
			popupWidget.Focus();
		}
	}
}