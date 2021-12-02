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
using System.Collections.Generic;
using System.Linq;
using MatterControl.Printing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class ZProbeCalibrateRetrieveTopProbeData : WizardPage
	{
		private bool validationRunning;
		private bool oldAllowLeveling;
		private Vector3 positionToSample;
		private List<double> babySteppingValue;
		private Vector3 sampledPosition;
		private bool waitingToCompleteNextSample;
		private bool dataCollected;
		private List<double> samplesForSinglePosition;
		private Vector3 positionToSampleWithProbeOffset;

		public ZProbeCalibrateRetrieveTopProbeData(ISetupWizard setupWizard, string headerText)
			: base(setupWizard, headerText, "")
		{
			contentRow.AddChild(this.CreateTextField("We will now sample the top of the part.".Localize()));

			contentRow.BackgroundColor = theme.MinimalShade;
		}

		public override void OnClosed(EventArgs e)
		{
			CancelValidation();
			printer.Connection.CanceleRequested -= Connection_PrintCanceled;

			base.OnClosed(e);
		}

		private void CancelValidation()
		{
			if (validationRunning)
			{
				validationRunning = false;

				printer.Connection.LineReceived -= GetZProbeHeight;

				// If leveling was on when we started, make sure it is on when we are done.
				printer.Connection.AllowLeveling = true;

				// set the baby stepping back to the last known good value
				printer.Settings.ForTools<double>(SettingsKey.baby_step_z_offset, (key, value, i) =>
				{
					printer.Settings.SetValue(key, babySteppingValue[i].ToString());
				});

				RetractProbe();
			}
		}

		private void Connection_PrintCanceled(object sender, EventArgs e)
		{
			CancelValidation();
		}

		private void RetractProbe()
		{
			// make sure we raise the probe on close
			if (printer.Settings.Helpers.ProbeBeingUsed
				&& printer.Settings.GetValue<bool>(SettingsKey.has_z_servo))
			{
				// make sure the servo is retracted
				var servoRetract = printer.Settings.GetValue<double>(SettingsKey.z_servo_retracted_angle);
				printer.Connection.QueueLine($"M280 P0 S{servoRetract}");
			}
		}

		private void SampleProbePoints()
		{
			if (waitingToCompleteNextSample)
			{
				return;
			}

			double startProbeHeight = printer.Settings.GetValue<double>(SettingsKey.print_leveling_probe_start) + ZProbePrintCalibrationPartPage.CalibrationObjectHeight(printer);

			if (!dataCollected)
			{
				var validProbePosition2D = PrintLevelingWizard.EnsureInPrintBounds(printer, printer.Bed.BedCenter);
				positionToSample = new Vector3(validProbePosition2D, startProbeHeight);

				this.SamplePoint();
			}
			else
			{
				SaveSamplePoints();
				CancelValidation();
			}
		}

		private void GetZProbeHeight(object sender, string line)
		{
			if (line != null)
			{
				double sampleRead = double.MinValue;
				if (line.StartsWith("Bed")) // marlin G30 return code (looks like: 'Bed Position X:20 Y:32 Z:.01')
				{
					sampledPosition.X = positionToSample.X;
					sampledPosition.Y = positionToSample.Y;
					GCodeFile.GetFirstNumberAfter("Z:", line, ref sampleRead);
				}
				else if (line.StartsWith("Z:")) // smoothie G30 return code (looks like: 'Z:10.01')
				{
					sampledPosition.X = positionToSample.X;
					sampledPosition.Y = positionToSample.Y;
					// smoothie returns the position relative to the start position
					double reportedProbeZ = 0;

					GCodeFile.GetFirstNumberAfter("Z:", line, ref reportedProbeZ);
					sampleRead = positionToSample.Z - reportedProbeZ;
				}

				if (sampleRead != double.MinValue)
				{
					samplesForSinglePosition.Add(sampleRead);

					int numberOfSamples = printer.Settings.GetValue<int>(SettingsKey.z_probe_samples);
					if (samplesForSinglePosition.Count >= numberOfSamples)
					{
						samplesForSinglePosition.Sort();
						if (samplesForSinglePosition.Count > 3)
						{
							// drop the high and low values
							samplesForSinglePosition.RemoveAt(0);
							samplesForSinglePosition.RemoveAt(samplesForSinglePosition.Count - 1);
						}

						sampledPosition.Z = Math.Round(samplesForSinglePosition.Average(), 2);

						// When probe data has been collected, resume our thread to continue collecting
						waitingToCompleteNextSample = false;
					}
					else
					{
						// add the next request for probe
						printer.Connection.QueueLine("G30");
						// raise the probe after each sample
						var feedRates = printer.Settings.Helpers.ManualMovementSpeeds();
						printer.Connection.QueueLine($"G1 X{positionToSampleWithProbeOffset.X:0.###}Y{positionToSampleWithProbeOffset.Y:0.###}Z{positionToSampleWithProbeOffset.Z:0.###} F{feedRates.X}");
					}
				}
			}
		}

		private void SaveSamplePoints()
		{
			printer.Settings.ForTools<double>(SettingsKey.baby_step_z_offset, (key, value, i) =>
			{
				printer.Settings.SetValue(key, "0");
			});
			printer.Connection.AllowLeveling = oldAllowLeveling;
		}

		private void SetupForValidation()
		{
			validationRunning = true;

			// make sure baby stepping is removed as this will be calibrated exactly (assuming it works)
			printer.Settings.ForTools<double>(SettingsKey.baby_step_z_offset, (key, value, i) =>
			{
				// remember the current baby stepping values
				babySteppingValue[i] = value;
				printer.Settings.SetValue(key, "0");
			});

			oldAllowLeveling = printer.Connection.AllowLeveling;
			// turn off print leveling
			printer.Connection.AllowLeveling = false;

			var levelingData = new PrintLevelingData()
			{
				LevelingSystem = printer.Settings.GetValue<LevelingSystem>(SettingsKey.print_leveling_solution)
			};
		}

		private void DeployServo()
		{
			if (printer.Settings.GetValue<bool>(SettingsKey.has_z_servo))
			{
				// make sure the servo is deployed
				var servoDeployCommand = printer.Settings.GetValue<double>(SettingsKey.z_servo_depolyed_angle);
				printer.Connection.QueueLine($"M280 P0 S{servoDeployCommand}");
			}
		}

		private Vector3 ProbeOffset
		{
			get => printer.Settings.GetValue<Vector3>(SettingsKey.probe_offset);
		}

		private Vector3 FeedRates
		{
			get => printer.Settings.Helpers.ManualMovementSpeeds();
		}

		private void SamplePoint()
		{
			positionToSampleWithProbeOffset = positionToSample;

			var feedRates = FeedRates;
			var probeOffset = ProbeOffset;
			// subtract out the probe offset
			// we are only interested in the xy position
			probeOffset.Z = 0;
			positionToSampleWithProbeOffset -= probeOffset;

			printer.Connection.QueueLine($"G1 Z{positionToSample.Z:0.###} F{feedRates.Z}");
			printer.Connection.QueueLine($"G1 X{positionToSampleWithProbeOffset.X:0.###}Y{positionToSampleWithProbeOffset.Y:0.###}Z{positionToSampleWithProbeOffset.Z:0.###} F{feedRates.X}");

			// probe the current position
			printer.Connection.QueueLine("G30");

			// raise the probe after each sample
			printer.Connection.QueueLine($"G1 X{positionToSampleWithProbeOffset.X:0.###}Y{positionToSampleWithProbeOffset.Y:0.###}Z{positionToSampleWithProbeOffset.Z:0.###} F{feedRates.X}");
		}

		public override void OnLoad(EventArgs args)
		{
			// register to listen to the printer
			printer.Connection.LineReceived += GetZProbeHeight;
			printer.Connection.CanceleRequested += Connection_PrintCanceled;

			// we have just completed the print of the calibration object move to the probe position and probe the top
			this.NextButton.Enabled = false;

			// make sure we are on T0
			printer.Connection.QueueLine("T0");
			// Move to the correct z height
			printer.Connection.MoveAbsolute(PrinterConnection.Axis.Z, 2, printer.Settings.Helpers.ManualMovementSpeeds().Z);

			base.OnLoad(args);
		}
	}
}