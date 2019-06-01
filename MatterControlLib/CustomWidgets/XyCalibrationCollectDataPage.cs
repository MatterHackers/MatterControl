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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;

namespace MatterHackers.MatterControl
{
	public class XyCalibrationCollectDataPage : WizardPage
	{
		private readonly XyCalibrationWizard calibrationWizard;

		public XyCalibrationCollectDataPage(XyCalibrationWizard calibrationWizard)
			: base(calibrationWizard)
		{
			this.calibrationWizard = calibrationWizard;
			this.WindowTitle = "Nozzle Offset Calibration Wizard".Localize();
			this.HeaderText = "Nozzle Offset Calibration".Localize();

			contentRow.Padding = theme.DefaultContainerPadding;

			contentRow.AddChild(
				new TextWidget("Remove the calibration part from the bed and compare the sides of the pads in each axis.".Localize(), textColor: theme.TextColor, pointSize: theme.DefaultFontSize)
				{
					Margin = new BorderDouble(0, 15, 0, 0)
				});

			contentRow.AddChild(
				new TextWidget("Pick the pad that is the most aligned with the base, the pad that is the most balance and centered.".Localize(), textColor: theme.TextColor, pointSize: theme.DefaultFontSize)
				{
					Margin = new BorderDouble(0, 15, 0, 0)
				});

			// disable the next button until we receive data about both the x and y axis alignment
			NextButton.Enabled = false;

			var calibrationRow = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			contentRow.AddChild(calibrationRow);

			calibrationRow.AddChild(
				new CalibrationTabWidget(calibrationWizard, NextButton, theme)
				{
					HAnchor = HAnchor.Center | HAnchor.Absolute,
					VAnchor = VAnchor.Center | VAnchor.Absolute,
					Width = 350,
					Height = 350
				});
		}

		public override void OnAdvance()
		{
			// save the offsets to the extruder
			if (calibrationWizard.XPick != -1
				&& calibrationWizard.YPick != -1)
			{
				var hotendOffset = printer.Settings.Helpers.ExtruderOffset(calibrationWizard.ExtruderToCalibrateIndex);
				hotendOffset.X -= calibrationWizard.Offset * 3 - calibrationWizard.Offset * calibrationWizard.XPick;
				hotendOffset.Y -= calibrationWizard.Offset * -3 + calibrationWizard.Offset * calibrationWizard.YPick;

				printer.Settings.Helpers.SetExtruderOffset(calibrationWizard.ExtruderToCalibrateIndex, hotendOffset);
			}
		}
	}
} 