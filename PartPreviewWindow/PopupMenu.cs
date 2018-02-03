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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PopupMenu : FlowLayoutWidget
	{
		public static int GutterWidth { get; set; } = 35;

		private ThemeConfig theme;

		public static BorderDouble MenuPadding { get; set; } = new BorderDouble(40, 8, 20, 8);

		public static Color DisabledTextColor { get; set; } = Color.Gray;

		public PopupMenu(ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Fit;
		}

		public HorizontalLine CreateHorizontalLine()
		{
			var line = new HorizontalLine(40)
			{
				Margin = new BorderDouble(PopupMenu.GutterWidth - 8, 1, 8, 1)
			};

			this.AddChild(line);

			return line;
		}

		public MenuItem CreateMenuItem(string name, ImageBuffer icon = null)
		{
			var textWidget = new TextWidget(name, pointSize: theme.DefaultFontSize)
			{
				Padding = MenuPadding,
			};

			var menuItem = new MenuItem(textWidget, theme)
			{
				Name = name + " Menu Item",
				Image = icon
			};

			this.AddChild(menuItem);

			return menuItem;
		}

		private static ImageBuffer faChecked = AggContext.StaticData.LoadIcon("fa-check_16.png");

		private static ImageBuffer radioIconChecked;

		private static ImageBuffer radioIconUnchecked;

		public MenuItem CreateBoolMenuItem(string name, Func<bool> getter, Action<bool> setter, bool useRadioStyle = false)
		{
			if (useRadioStyle
				&& radioIconChecked == null)
			{
				radioIconChecked = new ImageBuffer(16, 16).SetPreMultiply();
				radioIconUnchecked = new ImageBuffer(16, 16).SetPreMultiply();

				var rect = new RectangleDouble(0, 0, 16, 16);

				RadioImage.DrawCircle(
					radioIconChecked.NewGraphics2D(),
					rect.Center,
					Color.Black,
					isChecked: true,
					isActive: false);

				RadioImage.DrawCircle(
					radioIconUnchecked.NewGraphics2D(),
					rect.Center,
					Color.Black,
					isChecked: false,
					isActive: false);
			}

			var textWidget = new TextWidget(name, pointSize: theme.DefaultFontSize)
			{
				Padding = MenuPadding,
			};

			bool isChecked = (getter?.Invoke() == true);

			ImageBuffer checkedIcon = (useRadioStyle) ? radioIconChecked : faChecked;
			ImageBuffer uncheckedIcon = (useRadioStyle) ? radioIconUnchecked : null;

			var menuItem = new MenuItem(textWidget, theme)
			{
				Name = name + " Menu Item",
				Image = (isChecked) ? checkedIcon : uncheckedIcon
			};

			menuItem.Click += (s, e) =>
			{
				isChecked = !isChecked;
				setter?.Invoke(isChecked);
			};

			this.AddChild(menuItem);

			return menuItem;
		}

		public MenuItem CreateMenuItem(GuiWidget guiWidget, string name)
		{
			var menuItem = new MenuItem(guiWidget, theme)
			{
				Text = name,
				Name = name + " Menu Item"
			};

			this.AddChild(menuItem);

			return menuItem;
		}

		public class MenuItem : SimpleButton
		{
			private GuiWidget content;

			public MenuItem(GuiWidget content, ThemeConfig theme)
				: base (theme)
			{
				this.Padding = new BorderDouble(left: PopupMenu.GutterWidth, right: 15);
				this.BackgroundColor = Color.White;
				this.HAnchor = HAnchor.MaxFitOrStretch;
				this.VAnchor = VAnchor.Fit;
				this.MinimumSize = new VectorMath.Vector2(150, 32);
				this.content = content;

				content.VAnchor = VAnchor.Center;
				this.AddChild(content);
			}

			public ImageBuffer Image { get; set; }

			public override bool Enabled
			{
				get => base.Enabled;
				set
				{
					if (content is TextWidget textWidget)
					{
						textWidget.TextColor = (value) ? Color.Black : PopupMenu.DisabledTextColor;
					}

					base.Enabled = value;
				}
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				if (this.Image != null)
				{
					var x = this.Image.Width / 2 - PopupMenu.GutterWidth + 2;
					var y = this.Size.Y / 2 - this.Image.Height / 2;

					graphics2D.Render(this.Image, x, y);
				}

				base.OnDraw(graphics2D);
			}
		}

	}
}