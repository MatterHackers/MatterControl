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

using System.Collections.Generic;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;

namespace MatterHackers.MatterControl
{
	public class XyCalibrationSelectPage : WizardPage
	{
		private RadioButton coarseCalibration;
		private RadioButton normalCalibration;
		private RadioButton fineCalibration;

		public XyCalibrationSelectPage(ISetupWizard setupWizard, PrinterConfig printer, XyCalibrationData xyCalibrationData)
			: base(setupWizard)
		{
			this.WindowTitle = "Nozzle Offset Calibration Wizard".Localize();
			this.HeaderText = "Nozzle Offset Calibration".Localize() + ":";
			this.Name = "Nozzle Offset Calibration Wizard";

			contentRow.Padding = theme.DefaultContainerPadding;

			contentRow.AddChild(new TextWidget("Choose the calibration you would like to perform.".Localize(), textColor: theme.TextColor, pointSize: theme.DefaultFontSize)
			{
				Margin = new Agg.BorderDouble(0, 15, 0, 0)
			});

			contentRow.AddChild(coarseCalibration = new RadioButton("Coarse Calibration: If your printer is way off".Localize(), textColor: theme.TextColor, fontSize: theme.DefaultFontSize)
			{
				Checked = xyCalibrationData.Quality == XyCalibrationData.QualityType.Coarse
			});
			coarseCalibration.CheckedStateChanged += (s, e) => xyCalibrationData.Quality = XyCalibrationData.QualityType.Coarse;
			contentRow.AddChild(normalCalibration = new RadioButton("Normal Calibration: Start here".Localize(), textColor: theme.TextColor, fontSize: theme.DefaultFontSize)
			{
				Checked = xyCalibrationData.Quality == XyCalibrationData.QualityType.Normal
			});
			normalCalibration.CheckedStateChanged += (s, e) => xyCalibrationData.Quality = XyCalibrationData.QualityType.Normal;
			contentRow.AddChild(fineCalibration = new RadioButton("Fine Calibration: When you want that extra precision".Localize(), textColor: theme.TextColor, fontSize: theme.DefaultFontSize)
			{
				Checked = xyCalibrationData.Quality == XyCalibrationData.QualityType.Fine
			});
			fineCalibration.CheckedStateChanged += (s, e) => xyCalibrationData.Quality = XyCalibrationData.QualityType.Fine;
		}
	}
}
