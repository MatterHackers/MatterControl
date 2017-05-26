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

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PartPreviewWidget : GuiWidget
	{
		protected readonly int ShortButtonHeight = 25;
		protected int SideBarButtonWidth;

		public TextImageButtonFactory textImageButtonFactory;
		protected TextImageButtonFactory checkboxButtonFactory;
		public TextImageButtonFactory ExpandMenuOptionFactory;
		public TextImageButtonFactory WhiteButtonFactory;

		protected ViewControls2D viewControls2D;

		protected GuiWidget buttonRightPanelDisabledCover;
		protected FlowLayoutWidget buttonRightPanel;

		public PartPreviewWidget()
		{
			if (UserSettings.Instance.IsTouchScreen)
			{
				SideBarButtonWidth = 180;
				ShortButtonHeight = 40;
			}
			else
			{
				SideBarButtonWidth = 138;
				ShortButtonHeight = 30;
			}

			textImageButtonFactory = new TextImageButtonFactory()
			{
				normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
				hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
				pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
				disabledTextColor = ActiveTheme.Instance.TabLabelUnselected,
				disabledFillColor = new RGBA_Bytes()
			};

			WhiteButtonFactory = new TextImageButtonFactory()
			{
				FixedWidth = SideBarButtonWidth,
				FixedHeight = ShortButtonHeight,

				normalFillColor = RGBA_Bytes.White,
				normalTextColor = RGBA_Bytes.Black,
				hoverTextColor = RGBA_Bytes.Black,

				hoverFillColor = new RGBA_Bytes(255, 255, 255, 200),
				borderWidth = 1,

				normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200),
				hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200)
			};

			ExpandMenuOptionFactory = new TextImageButtonFactory()
			{
				FixedWidth = SideBarButtonWidth,
				normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
				hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
				disabledTextColor = ActiveTheme.Instance.PrimaryTextColor,
				pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,

				hoverFillColor = new RGBA_Bytes(255, 255, 255, 50),
				pressedFillColor = new RGBA_Bytes(255, 255, 255, 50),
				disabledFillColor = new RGBA_Bytes(255, 255, 255, 50)
			};

			checkboxButtonFactory = new TextImageButtonFactory()
			{
				fontSize = 11,
				FixedWidth = SideBarButtonWidth,
				borderWidth = 3,

				normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
				normalBorderColor = new RGBA_Bytes(0, 0, 0, 0),
				normalFillColor = ActiveTheme.Instance.PrimaryBackgroundColor,

				hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
				hoverBorderColor = new RGBA_Bytes(0, 0, 0, 50),
				hoverFillColor = new RGBA_Bytes(0, 0, 0, 50),

				pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
				pressedBorderColor = new RGBA_Bytes(0, 0, 0, 50),

				disabledTextColor = ActiveTheme.Instance.PrimaryTextColor
			};
		}
	}
}