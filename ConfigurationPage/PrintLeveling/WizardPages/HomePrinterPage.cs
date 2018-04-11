/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.PrinterCommunication;
using System;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class HomePrinterPage : InstructionsPage
	{
		protected WizardControl container;
		private EventHandler unregisterEvents;
		bool autoAdvance;

		public HomePrinterPage(PrinterConfig printer, WizardControl container, string pageDescription, string instructionsText, bool autoAdvance, ThemeConfig theme)
			: base(printer, pageDescription, instructionsText, theme)
		{
			this.autoAdvance = autoAdvance;
			this.container = container;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public override void PageIsBecomingActive()
		{
			// make sure we don't have anything left over
			unregisterEvents?.Invoke(this, null);

			printer.Connection.CommunicationStateChanged.RegisterEvent(CheckHomeFinished, ref unregisterEvents);

			printer.Connection.HomeAxis(PrinterConnection.Axis.XYZ);

			if (autoAdvance)
			{
				container.nextButton.Enabled = false;
			}

			base.PageIsBecomingActive();
		}

		private void CheckHomeFinished(object sender, EventArgs e)
		{
			if(printer.Connection.DetailedPrintingState != DetailedPrintingState.HomingAxis)
			{
				unregisterEvents?.Invoke(this, null);
				container.nextButton.Enabled = true;

				if (printer.Settings.Helpers.UseZProbe())
				{
					UiThread.RunOnIdle(() => container.nextButton.OnClick(null));
				}
			}
		}

		public override void PageIsBecomingInactive()
		{
			unregisterEvents?.Invoke(this, null);
			container.nextButton.Enabled = true;

			base.PageIsBecomingInactive();
		}
	}
}