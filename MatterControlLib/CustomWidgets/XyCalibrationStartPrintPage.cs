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
using System.Threading;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class XyCalibrationStartPrintPage : WizardPage
	{
		private XyCalibrationData xyCalibrationData;

		public XyCalibrationStartPrintPage(ISetupWizard setupWizard, PrinterConfig printer, XyCalibrationData xyCalibrationData)
			: base(setupWizard)
		{
			this.xyCalibrationData = xyCalibrationData;
			this.WindowTitle = "Nozzle Offset Calibration Wizard".Localize();
			this.HeaderText = "Nozzle Offset Calibration".Localize();
			this.Name = "Nozzle Offset Calibration Wizard";

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
				// stash the current bed
				var scene = printer.Bed.Scene;
				scene.Children.Modify((list) => list.Clear());

				// create the item we are adding
				IObject3D item = CreateCorectCalibrationObject(printer, xyCalibrationData);

				// add the calibration object to the bed
				scene.Children.Add(item);

				// move the part to the center of the bed
				var bedBounds = printer.Bed.Bounds;
				var aabb = item.GetAxisAlignedBoundingBox();
				item.Matrix *= Matrix4X4.CreateTranslation(bedBounds.Center.X - aabb.MinXYZ.X - aabb.XSize / 2, bedBounds.Center.Y - aabb.MinXYZ.Y - aabb.YSize / 2, -aabb.MinXYZ.Z);
				// switch to 3D view
				// register callbacks for print compleation
				printer.Connection.Disposed += Connection_Disposed;
				printer.Connection.CommunicationStateChanged += Connection_CommunicationStateChanged;

				// close this window
				this.DialogWindow.CloseOnIdle();

				// Save the bed that we have created before starting print operation
				await printer.Bed.SaveChanges(null, CancellationToken.None);

				// start the calibration print
				await ApplicationController.Instance.PrintPart(
					printer.Bed.EditContext,
					printer,
					null,
					CancellationToken.None);
			};

			theme.ApplyPrimaryActionStyle(startCalibrationPrint);

			this.AddPageAction(startCalibrationPrint);
		}

		private void RestoreBedAndClearPrinterCallbacks()
		{
			printer.Connection.Disposed -= Connection_Disposed;
			printer.Connection.CommunicationStateChanged -= Connection_CommunicationStateChanged;
		}

		private void Connection_CommunicationStateChanged(object sender, EventArgs e)
		{
			switch (printer.Connection.CommunicationState)
			{
				// We are no longer running this calibration, unwind anything that we have done
				case PrinterCommunication.CommunicationStates.Disconnected:
				case PrinterCommunication.CommunicationStates.AttemptingToConnect:
				case PrinterCommunication.CommunicationStates.FailedToConnect:
				case PrinterCommunication.CommunicationStates.ConnectionLost:
				case PrinterCommunication.CommunicationStates.PrintingFromSd:
					RestoreBedAndClearPrinterCallbacks();
					break;

				// The print hase finished, open the window to collect our calibration results
				case PrinterCommunication.CommunicationStates.FinishedPrint:
					// open up the next part of the wizard
					UiThread.RunOnIdle(() =>
					{
						DialogWindow.Show(new XyCalibrationWizard(printer, xyCalibrationData.ExtruderToCalibrateIndex, xyCalibrationData));
					});
					// close down our listening to the printer and restor the bed
					RestoreBedAndClearPrinterCallbacks();
					break;

				// printing the calibration normaly
				case PrinterCommunication.CommunicationStates.Connected:
				case PrinterCommunication.CommunicationStates.PreparingToPrint:
				case PrinterCommunication.CommunicationStates.Printing:
				case PrinterCommunication.CommunicationStates.Paused:
				case PrinterCommunication.CommunicationStates.Disconnecting:
					break;
			}
		}

		private void Connection_Disposed(object sender, EventArgs e)
		{
			RestoreBedAndClearPrinterCallbacks();
		}

		private static IObject3D CreateCorectCalibrationObject(PrinterConfig printer, XyCalibrationData xyCalibrationData)
		{
			IObject3D item;
			switch (xyCalibrationData.Quality)
			{
				case XyCalibrationData.QualityType.Coarse:
					item = XyCalibrationTabObject3D.Create(1,
						Math.Max(printer.Settings.GetValue<double>(SettingsKey.first_layer_height) * 2, printer.Settings.GetValue<double>(SettingsKey.layer_height) * 2),
						.5,
						printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter)).GetAwaiter().GetResult();
					break;

				case XyCalibrationData.QualityType.Fine:
					item = XyCalibrationFaceObject3D.Create(1,
						printer.Settings.GetValue<double>(SettingsKey.first_layer_height) * 2,
						printer.Settings.GetValue<double>(SettingsKey.layer_height),
						.05,
						printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter),
						printer.Settings.GetValue<double>(SettingsKey.wipe_tower_size),
						6).GetAwaiter().GetResult();
					break;

				default:
					item = XyCalibrationFaceObject3D.Create(1,
						printer.Settings.GetValue<double>(SettingsKey.first_layer_height) * 2,
						printer.Settings.GetValue<double>(SettingsKey.layer_height),
						.1,
						printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter),
						printer.Settings.GetValue<double>(SettingsKey.wipe_tower_size),
						6).GetAwaiter().GetResult();
					break;
			}

			return item;
		}

		void PrintHasCompleated()
		{
			// if we are done callibrating
			printer.Settings.SetValue(SettingsKey.xy_offsets_have_been_calibrated, "1");
		}
	}
}
