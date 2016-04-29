using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.ComponentModel;

using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{   
	//Normally step one of the setup process
	public class SetupWizardHome : WizardPanel
	{        
		TextImageButtonFactory setupButtonFactory = new TextImageButtonFactory();

		Button addProfileButton;
		Button editProfilesButton;
		Button setupWifiButton;
		Button troubleshootButton;
		Button accountButton;

		public SetupWizardHome(WizardWindow windowController)
			: base(windowController, unlocalizedTextForCancelButton: "Done")
		{
			headerLabel.Text = "Setup Options".Localize();
			SetButtonAttributes();

			contentRow.AddChild(new SetupPrinterView(this.wizardWindow));
			contentRow.AddChild(new SetupAccountView(this.wizardWindow));

			contentRow.AddChild(new EnterCodesView(this.wizardWindow));

			GuiWidget hSpacer = new GuiWidget();
			hSpacer.HAnchor = HAnchor.ParentLeftRight;

			//Add buttons to buttonContainer
			footerRow.AddChild(hSpacer);
			footerRow.AddChild(cancelButton);

			cancelButton.Text = "Back".Localize();
		}

		private void SetButtonAttributes()
		{   
			setupButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			setupButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			setupButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
			setupButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
			setupButtonFactory.normalFillColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			setupButtonFactory.fontSize = 16;
			setupButtonFactory.FixedWidth = 420;
			setupButtonFactory.ImageSpacing = 20;
		}
	}

	public class EnterCodesView : SetupViewBase
	{
		public static EventHandler RedeemDesignCode;
		public static EventHandler EnterShareCode;

		public EnterCodesView(WizardWindow windowController)
			: base("", windowController)
		{
			FlowLayoutWidget buttonContainer = new FlowLayoutWidget();
			buttonContainer.HAnchor = HAnchor.ParentLeftRight;
			buttonContainer.Margin = new BorderDouble(0, 14);

			mainContainer.AddChild(buttonContainer);

			if (UserSettings.Instance.IsTouchScreen)
			{
				// the redeem design code button
				{
					Button redeemPurchaseButton = textImageButtonFactory.Generate("Redeem Purchase".Localize());
					redeemPurchaseButton.Enabled = true; // The library selector (the first library selected) is protected so we can't add to it.
					redeemPurchaseButton.Name = "Redeem Code Button";
					buttonContainer.AddChild(redeemPurchaseButton);
					redeemPurchaseButton.Margin = new BorderDouble(0, 0, 10, 0);
					redeemPurchaseButton.Click += (sender, e) =>
					{
						if (RedeemDesignCode != null)
						{
							RedeemDesignCode(this, null);
						}
					};
				}

				// the redeem a share code button
				{
					Button redeemShareButton = textImageButtonFactory.Generate("Enter Share Code".Localize());
					redeemShareButton.Enabled = true; // The library selector (the first library selected) is protected so we can't add to it.
					redeemShareButton.Name = "Enter Share Code";
					buttonContainer.AddChild(redeemShareButton);
					redeemShareButton.Margin = new BorderDouble(0, 0, 3, 0);
					redeemShareButton.Click += (sender, e) =>
					{
						if (EnterShareCode != null)
						{
							EnterShareCode(this, null);
						}
					};
				}
			}
		}
	}

	public class SetupPrinterView : SetupViewBase
	{
		Button disconnectButton;

		TextWidget connectionStatus;

		event EventHandler unregisterEvents;

		public SetupPrinterView(WizardWindow windowController)
			: base("Printer Profile", windowController)
		{
			var buttonContainer = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble (0, 14)
			};

			var printerSelector = new PrinterSelector();
			printerSelector.AddPrinter += (s, e) => this.windowController.ChangeToSetupPrinterForm();
			buttonContainer.AddChild(printerSelector);

			disconnectButton = textImageButtonFactory.Generate("Disconnect");
			disconnectButton.Margin = new BorderDouble(left: 12);
			disconnectButton.VAnchor = VAnchor.ParentCenter;
			disconnectButton.Click += (sender, e) =>
			{
				PrinterConnectionAndCommunication.Instance.Disable();
				windowController.ChangeToHome();
			};
			buttonContainer.AddChild(disconnectButton);

			mainContainer.AddChild(buttonContainer);

			connectionStatus = new TextWidget("Status:", pointSize: 12, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				HAnchor = HAnchor.ParentLeftRight
			};
			mainContainer.AddChild(connectionStatus);

			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(updateConnectedState, ref unregisterEvents);
			updateConnectedState(null, null);
		}

		private void updateConnectedState(object sender, EventArgs e)
		{
			if (disconnectButton != null)
			{
				disconnectButton.Visible = PrinterConnectionAndCommunication.Instance.PrinterIsConnected;
			}

			if (connectionStatus != null)
			{
				connectionStatus.Text = string.Format ("{0}: {1}", "Status".Localize().ToUpper(), PrinterConnectionAndCommunication.Instance.PrinterConnectionStatusVerbose);
			}

			this.Invalidate();
		}
	}

	public class SetupAccountView : SetupViewBase
	{
		Button signInButton;
		Button signOutButton;
		event EventHandler unregisterEvents;
		TextWidget statusMessage;

		public SetupAccountView(WizardWindow windowController)
			: base("My Account", windowController)
		{
			bool signedIn = true;
			string username = ApplicationController.Instance.GetSessionUsername();
			if (username == null)
			{
				signedIn = false;
				username = "Not Signed In";
			}

			mainContainer.AddChild(new TextWidget(username, pointSize: 16, textColor: ActiveTheme.Instance.PrimaryTextColor));
			//mainContainer.AddChild(new TextWidget(statusDescription, pointSize: 12, textColor: ActiveTheme.Instance.PrimaryTextColor));

			FlowLayoutWidget buttonContainer = new FlowLayoutWidget();
			buttonContainer.HAnchor = HAnchor.ParentLeftRight;
			buttonContainer.Margin = new BorderDouble(0, 14);

			signInButton = textImageButtonFactory.Generate("Sign In");
			signInButton.Margin = new BorderDouble(left: 0);
			signInButton.VAnchor = VAnchor.ParentCenter;
			signInButton.Click += new EventHandler(signInButton_Click);
			signInButton.Visible = !signedIn;
			buttonContainer.AddChild(signInButton);

			signOutButton = textImageButtonFactory.Generate("Sign Out");
			signOutButton.Margin = new BorderDouble(left: 0);
			signOutButton.VAnchor = VAnchor.ParentCenter;
			signOutButton.Click += new EventHandler(signOutButton_Click);
			signOutButton.Visible = signedIn;		

			buttonContainer.AddChild(signOutButton);

			statusMessage = new TextWidget("Please wait...", pointSize: 12, textColor: ActiveTheme.Instance.SecondaryAccentColor);
			statusMessage.Visible = false;

			buttonContainer.AddChild(statusMessage);

			mainContainer.AddChild(buttonContainer);

			ApplicationController.Instance.DoneReloadingAll.RegisterEvent(onDoneReloading, ref unregisterEvents);

		}

		void onDoneReloading(object sender, EventArgs e)
		{
			this.windowController.ChangeToHome();
		}

		void signInButton_Click(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{ 
				signInButton.Visible = false;
				signOutButton.Visible = false;
				statusMessage.Visible = true;
				ApplicationController.Instance.StartLogin();
			});
		}

		void signOutButton_Click(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{ 
				signInButton.Visible = false;
				signOutButton.Visible = false;
				statusMessage.Visible = true;
				ApplicationController.Instance.StartLogout();
			});
		}
	}

	public class SetupViewBase : AltGroupBox
	{
		protected readonly int TallButtonHeight = 28;
		protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		protected LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
		protected RGBA_Bytes separatorLineColor;
		protected FlowLayoutWidget mainContainer;
		protected WizardWindow windowController;

		public SetupViewBase(string title,WizardWindow windowController)
			: base(title != "" ? new TextWidget(title, pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor) : null)
		{
			this.windowController = windowController;

			SetDisplayAttributes();
			mainContainer = new FlowLayoutWidget(Agg.UI.FlowDirection.TopToBottom);
			mainContainer.HAnchor = HAnchor.ParentLeftRight;
			mainContainer.Margin = new BorderDouble(6,0,0,6);
			AddChild(mainContainer);
		}

		private void SetDisplayAttributes()
		{
			//this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.separatorLineColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 100);
			this.Margin = new BorderDouble(2, 10, 2, 0);
			this.textImageButtonFactory.normalFillColor = RGBA_Bytes.Transparent;
			this.textImageButtonFactory.disabledFillColor = RGBA_Bytes.White;

			this.textImageButtonFactory.FixedHeight = TallButtonHeight;
			this.textImageButtonFactory.fontSize = 16;
			this.textImageButtonFactory.borderWidth = 1;
			this.textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
			this.textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

			this.textImageButtonFactory.disabledTextColor = RGBA_Bytes.DarkGray;
			this.textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.textImageButtonFactory.normalTextColor = ActiveTheme.Instance.SecondaryTextColor;
			this.textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

			this.linkButtonFactory.fontSize = 11;
		}       
	}
}
