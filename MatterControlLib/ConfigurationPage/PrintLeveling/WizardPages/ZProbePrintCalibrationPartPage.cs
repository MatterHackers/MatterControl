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
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class ZProbePrintCalibrationPartPage : WizardPage
	{
		public ZProbePrintCalibrationPartPage(ISetupWizard setupWizard, PrinterConfig printer, string headerText, string details)
			: base(setupWizard, headerText, details)
		{
			var spacer = new GuiWidget(15, 15);
			contentRow.AddChild(spacer);

			int tabIndex = 0;

			contentRow.AddChild(
				new TextWidget(
					"This wizard will close to print a calibration part and resume after the print completes.".Localize(),
					textColor: theme.TextColor,
					pointSize: theme.DefaultFontSize)
				{
					Margin = new BorderDouble(bottom: theme.DefaultContainerPadding)
				});

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

			if (printer.Settings.GetValue<int>(SettingsKey.top_solid_layers) < 4)
			{
				// The layer height is very small and it will be hard to see features. Show a warning.
				AddSettingsRow(contentRow, printer, "You should have at least 3 top layers for this calibration to measure off of.".Localize(), SettingsKey.top_solid_layers, theme, ref tabIndex);
			}

			this.NextButton.Visible = false;

			var startCalibrationPrint = theme.CreateDialogButton("Start Print".Localize());
			startCalibrationPrint.Name = "Start Calibration Print";
			startCalibrationPrint.Click += async (s, e) =>
			{
				var preCalibrationPrintViewMode = printer.ViewState.ViewMode;

				// create the calibration objects
				var item = CreateCalibrationObject(printer);

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

		public static double CalibrationObjectHeight(PrinterConfig printer)
		{
			var layerHeight = printer.Settings.GetValue<double>(SettingsKey.layer_height);
			var firstLayerHeight = printer.Settings.GetValue<double>(SettingsKey.first_layer_height);

			return firstLayerHeight + layerHeight * 4;
		}

		private static IObject3D CreateCalibrationObject(PrinterConfig printer)
		{
			var printObject = new Object3D();

			var layerHeight = printer.Settings.GetValue<double>(SettingsKey.layer_height);
			var baseSize = 20;
			var inset = 2.5;
			// add a base
			var mesh = PlatonicSolids.CreateCube(baseSize, baseSize, CalibrationObjectHeight(printer) - layerHeight);
			mesh.Translate(0, 0, mesh.GetAxisAlignedBoundingBox().ZSize / 2);
			printObject.Children.Add(new Object3D()
			{
				Mesh = mesh
			});

			// add a middle part where we will probe to find the height and the edges of
			mesh = PlatonicSolids.CreateCube(baseSize - inset, baseSize - inset, CalibrationObjectHeight(printer));
			mesh.Translate(0, 0, mesh.GetAxisAlignedBoundingBox().ZSize / 2);
			printObject.Children.Add(new Object3D()
			{
				Mesh = mesh
			});

			return printObject;
		}
	}
}