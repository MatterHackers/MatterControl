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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class ThemePreviewButton : GuiWidget
	{
		private GuiWidget accentColor;
		private Color activeColor;
		private GuiWidget secondaryBackground;
		private GuiWidget tertiaryBackground;
		private GuiWidget icon1;
		private GuiWidget icon2;
		private GuiWidget icon3;
		private ThemeConfig theme;
		private ImageWidget activeIcon;

		public ThemePreviewButton(ThemeConfig theme, ThemeColorPanel themeColorPanel)
		{
			this.theme = theme;
			activeColor = theme.Colors.SourceColor;

			var primaryAccentColor = theme.PrimaryAccentColor;

			this.Padding = 8;
			this.BackgroundColor = theme.ActiveTabColor;
			this.Cursor = Cursors.Hand;

			secondaryBackground = new GuiWidget()
			{
				HAnchor = HAnchor.Absolute | HAnchor.Left,
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(0),
				Width = 20,
				BackgroundColor = theme.MinimalShade,
			};
			this.AddChild(secondaryBackground);

			accentColor = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Absolute | VAnchor.Top,
				Height = 6,
				Margin = new BorderDouble(left: 25),
				BackgroundColor = primaryAccentColor,
			};
			this.AddChild(accentColor);

			icon1 = new GuiWidget()
			{
				HAnchor = HAnchor.Absolute | HAnchor.Left,
				VAnchor = VAnchor.Absolute | VAnchor.Top,
				Height = 8,
				Width = 8,
				Margin = new BorderDouble(left: 6, top: 6),
				BackgroundColor = primaryAccentColor,
			};
			this.AddChild(icon1);

			icon2 = new GuiWidget()
			{
				HAnchor = HAnchor.Absolute | HAnchor.Left,
				VAnchor = VAnchor.Absolute | VAnchor.Top,
				Height = 8,
				Width = 8,
				Margin = new BorderDouble(left: 6, top: 20),
				BackgroundColor = primaryAccentColor,
			};
			this.AddChild(icon2);

			icon3 = new GuiWidget()
			{
				HAnchor = HAnchor.Absolute | HAnchor.Left,
				VAnchor = VAnchor.Absolute | VAnchor.Top,
				Height = 8,
				Width = 8,
				Margin = new BorderDouble(left: 6, top: 34),
				BackgroundColor = primaryAccentColor,
			};
			this.AddChild(icon3);

			tertiaryBackground = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Absolute | VAnchor.Top,
				Height = 37,
				Margin = new BorderDouble(left: 25, top: 12),
				BackgroundColor = theme.SlightShade,
			};
			this.AddChild(tertiaryBackground);

			this.AddChild(activeIcon = new ImageWidget(themeColorPanel.CheckMark)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute,
				OriginRelativeParent = new Vector2(45, 20),
				Visible = false
			});

			var overlay = new GuiWidget
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Stretch,
				Cursor = Cursors.Hand
			};
			overlay.Click += (s, e) =>
			{
				UserSettings.Instance.set(UserSettingsKey.ThemeMode, this.Mode);

				// Activate the theme
				themeColorPanel.SetThemeColor(activeColor, this.Mode);
			};

			this.AddChild(overlay);
		}

		public bool IsActive
		{
			get => activeIcon.Visible;
			set => activeIcon.Visible = value;
		}

		public string Mode { get; internal set; }

		public void PreviewThemeColor(Color sourceColor)
		{
			var adjustedAccentColor = sourceColor;

			accentColor.BackgroundColor = adjustedAccentColor;
			icon1.BackgroundColor = adjustedAccentColor;
			icon2.BackgroundColor = adjustedAccentColor;
			icon3.BackgroundColor = adjustedAccentColor;

			activeColor = adjustedAccentColor;
		}
	}
}