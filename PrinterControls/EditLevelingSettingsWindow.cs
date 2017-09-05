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
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public class EditLevelingSettingsWindow : SystemWindow
	{
		private List<Vector3> positions = new List<Vector3>();
		PrinterSettings printerSettings;

		public EditLevelingSettingsWindow(PrinterSettings printerSettings)
			: base(400, 370)
		{
			this.printerSettings = printerSettings;
			var textImageButtonFactory = ApplicationController.Instance.Theme.ButtonFactory;

			AlwaysOnTopOfMain = true;
			Title = LocalizedString.Get("Leveling Settings".Localize());

			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			topToBottom.Padding = new BorderDouble(3, 0, 3, 5);

			FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.HAnchor = HAnchor.Stretch;
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 3, 0, 3);

			{
				string movementSpeedsLabel = LocalizedString.Get("Sampled Positions".Localize());
				TextWidget elementHeader = new TextWidget(string.Format("{0}:", movementSpeedsLabel), pointSize: 14);
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

			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			// put in the movement edit controls
			PrintLevelingData levelingData = printerSettings.Helpers.GetPrintLevelingData();
			for (int i = 0; i < levelingData.SampledPositions.Count; i++)
			{
				positions.Add(levelingData.SampledPositions[i]);
			}

			int tab_index = 0;
			for (int row = 0; row < positions.Count; row++)
			{
				FlowLayoutWidget leftRightEdit = new FlowLayoutWidget();
				leftRightEdit.Padding = new BorderDouble(3);
				leftRightEdit.HAnchor |= Agg.UI.HAnchor.Stretch;
				TextWidget positionLabel;

				string whichPositionText = "Position".Localize();
				positionLabel = new TextWidget("{0} {1,-5}".FormatWith(whichPositionText, row + 1), textColor: ActiveTheme.Instance.PrimaryTextColor);

				positionLabel.VAnchor = VAnchor.Center;
				leftRightEdit.AddChild(positionLabel);

				for (int axis = 0; axis < 3; axis++)
				{
					leftRightEdit.AddChild(new HorizontalSpacer());

					string axisName = "x";
					if (axis == 1) axisName = "y";
					else if (axis == 2) axisName = "z";

					TextWidget typeEdit = new TextWidget("  {0}: ".FormatWith(axisName), textColor: ActiveTheme.Instance.PrimaryTextColor);
					typeEdit.VAnchor = VAnchor.Center;
					leftRightEdit.AddChild(typeEdit);

					int linkCompatibleRow = row;
					int linkCompatibleAxis = axis;
					MHNumberEdit valueEdit = new MHNumberEdit(positions[linkCompatibleRow][linkCompatibleAxis], allowNegatives: true, allowDecimals: true, pixelWidth: 60, tabIndex: tab_index++);
					valueEdit.ActuallNumberEdit.InternalTextEditWidget.EditComplete += (sender, e) =>
					{
						Vector3 position = positions[linkCompatibleRow];
						position[linkCompatibleAxis] = valueEdit.ActuallNumberEdit.Value;
						positions[linkCompatibleRow] = position;
					};

					valueEdit.Margin = new BorderDouble(3);
					leftRightEdit.AddChild(valueEdit);
				}

				presetsFormContainer.AddChild(leftRightEdit);

				presetsFormContainer.AddChild(new CustomWidgets.HorizontalLine());
			}

			ShowAsSystemWindow();
			MinimumSize = new Vector2(Width, Height);

			Button savePresetsButton = textImageButtonFactory.Generate("Save".Localize());
			savePresetsButton.Click += (s,e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					PrintLevelingData newLevelingData = printerSettings.Helpers.GetPrintLevelingData();

					for (int i = 0; i < newLevelingData.SampledPositions.Count; i++)
					{
						newLevelingData.SampledPositions[i] = positions[i];
					}

					printerSettings.Helpers.SetPrintLevelingData(newLevelingData, false);
					printerSettings.Helpers.UpdateLevelSettings();
					Close();
				});
			};

			Button cancelPresetsButton = textImageButtonFactory.Generate("Cancel".Localize());
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
	}
}