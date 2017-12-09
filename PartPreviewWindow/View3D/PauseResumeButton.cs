/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PauseResumeButton : FlowLayoutWidget
	{
		private EventHandler unregisterEvents;
		private PrinterConfig printer;
		GuiWidget pausePrintButton;
		GuiWidget resumePrintButton;

		public PauseResumeButton(PrinterActionsBar printerActionsBar, PrinterConfig printer, ThemeConfig theme)
		{
			var defaultMargin = theme.ButtonSpacing;

			this.printer = printer;

			// add the pause / resume button
			pausePrintButton = theme.ButtonFactory.Generate("Pause".Localize().ToUpper());
			pausePrintButton.ToolTipText = "Pause the current print".Localize();
			pausePrintButton.Margin = defaultMargin;
			pausePrintButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(printer.Connection.RequestPause);
				pausePrintButton.Enabled = false;
			};
			this.AddChild(pausePrintButton);

			resumePrintButton = theme.ButtonFactory.Generate("Resume".Localize().ToUpper());
			resumePrintButton.ToolTipText = "Resume the current print".Localize();
			resumePrintButton.Margin = defaultMargin;
			resumePrintButton.Name = "Resume Button";
			resumePrintButton.Click += (s, e) =>
			{
				if (printer.Connection.PrinterIsPaused)
				{
					printer.Connection.Resume();
				}
				pausePrintButton.Enabled = true;
			};
			this.AddChild(resumePrintButton);

			printer.Connection.CommunicationStateChanged.RegisterEvent((s, e) =>
			{
				UiThread.RunOnIdle(SetButtonStates);
			}, ref unregisterEvents);

			SetButtonStates();
		}

		protected void SetButtonStates()
		{
			switch (printer.Connection.CommunicationState)
			{
				case CommunicationStates.PrintingFromSd:
				case CommunicationStates.Printing:
					pausePrintButton.Visible = true;
					resumePrintButton.Visible = false;
					break;

				case CommunicationStates.Paused:
					resumePrintButton.Visible = true;
					pausePrintButton.Visible = false;
					break;

				default:
					pausePrintButton.Visible = true;
					pausePrintButton.Enabled = false;
					resumePrintButton.Visible = false;
					break;
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}