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
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class CheckboxField : ISettingsField
	{
		private CheckBox checkBoxWidget;

		public Action UpdateStyle { get; set; }

		public string Value { get; set; }

		public GuiWidget Create(SettingsContext settingsContext, SliceSettingData settingData, int tabIndex)
		{
			checkBoxWidget = new CheckBox("")
			{
				Name = settingData.PresentationName + " Checkbox",
				ToolTipText = settingData.HelpText,
				VAnchor = VAnchor.Bottom,
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Checked = this.Value == "1"
			};
			checkBoxWidget.Click += (sender, e) =>
			{
				// SetValue should only be called when the checkbox is clicked. If this code makes its way into checkstatechanged
				// we end up adding a key back into the dictionary after we call .ClearValue, resulting in the blue override bar reappearing after
				// clearing a user override with the red x
				settingsContext.SetValue(settingData.SlicerConfigName, checkBoxWidget.Checked ? "1" : "0");
			};
			checkBoxWidget.CheckedStateChanged += (s, e) =>
			{
				// Linked settings should be updated in all cases (user clicked checkbox, user clicked clear)
				foreach (var setSettingsData in settingData.SetSettingsOnChange)
				{
					string targetValue;
					if (setSettingsData.TryGetValue(checkBoxWidget.Checked ? "OnValue" : "OffValue", out targetValue))
					{
						settingsContext.SetValue(setSettingsData["TargetSetting"], targetValue);
					}
				}

				this.UpdateStyle();
			};

			return checkBoxWidget;
		}

		public void OnValueChanged(string text)
		{
			checkBoxWidget.Checked = text == "1";
		}
	}
}
