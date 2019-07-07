/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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

namespace MatterHackers.MatterControl.PrinterControls
{
	public class CalibrationControls : FlowLayoutWidget
	{
		private readonly PrinterConfig printer;
		private readonly RoundedToggleSwitch printLevelingSwitch;

		private CalibrationControls(PrinterConfig printer, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.printer = printer;

			// add in the controls for configuring auto leveling
			{
				SettingsRow calibrationRow;

				this.AddChild(calibrationRow = new SettingsRow(
					"Printer Calibration".Localize(),
					null,
					theme));

				var runWizardButton = new IconButton(AggContext.StaticData.LoadIcon("compass.png", theme.InvertIcons), theme)
				{
					VAnchor = VAnchor.Center,
					Margin = theme.ButtonSpacing,
					Name = "Printer Calibration Button",
					ToolTipText = "Run Calibration".Localize()
				};

				runWizardButton.Click += (s, e) =>
				{
					UiThread.RunOnIdle(() =>
					{
						DialogWindow.Show(new PrinterCalibrationWizard(printer, theme));
					});
				};
				calibrationRow.AddChild(runWizardButton);
			}

			// Register listeners
			printer.Connection.CommunicationStateChanged += PrinterStatusChanged;

			SetVisibleControls();
		}

		public static SectionWidget CreateSection(PrinterConfig printer, ThemeConfig theme)
		{
			return new SectionWidget(
				"Calibration".Localize(),
				new CalibrationControls(printer, theme),
				theme);
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Connection.CommunicationStateChanged -= PrinterStatusChanged;

			base.OnClosed(e);
		}

		private void PrinterStatusChanged(object sender, EventArgs e)
		{
			this.SetVisibleControls();
			this.Invalidate();
		}

		private void SetVisibleControls()
		{
			if (!printer.Settings.PrinterSelected
				|| printer.Connection.CommunicationState == CommunicationStates.Printing
				|| printer.Connection.Paused)
			{
				this.Enabled = false;
			}
			else
			{
				this.Enabled = true;
			}
		}
	}
}