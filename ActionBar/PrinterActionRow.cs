using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;

namespace MatterHackers.MatterControl.ActionBar
{
	internal class PrinterActionRow : ActionRowBase
	{
		private TextImageButtonFactory actionBarButtonFactory = new TextImageButtonFactory();
		private Button connectPrinterButton;
		private Button disconnectPrinterButton;
		private Button selectActivePrinterButton;
		private Button resetConnectionButton;

		private ConnectionWindow connectionWindow;
		private bool connectionWindowIsOpen = false;

		protected override void Initialize()
		{
			actionBarButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			actionBarButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			actionBarButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

			actionBarButtonFactory.disabledTextColor = ActiveTheme.Instance.TabLabelUnselected;
			actionBarButtonFactory.disabledFillColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			actionBarButtonFactory.disabledBorderColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			actionBarButtonFactory.hoverFillColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			actionBarButtonFactory.invertImageLocation = true;
			actionBarButtonFactory.borderWidth = 0;
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
		}

		protected override void AddChildElements()
		{
			actionBarButtonFactory.invertImageLocation = false;
			actionBarButtonFactory.borderWidth = 1;
			if (ActiveTheme.Instance.IsDarkTheme)
			{
				actionBarButtonFactory.normalBorderColor = new RGBA_Bytes(77, 77, 77);
			}
			else
			{
				actionBarButtonFactory.normalBorderColor = new RGBA_Bytes(190, 190, 190);
			}
			actionBarButtonFactory.hoverBorderColor = new RGBA_Bytes(128, 128, 128);

			string connectString = "Connect".Localize().ToUpper();
			connectPrinterButton = actionBarButtonFactory.Generate(connectString, "icon_power_32x32.png");
			if (ApplicationController.Instance.WidescreenMode)
			{
				connectPrinterButton.Margin = new BorderDouble(0, 0, 3, 3);
			}
			else
			{
				connectPrinterButton.Margin = new BorderDouble(6, 0, 3, 3);
			}
			connectPrinterButton.VAnchor = VAnchor.ParentTop;
			connectPrinterButton.Cursor = Cursors.Hand;

			string disconnectString = "Disconnect".Localize().ToUpper();
			disconnectPrinterButton = actionBarButtonFactory.Generate(disconnectString, "icon_power_32x32.png");
			if (ApplicationController.Instance.WidescreenMode)
			{
				disconnectPrinterButton.Margin = new BorderDouble(0, 0, 3, 3);
			}
			else
			{
				disconnectPrinterButton.Margin = new BorderDouble(6, 0, 3, 3);
			}
			disconnectPrinterButton.VAnchor = VAnchor.ParentTop;
			disconnectPrinterButton.Cursor = Cursors.Hand;

			selectActivePrinterButton = new PrinterSelectButton();
			selectActivePrinterButton.HAnchor = HAnchor.ParentLeftRight;
			selectActivePrinterButton.Cursor = Cursors.Hand;
			if (ApplicationController.Instance.WidescreenMode)
			{
				selectActivePrinterButton.Margin = new BorderDouble(0, 6, 0, 3);
			}
			else
			{
				selectActivePrinterButton.Margin = new BorderDouble(0, 6, 6, 3);
			}

			string resetConnectionText = "Reset\nConnection".Localize().ToUpper();
			resetConnectionButton = actionBarButtonFactory.Generate(resetConnectionText, "e_stop4.png");
			if (ApplicationController.Instance.WidescreenMode)
			{
				resetConnectionButton.Margin = new BorderDouble(0, 0, 3, 3);
			}
			else
			{
				resetConnectionButton.Margin = new BorderDouble(6, 0, 3, 3);
			}

			// Bind connect button states to active printer state
			this.SetConnectionButtonVisibleState(null);

			actionBarButtonFactory.invertImageLocation = true;

			this.AddChild(connectPrinterButton);
			this.AddChild(disconnectPrinterButton);
			this.AddChild(selectActivePrinterButton);
			this.AddChild(resetConnectionButton);
			//this.AddChild(CreateOptionsMenu());
		}

		private event EventHandler unregisterEvents;

