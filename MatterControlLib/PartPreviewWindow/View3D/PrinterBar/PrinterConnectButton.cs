/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterControl.Printing;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.ActionBar
{
	public class PrinterConnectButton : FlowLayoutWidget
	{
		private GuiWidget cancelConnectButton;
		private GuiWidget connectButton;
		private GuiWidget disconnectButton;

		private PrinterConfig printer;

		public PrinterConnectButton(PrinterConfig printer, ThemeConfig theme)
		{
			this.printer = printer;
			this.HAnchor = HAnchor.Left | HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;
			this.Margin = 0;
			this.Padding = 0;

			connectButton = new TextIconButton(
				"Connect".Localize(),
				AggContext.StaticData.LoadIcon("connect.png", 14, 14, theme.InvertIcons),
				theme)
			{
				Name = "Connect to printer button",
				ToolTipText = "Connect to the currently selected printer".Localize(),
				MouseDownColor = theme.ToolbarButtonDown,
			};
			connectButton.Click += (s, e) =>
			{
				if (connectButton.Enabled)
				{
					ApplicationController.Instance.ConnectToPrinter(printer);
				}
			};
			this.AddChild(connectButton);

			theme.ApplyPrimaryActionStyle(connectButton);

			// add the cancel stop button
			cancelConnectButton = new TextIconButton(
				"Cancel".Localize(),
				AggContext.StaticData.LoadIcon("connect.png", 14, 14, theme.InvertIcons),
				theme)
			{
				ToolTipText = "Stop trying to connect to the printer.".Localize(),
				BackgroundColor = theme.ToolbarButtonBackground,
				HoverColor = theme.ToolbarButtonHover,
				MouseDownColor = theme.ToolbarButtonDown,
			};
			cancelConnectButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				printer.CancelPrint();
				cancelConnectButton.Enabled = false;
			});
			this.AddChild(cancelConnectButton);

			disconnectButton = new TextIconButton(
				"Disconnect".Localize(),
				AggContext.StaticData.LoadIcon("connect.png", 14, 14, theme.InvertIcons),
				theme)
			{
				Name = "Disconnect from printer button",
				Visible = false,
				ToolTipText = "Disconnect from current printer".Localize(),
				BackgroundColor = theme.ToolbarButtonBackground,
				HoverColor = theme.ToolbarButtonHover,
				MouseDownColor = theme.ToolbarButtonDown,
			};
			disconnectButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				if (printer.Connection.Printing)
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
						"WARNING: Disconnecting will stop the current print.\n\nAre you sure you want to disconnect?".Localize(),
						"Disconnect and stop the current print?".Localize(),
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

			// Register listeners
			printer.Connection.CommunicationStateChanged += Connection_CommunicationStateChanged;

			this.SetVisibleStates();
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Connection.CommunicationStateChanged -= Connection_CommunicationStateChanged;

			base.OnClosed(e);
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
					SetChildVisible(disconnectButton, true);
					break;
			}
		}

		private void Connection_CommunicationStateChanged(object s, EventArgs e)
		{
			this.SetVisibleStates();
		}
	}
}