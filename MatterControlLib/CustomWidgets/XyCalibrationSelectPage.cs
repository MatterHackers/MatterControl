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
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.SlicerConfiguration;
using static MatterHackers.MatterControl.ConfigurationPage.PrintLeveling.XyCalibrationWizard;

namespace MatterHackers.MatterControl
{
	public class XyCalibrationSelectPage : WizardPage
	{
		private RadioButton coarseCalibration;
		private RadioButton normalCalibration;
		private RadioButton fineCalibration;

		public XyCalibrationSelectPage(XyCalibrationWizard calibrationWizard)
			: base(calibrationWizard)
		{
			this.WindowTitle = "Nozzle Offset Calibration Wizard".Localize();
			this.HeaderText = "Calibration Mode".Localize();

			contentRow.Padding = theme.DefaultContainerPadding;

			// default to normal offset
			calibrationWizard.Offset = printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter) / 3.0;

			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(left: theme.DefaultContainerPadding),
				HAnchor = HAnchor.Stretch
			};
			contentRow.AddChild(column);

			var coarseText = calibrationWizard.Quality == QualityType.Coarse ? "Coarse (recommended)".Localize() : "Coarse".Localize();
			column.AddChild(coarseCalibration = new RadioButton(coarseText, textColor: theme.TextColor, fontSize: theme.DefaultFontSize)
			{
				Checked = calibrationWizard.Quality == QualityType.Coarse
			});
			coarseCalibration.CheckedStateChanged += (s, e) =>
			{
				calibrationWizard.Quality = QualityType.Coarse;
				calibrationWizard.Offset = printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter);
			};

			var normalText = calibrationWizard.Quality == QualityType.Normal ? "Normal (recommended)".Localize() : "Normal".Localize();
			column.AddChild(normalCalibration = new RadioButton(normalText, textColor: theme.TextColor, fontSize: theme.DefaultFontSize)
			{
				Checked = calibrationWizard.Quality == QualityType.Normal
			});
			normalCalibration.CheckedStateChanged += (s, e) =>
			{
				calibrationWizard.Quality = QualityType.Normal;
				calibrationWizard.Offset = printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter) / 3.0;
			};

			column.AddChild(fineCalibration = new RadioButton("Fine".Localize(), textColor: theme.TextColor, fontSize: theme.DefaultFontSize)
			{
				Checked = calibrationWizard.Quality == QualityType.Fine
			});
			fineCalibration.CheckedStateChanged += (s, e) =>
			{
				calibrationWizard.Quality = QualityType.Fine;
				calibrationWizard.Offset = printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter) / 9.0;
			};
		}
	}
}
