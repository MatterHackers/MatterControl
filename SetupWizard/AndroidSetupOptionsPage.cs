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
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.ImageProcessing;

namespace MatterHackers.MatterControl
{
	public class SetupOptionsPage : WizardPage
	{
		private EventHandler unregisterEvents;

		public SetupOptionsPage()
			: base("Done")
		{
			headerLabel.Text = "Setup Options".Localize();

			contentRow.AddChild(new SetupPrinterView(this.textImageButtonFactory) { WizardPage = this });
			contentRow.AddChild(new SetupAccountView(this.textImageButtonFactory));

			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);

			cancelButton.Text = "Back".Localize();

#if ANDROID_T7X
			headerRow.AddChild(new HorizontalSpacer());
			var powerImage = StaticData.Instance.LoadIcon("power.png", 32, 32);
			var powerButton = new TextImageButtonFactory() { FixedHeight = 42, Margin = new BorderDouble(0, 0, 10, 0), borderWidth = 0 }.GenerateTooltipButton("", powerImage);
			powerButton.Click += (s, e) => MatterControlApplication.Instance.RequestPowerShutDown();
			powerButton.VAnchor |= VAnchor.ParentCenter;
			headerRow.AddChild(powerButton);
#endif
		}
	}

	public class SetupPrinterView : SetupViewBase
	{
		internal WizardPage WizardPage { get; set; }

		private Button disconnectButton;
		private TextWidget connectionStatus;
		private EventHandler unregisterEvents;

		public SetupPrinterView(TextImageButtonFactory textImageButtonFactory)
			: base("Printer Profile")
		{
			this.textImageButtonFactory = textImageButtonFactory;

			var buttonContainer = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble (0, 14)
			};
			mainContainer.AddChild(buttonContainer);

			var printerSelectorAndEditButton = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
			};
			buttonContainer.AddChild(printerSelectorAndEditButton);

			var printerSelector = new PrinterSelector();
			printerSelectorAndEditButton.AddChild(printerSelector);

			disconnectButton = textImageButtonFactory.Generate("Disconnect");
			disconnectButton.Margin = new BorderDouble(left: 12);
			disconnectButton.VAnchor = VAnchor.Center;
			disconnectButton.Click += (sender, e) =>
			{
				PrinterConnection.Instance.Disable();
				UiThread.RunOnIdle(WizardPage.WizardWindow.ChangeToPage<SetupOptionsPage>);
			};
			buttonContainer.AddChild(disconnectButton);

			connectionStatus = new TextWidget("Status:", pointSize: 12, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				HAnchor = HAnchor.Stretch
			};
			mainContainer.AddChild(connectionStatus);

			PrinterConnection.Instance.CommunicationStateChanged.RegisterEvent(updateConnectedState, ref unregisterEvents);
			updateConnectedState(null, null);
		}

		private void updateConnectedState(object sender, EventArgs e)
		{
			if (disconnectButton != null)
			{
				disconnectButton.Visible = PrinterConnection.Instance.PrinterIsConnected;
			}

			if (connectionStatus != null)
			{
				connectionStatus.Text = string.Format ("{0}: {1}", "Status".Localize().ToUpper(), PrinterConnection.Instance.PrinterConnectionStatusVerbose);
			}

			this.Invalidate();
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}

	public class SetupAccountView : SetupViewBase
	{
		private EventHandler unregisterEvents;
		private Button signInButton;
		private Button signOutButton;
		private TextWidget statusMessage;
		TextWidget connectionStatus;

		public static string AuthenticationString { private get; set; } = "";

		internal void RefreshStatus()
		{
			connectionStatus.Text = AuthenticationString;
			if (!HasBeenClosed)
			{
				UiThread.RunOnIdle(RefreshStatus, 1);
			}
		}

		public SetupAccountView(TextImageButtonFactory textImageButtonFactory)
			: base("My Account")
		{
			this.textImageButtonFactory = textImageButtonFactory;

			bool signedIn = true;
			string username = AuthenticationData.Instance.ActiveSessionUsername;
			if (username == null)
			{
				signedIn = false;
				username = "Not Signed In";
			}

			FlowLayoutWidget nameAndStatus = new FlowLayoutWidget();
			nameAndStatus.AddChild(new TextWidget(username, pointSize: 16, textColor: ActiveTheme.Instance.PrimaryTextColor));

			connectionStatus = new TextWidget(AuthenticationString, pointSize: 8, textColor: ActiveTheme.Instance.SecondaryTextColor)
			{
				Margin = new BorderDouble(5, 0, 0, 0),
				AutoExpandBoundsToText = true,
			};

			if (signedIn)
			{
				nameAndStatus.AddChild(connectionStatus);
			}


			mainContainer.AddChild(nameAndStatus);

			RefreshStatus();

			FlowLayoutWidget buttonContainer = new FlowLayoutWidget();
			buttonContainer.HAnchor = HAnchor.Stretch;
			buttonContainer.Margin = new BorderDouble(0, 14);

			signInButton = textImageButtonFactory.Generate("Sign In".Localize());
			signInButton.Margin = new BorderDouble(left: 0);
			signInButton.VAnchor = VAnchor.Center;
			signInButton.Visible = !signedIn;
			signInButton.Click += (s, e) =>
			{
#if __ANDROID__
				if (MatterControlApplication.Instance.IsNetworkConnected() 
				    && AuthenticationData.Instance.IsConnected)
				{
					UiThread.RunOnIdle(ApplicationController.Instance.StartSignIn);
				}
				else
				{
					WizardWindow.Show<NetworkTroubleshooting>("/networktroubleshooting", "Network Troubleshooting");
				}
#else
				UiThread.RunOnIdle(ApplicationController.Instance.StartSignIn);
#endif
			};
			buttonContainer.AddChild(signInButton);

			signOutButton = textImageButtonFactory.Generate("Sign Out".Localize());
			signOutButton.Margin = new BorderDouble(left: 0);
			signOutButton.VAnchor = VAnchor.Center;
			signOutButton.Visible = signedIn;
			signOutButton.Click += (s, e) => UiThread.RunOnIdle(ApplicationController.Instance.StartSignOut);
			buttonContainer.AddChild(signOutButton);

			buttonContainer.AddChild(new HorizontalSpacer());

			// the redeem design code button
			Button redeemPurchaseButton = textImageButtonFactory.Generate("Redeem Purchase".Localize());
			redeemPurchaseButton.Enabled = true; // The library selector (the first library selected) is protected so we can't add to it.
			redeemPurchaseButton.Name = "Redeem Code Button";
			redeemPurchaseButton.Margin = new BorderDouble(0, 0, 10, 0);
			redeemPurchaseButton.Click += (sender, e) =>
			{
				ApplicationController.Instance.RedeemDesignCode?.Invoke();
			};
			buttonContainer.AddChild(redeemPurchaseButton);

			// the redeem a share code button
			Button redeemShareButton = textImageButtonFactory.Generate("Enter Share Code".Localize());
			redeemShareButton.Enabled = true; // The library selector (the first library selected) is protected so we can't add to it.
			redeemShareButton.Name = "Enter Share Code";
			redeemShareButton.Margin = new BorderDouble(0, 0, 10, 0);
			redeemShareButton.Click += (sender, e) =>
			{
				ApplicationController.Instance.EnterShareCode?.Invoke();
			};

			if (!signedIn)
			{
				redeemPurchaseButton.Enabled = false;
				redeemShareButton.Enabled = false;
			}

			buttonContainer.AddChild(redeemShareButton);

			statusMessage = new TextWidget("Please wait...", pointSize: 12, textColor: ActiveTheme.Instance.SecondaryAccentColor);
			statusMessage.Visible = false;
			buttonContainer.AddChild(statusMessage);

			mainContainer.AddChild(buttonContainer);

			ApplicationController.Instance.DoneReloadingAll.RegisterEvent(RemoveAndNewControl, ref unregisterEvents);
		}

		private void RemoveAndNewControl(object sender, EventArgs e)
		{
			GuiWidget parent = Parent;
			int thisIndex = parent.GetChildIndex(this);
			parent.RemoveChild(this);
			parent.AddChild(new SetupAccountView(this.textImageButtonFactory), thisIndex);
			this.Close();
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}

	public class SetupViewBase : AltGroupBox
	{
		protected TextImageButtonFactory textImageButtonFactory;
		protected FlowLayoutWidget mainContainer;

		public SetupViewBase(string title)
			: base(title != "" ? new TextWidget(title, pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor) : null)
		{
			this.Margin = new BorderDouble(2, 10, 2, 0);

			mainContainer = new FlowLayoutWidget(Agg.UI.FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(6, 0, 0, 6)
			};
			AddChild(mainContainer);
		}
	}
}
