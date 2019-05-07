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
		private List<RadioButton> yButtons;
		private bool HaveWrittenData = false;
		private bool pageCanceled;
		private XyCalibrationWizard calibrationWizard;

		public XyCalibrationCollectDataPage(XyCalibrationWizard calibrationWizard)
			: base(calibrationWizard)
		{
			this.calibrationWizard = calibrationWizard;
			this.WindowTitle = "Nozzle Offset Calibration Wizard".Localize();
			this.HeaderText = "Nozzle Offset Calibration".Localize() + ":";

			contentRow.Padding = theme.DefaultContainerPadding;

			contentRow.AddChild(new TextWidget("Pick the most balanced result for each axis.".Localize(), textColor: theme.TextColor, pointSize: theme.DefaultFontSize)
			{
				Margin = new Agg.BorderDouble(0, 15, 0, 0)
			});

			// disable the next button until we receive data about both the x and y axis alignment
			NextButton.Enabled = false;

			var xButtonsGroup = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Fit | HAnchor.Left
			};
			contentRow.AddChild(xButtonsGroup);
			xButtons = new List<RadioButton>();
			xButtons.Add(new RadioButton("-3", textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			xButtons.Add(new RadioButton("-2", textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			xButtons.Add(new RadioButton("-1", textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			xButtons.Add(new RadioButton(" 0", textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			xButtons.Add(new RadioButton("+1", textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			xButtons.Add(new RadioButton("+2", textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			xButtons.Add(new RadioButton("+3", textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
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
			yButtonsGroup.AddChild(new GuiWidget(24 * GuiWidget.DeviceScale, 16));
			yButtons = new List<RadioButton>();
			yButtons.Add(new RadioButton("-3", textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			yButtons.Add(new RadioButton("-2", textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			yButtons.Add(new RadioButton("-1", textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			yButtons.Add(new RadioButton(" 0", textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			yButtons.Add(new RadioButton("+1", textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			yButtons.Add(new RadioButton("+2", textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			yButtons.Add(new RadioButton("+3", textColor: theme.TextColor, fontSize: theme.DefaultFontSize));
			foreach (var button in yButtons)
			{
				var column = new FlowLayoutWidget(FlowDirection.TopToBottom);
				yButtonsGroup.AddChild(column);

				button.HAnchor = HAnchor.Center;
				column.AddChild(button);
				column.AddChild(new TextWidget(button.Text, textColor: theme.TextColor, pointSize: theme.DefaultFontSize)
				{
					HAnchor = HAnchor.Left
				});
				button.Text = "";

				button.CheckedStateChanged += YButton_CheckedStateChanged;
			}
		}

		protected override void OnCancel(out bool abortCancel)
		{
			pageCanceled = true;
			base.OnCancel(out abortCancel);
		}

		public override void OnClosed(EventArgs e)
		{
			// save the offsets to the extruder
			if (!pageCanceled
				&& !HaveWrittenData
				&& calibrationWizard.XPick != -1
				&& calibrationWizard.YPick != -1)
			{
				var hotendOffset = printer.Settings.Helpers.ExtruderOffset(calibrationWizard.ExtruderToCalibrateIndex);
				hotendOffset.X -= calibrationWizard.Offset * -3 + calibrationWizard.Offset * calibrationWizard.XPick;
				hotendOffset.Y -= calibrationWizard.Offset * -3 + calibrationWizard.Offset * calibrationWizard.YPick;

				printer.Settings.Helpers.SetExtruderOffset(calibrationWizard.ExtruderToCalibrateIndex, hotendOffset);
				HaveWrittenData = true;
			}

			base.OnClosed(e);
		}

		private void CheckIfCanAdvance()
		{
			if (calibrationWizard.YPick != -1
				&& calibrationWizard.XPick != -1)
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
					calibrationWizard.XPick = i;
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
					calibrationWizard.YPick = i;
					break;
				}
				i++;
			}
			CheckIfCanAdvance();
		}
	}
}