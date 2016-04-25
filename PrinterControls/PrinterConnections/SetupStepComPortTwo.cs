﻿using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using System;
using System.Linq;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupStepComPortTwo : SetupConnectionWidgetBase
	{
		private string[] startingPortNames;
		private string[] currentPortNames;
		private Button nextButton;
		private Button connectButton;
		private TextWidget printerErrorMessage;

		private event EventHandler unregisterEvents;

		public SetupStepComPortTwo(ConnectionWindow windowController, GuiWidget containerWindowToClose, PrinterSetupStatus setupPrinterStatus)
			: base(windowController, containerWindowToClose, setupPrinterStatus)
		{
			startingPortNames = FrostedSerialPort.GetPortNames();
			contentRow.AddChild(createPrinterConnectionMessageContainer());
			{
				//Construct buttons
				nextButton = textImageButtonFactory.Generate(LocalizedString.Get("Done"));
				nextButton.Click += new EventHandler(NextButton_Click);
				nextButton.Visible = false;

				connectButton = textImageButtonFactory.Generate(LocalizedString.Get("Connect"));
				connectButton.Click += new EventHandler(ConnectButton_Click);

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
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		public FlowLayoutWidget createPrinterConnectionMessageContainer()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.VAnchor = VAnchor.ParentBottomTop;
			container.Margin = new BorderDouble(5);
			BorderDouble elementMargin = new BorderDouble(top: 5);

			string printerMessageOneText = LocalizedString.Get("MatterControl will now attempt to auto-detect printer.");
			TextWidget printerMessageOne = new TextWidget(printerMessageOneText, 0, 0, 10);
			printerMessageOne.Margin = new BorderDouble(0, 10, 0, 5);
			printerMessageOne.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageOne.HAnchor = HAnchor.ParentLeftRight;
			printerMessageOne.Margin = elementMargin;

			string printerMessageTwoTxtBeg = LocalizedString.Get("Disconnect printer");
			string printerMessageTwoTxtEnd = LocalizedString.Get("if currently connected");
			string printerMessageTwoTxtFull = string.Format("1.) {0} ({1}).", printerMessageTwoTxtBeg, printerMessageTwoTxtEnd);
			TextWidget printerMessageTwo = new TextWidget(printerMessageTwoTxtFull, 0, 0, 12);
			printerMessageTwo.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageTwo.HAnchor = HAnchor.ParentLeftRight;
			printerMessageTwo.Margin = elementMargin;

			string printerMessageThreeTxtBeg = LocalizedString.Get("Press");
			string printerMessageThreeTxtEnd = LocalizedString.Get("Continue");
			string printerMessageThreeTxtFull = string.Format("2.) {0} '{1}'.", printerMessageThreeTxtBeg, printerMessageThreeTxtEnd);
			TextWidget printerMessageThree = new TextWidget(printerMessageThreeTxtFull, 0, 0, 12);
			printerMessageThree.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageThree.HAnchor = HAnchor.ParentLeftRight;
			printerMessageThree.Margin = elementMargin;

			string printerMessageFourBeg = LocalizedString.Get("Power on and connect printer");
			string printerMessageFourFull = string.Format("3.) {0}.", printerMessageFourBeg);
			TextWidget printerMessageFour = new TextWidget(printerMessageFourFull, 0, 0, 12);
			printerMessageFour.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageFour.HAnchor = HAnchor.ParentLeftRight;
			printerMessageFour.Margin = elementMargin;

			string printerMessageFiveTxtBeg = LocalizedString.Get("Press");
			string printerMessageFiveTxtEnd = LocalizedString.Get("Connect");
			string printerMessageFiveTxtFull = string.Format("4.) {0} '{1}'.", printerMessageFiveTxtBeg, printerMessageFiveTxtEnd);
			TextWidget printerMessageFive = new TextWidget(printerMessageFiveTxtFull, 0, 0, 12);
			printerMessageFive.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageFive.HAnchor = HAnchor.ParentLeftRight;
			printerMessageFive.Margin = elementMargin;

			GuiWidget vSpacer = new GuiWidget();
			vSpacer.VAnchor = VAnchor.ParentBottomTop;

			Button manualLink = linkButtonFactory.Generate(LocalizedString.Get("Manual Configuration"));
			manualLink.Margin = new BorderDouble(0, 5);
			manualLink.Click += new EventHandler(ManualLink_Click);

			printerErrorMessage = new TextWidget("", 0, 0, 10);
			printerErrorMessage.AutoExpandBoundsToText = true;
			printerErrorMessage.TextColor = RGBA_Bytes.Red;
			printerErrorMessage.HAnchor = HAnchor.ParentLeftRight;
			printerErrorMessage.Margin = elementMargin;

			container.AddChild(printerMessageOne);
			container.AddChild(printerMessageTwo);
			container.AddChild(printerMessageThree);
			container.AddChild(printerMessageFour);
			container.AddChild(printerMessageFive);
			container.AddChild(printerErrorMessage);
			container.AddChild(vSpacer);
			container.AddChild(manualLink);

			container.HAnchor = HAnchor.ParentLeftRight;
			return container;
		}

		private void ManualLink_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(MoveToManualConfiguration);
		}

		private void MoveToManualConfiguration()
		{
			Parent.AddChild(new SetupStepComPortManual((ConnectionWindow)Parent, Parent, this.currentPrinterSetupStatus));
			Parent.RemoveChild(this);
		}

		private void ConnectButton_Click(object sender, EventArgs mouseEvent)
		{
			// Select the first port that's in GetPortNames() but not in startingPortNames
			string candidatePort = FrostedSerialPort.GetPortNames().Except(startingPortNames).FirstOrDefault();
			if (candidatePort == null)
			{
				printerErrorMessage.TextColor = RGBA_Bytes.Red;
				string printerErrorMessageLabelFull = LocalizedString.Get("Oops! Printer could not be detected ");
				printerErrorMessage.Text = printerErrorMessageLabelFull;
			}
			else
			{
				ActivePrinter.ComPort = candidatePort;
				printerErrorMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				string printerErrorMessageLabelTwo = LocalizedString.Get("Attempting to connect");
				string printerErrorMessageLabelTwoFull = string.Format("{0}...", printerErrorMessageLabelTwo);
				printerErrorMessage.Text = printerErrorMessageLabelTwoFull;
				this.ActivePrinter.Commit();
				ActivePrinterProfile.Instance.ActivePrinter = this.ActivePrinter;
				PrinterConnectionAndCommunication.Instance.ConnectToActivePrinter();
				connectButton.Visible = false;
			}
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
			printerErrorMessage.TextColor = RGBA_Bytes.Red;
			printerErrorMessage.Text = LocalizedString.Get("Uh-oh! Could not connect to printer.");
			connectButton.Visible = true;
			nextButton.Visible = false;
		}

		private void onConnectionSuccess()
		{
			printerErrorMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			string printerErrorMessageLabelThree = LocalizedString.Get("Connection succeeded");
			string printerErrorMessageLabelThreeFull = string.Format("{0}!", printerErrorMessageLabelThree);
			printerErrorMessage.Text = printerErrorMessageLabelThreeFull;
			nextButton.Visible = true;
			connectButton.Visible = false;
			UiThread.RunOnIdle(Parent.Close);
		}

		private void NextButton_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(Parent.Close);
		}
	}
}