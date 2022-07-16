/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.ComponentModel;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class EnumDisplayField : UIField
	{
		private readonly EditableProperty property;
		private readonly EnumDisplayAttribute enumDisplayAttibute;
		private readonly ThemeConfig theme;

		public EnumDisplayField(EditableProperty property, EnumDisplayAttribute iconsAttribute, ThemeConfig theme)
		{
			this.property = property;
			this.enumDisplayAttibute = iconsAttribute;
			this.theme = theme;
		}

		// TODO: Violates UIField norms but consistent with past behavior - state is only correct at construction time, often reconstructed
		public string InitialValue { get; set; }

		T GetAttribute<T>(Enum value)
			where T : Attribute
		{
			var type = value.GetType();
			var name = Enum.GetName(type, value);
			return type.GetField(name) // I prefer to get attributes this way
				.GetCustomAttributes(false)
				.OfType<T>()
				.SingleOrDefault();
		}

		public override void Initialize(int tabIndex)
		{
			(string key, string name) GetKeyName(Enum value)
			{
				var name = value.ToString();

				if (GetAttribute<EnumNameAttribute>(value) is EnumNameAttribute attr)
				{
					return (name, attr.Name);
				}

				return (name, name.Replace('_', ' ').Trim());
			}

			string GetDescription(Enum value)
			{
				if (GetAttribute<DescriptionAttribute>(value) is DescriptionAttribute attr)
				{
					return attr.Description.Trim();
				}

				return null;
			}

			var enumDescriptions = new List<(string key, string name, string description)>();
			foreach (var value in Enum.GetValues(property.PropertyType))
			{
				var keyName = GetKeyName((Enum)value);
				enumDescriptions.Add((keyName.key, keyName.name, GetDescription((Enum)value)));
			}

			switch (enumDisplayAttibute.Mode)
			{
				case EnumDisplayAttribute.PresentationMode.IconRow:
					AddIconRow(enumDescriptions);
					break;

				case EnumDisplayAttribute.PresentationMode.Tabs:
					AddTabs(enumDescriptions);
					break;

				case EnumDisplayAttribute.PresentationMode.Buttons:
					AddButtons(enumDescriptions);
					break;

				default:
					throw new NotImplementedException();
			}
		}

		private void EnableReduceWidth(RadioTextButton enumTab)
		{
			var deviceScale = GuiWidget.DeviceScale;
			var padingSize = enumTab.Padding.Left * deviceScale;
			enumTab.MinimumSize = new Vector2(padingSize * 3, enumTab.Height);
			enumTab.HAnchor = HAnchor.Stretch;

			// delay this for an update so that the layout of the text widget has happened and its size has been updated.
			var textWidget = enumTab.Descendants<TextWidget>().First();
			textWidget.Margin = new BorderDouble(enumTab.Padding.Left, 0, 0, 0);
			textWidget.HAnchor = HAnchor.Left;

			enumTab.AfterDraw += (s, e) =>
			{
				if (enumTab.Width < enumTab.MaximumSize.X)
				{
					var bounds = enumTab.LocalBounds;
					var g = e.Graphics2D;
					var color = enumTab.SelectedBackgroundColor;
					if (!enumTab.Checked)
					{
						foreach (var parent in enumTab.Parents<GuiWidget>())
						{
							if (parent.BackgroundColor.alpha > 200)
							{
								color = parent.BackgroundColor;
								break;
							}
						}
					}
					// cover the text with an alpha mask
					for (int i = 0; i < padingSize + 1; i++)
					{
						var x = bounds.Right - padingSize + i;
						g.Line(x, bounds.Bottom, x, bounds.Top, color.WithAlpha(Math.Min(255, i / 10.0 * deviceScale)));
					}
				}
			};

			// the text
			var maxWidth = textWidget.Width + enumTab.Padding.Width * deviceScale;
			enumTab.MaximumSize = new Vector2(maxWidth, enumTab.MaximumSize.Y);
			enumTab.Padding = new BorderDouble(0, enumTab.Padding.Bottom, 0, enumTab.Padding.Top);

			if (string.IsNullOrEmpty(enumTab.ToolTipText))
			{
				// wait for this size change to take effect and update the tool tip
				enumTab.BoundsChanged += (s, e) =>
				{
					if (enumTab.Width < enumTab.MaximumSize.X)
					{
						enumTab.ToolTipText = textWidget.Text;
					}
					else
					{
						enumTab.ToolTipText = "";
					}
				};
			}

			enumTab.HAnchor = HAnchor.Stretch;
		}

		private void AddTabs(List<(string key, string name, string description)> items)
		{
			var menuRow = new FlowLayoutWidget()
			{
				Margin = 5,
			};

			int index = 0;
			foreach (var item in items)
			{
				var localIndex = index;

				var radioButton = new RadioTextButton(item.name, theme)
				{
					ToolTipText = item.description,
				};

				menuRow.AfterDraw += (s, e) =>
				{
					e.Graphics2D.Rectangle(menuRow.LocalBounds.Left, 0, menuRow.LocalBounds.Right, 2, theme.PrimaryAccentColor);
				};

				radioButton.CheckedStateChanged += (s, e) =>
				{
					var button = s as RadioTextButton;
					button.TextColor = button.Checked ? theme.BackgroundColor : theme.TextColor;
				};

				radioButton.SelectedBackgroundColor = theme.PrimaryAccentColor;

				// set it if checked
				if (item.key == this.InitialValue)
				{
					radioButton.Checked = true;
				}

				EnableReduceWidth(radioButton);

				menuRow.AddChild(radioButton);

				var localItem = item;
				radioButton.CheckedStateChanged += (s, e) =>
				{
					if (radioButton.Checked)
					{
						this.SetValue(localItem.key, true);
					}
				};

				index++;
			}

			this.Content = menuRow;
		}

		private void AddButtons(List<(string key, string name, string description)> items)
		{
			var menuRow = new FlowLayoutWidget();

			int index = 0;
			foreach (var item in items)
			{
				var localIndex = index;

				var radioButton = CreateThemedRadioButton(item.name, item.key, item.description, this.InitialValue == item.key, () => this.SetValue(item.key, true), theme);

				menuRow.AddChild(radioButton);

				var localItem = item;

				index++;
			}

			this.Content = menuRow;
		}

		public static RadioTextButton CreateThemedRadioButton(string text, string key, string toolTipText, bool startChecked, Action setChecked, ThemeConfig theme)
		{
			var radioButton = new RadioTextButton(text, theme)
			{
				VAnchor = VAnchor.Center | VAnchor.Fit,
				DrawUnderline = false,
				BackgroundRadius = (theme.ButtonRadius + 4) * GuiWidget.DeviceScale,
				Margin = new BorderDouble(5, 0, 0, 0),
				Padding = new BorderDouble(9, 5),
				// BackgroundInset = new BorderDouble(5, 4),
				SelectedBackgroundColor = theme.PrimaryAccentColor,
				UnselectedBackgroundColor = theme.MinimalShade,
				BackgroundColor = theme.MinimalShade,
				ToolTipText = toolTipText
			};

			radioButton.CheckedStateChanged += (s, e) =>
			{
				var button = s as RadioTextButton;
				button.TextColor = button.Checked ? theme.BackgroundColor : theme.TextColor;
			};

			// set it if checked
			radioButton.Checked = startChecked;

			radioButton.CheckedStateChanged += (s, e) =>
			{
				if (radioButton.Checked)
				{
					setChecked?.Invoke();
				}
			};

			return radioButton;
		}

		public static TextButton CreateThemedButton(string text, string key, string toolTipText, ThemeConfig theme)
		{
			var button = new TextButton(text, theme)
			{
				VAnchor = VAnchor.Center | VAnchor.Fit,
				BackgroundRadius = (theme.ButtonRadius + 4) * GuiWidget.DeviceScale,
				Margin = new BorderDouble(5, 0, 0, 0),
				Padding = new BorderDouble(9, 5),
				// BackgroundInset = new BorderDouble(5, 4),
				BackgroundColor = theme.MinimalShade,
				ToolTipText = toolTipText
			};

			return button;
		}

		private void AddIconRow(List<(string key, string name, string description)> items)
		{
			var iconsRow = new FlowLayoutWidget();

			int index = 0;
			var radioButtonSize = new Vector2(enumDisplayAttibute.IconWidth, enumDisplayAttibute.IconHeight);
			foreach (var item in items)
			{
				var localIndex = index;
				ImageBuffer iconImage = null;
				var iconPath = enumDisplayAttibute.IconPaths[localIndex];
				if (!string.IsNullOrWhiteSpace(iconPath))
				{
					if (enumDisplayAttibute.IconWidth > 0)
					{
						// If the attribute allows invert, use the theme.InvertIcons state
						iconImage = StaticData.Instance.LoadIcon(iconPath, enumDisplayAttibute.IconWidth, enumDisplayAttibute.IconHeight).SetToColor(theme.TextColor);
					}
					else
					{
						iconImage = StaticData.Instance.LoadIcon(iconPath);
					}

					var radioButton = new RadioIconButton(iconImage, theme)
					{
						ToolTipText = item.description == null ? item.name : item.description,
					};

					radioButtonSize = new Vector2(radioButton.Width, radioButton.Height);

					// set it if checked
					if (item.key == this.InitialValue)
					{
						radioButton.Checked = true;
					}

					iconsRow.AddChild(radioButton);

					var localItem = item;
					radioButton.CheckedStateChanged += (s, e) =>
					{
						if (radioButton.Checked)
						{
							this.SetValue(localItem.key, true);
						}
					};
				}
				else if (enumDisplayAttibute.IconWidth > 0)
				{
					// hold the space of the empty icon
					iconsRow.AddChild(new GuiWidget(radioButtonSize.X, radioButtonSize.Y));
				}

				index++;
			}

			this.Content = iconsRow;
		}

		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			//dropdownList.SelectedLabel = this.Value;
			base.OnValueChanged(fieldChangedEventArgs);
		}
	}

    public static class MenuExtensions
    {
		public static MenuItem CreateButtonSelectMenuItem(this PopupMenu popuMenu, GuiWidget guiWidget, string name, IEnumerable<(string key, string text)> buttonKvps, string startingValue, Action<string> setter, double minSpacerWidth = 0)
		{
			var row = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.MaxFitOrStretch,
				Name = name + " Menu Item",
			};

			row.AddChild(guiWidget);
			row.AddChild(new HorizontalSpacer()
			{
				MinimumSize = new Vector2(minSpacerWidth, 0)
			}); ;

			foreach (var buttonKvp in buttonKvps)
			{
				var localKey = buttonKvp.key;
				var button = EnumDisplayField.CreateThemedRadioButton(buttonKvp.text, buttonKvp.key, "", startingValue == buttonKvp.key, () =>
				{
					setter?.Invoke(localKey);
				}, popuMenu.Theme);
				row.AddChild(button);
			}

			MenuItem menuItem = new MenuItemHoldOpen(row);

			popuMenu.AddChild(menuItem);

			return menuItem;
		}

		public static MenuItem CreateButtonMenuItem(this PopupMenu popupMenu, GuiWidget guiWidget, string name, IEnumerable<(string key, string text, EventHandler<MouseEventArgs> click)> buttonKvps, double minSpacerWidth = 0)
		{
			var row = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.MaxFitOrStretch,
				Name = name + " Menu Item",
			};

			row.AddChild(guiWidget);
			row.AddChild(new HorizontalSpacer()
			{
				MinimumSize = new Vector2(minSpacerWidth, 0)
			});

			foreach (var buttonKvp in buttonKvps)
			{
				var button = EnumDisplayField.CreateThemedButton(buttonKvp.text, buttonKvp.key, "", popupMenu.Theme);
				button.Click += buttonKvp.click;
				row.AddChild(button);
			}

			var menuItem = new MenuItemHoldOpen(row)
			{
			};

			popupMenu.AddChild(menuItem);

			return menuItem;
		}

		/// <summary>
		/// Create and add a new menu item
		/// </summary>
		/// <param name="text">The text of the item</param>
		/// <param name="items"></param>
		/// <param name="getter"></param>
		/// <param name="setter"></param>
		/// <returns></returns>
		public static MenuItem CreateButtonSelectMenuItem(this PopupMenu popupMenu, string text, IEnumerable<(string key, string text)> buttonKvps, string startingValue, Action<string> setter, double minSpacerWidth = 0)
		{
			var textWidget = new TextWidget(text, pointSize: popupMenu.Theme.DefaultFontSize, textColor: popupMenu.Theme.TextColor)
			{
				Padding = PopupMenu.MenuPadding,
				VAnchor = VAnchor.Center,
			};

			return popupMenu.CreateButtonSelectMenuItem(textWidget, text, buttonKvps, startingValue, setter, minSpacerWidth);
		}

		public static MenuItem CreateButtonMenuItem(this PopupMenu popupMenu,
			string text,
			IEnumerable<(string key, string text, EventHandler<MouseEventArgs> click)> buttonKvps,
			double minSpacerWidth = 0,
			bool bold = false)
		{
			var textWidget = new TextWidget(text, pointSize: popupMenu.Theme.DefaultFontSize, textColor: popupMenu.Theme.TextColor, bold: bold)
			{
				Padding = PopupMenu.MenuPadding,
				VAnchor = VAnchor.Center,
			};

			return popupMenu.CreateButtonMenuItem(textWidget, text, buttonKvps, minSpacerWidth);
		}

		public static MenuItem CreateButtonSelectMenuItem(this PopupMenu popupMenu, string text, ImageBuffer icon, IEnumerable<(string key, string text)> buttonKvps, string startingValue, Action<string> setter)
		{
			var row = new FlowLayoutWidget()
			{
				Selectable = false
			};
			row.AddChild(new ThemedIconButton(icon, popupMenu.Theme));

			var textWidget = new TextWidget(text, pointSize: popupMenu.Theme.DefaultFontSize, textColor: popupMenu.Theme.TextColor)
			{
				Padding = PopupMenu.MenuPadding,
				VAnchor = VAnchor.Center
			};
			row.AddChild(textWidget);

			return popupMenu.CreateButtonSelectMenuItem(row, text, buttonKvps, startingValue, setter);
		}
	}
}
