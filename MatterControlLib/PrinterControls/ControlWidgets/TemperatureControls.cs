/*
Copyright (c) 2017, Kevin Pope, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ActionBar;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class TemperatureControls : FlowLayoutWidget
	{
		private PrinterConfig printer;
		private TextButton preHeatButton;
		private TextButton offButton;

		private TemperatureControls(PrinterConfig printer, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.HAnchor = HAnchor.Stretch;
			this.printer = printer;

			int hotendCount = printer.Settings.Helpers.HotendCount();

			// add in the hotend controls
			for (int extruderIndex = 0; extruderIndex < hotendCount; extruderIndex++)
			{
				var settingsRow = new SettingsRow(
							hotendCount == 1 ? "Hotend".Localize() : "Hotend {0}".Localize().FormatWith(extruderIndex + 1),
							null,
							theme);

				settingsRow.AddChild(new TemperatureWidgetHotend(printer, extruderIndex, theme, hotendCount));

				this.AddChild(settingsRow);
			}

			if (printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed))
			{
				var settingsRow = new SettingsRow(
							"Bed".Localize(),
							null,
							theme);

				settingsRow.AddChild(new TemperatureWidgetBed(printer, theme));

				this.AddChild(settingsRow);
			}

			// add in the all heaters section
			var heatersRow = new SettingsRow(
				"All Heaters".Localize(),
				null,
				theme);

			this.AddChild(heatersRow);
			var container = new FlowLayoutWidget();
			heatersRow.AddChild(container);

			preHeatButton = new TextButton("Preheat".Localize(), theme)
			{
				BackgroundColor = theme.MinimalShade,
				Margin = new BorderDouble(right: 10)
			};
			container.AddChild(preHeatButton);
			preHeatButton.Click += (s, e) =>
			{
				// turn on the bed
				printer.Connection.TargetBedTemperature = printer.Settings.GetValue<double>(SettingsKey.bed_temperature);
				for (int extruderIndex = 0; extruderIndex < hotendCount; extruderIndex++)
				{
					printer.Connection.SetTargetHotendTemperature(extruderIndex, printer.Settings.Helpers.ExtruderTargetTemperature(extruderIndex));
				}
				printer.Connection.TurnOffBedAndExtruders(TurnOff.AfterDelay);
			};

			offButton = new TextButton("Off".Localize(), theme)
			{
				BackgroundColor = theme.MinimalShade,
			};
			container.AddChild(offButton);
			offButton.Click += (s, e) =>
			{
				printer.Connection.TurnOffBedAndExtruders(TurnOff.Now);
			};

			this.AddChild(new FanControlsRow(printer, theme));

			// Register listeners
			printer.Connection.CommunicationStateChanged += Printer_StatusChanged;
			SetVisibleControls();
		}

		public static SectionWidget CreateSection(PrinterConfig printer, ThemeConfig theme)
		{
			return new SectionWidget("Temperature".Localize(), new TemperatureControls(printer, theme), theme);
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Connection.CommunicationStateChanged -= Printer_StatusChanged;

			base.OnClosed(e);
		}

		private void Printer_StatusChanged(object sender, EventArgs e)
		{
			SetVisibleControls();
			UiThread.RunOnIdle(this.Invalidate);
		}

		private void SetVisibleControls()
		{
			if (!printer.Settings.PrinterSelected)
			{
				offButton?.SetEnabled(false);
				preHeatButton?.SetEnabled(false);
			}
			else // we at least have a printer selected
			{
				switch (printer.Connection.CommunicationState)
				{
					case CommunicationStates.FinishedPrint:
					case CommunicationStates.Connected:
						offButton?.SetEnabled(true);
						preHeatButton?.SetEnabled(true);
						break;

					default:
						offButton?.SetEnabled(false);
						preHeatButton?.SetEnabled(false);
						break;
				}
			}
		}
	}
}