/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.Collections.Generic;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;

namespace MatterHackers.MatterControl
{
	public class XyCalibrationCollectDataPage : WizardPage
	{
		private List<RadioButton> xButtons;
		private XyCalibrationData xyCalibrationData;
		private List<RadioButton> yButtons;

		public XyCalibrationCollectDataPage(ISetupWizard setupWizard, PrinterConfig printer, XyCalibrationData xyCalibrationData)
			: base(setupWizard)
		{
			this.xyCalibrationData = xyCalibrationData;
			this.WindowTitle = "Nozzle Offset Calibration Wizard".Localize();
			this.HeaderText = "Nozzle Offset Calibration".Localize() + ":";
			this.Name = "Nozzle Offset Calibration Wizard";

			contentRow.Padding = theme.DefaultContainerPadding;

			contentRow.AddChild(new TextWidget("Choose the calibration you would like to perform.".Localize(), textColor: theme.TextColor, pointSize: theme.DefaultFontSize)
			{
				Margin = new Agg.BorderDouble(0, 15, 0, 0)
			});

			// disable the next button until we recieve data about both the x and y axis alignment
			NextButton.Enabled = false;

			var xButtonsGroup = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Fit | HAnchor.Left
			};
			contentRow.AddChild(xButtonsGroup);
			xButtons = new List<RadioButton>();
			xButtons.Add(new RadioButton("-2".Localize(), textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			xButtons.Add(new RadioButton("-1".Localize(), textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			xButtons.Add(new RadioButton(" 0".Localize(), textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			xButtons.Add(new RadioButton("+1".Localize(), textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			xButtons.Add(new RadioButton("+2".Localize(), textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			foreach (var button in xButtons)
			{
				xButtonsGroup.AddChild(button);
				button.CheckedStateChanged += XButton_CheckedStateChanged;
			}

			var yButtonsGroup = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit | HAnchor.Left
			};
			contentRow.AddChild(yButtonsGroup);
			yButtons = new List<RadioButton>();
			yButtons.Add(new RadioButton("-2".Localize(), textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			yButtons.Add(new RadioButton("-1".Localize(), textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			yButtons.Add(new RadioButton(" 0".Localize(), textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			yButtons.Add(new RadioButton("+1".Localize(), textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			yButtons.Add(new RadioButton("+2".Localize(), textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			foreach (var button in yButtons)
			{
				yButtonsGroup.AddChild(button);
				button.CheckedStateChanged += YButton_CheckedStateChanged;
			}
		}

		public override void OnClosed(EventArgs e)
		{
			// save the offsets to the extruder
			base.OnClosed(e);
		}

		private void CheckIfCanAdvance()
		{
			if (xyCalibrationData.YPick != -1
				&& xyCalibrationData.XPick != -1)
			{
				NextButton.Enabled = true;
			}
		}

		private void XButton_CheckedStateChanged(object sender, System.EventArgs e)
		{
			int i = 0;
			foreach (var button in xButtons)
			{
				if (button == sender)
				{
					xyCalibrationData.XPick = i;
					break;
				}
				i++;
			}
			CheckIfCanAdvance();
		}

		private void YButton_CheckedStateChanged(object sender, System.EventArgs e)
		{
			int i = 0;
			foreach (var button in yButtons)
			{
				if (button == sender)
				{
					xyCalibrationData.YPick = i;
					break;
				}
				i++;
			}
			CheckIfCanAdvance();
		}
	}
}