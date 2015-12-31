using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using System;
using System.Linq;

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

	internal class PrinterListItemView : PrinterListItem
	{
		private TextWidget printerName;

		private RGBA_Bytes defaultBackgroundColor = new RGBA_Bytes(250, 250, 250);
		private RGBA_Bytes hoverBackgroundColor = new RGBA_Bytes(204, 204, 204);

		private RGBA_Bytes defaultTextColor = new RGBA_Bytes(34, 34, 34);
		private RGBA_Bytes hoverTextColor = new RGBA_Bytes(34, 34, 34);

		public PrinterListItemView(Printer printerRecord, ConnectionWindow windowController)
			: base(printerRecord, windowController)
		{
			this.Margin = new BorderDouble(1);
			this.BackgroundColor = this.defaultBackgroundColor;
			this.Padding = new BorderDouble(0);
			this.Name = this.printerRecord.Name + " Profile";

			string[] comportNames = FrostedSerialPort.GetPortNames();
			bool portIsAvailable = comportNames.Contains(printerRecord.ComPort);

			printerName = new TextWidget(this.printerRecord.Name);
			printerName.TextColor = this.defaultTextColor;
			printerName.HAnchor = HAnchor.ParentLeftRight;
			printerName.Margin = new BorderDouble(5, 10, 5, 10);

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
					availableColor = new RGBA_Bytes(0, 95, 107);
				}
			}

			TextWidget availableIndicator = new TextWidget(availableText, pointSize: 10);
			availableIndicator.TextColor = availableColor;
			availableIndicator.Padding = new BorderDouble(3, 0, 0, 3);
			availableIndicator.Margin = new BorderDouble(right: 5);
			availableIndicator.VAnchor = Agg.UI.VAnchor.ParentCenter;

			this.AddChild(printerName);
			this.AddChild(availableIndicator);
			this.HAnchor = HAnchor.ParentLeftRight;

			BindHandlers();
		}

		public void BindHandlers()
		{
			this.MouseEnter += new EventHandler(onMouse_Enter);
			this.MouseLeave += new EventHandler(onMouse_Leave);
			this.MouseUp += (sender, e) =>
			{
				UiThread.RunOnIdle(onMouse_Up, e);
			};
		}

		private void onMouse_Up(object state)
		{
			MouseEventArgs mouseEvent = state as MouseEventArgs;
			//Turns this into a standard 'click' event
			if (this.PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				// Changing ordering around so that CloseOnIdle is called after ActivePrinter is set
				ActivePrinterProfile.Instance.ActivePrinter = this.printerRecord;

				UiThread.RunOnIdle(this.windowController.Close);
			}
		}

		private void onMouse_Enter(object sender, EventArgs args)
		{
			this.BackgroundColor = this.hoverBackgroundColor;
			this.printerName.TextColor = this.hoverTextColor;
		}

		private void onMouse_Leave(object sender, EventArgs args)
		{
			this.BackgroundColor = this.defaultBackgroundColor;
			this.printerName.TextColor = this.defaultTextColor;
		}
	}

	internal class PrinterListItemEdit : PrinterListItem
	{
		private TextWidget printerName;

		private RGBA_Bytes defaultBackgroundColor = new RGBA_Bytes(250, 250, 250);
		private RGBA_Bytes hoverBackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

		private RGBA_Bytes defaultTextColor = new RGBA_Bytes(34, 34, 34);
		private RGBA_Bytes hoverTextColor = new RGBA_Bytes(250, 250, 250);
		private SlideWidget rightButtonOverlay;

		public PrinterListItemEdit(Printer printerRecord, ConnectionWindow windowController)
			: base(printerRecord, windowController)
		{
			this.printerRecord = printerRecord;
			this.Margin = new BorderDouble(1);
			this.BackgroundColor = this.defaultBackgroundColor;
			this.Padding = new BorderDouble(0);
			this.HAnchor = HAnchor.ParentLeftRight;

			FlowLayoutWidget printerNameContainer = new FlowLayoutWidget();
			printerNameContainer.HAnchor = HAnchor.ParentLeftRight;

			printerName = new TextWidget(this.printerRecord.Name);
			printerName.TextColor = this.defaultTextColor;
			printerName.Margin = new BorderDouble(5, 10, 0, 10);
			printerName.HAnchor = HAnchor.ParentLeftRight;

			printerNameContainer.AddChild(printerName);

			FlowLayoutWidget rightButtonOverlayContainer = new FlowLayoutWidget(FlowDirection.RightToLeft);
			rightButtonOverlayContainer.HAnchor = HAnchor.ParentLeftRight;
			rightButtonOverlayContainer.VAnchor = VAnchor.ParentBottomTop;

			this.rightButtonOverlay = getItemActionButtons();
			this.rightButtonOverlay.Padding = new BorderDouble(0);
			this.rightButtonOverlay.Visible = true;
			rightButtonOverlayContainer.AddChild(rightButtonOverlay);

			this.AddChild(printerNameContainer);
			this.AddChild(rightButtonOverlayContainer);
		}

		private SlideWidget getItemActionButtons()
		{
			int buttonWidth;
			if (ActiveTheme.Instance.DisplayMode == ActiveTheme.ApplicationDisplayType.Touchscreen)
			{
				buttonWidth = 120;
			}
			else
			{
				buttonWidth = 80;//80
			}

			SlideWidget buttonContainer = new SlideWidget();
			buttonContainer.VAnchor = VAnchor.ParentBottomTop;

			FlowLayoutWidget buttonFlowContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			buttonFlowContainer.VAnchor = VAnchor.ParentBottomTop;

			TextWidget printLabel = new TextWidget("Remove".Localize());
			printLabel.TextColor = RGBA_Bytes.White;
			printLabel.VAnchor = VAnchor.ParentCenter;
			printLabel.HAnchor = HAnchor.ParentCenter;

			FatFlatClickWidget removeButton = new FatFlatClickWidget(printLabel);
			removeButton.VAnchor = VAnchor.ParentBottomTop;
			removeButton.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
			removeButton.Width = buttonWidth;
			removeButton.Click += RemoveConnectionLink_Click;

			TextWidget editLabel = new TextWidget("Edit".Localize());
			editLabel.TextColor = RGBA_Bytes.White;
			editLabel.VAnchor = VAnchor.ParentCenter;
			editLabel.HAnchor = HAnchor.ParentCenter;

			FatFlatClickWidget editButton = new FatFlatClickWidget(editLabel);
			editButton.VAnchor = VAnchor.ParentBottomTop;
			editButton.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
			editButton.Width = buttonWidth;

			editButton.Click += EditConnectionLink_Click;

			buttonFlowContainer.AddChild(editButton);
			buttonFlowContainer.AddChild(removeButton);

			buttonContainer.AddChild(buttonFlowContainer);
			buttonContainer.Width = buttonWidth * 2;

			return buttonContainer;
		}

		private void EditConnectionLink_Click(object sender, EventArgs mouseEvent)
		{
			this.windowController.ChangedToEditPrinter(this.printerRecord);
		}

		private void RemoveConnectionLink_Click(object sender, EventArgs mouseEvent)
		{
			//Disconnect printer if the printer being removed is currently connected
			if (ActivePrinterProfile.Instance.ActivePrinter != null && this.printerRecord.Id == ActivePrinterProfile.Instance.ActivePrinter.Id)
			{
				UiThread.RunOnIdle((state) =>
				{
					PrinterConnectionAndCommunication.Instance.Disable();
					ActivePrinterProfile.Instance.ActivePrinter = null;
				}, null, 0.5);
			}

			this.printerRecord.Delete();
			this.windowController.ChangeToChoosePrinter(true);
		}
	}
}