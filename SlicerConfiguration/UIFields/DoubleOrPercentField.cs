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
	public class DoubleOrPercentField : ISettingsField
	{
		public Action UpdateStyle { get; set; }

		private MHTextEditWidget editWidget;

		public string Value { get; set; }

		public GuiWidget Create(SettingsContext settingsContext, SliceSettingData settingData, int tabIndex)
		{
			editWidget = new MHTextEditWidget(this.Value, pixelWidth: DoubleField.DoubleEditWidth - 2, tabIndex: tabIndex)
			{
				ToolTipText = settingData.HelpText,
				SelectAllOnFocus = true
			};
			editWidget.ActualTextEditWidget.EditComplete += (sender, e) =>
			{
				var textEditWidget = (TextEditWidget)sender;
				string text = textEditWidget.Text.Trim();

				bool isPercent = text.Contains("%");
				if (isPercent)
				{
					text = text.Substring(0, text.IndexOf("%"));
				}
				double result;
				double.TryParse(text, out result);
				text = result.ToString();
				if (isPercent)
				{
					text += "%";
				}
				textEditWidget.Text = text;
				settingsContext.SetValue(settingData.SlicerConfigName, textEditWidget.Text);

				this.UpdateStyle();
			};

			editWidget.ActualTextEditWidget.InternalTextEditWidget.AllSelected += (sender, e) =>
			{
				// select everything up to the % (if present)
				InternalTextEditWidget textEditWidget = (InternalTextEditWidget)sender;
				int percentIndex = textEditWidget.Text.IndexOf("%");
				if (percentIndex != -1)
				{
					textEditWidget.SetSelection(0, percentIndex - 1);
				}
			};

			if (settingData.QuickMenuSettings.Count > 0)
			{
				return SliceSettingsWidget.CreateQuickMenu(settingData, settingsContext, editWidget, editWidget.ActualTextEditWidget.InternalTextEditWidget);
			}
			else
			{
				return editWidget;
			}
		}

		public void OnValueChanged(string text)
		{
			editWidget.Text = text;
		}
	}
}
