using System;
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
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;

using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.ActionBar
{
    class PrinterActionRow : ActionRowBase
    {
        TextImageButtonFactory actionBarButtonFactory = new TextImageButtonFactory();
        Button connectPrinterButton;
        Button disconnectPrinterButton;
        Button selectActivePrinterButton;

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
            if (ApplicationWidget.Instance.WidescreenMode)
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
            if (ApplicationWidget.Instance.WidescreenMode)
            {
                disconnectPrinterButton.Margin = new BorderDouble(0, 0, 3, 3);
            }
            else
            {
                disconnectPrinterButton.Margin = new BorderDouble(6, 0, 3, 3);
            }
            disconnectPrinterButton.VAnchor = VAnchor.ParentTop;
            disconnectPrinterButton.Visible = false;
            disconnectPrinterButton.Cursor = Cursors.Hand;

            selectActivePrinterButton = new PrinterSelectButton();
            selectActivePrinterButton.HAnchor = HAnchor.ParentLeftRight;
            selectActivePrinterButton.Cursor = Cursors.Hand;
            if (ApplicationWidget.Instance.WidescreenMode)
            {
                selectActivePrinterButton.Margin = new BorderDouble(0, 6,0,3);
            }
            else
            {
                selectActivePrinterButton.Margin = new BorderDouble(0, 6, 6, 3);
            }
            

            actionBarButtonFactory.invertImageLocation = true;

            this.AddChild(connectPrinterButton);
            this.AddChild(disconnectPrinterButton);
            this.AddChild(selectActivePrinterButton);
            //this.AddChild(CreateOptionsMenu());
        }

        event EventHandler unregisterEvents;
        protected override void AddHandlers()
        {
            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(ReloadPrinterSelectionWidget, ref unregisterEvents);
            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(onActivePrinterChanged, ref unregisterEvents);
            PrinterCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
            PrinterCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);

            selectActivePrinterButton.Click += new ButtonBase.ButtonEventHandler(onSelectActivePrinterButton_Click);
            connectPrinterButton.Click += new ButtonBase.ButtonEventHandler(onConnectButton_Click);
            disconnectPrinterButton.Click += new ButtonBase.ButtonEventHandler(onDisconnectButtonClick);

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

        void onConnectButton_Click(object sender, MouseEventArgs mouseEvent)
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

        void ConnectToActivePrinter()
        {            
            PrinterCommunication.Instance.HaltConnectionThread();
            PrinterCommunication.Instance.ConnectToActivePrinter();
        }

        void onSelectActivePrinterButton_Click(object sender, MouseEventArgs mouseEvent)
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
        }

        void onDisconnectButtonClick(object sender, MouseEventArgs e)
        {
            UiThread.RunOnIdle(OnIdleDisconnect);
        }

        string disconnectAndCancelMessage = "Disconnect and cancel the current print?".Localize();
        string disconnectAndCancelTitle = "WARNING: Disconnecting will cancel the current print.\n\nDo you want to disconnect?".Localize();
        void OnIdleDisconnect(object state)
        {
            bool doCancel = true;
            if (PrinterCommunication.Instance.PrinterIsPrinting)
            {
                if (StyledMessageBox.ShowMessageBox(disconnectAndCancelMessage, disconnectAndCancelTitle, StyledMessageBox.MessageType.YES_NO))
                {
                    PrinterCommunication.Instance.Stop();
                }
                else
                {
                    doCancel = false;
                }
            }

            if (doCancel)
            {
                PrinterCommunication.Instance.Disable();                
                selectActivePrinterButton.Invalidate();
            }
        }

        void SetConnectionButtonVisibleState(object state)
        {            
            
            if (PrinterCommunication.Instance.PrinterIsConnected)
            {
                disconnectPrinterButton.Visible = true;
                connectPrinterButton.Visible = false;
            }
            else
            {
                disconnectPrinterButton.Visible = false;
                connectPrinterButton.Visible = true;
            }
        }

        void onPrinterStatusChanged(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(SetConnectionButtonVisibleState);      
        }
    }
}
