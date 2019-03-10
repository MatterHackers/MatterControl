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

namespace MatterHackers.MatterControl
{
	public class MHDropDownList : DropDownList
	{
		private bool mouseInBounds = false;
		private ThemeConfig theme;

		public MHDropDownList(string noSelectionString, ThemeConfig theme, Direction direction = Direction.Down, double maxHeight = 0, bool useLeftIcons = false)
			: base (noSelectionString, theme.DropList.Inactive.TextColor, direction, maxHeight, useLeftIcons, theme.DefaultFontSize)
		{
			var menuTheme = AppContext.MenuTheme;

			this.MenuItemsBackgroundColor = menuTheme.BackgroundColor;
			this.MenuItemsTextColor = menuTheme.TextColor;
			this.MenuItemsBackgroundHoverColor = menuTheme.AccentMimimalOverlay;
			this.MenuItemsTextHoverColor = menuTheme.TextColor;
			this.MenuItemsBorderColor = menuTheme.DropList.Open.BackgroundColor;

			// Popup border color
			this.PopupBorderColor = theme.PopupBorderColor;

			this.theme = theme;

			// Clear border/background set by base, use border color override
			this.BorderColor = Color.Transparent;
			this.BackgroundColor = Color.Transparent;

			this.HoverColor = theme.EditFieldColors.Hovered.BackgroundColor;
		}

		public override Color BorderColor
		{
			get
			{
				if (base.BorderColor != Color.Transparent)
				{
					return base.BorderColor;
				}
				else if (menuVisible)
				{
					return Color.Transparent;
				}
				else if (this.mouseInBounds)
				{
					return theme.DropList.Hovered.BorderColor;
				}
				else if (this.ContainsFocus)
				{
					return theme.DropList.Focused.BorderColor;
				}
				else
				{
					return theme.DropList.Inactive.BorderColor;
				}
			}
			set => base.BorderColor = value;
		}

		public override Color BackgroundColor
		{
			get
			{
				if (base.BackgroundColor != Color.Transparent)
				{
					return base.BackgroundColor;
				}
				else if (menuVisible)
				{
					return theme.DropList.Open.BackgroundColor;
				}
				else if (this.mouseInBounds)
				{
					return theme.DropList.Hovered.BackgroundColor;
				}
				else
				{
					return theme.DropList.Inactive.BackgroundColor;
				}
			}
			set
			{
				base.BackgroundColor = value;
			}

		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			mouseInBounds = true;
			base.OnMouseEnterBounds(mouseEvent);

			this.Invalidate();
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			mouseInBounds = false;
			base.OnMouseLeaveBounds(mouseEvent);

			this.Invalidate();
		}

		protected override void OnMenuOpen()
		{
			mainControlText.TextColor = theme.DropList.Open.TextColor;
			base.OnMenuOpen();
		}

		protected override void OnMenuClose()
		{
			mainControlText.TextColor = theme.DropList.Inactive.TextColor;
			base.OnMenuClose();
		}
	}
}