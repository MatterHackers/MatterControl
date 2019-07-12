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
using MatterControl.Common.Repository;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using static MatterHackers.MatterControl.ConfigurationPage.PrintLeveling.XyCalibrationWizard;

namespace MatterHackers.MatterControl
{
	public class XyCalibrationSelectPage : WizardPage
	{
		private readonly RadioButton coarseCalibration;
		private readonly RadioButton normalCalibration;
		private readonly RadioButton fineCalibration;
		private PartViewMode preCalibrationPrintViewMode;

		public XyCalibrationSelectPage(XyCalibrationWizard calibrationWizard)
			: base(calibrationWizard)
		{
			this.WindowTitle = "Nozzle Offset Calibration Wizard".Localize();
			this.HeaderText = "Calibration Print".Localize();

			preCalibrationPrintViewMode = printer.ViewState.ViewMode;

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

			this.NextButton.Visible = false;

			// add in the option to tell the system the printer is already calibrated
			var alreadyCalibratedButton = theme.CreateDialogButton("Already Calibrated".Localize());
			alreadyCalibratedButton.Name = "Already Calibrated Button";
			alreadyCalibratedButton.Click += (s, e) =>
			{
				printer.Settings.SetValue(SettingsKey.xy_offsets_have_been_calibrated, "1");
				this.FinishWizard();
			};

			this.AddPageAction(alreadyCalibratedButton);

			var startCalibrationPrint = theme.CreateDialogButton("Start Print".Localize());
			startCalibrationPrint.Name = "Start Calibration Print";
			startCalibrationPrint.Click += async (s, e) =>
			{
				await PrintCalibrationPart(calibrationWizard);
			};

			this.AcceptButton = startCalibrationPrint;

			this.AddPageAction(startCalibrationPrint);
		}

		private async Task PrintCalibrationPart(XyCalibrationWizard calibrationWizard)
		{
			var scene = new Object3D();

			// create the calibration objects
			IObject3D item = await CreateCalibrationObject(printer, calibrationWizard);

			// add the calibration object to the bed
			scene.Children.Add(item);

			// move the part to the center of the bed
			var bedBounds = printer.Settings.BedBounds;
			var aabb = item.GetAxisAlignedBoundingBox();
			item.Matrix *= Matrix4X4.CreateTranslation(bedBounds.Center.X - aabb.MinXYZ.X - aabb.XSize / 2, bedBounds.Center.Y - aabb.MinXYZ.Y - aabb.YSize / 2, -aabb.MinXYZ.Z);

			// register callbacks for print completion
			printer.Connection.Disposed += this.Connection_Disposed;
			printer.Connection.PrintCanceled += this.Connection_PrintCanceled;
			printer.Connection.CommunicationStateChanged += this.Connection_CommunicationStateChanged;

			// hide this window
			this.DialogWindow.Visible = false;

			string gcodePath = EditContext.GCodeFilePath(printer, scene);

			printer.Connection.CommunicationState = CommunicationStates.PreparingToPrint;

			(bool slicingSucceeded, string finalGCodePath) = await ApplicationController.Instance.SliceItemLoadOutput(
				printer,
				scene,
				gcodePath);

			// Only start print if slicing completed
			if (slicingSucceeded)
			{
				await printer.Bed.LoadContent(new EditContext()
				{
					SourceItem = new FileSystemFileItem(gcodePath),
					ContentStore = null // No content store for GCode
				});

				var printTask = new PrintJob()
				{
					PrintStart = DateTime.Now,
					PrinterId = printer.Settings.ID.GetHashCode(),
					PrintName = "hello", // activePrintItem.PrintItem.Name,
					GCodeFile = gcodePath,
					PrintComplete = false
				};

				// TODO: Reimplement
				// await printer.Connection.StartPrint(finalGCodePath, calibrationPrint: true);
				printer.Connection.StartPrint(printTask, calibrationPrint: true);
				ApplicationController.Instance.MonitorPrintTask(printer);
			}
			else
			{
				printer.Connection.CommunicationState = CommunicationStates.Connected;
			}
		}

		private void Connection_PrintCanceled(object sender, EventArgs e)
		{
			this.ReturnToCalibrationWizard();
		}

		private void UnregisterPrinterEvents()
		{
			printer.Connection.Disposed -= this.Connection_Disposed;
			printer.Connection.CommunicationStateChanged -= this.Connection_CommunicationStateChanged;
			printer.Connection.PrintCanceled -= this.Connection_PrintCanceled;
		}

		private void Connection_CommunicationStateChanged(object sender, EventArgs e)
		{
			switch (printer.Connection.CommunicationState)
			{
				case CommunicationStates.Disconnected:
				case CommunicationStates.AttemptingToConnect:
				case CommunicationStates.FailedToConnect:
				case CommunicationStates.ConnectionLost:
				case CommunicationStates.PrintingFromSd:
				case CommunicationStates.FinishedPrint:
					// We are no longer printing, exit and return to where we started
					this.ReturnToCalibrationWizard();

					break;
			}
		}

		private void ReturnToCalibrationWizard()
		{
			UiThread.RunOnIdle(() =>
			{
				// Restore the original DialogWindow
				this.DialogWindow.Visible = true;

				// Restore to original view mode
				printer.ViewState.ViewMode = preCalibrationPrintViewMode;

				this.MoveToNextPage();
			});

			this.UnregisterPrinterEvents();
		}

		private void Connection_Disposed(object sender, EventArgs e)
		{
			this.UnregisterPrinterEvents();
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
