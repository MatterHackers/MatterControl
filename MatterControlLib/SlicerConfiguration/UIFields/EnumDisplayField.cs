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
			// Enum keyed on name to friendly name
			var enumItems = Enum.GetNames(property.PropertyType).Select(enumName => (enumName, enumName.Replace('_', ' ').Trim()));

			string GetDescription(Enum value)
			{
				if (GetAttribute<DescriptionAttribute>(value) is DescriptionAttribute attr)
				{
					return attr.Description.Trim();
				}

				return null;
			}

			var enumDescriptions = new List<string>();
			foreach (var value in Enum.GetValues(property.PropertyType))
			{
				enumDescriptions.Add(GetDescription((Enum)value));
			}

			switch (enumDisplayAttibute.Mode)
			{
				case EnumDisplayAttribute.PresentationMode.IconRow:
					AddIconRow(enumItems, enumDescriptions);
					break;

				case EnumDisplayAttribute.PresentationMode.Tabs:
					AddTabs(enumItems, enumDescriptions);
					break;

				case EnumDisplayAttribute.PresentationMode.Buttons:
					AddButtons(enumItems, enumDescriptions);
					break;

				default:
					throw new NotImplementedException();
			}
		}

		private void AddTabs(IEnumerable<(string Key, string Value)> enumItems, List<string> descriptions)
		{
			var menuRow = new FlowLayoutWidget()
			{
				Margin = 5
			};

			int index = 0;
			foreach (var enumItem in enumItems)
			{
				var localIndex = index;

				var radioButton = new RadioTextButton(enumItem.Value, theme)
				{
					ToolTipText = descriptions[index]
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
				if (enumItem.Key == this.InitialValue)
				{
					radioButton.Checked = true;
				}

				menuRow.AddChild(radioButton);

				var localItem = enumItem;
				radioButton.CheckedStateChanged += (s, e) =>
				{
					if (radioButton.Checked)
					{
						this.SetValue(localItem.Key, true);
					}
				};

				index++;
			}

			this.Content = menuRow;
		}

		private void AddButtons(IEnumerable<(string Key, string Value)> enumItems, List<string> descriptions)
		{
			var menuRow = new FlowLayoutWidget();

			int index = 0;
			foreach (var enumItem in enumItems)
			{
				var localIndex = index;

				var radioButton = new RadioTextButton(enumItem.Value, theme)
				{
					VAnchor = VAnchor.Center | VAnchor.Fit,
					DrawUnderline = false,
					BackgroundRadius = theme.ButtonRadius + 4,
					Margin = new BorderDouble(5, 0, 0, 0),
					Padding = new BorderDouble(9, 5),
					// BackgroundInset = new BorderDouble(5, 4),
					SelectedBackgroundColor = theme.PrimaryAccentColor,
					UnselectedBackgroundColor = theme.MinimalShade,
					BackgroundColor = theme.MinimalShade,
					ToolTipText = descriptions[index]
				};

				radioButton.CheckedStateChanged += (s, e) =>
				{
					var button = s as RadioTextButton;
					button.TextColor = button.Checked ? theme.BackgroundColor : theme.TextColor;
				};

				// set it if checked
				if (enumItem.Key == this.InitialValue)
				{
					radioButton.Checked = true;
				}

				menuRow.AddChild(radioButton);

				var localItem = enumItem;
				radioButton.CheckedStateChanged += (s, e) =>
				{
					if (radioButton.Checked)
					{
						this.SetValue(localItem.Key, true);
					}
				};

				index++;
			}

			this.Content = menuRow;
		}

		private void AddIconRow(IEnumerable<(string Key, string Value)> enumItems, List<string> descriptions)
		{
			var iconsRow = new FlowLayoutWidget();

			int index = 0;
			var radioButtonSize = new Vector2(enumDisplayAttibute.IconWidth, enumDisplayAttibute.IconHeight);
			foreach (var enumItem in enumItems)
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
						ToolTipText = descriptions[index] == null ? enumItem.Key : descriptions[index],
					};

					radioButtonSize = new Vector2(radioButton.Width, radioButton.Height);

					// set it if checked
					if (enumItem.Key == this.InitialValue)
					{
						radioButton.Checked = true;
					}

					iconsRow.AddChild(radioButton);

					var localItem = enumItem;
					radioButton.CheckedStateChanged += (s, e) =>
					{
						if (radioButton.Checked)
						{
							this.SetValue(localItem.Key, true);
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
}
