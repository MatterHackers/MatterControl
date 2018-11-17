/*
Copyright (c) 2018, Lars Brubaker
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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class CalibrationControls : FlowLayoutWidget
	{
		private PrinterConfig printer;
		private RoundedToggleSwitch printLevelingSwitch;

		private CalibrationControls(PrinterConfig printer, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.printer = printer;

			// add in the controls for configuring auto leveling
			{
				SettingsRow settingsRow;

				this.AddChild(settingsRow = new SettingsRow(
					"Bed Leveling".Localize(),
					null,
					theme,
					AggContext.StaticData.LoadIcon("leveling_32x32.png", 16, 16, theme.InvertIcons)));

				// run leveling button
				var runWizardButton = new IconButton(AggContext.StaticData.LoadIcon("fa-cog_16.png", theme.InvertIcons), theme)
				{
					VAnchor = VAnchor.Center,
					Margin = theme.ButtonSpacing,
					Name = "Run Leveling Button",

					ToolTipText = "Run Calibration".Localize()
				};
				runWizardButton.Click += (s, e) =>
				{
					UiThread.RunOnIdle(() =>
					{
						PrintLevelingWizard.Start(printer, theme);
					});
				};
				settingsRow.AddChild(runWizardButton);

				// only show the switch if leveling can be turned off (it can't if it is required).
				if (!printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print))
				{
					// put in the switch
					printLevelingSwitch = new RoundedToggleSwitch(theme)
					{
						VAnchor = VAnchor.Center,
						Margin = new BorderDouble(left: 16),
						Checked = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled)
					};
					printLevelingSwitch.CheckedStateChanged += (sender, e) =>
					{
						printer.Settings.Helpers.DoPrintLeveling(printLevelingSwitch.Checked);
					};

					// TODO: Why is this listener conditional? If the leveling changes somehow, shouldn't we be updated the UI to reflect that?
					// Register listeners
					printer.Settings.PrintLevelingEnabledChanged += Settings_PrintLevelingEnabledChanged;
					
					settingsRow.AddChild(printLevelingSwitch);
				}

				// add in the controls for configuring probe offset
				if (printer.Settings.GetValue<bool>(SettingsKey.has_z_probe)
					&& printer.Settings.GetValue<bool>(SettingsKey.use_z_probe))
				{
					this.AddChild(settingsRow = new SettingsRow(
						"Calibrate Probe Offset".Localize(),
						null,
						theme,
						AggContext.StaticData.LoadIcon("probing_32x32.png", 16, 16, theme.InvertIcons)));

					var runCalibrateProbeButton = new IconButton(AggContext.StaticData.LoadIcon("fa-cog_16.png", theme.InvertIcons), theme)
					{
						VAnchor = VAnchor.Center,
						Margin = theme.ButtonSpacing,
						ToolTipText = "Run Calibration".Localize()
					};
					runCalibrateProbeButton.Click += (s, e) =>
					{
						UiThread.RunOnIdle(() =>
						{
							ProbeCalibrationWizard.Start(printer, theme);
						});
					};

					settingsRow.BorderColor = Color.Transparent;
					settingsRow.AddChild(runCalibrateProbeButton);
				}
			}

			// Register listeners
			printer.Connection.CommunicationStateChanged += PrinterStatusChanged;
			printer.Connection.EnableChanged += PrinterStatusChanged;

			SetVisibleControls();
		}

		public static SectionWidget CreateSection(PrinterConfig printer, ThemeConfig theme)
		{
			var editButton = new IconButton(AggContext.StaticData.LoadIcon("icon_edit.png", 16, 16, theme.InvertIcons), theme);
			editButton.Name = "Edit Leveling Data Button";
			editButton.ToolTipText = "Edit Leveling Data".Localize();
			editButton.Click += (s, e) =>
			{
				DialogWindow.Show(new EditLevelingSettingsPage(printer, theme));
			};

			return new SectionWidget(
				"Calibration".Localize(),
				new CalibrationControls(printer, theme),
				theme,
				editButton);
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Settings.PrintLevelingEnabledChanged -= Settings_PrintLevelingEnabledChanged;
			printer.Connection.CommunicationStateChanged -= PrinterStatusChanged;
			printer.Connection.EnableChanged -= PrinterStatusChanged;

			base.OnClosed(e);
		}

		private void Settings_PrintLevelingEnabledChanged(object sender, EventArgs e)
		{
			printLevelingSwitch.Checked = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled);
		}

		private void PrinterStatusChanged(object sender, EventArgs e)
		{
			SetVisibleControls();
			this.Invalidate();
		}

		private void SetVisibleControls()
		{
			if (!printer.Settings.PrinterSelected
				|| printer.Connection.CommunicationState == CommunicationStates.Printing
				|| printer.Connection.PrinterIsPaused)
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