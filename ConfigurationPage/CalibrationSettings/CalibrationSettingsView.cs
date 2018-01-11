using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class CalibrationSettingsWidget : FlowLayoutWidget
	{
		private EventHandler unregisterEvents;
		private EditLevelingSettingsWindow editLevelingSettingsWindow;
		private Button runPrintLevelingButton;

		private TextImageButtonFactory buttonFactory;
		private PrinterConfig printer;

		public CalibrationSettingsWidget(PrinterConfig printer, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.printer = printer;
			this.buttonFactory = theme.ButtonFactory;

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(3, 0)
			};

			Button editButton = buttonFactory.GenerateIconButton(AggContext.StaticData.LoadIcon("icon_edit.png", 16, 16, IconColor.Theme));
			editButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					if (editLevelingSettingsWindow == null)
					{
						editLevelingSettingsWindow = new EditLevelingSettingsWindow(printer.Settings);
						editLevelingSettingsWindow.Closed += (sender2, e2) =>
						{
							editLevelingSettingsWindow = null;
						};
					}
					else
					{
						editLevelingSettingsWindow.BringToFront();
					}
				});
			};

			this.AddChild(
				new SectionWidget(
					"Calibration".Localize(),
					container,
					theme,
					editButton));

			if (!printer.Settings.GetValue<bool>(SettingsKey.has_hardware_leveling))
			{
				container.AddChild(GetAutoLevelControl());
			}

			printer.Connection.CommunicationStateChanged.RegisterEvent(PrinterStatusChanged, ref unregisterEvents);
			printer.Connection.EnableChanged.RegisterEvent(PrinterStatusChanged, ref unregisterEvents);

			SetVisibleControls();
		}

		private FlowLayoutWidget GetAutoLevelControl()
		{
			var buttonRow = new FlowLayoutWidget()
			{
				Name = "AutoLevelRowItem",
				HAnchor = HAnchor.Stretch,
			};

			buttonRow.AddChild(
				new ImageWidget(AggContext.StaticData.LoadIcon("leveling_32x32.png", 24, 24, IconColor.Theme))
				{
					Margin = new BorderDouble(right: 6),
					VAnchor = VAnchor.Center
				});

			// label
			buttonRow.AddChild(
				new TextWidget("")
				{
					AutoExpandBoundsToText = true,
					TextColor = ActiveTheme.Instance.PrimaryTextColor,
					VAnchor = VAnchor.Center,
					Text = "Software Print Leveling".Localize()
				});

			buttonRow.AddChild(new HorizontalSpacer());

			// configure button
			runPrintLevelingButton = buttonFactory.Generate("Configure".Localize().ToUpper());
			runPrintLevelingButton.Margin = new BorderDouble(left: 6);
			runPrintLevelingButton.VAnchor = VAnchor.Center;
			runPrintLevelingButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(() => LevelWizardBase.ShowPrintLevelWizard(printer, LevelWizardBase.RuningState.UserRequestedCalibration));
			};
			buttonRow.AddChild(runPrintLevelingButton);

			// put in the switch
			CheckBox printLevelingSwitch = ImageButtonFactory.CreateToggleSwitch(printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled));
			printLevelingSwitch.VAnchor = VAnchor.Center;
			printLevelingSwitch.Margin = new BorderDouble(left: 16);
			printLevelingSwitch.CheckedStateChanged += (sender, e) =>
			{
				printer.Settings.Helpers.DoPrintLeveling(printLevelingSwitch.Checked);
			};

			printer.Settings.PrintLevelingEnabledChanged.RegisterEvent((sender, e) =>
			{
				printLevelingSwitch.Checked = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled);
			}, ref unregisterEvents);

			// only show the switch if leveling can be turned off (it can't if it is required).
			if (!printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print))
			{
				buttonRow.AddChild(printLevelingSwitch);
			}

			return buttonRow;
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
				runPrintLevelingButton.Enabled = true; // setting this true when the element is disabled makes the colors stay correct
			}
			else
			{
				this.Enabled = true;
				runPrintLevelingButton.Enabled = printer.Connection.PrinterIsConnected;
			}
		}
	}
}