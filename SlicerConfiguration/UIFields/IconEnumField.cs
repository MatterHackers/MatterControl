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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class IconEnumField : UIField
	{
		private PPEContext context;
		private EditableProperty property;
		private UndoBuffer undoBuffer;
		private IconsAttribute iconsAttribute;

		public IconEnumField(PPEContext context, EditableProperty property, UndoBuffer undoBuffer, IconsAttribute iconsAttribute)
		{
			this.context = context;
			this.property = property;
			this.undoBuffer = undoBuffer;
			this.iconsAttribute = iconsAttribute;
		}

		public override void Initialize(int tabIndex)
		{
			var theme = ApplicationController.Instance.Theme;

			// Cast to optional types
			//var item = property.Item as IPublicPropertyObject;
			//var propertyGridModifier = property.Item as IPropertyGridModifier;

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
			foreach (var enumItem in enumItems)
			{
				var localIndex = index;
				ImageBuffer iconImage = null;
				var iconPath = iconsAttribute.IconPaths[localIndex];
				if (!string.IsNullOrWhiteSpace(iconPath))
				{
					if (iconsAttribute.Width > 0)
					{
						iconImage = AggContext.StaticData.LoadIcon(iconPath, iconsAttribute.Width, iconsAttribute.Height);
					}
					else
					{
						iconImage = AggContext.StaticData.LoadIcon(iconPath);
					}

					var radioButton = new RadioButton(new ImageWidget(iconImage))
					{
						ToolTipText = enumItem.Key
					};

					// set it if checked
					if (enumItem.Value == property.DisplayName)
					{
						radioButton.Checked = true;
						if (localIndex != 0
							|| !iconsAttribute.Item0IsNone)
						{
							radioButton.BackgroundColor = new Color(Color.Black, 100);
						}
					}

					iconsRow.AddChild(radioButton);

					var localItem = enumItem;
					radioButton.CheckedStateChanged += (s, e) =>
					{
						if (radioButton.Checked)
						{
							this.SetValue(localItem.Key, true);

							if (localIndex != 0
								|| !iconsAttribute.Item0IsNone)
							{
								radioButton.BackgroundColor = new Color(Color.Black, 100);
							}
						}
						else
						{
							radioButton.BackgroundColor = Color.Transparent;
						}
					};
					index++;
				}
			}

			this.Content = iconsRow;
		}

		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			//dropdownList.SelectedLabel = this.Value;
			base.OnValueChanged(fieldChangedEventArgs);
		}

		//private static GuiWidget CreateEnumEditor(PPEContext context, EditableProperty property, ThemeConfig theme, UndoBuffer undoBuffer)
		//{
		//	{

		//	}
		//	else
		//	{
		//		var dropDownList = new DropDownList("Name".Localize(), theme.Colors.PrimaryTextColor, Direction.Down, pointSize: theme.DefaultFontSize)
		//		{
		//			BorderColor = theme.GetBorderColor(75)
		//		};

		//		var sortableAttribute = property.PropertyInfo.GetCustomAttributes(true).OfType<SortableAttribute>().FirstOrDefault();
		//		var orderedItems = sortableAttribute != null ? enumItems.OrderBy(n => n.Value) : enumItems;

		//		foreach (var orderItem in orderedItems)
		//		{
		//			MenuItem newItem = dropDownList.AddItem(orderItem.Value);

		//			var localOrderedItem = orderItem;
		//			newItem.Selected += (sender, e) =>
		//			{
		//				property.PropertyInfo.GetSetMethod().Invoke(
		//					property.Item,
		//					new Object[] { Enum.Parse(property.PropertyType, localOrderedItem.Key) });
		//				item?.Rebuild(undoBuffer);
		//				propertyGridModifier?.UpdateControls(context);
		//			};
		//		}

		//		dropDownList.SelectedLabel = property.Value.ToString().Replace('_', ' ');
		//		rowContainer.AddChild(dropDownList);
		//	}

		//	return rowContainer;
		//}
	}
}
