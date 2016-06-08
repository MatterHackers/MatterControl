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
		public SetupWizardHome()
			: base(unlocalizedTextForCancelButton: "Done")
		{
			headerLabel.Text = "Setup Options".Localize();

			textImageButtonFactory.borderWidth = 1;
			textImageButtonFactory.normalBorderColor = RGBA_Bytes.White;

			contentRow.AddChild(new SetupPrinterView(this.textImageButtonFactory) { WizardPanel = this });
			contentRow.AddChild(new SetupAccountView(this.textImageButtonFactory));
			contentRow.AddChild(new EnterCodesView(this.textImageButtonFactory));

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

		public EnterCodesView(TextImageButtonFactory textImageButtonFactory) : base("")
		{
			this.textImageButtonFactory = textImageButtonFactory;
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
		internal WizardPanel WizardPanel { get; set; }

		private Button disconnectButton;
		private TextWidget connectionStatus;
		private event EventHandler unregisterEvents;

		public SetupPrinterView(TextImageButtonFactory textImageButtonFactory)
			: base("Printer Profile")
		{
			this.textImageButtonFactory = textImageButtonFactory;

			var buttonContainer = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble (0, 14)
			};
			mainContainer.AddChild(buttonContainer);

			var printerSelectorAndEditButton = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
			};
			buttonContainer.AddChild(printerSelectorAndEditButton);

			var printerSelector = new PrinterSelector();
			printerSelector.AddPrinter += (s, e) => WizardPanel.WizardWindow.ChangeToSetupPrinterForm();
			printerSelectorAndEditButton.AddChild(printerSelector);

			var editButton = TextImageButtonFactory.GetThemedEditButton();
			editButton.VAnchor = VAnchor.ParentCenter;
			editButton.Click += UiNavigation.GoToEditPrinter_Click;
			printerSelectorAndEditButton.AddChild(editButton);

			disconnectButton = textImageButtonFactory.Generate("Disconnect");
			disconnectButton.Margin = new BorderDouble(left: 12);
			disconnectButton.VAnchor = VAnchor.ParentCenter;
			disconnectButton.Click += (sender, e) =>
			{
				PrinterConnectionAndCommunication.Instance.Disable();
				WizardPanel.WizardWindow.ChangeToPanel<SetupWizardHome>();
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

		public SetupAccountView(TextImageButtonFactory textImageButtonFactory)
			: base("My Account")
		{
			this.textImageButtonFactory = textImageButtonFactory;

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
			signInButton.Visible = !signedIn;
			signInButton.Click +=  (s, e) => UiThread.RunOnIdle(() =>
			{
				signInButton.Visible = false;
				signOutButton.Visible = false;
				statusMessage.Visible = true;
				ApplicationController.Instance.StartLogin();
			});
			buttonContainer.AddChild(signInButton);

			signOutButton = textImageButtonFactory.Generate("Sign Out");
			signOutButton.Margin = new BorderDouble(left: 0);
			signOutButton.VAnchor = VAnchor.ParentCenter;
			signOutButton.Visible = signedIn;
			signOutButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				signInButton.Visible = false;
				signOutButton.Visible = false;
				statusMessage.Visible = true;
				ApplicationController.Instance.StartLogout();
			});
			buttonContainer.AddChild(signOutButton);

			statusMessage = new TextWidget("Please wait...", pointSize: 12, textColor: ActiveTheme.Instance.SecondaryAccentColor);
			statusMessage.Visible = false;
			buttonContainer.AddChild(statusMessage);

			mainContainer.AddChild(buttonContainer);
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
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(6, 0, 0, 6)
			};
			AddChild(mainContainer);
		}
	}
}
