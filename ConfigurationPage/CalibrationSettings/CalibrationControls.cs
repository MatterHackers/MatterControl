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
		private EventHandler unregisterEvents;

		private TextImageButtonFactory buttonFactory;
		private PrinterConfig printer;

		private CalibrationControls(PrinterConfig printer, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.printer = printer;
			this.buttonFactory = theme.ButtonFactory;

			// add in the controls for configuring auto leveling
			{
				SettingsRow settingsRow;

				this.AddChild(settingsRow = new SettingsRow(
					"Print Leveling Plane".Localize(),
					null,
					theme.Colors.PrimaryTextColor,
					theme,
					AggContext.StaticData.LoadIcon("leveling_32x32.png", 16, 16, IconColor.Theme)));

				// run leveling button
				var runWizardButton = new IconButton(AggContext.StaticData.LoadIcon("fa-cog_16.png", IconColor.Theme), theme)
				{
					VAnchor = VAnchor.Center,
					Margin = theme.ButtonSpacing,
					ToolTipText = "Print Leveling Wizard".Localize()
				};
				runWizardButton.Click += (s, e) =>
				{
					UiThread.RunOnIdle(() =>
					{
						LevelWizardBase.ShowPrintLevelWizard(printer);
					});
				};
				settingsRow.AddChild(runWizardButton);

				// only show the switch if leveling can be turned off (it can't if it is required).
				if (!printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print))
				{
					// put in the switch
					var printLevelingSwitch = new RoundedToggleSwitch(theme)
					{
						VAnchor = VAnchor.Center,
						Margin = new BorderDouble(left: 16),
						Checked = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled)
					};
					printLevelingSwitch.CheckedStateChanged += (sender, e) =>
					{
						printer.Settings.Helpers.DoPrintLeveling(printLevelingSwitch.Checked);
					};

					printer.Settings.PrintLevelingEnabledChanged.RegisterEvent((sender, e) =>
					{
						printLevelingSwitch.Checked = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled);
					}, ref unregisterEvents);

					settingsRow.AddChild(printLevelingSwitch);
				}

				// add in the controls for configuring probe offset
				if (printer.Settings.GetValue<bool>(SettingsKey.has_z_probe)
					&& printer.Settings.GetValue<bool>(SettingsKey.use_z_probe))
				{
					this.AddChild(settingsRow = new SettingsRow(
						"Print Leveling Probe".Localize(),
						null,
						theme.Colors.PrimaryTextColor,
						theme,
						AggContext.StaticData.LoadIcon("probing_32x32.png", 16, 16, IconColor.Theme)));

					var runCalibrateProbeButton = new IconButton(AggContext.StaticData.LoadIcon("fa-cog_16.png", IconColor.Theme), theme)
					{
						VAnchor = VAnchor.Center,
						Margin = theme.ButtonSpacing,
						ToolTipText = "Probe Calibration Wizard".Localize()
					};
					runCalibrateProbeButton.Click += (s, e) =>
					{
						UiThread.RunOnIdle(() =>
						{
							ProbeCalibrationWizard.ShowProbeCalibrationWizard(printer);
						});
					};

					settingsRow.BorderColor = Color.Transparent;
					settingsRow.AddChild(runCalibrateProbeButton);
				}
			}

			printer.Connection.CommunicationStateChanged.RegisterEvent(PrinterStatusChanged, ref unregisterEvents);
			printer.Connection.EnableChanged.RegisterEvent(PrinterStatusChanged, ref unregisterEvents);

			SetVisibleControls();
		}

		public static SectionWidget CreateSection(PrinterConfig printer, ThemeConfig theme)
		{
			var editButton = new IconButton(AggContext.StaticData.LoadIcon("icon_edit.png", 16, 16, IconColor.Theme), theme);
			editButton.Click += (s, e) =>
			{
				DialogWindow.Show(new EditLevelingSettingsPage(printer));
			};

			return new SectionWidget(
				"Calibration".Localize(),
				new CalibrationControls(printer, theme),
				theme,
				editButton);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
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