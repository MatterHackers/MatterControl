using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.SerialPortCommunication.FrostedSerial;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
    //Empty base class for selectable printer list
    public class PrinterListItem : FlowLayoutWidget
    {
        protected ConnectionWindow windowController;
        protected Printer printerRecord;

        public PrinterListItem(Printer printerRecord, ConnectionWindow windowController)
        {
            this.printerRecord = printerRecord;
            this.windowController = windowController;
        }

    }

    class PrinterListItemView : PrinterListItem
    {        
        TextWidget printerName;       
        
        RGBA_Bytes defaultBackgroundColor = new RGBA_Bytes(250,250,250);
        RGBA_Bytes hoverBackgroundColor = new RGBA_Bytes(204, 204, 204);

        RGBA_Bytes defaultTextColor = new RGBA_Bytes(34, 34, 34);
        RGBA_Bytes hoverTextColor = new RGBA_Bytes(34, 34, 34);
		SlideWidget rightButtonOverlay;

        public PrinterListItemView(Printer printerRecord, ConnectionWindow windowController)
            :base(printerRecord, windowController)
        {            
            this.Margin = new BorderDouble(1);
            this.BackgroundColor = this.defaultBackgroundColor;
            this.Padding = new BorderDouble(5);            
            
            string[] comportNames = FrostedSerialPort.GetPortNames();
            bool portIsAvailable = comportNames.Contains(printerRecord.ComPort);

            printerName = new TextWidget(this.printerRecord.Name);
            printerName.TextColor = this.defaultTextColor;
            printerName.HAnchor = HAnchor.ParentLeftRight;

			string availableText = LocalizedString.Get("Unavailable");
            RGBA_Bytes availableColor = new RGBA_Bytes(158, 18, 0);
            if (portIsAvailable)
            {
                availableText = "";
            }

            if (ActivePrinterProfile.Instance.ActivePrinter != null)
            {
                int connectedPrinterHash = ActivePrinterProfile.Instance.ActivePrinter.GetHashCode();
                int printerOptionHash = printerRecord.GetHashCode();
                if (connectedPrinterHash == printerOptionHash)
                {
                    availableText = PrinterConnectionAndCommunication.Instance.PrinterConnectionStatusVerbose;                    
                    availableColor = new RGBA_Bytes(0,95,107);
                }
            }        

            TextWidget availableIndicator = new TextWidget(availableText, pointSize: 10);
            availableIndicator.TextColor = availableColor;
            availableIndicator.Padding = new BorderDouble(3, 0, 0, 3);
            availableIndicator.VAnchor = Agg.UI.VAnchor.ParentCenter;

			rightButtonOverlay = getItemActionButtons();
			rightButtonOverlay.Visible = false;
            this.AddChild(printerName);
			this.AddChild (rightButtonOverlay);
            //this.AddChild(availableIndicator);
            this.HAnchor = HAnchor.ParentLeftRight;

            BindHandlers();
        }

		SlideWidget getItemActionButtons()
		{
			SlideWidget buttonContainer = new SlideWidget();
			buttonContainer.VAnchor = VAnchor.ParentBottomTop;

			FlowLayoutWidget buttonFlowContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			buttonFlowContainer.VAnchor = VAnchor.ParentBottomTop;

			ClickWidget removeButton = new ClickWidget();
			removeButton.VAnchor = VAnchor.ParentBottomTop;
			removeButton.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
			removeButton.Width = 60;

			TextWidget printLabel = new TextWidget("remove".Localize());
			printLabel.TextColor = RGBA_Bytes.White;
			printLabel.VAnchor = VAnchor.ParentCenter;
			printLabel.HAnchor = HAnchor.ParentCenter;

			removeButton.AddChild(printLabel);
			removeButton.Click += (sender, e) =>
			{
				//				QueueData.Instance.AddItem(this.printItemWrapper,0);
				//				QueueData.Instance.SelectedIndex = 0;
				//				this.Invalidate();

			};;

			ClickWidget editButton = new ClickWidget();
			editButton.VAnchor = VAnchor.ParentBottomTop;
			editButton.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
			editButton.Width = 60;


			TextWidget editLabel = new TextWidget("edit".Localize());
			editLabel.TextColor = RGBA_Bytes.White;
			editLabel.VAnchor = VAnchor.ParentCenter;
			editLabel.HAnchor = HAnchor.ParentCenter;

			editButton.AddChild(editLabel);
			editButton.Click += (sender, e) => 
			{
				this.windowController.ChangedToEditPrinter(this.printerRecord);
			};



			buttonFlowContainer.AddChild(editButton);
			buttonFlowContainer.AddChild(removeButton);

			buttonContainer.AddChild(buttonFlowContainer);
			buttonContainer.Width = 120;
			return buttonContainer;
		}


        public void BindHandlers()
        {
            this.MouseEnter += new EventHandler(onMouse_Enter);
            this.MouseLeave += new EventHandler(onMouse_Leave);
            this.MouseUp += new MouseEventHandler(onMouse_Up);
        }
			

        void onMouse_Up(object sender, MouseEventArgs mouseEvent)
        {
            //Turns this into a standard 'click' event
            if (this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
            {
                UiThread.RunOnIdle(CloseOnIdle);
                ActivePrinterProfile.Instance.ActivePrinter = this.printerRecord;                
            }
        }

        void CloseOnIdle(object state)
        {
            this.windowController.Close();
        }

        void onMouse_Enter(object sender, EventArgs args)
        {
			this.rightButtonOverlay.SlideIn ();
            this.BackgroundColor = this.hoverBackgroundColor;
            this.printerName.TextColor = this.hoverTextColor;

        }
			

        void onMouse_Leave(object sender, EventArgs args)
        {
			this.rightButtonOverlay.SlideOut ();
            this.BackgroundColor = this.defaultBackgroundColor;
            this.printerName.TextColor = this.defaultTextColor;  

        }
    }

    class PrinterListItemEdit : PrinterListItem
    {
        TextWidget printerName;

        Button editLink;
        Button removeLink;

        LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
        
        RGBA_Bytes defaultBackgroundColor = new RGBA_Bytes(250,250,250);
        RGBA_Bytes hoverBackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

        RGBA_Bytes defaultTextColor = new RGBA_Bytes(34, 34, 34);
        RGBA_Bytes hoverTextColor = new RGBA_Bytes(250, 250, 250);

        public PrinterListItemEdit(Printer printerRecord, ConnectionWindow windowController)
            :base(printerRecord, windowController)
        {            
            linkButtonFactory.fontSize = 10;
            linkButtonFactory.padding = 0;
            linkButtonFactory.margin = new BorderDouble(3, 0);
            
            this.printerRecord = printerRecord;
            this.Margin = new BorderDouble(1);
            this.BackgroundColor = this.defaultBackgroundColor;
            this.Padding = new BorderDouble(5);
            this.HAnchor = HAnchor.ParentLeftRight;

            printerName = new TextWidget(this.printerRecord.Name);
            printerName.TextColor = this.defaultTextColor;
            printerName.HAnchor = HAnchor.ParentLeftRight;

			editLink = linkButtonFactory.Generate(LocalizedString.Get("edit"));
            editLink.VAnchor = VAnchor.ParentCenter;

			removeLink = linkButtonFactory.Generate(LocalizedString.Get("remove"));
            removeLink.VAnchor = VAnchor.ParentCenter;

            this.AddChild(printerName);
            this.AddChild(editLink);
            this.AddChild(removeLink);     

            BindHandlers();
        }

        public void BindHandlers()
        {
            editLink.Click += new ButtonBase.ButtonEventHandler(EditConnectionLink_Click);
            removeLink.Click += new ButtonBase.ButtonEventHandler(RemoveConnectionLink_Click);            
        }

        void EditConnectionLink_Click(object sender, MouseEventArgs mouseEvent)
        {            
            this.windowController.ChangedToEditPrinter(this.printerRecord);
        }

        void RemoveConnectionLink_Click(object sender, MouseEventArgs mouseEvent)
        {

            if (ActivePrinterProfile.Instance.ActivePrinter != null)
            {
                int connectedPrinterHash = ActivePrinterProfile.Instance.ActivePrinter.GetHashCode();
                int printerOptionHash = this.printerRecord.GetHashCode();

                //Disconnect printer if the printer being removed is currently connected
                if (connectedPrinterHash == printerOptionHash)
                {
                    PrinterConnectionAndCommunication.Instance.Disable();
                    ActivePrinterProfile.Instance.ActivePrinter = null;
                }
            }
            this.printerRecord.Delete();
            this.windowController.ChangeToChoosePrinter(true);
        }
    }
}
