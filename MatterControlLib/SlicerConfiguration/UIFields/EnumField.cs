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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class EnumField : UIField
	{
		private readonly EditableProperty property;
		private readonly ThemeConfig theme;
		private DropDownList dropDownList;

		public EnumField(EditableProperty property, ThemeConfig theme)
		{
			this.property = property;
			this.theme = theme;
		}

		public override void Initialize(int tabIndex)
		{
			// Enum keyed on name to friendly name
			var enumItems = Enum.GetNames(property.PropertyType).Select(enumName =>
			{
				var renamedName = enumName;

				var renameAttribute = property.PropertyInfo.GetCustomAttributes(true).OfType<EnumRenameAttribute>().FirstOrDefault();
				if (renameAttribute != null)
				{
					if (renameAttribute.NameMaping.TryGetValue(renamedName, out string value))
					{
						renamedName = value;
					}
				}

				return new
				{
					Key = enumName,
					Value = renamedName.Replace('_', ' ')
				};
			});

			dropDownList = new MHDropDownList("Name".Localize(), theme)
			{
				Name = property.DisplayName + " DropDownList"
			};

			var sortableAttribute = property.PropertyInfo.GetCustomAttributes(true).OfType<SortableAttribute>().FirstOrDefault();
			var orderedItems = sortableAttribute != null ? enumItems.OrderBy(n => n.Value) : enumItems;

			foreach (var orderItem in orderedItems)
			{
				MenuItem newItem = dropDownList.AddItem(orderItem.Value, orderItem.Key);

				var localOrderedItem = orderItem;
				newItem.Selected += (sender, e) =>
				{
					this.SetValue(localOrderedItem.Key, true);
				};
			}

			dropDownList.SelectedLabel = property.Value.ToString().Replace('_', ' ');

			this.Content = dropDownList;
		}

		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			if (this.Value != dropDownList.SelectedValue)
			{
				dropDownList.SelectedValue = this.Value;
			}

			base.OnValueChanged(fieldChangedEventArgs);
		}
	}
}
