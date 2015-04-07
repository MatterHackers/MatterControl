using System;
using System.Collections.Generic;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.EeProm;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage
{
    public class HardwareSettingsWidget : SettingsViewBase
    {
		Button openGcodeTerminalButton;
		Button openCameraButton;

        DisableableWidget eePromControlsContainer;
        DisableableWidget terminalCommunicationsContainer;
        DisableableWidget printLevelingContainer;

        event EventHandler unregisterEvents;

        public HardwareSettingsWidget()
			: base(LocalizedString.Get("Hardware Settings"))
        {

            eePromControlsContainer = new DisableableWidget();
            eePromControlsContainer.AddChild(GetEEPromControl());
            terminalCommunicationsContainer = new DisableableWidget();
			terminalCommunicationsContainer.AddChild(GetGcodeTerminalControl());

			printLevelingContainer = new DisableableWidget();
			if (!ActiveSliceSettings.Instance.HasHardwareLeveling())
			{
				printLevelingContainer.AddChild(GetAutoLevelControl());

				mainContainer.AddChild(printLevelingContainer);
			}

            mainContainer.AddChild(new HorizontalLine(separatorLineColor));
            mainContainer.AddChild(eePromControlsContainer);
			mainContainer.AddChild(new HorizontalLine(separatorLineColor));

			mainContainer.AddChild(terminalCommunicationsContainer);

			DisableableWidget cameraContainer = new DisableableWidget();
			cameraContainer.AddChild(GetCameraControl());

			if (ApplicationSettings.Instance.get("HardwareHasCamera") == "true")
			{
				mainContainer.AddChild(new HorizontalLine(separatorLineColor));
				mainContainer.AddChild(cameraContainer);
			}

            AddChild(mainContainer);
            AddHandlers();
            SetVisibleControls();
        }

        EditLevelingSettingsWindow editLevelingSettingsWindow;
        TextWidget printLevelingStatusLabel;
        private FlowLayoutWidget GetAutoLevelControl()
        {
            FlowLayoutWidget buttonRow = new FlowLayoutWidget();
            buttonRow.HAnchor = HAnchor.ParentLeftRight;
            buttonRow.Margin = new BorderDouble(0,4);

            Button configureAutoLevelButton = textImageButtonFactory.Generate("Configure".Localize().ToUpper());
            configureAutoLevelButton.Margin = new BorderDouble(left: 6);
            configureAutoLevelButton.VAnchor = VAnchor.ParentCenter;
			configureAutoLevelButton.Click += new EventHandler(configureAutoLevelButton_Click);

            TextWidget notificationSettingsLabel = new TextWidget("Automatic Print Leveling");
            notificationSettingsLabel.AutoExpandBoundsToText = true;
            notificationSettingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            notificationSettingsLabel.VAnchor = VAnchor.ParentCenter;

            Button editButton = textImageButtonFactory.GenerateEditButton();
            editButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
            editButton.Click += (sender, e) =>
            {
                UiThread.RunOnIdle((state) =>
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
            runPrintLevelingButton.Margin = new BorderDouble(left:6);
            runPrintLevelingButton.VAnchor = VAnchor.ParentCenter;
            runPrintLevelingButton.Click += (sender, e) =>
            {
                UiThread.RunOnIdle((state) =>
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
			levelingIcon.Margin = new BorderDouble (right: 6);

			GuiWidget levelingSwitchContainer = new FlowLayoutWidget();
			levelingSwitchContainer.VAnchor = VAnchor.ParentCenter;
			levelingSwitchContainer.Margin = new BorderDouble(left: 16);

			ToggleSwitch printLevelingSwitch = GenerateToggleSwitch(levelingSwitchContainer, PrinterSettings.Instance.get("PublishBedImage") == "true");
			printLevelingSwitch.SwitchState = ActivePrinterProfile.Instance.DoPrintLeveling;
			printLevelingSwitch.SwitchStateChanged += (sender, e) => 
			{
				ActivePrinterProfile.Instance.DoPrintLeveling = printLevelingSwitch.SwitchState;
			};
			levelingSwitchContainer.SetBoundsToEncloseChildren();

			printLevelingStatusLabel = new TextWidget ("");
			printLevelingStatusLabel.AutoExpandBoundsToText = true;
			printLevelingStatusLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printLevelingStatusLabel.VAnchor = VAnchor.ParentCenter;

			GuiWidget hSpacer = new GuiWidget ();
			hSpacer.HAnchor = HAnchor.ParentLeftRight;

            ActivePrinterProfile.Instance.DoPrintLevelingChanged.RegisterEvent((sender, e) =>
            {
                SetPrintLevelButtonVisiblity();

            }, ref unregisterEvents);

            buttonRow.AddChild(levelingIcon);
            buttonRow.AddChild(printLevelingStatusLabel);
            buttonRow.AddChild(editButton);
            buttonRow.AddChild(new HorizontalSpacer());
            buttonRow.AddChild(runPrintLevelingButton);
			buttonRow.AddChild(levelingSwitchContainer);

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
			buttonRow.Margin = new BorderDouble(0,4);

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

			openCameraButton = textImageButtonFactory.Generate("Preview".Localize().ToUpper());
			openCameraButton.Click += new EventHandler(openCameraPreview_Click);
			openCameraButton.Margin = new BorderDouble(left:6);

			buttonRow.AddChild(cameraIcon);
			buttonRow.AddChild(cameraLabel);
			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(openCameraButton);
#if __ANDROID__ 

			GuiWidget publishImageSwitchContainer = new FlowLayoutWidget();
			publishImageSwitchContainer.VAnchor = VAnchor.ParentCenter;
			publishImageSwitchContainer.Margin = new BorderDouble(left: 16);

			ToggleSwitch toggleSwitch = GenerateToggleSwitch(publishImageSwitchContainer, PrinterSettings.Instance.get("PublishBedImage") == "true");
			toggleSwitch.SwitchStateChanged += (sender, e) => 
			{
				ToggleSwitch thisControl = sender as ToggleSwitch;
				PrinterSettings.Instance.set("PublishBedImage", thisControl.SwitchState ? "true" : "false");
			};

			publishImageSwitchContainer.SetBoundsToEncloseChildren();

			buttonRow.AddChild(publishImageSwitchContainer);
#endif

			return buttonRow;
		}

        private FlowLayoutWidget GetGcodeTerminalControl()
		{
			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Margin = new BorderDouble(0,4);

            Agg.Image.ImageBuffer terminalSettingsImage = StaticData.Instance.LoadIcon(Path.Combine("PrintStatusControls", "terminal-24x24.png"));
            if (!ActiveTheme.Instance.IsDarkTheme)
            {
                InvertLightness.DoInvertLightness(terminalSettingsImage);
            }

            ImageWidget terminalIcon = new ImageWidget(terminalSettingsImage);
            terminalIcon.Margin = new BorderDouble(right: 6, bottom: 6);

			TextWidget gcodeTerminalLabel = new TextWidget("Gcode Terminal");
			gcodeTerminalLabel.AutoExpandBoundsToText = true;
			gcodeTerminalLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			gcodeTerminalLabel.VAnchor = VAnchor.ParentCenter;

			openGcodeTerminalButton = textImageButtonFactory.Generate("Show Terminal".Localize().ToUpper());
			openGcodeTerminalButton.Click += new EventHandler(openGcodeTerminalButton_Click);

            buttonRow.AddChild(terminalIcon);
            buttonRow.AddChild(gcodeTerminalLabel);
			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(openGcodeTerminalButton);

			return buttonRow;
		}

        static EePromMarlinWindow openEePromMarlinWidget = null;
        static EePromRepetierWindow openEePromRepetierWidget = null;
        string noEepromMappingMessage = "Oops! There is no eeprom mapping for your printer's firmware.".Localize() + "\n\n" + "You may need to wait a minute for your printer to finish initializing.".Localize();
        string noEepromMappingTitle = "Warning - No EEProm Mapping".Localize();
        private FlowLayoutWidget GetEEPromControl()
        {
            FlowLayoutWidget buttonRow = new FlowLayoutWidget();
            buttonRow.HAnchor = HAnchor.ParentLeftRight;
            buttonRow.Margin = new BorderDouble(0,4);

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

		void openCameraPreview_Click(object sender, EventArgs e)
		{
			MatterControlApplication.Instance.OpenCameraPreview();
		}

        void configureEePromButton_Click(object sender, EventArgs mouseEvent)
        {
            UiThread.RunOnIdle((state) =>
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

        void configureAutoLevelButton_Click(object sender, EventArgs mouseEvent)
        {
            UiThread.RunOnIdle((state) =>
            {
                //Do stuff
            });
        }

		void openGcodeTerminalButton_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle((state) =>
			{
				TerminalWindow.Show();
			});
		}

        private void onPrinterStatusChanged(object sender, EventArgs e)
        {
            SetVisibleControls();
            this.Invalidate();
        }

        void SetPrintLevelButtonVisiblity()
        {
            if (ActivePrinterProfile.Instance.DoPrintLeveling)
            {
                printLevelingStatusLabel.Text = LocalizedString.Get("Automatic Print Leveling (enabled)");
            }
            else
            {
                printLevelingStatusLabel.Text = LocalizedString.Get("Automatic Print Leveling (disabled)");
            }
        }

        private void SetVisibleControls()
        {
            if (ActivePrinterProfile.Instance.ActivePrinter == null)
            {
                // no printer selected                         
                eePromControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                terminalCommunicationsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                printLevelingContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                //cloudMonitorContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
            }
            else // we at least have a printer selected
            {
                //cloudMonitorContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                switch (PrinterConnectionAndCommunication.Instance.CommunicationState)
                {
                    case PrinterConnectionAndCommunication.CommunicationStates.Disconnecting:
                    case PrinterConnectionAndCommunication.CommunicationStates.ConnectionLost:
                    case PrinterConnectionAndCommunication.CommunicationStates.Disconnected:
                    case PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect:
                    case PrinterConnectionAndCommunication.CommunicationStates.FailedToConnect:
                        eePromControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        printLevelingContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        terminalCommunicationsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        break;

                    case PrinterConnectionAndCommunication.CommunicationStates.FinishedPrint:
                    case PrinterConnectionAndCommunication.CommunicationStates.Connected:
                        eePromControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        printLevelingContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        terminalCommunicationsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        break;

                    case PrinterConnectionAndCommunication.CommunicationStates.PrintingFromSd:
                        eePromControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        printLevelingContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        terminalCommunicationsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        break;

                    case PrinterConnectionAndCommunication.CommunicationStates.PreparingToPrint:
                    case PrinterConnectionAndCommunication.CommunicationStates.Printing:
                        switch (PrinterConnectionAndCommunication.Instance.PrintingState)
                        {
                            case PrinterConnectionAndCommunication.DetailedPrintingState.HomingAxis:
                            case PrinterConnectionAndCommunication.DetailedPrintingState.HeatingBed:
                            case PrinterConnectionAndCommunication.DetailedPrintingState.HeatingExtruder:
                            case PrinterConnectionAndCommunication.DetailedPrintingState.Printing:
                                eePromControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                                printLevelingContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                                terminalCommunicationsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                                break;

                            default:
                                throw new NotImplementedException();
                        }
                        break;

                    case PrinterConnectionAndCommunication.CommunicationStates.Paused:
                        eePromControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        printLevelingContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        terminalCommunicationsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }
        }
    }
}