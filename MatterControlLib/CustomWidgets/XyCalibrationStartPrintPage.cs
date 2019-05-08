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
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using static MatterHackers.MatterControl.ConfigurationPage.PrintLeveling.XyCalibrationWizard;

namespace MatterHackers.MatterControl
{
	public class XyCalibrationStartPrintPage : WizardPage
	{
		public XyCalibrationStartPrintPage(XyCalibrationWizard calibrationWizard)
			: base(calibrationWizard)
		{
			this.WindowTitle = "Nozzle Offset Calibration Wizard".Localize();
			this.HeaderText = "Calibration Print".Localize();

			var content = "Here is what we are going to do:".Localize();
			content += "\n\n    • " + "Stash your current bed".Localize();
			content += "\n    • " + "Print the calibration object".Localize();
			content += "\n    • " + "Collect data".Localize();
			content += "\n    • " + "Restore your current bed, after all calibration is complete".Localize();

			contentRow.AddChild(this.CreateTextField(content));

			contentRow.Padding = theme.DefaultContainerPadding;

			this.NextButton.Visible = false;

			var startCalibrationPrint = theme.CreateDialogButton("Start Print".Localize());
			startCalibrationPrint.Name = "Start Calibration Print";
			startCalibrationPrint.Click += async (s, e) =>
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

				this.MoveToNextPage();

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

					await printer.Connection.StartPrint(finalGCodePath, calibrationPrint: true);
					ApplicationController.Instance.MonitorPrintTask(printer);
				}
				else
				{
					printer.Connection.CommunicationState = CommunicationStates.Connected;
				}
			};

			theme.ApplyPrimaryActionStyle(startCalibrationPrint);

			this.AddPageAction(startCalibrationPrint);
		}

		private void Connection_PrintCanceled(object sender, EventArgs e)
		{
			this.ReturnToCalibrationWizard();

			// Exit the calibration and return to wizard home page
			this.DialogWindow.ClosePage();
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

			switch (calibrationWizard.Quality)
			{
				case QualityType.Coarse:
					return await XyCalibrationTabObject3D.Create(
						1,
						Math.Max(printer.Settings.GetValue<double>(SettingsKey.first_layer_height) * 2, layerHeight * 2),
						calibrationWizard.Offset,
						printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter));

				default:
					return await XyCalibrationFaceObject3D.Create(
						1,
						printer.Settings.GetValue<double>(SettingsKey.first_layer_height) + layerHeight,
						layerHeight,
						calibrationWizard.Offset,
						printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter),
						printer.Settings.GetValue<double>(SettingsKey.wipe_tower_size),
						6);
			}
		}
	}
}
