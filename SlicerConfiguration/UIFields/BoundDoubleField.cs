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

using MatterHackers.Agg;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class BoundDoubleField : TextField
	{
		private const string ValuesDifferToken = "-";

		private bool ChangesMultipleOtherSettings;

		private SliceSettingData settingData;

		private SettingsContext settingsContext;

		public BoundDoubleField(SettingsContext settingsContext, SliceSettingData settingData)
		{
			this.settingsContext = settingsContext;
			this.settingData = settingData;
		}

		public override void Initialize(int tabIndex)
		{
			base.Initialize(tabIndex);
			this.textEditWidget.BackgroundColor = Color.Pink;
			ChangesMultipleOtherSettings = settingData.SetSettingsOnChange.Count > 0;
		}

		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			if (fieldChangedEventArgs.UserInitiated)
			{
				// If this setting sets other settings, then do that.
				if (ChangesMultipleOtherSettings)
				{
					// Iterate over each bound setting pushing the current value into each
					for (int i = 0; i < settingData.SetSettingsOnChange.Count; i++)
					{
						string slicerConfigName = settingData.SetSettingsOnChange[i]["TargetSetting"];
						settingsContext.SetValue(slicerConfigName, this.Value);
					}
				}

				// also always save to the local setting
				settingsContext.SetValue(settingData.SlicerConfigName, this.Value);
			}
			else
			{
				// Otherwise simply show the new value
				textEditWidget.ActualTextEditWidget.Text = FilterValue(this.Value);
			}

			base.OnValueChanged(fieldChangedEventArgs);
		}

		/// <summary>
		/// Overrides the current value to display the ValuesDifferToken if they are not all equal
		/// </summary>
		/// <param name="currentValue"></param>
		/// <returns>The current value cast to double or the ValuesDifferToken (-)</returns>
		private string FilterValue(string currentValue)
		{
			string text = currentValue.Trim();

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
					double.TryParse(setting.Substring(0, setting.Length - 2), out double castValue);
					return castValue.ToString();
				}
				else
				{
					return ValuesDifferToken;
				}
			}
			else // just set the setting normally
			{
				double.TryParse(this.Value, out double castValue);
				return castValue.ToString();
			}
		}
	}
}
