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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl
{
	public class NozzleOffsetCalibrationResultsPage : WizardPage
	{
		public NozzleOffsetCalibrationResultsPage(ISetupWizard setupWizard, PrinterConfig printer, double xOffset, double yOffset)
			: base(setupWizard)
		{
			this.WindowTitle = "Nozzle Offset Calibration Wizard".Localize();
			this.HeaderText = "Nozzle Offset Calibration".Localize() + ":";
			this.Name = "Nozzle Offset Calibration Wizard";

			this.CreateTextField("Congratulations, your nozzle offsets have been collected and are ready to be saved. Click next to save and finish the wizard".Localize());

			var row =  new SettingsRow(
				"X Offset".Localize(),
				null,
				theme,
				AggContext.StaticData.LoadIcon("probing_32x32.png", 16, 16, theme.InvertIcons));
			contentRow.AddChild(row);

			row.AddChild(new TextWidget(xOffset.ToString("0.###") + "mm", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(right: 10)
			});

			row = new SettingsRow(
				"Y Offset".Localize(),
				null,
				theme,
				AggContext.StaticData.LoadIcon("probing_32x32.png", 16, 16, theme.InvertIcons));
			contentRow.AddChild(row);

			row.AddChild(new TextWidget(yOffset.ToString("0.###") + "mm", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(right: 10)
			});

			this.NextButton.Visible = false;

			var nextButton = theme.CreateDialogButton("Finish".Localize());
			nextButton.Name = "FinishCalibration";
			nextButton.Click += (s, e) =>
			{
				// TODO: removed fixed index
				var hotendOffset = printer.Settings.Helpers.ExtruderOffset(1);
				hotendOffset.X += xOffset;
				hotendOffset.Y += yOffset;

				printer.Settings.Helpers.SetExtruderOffset(1, hotendOffset);

				this.DialogWindow.CloseOnIdle();
			};

			theme.ApplyPrimaryActionStyle(nextButton);

			this.AddPageAction(nextButton);
		}
	}
}
