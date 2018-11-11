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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class GettingThirdPointFor2PointCalibration : PrinterSetupWizardPage
	{
		protected Vector3 probeStartPosition;
		private ProbePosition probePosition;
		private EventHandler unregisterEvents;

		public GettingThirdPointFor2PointCalibration(PrinterSetupWizard context, string pageDescription, Vector3 probeStartPosition, string instructionsText,
			ProbePosition probePosition)
			: base(context, pageDescription, instructionsText)
		{
			this.probeStartPosition = probeStartPosition;
			this.probePosition = probePosition;
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public override void PageIsBecomingActive()
		{
			// first make sure there is no leftover FinishedProbe event
			printer.Connection.LineReceived += FinishedProbe;

			var feedRates = printer.Settings.Helpers.ManualMovementSpeeds();

			printer.Connection.MoveAbsolute(PrinterConnection.Axis.Z, probeStartPosition.Z, feedRates.Z);
			printer.Connection.MoveAbsolute(probeStartPosition, feedRates.X);
			printer.Connection.QueueLine("G30");
			printer.Connection.LineReceived += FinishedProbe;

			base.PageIsBecomingActive();

			NextButton.Enabled = false;
		}

		private void FinishedProbe(object sender, string line)
		{
			if (line != null)
			{
				if (line.Contains("endstops hit"))
				{
					printer.Connection.LineReceived -= FinishedProbe;

					int zStringPos = line.LastIndexOf("Z:");
					string zProbeHeight = line.Substring(zStringPos + 2);
					probePosition.position = new Vector3(probeStartPosition.X, probeStartPosition.Y, double.Parse(zProbeHeight));
					printer.Connection.MoveAbsolute(probeStartPosition, printer.Settings.Helpers.ManualMovementSpeeds().Z);
					printer.Connection.ReadPosition();

					UiThread.RunOnIdle(() => NextButton.InvokeClick());
				}
			}
		}
	}
}