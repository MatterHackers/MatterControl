﻿using System;
using System.Collections.Generic;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.SerialPortCommunication.FrostedSerial;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
    public class SetupStepComPortManual : SetupConnectionWidgetBase
    {
        List<SerialPortIndexRadioButton> SerialPortButtonsList = new List<SerialPortIndexRadioButton>();
        TextWidget printerComPortError;
        //GuiWidget comPortWidget;
        Button nextButton;
        Button connectButton;
        Button refreshButton;
        Button printerComPortHelpLink;
        TextWidget printerComPortHelpMessage;
        bool printerComPortIsAvailable = false;

        public SetupStepComPortManual(ConnectionWindow windowController, GuiWidget containerWindowToClose, PrinterSetupStatus setupPrinterStatus)
            : base(windowController, containerWindowToClose, setupPrinterStatus)
        {
            linkButtonFactory.fontSize = 8;

            FlowLayoutWidget printerComPortContainer = createComPortContainer();
            contentRow.AddChild(printerComPortContainer);
            {
                //Construct buttons
				nextButton = textImageButtonFactory.Generate(LocalizedString.Get("Done"));
                nextButton.Click += new EventHandler(NextButton_Click);
                nextButton.Visible = false;

				connectButton = textImageButtonFactory.Generate(LocalizedString.Get("Connect"));
                connectButton.Click += new EventHandler(ConnectButton_Click);

                PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);

				refreshButton = textImageButtonFactory.Generate(LocalizedString.Get("Refresh"));
                refreshButton.Click += new EventHandler(RefreshButton_Click);

                GuiWidget hSpacer = new GuiWidget();
                hSpacer.HAnchor = HAnchor.ParentLeftRight;

                //Add buttons to buttonContainer
                footerRow.AddChild(nextButton);
                footerRow.AddChild(connectButton);
                footerRow.AddChild(refreshButton);
                footerRow.AddChild(hSpacer);
                footerRow.AddChild(cancelButton);
            }
        }

        event EventHandler unregisterEvents;
        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        private FlowLayoutWidget createComPortContainer()
        {
            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            container.Margin = new BorderDouble(0);
            container.VAnchor = VAnchor.ParentBottomTop;
            BorderDouble elementMargin = new BorderDouble(top: 3);

			string serialPortLabel = LocalizedString.Get("Serial Port");
			string serialPortLabelFull = string.Format("{0}:", serialPortLabel);

			TextWidget comPortLabel = new TextWidget(serialPortLabelFull, 0, 0, 12);
            comPortLabel.TextColor = this.defaultTextColor;
            comPortLabel.Margin = new BorderDouble(0, 0, 0, 10);
            comPortLabel.HAnchor = HAnchor.ParentLeftRight;

            FlowLayoutWidget comPortWidget = GetComPortWidget();
            comPortWidget.HAnchor = HAnchor.ParentLeftRight;

            FlowLayoutWidget comPortMessageContainer = new FlowLayoutWidget();
            comPortMessageContainer.Margin = elementMargin;
            comPortMessageContainer.HAnchor = HAnchor.ParentLeftRight;

			printerComPortError = new TextWidget(LocalizedString.Get("Currently available serial ports."), 0, 0, 10);
			printerComPortError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            printerComPortError.AutoExpandBoundsToText = true;            

			printerComPortHelpLink = linkButtonFactory.Generate(LocalizedString.Get("What's this?"));
            printerComPortHelpLink.Margin = new BorderDouble(left: 5);
            printerComPortHelpLink.VAnchor = VAnchor.ParentBottom;
            printerComPortHelpLink.Click += new EventHandler(printerComPortHelp_Click);

			printerComPortHelpMessage = new TextWidget(LocalizedString.Get("The 'Serial Port' identifies which connected device is\nyour printer. Changing which usb plug you use may\nchange the associated serial port.\n\nTip: If you are uncertain, plug-in in your printer and hit\nrefresh. The new port that appears should be your\nprinter."), 0, 0, 10);
			printerComPortHelpMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            printerComPortHelpMessage.Margin = new BorderDouble(top: 10);
            printerComPortHelpMessage.Visible = false;

            comPortMessageContainer.AddChild(printerComPortError);
            comPortMessageContainer.AddChild(printerComPortHelpLink);

            container.AddChild(comPortLabel);
            container.AddChild(comPortWidget);
            container.AddChild(comPortMessageContainer);
            container.AddChild(printerComPortHelpMessage);


            container.HAnchor = HAnchor.ParentLeftRight;
            return container;
        }

        FlowLayoutWidget GetComPortWidget()
        {
            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            
            int portIndex = 0;
            foreach (string serialPort in FrostedSerialPort.GetPortNames())
            {
                //Filter com port list based on usb type (applies to Mac mostly)
                bool looks_like_mac = serialPort.StartsWith("/dev/tty.");
                bool looks_like_pc = serialPort.StartsWith("COM");
                if (looks_like_mac || looks_like_pc)
                {
                    SerialPortIndexRadioButton comPortOption = createComPortOption(serialPort);
                    container.AddChild(comPortOption);
                    portIndex++;
                }
            }

            //If there are no com ports in the filtered list assume we are missing something and show the unfiltered list
            if (portIndex == 0)
            {

                foreach (string serialPort in FrostedSerialPort.GetPortNames())
                {
                    SerialPortIndexRadioButton comPortOption = createComPortOption(serialPort);
                    container.AddChild(comPortOption);
                    portIndex++;
                }
            }

            if (!printerComPortIsAvailable && this.ActivePrinter.ComPort != null)
            {
                SerialPortIndexRadioButton comPortOption = createComPortOption(this.ActivePrinter.ComPort);
                comPortOption.Enabled = false;
                container.AddChild(comPortOption);
                portIndex++;
            }

            //If there are still no com ports show a message to that effect
            if (portIndex == 0)
            {
				TextWidget comPortOption = new TextWidget(LocalizedString.Get("No COM ports available"));
                comPortOption.Margin = new BorderDouble(3, 6, 5, 6);
                comPortOption.TextColor = this.subContainerTextColor;
                container.AddChild(comPortOption);
            }
            return container;
        }

        public SerialPortIndexRadioButton createComPortOption(string serialPort)
        {
            //Add formatting here to make port names prettier
            string portName = serialPort;

            SerialPortIndexRadioButton comPortOption = new SerialPortIndexRadioButton(portName, serialPort);
            SerialPortButtonsList.Add(comPortOption);

            comPortOption.Margin = new BorderDouble(3, 3, 5, 3);
            comPortOption.TextColor = this.subContainerTextColor;

            if (this.ActivePrinter.ComPort == serialPort)
            {
                comPortOption.Checked = true;
                printerComPortIsAvailable = true;
            }
            return comPortOption;
        }

        void onPrinterStatusChanged(object sender, EventArgs e)
        {
            if (PrinterConnectionAndCommunication.Instance.PrinterIsConnected)
            {
                onConnectionSuccess();
            }
            else if (PrinterConnectionAndCommunication.Instance.CommunicationState != PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect)
            {
                onConnectionFailed();
            }
        }

        void onConnectionFailed()
        {
            printerComPortHelpLink.Visible = false;
            printerComPortError.TextColor = RGBA_Bytes.Red;            
			printerComPortError.Text = LocalizedString.Get("Uh-oh! Could not connect to printer.");
            connectButton.Visible = true;
            nextButton.Visible = false;
        }

        void onConnectionSuccess()
        {
            printerComPortHelpLink.Visible = false;
            printerComPortError.TextColor = this.subContainerTextColor;
			printerComPortError.Text = LocalizedString.Get("Connection succeeded!");
            nextButton.Visible = true;
            connectButton.Visible = false;
        }

        void printerComPortHelp_Click(object sender, EventArgs mouseEvent)
        {
            printerComPortHelpMessage.Visible = !printerComPortHelpMessage.Visible;
        }

        void MoveToNextWidget(object state)
        {
            // you can call this like this
            //             AfterUiEvents.AddAction(new AfterUIAction(MoveToNextWidget));

            if (this.PrinterSetupStatus.DriversToInstall.Count > 0)
            {
                Parent.AddChild(new SetupStepInstallDriver((ConnectionWindow)Parent, Parent, this.PrinterSetupStatus));
                Parent.RemoveChild(this);
            }
            else
            {
                Parent.AddChild(new SetupStepComPortOne((ConnectionWindow)Parent, Parent, this.PrinterSetupStatus));
                Parent.RemoveChild(this);
            }
        }

        void RecreateCurrentWidget(object state)
        {
            Parent.AddChild(new SetupStepComPortManual((ConnectionWindow)Parent, Parent, this.PrinterSetupStatus));
            Parent.RemoveChild(this);
        }

        void RefreshButton_Click(object sender, EventArgs mouseEvent)
        {
            UiThread.RunOnIdle(RecreateCurrentWidget);
        }

        void ConnectButton_Click(object sender, EventArgs mouseEvent)
        {
            string serialPort;
            try
            {
                serialPort = GetSelectedSerialPort();
                this.ActivePrinter.ComPort = serialPort;
                this.ActivePrinter.Commit();
                printerComPortHelpLink.Visible = false;
                printerComPortError.TextColor = this.subContainerTextColor;
				string printerComPortErrorLabel = LocalizedString.Get("Attempting to connect");
				string printerComPortErrorLabelFull = string.Format("{0}...",printerComPortErrorLabel);
				printerComPortError.Text = printerComPortErrorLabelFull;
                printerComPortError.TextColor = ActiveTheme.Instance.PrimaryTextColor;

                ActivePrinterProfile.Instance.ActivePrinter = this.ActivePrinter;
                PrinterConnectionAndCommunication.Instance.ConnectToActivePrinter();
                connectButton.Visible = false;
                refreshButton.Visible = false;
            }
            catch
            {
                printerComPortHelpLink.Visible = false;
                printerComPortError.TextColor = RGBA_Bytes.Red;
				printerComPortError.Text = LocalizedString.Get("Oops! Please select a serial port.");
            }
        }


        void NextButton_Click(object sender, EventArgs mouseEvent)
        {
            UiThread.RunOnIdle((state) =>
            {
                Parent.Close();
            });
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
			throw new Exception(LocalizedString.Get("Could not find a selected button."));
        }
    }
}
