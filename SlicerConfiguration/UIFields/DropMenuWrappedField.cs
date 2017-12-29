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
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class DropMenuWrappedField
	{
		private UIField uiField;
		private Color textColor;
		private SliceSettingData settingData;

		public DropMenuWrappedField(UIField uiField, SliceSettingData settingData, Color textColor)
		{
			this.settingData = settingData;
			this.uiField = uiField;
			this.textColor = textColor;
		}

		public void SetValue(string newValue, bool userInitiated)
		{
			uiField.SetValue(newValue, userInitiated);
		}

		public string Value { get => uiField.Value; }

		public GuiWidget Content { get; private set; }

		public event EventHandler<FieldChangedEventArgs> ValueChanged;

		public void Initialize(int tabIndex)
		{
			var totalContent = new FlowLayoutWidget();

			var selectableOptions = new DropDownList("Custom", textColor, maxHeight: 200);
			selectableOptions.Margin = new BorderDouble(0, 0, 10, 0);

			foreach (QuickMenuNameValue nameValue in settingData.QuickMenuSettings)
			{
				string valueLocal = nameValue.Value;

				MenuItem newItem = selectableOptions.AddItem(nameValue.MenuName);
				if (uiField.Value == valueLocal)
				{
					selectableOptions.SelectedLabel = nameValue.MenuName;
				}

				newItem.Selected += (s, e) =>
				{
					uiField.SetValue(valueLocal, userInitiated: true);
				};
			}

			totalContent.AddChild(selectableOptions);

			uiField.Content.VAnchor = VAnchor.Center;
			totalContent.AddChild(uiField.Content);

			EventHandler localUnregisterEvents = null;

			ActiveSliceSettings.SettingChanged.RegisterEvent((sender, e) =>
			{
				if (e is StringEventArgs stringArgs
					&& stringArgs.Data == settingData.SlicerConfigName)
				{
					bool foundSetting = false;
					foreach (QuickMenuNameValue nameValue in settingData.QuickMenuSettings)
					{
						string localName = nameValue.MenuName;
						string newSliceSettingValue = uiField.Value;
						if (newSliceSettingValue == nameValue.Value)
						{
							selectableOptions.SelectedLabel = localName;
							foundSetting = true;
							break;
						}
					}

					if (!foundSetting)
					{
						selectableOptions.SelectedLabel = "Custom";
					}
				}
			}, ref localUnregisterEvents);

			totalContent.Closed += (s, e) =>
			{
				localUnregisterEvents?.Invoke(s, null);
			};

			this.Content = totalContent;
		}
	}
}
