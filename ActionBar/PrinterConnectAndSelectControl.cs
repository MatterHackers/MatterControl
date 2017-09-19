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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.SerialPortCommunication.FrostedSerial;

namespace MatterHackers.MatterControl.ActionBar
{
	public class ResetButton : GuiWidget
	{
		private readonly string resetConnectionText = "Reset\nConnection".Localize().ToUpper();
		private EventHandler unregisterEvents;

		public ResetButton(PrinterConfig printer, TextImageButtonFactory buttonFactory)
		{
			this.HAnchor = HAnchor.Stretch | HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			Button resetConnectionButton = buttonFactory.Generate(resetConnectionText, "e_stop4.png");
			resetConnectionButton.Visible = printer.Settings.GetValue<bool>(SettingsKey.show_reset_connection);
			resetConnectionButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(printer.Connection.RebootBoard);
			};
			this.AddChild(resetConnectionButton);

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				var stringEvent = e as StringEventArgs;
				if (stringEvent?.Data == SettingsKey.show_reset_connection)
				{
					resetConnectionButton.Visible = printer.Settings.GetValue<bool>(SettingsKey.show_reset_connection);
				}
			}, ref unregisterEvents);
		}
	}

	public class SimpleButton : FlowLayoutWidget
	{
		public ImageBuffer Image { get; set; }
		public BorderDouble ImageMargin { get; set; }
		public BorderDouble ImagePadding { get; set; }
		public double FontSize { get; set; } = 12;

		public int BorderWidth { get; set; }

		public RGBA_Bytes BorderColor { get; set; } = RGBA_Bytes.Transparent;

		private int borderRadius = 0;

		public SimpleButton(string text, ImageBuffer image = null)
		{
			this.HAnchor = HAnchor.Left | HAnchor.Fit;
			this.VAnchor = VAnchor.Top | VAnchor.Fit;

			this.Text = text;
			this.Image = image;

			this.AddChild(new GuiWidget() { BackgroundColor = RGBA_Bytes.Green, Height = 10, Width = 20 });

			UiThread.RunOnIdle(() =>
			{
				if (this.Image != null)
				{
					this.AddChild(
						new ImageWidget(this.Image)
						{
							Margin = ImageMargin,
							Padding = ImagePadding,
							VAnchor = VAnchor.Stretch,
						});
				}

				if (!string.IsNullOrEmpty(this.Text))
				{
					this.AddChild(new TextWidget(this.Text, pointSize: this.FontSize));
				}

				this.AddChild(new GuiWidget() { BackgroundColor = RGBA_Bytes.Red, Height = 10, Width = 20 });
			});
		}

		public override void OnLayout(LayoutEventArgs layoutEventArgs)
		{
			base.OnLayout(layoutEventArgs);
		}

		/*
		public override void OnLoad(EventArgs args)
		{
			if (this.Image != null)
			{
				this.AddChild(new ImageWidget(this.Image)
				{
					Margin = ImageMargin
				});
			}

			if (!string.IsNullOrEmpty(this.Text))
			{
				this.AddChild(new TextWidget(this.Text, pointSize: this.FontSize));
			}

			base.OnLoad(args);
		}*/

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (this.BorderColor.Alpha0To255 > 0)
			{
				RectangleDouble borderRectangle = LocalBounds;

				if (BorderWidth > 0)
				{
					if (BorderWidth == 1)
					{
						graphics2D.Rectangle(borderRectangle, this.BorderColor);
					}
					else
					{
						graphics2D.Render(
							new Stroke(
								new RoundedRect(borderRectangle, this.borderRadius), 
								BorderWidth), 
							this.BorderColor);
					}
				}
			}

			base.OnDraw(graphics2D);
		}
	}

	public class PrinterConnectButton : GuiWidget
	{
		private readonly string disconnectAndCancelTitle = "Disconnect and stop the current print?".Localize();
		private readonly string disconnectAndCancelMessage = "WARNING: Disconnecting will stop the current print.\n\nAre you sure you want to disconnect?".Localize();

		private GuiWidget connectButton;
		private Button disconnectButton;

		private EventHandler unregisterEvents;
		private PrinterConfig printer;

		public PrinterConnectButton(PrinterConfig printer, TextImageButtonFactory buttonFactory, BorderDouble margin)
		{
			this.printer = printer;
			this.HAnchor = HAnchor.Left | HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;
			this.Margin = 0;
			this.Padding = 0;

			connectButton = buttonFactory.Generate("Connect".Localize().ToUpper(), AggContext.StaticData.LoadIcon("connect.png", 14, 14));
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

			disconnectButton = buttonFactory.Generate("Disconnect".Localize().ToUpper(), AggContext.StaticData.LoadIcon("connect.png", 14, 14));
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
				child.Margin = margin;
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
			WizardWindow.Show<SetupWizardTroubleshooting>();
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