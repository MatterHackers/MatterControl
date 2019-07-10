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
using System.IO;
using System.Linq;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.SerialPortCommunication.FrostedSerial;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupStepComPortTwo : DialogPage
	{
		private string[] startingPortNames;

		private GuiWidget nextButton;
		private GuiWidget connectButton;
		private TextWidget printerErrorMessage;

		private PrinterConfig printer;

		public SetupStepComPortTwo(PrinterConfig printer)
		{
			this.printer = printer;

			startingPortNames = FrostedSerialPort.GetPortNames();
			contentRow.AddChild(createPrinterConnectionMessageContainer());

			//Construct buttons
			nextButton = theme.CreateDialogButton("Done".Localize());
			nextButton.Click += (s, e) => Parent.Close();
			nextButton.Visible = false;

			connectButton = theme.CreateDialogButton("Connect".Localize());
			connectButton.Click += (s, e) =>
			{
				// Select the first port that's in GetPortNames() but not in startingPortNames
				string candidatePort = FrostedSerialPort.GetPortNames().Except(startingPortNames).FirstOrDefault();
				if (candidatePort == null)
				{
					printerErrorMessage.TextColor = Color.Red;
					printerErrorMessage.Text = "Oops! Printer could not be detected ".Localize();
				}
				else
				{
					printerErrorMessage.TextColor = theme.TextColor;
					printerErrorMessage.Text = "Attempting to connect".Localize() + "...";

					printer.Settings.Helpers.SetComPort(candidatePort);
					printer.Connection.Connect();
					connectButton.Visible = false;
				}
			};

			var backButton = theme.CreateDialogButton("<< Back".Localize());
			backButton.Click += (s, e) =>
			{
				DialogWindow.ChangeToPage(new SetupStepComPortOne(printer));
			};

			this.AddPageAction(nextButton);
			this.AddPageAction(backButton);
			this.AddPageAction(connectButton);

			// Register listeners
			printer.Connection.CommunicationStateChanged += Connection_CommunicationStateChanged;
		}

		protected override void OnCancel(out bool abortCancel)
		{
			printer.Connection.HaltConnectionThread();
			abortCancel = false;
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Connection.CommunicationStateChanged -= Connection_CommunicationStateChanged;

			base.OnClosed(e);
		}

		public FlowLayoutWidget createPrinterConnectionMessageContainer()
		{
			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(5)
			};

			var elementMargin = new BorderDouble(top: 5);

			var printerMessageOne = new TextWidget("MatterControl will now attempt to auto-detect printer.".Localize(), 0, 0, 10)
			{
				Margin = elementMargin,
				TextColor = theme.TextColor,
				HAnchor = HAnchor.Stretch
			};
			container.AddChild(printerMessageOne);

			var printerMessageFour = new TextWidget(string.Format("1.) {0}.", "Connect printer (make sure it is on)".Localize()), 0, 0, 12)
			{
				TextColor = theme.TextColor,
				HAnchor = HAnchor.Stretch,
				Margin = elementMargin
			};
			container.AddChild(printerMessageFour);


			var printerMessageFive = new TextWidget(string.Format("2.) {0} '{1}'.", "Press".Localize(), "Connect".Localize()), 0, 0, 12)
			{
				TextColor = theme.TextColor,
				HAnchor = HAnchor.Stretch,
				Margin = elementMargin
			};

			printerErrorMessage = new TextWidget("", 0, 0, 10)
			{
				AutoExpandBoundsToText = true,
				TextColor = Color.Red,
				HAnchor = HAnchor.Stretch,
				Margin = elementMargin
			};
			container.AddChild(printerErrorMessage);

			var removeImage = AggContext.StaticData.LoadImage(Path.Combine("Images", "insert usb.png")).SetPreMultiply();
			container.AddChild(new ImageWidget(removeImage)
			{
				HAnchor = HAnchor.Center,
				Margin = new BorderDouble(0, 10),
			});

			container.AddChild(new GuiWidget
			{
				VAnchor = VAnchor.Stretch
			});

			container.HAnchor = HAnchor.Stretch;
			return container;
		}

		private void Connection_CommunicationStateChanged(object sender, EventArgs e)
		{
			if (printer.Connection.IsConnected)
			{
				printerErrorMessage.TextColor = theme.TextColor;
				printerErrorMessage.Text = "Connection succeeded".Localize() + "!";
				nextButton.Visible = true;
				connectButton.Visible = false;
				this?.Parent?.Close();
			}
			else if (printer.Connection.CommunicationState != CommunicationStates.AttemptingToConnect)
			{
				printerErrorMessage.TextColor = Color.Red;
				printerErrorMessage.Text = "Uh-oh! Could not connect to printer.".Localize();
				connectButton.Visible = true;
				nextButton.Visible = false;
			}
		}
	}
}