		protected override void AddHandlers()
		{
			ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(ReloadPrinterSelectionWidget, ref unregisterEvents);
			ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(onActivePrinterChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);

			selectActivePrinterButton.Click += new EventHandler(onSelectActivePrinterButton_Click);
			connectPrinterButton.Click += new EventHandler(onConnectButton_Click);
			disconnectPrinterButton.Click += new EventHandler(onDisconnectButtonClick);
			resetConnectionButton.Click += new EventHandler(resetConnectionButton_Click);

			base.AddHandlers();
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		private void onConnectButton_Click(object sender, EventArgs mouseEvent)
		{
			Button buttonClicked = ((Button)sender);
			if (buttonClicked.Enabled)
			{
				if (ActivePrinterProfile.Instance.ActivePrinter == null)
				{
					OpenConnectionWindow(ConnectToActivePrinter);
				}
				else
				{
					ConnectToActivePrinter();
				}
			}
		}

		private void resetConnectionButton_Click(object sender, EventArgs mouseEvent)
		{
			PrinterConnectionAndCommunication.Instance.RebootBoard();
		}

		private void ConnectToActivePrinter()
		{
			PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
			PrinterConnectionAndCommunication.Instance.ConnectToActivePrinter();
		}

		private void onSelectActivePrinterButton_Click(object sender, EventArgs mouseEvent)
		{
			OpenConnectionWindow();
		}

		public delegate void ConnectOnSelectFunction();

		private ConnectOnSelectFunction functionToCallOnSelect;

		private void OpenConnectionWindow(ConnectOnSelectFunction functionToCallOnSelect = null)
		{
			if (this.connectionWindowIsOpen == false)
			{
				connectionWindow = new ConnectionWindow();
				this.connectionWindowIsOpen = true;

				//This function gets called on printer selection (see onActivePrinterChanged)
				this.functionToCallOnSelect = functionToCallOnSelect;

				connectionWindow.Closed += new EventHandler(ConnectionWindow_Closed);
			}
			else
			{
				if (connectionWindow != null)
				{
					connectionWindow.BringToFront();
				}
			}
		}

		private void ConnectionWindow_Closed(object sender, EventArgs e)
		{
			this.connectionWindowIsOpen = false;
		}

		private void ReloadPrinterSelectionWidget(object sender, EventArgs e)
		{
			//selectActivePrinterButton.Invalidate();
		}

		private void onActivePrinterChanged(object sender, EventArgs e)
		{
			connectPrinterButton.Enabled = true;
			if (functionToCallOnSelect != null)
			{
				functionToCallOnSelect();
				functionToCallOnSelect = null;
			}
		}

		private void onDisconnectButtonClick(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(OnIdleDisconnect);
		}

		private string disconnectAndCancelMessage = "Disconnect and cancel the current print?".Localize();
		private string disconnectAndCancelTitle = "WARNING: Disconnecting will cancel the print.".Localize();

		private void OnIdleDisconnect(object state)
		{
			if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting)
			{
				StyledMessageBox.ShowMessageBox(onConfirmStopPrint, disconnectAndCancelMessage, disconnectAndCancelTitle, StyledMessageBox.MessageType.YES_NO);
			}
			else
			{
				PrinterConnectionAndCommunication.Instance.Disable();
				selectActivePrinterButton.Invalidate();
			}
		}

		private void onConfirmStopPrint(bool messageBoxResponse)
		{
			if (messageBoxResponse)
			{
				PrinterConnectionAndCommunication.Instance.Stop();
				PrinterConnectionAndCommunication.Instance.Disable();
				selectActivePrinterButton.Invalidate();
			}
		}

		private void SetConnectionButtonVisibleState(object state)
		{
			if (PrinterConnectionAndCommunication.Instance.PrinterIsConnected)
			{
				disconnectPrinterButton.Visible = true;
				connectPrinterButton.Visible = false;
			}
			else
			{
				disconnectPrinterButton.Visible = false;
				connectPrinterButton.Visible = true;
			}

			var communicationState = PrinterConnectionAndCommunication.Instance.CommunicationState;

			// Ensure connect buttons are locked while long running processes are executing to prevent duplicate calls into said actions
			connectPrinterButton.Enabled = communicationState != PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect;
			disconnectPrinterButton.Enabled = communicationState != PrinterConnectionAndCommunication.CommunicationStates.Disconnecting;
			resetConnectionButton.Visible = ActiveSliceSettings.Instance.ShowResetConnection();
		}

		private void onPrinterStatusChanged(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(SetConnectionButtonVisibleState);
		}
	}
}