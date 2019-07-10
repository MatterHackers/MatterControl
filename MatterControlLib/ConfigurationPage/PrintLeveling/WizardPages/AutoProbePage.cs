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
using System.Threading;
using System.Threading.Tasks;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class AutoProbePage : WizardPage
	{
		private List<Vector2> probePoints;
		private int probePositionsBeingEditedIndex;

		private List<double> samples = new List<double>();
		private int numberOfSamples;
		private AutoResetEvent autoResetEvent;
		private Vector3 feedRates;
		private Vector3 adjustedProbePosition;
		private Vector3 probeStartPosition;
		private List<PrintLevelingWizard.ProbePosition> probePositions;
		private double servoDeployCommand;
		private ProbePositionsWidget probePositionsWidget;

		public AutoProbePage(ISetupWizard setupWizard, PrinterConfig printer, string headerText, List<Vector2> probePoints, List<PrintLevelingWizard.ProbePosition> probePositions)
			: base(setupWizard)
		{
			this.HeaderText = headerText;
			this.probePoints = probePoints;
			this.printer = printer;
			this.probePositions = probePositions;

			contentRow.BackgroundColor = Color.Transparent;
			contentRow.AddChild(probePositionsWidget = new ProbePositionsWidget(printer, probePoints, AppContext.Theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			});

			this.NextButton.Enabled = false;

			// Disable leveling
			printer.Connection.AllowLeveling = false;

			// Collect settings
			servoDeployCommand = printer.Settings.GetValue<double>(SettingsKey.z_servo_depolyed_angle);
			feedRates = printer.Settings.Helpers.ManualMovementSpeeds();
			numberOfSamples = printer.Settings.GetValue<int>(SettingsKey.z_probe_samples) - 1;

			autoResetEvent = new AutoResetEvent(false);

			// Register listener
			if (printer.Connection.IsConnected
				&& !(printer.Connection.Printing
				|| printer.Connection.Paused))
			{
				printer.Connection.LineReceived += GetZProbeHeight;
			}
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listener
			printer.Connection.LineReceived -= GetZProbeHeight;

			base.OnClosed(e);
		}

		private void SampleProbePoints()
		{
			double startProbeHeight = printer.Settings.GetValue<double>(SettingsKey.print_leveling_probe_start);

			for (var i = 0; i < probePoints.Count; i++)
			{
				var goalProbePosition = probePoints[i];

				if (this.HasBeenClosed)
				{
					// Make sure when the wizard is done we turn off the bed heating
					printer.Connection.TurnOffBedAndExtruders(TurnOff.AfterDelay);

					if (printer.Settings.GetValue<bool>(SettingsKey.z_homes_to_max))
					{
						printer.Connection.HomeAxis(PrinterAxis.XYZ);
					}

					break;
				}

				var validProbePosition = PrintLevelingWizard.EnsureInPrintBounds(printer, goalProbePosition);
				var probePosition = new Vector3(validProbePosition, startProbeHeight);

				//this.lastReportedPosition = printer.Connection.LastReportedPosition;

				probePositionsWidget.ActiveProbeIndex = i;
				probePositionsWidget.Invalidate();

				this.StartSampling(i, probePosition);

				autoResetEvent.WaitOne();
			}

			probePositionsWidget.ActiveProbeIndex = probePoints.Count;

			this.NextButton.Enabled = true;

			// Auto advance
			UiThread.RunOnIdle(this.NextButton.InvokeClick);
		}

		private void StartSampling(int probeIndex, Vector3 probePosition)
		{
			samples.Clear();

			probePositionsBeingEditedIndex = probeIndex;

			this.probeStartPosition = probePosition;

			if (printer.Settings.GetValue<bool>(SettingsKey.has_z_probe)
				&& printer.Settings.GetValue<bool>(SettingsKey.use_z_probe)
				&& printer.Settings.GetValue<bool>(SettingsKey.has_z_servo))
			{
				// make sure the servo is deployed
				printer.Connection.QueueLine($"M280 P0 S{servoDeployCommand}");
			}

			adjustedProbePosition = probeStartPosition;

			// subtract out the probe offset
			var probeOffset = printer.Settings.GetValue<Vector3>(SettingsKey.probe_offset);
			// we are only interested in the xy position
			probeOffset.Z = 0;
			adjustedProbePosition -= probeOffset;

			printer.Connection.MoveAbsolute(PrinterAxis.Z, probeStartPosition.Z, feedRates.Z);
			printer.Connection.MoveAbsolute(adjustedProbePosition, feedRates.X);

			// probe the current position
			printer.Connection.QueueLine("G30");

			// raise the probe after each sample
			printer.Connection.MoveAbsolute(adjustedProbePosition, feedRates.X);
		}

		public override void OnLoad(EventArgs args)
		{
			Task.Run((Action)this.SampleProbePoints);
			base.OnLoad(args);
		}

		private void GetZProbeHeight(object sender, string line)
		{
			if (line != null)
			{
				double sampleRead = double.MinValue;
				if (line.StartsWith("Bed")) // marlin G30 return code (looks like: 'Bed Position X:20 Y:32 Z:.01')
				{
					probePositions[probePositionsBeingEditedIndex].Position.X = probeStartPosition.X;
					probePositions[probePositionsBeingEditedIndex].Position.Y = probeStartPosition.Y;
					GCodeFile.GetFirstNumberAfter("Z:", line, ref sampleRead);
				}
				else if (line.StartsWith("Z:")) // smoothie G30 return code (looks like: 'Z:10.01')
				{
					probePositions[probePositionsBeingEditedIndex].Position.X = probeStartPosition.X;
					probePositions[probePositionsBeingEditedIndex].Position.Y = probeStartPosition.Y;
					// smoothie returns the position relative to the start position
					double reportedProbeZ = 0;

					GCodeFile.GetFirstNumberAfter("Z:", line, ref reportedProbeZ);
					sampleRead = probeStartPosition.Z - reportedProbeZ;
				}

				if (sampleRead != double.MinValue)
				{
					samples.Add(sampleRead);

					int numberOfSamples = printer.Settings.GetValue<int>(SettingsKey.z_probe_samples);
					if (samples.Count >= numberOfSamples)
					{
						samples.Sort();
						if (samples.Count > 3)
						{
							// drop the high and low values
							samples.RemoveAt(0);
							samples.RemoveAt(samples.Count - 1);
						}

						probePositions[probePositionsBeingEditedIndex].Position.Z = Math.Round(samples.Average(), 2);

						// When probe data has been collected, resume our thread to continue collecting
						autoResetEvent.Set();
					}
					else if (!this.HasBeenClosed)
					{
						// add the next request for probe
						printer.Connection.QueueLine("G30");
						// raise the probe after each sample
						printer.Connection.MoveAbsolute(adjustedProbePosition, feedRates.X);
					}
				}
			}
		}
	}
}