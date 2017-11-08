/*
Copyright (c) 2014, Kevin Pope
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
	public class EditManualMovementSpeedsWindow : SystemWindow
	{
		private Action<string> functionToCallOnSave;
		private List<string> axisLabels = new List<string>();
		private List<GuiWidget> valueEditors = new List<GuiWidget>();

		public EditManualMovementSpeedsWindow(string windowTitle, string movementSpeedsString, Action<string> functionToCallOnSave)
			: base(260, 300)
		{
			var buttonFactory = ApplicationController.Instance.Theme.ButtonFactory;

			AlwaysOnTopOfMain = true;
			Title = windowTitle;

			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			topToBottom.Padding = new BorderDouble(3, 0, 3, 5);

			FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.HAnchor = HAnchor.Stretch;
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 3, 0, 3);

			{
				var elementHeader = new TextWidget("Movement Speeds Presets".Localize() + ":", pointSize: 14);
				elementHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				elementHeader.HAnchor = HAnchor.Stretch;
				elementHeader.VAnchor = Agg.UI.VAnchor.Bottom;

				headerRow.AddChild(elementHeader);
			}

			topToBottom.AddChild(headerRow);

			FlowLayoutWidget presetsFormContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			//ListBox printerListContainer = new ListBox();
			{
				presetsFormContainer.HAnchor = HAnchor.Stretch;
				presetsFormContainer.VAnchor = VAnchor.Stretch;
				presetsFormContainer.Padding = new BorderDouble(3);
				presetsFormContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			}

			topToBottom.AddChild(presetsFormContainer);

			this.functionToCallOnSave = functionToCallOnSave;
			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			TextWidget tempTypeLabel = new TextWidget(windowTitle, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 10);
			tempTypeLabel.Margin = new BorderDouble(3);
			tempTypeLabel.HAnchor = HAnchor.Left;
			presetsFormContainer.AddChild(tempTypeLabel);

			FlowLayoutWidget leftRightLabels = new FlowLayoutWidget();
			leftRightLabels.Padding = new BorderDouble(3, 6);
			leftRightLabels.HAnchor |= Agg.UI.HAnchor.Stretch;

			GuiWidget hLabelSpacer = new GuiWidget();
			hLabelSpacer.HAnchor = HAnchor.Stretch;

			GuiWidget tempLabelContainer = new GuiWidget();
			tempLabelContainer.Width = 76;
			tempLabelContainer.Height = 16;
			tempLabelContainer.Margin = new BorderDouble(3, 0);

			TextWidget tempLabel = new TextWidget("mm/s".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: 10);
			tempLabel.HAnchor = HAnchor.Left;
			tempLabel.VAnchor = VAnchor.Center;

			tempLabelContainer.AddChild(tempLabel);

			leftRightLabels.AddChild(hLabelSpacer);
			leftRightLabels.AddChild(tempLabelContainer);

			presetsFormContainer.AddChild(leftRightLabels);

			// put in the movement edit controls
			string[] settingsArray = movementSpeedsString.Split(',');
			int preset_count = 1;
			int tab_index = 0;
			for (int i = 0; i < settingsArray.Count() - 1; i += 2)
			{
				FlowLayoutWidget leftRightEdit = new FlowLayoutWidget();
				leftRightEdit.Padding = new BorderDouble(3);
				leftRightEdit.HAnchor |= Agg.UI.HAnchor.Stretch;
				TextWidget axisLabel;
				if (settingsArray[i].StartsWith("e"))
				{
					axisLabel = new TextWidget(string.Format("{0}(s)", "Extruder".Localize()), textColor: ActiveTheme.Instance.PrimaryTextColor);
				}
				else
				{
					axisLabel = new TextWidget(string.Format("{0} {1}", "Axis".Localize(), settingsArray[i].ToUpper()), textColor: ActiveTheme.Instance.PrimaryTextColor);
				}
				axisLabel.VAnchor = VAnchor.Center;
				leftRightEdit.AddChild(axisLabel);

				leftRightEdit.AddChild(new HorizontalSpacer());

				axisLabels.Add(settingsArray[i]);

				double movementSpeed = 0;
				double.TryParse(settingsArray[i + 1], out movementSpeed);
				movementSpeed = movementSpeed / 60.0;	// Convert from mm/min to mm/s
				MHNumberEdit valueEdit = new MHNumberEdit(movementSpeed, minValue: 0, pixelWidth: 60, tabIndex: tab_index++, allowDecimals: true);
				valueEdit.Margin = new BorderDouble(3);
				leftRightEdit.AddChild(valueEdit);
				valueEditors.Add(valueEdit);

				//leftRightEdit.AddChild(textImageButtonFactory.Generate("Delete".Localize()));
				presetsFormContainer.AddChild(leftRightEdit);
				preset_count += 1;
			}

			ShowAsSystemWindow();
			MinimumSize = new Vector2(260, 300);

			Button savePresetsButton = buttonFactory.Generate("Save".Localize());
			savePresetsButton.Click += save_Click;

			Button cancelPresetsButton = buttonFactory.Generate("Cancel".Localize());
			cancelPresetsButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(Close);
			};

			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.Stretch;
			buttonRow.Padding = new BorderDouble(0, 3);

			GuiWidget hButtonSpacer = new GuiWidget();
			hButtonSpacer.HAnchor = HAnchor.Stretch;

			buttonRow.AddChild(savePresetsButton);
			buttonRow.AddChild(hButtonSpacer);
			buttonRow.AddChild(cancelPresetsButton);

			topToBottom.AddChild(buttonRow);

			AddChild(topToBottom);
		}

		private void save_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(DoSave_Click);
		}

		private void DoSave_Click()
		{
			bool first = true;
			StringBuilder settingString = new StringBuilder();
			for (int i = 0; i < valueEditors.Count(); i++)
			{
				if (!first)
				{
					settingString.Append(",");
				}
				first = false;

				settingString.Append(axisLabels[i]);
				settingString.Append(",");

				double movementSpeed = 0;
				double.TryParse(valueEditors[i].Text, out movementSpeed);
				movementSpeed = movementSpeed * 60;	// Convert to mm/min

				settingString.Append(movementSpeed.ToString());
			}

			functionToCallOnSave(settingString.ToString());
			Close();
		}
	}
}
