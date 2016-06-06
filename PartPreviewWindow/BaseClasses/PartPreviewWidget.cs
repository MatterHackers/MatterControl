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

		public TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		protected TextImageButtonFactory checkboxButtonFactory = new TextImageButtonFactory();
		public TextImageButtonFactory ExpandMenuOptionFactory = new TextImageButtonFactory();
		public TextImageButtonFactory WhiteButtonFactory = new TextImageButtonFactory();

		protected ViewControls2D viewControls2D;

		protected Cover buttonRightPanelDisabledCover;
		protected FlowLayoutWidget buttonRightPanel;

		public PartPreviewWidget()
		{
			if (UserSettings.Instance.DisplayMode == ApplicationDisplayType.Touchscreen)
			{
				SideBarButtonWidth = 180;
				ShortButtonHeight = 40;
			}
			else
			{
				SideBarButtonWidth = 138;
				ShortButtonHeight = 30;
			}

			textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
			
			textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.TabLabelUnselected;
			textImageButtonFactory.disabledFillColor = new RGBA_Bytes();


			WhiteButtonFactory.FixedWidth = SideBarButtonWidth;
			WhiteButtonFactory.FixedHeight = ShortButtonHeight;
			WhiteButtonFactory.normalFillColor = RGBA_Bytes.White;
			WhiteButtonFactory.normalTextColor = RGBA_Bytes.Black;
			WhiteButtonFactory.hoverTextColor = RGBA_Bytes.Black;
			WhiteButtonFactory.hoverFillColor = new RGBA_Bytes(255, 255, 255, 200);
			WhiteButtonFactory.borderWidth = 1;
			WhiteButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
			WhiteButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

			ExpandMenuOptionFactory.FixedWidth = SideBarButtonWidth;
			ExpandMenuOptionFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			ExpandMenuOptionFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			ExpandMenuOptionFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
			ExpandMenuOptionFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
			ExpandMenuOptionFactory.hoverFillColor = new RGBA_Bytes(255, 255, 255, 50);
			ExpandMenuOptionFactory.pressedFillColor = new RGBA_Bytes(255, 255, 255, 50);
			ExpandMenuOptionFactory.disabledFillColor = new RGBA_Bytes(255, 255, 255, 50);

			checkboxButtonFactory.fontSize = 11;
			checkboxButtonFactory.FixedWidth = SideBarButtonWidth;
			checkboxButtonFactory.borderWidth = 3;

			checkboxButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			checkboxButtonFactory.normalBorderColor = new RGBA_Bytes(0, 0, 0, 0);
			checkboxButtonFactory.normalFillColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			checkboxButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			checkboxButtonFactory.hoverBorderColor = new RGBA_Bytes(0, 0, 0, 50);
			checkboxButtonFactory.hoverFillColor = new RGBA_Bytes(0, 0, 0, 50);

			checkboxButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
			checkboxButtonFactory.pressedBorderColor = new RGBA_Bytes(0, 0, 0, 50);

			checkboxButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;

			BackgroundColor = RGBA_Bytes.White;
		}
	}
}