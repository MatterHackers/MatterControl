using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.Platform;
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
		PrinterConnection printerConnection;

		public CalibrationSettingsWidget(PrinterConnection printerConnection, TextImageButtonFactory buttonFactory, int headingPointSize)
		{
			this.printerConnection = printerConnection;
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

			if (!printerConnection.PrinterSettings.GetValue<bool>(SettingsKey.has_hardware_leveling))
			{
				container.AddChild(GetAutoLevelControl());
			}

			container.AddChild(CreateSeparatorLine());

			printerConnection.CommunicationStateChanged.RegisterEvent(PrinterStatusChanged, ref unregisterEvents);
			printerConnection.EnableChanged.RegisterEvent(PrinterStatusChanged, ref unregisterEvents);

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

			ImageBuffer levelingImage = AggContext.StaticData.LoadIcon("leveling_32x32.png", 24, 24).InvertLightness();
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

			// edit 
			Button editButton = buttonFactory.GenerateIconButton(AggContext.StaticData.LoadIcon("icon_edit.png", 16, 16));
			editButton.Margin = new BorderDouble(2, 2, 2, 0);
			editButton.VAnchor = VAnchor.Top;
			editButton.VAnchor = VAnchor.Center;
			editButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					if (editLevelingSettingsWindow == null)
					{
						editLevelingSettingsWindow = new EditLevelingSettingsWindow(printerConnection.PrinterSettings);
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
				UiThread.RunOnIdle(() => LevelWizardBase.ShowPrintLevelWizard(printerConnection, LevelWizardBase.RuningState.UserRequestedCalibration));
			};
			buttonRow.AddChild(runPrintLevelingButton);

			// put in the switch
			CheckBox printLevelingSwitch = ImageButtonFactory.CreateToggleSwitch(printerConnection.PrinterSettings.GetValue<bool>(SettingsKey.print_leveling_enabled));
			printLevelingSwitch.VAnchor = VAnchor.Center;
			printLevelingSwitch.Margin = new BorderDouble(left: 16);
			printLevelingSwitch.CheckedStateChanged += (sender, e) =>
			{
				printerConnection.PrinterSettings.Helpers.DoPrintLeveling(printLevelingSwitch.Checked);
			};

			printerConnection.PrinterSettings.PrintLevelingEnabledChanged.RegisterEvent((sender, e) =>
			{
				printLevelingSwitch.Checked = printerConnection.PrinterSettings.GetValue<bool>(SettingsKey.print_leveling_enabled);
			}, ref unregisterEvents);

			// only show the switch if leveling can be turned off (it can't if it is required).
			if (!printerConnection.PrinterSettings.GetValue<bool>(SettingsKey.print_leveling_required_to_print))
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
			if (!printerConnection.PrinterSettings.PrinterSelected
				|| printerConnection.CommunicationState == CommunicationStates.Printing
				|| printerConnection.PrinterIsPaused)
			{
				this.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				runPrintLevelingButton.Enabled = true; // setting this true when the element is disabled makes the colors stay correct
			}
			else
			{
				this.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
				runPrintLevelingButton.Enabled = printerConnection.PrinterIsConnected;
			}
		}
	}
}