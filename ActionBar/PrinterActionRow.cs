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
            actionBarButtonFactory.normalTextColor = RGBA_Bytes.White;
            actionBarButtonFactory.hoverTextColor = RGBA_Bytes.White;
            actionBarButtonFactory.pressedTextColor = RGBA_Bytes.White;

            actionBarButtonFactory.disabledTextColor = RGBA_Bytes.LightGray;
            actionBarButtonFactory.disabledFillColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            actionBarButtonFactory.disabledBorderColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            actionBarButtonFactory.invertImageLocation = true;
            actionBarButtonFactory.borderWidth = 0;
            this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
        }

        protected override void AddChildElements()
        {
            actionBarButtonFactory.invertImageLocation = false;
            string connectString = new LocalizedString("Connect").Translated;
            connectPrinterButton = actionBarButtonFactory.Generate(connectString, "icon_power_32x32.png");
            connectPrinterButton.Margin = new BorderDouble(3, 0);
            connectPrinterButton.VAnchor = VAnchor.ParentCenter;
            connectPrinterButton.Cursor = Cursors.Hand;

            string disconnectString = new LocalizedString("Disconnect").Translated;
            disconnectPrinterButton = actionBarButtonFactory.Generate(disconnectString, "icon_power_32x32.png");
            disconnectPrinterButton.Margin = new BorderDouble(3, 0);
            disconnectPrinterButton.VAnchor = VAnchor.ParentCenter;
            disconnectPrinterButton.Visible = false;
            disconnectPrinterButton.Cursor = Cursors.Hand;

            selectActivePrinterButton = new PrinterSelectButton();
            selectActivePrinterButton.HAnchor = HAnchor.ParentLeftRight;
            selectActivePrinterButton.Cursor = Cursors.Hand;

            actionBarButtonFactory.invertImageLocation = true;

            this.AddChild(connectPrinterButton);
            this.AddChild(disconnectPrinterButton);
            this.AddChild(selectActivePrinterButton);
            this.AddChild(CreateOptionsMenu());
        }

        GuiWidget CreateOptionsMenu()
        {
            ImageBuffer gearImage = new ImageBuffer();
            string imagePathAndFile = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "gear_icon.png");
            ImageBMPIO.LoadImageData(imagePathAndFile, gearImage);

            FlowLayoutWidget leftToRight = new FlowLayoutWidget();
            leftToRight.Margin = new BorderDouble(5, 0);
            string optionsString = new LocalizedString("Options").Translated;
            TextWidget optionsText = new TextWidget(optionsString, textColor: RGBA_Bytes.White);
            optionsText.VAnchor = Agg.UI.VAnchor.ParentCenter;
            optionsText.Margin = new BorderDouble(0, 0, 3, 0);
            leftToRight.AddChild(optionsText);
            GuiWidget gearWidget = new ImageWidget(gearImage);
            gearWidget.VAnchor = Agg.UI.VAnchor.ParentCenter;
            leftToRight.AddChild(gearWidget);
            leftToRight.HAnchor = HAnchor.FitToChildren;
            leftToRight.VAnchor = VAnchor.FitToChildren;

            Menu optionMenu = new Menu(leftToRight);
            optionMenu.OpenOffset = new Vector2(-2, -10);
            optionMenu.VAnchor = Agg.UI.VAnchor.ParentCenter;
            optionMenu.MenuItems.Add(new MenuItem(new ThemeColorSelectorWidget()));

            return optionMenu;
        }

        event EventHandler unregisterEvents;
        protected override void AddHandlers()
        {
            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(ReloadPrinterSelectionWidget, ref unregisterEvents);
            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(onActivePrinterChanged, ref unregisterEvents);
            PrinterCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
            PrinterCommunication.Instance.ConnectionStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);

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
                    OpenConnectionWindow();
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

        void OpenConnectionWindow()
        {
            if (this.connectionWindowIsOpen == false)
            {
                connectionWindow = new ConnectionWindow();
                this.connectionWindowIsOpen = true;
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
        }

        void onDisconnectButtonClick(object sender, MouseEventArgs e)
        {
            UiThread.RunOnIdle(OnIdleDisconnect);
        }
        void OnIdleDisconnect(object state)
        {
            bool doCancel = true;
            if (PrinterCommunication.Instance.PrinterIsPrinting)
            {
                if (StyledMessageBox.ShowMessageBox("Disconnect and cancel the current print?", "WARNING: Disconneccting will cancel the current print.\n\nDo you want to disconnect?", StyledMessageBox.MessageType.YES_NO))
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
                disconnectPrinterButton.Visible = false;
                connectPrinterButton.Visible = true;
                connectPrinterButton.Enabled = true;
                selectActivePrinterButton.Invalidate();
            }
        }

        void onPrinterStatusChanged(object sender, EventArgs e)
        {
            if (PrinterCommunication.Instance.PrinterIsConnected)
            {
                onConnectionSuccess();
            }
            else 
            {
                onConnectionFailed();
            }        
        }

        void onConnectionFailed()
        {
            disconnectPrinterButton.Visible = false;
            connectPrinterButton.Visible = true;
            connectPrinterButton.Enabled = true;
        }

        void onConnectionSuccess()
        {
            disconnectPrinterButton.Visible = true;
            connectPrinterButton.Visible = false;
        }
    }
}
