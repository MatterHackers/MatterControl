/*
Copyright (c) 2017, John Lewin
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
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class ThemePreviewButton : GuiWidget
	{
		private GuiWidget accentColor;
		private GuiWidget secondaryBackground;
		private GuiWidget tertiaryBackground;
		private GuiWidget icon1;
		private GuiWidget icon2;
		private GuiWidget icon3;
		private string themeName = "";

		public ThemePreviewButton(IThemeColors theme, bool isActive)
		{
			this.Padding = 8;
			this.BackgroundColor = theme.PrimaryBackgroundColor;
			this.Cursor = Cursors.Hand;

			secondaryBackground = new GuiWidget()
			{
				HAnchor = HAnchor.AbsolutePosition | HAnchor.ParentLeft,
				VAnchor = VAnchor.ParentBottomTop,
				Margin = new BorderDouble(0),
				Width = 20,
				BackgroundColor = theme.SecondaryBackgroundColor,
			};
			this.AddChild(secondaryBackground);

			accentColor = new GuiWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.AbsolutePosition | VAnchor.ParentTop,
				Height = 6,
				Margin = new BorderDouble(left: 25),
				BackgroundColor = theme.PrimaryAccentColor,
			};
			this.AddChild(accentColor);

			icon1 = new GuiWidget()
			{
				HAnchor = HAnchor.AbsolutePosition | HAnchor.ParentLeft,
				VAnchor = VAnchor.AbsolutePosition | VAnchor.ParentTop,
				Height = 8,
				Width = 8,
				Margin = new BorderDouble(left: 6, top: 6),
				BackgroundColor = theme.PrimaryAccentColor,
			};
			this.AddChild(icon1);

			icon2 = new GuiWidget()
			{
				HAnchor = HAnchor.AbsolutePosition | HAnchor.ParentLeft,
				VAnchor = VAnchor.AbsolutePosition | VAnchor.ParentTop,
				Height = 8,
				Width = 8,
				Margin = new BorderDouble(left: 6, top: 20),
				BackgroundColor = theme.PrimaryAccentColor,
			};
			this.AddChild(icon2);

			icon3 = new GuiWidget()
			{
				HAnchor = HAnchor.AbsolutePosition | HAnchor.ParentLeft,
				VAnchor = VAnchor.AbsolutePosition | VAnchor.ParentTop,
				Height = 8,
				Width = 8,
				Margin = new BorderDouble(left: 6, top: 34),
				BackgroundColor = theme.PrimaryAccentColor,
			};
			this.AddChild(icon3);

			tertiaryBackground = new GuiWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.AbsolutePosition | VAnchor.ParentTop,
				Height = 37,
				Margin = new BorderDouble(left: 25, top: 12),
				BackgroundColor = theme.TertiaryBackgroundColor,
			};
			this.AddChild(tertiaryBackground);

			if (isActive)
			{
				this.AddChild(new ImageWidget(StaticData.Instance.LoadIcon("426.png", 16, 16).InvertLightness())
				{
					HAnchor = HAnchor.AbsolutePosition,
					VAnchor = VAnchor.AbsolutePosition,
					OriginRelativeParent = new VectorMath.Vector2(45, 20)
				});
			}

			var overlay = new GuiWidget();
			overlay.AnchorAll();
			overlay.Cursor = Cursors.Hand;
			overlay.Click += (s, e) =>
			{
				ThemeColorSelectorWidget.SetTheme(this.themeName);
			};

			this.AddChild(overlay);
		}

		public void SetThemeColors(IThemeColors theme)
		{
			accentColor.BackgroundColor = theme.PrimaryAccentColor;
			icon1.BackgroundColor = theme.PrimaryAccentColor;
			icon2.BackgroundColor = theme.PrimaryAccentColor;
			icon3.BackgroundColor = theme.PrimaryAccentColor;

			tertiaryBackground.BackgroundColor = theme.TertiaryBackgroundColor;
			secondaryBackground.BackgroundColor = theme.SecondaryBackgroundColor;

			this.BackgroundColor = theme.PrimaryBackgroundColor;
			this.themeName = theme.Name;
		}

		public override void OnClick(MouseEventArgs mouseEvent)
		{
			ThemeColorSelectorWidget.SetTheme(this.themeName);
			base.OnClick(mouseEvent);
		}
	}
}