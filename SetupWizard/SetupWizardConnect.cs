using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.SerialPortCommunication.FrostedSerial;

namespace MatterHackers.MatterControl
{   
	//Normally step one of the setup process
	public class SetupWizardConnect : WizardPanel
	{  
		event EventHandler unregisterEvents;

		TextWidget generalError;

		Button connectButton;
		Button skipButton;
		Button nextButton;
		Button retryButton;
		Button troubleshootButton;

		TextWidget skipMessage;

		FlowLayoutWidget retryButtonContainer;
		FlowLayoutWidget connectButtonContainer;

		public SetupWizardConnect(WizardWindow windowController)
			: base(windowController)
		{
			string printerNameLabelTxt = LocalizedString.Get("Connect Your Device");
			string printerNameLabelTxtFull = string.Format ("{0}:", printerNameLabelTxt);
			TextWidget printerNameLabel = new TextWidget(printerNameLabelTxtFull, 0, 0, labelFontSize);
			printerNameLabel.TextColor = this.defaultTextColor;
			printerNameLabel.Margin = new BorderDouble(bottom: 10);

			contentRow.AddChild(printerNameLabel);

			contentRow.AddChild(new TextWidget(LocalizedString.Get("Instructions:"), 0, 0, 12,textColor:ActiveTheme.Instance.PrimaryTextColor));
			contentRow.AddChild(new TextWidget(LocalizedString.Get("1. Power on your 3D Printer."), 0, 0, 12,textColor:ActiveTheme.Instance.PrimaryTextColor));
			contentRow.AddChild(new TextWidget(LocalizedString.Get("2. Attach your 3D Printer via USB."), 0, 0, 12,textColor:ActiveTheme.Instance.PrimaryTextColor));
			contentRow.AddChild(new TextWidget(LocalizedString.Get("3. Press 'Connect'."), 0, 0, 12,textColor:ActiveTheme.Instance.PrimaryTextColor));

			//Add inputs to main container
			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(communicationStateChanged, ref unregisterEvents);

			connectButtonContainer = new FlowLayoutWidget();
			connectButtonContainer.HAnchor = HAnchor.ParentLeftRight;
			connectButtonContainer.Margin = new BorderDouble(0, 6);

			//Construct buttons
			connectButton = whiteImageButtonFactory.Generate(LocalizedString.Get("Connect"),centerText:true);
			connectButton.Margin = new BorderDouble(0,0,10,0);
			connectButton.Click += new EventHandler(ConnectButton_Click);

			//Construct buttons
			skipButton = whiteImageButtonFactory.Generate(LocalizedString.Get("Skip"), centerText:true);
			skipButton.Click += new EventHandler(NextButton_Click);

			connectButtonContainer.AddChild(connectButton);
			connectButtonContainer.AddChild(skipButton);
			connectButtonContainer.AddChild(new HorizontalSpacer());

			contentRow.AddChild(connectButtonContainer);

			skipMessage = new TextWidget(LocalizedString.Get("(Press 'Skip' to setup connection later)"), 0, 0, 10, textColor: ActiveTheme.Instance.PrimaryTextColor);

			contentRow.AddChild(skipMessage);

			generalError = new TextWidget("", 0, 0, errorFontSize);
			generalError.TextColor = ActiveTheme.Instance.SecondaryAccentColor;
			generalError.HAnchor = HAnchor.ParentLeftRight;
			generalError.Visible = false;
			generalError.Margin = new BorderDouble(top: 20);

			contentRow.AddChild(generalError);

			//Construct buttons
			retryButton = whiteImageButtonFactory.Generate(LocalizedString.Get("Retry"), centerText:true);
			retryButton.Click += new EventHandler(ConnectButton_Click);
			retryButton.Margin = new BorderDouble(0,0,10,0);

			//Construct buttons
			troubleshootButton = whiteImageButtonFactory.Generate(LocalizedString.Get("Troubleshoot"), centerText:true);
			troubleshootButton.Click += new EventHandler(TroubleshootButton_Click);

			retryButtonContainer = new FlowLayoutWidget();
			retryButtonContainer.HAnchor = HAnchor.ParentLeftRight;
			retryButtonContainer.Margin = new BorderDouble(0, 6);
			retryButtonContainer.AddChild(retryButton);
			retryButtonContainer.AddChild(troubleshootButton);
			retryButtonContainer.AddChild(new HorizontalSpacer());
			retryButtonContainer.Visible = false;

			contentRow.AddChild(retryButtonContainer);

			//Construct buttons
			nextButton = textImageButtonFactory.Generate(LocalizedString.Get("Continue"));
			nextButton.Click += new EventHandler(NextButton_Click);
			nextButton.Visible = false;

			GuiWidget hSpacer = new GuiWidget();
			hSpacer.HAnchor = HAnchor.ParentLeftRight;

			//Add buttons to buttonContainer
			footerRow.AddChild(nextButton);
			footerRow.AddChild(hSpacer);
			footerRow.AddChild(cancelButton);

			updateControls(true);
		}
			
		void ConnectButton_Click(object sender, EventArgs mouseEvent)
		{
			PrinterConnectionAndCommunication.Instance.ConnectToActivePrinter();
		}

		void TroubleshootButton_Click(object sender, EventArgs mouseEvent)
		{
			wizardWindow.ChangeToTroubleshooting();
		}

		void NextButton_Click(object sender, EventArgs mouseEvent)
		{
			this.generalError.Text = "Please wait...";
			this.generalError.Visible = true;
			nextButton.Visible = false;
			UiThread.RunOnIdle(this.wizardWindow.Close);
		}

		private void communicationStateChanged(object sender, EventArgs args)
		{
			UiThread.RunOnIdle(() => updateControls(false));
		}

		private void updateControls(bool firstLoad)
		{
			connectButton.Visible = false;
			skipMessage.Visible = false;
			generalError.Visible = false;
			nextButton.Visible = false;

			connectButtonContainer.Visible = false;
			retryButtonContainer.Visible = false;

			if (PrinterConnectionAndCommunication.Instance.PrinterIsConnected)
			{
				generalError.Text = "{0}!".FormatWith ("Connection succeeded".Localize ());
				generalError.Visible = true;
				nextButton.Visible = true;
			}
			else if (firstLoad || PrinterConnectionAndCommunication.Instance.CommunicationState == PrinterConnectionAndCommunication.CommunicationStates.Disconnected)
			{
				generalError.Text = "";
				connectButton.Visible = true;
				connectButtonContainer.Visible = true;
			}
			else if (PrinterConnectionAndCommunication.Instance.CommunicationState == PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect)
			{
				generalError.Text = "{0}...".FormatWith("Attempting to connect".Localize());
				generalError.Visible = true;
			}
			else
			{
				generalError.Text = "Uh-oh! Could not connect to printer.".Localize();
				generalError.Visible = true;
				nextButton.Visible = false;
				retryButtonContainer.Visible = true;
			}
			this.Invalidate();
		}
	}
}
