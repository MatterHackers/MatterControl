using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Ports;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
    public class SetupStepComPortTwo : SetupConnectionWidgetBase
    {
        string[] startingPortNames;
        string[] currentPortNames;
        Button nextButton;
        Button connectButton;
        TextWidget printerErrorMessage;
        event EventHandler unregisterEvents;

        public SetupStepComPortTwo(ConnectionWindow windowController, GuiWidget containerWindowToClose, PrinterSetupStatus setupPrinterStatus)
            : base(windowController, containerWindowToClose, setupPrinterStatus)
        {

            startingPortNames = SerialPort.GetPortNames();
            contentRow.AddChild(createPrinterConnectionMessageContainer());
            {
                //Construct buttons
                nextButton = textImageButtonFactory.Generate("Done");
                nextButton.Click += new ButtonBase.ButtonEventHandler(NextButton_Click);
                nextButton.Visible = false;

                connectButton = textImageButtonFactory.Generate("Connect");
                connectButton.Click += new ButtonBase.ButtonEventHandler(ConnectButton_Click);

                PrinterCommunication.Instance.ConnectionStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);

                GuiWidget hSpacer = new GuiWidget();
                hSpacer.HAnchor = HAnchor.ParentLeftRight;

                //Add buttons to buttonContainer
                footerRow.AddChild(nextButton);
                footerRow.AddChild(connectButton);
                footerRow.AddChild(hSpacer);

                footerRow.AddChild(cancelButton);
            }
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        public FlowLayoutWidget createPrinterConnectionMessageContainer()
        {
            FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
            container.VAnchor = VAnchor.ParentBottomTop;
            container.Margin = new BorderDouble(5);
            BorderDouble elementMargin = new BorderDouble(top: 5);

            TextWidget printerMessageOne = new TextWidget("MatterControl will now attempt to auto-detect printer.", 0, 0, 10);
            printerMessageOne.Margin = new BorderDouble(0, 10, 0, 5);
            printerMessageOne.TextColor = RGBA_Bytes.White;
            printerMessageOne.HAnchor = HAnchor.ParentLeftRight;
            printerMessageOne.Margin = elementMargin;

            TextWidget printerMessageTwo = new TextWidget("1.) Disconnect printer (if currently connected).", 0, 0, 12);
            printerMessageTwo.TextColor = RGBA_Bytes.White;
            printerMessageTwo.HAnchor = HAnchor.ParentLeftRight;
            printerMessageTwo.Margin = elementMargin;

            TextWidget printerMessageThree = new TextWidget("2.) Press 'Continue'.", 0, 0, 12);
            printerMessageThree.TextColor = RGBA_Bytes.White;
            printerMessageThree.HAnchor = HAnchor.ParentLeftRight;
            printerMessageThree.Margin = elementMargin;

            TextWidget printerMessageFour = new TextWidget("3.) Power on and connect printer.", 0, 0, 12);
            printerMessageFour.TextColor = RGBA_Bytes.White;
            printerMessageFour.HAnchor = HAnchor.ParentLeftRight;
            printerMessageFour.Margin = elementMargin;

            TextWidget printerMessageFive = new TextWidget("4.) Press 'Connect'.", 0, 0, 12);
            printerMessageFive.TextColor = RGBA_Bytes.White;
            printerMessageFive.HAnchor = HAnchor.ParentLeftRight;
            printerMessageFive.Margin = elementMargin;

            GuiWidget vSpacer = new GuiWidget();
            vSpacer.VAnchor = VAnchor.ParentBottomTop;

            Button manualLink = linkButtonFactory.Generate("Manual Configuration");
            manualLink.Margin = new BorderDouble(0, 5);
            manualLink.Click += new ButtonBase.ButtonEventHandler(ManualLink_Click);

            printerErrorMessage = new TextWidget("", 0, 0, 10);
            printerErrorMessage.AutoExpandBoundsToText = true;
            printerErrorMessage.TextColor = RGBA_Bytes.Red;
            printerErrorMessage.HAnchor = HAnchor.ParentLeftRight;
            printerErrorMessage.Margin = elementMargin;

            container.AddChild(printerMessageOne);
            container.AddChild(printerMessageTwo);
            container.AddChild(printerMessageThree);
            container.AddChild(printerMessageFour);
            container.AddChild(printerMessageFive);
            container.AddChild(printerErrorMessage);
            container.AddChild(vSpacer);
            container.AddChild(manualLink);

            container.HAnchor = HAnchor.ParentLeftRight;
            return container;
        }

        void ManualLink_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle(MoveToManualConfiguration);
        }

        void MoveToManualConfiguration(object state)
        {
            Parent.AddChild(new SetupStepComPortManual((ConnectionWindow)Parent, Parent, this.PrinterSetupStatus));
            Parent.RemoveChild(this);
        }

        void ConnectButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            string candidatePort = null;
            currentPortNames = SerialPort.GetPortNames();
            foreach (string portName in currentPortNames)
            {
                if (!startingPortNames.Any(portName.Contains))
                {
                    candidatePort = portName;
                }
            }

            if (candidatePort == null)
            {
                printerErrorMessage.TextColor = RGBA_Bytes.Red;
                printerErrorMessage.Text = "Oops! Printer could not be detected.";                
            }
            else
            {
                ActivePrinter.ComPort = candidatePort;
                printerErrorMessage.TextColor = RGBA_Bytes.White;
                printerErrorMessage.Text = "Attempting to connect...";
                this.ActivePrinter.Commit();
                PrinterCommunication.Instance.ActivePrinter = this.ActivePrinter;
                PrinterCommunication.Instance.ConnectToActivePrinter();
                connectButton.Visible = false;                
            }     
        }


        void onPrinterStatusChanged(object sender, EventArgs e)
        {
            if (PrinterCommunication.Instance.PrinterIsConnected)
            {
                onConnectionSuccess();
            }
            else if (PrinterCommunication.Instance.CommunicationState != PrinterCommunication.CommunicationStates.AttemptingToConnect)
            {
                onConnectionFailed();
            }
        }

        void onConnectionFailed()
        {
            printerErrorMessage.TextColor = RGBA_Bytes.Red;
            printerErrorMessage.Text = "Uh-oh! Could not connect to printer.";
            connectButton.Visible = true;
            nextButton.Visible = false;
        }

        void onConnectionSuccess()
        {
            printerErrorMessage.TextColor = RGBA_Bytes.White;
            printerErrorMessage.Text = "Connection succeeded!";
            nextButton.Visible = true;
            connectButton.Visible = false;
        }

        void NextButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            Parent.Close();
        }
    }
}
