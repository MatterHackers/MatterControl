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
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.SerialPortCommunication.FrostedSerial;

namespace MatterHackers.MatterControl.ActionBar
{
	public class PrinterConnectButton : GuiWidget
	{
		private readonly string disconnectAndCancelTitle = "Disconnect and stop the current print?".Localize();
		private readonly string disconnectAndCancelMessage = "WARNING: Disconnecting will stop the current print.\n\nAre you sure you want to disconnect?".Localize();

		private GuiWidget connectButton;
		private Button disconnectButton;

		private EventHandler unregisterEvents;
		private PrinterConfig printer;

		public PrinterConnectButton(PrinterConfig printer, ThemeConfig theme)
		{
			this.printer = printer;
			this.HAnchor = HAnchor.Left | HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;
			this.Margin = 0;
			this.Padding = 0;

			connectButton = theme.ButtonFactory.Generate("Connect".Localize().ToUpper(), AggContext.StaticData.LoadIcon("connect.png", 14, 14, IconColor.Theme));
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

			disconnectButton = theme.ButtonFactory.Generate("Disconnect".Localize().ToUpper(), AggContext.StaticData.LoadIcon("connect.png", 14, 14, IconColor.Theme));
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
				child.VAnchor = VAnchor.Top;
				child.HAnchor = HAnchor.Left;
				child.Cursor = Cursors.Hand;
				child.Margin = theme.ButtonSpacing;
			}

			// Bind connect button states to active printer state
			this.SetVisibleStates(null, null);

			printer.Connection.EnableChanged.RegisterEvent(SetVisibleStates, ref unregisterEvents);
			printer.Connection.CommunicationStateChanged.RegisterEvent(SetVisibleStates, ref unregisterEvents);
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
					printer.Connection.Connect(true);
				}
			}
		}

		private static void RunTroubleShooting()
		{
			DialogWindow.Show<SetupWizardTroubleshooting>();
		}

		private void SetVisibleStates(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				if (printer.Connection.PrinterIsConnected)
				{
					disconnectButton.Visible = true;
					connectButton.Visible = false;
				}
				else
				{
					disconnectButton.Visible = false;
					connectButton.Visible = true;
				}

				var communicationState = printer.Connection.CommunicationState;

				// Ensure connect buttons are locked while long running processes are executing to prevent duplicate calls into said actions
				connectButton.Enabled = printer.Settings.PrinterSelected
					&& communicationState != CommunicationStates.AttemptingToConnect;

				disconnectButton.Enabled = communicationState != CommunicationStates.Disconnecting;
			});
		}
	}
}