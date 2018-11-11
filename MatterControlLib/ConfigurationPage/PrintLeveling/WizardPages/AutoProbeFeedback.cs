/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class AutoProbeFeedback : PrinterSetupWizardPage
	{
		private Vector3 lastReportedPosition;
		private List<ProbePosition> probePositions;
		private int probePositionsBeingEditedIndex;

		private EventHandler unregisterEvents;
		protected Vector3 probeStartPosition;

		public AutoProbeFeedback(PrinterSetupWizard context, Vector3 probeStartPosition, string headerText, List<ProbePosition> probePositions, int probePositionsBeingEditedIndex)
			: base(context, headerText, headerText)
		{
			this.probeStartPosition = probeStartPosition;
			this.probePositions = probePositions;

			this.lastReportedPosition = printer.Connection.LastReportedPosition;
			this.probePositionsBeingEditedIndex = probePositionsBeingEditedIndex;

			var spacer = new GuiWidget(15, 15);
			contentRow.AddChild(spacer);

			FlowLayoutWidget textFields = new FlowLayoutWidget(FlowDirection.TopToBottom);
		}

		private void GetZProbeHeight(object sender, string line)
		{
			if (line != null)
			{
				double sampleRead = double.MinValue;
				if (line.StartsWith("Bed")) // marlin G30 return code (looks like: 'Bed Position X:20 Y:32 Z:.01')
				{
					probePositions[probePositionsBeingEditedIndex].position.X = probeStartPosition.X;
					probePositions[probePositionsBeingEditedIndex].position.Y = probeStartPosition.Y;
					GCodeFile.GetFirstNumberAfter("Z:", line, ref sampleRead);
				}
				else if (line.StartsWith("Z:")) // smoothie G30 return code (looks like: 'Z:10.01')
				{
					probePositions[probePositionsBeingEditedIndex].position.X = probeStartPosition.X;
					probePositions[probePositionsBeingEditedIndex].position.Y = probeStartPosition.Y;
					// smoothie returns the position relative to the start position
					double reportedProbeZ = 0;
					GCodeFile.GetFirstNumberAfter("Z:", line, ref reportedProbeZ);
					sampleRead = probeStartPosition.Z - reportedProbeZ;
				}

				if (sampleRead != double.MinValue)
				{
					samples.Add(sampleRead);

					int numberOfSamples = printer.Settings.GetValue<int>(SettingsKey.z_probe_samples);
					if (samples.Count == numberOfSamples)
					{
						samples.Sort();
						if (samples.Count > 3)
						{
							// drop the high and low values
							samples.RemoveAt(0);
							samples.RemoveAt(samples.Count - 1);
						}

						probePositions[probePositionsBeingEditedIndex].position.Z = Math.Round(samples.Average(), 2);

						UiThread.RunOnIdle(() => NextButton.InvokeClick());
					}
				}
			}
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		List<double> samples = new List<double>();

		public override void PageIsBecomingActive()
		{
			// always make sure we don't have print leveling turned on
			PrintLevelingStream.AllowLeveling = false;

			base.PageIsBecomingActive();

			if (printer.Settings.GetValue<bool>(SettingsKey.has_z_probe)
				&& printer.Settings.GetValue<bool>(SettingsKey.use_z_probe)
				&& printer.Settings.GetValue<bool>(SettingsKey.has_z_servo))
			{
				// make sure the servo is deployed
				var servoDeploy = printer.Settings.GetValue<double>(SettingsKey.z_servo_depolyed_angle);
				printer.Connection.QueueLine($"M280 P0 S{servoDeploy}");
			}

			var feedRates = printer.Settings.Helpers.ManualMovementSpeeds();

			var adjustedProbePosition = probeStartPosition;
			// subtract out the probe offset
			var probeOffset = printer.Settings.GetValue<Vector2>(SettingsKey.z_probe_xy_offset);
			adjustedProbePosition -= new Vector3(probeOffset);

			printer.Connection.MoveAbsolute(PrinterConnection.Axis.Z, probeStartPosition.Z, feedRates.Z);
			printer.Connection.MoveAbsolute(adjustedProbePosition, feedRates.X);

			int numberOfSamples = printer.Settings.GetValue<int>(SettingsKey.z_probe_samples);
			for (int i = 0; i < numberOfSamples; i++)
			{
				// probe the current position
				printer.Connection.QueueLine("G30");
				// raise the probe after each sample
				printer.Connection.MoveAbsolute(adjustedProbePosition, feedRates.X);
			}

			NextButton.Enabled = false;

			if (printer.Connection.IsConnected
				&& !(printer.Connection.PrinterIsPrinting
				|| printer.Connection.PrinterIsPaused))
			{
				printer.Connection.LineReceived += GetZProbeHeight;
			}
		}

		public override void PageIsBecomingInactive()
		{
			printer.Connection.LineReceived -= GetZProbeHeight;
			base.PageIsBecomingInactive();
		}
	}
}