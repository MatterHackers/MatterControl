using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.MatterControl.PrintLibrary;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{       
    public class SetupConnectionWidgetBase : ConnectionWidgetBase
    {        
        public PrinterSetupStatus PrinterSetupStatus;

        //private GuiWidget mainContainer;

        protected FlowLayoutWidget headerRow;
        protected FlowLayoutWidget contentRow;
        protected FlowLayoutWidget footerRow;
        protected TextWidget headerLabel;
        protected Button cancelButton;
        protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        protected LinkButtonFactory linkButtonFactory = new LinkButtonFactory();

        public Printer ActivePrinter 
        { 
            get { return PrinterSetupStatus.ActivePrinter; } 
        }

        public SetupConnectionWidgetBase(ConnectionWindow windowController, GuiWidget containerWindowToClose, PrinterSetupStatus printerSetupStatus = null)
            : base(windowController, containerWindowToClose)
        {
            SetDisplayAttributes();

            if (printerSetupStatus == null)
            {
                this.PrinterSetupStatus = new PrinterSetupStatus();
            }
            else
            {
                this.PrinterSetupStatus = printerSetupStatus;
            }

			cancelButton = textImageButtonFactory.Generate (LocalizedString.Get ("Cancel"));
            cancelButton.Click += new EventHandler(CancelButton_Click);

            //Create the main container
            GuiWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            mainContainer.AnchorAll();
			mainContainer.Padding = new BorderDouble(3, 5, 3, 5);
            mainContainer.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            //Create the header row for the widget
            headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
            headerRow.Margin = new BorderDouble(0, 3, 0, 0);
            headerRow.Padding = new BorderDouble(0, 3, 0, 3);
            headerRow.HAnchor = HAnchor.ParentLeftRight;
            {
				string defaultHeaderTitle = LocalizedString.Get("3D Printer Setup");                
                headerLabel = new TextWidget(defaultHeaderTitle, pointSize: 14);
                headerLabel.AutoExpandBoundsToText = true;
                headerLabel.TextColor = this.defaultTextColor;
                headerRow.AddChild(headerLabel);
            }

            //Create the main control container
            contentRow = new FlowLayoutWidget(FlowDirection.TopToBottom);
            contentRow.Padding = new BorderDouble(5);
            contentRow.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            contentRow.HAnchor = HAnchor.ParentLeftRight;
            contentRow.VAnchor = VAnchor.ParentBottomTop;

            //Create the footer (button) container
            footerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
            footerRow.HAnchor = HAnchor.ParentLeft | HAnchor.ParentRight;
            footerRow.Margin = new BorderDouble(0, 3);

            mainContainer.AddChild(headerRow);
            mainContainer.AddChild(contentRow);
            mainContainer.AddChild(footerRow);
            this.AddChild(mainContainer);
        }

        protected void SaveAndExit()
        {
            this.ActivePrinter.Commit();
            ActivePrinterProfile.Instance.ActivePrinter = this.ActivePrinter;
            this.containerWindowToClose.Close();

        }

        private void SetDisplayAttributes()
        {
			textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.borderWidth = 0;

			linkButtonFactory.textColor = ActiveTheme.Instance.PrimaryTextColor;
            linkButtonFactory.fontSize = 10;

            this.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            this.AnchorAll();
            this.Padding = new BorderDouble(0); //To be re-enabled once native borders are turned off
        }

        void CloseWindow(object o, EventArgs e)
        {
            PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
            this.containerWindowToClose.Close();
        }

        void CancelButton_Click(object sender, EventArgs mouseEvent)
        {
            PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
            if (GetPrinterRecordCount() > 0)
            {
                this.windowController.ChangeToChoosePrinter();
            }
            else
            {
                UiThread.RunOnIdle((state) =>
                {
                    Parent.Close();
                });
            }
        }
    }
}
