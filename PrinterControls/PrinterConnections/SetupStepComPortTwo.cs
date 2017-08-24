using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using System;
using System.Linq;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupStepComPortTwo : ConnectionWizardPage
	{
		private string[] startingPortNames;
		private string[] currentPortNames;
		private Button nextButton;
		private Button connectButton;
		private TextWidget printerErrorMessage;

		private EventHandler unregisterEvents;

		public SetupStepComPortTwo()
		{
			startingPortNames = FrostedSerialPort.GetPortNames();
			contentRow.AddChild(createPrinterConnectionMessageContainer());
			{
				//Construct buttons
				nextButton = textImageButtonFactory.Generate("Done".Localize());
				nextButton.Click += (s, e) => UiThread.RunOnIdle(Parent.Close);
				nextButton.Visible = false;

				connectButton = textImageButtonFactory.Generate("Connect".Localize());
				connectButton.Click += ConnectButton_Click;

				PrinterConnection.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);

				this.AddPageAction(nextButton);
				this.AddPageAction(connectButton);
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public FlowLayoutWidget createPrinterConnectionMessageContainer()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.VAnchor = VAnchor.Stretch;
			container.Margin = new BorderDouble(5);
			BorderDouble elementMargin = new BorderDouble(top: 5);

			string printerMessageOneText = "MatterControl will now attempt to auto-detect printer.".Localize();
			TextWidget printerMessageOne = new TextWidget(printerMessageOneText, 0, 0, 10);
			printerMessageOne.Margin = new BorderDouble(0, 10, 0, 5);
			printerMessageOne.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageOne.HAnchor = HAnchor.Stretch;
			printerMessageOne.Margin = elementMargin;

			string printerMessageFourBeg = "Connect printer and power on".Localize();
			string printerMessageFourFull = string.Format("1.) {0}.", printerMessageFourBeg);
			TextWidget printerMessageFour = new TextWidget(printerMessageFourFull, 0, 0, 12);
			printerMessageFour.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageFour.HAnchor = HAnchor.Stretch;
			printerMessageFour.Margin = elementMargin;

			string printerMessageFiveTxtBeg = "Press".Localize();
			string printerMessageFiveTxtEnd = "Connect".Localize();
			string printerMessageFiveTxtFull = string.Format("2.) {0} '{1}'.", printerMessageFiveTxtBeg, printerMessageFiveTxtEnd);
			TextWidget printerMessageFive = new TextWidget(printerMessageFiveTxtFull, 0, 0, 12);
			printerMessageFive.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageFive.HAnchor = HAnchor.Stretch;
			printerMessageFive.Margin = elementMargin;

			GuiWidget vSpacer = new GuiWidget();
			vSpacer.VAnchor = VAnchor.Stretch;

			Button manualLink = linkButtonFactory.Generate("Manual Configuration".Localize());
			manualLink.Margin = new BorderDouble(0, 5);
			manualLink.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				WizardWindow.ChangeToPage<SetupStepComPortManual>();
			});

			printerErrorMessage = new TextWidget("", 0, 0, 10);
			printerErrorMessage.AutoExpandBoundsToText = true;
			printerErrorMessage.TextColor = RGBA_Bytes.Red;
			printerErrorMessage.HAnchor = HAnchor.Stretch;
			printerErrorMessage.Margin = elementMargin;

			container.AddChild(printerMessageOne);
			container.AddChild(printerMessageFour);
			container.AddChild(printerErrorMessage);
			container.AddChild(vSpacer);
			container.AddChild(manualLink);

			container.HAnchor = HAnchor.Stretch;
			return container;
		}

		private void ConnectButton_Click(object sender, EventArgs mouseEvent)
		{
			// Select the first port that's in GetPortNames() but not in startingPortNames
			string candidatePort = FrostedSerialPort.GetPortNames().Except(startingPortNames).FirstOrDefault();
			if (candidatePort == null)
			{
				printerErrorMessage.TextColor = RGBA_Bytes.Red;
				printerErrorMessage.Text = "Oops! Printer could not be detected ".Localize();
			}
			else
			{
				printerErrorMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				printerErrorMessage.Text = "Attempting to connect".Localize() + "...";

				ActiveSliceSettings.Instance.Helpers.SetComPort(candidatePort);
				PrinterConnection.Instance.ConnectToActivePrinter();
				connectButton.Visible = false;
			}
		}

		private void onPrinterStatusChanged(object sender, EventArgs e)
		{
			if (PrinterConnection.Instance.PrinterIsConnected)
			{
				printerErrorMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				printerErrorMessage.Text = "Connection succeeded".Localize() + "!";
				nextButton.Visible = true;
				connectButton.Visible = false;
				UiThread.RunOnIdle(() => this?.Parent?.Close());
			}
			else if (PrinterConnection.Instance.CommunicationState != CommunicationStates.AttemptingToConnect)
			{
				printerErrorMessage.TextColor = RGBA_Bytes.Red;
				printerErrorMessage.Text = "Uh-oh! Could not connect to printer.".Localize();
				connectButton.Visible = true;
				nextButton.Visible = false;
			}
		}
	}
}