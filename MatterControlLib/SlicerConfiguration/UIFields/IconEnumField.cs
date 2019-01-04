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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class IconEnumField : UIField
	{
		private EditableProperty property;
		private IconsAttribute iconsAttribute;
		private ThemeConfig theme;

		public IconEnumField(EditableProperty property, IconsAttribute iconsAttribute, ThemeConfig theme)
		{
			this.property = property;
			this.iconsAttribute = iconsAttribute;
			this.theme = theme;
		}

		// TODO: Violates UIField norms but consistent with past behavior - state is only correct at construction time, often reconstructed
		public string InitialValue { get; set; }

		public override void Initialize(int tabIndex)
		{
			// Enum keyed on name to friendly name
			var enumItems = Enum.GetNames(property.PropertyType).Select(enumName =>
			{
				return new
				{
					Key = enumName,
					Value = enumName.Replace('_', ' ')
				};
			});

			var iconsRow = new FlowLayoutWidget();

			int index = 0;
			var radioButtonSize = new Vector2(iconsAttribute.Width, iconsAttribute.Height);
			foreach (var enumItem in enumItems)
			{
				var localIndex = index;
				ImageBuffer iconImage = null;
				var iconPath = iconsAttribute.IconPaths[localIndex];
				if (!string.IsNullOrWhiteSpace(iconPath))
				{
					if (iconsAttribute.Width > 0)
					{
						// If the attribute allows invert, use the theme.InvertIcons state
						bool invertIcons = iconsAttribute.InvertIcons ? theme.InvertIcons : false;

						iconImage = AggContext.StaticData.LoadIcon(iconPath, iconsAttribute.Width, iconsAttribute.Height, invertIcons);
					}
					else
					{
						iconImage = AggContext.StaticData.LoadIcon(iconPath);
					}

					var radioButton = new RadioIconButton(iconImage, theme)
					{
						ToolTipText = enumItem.Key
					};

					radioButtonSize = new Vector2(radioButton.Width, radioButton.Height);

					// set it if checked
					if (enumItem.Value == this.InitialValue)
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
				else if(iconsAttribute.Width > 0)
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
