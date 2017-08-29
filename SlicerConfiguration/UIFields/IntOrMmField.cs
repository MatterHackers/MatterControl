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
	public class IntOrMmField : ISettingsField
	{
		private MHTextEditWidget editWidget;

		public Action UpdateStyle { get; set; }

		public string Value { get; set; }

		public GuiWidget Create(SettingsContext settingsContext, SliceSettingData settingData, int tabIndex)
		{
			editWidget = new MHTextEditWidget(this.Value, pixelWidth: DoubleField.DoubleEditWidth - 2, tabIndex: tabIndex)
			{
				ToolTipText = settingData.HelpText,
				SelectAllOnFocus = true
			};

			string startingText = editWidget.Text;
			editWidget.ActualTextEditWidget.EditComplete += (sender, e) =>
			{
				TextEditWidget textEditWidget = (TextEditWidget)sender;
				// only validate when we lose focus
				if (!textEditWidget.ContainsFocus)
				{
					string text = textEditWidget.Text;
					text = text.Trim();
					bool isMm = text.Contains("mm");
					if (isMm)
					{
						text = text.Substring(0, text.IndexOf("mm"));
					}
					double result;
					double.TryParse(text, out result);
					text = result.ToString();
					if (isMm)
					{
						text += "mm";
					}
					else
					{
						result = (int)result;
						text = result.ToString();
					}
					textEditWidget.Text = text;
					startingText = editWidget.Text;
				}

				settingsContext.SetValue(settingData.SlicerConfigName, textEditWidget.Text);
				this.UpdateStyle();

				// make sure we are still looking for the final validation before saving.
				if (textEditWidget.ContainsFocus)
				{
					UiThread.RunOnIdle(() =>
					{
						string currentText = textEditWidget.Text;
						int cursorIndex = textEditWidget.InternalTextEditWidget.CharIndexToInsertBefore;
						textEditWidget.Text = startingText;
						textEditWidget.InternalTextEditWidget.MarkAsStartingState();
						textEditWidget.Text = currentText;
						textEditWidget.InternalTextEditWidget.CharIndexToInsertBefore = cursorIndex;
					});
				}
			};

			editWidget.ActualTextEditWidget.InternalTextEditWidget.AllSelected += (sender, e) =>
			{
				// select everything up to the mm (if present)
				InternalTextEditWidget textEditWidget = (InternalTextEditWidget)sender;
				int mMIndex = textEditWidget.Text.IndexOf("mm");
				if (mMIndex != -1)
				{
					textEditWidget.SetSelection(0, mMIndex - 1);
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
