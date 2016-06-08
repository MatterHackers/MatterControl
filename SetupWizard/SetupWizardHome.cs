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
	//Normally step one of the setup process
	public class SetupWizardHome : WizardPanel
	{
		public SetupWizardHome()
			: base(unlocalizedTextForCancelButton: "Done")
		{
			headerLabel.Text = "Setup Options".Localize();

			contentRow.AddChild(new SetupPrinterView() { WizardWindow = this.WizardWindow });
			contentRow.AddChild(new SetupAccountView() { WizardWindow = this.WizardWindow });
			contentRow.AddChild(new EnterCodesView() { WizardWindow = this.WizardWindow });

			GuiWidget hSpacer = new GuiWidget();
			hSpacer.HAnchor = HAnchor.ParentLeftRight;

			//Add buttons to buttonContainer
			footerRow.AddChild(hSpacer);
			footerRow.AddChild(cancelButton);

			cancelButton.Text = "Back".Localize();
		}
	}

	public class EnterCodesView : SetupViewBase
	{
		public static EventHandler RedeemDesignCode;
		public static EventHandler EnterShareCode;

		public EnterCodesView() : base("")
		{
			FlowLayoutWidget buttonContainer = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(0, 14)
			};
			mainContainer.AddChild(buttonContainer);

			if (UserSettings.Instance.IsTouchScreen)
			{
				// the redeem design code button
				Button redeemPurchaseButton = textImageButtonFactory.Generate("Redeem Purchase".Localize());
				redeemPurchaseButton.Enabled = true; // The library selector (the first library selected) is protected so we can't add to it.
				redeemPurchaseButton.Name = "Redeem Code Button";
				redeemPurchaseButton.Margin = new BorderDouble(0, 0, 10, 0);
				redeemPurchaseButton.Click += (sender, e) =>
				{
					RedeemDesignCode?.Invoke(this, null);
				};
				buttonContainer.AddChild(redeemPurchaseButton);

				// the redeem a share code button
				Button redeemShareButton = textImageButtonFactory.Generate("Enter Share Code".Localize());
				redeemShareButton.Enabled = true; // The library selector (the first library selected) is protected so we can't add to it.
				redeemShareButton.Name = "Enter Share Code";
				redeemShareButton.Margin = new BorderDouble(0, 0, 3, 0);
				redeemShareButton.Click += (sender, e) =>
				{
					EnterShareCode?.Invoke(this, null);
				};

				buttonContainer.AddChild(redeemShareButton);
			}
		}
	}

	public class SetupPrinterView : SetupViewBase
	{
		private Button disconnectButton;
		private TextWidget connectionStatus;
		private event EventHandler unregisterEvents;

		public SetupPrinterView()
			: base("Printer Profile")
		{
			var buttonContainer = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble (0, 14)
			};
			mainContainer.AddChild(buttonContainer);

			var printerSelector = new PrinterSelector();
			printerSelector.AddPrinter += (s, e) => WizardWindow.ChangeToSetupPrinterForm();
			FlowLayoutWidget printerSelectorAndEditButton = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
			};
			printerSelectorAndEditButton.AddChild(printerSelector);
			Button editButton = TextImageButtonFactory.GetThemedEditButton();
			editButton.VAnchor = VAnchor.ParentCenter;
			editButton.Click += UiNavigation.GoToEditPrinter_Click;
			printerSelectorAndEditButton.AddChild(editButton);
			buttonContainer.AddChild(printerSelectorAndEditButton);

			disconnectButton = textImageButtonFactory.Generate("Disconnect");
			disconnectButton.Margin = new BorderDouble(left: 12);
			disconnectButton.VAnchor = VAnchor.ParentCenter;
			disconnectButton.Click += (sender, e) =>
			{
				PrinterConnectionAndCommunication.Instance.Disable();
				WizardWindow.ChangeToPanel<SetupWizardHome>();
			};
			buttonContainer.AddChild(disconnectButton);

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
		private Button signInButton;
		private Button signOutButton;
		private TextWidget statusMessage;

		public SetupAccountView()
			: base("My Account")
		{
			bool signedIn = true;
			string username = ApplicationController.Instance.GetSessionUsername();
			if (username == null)
			{
				signedIn = false;
				username = "Not Signed In";
			}

			mainContainer.AddChild(new TextWidget(username, pointSize: 16, textColor: ActiveTheme.Instance.PrimaryTextColor));

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
		protected TextImageButtonFactory textImageButtonFactory;
		protected FlowLayoutWidget mainContainer;

		internal WizardWindow WizardWindow { get; set; }

		public SetupViewBase(string title)
			: base(title != "" ? new TextWidget(title, pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor) : null)
		{
			this.Margin = new BorderDouble(2, 10, 2, 0);

			textImageButtonFactory = new TextImageButtonFactory()
			{
				normalFillColor = RGBA_Bytes.Transparent,
				disabledFillColor = RGBA_Bytes.White,
				FixedHeight = TallButtonHeight,
				fontSize = 16,
				borderWidth = 1,
				normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200),
				hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200),
				disabledTextColor = RGBA_Bytes.DarkGray,
				hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
				normalTextColor = ActiveTheme.Instance.SecondaryTextColor,
				pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
			};

			mainContainer = new FlowLayoutWidget(Agg.UI.FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(6, 0, 0, 6)
			};
			AddChild(mainContainer);
		}
	}
}
