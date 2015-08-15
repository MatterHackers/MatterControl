using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;

namespace MatterHackers.MatterControl.ActionBar
{
	public class PrinterActionRow : ActionRowBase
	{
		static private ConnectionWindow connectionWindow;
		private TextImageButtonFactory actionBarButtonFactory = new TextImageButtonFactory();
		private Button connectPrinterButton;
		private string disconnectAndCancelMessage = "Disconnect and cancel the current print?".Localize();
		private string disconnectAndCancelTitle = "WARNING: Disconnecting will cancel the print.".Localize();
		private Button disconnectPrinterButton;
		private Button resetConnectionButton;
		private Button selectActivePrinterButton;

		private event EventHandler unregisterEvents;
		static EventHandler staticUnregisterEvents;

		public static void OpenConnectionWindow(bool connectAfterSelection = false)
		{
			if (connectAfterSelection)
			{
				ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(ConnectToActivePrinter, ref staticUnregisterEvents);
			}

			if (connectionWindow == null)
			{
				connectionWindow = new ConnectionWindow();

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

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
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
			connectPrinterButton.ToolTipText = "Connect to the currently selected printer".Localize();
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
			disconnectPrinterButton.ToolTipText = "Disconnect from current printer".Localize();
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
			this.SetConnectionButtonVisibleState();

			actionBarButtonFactory.invertImageLocation = true;

			this.AddChild(connectPrinterButton);
			this.AddChild(disconnectPrinterButton);
			this.AddChild(selectActivePrinterButton);
			this.AddChild(resetConnectionButton);
			//this.AddChild(CreateOptionsMenu());
		}

		protected override void AddHandlers()
		{
			ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(onActivePrinterChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);

			selectActivePrinterButton.Click += new EventHandler(onSelectActivePrinterButton_Click);
			connectPrinterButton.Click += new EventHandler(onConnectButton_Click);
			disconnectPrinterButton.Click += new EventHandler(onDisconnectButtonClick);
			resetConnectionButton.Click += new EventHandler(resetConnectionButton_Click);

			base.AddHandlers();
		}

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
		static private void ConnectionWindow_Closed(object sender, EventArgs e)
		{
			connectionWindow = null;
		}

		static public void ConnectToActivePrinter(object sender, EventArgs e)
		{
			if (staticUnregisterEvents != null)
			{
				staticUnregisterEvents(null, e);
				staticUnregisterEvents = null;
			}
			PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
			PrinterConnectionAndCommunication.Instance.ConnectToActivePrinter();
		}

		private void onActivePrinterChanged(object sender, EventArgs e)
		{
			connectPrinterButton.Enabled = true;
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

		private void onConnectButton_Click(object sender, EventArgs mouseEvent)
		{
			Button buttonClicked = ((Button)sender);
			if (buttonClicked.Enabled)
			{
				if (ActivePrinterProfile.Instance.ActivePrinter == null)
				{
					OpenConnectionWindow(true);
				}
				else
				{
					ConnectToActivePrinter(null, null);
				}
			}
		}

		private void onDisconnectButtonClick(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(OnIdleDisconnect);
		}

		private void OnIdleDisconnect()
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

		private void onPrinterStatusChanged(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(SetConnectionButtonVisibleState);
		}

		private void onSelectActivePrinterButton_Click(object sender, EventArgs mouseEvent)
		{
			OpenConnectionWindow();
		}

		private void resetConnectionButton_Click(object sender, EventArgs mouseEvent)
		{
			PrinterConnectionAndCommunication.Instance.RebootBoard();
		}
		private void SetConnectionButtonVisibleState()
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
	}
}