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
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class ChildrenSelectorListField : UIField
	{
		private readonly EditableProperty property;
		private readonly ThemeConfig theme;
		private DropDownList dropDownList;

		public ChildrenSelectorListField(EditableProperty property, ThemeConfig theme)
		{
			this.property = property;
			this.theme = theme;
		}

		public override void Initialize(int tabIndex)
		{
			// Enum keyed on name to friendly name
			List<(string key, string value)> names = null;
			string selectedID = "";
			if (property.Source is AlignObject3D item)
			{
				names = item.Children.Select(child => (child.ID, child.Name)).ToList();
				if (item.SelectedChild.Count == 1)
				{
					selectedID = item.SelectedChild.First();
				}
			}

			dropDownList = new MHDropDownList("Name".Localize(), theme);

			var orderedItems = names.OrderBy(n => n.value);

			foreach (var orderItem in orderedItems)
			{
				MenuItem newItem = dropDownList.AddItem(orderItem.value, orderItem.key);

				var (key, value) = orderItem;
				newItem.Selected += (sender, e) =>
				{
					this.SetValue(key, true);
				};
			}

			if (!string.IsNullOrWhiteSpace(selectedID))
			{
				dropDownList.SelectedValue = selectedID;
			}
			else if (dropDownList.MenuItems.Count > 0)
			{
				dropDownList.SelectedIndex = 0;
			}

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
