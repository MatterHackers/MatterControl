﻿using MatterHackers.Agg;
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
using System.IO;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class CalibrationSettingsWidget : SettingsViewBase
	{
		private DisableableWidget printLevelingContainer;

		private event EventHandler unregisterEvents;

		public CalibrationSettingsWidget()
			: base("Calibration".Localize())
		{
			printLevelingContainer = new DisableableWidget();
			if (!ActiveSliceSettings.Instance.HasHardwareLeveling())
			{
				printLevelingContainer.AddChild(GetAutoLevelControl());

				mainContainer.AddChild(printLevelingContainer);
			}

			mainContainer.AddChild(new HorizontalLine(separatorLineColor));

			AddChild(mainContainer);
			AddHandlers();
			SetVisibleControls();
		}

		private EditLevelingSettingsWindow editLevelingSettingsWindow;
		private TextWidget printLevelingStatusLabel;

		private FlowLayoutWidget GetAutoLevelControl()
		{
			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Margin = new BorderDouble(0, 4);

			TextWidget notificationSettingsLabel = new TextWidget("Software Print Leveling");
			notificationSettingsLabel.AutoExpandBoundsToText = true;
			notificationSettingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			notificationSettingsLabel.VAnchor = VAnchor.ParentCenter;

			Button editButton = textImageButtonFactory.GenerateEditButton();
			editButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
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

			Button runPrintLevelingButton = textImageButtonFactory.Generate("Configure".Localize().ToUpper());
			runPrintLevelingButton.Margin = new BorderDouble(left: 6);
			runPrintLevelingButton.VAnchor = VAnchor.ParentCenter;
			runPrintLevelingButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					LevelWizardBase.ShowPrintLevelWizard(LevelWizardBase.RuningState.UserRequestedCalibration);
				});
			};

			Agg.Image.ImageBuffer levelingImage = StaticData.Instance.LoadIcon(Path.Combine("PrintStatusControls", "leveling-24x24.png"));
			if (!ActiveTheme.Instance.IsDarkTheme)
			{
				InvertLightness.DoInvertLightness(levelingImage);
			}

			ImageWidget levelingIcon = new ImageWidget(levelingImage);
			levelingIcon.Margin = new BorderDouble(right: 6);

			CheckBox printLevelingSwitch = ImageButtonFactory.CreateToggleSwitch(ActivePrinterProfile.Instance.DoPrintLeveling);
			printLevelingSwitch.VAnchor = VAnchor.ParentCenter;
			printLevelingSwitch.Margin = new BorderDouble(left: 16);
			printLevelingSwitch.CheckedStateChanged += (sender, e) =>
			{
				ActivePrinterProfile.Instance.DoPrintLeveling = printLevelingSwitch.Checked;
			};

			printLevelingStatusLabel = new TextWidget("");
			printLevelingStatusLabel.AutoExpandBoundsToText = true;
			printLevelingStatusLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printLevelingStatusLabel.VAnchor = VAnchor.ParentCenter;

			ActivePrinterProfile.Instance.DoPrintLevelingChanged.RegisterEvent((sender, e) =>
			{
				SetPrintLevelButtonVisiblity();
				printLevelingSwitch.Checked = ActivePrinterProfile.Instance.DoPrintLeveling;
			}, ref unregisterEvents);

			buttonRow.AddChild(levelingIcon);
			buttonRow.AddChild(printLevelingStatusLabel);
			buttonRow.AddChild(editButton);
			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(runPrintLevelingButton);

			// only show the switch if leveling can be turned off (it can't if it is required).
			if (!ActiveSliceSettings.Instance.LevelingRequiredToPrint)
			{
				buttonRow.AddChild(printLevelingSwitch);
			}

			SetPrintLevelButtonVisiblity();
			return buttonRow;
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		private FlowLayoutWidget GetCameraControl()
		{
			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Margin = new BorderDouble(0, 4);

			Agg.Image.ImageBuffer cameraIconImage = StaticData.Instance.LoadIcon(Path.Combine("PrintStatusControls", "camera-24x24.png"));
			if (!ActiveTheme.Instance.IsDarkTheme)
			{
				InvertLightness.DoInvertLightness(cameraIconImage);
			}

			ImageWidget cameraIcon = new ImageWidget(cameraIconImage);
			cameraIcon.Margin = new BorderDouble(right: 6);

			TextWidget cameraLabel = new TextWidget("Camera Monitoring");
			cameraLabel.AutoExpandBoundsToText = true;
			cameraLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			cameraLabel.VAnchor = VAnchor.ParentCenter;

#if __ANDROID__

			GuiWidget publishImageSwitchContainer = new FlowLayoutWidget();
			publishImageSwitchContainer.VAnchor = VAnchor.ParentCenter;
			publishImageSwitchContainer.Margin = new BorderDouble(left: 16);

			CheckBox toggleSwitch = ImageButtonFactory.CreateToggleSwitch(PrinterSettings.Instance.get("PublishBedImage") == "true");
			toggleSwitch.CheckedStateChanged += (sender, e) =>
			{
				CheckBox thisControl = sender as CheckBox;
				PrinterSettings.Instance.set("PublishBedImage", thisControl.Checked ? "true" : "false");
			};
			publishImageSwitchContainer.AddChild(toggleSwitch);

			publishImageSwitchContainer.SetBoundsToEncloseChildren();

			buttonRow.AddChild(publishImageSwitchContainer);
#endif

			return buttonRow;
		}

		private static EePromMarlinWindow openEePromMarlinWidget = null;
		private static EePromRepetierWindow openEePromRepetierWidget = null;
		private string noEepromMappingMessage = "Oops! There is no eeprom mapping for your printer's firmware.".Localize() + "\n\n" + "You may need to wait a minute for your printer to finish initializing.".Localize();
		private string noEepromMappingTitle = "Warning - No EEProm Mapping".Localize();

		private FlowLayoutWidget GetEEPromControl()
		{
			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Margin = new BorderDouble(0, 4);

			TextWidget notificationSettingsLabel = new TextWidget("EEProm Settings".Localize());
			notificationSettingsLabel.AutoExpandBoundsToText = true;
			notificationSettingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			notificationSettingsLabel.VAnchor = VAnchor.ParentCenter;

			Agg.Image.ImageBuffer eePromImage = StaticData.Instance.LoadIcon(Path.Combine("PrintStatusControls", "leveling-24x24.png"));
			if (!ActiveTheme.Instance.IsDarkTheme)
			{
				InvertLightness.DoInvertLightness(eePromImage);
			}
			ImageWidget eePromIcon = new ImageWidget(eePromImage);
			eePromIcon.Margin = new BorderDouble(right: 6);

			Button configureEePromButton = textImageButtonFactory.Generate("Configure".Localize().ToUpper());
			configureEePromButton.Click += new EventHandler(configureEePromButton_Click);

			//buttonRow.AddChild(eePromIcon);
			buttonRow.AddChild(notificationSettingsLabel);
			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(configureEePromButton);

			return buttonRow;
		}

		private void AddHandlers()
		{
			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
		}

		private void openCameraPreview_Click(object sender, EventArgs e)
		{
			MatterControlApplication.Instance.OpenCameraPreview();
		}

		private void configureEePromButton_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(()=>
			{
#if false // This is to force the creation of the repetier window for testing when we don't have repetier firmware.
                        new MatterHackers.MatterControl.EeProm.EePromRepetierWidget();
#else
				switch (PrinterConnectionAndCommunication.Instance.FirmwareType)
				{
					case PrinterConnectionAndCommunication.FirmwareTypes.Repetier:
						if (openEePromRepetierWidget != null)
						{
							openEePromRepetierWidget.BringToFront();
						}
						else
						{
							openEePromRepetierWidget = new EePromRepetierWindow();
							openEePromRepetierWidget.Closed += (RepetierWidget, RepetierEvent) =>
							{
								openEePromRepetierWidget = null;
							};
						}
						break;

					case PrinterConnectionAndCommunication.FirmwareTypes.Marlin:
						if (openEePromMarlinWidget != null)
						{
							openEePromMarlinWidget.BringToFront();
						}
						else
						{
							openEePromMarlinWidget = new EePromMarlinWindow();
							openEePromMarlinWidget.Closed += (marlinWidget, marlinEvent) =>
							{
								openEePromMarlinWidget = null;
							};
						}
						break;

					default:
						StyledMessageBox.ShowMessageBox(null, noEepromMappingMessage, noEepromMappingTitle, StyledMessageBox.MessageType.OK);
						break;
				}
#endif
			});
		}

		private void openGcodeTerminalButton_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(TerminalWindow.Show);
		}

		private void onPrinterStatusChanged(object sender, EventArgs e)
		{
			SetVisibleControls();
			this.Invalidate();
		}

		private void SetPrintLevelButtonVisiblity()
		{
			if (ActivePrinterProfile.Instance.DoPrintLeveling)
			{
				printLevelingStatusLabel.Text = "Software Print Leveling (enabled)".Localize();
			}
			else
			{
				printLevelingStatusLabel.Text = "Software Print Leveling (disabled)".Localize();
			}
		}

		private void SetVisibleControls()
		{

			var currentStatus = PrinterConnectionAndCommunication.Instance.CommunicationState;
			var connected =
					currentStatus == PrinterConnectionAndCommunication.CommunicationStates.Connected ||
					currentStatus == PrinterConnectionAndCommunication.CommunicationStates.FinishedPrint;

			if (ActivePrinterProfile.Instance.ActivePrinter == null || !connected)
			{
				printLevelingContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
			}
			else
			{
				printLevelingContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
			}
		}
	}
}