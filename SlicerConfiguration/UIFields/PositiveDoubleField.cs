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
	public class PositiveDoubleField : ISettingsField
	{
		private MHNumberEdit doubleEditWidget;

		public Action UpdateStyle { get; set; }

		public string Value { get; set; }

		public GuiWidget Create(SettingsContext settingsContext, SliceSettingData settingData, int tabIndex)
		{
			const string multiValuesAreDiffernt = "-";

			doubleEditWidget = new MHNumberEdit(0, allowDecimals: true, pixelWidth: DoubleField.DoubleEditWidth, tabIndex: tabIndex)
			{
				ToolTipText = settingData.HelpText,
				Name = settingData.PresentationName + " Textbox",
				SelectAllOnFocus = true
			};

			double currentValue;
			bool ChangesMultipleOtherSettings = settingData.SetSettingsOnChange.Count > 0;
			if (ChangesMultipleOtherSettings)
			{
				bool allTheSame = true;
				string setting = settingsContext.GetValue(settingData.SetSettingsOnChange[0]["TargetSetting"]);
				for (int i = 1; i < settingData.SetSettingsOnChange.Count; i++)
				{
					string nextSetting = settingsContext.GetValue(settingData.SetSettingsOnChange[i]["TargetSetting"]);
					if (setting != nextSetting)
					{
						allTheSame = false;
						break;
					}
				}

				if (allTheSame && setting.EndsWith("mm"))
				{
					double.TryParse(setting.Substring(0, setting.Length - 2), out currentValue);
					doubleEditWidget.ActuallNumberEdit.Value = currentValue;
				}
				else
				{
					doubleEditWidget.ActuallNumberEdit.InternalNumberEdit.Text = multiValuesAreDiffernt;
				}
			}
			else // just set the setting normally
			{
				double.TryParse(this.Value, out currentValue);
				doubleEditWidget.ActuallNumberEdit.Value = currentValue;
			}
			doubleEditWidget.ActuallNumberEdit.InternalTextEditWidget.MarkAsStartingState();

			doubleEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
			{
				NumberEdit numberEdit = (NumberEdit)sender;
				// If this setting sets other settings, then do that.
				if (ChangesMultipleOtherSettings
					&& numberEdit.Text != multiValuesAreDiffernt)
				{
					{
						settingsContext.SetValue(settingData.SetSettingsOnChange[0]["TargetSetting"], numberEdit.Value.ToString() + "mm");
					}
				}

				// also always save to the local setting
				settingsContext.SetValue(settingData.SlicerConfigName, numberEdit.Value.ToString());
				this.UpdateStyle();
			};
			
			if (settingData.QuickMenuSettings.Count > 0)
			{
				return SliceSettingsWidget.CreateQuickMenu(settingData, settingsContext, doubleEditWidget, doubleEditWidget.ActuallNumberEdit.InternalTextEditWidget);
			}
			else
			{
				return doubleEditWidget;
			}
		}

		public void OnValueChanged(string text)
		{
			double.TryParse(text, out double currentValue);
			doubleEditWidget.ActuallNumberEdit.Value = currentValue;
		}
	}
}
