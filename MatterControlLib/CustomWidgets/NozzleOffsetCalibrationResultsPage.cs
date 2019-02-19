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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
	public class NozzleOffsetCalibrationResultsPage : WizardPage
	{
		private TextWidget activeOffset;
		private CalibrationLine calibrationLine;

		public NozzleOffsetCalibrationResultsPage(ISetupWizard setupWizard, PrinterConfig printer, double[] activeOffsets)
			: base(setupWizard)
		{
			this.WindowTitle = "Nozzle Offset Calibration Wizard".Localize();
			this.HeaderText = "Nozzle Offset Calibration".Localize() + ":";
			this.Name = "Nozzle Offset Calibration Wizard";

			var commonMargin = new BorderDouble(4, 2);

			var row = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Absolute,
				Padding = new BorderDouble(6, 0),
				Height = 125
			};
			contentRow.AddChild(row);

			contentRow.AddChild(activeOffset = new TextWidget("", pointSize: theme.DefaultFontSize, textColor: theme.TextColor));

			var nextButton = theme.CreateDialogButton("Next".Localize());
			nextButton.Name = "Begin calibration print";
			nextButton.Click += (s, e) =>
			{
				var hotendOffset = printer.Settings.Helpers.ExtruderOffset(1);
				hotendOffset.Y += double.Parse(activeOffset.Text);
				printer.Settings.Helpers.SetExtruderOffset(1, hotendOffset);
			};

			theme.ApplyPrimaryActionStyle(nextButton);

			this.AddPageAction(nextButton);
		}
	}
}
