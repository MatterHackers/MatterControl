/*
Copyright (c) 2018, Kevin Pope, John Lewin
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
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class EditLevelingSettingsPage : DialogPage
	{
		public EditLevelingSettingsPage(PrinterConfig printer)
		{
			var theme = ApplicationController.Instance.Theme;
			var textImageButtonFactory = theme.ButtonFactory;

			this.WindowTitle = "Leveling Settings".Localize();
			this.HeaderText = "Sampled Positions".Localize();

			var positions = new List<Vector3>();

			// put in the movement edit controls
			PrintLevelingData levelingData = printer.Settings.Helpers.GetPrintLevelingData();
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

				var positionLabel = new TextWidget("{0} {1,-5}".FormatWith("Position".Localize(), row + 1), textColor: ActiveTheme.Instance.PrimaryTextColor);

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

				this.contentRow.AddChild(leftRightEdit);
			}

			var runWizardButton = new TextButton("Run Leveling Wizard".Localize(), theme)
			{
				VAnchor = VAnchor.Absolute,
				HAnchor = HAnchor.Right,
				BackgroundColor = theme.MinimalShade,
				Margin = new BorderDouble(5, 0, 5, 20)
			};
			runWizardButton.Click += (s, e) =>
			{
				this.WizardWindow.CloseOnIdle();
				UiThread.RunOnIdle(() =>
				{
					LevelWizardBase.ShowPrintLevelWizard(printer, LevelWizardBase.RuningState.UserRequestedCalibration);
				});
			};

			this.contentRow.AddChild(runWizardButton);

			Button savePresetsButton = textImageButtonFactory.Generate("Save".Localize());
			savePresetsButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					PrintLevelingData newLevelingData = printer.Settings.Helpers.GetPrintLevelingData();

					for (int i = 0; i < newLevelingData.SampledPositions.Count; i++)
					{
						newLevelingData.SampledPositions[i] = positions[i];
					}

					printer.Settings.Helpers.SetPrintLevelingData(newLevelingData, false);
					printer.Settings.Helpers.UpdateLevelSettings();
					this.Close();
				});
			};

			this.AddPageAction(savePresetsButton);
		}
	}
}