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
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.VectorMath;

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

		public MenuItem CreateMenuItem(string name, ImageBuffer icon = null, string shortCut = null)
		{
			GuiWidget content;

			var textWidget = new TextWidget(name, pointSize: theme.DefaultFontSize)
			{
				Padding = MenuPadding,
			};

			if (shortCut != null)
			{
				content = new GuiWidget()
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit
				};

				content.AddChild(new TextWidget(shortCut, pointSize: theme.DefaultFontSize)
				{
					HAnchor = HAnchor.Right
				});

				content.AddChild(textWidget);
			}
			else
			{
				content = textWidget;
			}

			content.Selectable = false;

			var menuItem = new MenuItem(content, theme)
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

		public class CheckboxMenuItem : MenuItem, IIgnoredPopupChild, ICheckbox
		{
			private bool _checked;

			public CheckboxMenuItem(GuiWidget widget, ThemeConfig theme)
				: base(widget, theme)
			{
			}

			public override void OnLoad(EventArgs args)
			{
				this.Image = _checked ? faChecked : null;
				base.OnLoad(args);
			}

			public bool Checked
			{
				get => _checked;
				set
				{
					if (_checked != value)
					{
						_checked = value;
						this.Image = _checked ? faChecked : null;

						this.CheckedStateChanged?.Invoke(this, null);
						this.Invalidate();
					}
				}
			}

			public event EventHandler CheckedStateChanged;
		}

		public class RadioMenuItem : MenuItem, IIgnoredPopupChild, IRadioButton
		{
			private bool _checked;

			public RadioMenuItem(GuiWidget widget, ThemeConfig theme)
				: base (widget, theme)
			{
			}

			public override void OnLoad(EventArgs args)
			{
				// Init static radio icons if null
				if (radioIconChecked == null)
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
						Color.Gray,
						isChecked: false,
						isActive: false);
				}

				this.Image = _checked ? radioIconChecked : radioIconUnchecked;

				this.Invalidate();

				if (!this.SiblingRadioButtonList.Contains(this))
				{
					this.SiblingRadioButtonList.Add(this);
				}

				base.OnLoad(args);
			}

			public IList<GuiWidget> SiblingRadioButtonList { get; set; }

			public bool Checked
			{
				get => _checked;
				set
				{
					if (_checked != value)
					{
						_checked = value;

						this.Image = _checked ? radioIconChecked : radioIconUnchecked;

						if (_checked)
						{
							this.UncheckAllOtherRadioButtons();
						}

						this.CheckedStateChanged?.Invoke(this, null);

						this.Invalidate();
					}
				}
			}

			public event EventHandler CheckedStateChanged;
		}

		public MenuItem CreateBoolMenuItem(string name, Func<bool> getter, Action<bool> setter, bool useRadioStyle = false, IList<GuiWidget> SiblingRadioButtonList = null)
		{
			var textWidget = new TextWidget(name, pointSize: theme.DefaultFontSize)
			{
				Padding = MenuPadding,
			};

			bool isChecked = (getter?.Invoke() == true);

			MenuItem menuItem;

			if (useRadioStyle)
			{
				menuItem = new RadioMenuItem(textWidget, theme)
				{
					Name = name + " Menu Item",
					Checked = isChecked,
					SiblingRadioButtonList = SiblingRadioButtonList
				};
			}
			else
			{
				menuItem = new CheckboxMenuItem(textWidget, theme)
				{
					Name = name + " Menu Item",
					Checked = isChecked
				};
			}

			menuItem.Click += (s, e) =>
			{
				if (menuItem is RadioMenuItem radioMenu)
				{
					// Do nothing on reclick of active radio menu
					if (radioMenu.Checked)
					{
						return;
					}

					isChecked  = radioMenu.Checked = !radioMenu.Checked;
				}
				else if (menuItem is CheckboxMenuItem checkboxMenu)
				{
					isChecked = checkboxMenu.Checked = !isChecked;
				}

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
				this.MinimumSize = new Vector2(150, 32);
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