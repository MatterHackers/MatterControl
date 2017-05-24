/*
Copyright (c) 2016, Lars Brubaker
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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ActionBar
{
	public class PrinterConnectAndSelectControl : FlowLayoutWidget
	{
		private Button connectPrinterButton;
		private Button editPrinterButton;
		private string disconnectAndCancelTitle = "Disconnect and stop the current print?".Localize();
		private string disconnectAndCancelMessage = "WARNING: Disconnecting will stop the current print.\n\nAre you sure you want to disconnect?".Localize();
		private Button disconnectPrinterButton;
		private PrinterSelector printerSelector;
		GuiWidget printerSelectorAndEditOverlay;

		private EventHandler unregisterEvents;
		static EventHandler staticUnregisterEvents;

		public PrinterConnectAndSelectControl()
		{
			this.HAnchor = HAnchor.ParentLeftRight;

			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			AddChildElements();
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		protected void AddChildElements()
		{
			var buttonFactory = ApplicationController.Instance.Theme.PrinterConnectButtonFactory;
			// connect and disconnect buttons
			{
				var normalImage = StaticData.Instance.LoadIcon("connect.png", 32, 32);

				// Create the image button with the normal and disabled ImageBuffers
				connectPrinterButton = buttonFactory.Generate("Connect".Localize().ToUpper(), normalImage);
				connectPrinterButton.Name = "Connect to printer button";
				connectPrinterButton.ToolTipText = "Connect to the currently selected printer".Localize();
				connectPrinterButton.Margin = new BorderDouble(0, 0, 3, 3);

				connectPrinterButton.VAnchor = VAnchor.ParentTop;
				connectPrinterButton.Cursor = Cursors.Hand;
				connectPrinterButton.Click += (s, e) =>
				{
					Button buttonClicked = ((Button)s);
					if (buttonClicked.Enabled)
					{
						if (ActiveSliceSettings.Instance.PrinterSelected)
						{
							UserRequestedConnectToActivePrinter();
						}
					}
				};

				disconnectPrinterButton = buttonFactory.Generate("Disconnect".Localize().ToUpper(), StaticData.Instance.LoadIcon("connect.png", 32, 32));
				disconnectPrinterButton.Name = "Disconnect from printer button";
				disconnectPrinterButton.ToolTipText = "Disconnect from current printer".Localize();
				disconnectPrinterButton.Margin = new BorderDouble(6, 0, 3, 3);
				disconnectPrinterButton.VAnchor = VAnchor.ParentTop;
				disconnectPrinterButton.Cursor = Cursors.Hand;
				disconnectPrinterButton.Click += (s, e) => UiThread.RunOnIdle(OnIdleDisconnect);

				this.AddChild(connectPrinterButton);
				this.AddChild(disconnectPrinterButton);
			}

			// printer selector and edit button
			{
				GuiWidget container = new GuiWidget()
				{
					HAnchor = HAnchor.ParentLeftRight,
					VAnchor = VAnchor.FitToChildren,
				};

				FlowLayoutWidget printerSelectorAndEditButton = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.ParentLeftRight,
				};

				printerSelector = new PrinterSelector()
				{
					HAnchor = HAnchor.ParentLeftRight,
					Cursor = Cursors.Hand,
					Margin = new BorderDouble(0, 6, 0, 3)
				};
				printerSelector.AddPrinter += (s, e) => WizardWindow.ShowPrinterSetup(true);
				// make sure the control can get smaller but maintains its height
				printerSelector.MinimumSize = new Vector2(0, connectPrinterButton.MinimumSize.y);
				printerSelectorAndEditButton.AddChild(printerSelector);

				editPrinterButton = TextImageButtonFactory.GetThemedEditButton();
				editPrinterButton.Name = "Edit Printer Button";
				editPrinterButton.VAnchor = VAnchor.ParentCenter;
				editPrinterButton.Click += UiNavigation.OpenEditPrinterWizard_Click;
				printerSelectorAndEditButton.AddChild(editPrinterButton);

				container.AddChild(printerSelectorAndEditButton);
				printerSelectorAndEditOverlay = new GuiWidget()
				{
					HAnchor = HAnchor.ParentLeftRight,
					VAnchor = VAnchor.ParentBottomTop,
					Selectable = false,
				};
				container.AddChild(printerSelectorAndEditOverlay);

				this.AddChild(container);
			}

			// reset connection button
			{
				string resetConnectionText = "Reset\nConnection".Localize().ToUpper();
				Button resetConnectionButton = buttonFactory.Generate(resetConnectionText, "e_stop4.png");
				resetConnectionButton.Margin = new BorderDouble(6, 0, 3, 3);
				this.AddChild(resetConnectionButton);

				resetConnectionButton.Click += (s, e) => 
				{
					UiThread.RunOnIdle(PrinterConnectionAndCommunication.Instance.RebootBoard);
				};
				resetConnectionButton.Visible = ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.show_reset_connection);

				ActiveSliceSettings.SettingChanged.RegisterEvent((sender, e) => 
				{
					StringEventArgs stringEvent = e as StringEventArgs;
					if (stringEvent != null)
					{
						if (stringEvent.Data == SettingsKey.show_reset_connection)
						{
							resetConnectionButton.Visible = ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.show_reset_connection);
						}
					}
				}, ref unregisterEvents);
			}

			// Bind connect button states to active printer state
			this.SetConnectionButtonVisibleState();

			PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
		}

		static public void UserRequestedConnectToActivePrinter()
		{
			if (staticUnregisterEvents != null)
			{
				staticUnregisterEvents(null, null);
				staticUnregisterEvents = null;
			}
			PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
			PrinterConnectionAndCommunication.Instance.ConnectToActivePrinter(true);
		}

		private void onConfirmStopPrint(bool messageBoxResponse)
		{
			if (messageBoxResponse)
			{
				PrinterConnectionAndCommunication.Instance.Stop(false);
				PrinterConnectionAndCommunication.Instance.Disable();
				printerSelector.Invalidate();
			}
		}

		private void OnIdleDisconnect()
		{
			if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting)
			{
				StyledMessageBox.ShowMessageBox(onConfirmStopPrint, disconnectAndCancelMessage, disconnectAndCancelTitle, StyledMessageBox.MessageType.YES_NO, "Disconnect".Localize(), "Stay Connected".Localize());
			}
			else
			{
				PrinterConnectionAndCommunication.Instance.Disable();
				printerSelector.Invalidate();
			}
		}

		private void onPrinterStatusChanged(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(SetConnectionButtonVisibleState);
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
			connectPrinterButton.Enabled = communicationState != PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect && ActiveSliceSettings.Instance.PrinterSelected;
			bool printerIsPrintigOrPause = PrinterConnectionAndCommunication.Instance.PrinterIsPrinting || PrinterConnectionAndCommunication.Instance.PrinterIsPaused;
			editPrinterButton.Enabled = ActiveSliceSettings.Instance.PrinterSelected && !printerIsPrintigOrPause;
			printerSelector.Enabled = !printerIsPrintigOrPause;
			if(printerIsPrintigOrPause)
			{
				printerSelectorAndEditOverlay.BackgroundColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryBackgroundColor, 150);
			}
			else
			{
				printerSelectorAndEditOverlay.BackgroundColor = new RGBA_Bytes(0, 0, 0, 0);
			}
			disconnectPrinterButton.Enabled = communicationState != PrinterConnectionAndCommunication.CommunicationStates.Disconnecting;
		}
	}
}