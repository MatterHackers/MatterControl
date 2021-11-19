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
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;
using static MatterHackers.MatterControl.ConfigurationPage.PrintLeveling.XyCalibrationWizard;

namespace MatterHackers.MatterControl
{
	public class XyCalibrationSelectPage : WizardPage
	{
		private readonly RadioButton coarseCalibration;
		private readonly RadioButton normalCalibration;
		private readonly RadioButton fineCalibration;

		public XyCalibrationSelectPage(XyCalibrationWizard calibrationWizard)
			: base(calibrationWizard)
		{
			this.WindowTitle = "Nozzle Offset Calibration Wizard".Localize();
			this.HeaderText = "Calibration Print".Localize();

			contentRow.Padding = theme.DefaultContainerPadding;

			// default to normal offset
			calibrationWizard.Offset = printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter) / 3.0;

			contentRow.AddChild(
				new TextWidget(
					"This wizard will close to print a calibration part and resume after the print completes.".Localize(),
					textColor: theme.TextColor,
					pointSize: theme.DefaultFontSize)
				{
					Margin = new BorderDouble(bottom: theme.DefaultContainerPadding)
				});

			contentRow.AddChild(
				new TextWidget(
					"Calibration Mode".Localize(),
					textColor: theme.TextColor,
					pointSize: theme.DefaultFontSize)
				{
					Margin = new BorderDouble(0, theme.DefaultContainerPadding)
				});


			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(left: theme.DefaultContainerPadding),
				HAnchor = HAnchor.Stretch,
			};
			contentRow.AddChild(column);

			var coarseText = calibrationWizard.Quality == QualityType.Coarse ? "Initial (Recommended)".Localize() : "Coarse".Localize();
			column.AddChild(coarseCalibration = new RadioButton(coarseText, textColor: theme.TextColor, fontSize: theme.DefaultFontSize)
			{
				Checked = calibrationWizard.Quality == QualityType.Coarse
			});
			coarseCalibration.CheckedStateChanged += (s, e) =>
			{
				calibrationWizard.Quality = QualityType.Coarse;
				calibrationWizard.Offset = printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter);
			};

			var normalText = calibrationWizard.Quality == QualityType.Normal ? "Normal (Recommended)".Localize() : "Normal".Localize();
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

			int tabIndex = 0;

			if (printer.Settings.GetValue<double>(SettingsKey.layer_height) < printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter) / 2)
			{
				// The layer height is very small and it will be hard to see features. Show a warning.
				AddSettingsRow(contentRow, printer, "The calibration object will printer better if the layer hight is set to a larger value. It is recommended that your increase it.".Localize(), SettingsKey.layer_height, theme, ref tabIndex);
			}

			if (printer.Settings.GetValue<bool>(SettingsKey.create_raft))
			{
				// The layer height is very small and it will be hard to see features. Show a warning.
				AddSettingsRow(contentRow, printer, "A raft is not needed for the calibration object. It is recommended that you turn it off.".Localize(), SettingsKey.create_raft, theme, ref tabIndex);
			}

			this.NextButton.Visible = false;

			// add in the option to tell the system the printer is already calibrated
			var alreadyCalibratedButton = theme.CreateDialogButton("Already Calibrated".Localize());
			alreadyCalibratedButton.Name = "Already Calibrated Button";
			alreadyCalibratedButton.Click += (s, e) =>
			{
				printer.Settings.SetValue(SettingsKey.xy_offsets_have_been_calibrated, "1");
				FinishWizard();
			};

			this.AddPageAction(alreadyCalibratedButton);

			var startCalibrationPrint = theme.CreateDialogButton("Start Print".Localize());
			startCalibrationPrint.Name = "Start Calibration Print";
			startCalibrationPrint.Click += async (s, e) =>
			{
				var preCalibrationPrintViewMode = printer.ViewState.ViewMode;

				// create the calibration objects
				IObject3D item = await CreateCalibrationObject(printer, calibrationWizard);

				var calibrationObjectPrinter = new CalibrationObjectPrinter(printer, item);
				// hide this window
				this.DialogWindow.Visible = false;

				await calibrationObjectPrinter.PrintCalibrationPart();

				// Restore the original DialogWindow
				this.DialogWindow.Visible = true;

				// Restore to original view mode
				printer.ViewState.ViewMode = preCalibrationPrintViewMode;

				this.MoveToNextPage();
			};

			this.AcceptButton = startCalibrationPrint;

			this.AddPageAction(startCalibrationPrint);
		}

		private static async Task<IObject3D> CreateCalibrationObject(PrinterConfig printer, XyCalibrationWizard calibrationWizard)
		{
			var layerHeight = printer.Settings.GetValue<double>(SettingsKey.layer_height);
			var firstLayerHeight = printer.Settings.GetValue<double>(SettingsKey.first_layer_height);

			switch (calibrationWizard.Quality)
			{
				case QualityType.Coarse:
					return await XyCalibrationTabObject3D.Create(
						1,
						Math.Max(firstLayerHeight * 2, layerHeight * 2),
						calibrationWizard.Offset,
						printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter),
						printer.Settings.GetValue<double>(SettingsKey.wipe_tower_size));

				default:
					return await XyCalibrationFaceObject3D.Create(
						1,
						firstLayerHeight + layerHeight,
						layerHeight,
						calibrationWizard.Offset,
						printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter),
						printer.Settings.GetValue<double>(SettingsKey.wipe_tower_size),
						4);
			}
		}
	}
}
