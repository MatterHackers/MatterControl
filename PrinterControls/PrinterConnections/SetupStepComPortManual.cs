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
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.SerialPortCommunication.FrostedSerial;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupStepComPortManual : WizardPage
	{
		private Button nextButton;
		private Button connectButton;
		private Button refreshButton;
		private Button printerComPortHelpLink;

		private bool printerComPortIsAvailable = false;

		private TextWidget printerComPortHelpMessage;
		private TextWidget printerComPortError;

		private EventHandler unregisterEvents;
		protected List<SerialPortIndexRadioButton> SerialPortButtonsList = new List<SerialPortIndexRadioButton>();
		private PrinterConfig printer;

		public SetupStepComPortManual(PrinterConfig printer)
		{
			this.printer = printer;

			FlowLayoutWidget printerComPortContainer = createComPortContainer();
			contentRow.AddChild(printerComPortContainer);

			//Construct buttons
			nextButton = textImageButtonFactory.Generate("Done".Localize());
			nextButton.Click += (s, e) => UiThread.RunOnIdle(Parent.Close);
			nextButton.Visible = false;

			connectButton = textImageButtonFactory.Generate("Connect".Localize());
			connectButton.Click += (s, e) =>
			{
				try
				{
					printerComPortHelpLink.Visible = false;
					printerComPortError.TextColor = ActiveTheme.Instance.PrimaryTextColor;

					printerComPortError.Text = "Attempting to connect".Localize() + "...";
					printerComPortError.TextColor = ActiveTheme.Instance.PrimaryTextColor;

					ActiveSliceSettings.Instance.Helpers.SetComPort(GetSelectedSerialPort());
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

			refreshButton = textImageButtonFactory.Generate("Refresh".Localize());
			refreshButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				WizardWindow.ChangeToPage(new SetupStepComPortManual(printer));
			});

			this.AddPageAction(nextButton);
			this.AddPageAction(connectButton);
			this.AddPageAction(refreshButton);

			printer.Connection.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
		}

		protected override void OnCancel(out bool abortCancel)
		{
			printer.Connection.HaltConnectionThread();
			abortCancel = false;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private FlowLayoutWidget createComPortContainer()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.Margin = new BorderDouble(0);
			container.VAnchor = VAnchor.Stretch;
			BorderDouble elementMargin = new BorderDouble(top: 3);

			string serialPortLabel = "Serial Port".Localize();
			string serialPortLabelFull = string.Format("{0}:", serialPortLabel);

			TextWidget comPortLabel = new TextWidget(serialPortLabelFull, 0, 0, 12);
			comPortLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			comPortLabel.Margin = new BorderDouble(0, 0, 0, 10);
			comPortLabel.HAnchor = HAnchor.Stretch;

			FlowLayoutWidget serialPortContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			CreateSerialPortControls(serialPortContainer, null);

			FlowLayoutWidget comPortMessageContainer = new FlowLayoutWidget();
			comPortMessageContainer.Margin = elementMargin;
			comPortMessageContainer.HAnchor = HAnchor.Stretch;

			printerComPortError = new TextWidget("Currently available serial ports.".Localize(), 0, 0, 10);
			printerComPortError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerComPortError.AutoExpandBoundsToText = true;

			printerComPortHelpLink = linkButtonFactory.Generate("What's this?".Localize());
			printerComPortHelpLink.Margin = new BorderDouble(left: 5);
			printerComPortHelpLink.VAnchor = VAnchor.Bottom;
			printerComPortHelpLink.Click += (s, e) => printerComPortHelpMessage.Visible = !printerComPortHelpMessage.Visible;

			printerComPortHelpMessage = new TextWidget("The 'Serial Port' section lists all available serial\nports on your device. Changing which USB port the printer\nis connected to may change the associated serial port.\n\nTip: If you are uncertain, unplug/plug in your printer\nand hit refresh. The new port that appears should be\nyour printer.".Localize(), 0, 0, 10);
			printerComPortHelpMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerComPortHelpMessage.Margin = new BorderDouble(top: 10);
			printerComPortHelpMessage.Visible = false;

			comPortMessageContainer.AddChild(printerComPortError);
			comPortMessageContainer.AddChild(printerComPortHelpLink);

			container.AddChild(comPortLabel);
			container.AddChild(serialPortContainer);
			container.AddChild(comPortMessageContainer);
			container.AddChild(printerComPortHelpMessage);

			container.HAnchor = HAnchor.Stretch;

			return container;
		}

		private void onPrinterStatusChanged(object sender, EventArgs e)
		{
			if (printer.Connection.PrinterIsConnected)
			{
				printerComPortHelpLink.Visible = false;
				printerComPortError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				printerComPortError.Text = "Connection succeeded".Localize() + "!";
				nextButton.Visible = true;
				connectButton.Visible = false;
				UiThread.RunOnIdle(() => this?.Parent?.Close());
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
			string[] portsToCreate = FrostedSerialPort.GetPortNames();

			// Add a radio button for each filtered port
			foreach (string portName in portsToCreate)
			{
				SerialPortIndexRadioButton comPortOption = createComPortOption(portName, activePrinterSerialPort == portName);
				if (comPortOption.Checked)
				{
					printerComPortIsAvailable = true;
				}

				SerialPortButtonsList.Add(comPortOption);
				comPortContainer.AddChild(comPortOption);

				portIndex++;
			}

			// Add a virtual entry for serial ports that were previously configured but are not currently connected
			if (!printerComPortIsAvailable && activePrinterSerialPort != null)
			{
				SerialPortIndexRadioButton comPortOption = createComPortOption(activePrinterSerialPort, true);
				comPortOption.Enabled = false;

				comPortContainer.AddChild(comPortOption);
				SerialPortButtonsList.Add(comPortOption);
				portIndex++;
			}

			//If there are still no com ports show a message to that effect
			if (portIndex == 0)
			{
				TextWidget comPortOption = new TextWidget("No COM ports available".Localize());
				comPortOption.Margin = new BorderDouble(3, 6, 5, 6);
				comPortOption.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				comPortContainer.AddChild(comPortOption);
			}
		}

		private SerialPortIndexRadioButton createComPortOption(string portName, bool isActivePrinterPort)
		{
			SerialPortIndexRadioButton comPortOption = new SerialPortIndexRadioButton(portName, portName)
			{
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(3, 3, 5, 3),
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Checked = isActivePrinterPort
			};
			return comPortOption;
		}

		private string GetSelectedSerialPort()
		{
			foreach (SerialPortIndexRadioButton button in SerialPortButtonsList)
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