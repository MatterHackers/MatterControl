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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class CancelButton : FlowLayoutWidget
	{
		private Button cancelPrintButton;
		private PrinterConfig printer;
		private EventHandler unregisterEvents;

		public CancelButton(PrinterConfig printer, ThemeConfig theme)
		{
			var defaultMargin = theme.ButtonSpacing;

			this.printer = printer;

			cancelPrintButton = theme.ButtonFactory.Generate("Cancel".Localize().ToUpper(), AggContext.StaticData.LoadIcon("icon_stop_32x32.png", 14, 14, IconColor.Theme));
			cancelPrintButton.ToolTipText = "Stop the current print".Localize();
			cancelPrintButton.Name = "Cancel Print Button";
			cancelPrintButton.Margin = defaultMargin;
			cancelPrintButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				ApplicationController.Instance.ConditionalCancelPrint();
				SetButtonStates();
			});
			this.AddChild(cancelPrintButton);

			printer.Connection.CommunicationStateChanged.RegisterEvent((s, e) =>
			{
				UiThread.RunOnIdle(SetButtonStates);
			}, ref unregisterEvents);

			SetButtonStates();
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		protected void SetButtonStates()
		{
			switch (printer.Connection.CommunicationState)
			{
				case CommunicationStates.PreparingToPrint:
				case CommunicationStates.PrintingFromSd:
				case CommunicationStates.Printing:
				case CommunicationStates.Paused:
					cancelPrintButton.Enabled = !printer.Connection.PrintWasCanceled;
					break;

				default:
					cancelPrintButton.Enabled = false;
					break;
			}
		}
	}
}