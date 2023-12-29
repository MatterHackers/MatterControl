﻿/*
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
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.SerialPortCommunication.FrostedSerial;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class ComPortField : UIField
	{
		private DropDownList dropdownList;
		private ThemeConfig theme;
		private PrinterConfig printer;

		public ComPortField(PrinterConfig printer, ThemeConfig theme)
		{
			this.theme = theme;
			this.printer = printer;
		}

		public new string Name { get; set; }
		public static bool ShowPortWizardButton { get; set; } = true;

		public override void Initialize(ref int tabIndex)
		{
			EventHandler unregisterEvents = null;

			bool canChangeComPort = !printer.Connection.IsConnected && printer.Connection.CommunicationState != CommunicationStates.AttemptingToConnect;

			var panel = new FlowLayoutWidget();

			// The COM_PORT control is unique in its approach to the SlicerConfigName. It uses "com_port" settings name to
			// bind to a context that will place it in the SliceSetting view but it binds its values to a machine
			// specific dictionary key that is not exposed in the UI. At runtime we lookup and store to '<machinename>_com_port'
			// ensuring that a single printer can be shared across different devices and we'll select the correct com port in each case
			dropdownList = new MHDropDownList("None".Localize(), theme, maxHeight: 200 * GuiWidget.DeviceScale)
			{
				ToolTipText = this.HelpText,
				Margin = new BorderDouble(),
				Name = "com_port Field",
				// Prevent droplist interaction when connected
				Enabled = canChangeComPort,
                TabIndex = tabIndex++,
            };

			dropdownList.Click += (s, e) =>
			{
				// TODO: why doesn't this blow up without runonidle?
				RebuildMenuItems();
			};

			RebuildMenuItems();

			// Prevent droplist interaction when connected
			void CommunicationStateChanged(object s, EventArgs e)
			{
				canChangeComPort = !printer.Connection.IsConnected && printer.Connection.CommunicationState != CommunicationStates.AttemptingToConnect;
				dropdownList.Enabled = canChangeComPort;

				if (printer.Connection.ComPort != dropdownList.SelectedLabel)
				{
					dropdownList.SelectedLabel = printer.Connection.ComPort;
				}
			}
			printer.Connection.CommunicationStateChanged += CommunicationStateChanged;
			dropdownList.Closed += (s, e) => printer.Connection.CommunicationStateChanged -= CommunicationStateChanged;

			// Release event listener on close
			dropdownList.Closed += (s, e) =>
			{
				unregisterEvents?.Invoke(null, null);
			};

			if (ShowPortWizardButton)
			{
				var configureIcon = new ThemedIconButton(StaticData.Instance.LoadIcon("fa-cog_16.png", 16, 16).GrayToColor(theme.TextColor), theme)
				{
					VAnchor = VAnchor.Center,
					Margin = theme.ButtonSpacing,
					ToolTipText = "Port Wizard".Localize()
				};
				configureIcon.Click += (s, e) =>
				{
                    throw new NotImplementedException();
                };
			
				panel.AddChild(configureIcon);
			}

			panel.AddChild(dropdownList);

			this.Content = panel;
		}

		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			dropdownList.SelectedLabel = this.Value;
			base.OnValueChanged(fieldChangedEventArgs);
		}

		private void RebuildMenuItems()
		{
			dropdownList.MenuItems.Clear();

			foreach (string listItem in FrostedSerialPort.GetPortNames())
			{
				// Add each serial port to the dropdown list
				MenuItem newItem = dropdownList.AddItem(listItem);

				// When the given menu item is selected, save its value back into settings
				newItem.Selected += (sender, e) =>
				{
					if (sender is MenuItem menuItem)
					{
						this.SetValue(
							menuItem.Text,
							userInitiated: true);
					}
				};
			}

			// Set control text
			dropdownList.SelectedLabel = this.Value;
		}
	}
}
