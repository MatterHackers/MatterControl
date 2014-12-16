﻿using System;
using System.Collections.Generic;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.EeProm;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage
{
    public class HardwareSettingsWidget : SettingsViewBase
    {
        Button configureAutoLevelButton;
        Button configureEePromButton;
		Button openGcodeTerminalButton;

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
            printLevelingContainer.AddChild(GetAutoLevelControl());

            mainContainer.AddChild(printLevelingContainer); 
            mainContainer.AddChild(new HorizontalLine(separatorLineColor));
            mainContainer.AddChild(eePromControlsContainer);
			mainContainer.AddChild(new HorizontalLine(separatorLineColor));

			mainContainer.AddChild(terminalCommunicationsContainer);


            AddChild(mainContainer);

            AddHandlers();
            SetVisibleControls();
        }


        EditLevelingSettingsWindow editLevelingSettingsWindow;
        Button enablePrintLevelingButton;
        Button disablePrintLevelingButton;
        TextWidget printLevelingStatusLabel;
        private FlowLayoutWidget GetAutoLevelControl()
        {
            FlowLayoutWidget buttonRow = new FlowLayoutWidget();
            buttonRow.HAnchor = HAnchor.ParentLeftRight;
            buttonRow.Margin = new BorderDouble(0,4);

            configureAutoLevelButton = textImageButtonFactory.Generate("Configure".Localize().ToUpper());
            configureAutoLevelButton.Margin = new BorderDouble(left: 6);
            configureAutoLevelButton.VAnchor = VAnchor.ParentCenter;

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

            enablePrintLevelingButton = textImageButtonFactory.Generate("Enable".Localize().ToUpper());
			enablePrintLevelingButton.Margin = new BorderDouble(left:6);
			enablePrintLevelingButton.VAnchor = VAnchor.ParentCenter;
			enablePrintLevelingButton.Click += new EventHandler(enablePrintLeveling_Click);

            disablePrintLevelingButton = textImageButtonFactory.Generate("Disable".Localize().ToUpper());
			disablePrintLevelingButton.Margin = new BorderDouble(left:6);
			disablePrintLevelingButton.VAnchor = VAnchor.ParentCenter;
			disablePrintLevelingButton.Click += new EventHandler(disablePrintLeveling_Click);

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
            buttonRow.AddChild(enablePrintLevelingButton);
            buttonRow.AddChild(disablePrintLevelingButton);
            buttonRow.AddChild(runPrintLevelingButton);
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
        
        private FlowLayoutWidget GetGcodeTerminalControl()
		{
			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Margin = new BorderDouble(0,4);

			TextWidget gcodeTerminalLabel = new TextWidget("Gcode Terminal");
			gcodeTerminalLabel.AutoExpandBoundsToText = true;
			gcodeTerminalLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			gcodeTerminalLabel.VAnchor = VAnchor.ParentCenter;

			openGcodeTerminalButton = textImageButtonFactory.Generate("Show Terminal".Localize().ToUpper());
			openGcodeTerminalButton.Click += new EventHandler(openGcodeTerminalButton_Click);

			buttonRow.AddChild(gcodeTerminalLabel);
			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(openGcodeTerminalButton);

			return buttonRow;


		}

        static EePromMarlinWidget openEePromMarlinWidget = null;
        static EePromRepetierWidget openEePromRepetierWidget = null;
        string noEepromMappingMessage = "Oops! There is no eeprom mapping for your printer's firmware.".Localize();
        string noEepromMappingTitle = "Warning - No EEProm Mapping".Localize();
        string groupBoxTitle = "EEProm Settings".Localize();
        private FlowLayoutWidget GetEEPromControl()
        {
            FlowLayoutWidget buttonRow = new FlowLayoutWidget();
            buttonRow.HAnchor = HAnchor.ParentLeftRight;
            buttonRow.Margin = new BorderDouble(0,4);


            TextWidget notificationSettingsLabel = new TextWidget("EEProm Settings");
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

            configureEePromButton = textImageButtonFactory.Generate("Configure".Localize().ToUpper());
            
            //buttonRow.AddChild(eePromIcon);
            buttonRow.AddChild(notificationSettingsLabel);
            buttonRow.AddChild(new HorizontalSpacer());
            buttonRow.AddChild(configureEePromButton);

            return buttonRow;
        }     

        private void AddHandlers()
        {
            configureAutoLevelButton.Click += new EventHandler(configureAutoLevelButton_Click);
            configureEePromButton.Click += new EventHandler(configureEePromButton_Click);
            PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
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
                                openEePromRepetierWidget = new EePromRepetierWidget();
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
                                openEePromMarlinWidget = new EePromMarlinWidget();
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

        void enablePrintLeveling_Click(object sender, EventArgs mouseEvent)
        {
            ActivePrinterProfile.Instance.DoPrintLeveling = true;
        }

        void disablePrintLeveling_Click(object sender, EventArgs mouseEvent)
        {
            ActivePrinterProfile.Instance.DoPrintLeveling = false;
        }

        void SetPrintLevelButtonVisiblity()
        {
            enablePrintLevelingButton.Visible = !ActivePrinterProfile.Instance.DoPrintLeveling;
            disablePrintLevelingButton.Visible = ActivePrinterProfile.Instance.DoPrintLeveling;

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
                    case PrinterConnectionAndCommunication.CommunicationStates.PreparingToPrintToSd:
                    case PrinterConnectionAndCommunication.CommunicationStates.PrintingToSd:
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