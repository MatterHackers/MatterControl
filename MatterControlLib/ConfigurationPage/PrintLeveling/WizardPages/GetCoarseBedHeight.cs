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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class GetCoarseBedHeight : FindBedHeight
	{
		protected Vector3 probeStartPosition;

		public GetCoarseBedHeight(PrinterSetupWizard context, Vector3 probeStartPosition, string pageDescription, List<ProbePosition> probePositions,
			int probePositionsBeingEditedIndex, LevelingStrings levelingStrings)
			: base(context, pageDescription, "Using the [Z] controls on this screen, we will now take a coarse measurement of the extruder height at this position.".Localize(),
				  levelingStrings.CoarseInstruction2, 1, probePositions, probePositionsBeingEditedIndex)
		{
			this.probeStartPosition = probeStartPosition;
		}

		public override void PageIsBecomingActive()
		{
			base.PageIsBecomingActive();

			// make sure the probe is not deployed
			if (printer.Settings.GetValue<bool>(SettingsKey.has_z_probe)
				&& printer.Settings.GetValue<bool>(SettingsKey.use_z_probe)
				&& printer.Settings.GetValue<bool>(SettingsKey.has_z_servo))
			{
				// make sure the servo is retracted
				var servoRetract = printer.Settings.GetValue<double>(SettingsKey.z_servo_retracted_angle);
				printer.Connection.QueueLine($"M280 P0 S{servoRetract}");
			}

			var feedRates = printer.Settings.Helpers.ManualMovementSpeeds();

			printer.Connection.MoveAbsolute(PrinterConnection.Axis.Z, probeStartPosition.Z, feedRates.Z);
			printer.Connection.MoveAbsolute(probeStartPosition, feedRates.X);
			printer.Connection.ReadPosition();

			NextButton.Enabled = false;

			zPlusControl.Click += zControl_Click;
			zMinusControl.Click += zControl_Click;
		}

		protected void zControl_Click(object sender, EventArgs mouseEvent)
		{
			NextButton.Enabled = true;
		}

		public override void PageIsBecomingInactive()
		{
			NextButton.Enabled = true;

			base.PageIsBecomingInactive();
		}
	}
}