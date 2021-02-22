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

using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class ListField : UIField
	{
		private DropDownList dropdownList;
		private ThemeConfig theme;

		public List<(string key, string value)> Items { get; set; } = new List<(string key, string value)>();

		public ListField(ThemeConfig theme)
		{
			this.theme = theme;
		}

		int GetKeyIndex(string key)
		{
			int i = 0;
			foreach (var item in Items)
			{
				if (item.key == key)
				{
					return i;
				}
				i++;
			}

			return 0;
		}

		int GetValueIndex(string value)
		{
			int i = 0;
			foreach (var item in Items)
			{
				if (item.value == value)
				{
					return i;
				}
				i++;
			}

			return 0;
		}

		public override void Initialize(int tabIndex)
		{
			dropdownList = new MHDropDownList("None".Localize(), theme, maxHeight: 200 * GuiWidget.DeviceScale)
			{
				ToolTipText = this.HelpText,
				TabIndex = tabIndex,
				Margin = new BorderDouble(),
			};

			foreach (var item in this.Items)
			{
				MenuItem newItem = dropdownList.AddItem(item.value);

				newItem.Selected += (sender, e) =>
				{
					if (sender is MenuItem menuItem)
					{
						this.SetValue(Items[GetValueIndex(menuItem.Text)].key, userInitiated: true);
					}
				};
			}

			dropdownList.SelectedIndex = GetKeyIndex(this.Value);

			this.Content = dropdownList;
		}

		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			dropdownList.SelectedIndex = GetKeyIndex(this.Value);
			base.OnValueChanged(fieldChangedEventArgs);
		}
	}
}
