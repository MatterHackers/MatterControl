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

using System.Collections.Generic;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class CalibrateProbeLastPagelInstructions : LevelingWizardPage
	{
		private List<ProbePosition> autoProbePositions;
		private List<ProbePosition> manualProbePositions;

		public CalibrateProbeLastPagelInstructions(PrinterConfig printer, LevelingWizardContext context, string headerText, string instructionsText,
			List<ProbePosition> autoProbePositions,
			List<ProbePosition> manualProbePositions)
			: base(printer, context, headerText, instructionsText)
		{
			this.autoProbePositions = autoProbePositions;
			this.manualProbePositions = manualProbePositions;

			this.ShowWizardFinished();
		}

		public override void PageIsBecomingActive()
		{
			// make sure we don't have leveling data
			double newProbeOffset = autoProbePositions[0].position.Z - manualProbePositions[0].position.Z;
			printer.Settings.SetValue(SettingsKey.z_probe_z_offset, newProbeOffset.ToString("0.###"));
			printer.Settings.SetValue(SettingsKey.probe_has_been_calibrated, "1");

			if (printer.Settings.GetValue<bool>(SettingsKey.z_homes_to_max))
			{
				printer.Connection.HomeAxis(PrinterConnection.Axis.XYZ);
			}

			// TODO: Why not use OnClosed?
			this.Closed += (s, e) =>
			{
				// move from this wizard to the print leveling wizard if needed
				ApplicationController.Instance.RunAnyRequiredPrinterSetup(printer, theme);
			};

			base.PageIsBecomingActive();
		}
	}
}