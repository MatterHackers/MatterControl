/*
Copyright (c) 2014, Kevin Pope
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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class PowerControls : FlowLayoutWidget
	{
		private EventHandler unregisterEvents;
		private PrinterConfig printer;
		private SettingsItem settingsItem;

		private PowerControls(PrinterConfig printer, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.printer = printer;
			this.Enabled = printer.Connection.PrinterIsConnected;

			this.AddChild(
				settingsItem = new SettingsItem(
				"ATX Power Control".Localize(),
				theme.Colors.PrimaryTextColor,
				new SettingsItem.ToggleSwitchConfig()
				{
					Checked = printer.Connection.AtxPowerEnabled,
					ToggleAction = (itemChecked) =>
					{
						if (printer.Connection.AtxPowerEnabled != itemChecked)
						{
							printer.Connection.AtxPowerEnabled = itemChecked;
						}
					}
				},
				enforceGutter: false));

			printer.Connection.CommunicationStateChanged.RegisterEvent((s, e) =>
			{
				this.Enabled = printer.Connection.PrinterIsConnected 
					&& printer.Settings.GetValue<bool>(SettingsKey.has_power_control);
			}, ref unregisterEvents);

			printer.Connection.AtxPowerStateChanged.RegisterEvent((s, e) =>
			{
				if (settingsItem.SettingsControl is ICheckbox toggleSwitch)
				{
					if (toggleSwitch.Checked != printer.Connection.AtxPowerEnabled)
					{
						toggleSwitch.Checked = printer.Connection.AtxPowerEnabled;
					}
				}
			}, ref unregisterEvents);
		}

		public static SectionWidget CreateSection(PrinterConfig printer, ThemeConfig theme)
		{
			if (!printer.Settings.GetValue<bool>(SettingsKey.has_power_control))
			{
				return null;
			}

			return new SectionWidget(
				"Power Control".Localize(),
				new PowerControls(printer, theme),
				theme);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}