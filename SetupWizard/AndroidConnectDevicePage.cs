/*
Copyright (c) 2016, Kevin Pope, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl
{
	public class AndroidConnectDevicePage : WizardPage
	{  
		private EventHandler unregisterEvents;

		private TextWidget generalError;

		private Button connectButton;
		private Button skipButton;
		private Button nextButton;
		private Button retryButton;
		private Button troubleshootButton;

		private TextWidget skipMessage;

		private FlowLayoutWidget retryButtonContainer;
		private FlowLayoutWidget connectButtonContainer;

		public AndroidConnectDevicePage()
		{
			TextWidget printerNameLabel = new TextWidget("Connect Your Device".Localize() + ":", 0, 0, labelFontSize)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Margin = new BorderDouble(bottom: 10)
			};
			contentRow.AddChild(printerNameLabel);

			contentRow.AddChild(new TextWidget("Instructions:".Localize(), 0, 0, 12,textColor:ActiveTheme.Instance.PrimaryTextColor));
			contentRow.AddChild(new TextWidget("1. Power on your 3D Printer.".Localize(), 0, 0, 12,textColor:ActiveTheme.Instance.PrimaryTextColor));
			contentRow.AddChild(new TextWidget("2. Attach your 3D Printer via USB.".Localize(), 0, 0, 12,textColor:ActiveTheme.Instance.PrimaryTextColor));
			contentRow.AddChild(new TextWidget("3. Press 'Connect'.".Localize(), 0, 0, 12,textColor:ActiveTheme.Instance.PrimaryTextColor));

			//Add inputs to main container
			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(communicationStateChanged, ref unregisterEvents);

			connectButtonContainer = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(0, 6)
			};

			//Construct buttons
			connectButton = whiteImageButtonFactory.Generate("Connect".Localize(),centerText:true);
			connectButton.Margin = new BorderDouble(0,0,10,0);
			connectButton.Click += new EventHandler(ConnectButton_Click);

			skipButton = whiteImageButtonFactory.Generate("Skip".Localize(), centerText:true);
			skipButton.Click += new EventHandler(NextButton_Click);

			connectButtonContainer.AddChild(connectButton);
			connectButtonContainer.AddChild(skipButton);
			connectButtonContainer.AddChild(new HorizontalSpacer());
			contentRow.AddChild(connectButtonContainer);

			skipMessage = new TextWidget("(Press 'Skip' to setup connection later)".Localize(), 0, 0, 10, textColor: ActiveTheme.Instance.PrimaryTextColor);
			contentRow.AddChild(skipMessage);

			generalError = new TextWidget("", 0, 0, errorFontSize)
			{
				TextColor = ActiveTheme.Instance.SecondaryAccentColor,
				HAnchor = HAnchor.ParentLeftRight,
				Visible = false,
				Margin = new BorderDouble(top: 20),
			};
			contentRow.AddChild(generalError);

			//Construct buttons
			retryButton = whiteImageButtonFactory.Generate("Retry".Localize(), centerText:true);
			retryButton.Click += ConnectButton_Click;
			retryButton.Margin = new BorderDouble(0,0,10,0);

			//Construct buttons
			troubleshootButton = whiteImageButtonFactory.Generate("Troubleshoot".Localize(), centerText:true);
			troubleshootButton.Click += (s, e) => UiThread.RunOnIdle(WizardWindow.ChangeToPage<SetupWizardTroubleshooting>);

			retryButtonContainer = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(0, 6),
				Visible = false
			};
			
			retryButtonContainer.AddChild(retryButton);
			retryButtonContainer.AddChild(troubleshootButton);
			retryButtonContainer.AddChild(new HorizontalSpacer());
			
			contentRow.AddChild(retryButtonContainer);

			//Construct buttons
			nextButton = textImageButtonFactory.Generate("Continue".Localize());
			nextButton.Click += NextButton_Click;
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
			PrinterConnectionAndCommunication.Instance.ConnectToActivePrinter(true);
		}

		void NextButton_Click(object sender, EventArgs mouseEvent)
		{
			this.generalError.Text = "Please wait...";
			this.generalError.Visible = true;
			nextButton.Visible = false;
			UiThread.RunOnIdle(this.WizardWindow.Close);
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

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}
