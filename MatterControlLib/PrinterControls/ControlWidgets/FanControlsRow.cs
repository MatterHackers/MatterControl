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
using System.Diagnostics;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class FanControlsRow : SettingsRow
	{
		private MHNumberEdit fanSpeedDisplay;

		private RoundedToggleSwitch toggleSwitch;
		private PrinterConfig printer;

		public FanControlsRow(PrinterConfig printer, ThemeConfig theme)
			: base ("Part Cooling Fan".Localize(), null, theme)
		{
			this.printer = printer;

			var timeSinceLastManualSend = new Stopwatch();

			var container = new FlowLayoutWidget();
			this.AddChild(container);
			this.BorderColor = Color.Transparent;

			fanSpeedDisplay = new MHNumberEdit(0, theme, minValue: 0, maxValue: 100, pixelWidth: 30)
			{
				Value = printer.Connection.FanSpeed0To255 * 100 / 255,
				VAnchor = VAnchor.Center | VAnchor.Fit,
				Margin = new BorderDouble(right: 2),
			};
			fanSpeedDisplay.ActuallNumberEdit.EditComplete += (sender, e) =>
			{
				// limit the rate we can send this message to 2 per second so we don't get in a crazy toggle state.
				if (!timeSinceLastManualSend.IsRunning
					|| timeSinceLastManualSend.ElapsedMilliseconds > 500)
				{
					timeSinceLastManualSend.Restart();
					printer.Connection.FanSpeed0To255 = (int)(fanSpeedDisplay.Value * 255 / 100 + .5);
				}
			};
			container.AddChild(fanSpeedDisplay);

			container.Selectable = true;

			// put in %
			container.AddChild(new TextWidget("%", pointSize: 10, textColor: theme.TextColor)
			{
				VAnchor = VAnchor.Center
			});

			toggleSwitch = new RoundedToggleSwitch(theme)
			{
				Margin = new BorderDouble(5, 0),
				VAnchor = VAnchor.Center
			};
			toggleSwitch.CheckedStateChanged += (s, e) =>
			{
				if (!timeSinceLastManualSend.IsRunning
					|| timeSinceLastManualSend.ElapsedMilliseconds > 500)
				{
					timeSinceLastManualSend.Restart();
					if (toggleSwitch.Checked)
					{
						printer.Connection.FanSpeed0To255 = 255;
					}
					else
					{
						printer.Connection.FanSpeed0To255 = 0;
					}
				}
			};
			container.AddChild(toggleSwitch);
			this.ActionWidget = toggleSwitch;

			// Register listeners
			printer.Connection.FanSpeedSet += Connection_FanSpeedSet;
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Connection.FanSpeedSet -= Connection_FanSpeedSet;

			base.OnClosed(e);
		}

		private void Connection_FanSpeedSet(object s, EventArgs e)
		{
			if ((int)printer.Connection.FanSpeed0To255 > 0)
			{
				toggleSwitch.Checked = true;
			}
			else
			{
				toggleSwitch.Checked = false;
			}

			fanSpeedDisplay.Value = printer.Connection.FanSpeed0To255 * 100 / 255;
		}
	}
}