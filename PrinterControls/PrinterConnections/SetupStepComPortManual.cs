using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;

using System;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupStepComPortManual : SetupConnectionWidgetBase
	{
		private TextWidget printerComPortError;

		//GuiWidget comPortWidget;
		private Button nextButton;

		private Button connectButton;
		private Button refreshButton;
		private Button printerComPortHelpLink;
		private TextWidget printerComPortHelpMessage;

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

				//Add buttons to buttonContainer
				footerRow.AddChild(nextButton);
				footerRow.AddChild(connectButton);
				footerRow.AddChild(refreshButton);
				footerRow.AddChild(new HorizontalSpacer());
				footerRow.AddChild(cancelButton);
			}
		}

		private event EventHandler unregisterEvents;

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

			FlowLayoutWidget serialPortContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			CreateSerialPortControls(serialPortContainer, null);

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
				onConnectionSuccess();
			}
			else if (PrinterConnectionAndCommunication.Instance.CommunicationState != PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect)
			{
				onConnectionFailed();
			}
		}

		private void onConnectionFailed()
		{
			printerComPortHelpLink.Visible = false;
			printerComPortError.TextColor = RGBA_Bytes.Red;
			printerComPortError.Text = LocalizedString.Get("Uh-oh! Could not connect to printer.");
			connectButton.Visible = true;
			nextButton.Visible = false;
		}

		private void onConnectionSuccess()
		{
			printerComPortHelpLink.Visible = false;
			printerComPortError.TextColor = this.subContainerTextColor;
			printerComPortError.Text = LocalizedString.Get("Connection succeeded!");
			nextButton.Visible = true;
			connectButton.Visible = false;
			UiThread.RunOnIdle(Parent.Close);
		}

		private void printerComPortHelp_Click(object sender, EventArgs mouseEvent)
		{
			printerComPortHelpMessage.Visible = !printerComPortHelpMessage.Visible;
		}

		private void MoveToNextWidget(object state)
		{
			// you can call this like this
			//             AfterUiEvents.AddAction(new AfterUIAction(MoveToNextWidget));

			if (this.currentPrinterSetupStatus.DriversToInstall.Count > 0)
			{
				Parent.AddChild(new SetupStepInstallDriver((ConnectionWindow)Parent, Parent, this.currentPrinterSetupStatus));
				Parent.RemoveChild(this);
			}
			else
			{
				Parent.AddChild(new SetupStepComPortOne((ConnectionWindow)Parent, Parent, this.currentPrinterSetupStatus));
				Parent.RemoveChild(this);
			}
		}

		private void RecreateCurrentWidget()
		{
			Parent.AddChild(new SetupStepComPortManual((ConnectionWindow)Parent, Parent, this.currentPrinterSetupStatus));
			Parent.RemoveChild(this);
		}

		private void RefreshButton_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(RecreateCurrentWidget);
		}

		private void ConnectButton_Click(object sender, EventArgs mouseEvent)
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
				string printerComPortErrorLabelFull = string.Format("{0}...", printerComPortErrorLabel);
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

		private void NextButton_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(Parent.Close);
		}
	}
}