using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupStepComPortManual : SetupConnectionWidgetBase
	{
		private Button nextButton;
		private Button connectButton;
		private Button refreshButton;
		private Button printerComPortHelpLink;

		private TextWidget printerComPortHelpMessage;
		private TextWidget printerComPortError;

		private event EventHandler unregisterEvents;

		public SetupStepComPortManual(ConnectionWizard connectionWizard) : base(connectionWizard)
		{
			linkButtonFactory.fontSize = 8;

			FlowLayoutWidget printerComPortContainer = createComPortContainer();
			contentRow.AddChild(printerComPortContainer);

			//Construct buttons
			nextButton = textImageButtonFactory.Generate("Done".Localize());
			nextButton.Click += (s, e) => UiThread.RunOnIdle(Parent.Close);
			nextButton.Visible = false;

			connectButton = textImageButtonFactory.Generate("Connect".Localize());
			connectButton.Click += ConnectButton_Click;

			refreshButton = textImageButtonFactory.Generate("Refresh".Localize());
			refreshButton.Click += (s, e) => connectionWizard.ChangeToSetupComPortManual();

			//Add buttons to buttonContainer
			footerRow.AddChild(nextButton);
			footerRow.AddChild(connectButton);
			footerRow.AddChild(refreshButton);
			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);

			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
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
			comPortLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			comPortLabel.Margin = new BorderDouble(0, 0, 0, 10);
			comPortLabel.HAnchor = HAnchor.ParentLeftRight;

			FlowLayoutWidget serialPortContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			CreateSerialPortControls(serialPortContainer, null);

			FlowLayoutWidget comPortMessageContainer = new FlowLayoutWidget();
			comPortMessageContainer.Margin = elementMargin;
			comPortMessageContainer.HAnchor = HAnchor.ParentLeftRight;

			printerComPortError = new TextWidget("Currently available serial ports.".Localize(), 0, 0, 10);
			printerComPortError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerComPortError.AutoExpandBoundsToText = true;

			printerComPortHelpLink = linkButtonFactory.Generate("What's this?".Localize());
			printerComPortHelpLink.Margin = new BorderDouble(left: 5);
			printerComPortHelpLink.VAnchor = VAnchor.ParentBottom;
			printerComPortHelpLink.Click += (s, e) => printerComPortHelpMessage.Visible = !printerComPortHelpMessage.Visible;

			printerComPortHelpMessage = new TextWidget("The 'Serial Port' identifies which connected device is\nyour printer. Changing which usb plug you use may\nchange the associated serial port.\n\nTip: If you are uncertain, plug-in in your printer and hit\nrefresh. The new port that appears should be your\nprinter.".Localize(), 0, 0, 10);
			printerComPortHelpMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerComPortHelpMessage.Margin = new BorderDouble(top: 10);
			printerComPortHelpMessage.Visible = false;

			comPortMessageContainer.AddChild(printerComPortError);
			comPortMessageContainer.AddChild(printerComPortHelpLink);

			container.AddChild(comPortLabel);
			container.AddChild(serialPortContainer);
			container.AddChild(comPortMessageContainer);
			container.AddChild(printerComPortHelpMessage);

			container.HAnchor = HAnchor.ParentLeftRight;
			return container;
		}

		private void onPrinterStatusChanged(object sender, EventArgs e)
		{
			if (PrinterConnectionAndCommunication.Instance.PrinterIsConnected)
			{
				printerComPortHelpLink.Visible = false;
				printerComPortError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				printerComPortError.Text = "Connection succeeded".Localize() + "!";
				nextButton.Visible = true;
				connectButton.Visible = false;
				UiThread.RunOnIdle(Parent.Close);
			}
			else if (PrinterConnectionAndCommunication.Instance.CommunicationState != PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect)
			{
				printerComPortHelpLink.Visible = false;
				printerComPortError.TextColor = RGBA_Bytes.Red;
				printerComPortError.Text = "Uh-oh! Could not connect to printer.".Localize();
				connectButton.Visible = true;
				nextButton.Visible = false;
			}
		}

		private void MoveToNextWidget(object state)
		{
			connectionWizard.ChangeToInstallDriverOrComPortOne();
		}

		private void ConnectButton_Click(object sender, EventArgs mouseEvent)
		{
			try
			{
				printerComPortHelpLink.Visible = false;
				printerComPortError.TextColor = ActiveTheme.Instance.PrimaryTextColor;

				printerComPortError.Text = "Attempting to connect".Localize() + "...";
				printerComPortError.TextColor = ActiveTheme.Instance.PrimaryTextColor;

				ActiveSliceSettings.Instance.SetComPort(GetSelectedSerialPort());
				PrinterConnectionAndCommunication.Instance.ConnectToActivePrinter();

				connectButton.Visible = false;
				refreshButton.Visible = false;
			}
			catch
			{
				printerComPortHelpLink.Visible = false;
				printerComPortError.TextColor = RGBA_Bytes.Red;
				printerComPortError.Text = "Oops! Please select a serial port.".Localize();
			}
		}
	}
}