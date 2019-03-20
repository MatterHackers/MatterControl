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

using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;

namespace MatterHackers.MatterControl
{
	public class XyCalibrationDataRecieved : WizardPage
	{
		public XyCalibrationDataRecieved(ISetupWizard setupWizard, PrinterConfig printer, XyCalibrationData xyCalibrationData)
			: base(setupWizard)
		{
			this.WindowTitle = "Nozzle Offset Calibration Wizard".Localize();
			this.HeaderText = "Nozzle Offset Calibration".Localize() + ":";
			this.Name = "Nozzle Offset Calibration Wizard";

			contentRow.Padding = theme.DefaultContainerPadding;

			bool printAgain = false;

			// check if we picked an outside of the calibration
			if (xyCalibrationData.XPick == 0
				|| xyCalibrationData.XPick == 4
				|| xyCalibrationData.YPick == 0
				|| xyCalibrationData.YPick == 4)
			{
				// offer to re-run the calibration with the same settings as last time
				printAgain = true;
			}
			else
			{
				switch (xyCalibrationData.Quality)
				{
					case XyCalibrationData.QualityType.Coarse:
						// if we are on coarse calibration offer to move down to normal
						contentRow.AddChild(new TextWidget("Coarse calibration complete, you should now do a fine calibration to improve accuracy.".Localize(), textColor: theme.TextColor, pointSize: theme.DefaultFontSize)
						{
							Margin = new Agg.BorderDouble(0, 15, 0, 0)
						});
						printAgain = true;
						break;

					case XyCalibrationData.QualityType.Normal:
						// let the user know they are done with calibration, but if they would like they can print a fine calibration for even better results
						// add a button to request fine calibration
						break;

					case XyCalibrationData.QualityType.Fine:
						// done!
						break;
				}
			}

			// this is the last page of the wizard hide the next button
			this.NextButton.Visible = false;

			var doneCalibratingButton = theme.CreateDialogButton(printAgain ? "Start Print".Localize() : "Done".Localize());
			doneCalibratingButton.Name = "Done Calibration Print";
			doneCalibratingButton.Click += (s, e) =>
			{
				// close this window
				this.DialogWindow.CloseOnIdle();
				if (printAgain)
				{
					UiThread.RunOnIdle(() =>
					{
						DialogWindow.Show(new XyCalibrationWizard(printer, xyCalibrationData.ExtruderToCalibrateIndex, xyCalibrationData, true));
					});
				}
			};

			theme.ApplyPrimaryActionStyle(doneCalibratingButton);

			this.AddPageAction(doneCalibratingButton);
		}
	}
}
