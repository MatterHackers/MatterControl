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
using System.Collections.Generic;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.SerialPortCommunication.FrostedSerial;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupStepComPortManual : DialogPage
	{
		private GuiWidget nextButton;
		private GuiWidget connectButton;
		private GuiWidget refreshButton;
		private GuiWidget printerComPortHelpLink;

		private bool printerComPortIsAvailable = false;

		private TextWidget printerComPortHelpMessage;
		private TextWidget printerComPortError;

		private List<SerialPortIndexRadioButton> serialPortButtonsList = new List<SerialPortIndexRadioButton>();

		private PrinterConfig printer;

		public SetupStepComPortManual(PrinterConfig printer)
		{
			this.printer = printer;

			FlowLayoutWidget printerComPortContainer = CreateComPortContainer();
			contentRow.AddChild(printerComPortContainer);

			// Construct buttons
			nextButton = theme.CreateDialogButton("Done".Localize());
			nextButton.Click += (s, e) => Parent.Close();
			nextButton.Visible = false;

			connectButton = theme.CreateDialogButton("Connect".Localize());
			connectButton.Click += (s, e) =>
			{
				try
				{
					printerComPortHelpLink.Visible = false;
					printerComPortError.TextColor = theme.TextColor;

					printerComPortError.Text = "Attempting to connect".Localize() + "...";
					printerComPortError.TextColor = theme.TextColor;

					printer.Connection.ConnectionFailed += Connection_CommunicationStateChanged;
					printer.Connection.ConnectionSucceeded += Connection_CommunicationStateChanged;

					printer.Settings.Helpers.SetComPort(GetSelectedSerialPort());
					printer.Connection.Connect();

					connectButton.Visible = false;
					refreshButton.Visible = false;
				}
				catch
				{
					printerComPortHelpLink.Visible = false;
					printerComPortError.TextColor = Color.Red;
					printerComPortError.Text = "Oops! Please select a serial port.".Localize();
				}
			};

			refreshButton = theme.CreateDialogButton("Refresh".Localize());
			refreshButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				DialogWindow.ChangeToPage(new SetupStepComPortManual(printer));
			});

			this.AddPageAction(nextButton);
			this.AddPageAction(connectButton);
			this.AddPageAction(refreshButton);

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
			printer.Connection.ConnectionFailed -= Connection_CommunicationStateChanged;
			printer.Connection.ConnectionSucceeded -= Connection_CommunicationStateChanged;

			base.OnClosed(e);
		}

		private FlowLayoutWidget CreateComPortContainer()
		{
			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(0),
				VAnchor = VAnchor.Stretch
			};

			BorderDouble elementMargin = new BorderDouble(top: 3);

			var comPortLabel = new TextWidget("Serial Port".Localize() + ":", 0, 0, 12)
			{
				TextColor = theme.TextColor,
				Margin = new BorderDouble(0, 0, 0, 10),
				HAnchor = HAnchor.Stretch
			};

			var serialPortContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			CreateSerialPortControls(serialPortContainer, null);

			var comPortMessageContainer = new FlowLayoutWidget
			{
				Margin = elementMargin,
				HAnchor = HAnchor.Stretch
			};

			printerComPortError = new TextWidget("Currently available serial ports.".Localize(), 0, 0, 10)
			{
				TextColor = theme.TextColor,
				AutoExpandBoundsToText = true
			};

			printerComPortHelpLink = new LinkLabel("What's this?".Localize(), theme)
			{
				Margin = new BorderDouble(left: 5),
				VAnchor = VAnchor.Bottom
			};
			printerComPortHelpLink.Click += (s, e) => printerComPortHelpMessage.Visible = !printerComPortHelpMessage.Visible;

			printerComPortHelpMessage = new TextWidget("The 'Serial Port' section lists all available serial\nports on your device. Changing which USB port the printer\nis connected to may change the associated serial port.\n\nTip: If you are uncertain, unplug/plug in your printer\nand hit refresh. The new port that appears should be\nyour printer.".Localize(), 0, 0, 10)
			{
				TextColor = theme.TextColor,
				Margin = new BorderDouble(top: 10),
				Visible = false
			};

			comPortMessageContainer.AddChild(printerComPortError);
			comPortMessageContainer.AddChild(printerComPortHelpLink);

			container.AddChild(comPortLabel);
			container.AddChild(serialPortContainer);
			container.AddChild(comPortMessageContainer);
			container.AddChild(printerComPortHelpMessage);

			container.HAnchor = HAnchor.Stretch;

			return container;
		}

		private void Connection_CommunicationStateChanged(object sender, EventArgs e)
		{
			if (printer.Connection.IsConnected)
			{
				printerComPortHelpLink.Visible = false;
				printerComPortError.TextColor = theme.TextColor;
				printerComPortError.Text = "Connection succeeded".Localize() + "!";
				nextButton.Visible = true;
				connectButton.Visible = false;
				this?.Parent?.CloseOnIdle();
			}
			else if (printer.Connection.CommunicationState != CommunicationStates.AttemptingToConnect)
			{
				printerComPortHelpLink.Visible = false;
				printerComPortError.TextColor = Color.Red;
				printerComPortError.Text = "Uh-oh! Could not connect to printer.".Localize();
				connectButton.Visible = true;
				nextButton.Visible = false;
			}
		}

		protected void CreateSerialPortControls(FlowLayoutWidget comPortContainer, string activePrinterSerialPort)
		{
			int portIndex = 0;

			// Add a radio button for each filtered port
			foreach (string portName in FrostedSerialPort.GetPortNames())
			{
				SerialPortIndexRadioButton comPortOption = CreateComPortOption(portName, activePrinterSerialPort == portName);
				if (comPortOption.Checked)
				{
					printerComPortIsAvailable = true;
				}

				serialPortButtonsList.Add(comPortOption);
				comPortContainer.AddChild(comPortOption);

				portIndex++;
			}

			// Add a virtual entry for serial ports that were previously configured but are not currently connected
			if (!printerComPortIsAvailable && activePrinterSerialPort != null)
			{
				SerialPortIndexRadioButton comPortOption = CreateComPortOption(activePrinterSerialPort, true);
				comPortOption.Enabled = false;

				comPortContainer.AddChild(comPortOption);
				serialPortButtonsList.Add(comPortOption);
				portIndex++;
			}

			// If there are still no com ports show a message to that effect
			if (portIndex == 0)
			{
				var comPortOption = new TextWidget("No COM ports available".Localize())
				{
					Margin = new BorderDouble(3, 6, 5, 6),
					TextColor = theme.TextColor
				};
				comPortContainer.AddChild(comPortOption);
			}
		}

		private SerialPortIndexRadioButton CreateComPortOption(string portName, bool isActivePrinterPort)
		{
			return new SerialPortIndexRadioButton(portName, portName)
			{
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(3, 3, 5, 3),
				TextColor = theme.TextColor,
				Checked = isActivePrinterPort
			};
		}

		private string GetSelectedSerialPort()
		{
			foreach (SerialPortIndexRadioButton button in serialPortButtonsList)
			{
				if (button.Checked)
				{
					return button.PortValue;
				}
			}

			throw new Exception("Could not find a selected button.".Localize());
		}
	}
}