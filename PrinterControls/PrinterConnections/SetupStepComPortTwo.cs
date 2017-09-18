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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.SerialPortCommunication.FrostedSerial;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupStepComPortTwo : WizardPage
	{
		private string[] startingPortNames;
		private string[] currentPortNames;
		private Button nextButton;
		private Button connectButton;
		private TextWidget printerErrorMessage;

		private EventHandler unregisterEvents;
		private PrinterConfig printer;

		public SetupStepComPortTwo(PrinterConfig printer)
		{
			this.printer = printer;

			startingPortNames = FrostedSerialPort.GetPortNames();
			contentRow.AddChild(createPrinterConnectionMessageContainer());
			{
				cancelButton.Click += (s, e) => printer.Connection.HaltConnectionThread();

				//Construct buttons
				nextButton = textImageButtonFactory.Generate("Done".Localize());
				nextButton.Click += (s, e) => UiThread.RunOnIdle(Parent.Close);
				nextButton.Visible = false;

				connectButton = textImageButtonFactory.Generate("Connect".Localize());
				connectButton.Click += (s, e) =>
				{
					// Select the first port that's in GetPortNames() but not in startingPortNames
					string candidatePort = FrostedSerialPort.GetPortNames().Except(startingPortNames).FirstOrDefault();
					if (candidatePort == null)
					{
						printerErrorMessage.TextColor = RGBA_Bytes.Red;
						printerErrorMessage.Text = "Oops! Printer could not be detected ".Localize();
					}
					else
					{
						printerErrorMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;
						printerErrorMessage.Text = "Attempting to connect".Localize() + "...";

						ActiveSliceSettings.Instance.Helpers.SetComPort(candidatePort);
						printer.Connection.ConnectToActivePrinter();
						connectButton.Visible = false;
					}
				};

				printer.Connection.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);

				this.AddPageAction(nextButton);
				this.AddPageAction(connectButton);
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public FlowLayoutWidget createPrinterConnectionMessageContainer()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.VAnchor = VAnchor.Stretch;
			container.Margin = new BorderDouble(5);
			BorderDouble elementMargin = new BorderDouble(top: 5);

			string printerMessageOneText = "MatterControl will now attempt to auto-detect printer.".Localize();
			TextWidget printerMessageOne = new TextWidget(printerMessageOneText, 0, 0, 10);
			printerMessageOne.Margin = new BorderDouble(0, 10, 0, 5);
			printerMessageOne.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageOne.HAnchor = HAnchor.Stretch;
			printerMessageOne.Margin = elementMargin;

			string printerMessageFourBeg = "Connect printer and power on".Localize();
			string printerMessageFourFull = string.Format("1.) {0}.", printerMessageFourBeg);
			TextWidget printerMessageFour = new TextWidget(printerMessageFourFull, 0, 0, 12);
			printerMessageFour.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageFour.HAnchor = HAnchor.Stretch;
			printerMessageFour.Margin = elementMargin;

			string printerMessageFiveTxtBeg = "Press".Localize();
			string printerMessageFiveTxtEnd = "Connect".Localize();
			string printerMessageFiveTxtFull = string.Format("2.) {0} '{1}'.", printerMessageFiveTxtBeg, printerMessageFiveTxtEnd);
			TextWidget printerMessageFive = new TextWidget(printerMessageFiveTxtFull, 0, 0, 12);
			printerMessageFive.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageFive.HAnchor = HAnchor.Stretch;
			printerMessageFive.Margin = elementMargin;

			GuiWidget vSpacer = new GuiWidget();
			vSpacer.VAnchor = VAnchor.Stretch;

			Button manualLink = linkButtonFactory.Generate("Manual Configuration".Localize());
			manualLink.Margin = new BorderDouble(0, 5);
			manualLink.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				WizardWindow.ChangeToPage(new SetupStepComPortManual(printer));
			});

			printerErrorMessage = new TextWidget("", 0, 0, 10)
			{
				AutoExpandBoundsToText = true,
				TextColor = RGBA_Bytes.Red,
				HAnchor = HAnchor.Stretch,
				Margin = elementMargin
			};

			container.AddChild(printerMessageOne);
			container.AddChild(printerMessageFour);
			container.AddChild(printerErrorMessage);
			container.AddChild(vSpacer);
			container.AddChild(manualLink);

			container.HAnchor = HAnchor.Stretch;
			return container;
		}

		private void onPrinterStatusChanged(object sender, EventArgs e)
		{
			if (printer.Connection.PrinterIsConnected)
			{
				printerErrorMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				printerErrorMessage.Text = "Connection succeeded".Localize() + "!";
				nextButton.Visible = true;
				connectButton.Visible = false;
				UiThread.RunOnIdle(() => this?.Parent?.Close());
			}
			else if (printer.Connection.CommunicationState != CommunicationStates.AttemptingToConnect)
			{
				printerErrorMessage.TextColor = RGBA_Bytes.Red;
				printerErrorMessage.Text = "Uh-oh! Could not connect to printer.".Localize();
				connectButton.Visible = true;
				nextButton.Visible = false;
			}
		}
	}
}