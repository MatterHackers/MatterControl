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

            actionBarButtonFactory.invertImageLocation = true;
            actionBarButtonFactory.borderWidth = 0;
            this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
        }

        protected override void AddChildElements()
        {
            actionBarButtonFactory.invertImageLocation = false;
            string connectString = "Connect".Localize().ToUpper();
            connectPrinterButton = actionBarButtonFactory.Generate(connectString, "icon_power_32x32.png");
            connectPrinterButton.Margin = new BorderDouble(0, 0, 3);
            connectPrinterButton.VAnchor = VAnchor.ParentCenter;
            connectPrinterButton.Cursor = Cursors.Hand;

            string disconnectString = "Disconnect".Localize().ToUpper();
            disconnectPrinterButton = actionBarButtonFactory.Generate(disconnectString, "icon_power_32x32.png");
            disconnectPrinterButton.Margin = new BorderDouble(0, 0, 3);
            disconnectPrinterButton.VAnchor = VAnchor.ParentCenter;
            disconnectPrinterButton.Visible = false;
            disconnectPrinterButton.Cursor = Cursors.Hand;

            selectActivePrinterButton = new PrinterSelectButton();
            selectActivePrinterButton.HAnchor = HAnchor.ParentLeftRight;
            selectActivePrinterButton.Cursor = Cursors.Hand;
            if (ApplicationWidget.Instance.WidescreenMode)
            {
                selectActivePrinterButton.Margin = new BorderDouble(0, 6);
            }
            else
            {
                selectActivePrinterButton.Margin = new BorderDouble(0, 6, 6, 6);
            }
            

            actionBarButtonFactory.invertImageLocation = true;

            this.AddChild(connectPrinterButton);
            this.AddChild(disconnectPrinterButton);
            this.AddChild(selectActivePrinterButton);
            //this.AddChild(CreateOptionsMenu());
        }

        GuiWidget CreateOptionsMenu()
        {
            ImageBuffer gearImage = new ImageBuffer();
            string imagePathAndFile = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "gear_icon.png");
            ImageIO.LoadImageData(imagePathAndFile, gearImage);

            FlowLayoutWidget leftToRight = new FlowLayoutWidget();
            leftToRight.Margin = new BorderDouble(5, 0);
            string optionsString = "Options".Localize().ToUpper();
            TextWidget optionsText = new TextWidget(optionsString, textColor: ActiveTheme.Instance.PrimaryTextColor);
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
