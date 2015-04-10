﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using MatterHackers.Agg.Image;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.PrinterCommunication;

using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.ActionBar
{
    class PrinterActionRow : ActionRowBase
    {
        TextImageButtonFactory actionBarButtonFactory = new TextImageButtonFactory();
        Button connectPrinterButton;
        Button disconnectPrinterButton;
        Button selectActivePrinterButton;
        Button resetConnectionButton;
        Button powerOnButton;
        Button powerOffButton;

        ConnectionWindow connectionWindow;
        bool connectionWindowIsOpen = false;

        protected override void Initialize()
        {
            actionBarButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
            actionBarButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
            actionBarButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

            actionBarButtonFactory.disabledTextColor = ActiveTheme.Instance.TabLabelUnselected;
            actionBarButtonFactory.disabledFillColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            actionBarButtonFactory.disabledBorderColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            actionBarButtonFactory.hoverFillColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            actionBarButtonFactory.invertImageLocation = true;
            actionBarButtonFactory.borderWidth = 0;
            this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
        }

        protected override void AddChildElements()
        {
            actionBarButtonFactory.invertImageLocation = false;
            actionBarButtonFactory.borderWidth = 1;
            if (ActiveTheme.Instance.IsDarkTheme)
            {
                actionBarButtonFactory.normalBorderColor = new RGBA_Bytes(77, 77, 77);
            }
            else
            {
                actionBarButtonFactory.normalBorderColor = new RGBA_Bytes(190, 190, 190);
            }
            actionBarButtonFactory.hoverBorderColor = new RGBA_Bytes(128, 128, 128);

            string connectString = "Connect".Localize().ToUpper();
            connectPrinterButton = actionBarButtonFactory.Generate(connectString, "icon_power_32x32.png");
            if (ApplicationController.Instance.WidescreenMode)
            {
                connectPrinterButton.Margin = new BorderDouble(0, 0, 3, 3);
            }
            else
            {
                connectPrinterButton.Margin = new BorderDouble(6, 0, 3, 3);
            }
            connectPrinterButton.VAnchor = VAnchor.ParentTop;
            connectPrinterButton.Cursor = Cursors.Hand;

            string disconnectString = "Disconnect".Localize().ToUpper();
            disconnectPrinterButton = actionBarButtonFactory.Generate(disconnectString, "icon_power_32x32.png");
            if (ApplicationController.Instance.WidescreenMode)
            {
                disconnectPrinterButton.Margin = new BorderDouble(0, 0, 3, 3);
            }
            else
            {
                disconnectPrinterButton.Margin = new BorderDouble(6, 0, 3, 3);
            }
            disconnectPrinterButton.VAnchor = VAnchor.ParentTop;
            disconnectPrinterButton.Cursor = Cursors.Hand;

            string powerOnString = "On".Localize().ToUpper();
            powerOnButton = actionBarButtonFactory.Generate(powerOnString, "icon_power_32x32.png");
            if (ApplicationController.Instance.WidescreenMode) {
                powerOnButton.Margin = new BorderDouble(0, 0, 3, 3);
            }
            else {
                powerOnButton.Margin = new BorderDouble(6, 0, 3, 3);
            }
            connectPrinterButton.VAnchor = VAnchor.ParentTop;
            connectPrinterButton.Cursor = Cursors.Hand;

            string powerOffString = "Off".Localize().ToUpper();
            powerOffButton = actionBarButtonFactory.Generate(powerOffString, "icon_power_32x32.png");
            if (ApplicationController.Instance.WidescreenMode) {
                powerOffButton.Margin = new BorderDouble(0, 0, 3, 3);
            }
            else {
                powerOffButton.Margin = new BorderDouble(6, 0, 3, 3);
            }
            connectPrinterButton.VAnchor = VAnchor.ParentTop;
            connectPrinterButton.Cursor = Cursors.Hand;

            selectActivePrinterButton = new PrinterSelectButton();
            selectActivePrinterButton.HAnchor = HAnchor.ParentLeftRight;
            selectActivePrinterButton.Cursor = Cursors.Hand;
            if (ApplicationController.Instance.WidescreenMode)
            {
                selectActivePrinterButton.Margin = new BorderDouble(0, 6,0,3);
            }
            else
            {
                selectActivePrinterButton.Margin = new BorderDouble(0, 6, 6, 3);
            }

            string resetConnectionText = "Reset\nConnection".Localize().ToUpper();
            resetConnectionButton = actionBarButtonFactory.Generate(resetConnectionText, "e_stop4.png");
            if (ApplicationController.Instance.WidescreenMode)
            {
                resetConnectionButton.Margin = new BorderDouble(0, 0, 3, 3);
            }
            else
            {
                resetConnectionButton.Margin = new BorderDouble(6, 0, 3, 3);
            }
            
            // Bind connect button states to active printer state
            this.SetConnectionButtonVisibleState(null);

            actionBarButtonFactory.invertImageLocation = true;

            this.AddChild(connectPrinterButton);
            this.AddChild(disconnectPrinterButton);
            this.AddChild(powerOnButton);
            this.AddChild(powerOffButton);
            this.AddChild(selectActivePrinterButton);
            this.AddChild(resetConnectionButton);
            //this.AddChild(CreateOptionsMenu());
        }

        event EventHandler unregisterEvents;
        protected override void AddHandlers()
        {
            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(ReloadPrinterSelectionWidget, ref unregisterEvents);
            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(onActivePrinterChanged, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.PowerStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);

            selectActivePrinterButton.Click += new EventHandler(onSelectActivePrinterButton_Click);
            connectPrinterButton.Click += new EventHandler(onConnectButton_Click);
            disconnectPrinterButton.Click += new EventHandler(onDisconnectButtonClick);
            resetConnectionButton.Click += new EventHandler(resetConnectionButton_Click);
            powerOnButton.Click += new EventHandler(onPowerOnButton_Click);
            powerOffButton.Click += new EventHandler(onPowerOffButton_Click);

            base.AddHandlers();
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        void onPowerOnButton_Click(object sender, EventArgs mouseEvent) {
            Button buttonClicked = ((Button)sender);
            if (buttonClicked.Enabled) {
                PrinterConnectionAndCommunication.Instance.PowerisOn = true;
            }
        }

        void onPowerOffButton_Click(object sender, EventArgs mouseEvent) {
            Button buttonClicked = ((Button)sender);
            if (buttonClicked.Enabled) {
                PrinterConnectionAndCommunication.Instance.PowerisOn = false;
            }
        }

        void onConnectButton_Click(object sender, EventArgs mouseEvent)
        {
            Button buttonClicked = ((Button)sender);  
            if (buttonClicked.Enabled)
            {
                if (ActivePrinterProfile.Instance.ActivePrinter == null)
                {
                    OpenConnectionWindow(ConnectToActivePrinter);
                }
                else
                {
                    ConnectToActivePrinter();
                }

            }
        }

        void resetConnectionButton_Click(object sender, EventArgs mouseEvent)
        {
            PrinterConnectionAndCommunication.Instance.RebootBoard();
        }


        void ConnectToActivePrinter()
        {            
            PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
            PrinterConnectionAndCommunication.Instance.ConnectToActivePrinter();
        }

        void onSelectActivePrinterButton_Click(object sender, EventArgs mouseEvent)
        {
            OpenConnectionWindow();
        }

        public delegate void ConnectOnSelectFunction();
        ConnectOnSelectFunction functionToCallOnSelect;
        void OpenConnectionWindow(ConnectOnSelectFunction functionToCallOnSelect = null)
        {
            if (this.connectionWindowIsOpen == false)
            {
                connectionWindow = new ConnectionWindow();
                this.connectionWindowIsOpen = true;

				//This function gets called on printer selection (see onActivePrinterChanged)
                this.functionToCallOnSelect = functionToCallOnSelect;

                connectionWindow.Closed += new EventHandler(ConnectionWindow_Closed);
            }
            else
            {
                if (connectionWindow != null)
                {
                    connectionWindow.BringToFront();
                }
            }
        }

        void ConnectionWindow_Closed(object sender, EventArgs e)
        {
            this.connectionWindowIsOpen = false;
        }

        void ReloadPrinterSelectionWidget(object sender, EventArgs e)
        {
            //selectActivePrinterButton.Invalidate();
        }

        void onActivePrinterChanged(object sender, EventArgs e)
        {
            connectPrinterButton.Enabled = true;
            if (functionToCallOnSelect != null)
            {
                functionToCallOnSelect();
                functionToCallOnSelect = null;
            }
            if (PrinterConnectionAndCommunication.Instance.HasPower) {
                powerOnButton.Visible = true;
                powerOnButton.Enabled  = false;
                powerOffButton.Visible = false;
            }
            else {
                powerOnButton.Visible = false;
                powerOffButton.Visible = false;
            }
        }

        void onDisconnectButtonClick(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(OnIdleDisconnect);
        }

        string disconnectAndCancelMessage = "Disconnect and cancel the current print?".Localize();
        string disconnectAndCancelTitle = "WARNING: Disconnecting will cancel the print.".Localize();
        void OnIdleDisconnect(object state)
        {            
            if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting)
            {
                StyledMessageBox.ShowMessageBox(onConfirmStopPrint, disconnectAndCancelMessage, disconnectAndCancelTitle, StyledMessageBox.MessageType.YES_NO);                
            }
            else
            {
                PrinterConnectionAndCommunication.Instance.Disable();                
                selectActivePrinterButton.Invalidate();
            }
        }

        void onConfirmStopPrint(bool messageBoxResponse)
        {
            if (messageBoxResponse)
            {
                PrinterConnectionAndCommunication.Instance.Stop();
                PrinterConnectionAndCommunication.Instance.Disable();
                selectActivePrinterButton.Invalidate();
            }
        }

        
        void SetConnectionButtonVisibleState(object state)
        {            
            
            if (PrinterConnectionAndCommunication.Instance.PrinterIsConnected)
            {
                disconnectPrinterButton.Visible = true;
                connectPrinterButton.Visible = false;

                if (PrinterConnectionAndCommunication.Instance.HasPower) {
                    if (PrinterConnectionAndCommunication.Instance.PowerisOn) {
                        powerOnButton.Visible = false;
                        powerOnButton.Enabled = false;
                        powerOffButton.Visible = true;
                        powerOffButton.Enabled = true;
                    }
                    else {
                        powerOnButton.Visible = true;
                        powerOnButton.Enabled = true;
                        powerOffButton.Visible = false;
                        powerOffButton.Enabled = false;
                    }
                }
                else {
                    powerOnButton.Visible = false;
                    powerOffButton.Visible = false;
                }

            }
            else
            {
                disconnectPrinterButton.Visible = false;
                connectPrinterButton.Visible = true;

                if (PrinterConnectionAndCommunication.Instance.HasPower) {
                    powerOnButton.Visible = true;
                    powerOnButton.Enabled = false;
                    powerOffButton.Visible = false;
                }
                else {
                    powerOnButton.Visible = false;
                    powerOffButton.Visible = false;
                }

            }

            var communicationState = PrinterConnectionAndCommunication.Instance.CommunicationState;

            // Ensure connect buttons are locked while long running processes are executing to prevent duplicate calls into said actions
            connectPrinterButton.Enabled = communicationState != PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect;
            disconnectPrinterButton.Enabled = communicationState != PrinterConnectionAndCommunication.CommunicationStates.Disconnecting;
            resetConnectionButton.Visible = ActiveSliceSettings.Instance.ShowResetConnection();
        }

        void onPrinterStatusChanged(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(SetConnectionButtonVisibleState);
        }
    }
}
