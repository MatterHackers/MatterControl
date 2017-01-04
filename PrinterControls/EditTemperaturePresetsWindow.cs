/*
Copyright (c) 2014, Lars Brubaker
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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterControl
{
	public class EditTemperaturePresetsWindow : SystemWindow
	{
		protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		private EventHandler functionToCallOnSave;
		private List<GuiWidget> listWithValues = new List<GuiWidget>();

		public EditTemperaturePresetsWindow(string windowTitle, string temperatureSettings, EventHandler functionToCallOnSave)
			: base(360, 300)
		{
			AlwaysOnTopOfMain = true;
			Title = LocalizedString.Get(windowTitle);

			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			topToBottom.Padding = new BorderDouble(3, 0, 3, 5);

			FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.HAnchor = HAnchor.ParentLeftRight;
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 3, 0, 3);

			{
				string tempShortcutPresetLabel = "Temperature Shortcut Presets".Localize();
				string tempShortcutPresetLabelFull = string.Format("{0}:", tempShortcutPresetLabel);
				TextWidget elementHeader = new TextWidget(tempShortcutPresetLabelFull, pointSize: 14);
				elementHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				elementHeader.HAnchor = HAnchor.ParentLeftRight;
				elementHeader.VAnchor = Agg.UI.VAnchor.ParentBottom;

				headerRow.AddChild(elementHeader);
			}

			topToBottom.AddChild(headerRow);

			FlowLayoutWidget presetsFormContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			//ListBox printerListContainer = new ListBox();
			{
				presetsFormContainer.HAnchor = HAnchor.ParentLeftRight;
				presetsFormContainer.VAnchor = VAnchor.ParentBottomTop;
				presetsFormContainer.Padding = new BorderDouble(3);
				presetsFormContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			}

			topToBottom.AddChild(presetsFormContainer);

			this.functionToCallOnSave = functionToCallOnSave;
			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			double oldHeight = textImageButtonFactory.FixedHeight;
			textImageButtonFactory.FixedHeight = 30 * GuiWidget.DeviceScale;

			TextWidget tempTypeLabel = new TextWidget(windowTitle, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 10);
			tempTypeLabel.Margin = new BorderDouble(3);
			tempTypeLabel.HAnchor = HAnchor.ParentLeft;
			presetsFormContainer.AddChild(tempTypeLabel);

			FlowLayoutWidget leftRightLabels = new FlowLayoutWidget();
			leftRightLabels.Padding = new BorderDouble(3, 6);
			leftRightLabels.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;

			GuiWidget hLabelSpacer = new GuiWidget();
			hLabelSpacer.HAnchor = HAnchor.ParentLeftRight;

			GuiWidget labelLabelContainer = new GuiWidget();
			labelLabelContainer.Width = 66;
			labelLabelContainer.Height = 16;
			labelLabelContainer.Margin = new BorderDouble(3, 0);

			string labelLabelTxt = "Label".Localize();
			TextWidget labelLabel = new TextWidget(string.Format(labelLabelTxt), textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 10);
			labelLabel.HAnchor = HAnchor.ParentLeft;
			labelLabel.VAnchor = VAnchor.ParentCenter;

			labelLabelContainer.AddChild(labelLabel);

			GuiWidget tempLabelContainer = new GuiWidget();
			tempLabelContainer.Width = 66;
			tempLabelContainer.Height = 16;
			tempLabelContainer.Margin = new BorderDouble(3, 0);

			TextWidget tempLabel = new TextWidget(string.Format("Temp (C)"), textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 10);
			tempLabel.HAnchor = HAnchor.ParentLeft;
			tempLabel.VAnchor = VAnchor.ParentCenter;

			tempLabelContainer.AddChild(tempLabel);

			leftRightLabels.AddChild(hLabelSpacer);
			leftRightLabels.AddChild(labelLabelContainer);
			leftRightLabels.AddChild(tempLabelContainer);

			presetsFormContainer.AddChild(leftRightLabels);

			// put in the temperature edit controls
			string[] settingsArray = temperatureSettings.Split(',');
			int preset_count = 1;
			int tab_index = 0;
			for (int i = 0; i < settingsArray.Count() - 1; i += 2)
			{
				FlowLayoutWidget leftRightEdit = new FlowLayoutWidget();
				leftRightEdit.Padding = new BorderDouble(3);
				leftRightEdit.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
				string presetLabelTxt = "Preset".Localize();
				TextWidget label = new TextWidget(string.Format("{1} {0}", preset_count, presetLabelTxt), textColor: ActiveTheme.Instance.PrimaryTextColor);
				label.VAnchor = VAnchor.ParentCenter;
				leftRightEdit.AddChild(label);

				leftRightEdit.AddChild(new HorizontalSpacer());

				MHTextEditWidget typeEdit = new MHTextEditWidget(settingsArray[i], pixelWidth: 60, tabIndex: tab_index++);

				typeEdit.Margin = new BorderDouble(3);
				leftRightEdit.AddChild(typeEdit);
				listWithValues.Add(typeEdit);

				double temperatureValue = 0;
				double.TryParse(settingsArray[i + 1], out temperatureValue);
				MHNumberEdit valueEdit = new MHNumberEdit(temperatureValue, minValue: 0, pixelWidth: 60, tabIndex: tab_index++);
				valueEdit.Margin = new BorderDouble(3);
				leftRightEdit.AddChild(valueEdit);
				listWithValues.Add(valueEdit);

				//leftRightEdit.AddChild(textImageButtonFactory.Generate("Delete".Localize()));
				presetsFormContainer.AddChild(leftRightEdit);
				preset_count += 1;
			}

			{
				FlowLayoutWidget leftRightEdit = new FlowLayoutWidget();
				leftRightEdit.Padding = new BorderDouble(3);
				leftRightEdit.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;

				TextWidget maxWidgetLabel = new TextWidget("Max Temp".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				maxWidgetLabel.VAnchor = VAnchor.ParentCenter;
				leftRightEdit.AddChild(maxWidgetLabel);
				leftRightEdit.AddChild(new HorizontalSpacer());

				double maxTemperature = 0;
				double.TryParse(settingsArray[settingsArray.Count() - 1], out maxTemperature);
				MHNumberEdit valueEdit = new MHNumberEdit(maxTemperature, minValue: 0, pixelWidth: 60, tabIndex: tab_index);
				valueEdit.Margin = new BorderDouble(3);
				leftRightEdit.AddChild(valueEdit);
				listWithValues.Add(valueEdit);

				presetsFormContainer.AddChild(leftRightEdit);
			}

			textImageButtonFactory.FixedHeight = oldHeight;

			ShowAsSystemWindow();
			MinimumSize = new Vector2(360, 300);

			Button savePresetsButton = textImageButtonFactory.Generate("Save".Localize());
			savePresetsButton.Click += new EventHandler(save_Click);

			Button cancelPresetsButton = textImageButtonFactory.Generate("Cancel".Localize());
			cancelPresetsButton.Click += (sender, e) => { CloseOnIdle(); };

			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Padding = new BorderDouble(0, 3);

			GuiWidget hButtonSpacer = new GuiWidget();
			hButtonSpacer.HAnchor = HAnchor.ParentLeftRight;

			buttonRow.AddChild(savePresetsButton);
			buttonRow.AddChild(hButtonSpacer);
			buttonRow.AddChild(cancelPresetsButton);

			topToBottom.AddChild(buttonRow);

			AddChild(topToBottom);
		}

		private void save_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(save_OnIdle);
		}

		private void save_OnIdle()
		{
			bool first = true;
			StringBuilder settingString = new StringBuilder();
			foreach (GuiWidget valueToAdd in listWithValues)
			{
				if (!first)
				{
					settingString.Append(",");
				}

				settingString.Append(valueToAdd.Text);
				first = false;
			}
			functionToCallOnSave(this, new StringEventArgs(settingString.ToString()));
			CloseOnIdle();
		}
	}
}