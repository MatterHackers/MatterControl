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
using MatterHackers.Agg.VertexSource;
using MatterHackers.ImageProcessing;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PopupMenu : FlowLayoutWidget, IIgnoredPopupChild
	{
		private ThemeConfig theme;

		public BorderDouble MenuPadding => new BorderDouble(40, 8, 20, 8);

		public static Color DisabledTextColor { get; set; } = Color.Gray;

		public PopupMenu(ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Fit;
			this.BackgroundColor = theme.BackgroundColor;
		}

		public HorizontalLine CreateSeparator()
		{
			var line = new HorizontalLine(theme: ApplicationController.Instance.MenuTheme)
			{
				Margin = new BorderDouble(theme.MenuGutterWidth - 8, 1, 8, 1),
				BackgroundColor = theme.RowBorder
			};

			this.AddChild(line);

			return line;
		}

		public MenuItem CreateMenuItem(string name, ImageBuffer icon = null, string shortCut = null)
		{
			GuiWidget content;

			var textWidget = new TextWidget(name, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
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

				content.AddChild(new TextWidget(shortCut, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
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

			menuItem.Click += (s, e) =>
			{
				Unfocus();
			};

			this.AddChild(menuItem);

			return menuItem;
		}

		public class SubMenuItemButton : MenuItem, IIgnoredPopupChild
		{
			public PopupMenu SubMenu { get; set; }


			public SubMenuItemButton(GuiWidget content, ThemeConfig theme) : base(content, theme)
			{
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				base.OnDraw(graphics2D);

				// draw the right arrow
				var x = this.LocalBounds.Right - this.LocalBounds.Height / 2;
				var y = this.Size.Y / 2 + 2;

				var arrow = new VertexStorage();
				arrow.MoveTo(x + 3, y);
				arrow.LineTo(x - 3, y + 5);
				arrow.LineTo(x - 3, y - 5);

				graphics2D.Render(arrow, theme.TextColor);
			}

			public bool KeepMenuOpen
			{
				get
				{
					if (SubMenu != null)
					{
						return SubMenu.ContainsFocus;
					}

					return false;
				}
			}
		}

		public class CheckboxMenuItem : MenuItem, IIgnoredPopupChild, ICheckbox
		{
			private bool _checked;

			private ImageBuffer faChecked;

			public CheckboxMenuItem(GuiWidget widget, ThemeConfig theme)
				: base(widget, theme)
			{
				faChecked = AggContext.StaticData.LoadIcon("fa-check_16.png", 16, 16, theme.InvertIcons);
			}

			public override void OnLoad(EventArgs args)
			{
				this.Image = _checked ? faChecked : null;
				base.OnLoad(args);
			}

			public bool KeepMenuOpen => false;

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

			private ImageBuffer radioIconChecked;

			private ImageBuffer radioIconUnchecked;

			public RadioMenuItem(GuiWidget widget, ThemeConfig theme)
				: base(widget, theme)
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
						theme.TextColor,
						isChecked: true,
						isActive: false);

					RadioImage.DrawCircle(
						radioIconUnchecked.NewGraphics2D(),
						rect.Center,
						theme.TextColor,
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

			public bool KeepMenuOpen => false;

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
							this.UncheckSiblings();
						}

						this.CheckedStateChanged?.Invoke(this, null);

						this.Invalidate();
					}
				}
			}

			public event EventHandler CheckedStateChanged;
		}

		public void CreateSubMenu(string menuTitle, ThemeConfig menuTheme, Action<PopupMenu> populateSubMenu, ImageBuffer icon = null)
		{
			var content = new TextWidget(menuTitle, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				Padding = MenuPadding,
			};

			content.Selectable = false;

			var subMenuItemButton = new SubMenuItemButton(content, theme)
			{
				Name = menuTitle + " Menu Item",
				Image = icon
			};

			this.AddChild(subMenuItemButton);

			subMenuItemButton.Click += (s, e) =>
			{
				var systemWindow = this.Parents<SystemWindow>().FirstOrDefault();
				if (systemWindow == null)
				{
					return;
				}

				var subMenu = new PopupMenu(menuTheme);
				subMenuItemButton.SubMenu = subMenu;

				UiThread.RunOnIdle(() =>
				{
					populateSubMenu(subMenu);
					
					systemWindow.ShowPopup(
						new MatePoint(subMenuItemButton)
						{
							Mate = new MateOptions(MateEdge.Right, MateEdge.Top),
							AltMate = new MateOptions(MateEdge.Left, MateEdge.Top)
						},
						new MatePoint(subMenu)
						{
							Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
							AltMate = new MateOptions(MateEdge.Right, MateEdge.Top)
						}
						);// altBounds: new RectangleDouble(mouseEvent.X + 1, mouseEvent.Y + 1, mouseEvent.X + 1, mouseEvent.Y + 1));
				});

				subMenu.Closed += (s1, e1) =>
				{
					subMenu.ClearRemovedFlag();
					subMenuItemButton.SubMenu = null;
					if(!this.ContainsFocus)
					{
						this.Close();
					}
				};
			};
		}

		public MenuItem CreateBoolMenuItem(string name, Func<bool> getter, Action<bool> setter, bool useRadioStyle = false, IList<GuiWidget> siblingRadioButtonList = null)
		{
			var textWidget = new TextWidget(name, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				Padding = MenuPadding,
			};

			return this.CreateBoolMenuItem(textWidget, name, getter, setter, useRadioStyle, siblingRadioButtonList);
		}

		public MenuItem CreateBoolMenuItem(string name, ImageBuffer icon, Func<bool> getter, Action<bool> setter, bool useRadioStyle = false, IList<GuiWidget> siblingRadioButtonList = null)
		{
			var row = new FlowLayoutWidget()
			{
				Selectable = false
			};
			row.AddChild(new IconButton(icon, theme));

			var textWidget = new TextWidget(name, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				Padding = MenuPadding,
				VAnchor = VAnchor.Center
			};
			row.AddChild(textWidget);

			return this.CreateBoolMenuItem(row, name, getter, setter, useRadioStyle, siblingRadioButtonList);
		}

		public MenuItem CreateBoolMenuItem(GuiWidget guiWidget, string name, Func<bool> getter, Action<bool> setter, bool useRadioStyle = false, IList<GuiWidget> siblingRadioButtonList = null)
		{
			bool isChecked = (getter?.Invoke() == true);

			MenuItem menuItem;

			if (useRadioStyle)
			{
				menuItem = new RadioMenuItem(guiWidget, theme)
				{
					Name = name + " Menu Item",
					Checked = isChecked,
					SiblingRadioButtonList = siblingRadioButtonList
				};
			}
			else
			{
				menuItem = new CheckboxMenuItem(guiWidget, theme)
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

		public MenuItem CreateMenuItem(GuiWidget guiWidget, string name, ImageBuffer icon = null)
		{
			var menuItem = new MenuItem(guiWidget, theme)
			{
				Text = name,
				Name = name + " Menu Item",
				Image = icon
			};

			this.AddChild(menuItem);

			return menuItem;
		}

		public bool KeepMenuOpen => false;

		public class MenuItem : SimpleButton
		{
			private GuiWidget content;

			public MenuItem(GuiWidget content, ThemeConfig theme)
				: base (theme)
			{
				// Inflate padding to match the target (MenuGutterWidth) after scale operation in assignment
				this.Padding = new BorderDouble(left: Math.Ceiling(theme.MenuGutterWidth / GuiWidget.DeviceScale) , right: 15);
				this.HAnchor = HAnchor.MaxFitOrStretch;
				this.VAnchor = VAnchor.Fit;
				this.MinimumSize = new Vector2(150 * GuiWidget.DeviceScale, theme.ButtonHeight);
				this.content = content;
				this.GutterWidth = theme.MenuGutterWidth;
				this.HoverColor = theme.AccentMimimalOverlay;

				content.VAnchor = VAnchor.Center;
				content.HAnchor |= HAnchor.Left;

				this.AddChild(content);
			}

			public double GutterWidth { get; set; }

			public ImageBuffer Image { get; set; }

			private ImageBuffer _disabledImage;
			public ImageBuffer DisabledImage
			{
				get
				{
					// Lazy construct on first access
					if (this.Image != null &&
						_disabledImage == null)
					{
						_disabledImage = this.Image.AjustAlpha(0.2);
					}

					return _disabledImage;
				}
			}

			public override bool Enabled
			{
				get => base.Enabled;
				set
				{
					if (content is TextWidget textWidget)
					{
						textWidget.Enabled = value;
					}

					base.Enabled = value;
				}
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				if (this.Image != null)
				{
					var x = this.LocalBounds.Left + (this.GutterWidth / 2 - this.Image.Width / 2);
					var y = this.Size.Y / 2 - this.Image.Height / 2;

					graphics2D.Render((this.Enabled) ? this.Image : this.DisabledImage, (int)x, (int)y);
				}

				base.OnDraw(graphics2D);
			}
		}
	}
}