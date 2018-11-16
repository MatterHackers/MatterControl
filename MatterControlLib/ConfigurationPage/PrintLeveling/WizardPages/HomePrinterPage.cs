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
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class HomePrinterPage : PrinterSetupWizardPage
	{
		private bool autoAdvance;

		public HomePrinterPage(PrinterSetupWizard context, string headerText, string instructionsText, bool autoAdvance)
			: base(context, headerText, instructionsText)
		{
			this.autoAdvance = autoAdvance;
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Connection.CommunicationStateChanged -= CheckHomeFinished;

			base.OnClosed(e);
		}

		public override void PageIsBecomingActive()
		{
			printer.Connection.CommunicationStateChanged += CheckHomeFinished;

			printer.Connection.HomeAxis(PrinterConnection.Axis.XYZ);

			if(!printer.Settings.GetValue<bool>(SettingsKey.z_homes_to_max))
			{
				// move so we don't heat the printer while the nozzle is touching the bed
				printer.Connection.MoveAbsolute(PrinterConnection.Axis.Z, 10, printer.Settings.Helpers.ManualMovementSpeeds().Z);
			}

			if (autoAdvance)
			{
				NextButton.Enabled = false;
			}

			base.PageIsBecomingActive();
		}

		private void CheckHomeFinished(object sender, EventArgs e)
		{
			if (printer.Connection.DetailedPrintingState != DetailedPrintingState.HomingAxis)
			{
				NextButton.Enabled = true;

				if (printer.Settings.Helpers.UseZProbe())
				{
					UiThread.RunOnIdle(() => NextButton.InvokeClick());
				}
			}
		}

		public override void PageIsBecomingInactive()
		{
			NextButton.Enabled = true;

			base.PageIsBecomingInactive();
		}
	}
}