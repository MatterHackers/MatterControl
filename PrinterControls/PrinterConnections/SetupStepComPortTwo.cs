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

		private event EventHandler unregisterEvents;

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

				PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);

				//Add buttons to buttonContainer
				footerRow.AddChild(nextButton);
				footerRow.AddChild(connectButton);
				footerRow.AddChild(new HorizontalSpacer());
				footerRow.AddChild(cancelButton);
			}
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public FlowLayoutWidget createPrinterConnectionMessageContainer()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.VAnchor = VAnchor.ParentBottomTop;
			container.Margin = new BorderDouble(5);
			BorderDouble elementMargin = new BorderDouble(top: 5);

			string printerMessageOneText = "MatterControl will now attempt to auto-detect printer.".Localize();
			TextWidget printerMessageOne = new TextWidget(printerMessageOneText, 0, 0, 10);
			printerMessageOne.Margin = new BorderDouble(0, 10, 0, 5);
			printerMessageOne.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageOne.HAnchor = HAnchor.ParentLeftRight;
			printerMessageOne.Margin = elementMargin;

			string disconnectMessage = string.Format("1.) {0} ({1}).", "Disconnect printer".Localize(), "if currently connected".Localize());
			TextWidget printerMessageTwo = new TextWidget(disconnectMessage, 0, 0, 12);
			printerMessageTwo.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageTwo.HAnchor = HAnchor.ParentLeftRight;
			printerMessageTwo.Margin = elementMargin;

			string printerMessageThreeBeg = "Power on and connect printer".Localize();
			string printerMessageThreeFull = string.Format("2.) {0}.", printerMessageThreeBeg);
			TextWidget printerMessageThree = new TextWidget(printerMessageThreeFull, 0, 0, 12);
            printerMessageThree.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            printerMessageThree.HAnchor = HAnchor.ParentLeftRight;
            printerMessageThree.Margin = elementMargin;

			string printerMessageFourTxtBeg = "Press".Localize();
			string printerMessageFourTxtEnd = "Connect".Localize();
			string printerMessageFourTxtFull = string.Format("3.) {0} '{1}'.", printerMessageFourTxtBeg, printerMessageFourTxtEnd);
			TextWidget printerMessageFour = new TextWidget(printerMessageFourTxtFull, 0, 0, 12);
            printerMessageFour.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            printerMessageFour.HAnchor = HAnchor.ParentLeftRight;
            printerMessageFour.Margin = elementMargin;

			GuiWidget vSpacer = new GuiWidget();
			vSpacer.VAnchor = VAnchor.ParentBottomTop;

			Button manualLink = linkButtonFactory.Generate("Manual Configuration".Localize());
			manualLink.Margin = new BorderDouble(0, 5);
			manualLink.Click += (s, e) => WizardWindow.ChangeToPage<SetupStepComPortManual>();

			printerErrorMessage = new TextWidget("", 0, 0, 10);
			printerErrorMessage.AutoExpandBoundsToText = true;
			printerErrorMessage.TextColor = RGBA_Bytes.Red;
			printerErrorMessage.HAnchor = HAnchor.ParentLeftRight;
			printerErrorMessage.Margin = elementMargin;

			container.AddChild(printerMessageOne);
			container.AddChild(printerMessageTwo);
			container.AddChild(printerMessageThree);
			container.AddChild(printerMessageFour);
			container.AddChild(printerErrorMessage);
			container.AddChild(vSpacer);
			container.AddChild(manualLink);

			container.HAnchor = HAnchor.ParentLeftRight;
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
				PrinterConnectionAndCommunication.Instance.ConnectToActivePrinter();
				connectButton.Visible = false;
			}
		}

		private void onPrinterStatusChanged(object sender, EventArgs e)
		{
			if (PrinterConnectionAndCommunication.Instance.PrinterIsConnected)
			{
				printerErrorMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				printerErrorMessage.Text = "Connection succeeded".Localize() + "!";
				nextButton.Visible = true;
				connectButton.Visible = false;
				UiThread.RunOnIdle(() => this?.Parent?.Close());
			}
			else if (PrinterConnectionAndCommunication.Instance.CommunicationState != PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect)
			{
				printerErrorMessage.TextColor = RGBA_Bytes.Red;
				printerErrorMessage.Text = "Uh-oh! Could not connect to printer.".Localize();
				connectButton.Visible = true;
				nextButton.Visible = false;
			}
		}
	}
}