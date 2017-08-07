using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.EeProm;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;
using System.Diagnostics;
using System.IO;
using MatterHackers.MatterControl.PrinterControls;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class CalibrationSettingsWidget : ControlWidgetBase
	{
		private EventHandler unregisterEvents;
		private EditLevelingSettingsWindow editLevelingSettingsWindow;
		private TextWidget printLevelingStatusLabel;
		private Button runPrintLevelingButton;

		private TextImageButtonFactory buttonFactory;

		public CalibrationSettingsWidget(TextImageButtonFactory buttonFactory, int headingPointSize)
		{
			var mainContainer = new AltGroupBox(new TextWidget("Calibration".Localize(), pointSize: headingPointSize, textColor: ActiveTheme.Instance.SecondaryAccentColor))
			{
				Margin = new BorderDouble(0),
				BorderColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};
			this.AddChild(mainContainer);

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Padding = new BorderDouble(3, 0)
			};
			mainContainer.AddChild(container);

			this.buttonFactory = buttonFactory;

			if (!ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_hardware_leveling))
			{
				container.AddChild(GetAutoLevelControl());
			}

			container.AddChild(CreateSeparatorLine());

			PrinterConnection.Instance.CommunicationStateChanged.RegisterEvent(PrinterStatusChanged, ref unregisterEvents);
			PrinterConnection.Instance.EnableChanged.RegisterEvent(PrinterStatusChanged, ref unregisterEvents);

			SetVisibleControls();
		}

		private FlowLayoutWidget GetAutoLevelControl()
		{
			var buttonRow = new FlowLayoutWidget()
			{
				Name = "AutoLevelRowItem",
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(0, 8, 0, 4)
			};

			ImageBuffer levelingImage = StaticData.Instance.LoadIcon("leveling_32x32.png", 24, 24).InvertLightness();
			if (!ActiveTheme.Instance.IsDarkTheme)
			{
				levelingImage.InvertLightness();
			}

			ImageWidget levelingIcon = new ImageWidget(levelingImage);
			levelingIcon.Margin = new BorderDouble(right: 6);

			buttonRow.AddChild(levelingIcon);

			// label
			printLevelingStatusLabel = new TextWidget("")
			{
				AutoExpandBoundsToText = true,
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				VAnchor = VAnchor.Center,
				Text = "Software Print Leveling".Localize()
			};
			buttonRow.AddChild(printLevelingStatusLabel);

			// edit button
			Button editButton = TextImageButtonFactory.GetThemedEditButton();
			editButton.Margin = new BorderDouble(2, 2, 2, 0);
			editButton.VAnchor = VAnchor.Top;
			editButton.VAnchor = VAnchor.Center;
			editButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					if (editLevelingSettingsWindow == null)
					{
						editLevelingSettingsWindow = new EditLevelingSettingsWindow();
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

			buttonRow.AddChild(editButton);

			buttonRow.AddChild(new HorizontalSpacer());

			// configure button
			runPrintLevelingButton = buttonFactory.Generate("Configure".Localize().ToUpper());
			runPrintLevelingButton.Margin = new BorderDouble(left: 6);
			runPrintLevelingButton.VAnchor = VAnchor.Center;
			runPrintLevelingButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(() => LevelWizardBase.ShowPrintLevelWizard(LevelWizardBase.RuningState.UserRequestedCalibration));
			};
			buttonRow.AddChild(runPrintLevelingButton);

			// put in the switch
			CheckBox printLevelingSwitch = ImageButtonFactory.CreateToggleSwitch(ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled));
			printLevelingSwitch.VAnchor = VAnchor.Center;
			printLevelingSwitch.Margin = new BorderDouble(left: 16);
			printLevelingSwitch.CheckedStateChanged += (sender, e) =>
			{
				ActiveSliceSettings.Instance.Helpers.DoPrintLeveling(printLevelingSwitch.Checked);
			};

			PrinterSettings.PrintLevelingEnabledChanged.RegisterEvent((sender, e) =>
			{
				printLevelingSwitch.Checked = ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled);
			}, ref unregisterEvents);

			// only show the switch if leveling can be turned off (it can't if it is required).
			if (!ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_required_to_print))
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
			if (!ActiveSliceSettings.Instance.PrinterSelected
				|| PrinterConnection.Instance.CommunicationState == CommunicationStates.Printing
				|| PrinterConnection.Instance.PrinterIsPaused)
			{
				this.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				runPrintLevelingButton.Enabled = true; // setting this true when the element is disabled makes the colors stay correct
			}
			else
			{
				this.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
				runPrintLevelingButton.Enabled = PrinterConnection.Instance.PrinterIsConnected;
			}
		}
	}
}