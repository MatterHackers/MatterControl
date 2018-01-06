/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.SerialPortCommunication.FrostedSerial;

namespace MatterHackers.MatterControl.ActionBar
{
	public class PrinterConnectButton : FlowLayoutWidget
	{
		private readonly string disconnectAndCancelTitle = "Disconnect and stop the current print?".Localize();
		private readonly string disconnectAndCancelMessage = "WARNING: Disconnecting will stop the current print.\n\nAre you sure you want to disconnect?".Localize();

		private Button cancelConnectButton;
		private GuiWidget connectButton;
		private Button disconnectButton;

		private EventHandler unregisterEvents;
		private PrinterConfig printer;

		private bool listenForConnectFailed = false;
		private long connectStartMs;

		public PrinterConnectButton(PrinterConfig printer, ThemeConfig theme)
		{
			this.printer = printer;
			this.HAnchor = HAnchor.Left | HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;
			this.Margin = 0;
			this.Padding = 0;

			connectButton = theme.ButtonFactory.Generate("Connect".Localize(), AggContext.StaticData.LoadIcon("connect.png", 14, 14, IconColor.Theme));
			connectButton.Name = "Connect to printer button";
			connectButton.ToolTipText = "Connect to the currently selected printer".Localize();
			connectButton.Click += (s, e) =>
			{
				if (connectButton.Enabled)
				{
					if (printer.Settings.PrinterSelected)
					{
						UserRequestedConnectToActivePrinter();
					}
				}
			};
			this.AddChild(connectButton);

			// add the cancel stop button
			cancelConnectButton = theme.ButtonFactory.Generate("Cancel".Localize(), AggContext.StaticData.LoadIcon("connect.png", 14, 14, IconColor.Theme));
			cancelConnectButton.ToolTipText = "Stop trying to connect to the printer.".Localize();
			cancelConnectButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				listenForConnectFailed = false;
				ApplicationController.Instance.ConditionalCancelPrint();
				cancelConnectButton.Enabled = false;
			});
			this.AddChild(cancelConnectButton);

			disconnectButton = theme.ButtonFactory.Generate("Disconnect".Localize(), AggContext.StaticData.LoadIcon("connect.png", 14, 14, IconColor.Theme));
			disconnectButton.Name = "Disconnect from printer button";
			disconnectButton.Visible = false;
			disconnectButton.ToolTipText = "Disconnect from current printer".Localize();
			disconnectButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				if (printer.Connection.PrinterIsPrinting)
				{
					StyledMessageBox.ShowMessageBox(
						(bool disconnectCancel) =>
						{
							if (disconnectCancel)
							{
								printer.Connection.Stop(false);
								printer.Connection.Disable();
							}
						},
						disconnectAndCancelMessage,
						disconnectAndCancelTitle,
						StyledMessageBox.MessageType.YES_NO,
						"Disconnect".Localize(),
						"Stay Connected".Localize());
				}
				else
				{
					printer.Connection.Disable();
				}
			});
			this.AddChild(disconnectButton);

			foreach (var child in Children)
			{
				child.VAnchor = VAnchor.Center;
				child.Cursor = Cursors.Hand;
				child.Margin = theme.ButtonSpacing;
			}

			printer.Connection.EnableChanged.RegisterEvent((s, e) => SetVisibleStates(), ref unregisterEvents);
			printer.Connection.CommunicationStateChanged.RegisterEvent((s, e) => SetVisibleStates(), ref unregisterEvents);
			printer.Connection.ConnectionFailed.RegisterEvent((s, e) =>
			{
#if !__ANDROID__
				// TODO: Someday this functionality should be revised to an awaitable Connect() call in the Connect button that
				// shows troubleshooting on failed attempts, rather than hooking the failed event and trying to determine if the
				// Connect button started the task
				if (listenForConnectFailed
					&& UiThread.CurrentTimerMs - connectStartMs < 25000)
				{
					// User initiated connect attempt failed, show port selection dialog
					DialogWindow.Show(new SetupStepComPortOne(printer));
				}
#endif
				listenForConnectFailed = false;
			}, ref unregisterEvents);

			this.SetVisibleStates();
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public void UserRequestedConnectToActivePrinter()
		{
			if (printer.Settings.PrinterSelected)
			{
				listenForConnectFailed = true;
				connectStartMs = UiThread.CurrentTimerMs;

#if __ANDROID__
				if (!printer.Settings.GetValue<bool>(SettingsKey.enable_network_printing)
					&& !FrostedSerialPort.HasPermissionToDevice())
				{
					// Opens the USB device permissions dialog which will call back into our UsbDevice broadcast receiver to connect
					FrostedSerialPort.RequestPermissionToDevice(RunTroubleShooting);
				}
				else
#endif
				{
					printer.Connection.HaltConnectionThread();
					printer.Connection.Connect();
				}
			}
		}

		private static void RunTroubleShooting()
		{
			DialogWindow.Show<SetupWizardTroubleshooting>();
		}

		private void SetChildVisible(GuiWidget visibleChild, bool enabled)
		{
			foreach (var child in Children)
			{
				if (child == visibleChild)
				{
					child.Visible = true;
					child.Enabled = enabled;
				}
				else
				{
					child.Visible = false;
				}
			}
		}

		private void SetVisibleStates()
		{
			switch (printer.Connection.CommunicationState)
			{
				case CommunicationStates.FailedToConnect:
				case CommunicationStates.Disconnected:
				case CommunicationStates.ConnectionLost:
					SetChildVisible(connectButton, true);
					break;

				case CommunicationStates.Disconnecting:
					SetChildVisible(disconnectButton, false);
					break;

				case CommunicationStates.AttemptingToConnect:
					SetChildVisible(cancelConnectButton, true);
					break;

				default:
					listenForConnectFailed = false;
					SetChildVisible(disconnectButton, true);
					break;
			}
		}
	}
}