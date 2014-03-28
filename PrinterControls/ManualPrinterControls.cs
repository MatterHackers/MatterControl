/*
Copyright (c) 2014, Lars Brubaker
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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.IO.Ports;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Image;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
    public class XYZColors
    {
        public static RGBA_Bytes xColor = new RGBA_Bytes(190, 190, 190);
        public static RGBA_Bytes yColor = new RGBA_Bytes(255, 255, 255);
        public static RGBA_Bytes zColor = new RGBA_Bytes(255, 255, 255);
        public static RGBA_Bytes eColor = new RGBA_Bytes(190, 190, 190);
        public XYZColors()
        {
        }
    }

    

    public class ManualPrinterControls : GuiWidget
    {
        readonly double minExtrutionRatio = .5;
        readonly double maxExtrusionRatio = 2;
        readonly double minFeedRateRatio = .5;
        readonly double maxFeedRateRatio = 2;
        readonly int TallButtonHeight = 25;

        Button disableMotors;
        Button manualMove;
        Button homeAllButton;
        Button homeXButton;
        Button homeYButton;
        Button homeZButton;
		Button enablePrintLevelingButton;
		Button disablePrintLevelingButton;

        DisableableWidget extruderTemperatureControlWidget;
        DisableableWidget bedTemperatureControlWidget;
        DisableableWidget movementControlsContainer;
        DisableableWidget fanControlsContainer;
        DisableableWidget eePromControlsContainer;
        DisableableWidget tuningAdjustmentControlsContainer;
        DisableableWidget terminalCommunicationsContainer;
        DisableableWidget sdCardManagerContainer;
        DisableableWidget printLevelContainer;
        DisableableWidget macroControls;

        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

        public static double XSpeed { get { return GetMovementSpeeds()["x"]; } }
        public static double YSpeed { get { return GetMovementSpeeds()["y"]; } }
        public static double ZSpeed { get { return GetMovementSpeeds()["z"]; } }

        public static double EFeedRate(int extruderIndex)
        {
            if (GetMovementSpeeds().ContainsKey("e" + extruderIndex.ToString()))
            {
                return GetMovementSpeeds()["e" + extruderIndex.ToString()];
            }

            return GetMovementSpeeds()["e0"];
        }

        static Dictionary<string, double> GetMovementSpeeds()
        {
            Dictionary<string, double> speeds = new Dictionary<string, double>();
            string movementSpeedsString = GetMovementSpeedsString();
            string[] allSpeeds = movementSpeedsString.Split(',');
            for (int i = 0; i < allSpeeds.Length / 2; i++)
            {
                speeds.Add(allSpeeds[i * 2 + 0], double.Parse(allSpeeds[i * 2 + 1]));
            }

            return speeds;
        }

        static string GetMovementSpeedsString()
        {
            string presets = "x,3000,y,3000,z,315,e0,150"; // stored x,y,z,e1,e2,e3,...
            if (PrinterCommunication.Instance != null && ActivePrinterProfile.Instance.ActivePrinter != null)
            {
                string savedSettings = ActivePrinterProfile.Instance.ActivePrinter.ManualMovementSpeeds;
                if (savedSettings != null && savedSettings != "")
                {
                    presets = savedSettings;
                }
            }

            return presets;
        }

        static void SetMovementSpeeds(object seder, EventArgs e)
        {
            StringEventArgs stringEvent = e as StringEventArgs;
            if (stringEvent != null && stringEvent.Data != null)
            {
                ActivePrinterProfile.Instance.ActivePrinter.ManualMovementSpeeds = stringEvent.Data;
                ActivePrinterProfile.Instance.ActivePrinter.Commit();
                ApplicationWidget.Instance.ReloadBackPanel();
            }
        }

        EditManualMovementSpeedsWindow editSettingsWindow;
        public ManualPrinterControls()
        {
            SetDisplayAttributes();

            HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            VAnchor = Agg.UI.VAnchor.FitToChildren;

            FlowLayoutWidget controlsTopToBottomLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
            controlsTopToBottomLayout.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            controlsTopToBottomLayout.VAnchor = Agg.UI.VAnchor.FitToChildren;
            controlsTopToBottomLayout.Name = "ManualPrinterControls.ControlsContainer";

            controlsTopToBottomLayout.Padding = new BorderDouble(3, 0);

            AddTemperatureControls(controlsTopToBottomLayout);

            FlowLayoutWidget centerControlsContainer = new FlowLayoutWidget();
            centerControlsContainer.HAnchor = HAnchor.ParentLeftRight;

            AddMovementControls(centerControlsContainer);

            // put in the terminal communications
            {
                terminalCommunicationsContainer = new DisableableWidget();
                terminalCommunicationsContainer.AddChild(CreateTerminalControlsContainer());

                FlowLayoutWidget rightColumnContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                rightColumnContainer.AddChild(terminalCommunicationsContainer);
				rightColumnContainer.Width = 220;
                rightColumnContainer.VAnchor |= VAnchor.ParentTop;

                AddFanControls(rightColumnContainer);

                centerControlsContainer.AddChild(rightColumnContainer);
            }

            controlsTopToBottomLayout.AddChild(centerControlsContainer);

            sdCardManagerContainer = new DisableableWidget();
            sdCardManagerContainer.AddChild(CreateSdCardManagerContainer());
            if (false)// || ActivePrinterProfile.Instance.ActivePrinter == null || ActivePrinterProfile.Instance.ActivePrinter.GetFeatures().HasSdCard())
            {
                controlsTopToBottomLayout.AddChild(sdCardManagerContainer);
            }

            macroControls = new DisableableWidget();
            macroControls.AddChild(new MacroControls());
            controlsTopToBottomLayout.AddChild(macroControls);

            AddAdjustmentControls(controlsTopToBottomLayout);

            this.AddChild(controlsTopToBottomLayout);
            AddHandlers();
            SetVisibleControls();
        }

        private void AddFanControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
			GroupBox fanControlsGroupBox = new GroupBox(LocalizedString.Get("Fan Controls"));

            fanControlsGroupBox.Margin = new BorderDouble(0);
            fanControlsGroupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            fanControlsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            fanControlsGroupBox.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
            fanControlsGroupBox.VAnchor = Agg.UI.VAnchor.FitToChildren;

            {
                FlowLayoutWidget fanControlsLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
                fanControlsLayout.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                fanControlsLayout.VAnchor = Agg.UI.VAnchor.FitToChildren;
                fanControlsLayout.Padding = new BorderDouble(3, 5, 3, 0);
                {
                    fanControlsLayout.AddChild(CreateFanControls());
                }

                fanControlsGroupBox.AddChild(fanControlsLayout);
            }

            fanControlsContainer = new DisableableWidget();
            fanControlsContainer.AddChild(fanControlsGroupBox);

            if (ActivePrinterProfile.Instance.ActivePrinter == null
                || ActivePrinterProfile.Instance.ActivePrinter.GetFeatures().HasFan())
            {
                controlsTopToBottomLayout.AddChild(fanControlsContainer);
            }
        }

        private void AddEePromControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
            GroupBox eePromControlsGroupBox = new GroupBox(LocalizedString.Get("EEProm Settings"));

			eePromControlsGroupBox.Margin = new BorderDouble(0);
            eePromControlsGroupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            eePromControlsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            eePromControlsGroupBox.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
            eePromControlsGroupBox.VAnchor = Agg.UI.VAnchor.FitToChildren;
			eePromControlsGroupBox.Height = 68;

            {
				FlowLayoutWidget eePromControlsLayout = new FlowLayoutWidget();
				eePromControlsLayout.HAnchor |= HAnchor.ParentLeftRight;
				eePromControlsLayout.VAnchor |= Agg.UI.VAnchor.ParentCenter;
				eePromControlsLayout.Margin = new BorderDouble(3, 0, 3, 6);
				eePromControlsLayout.Padding = new BorderDouble(0);
                {
					Agg.Image.ImageBuffer eePromImage = new Agg.Image.ImageBuffer();
					ImageIO.LoadImageData(Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath,"Icons", "PrintStatusControls", "leveling-24x24.png"), eePromImage);
					ImageWidget eePromIcon = new ImageWidget(eePromImage);
					eePromIcon.Margin = new BorderDouble (right: 6);

					Button openEePromWindow = textImageButtonFactory.Generate(LocalizedString.Get("CONFIGURE"));
                    openEePromWindow.Click += (sender, e) =>
                    {
#if false // This is to force the creation of the repetier window for testing when we don't have repetier firmware.
                        new MatterHackers.MatterControl.EeProm.EePromRepetierWidget();
#else
						switch(PrinterCommunication.Instance.FirmwareType)
                        {
                            case PrinterCommunication.FirmwareTypes.Repetier:
                                new MatterHackers.MatterControl.EeProm.EePromRepetierWidget();
                            break;

                            case PrinterCommunication.FirmwareTypes.Marlin:
                                new MatterHackers.MatterControl.EeProm.EePromMarlinWidget();
                            break;

                            default:
                                UiThread.RunOnIdle((state) => 
                                {
									string message = LocalizedString.Get("Oops! There is no eeprom mapping for your printer's firmware.");
                                    StyledMessageBox.ShowMessageBox(message, "Warning no eeprom mapping", StyledMessageBox.MessageType.OK);
                                }
                                );
                            break;
                        }
#endif
                    };
					//eePromControlsLayout.AddChild(eePromIcon);
                    eePromControlsLayout.AddChild(openEePromWindow);
                }

                eePromControlsGroupBox.AddChild(eePromControlsLayout);
            }

            eePromControlsContainer = new DisableableWidget();
            eePromControlsContainer.AddChild(eePromControlsGroupBox);

            controlsTopToBottomLayout.AddChild(eePromControlsContainer);
        }

        private void AddMovementControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
            Button editButton;
			GroupBox movementControlsGroupBox = new GroupBox(textImageButtonFactory.GenerateGroupBoxLableWithEdit(LocalizedString.Get("Movement Controls"), out editButton));
            editButton.Click += (sender, e) =>
            {
                if (editSettingsWindow == null)
                {
                    editSettingsWindow = new EditManualMovementSpeedsWindow("Movement Speeds", GetMovementSpeedsString(), SetMovementSpeeds);
                    editSettingsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { editSettingsWindow = null; };
                }
                else
                {
                    editSettingsWindow.BringToFront();
                }
            };

            movementControlsGroupBox.Margin = new BorderDouble(0);
            movementControlsGroupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            movementControlsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            movementControlsGroupBox.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
            movementControlsGroupBox.VAnchor = Agg.UI.VAnchor.FitToChildren;

            {
                FlowLayoutWidget manualControlsLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
                manualControlsLayout.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                manualControlsLayout.VAnchor = Agg.UI.VAnchor.FitToChildren;
                manualControlsLayout.Padding = new BorderDouble(3, 5, 3, 0);
                {
                    manualControlsLayout.AddChild(GetHomeButtonBar());
                    manualControlsLayout.AddChild(CreateSeparatorLine());
                    manualControlsLayout.AddChild(new JogControls(new XYZColors()));
                    manualControlsLayout.AddChild(CreateSeparatorLine());
                    //manualControlsLayout.AddChild(GetManualMoveBar());
                }

                movementControlsGroupBox.AddChild(manualControlsLayout);
            }

            movementControlsContainer = new DisableableWidget();
            movementControlsContainer.AddChild(movementControlsGroupBox);
            controlsTopToBottomLayout.AddChild(movementControlsContainer);
        }

        private void AddTemperatureControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
            FlowLayoutWidget temperatureControlContainer = new FlowLayoutWidget();
            temperatureControlContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			temperatureControlContainer.Margin = new BorderDouble (top: 10);

            extruderTemperatureControlWidget = new DisableableWidget();
            extruderTemperatureControlWidget.AddChild(new ExtruderTemperatureControlWidget());
            temperatureControlContainer.AddChild(extruderTemperatureControlWidget);

            bedTemperatureControlWidget = new DisableableWidget();
            bedTemperatureControlWidget.AddChild(new BedTemperatureControlWidget());

            if (ActivePrinterProfile.Instance.ActivePrinter == null
                || ActivePrinterProfile.Instance.ActivePrinter.GetFeatures().HasHeatedBed())
            {
                temperatureControlContainer.AddChild(bedTemperatureControlWidget);
            }

            controlsTopToBottomLayout.AddChild(temperatureControlContainer);
        }

        EditableNumberDisplay fanSpeedDisplay;
        private GuiWidget CreateFanControls()
        {
            PrinterCommunication.Instance.FanSpeedSet.RegisterEvent(FanSpeedChanged_Event, ref unregisterEvents);

            FlowLayoutWidget leftToRight = new FlowLayoutWidget();
            leftToRight.Padding = new BorderDouble(3, 0, 0, 5);

			TextWidget fanSpeedDescription = new TextWidget(LocalizedString.Get("Fan Speed:"), textColor: ActiveTheme.Instance.PrimaryTextColor);
            fanSpeedDescription.VAnchor = Agg.UI.VAnchor.ParentCenter;
            leftToRight.AddChild(fanSpeedDescription);

            fanSpeedDisplay = new EditableNumberDisplay(textImageButtonFactory, PrinterCommunication.Instance.FanSpeed.ToString(), "255");
            fanSpeedDisplay.EditComplete += (sender, e) =>
            {
                PrinterCommunication.Instance.FanSpeed = (int)fanSpeedDisplay.GetValue();
            };

            leftToRight.AddChild(fanSpeedDisplay);

            return leftToRight;
        }

        void FanSpeedChanged_Event(object sender, EventArgs e)
        {
            int printerFanSpeed = PrinterCommunication.Instance.FanSpeed;

            fanSpeedDisplay.SetDisplayString(printerFanSpeed.ToString());
        }

        private static GuiWidget CreateSeparatorLine()
        {
            GuiWidget topLine = new GuiWidget(10, 1);
            topLine.Margin = new BorderDouble(0, 5);
            topLine.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            topLine.BackgroundColor = ActiveTheme.Instance.PrimaryTextColor;
            return topLine;
        }

        NumberEdit feedRateValue;
        Slider feedRateRatioSlider;
        Slider extrusionRatioSlider;
        NumberEdit extrusionValue;
        PrintLevelWizardWindow printLevelWizardWindow;

        private void AddAdjustmentControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
			GroupBox adjustmentControlsGroupBox = new GroupBox(LocalizedString.Get("Tuning Adjustment (while printing)"));
            adjustmentControlsGroupBox.Margin = new BorderDouble(0);
            adjustmentControlsGroupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            adjustmentControlsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            adjustmentControlsGroupBox.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            adjustmentControlsGroupBox.Height = 93;

            {
                FlowLayoutWidget tuningRatiosLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
                tuningRatiosLayout.Margin = new BorderDouble(0, 0, 0, 6);
                tuningRatiosLayout.AnchorAll();
                tuningRatiosLayout.Padding = new BorderDouble(3, 5, 3, 0);
                TextWidget feedRateDescription;
                {
                    FlowLayoutWidget feedRateLeftToRight;
                    {
                        feedRateValue = new NumberEdit(1, allowDecimals: true, minValue: minFeedRateRatio, maxValue: maxFeedRateRatio, pixelWidth: 40);

                        feedRateLeftToRight = new FlowLayoutWidget();

						feedRateDescription = new TextWidget(LocalizedString.Get("Speed Multiplier"));
                        feedRateDescription.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                        feedRateLeftToRight.AddChild(feedRateDescription);
                        feedRateRatioSlider = new Slider(new Vector2(), 300, minFeedRateRatio, maxFeedRateRatio);
                        feedRateRatioSlider.Margin = new BorderDouble(5, 0);
                        feedRateRatioSlider.Value = PrinterCommunication.Instance.FeedRateRatio;
                        feedRateRatioSlider.View.BackgroundColor = new RGBA_Bytes();
                        feedRateRatioSlider.ValueChanged += (sender, e) =>
                        {
                            PrinterCommunication.Instance.FeedRateRatio = feedRateRatioSlider.Value;
                        };
                        PrinterCommunication.Instance.FeedRateRatioChanged.RegisterEvent(FeedRateRatioChanged_Event, ref unregisterEvents);
                        feedRateValue.EditComplete += (sender, e) =>
                        {
                            feedRateRatioSlider.Value = feedRateValue.Value;
                        };
                        feedRateLeftToRight.AddChild(feedRateRatioSlider);
                        tuningRatiosLayout.AddChild(feedRateLeftToRight);

                        feedRateLeftToRight.AddChild(feedRateValue);
                        feedRateValue.Margin = new BorderDouble(0, 0, 5, 0);
                        textImageButtonFactory.FixedHeight = (int)feedRateValue.Height + 1;
						feedRateLeftToRight.AddChild(textImageButtonFactory.Generate(LocalizedString.Get("Set")));
                    }

                    TextWidget extrusionDescription;
                    {
                        extrusionValue = new NumberEdit(1, allowDecimals: true, minValue: minExtrutionRatio, maxValue: maxExtrusionRatio, pixelWidth: 40);

                        FlowLayoutWidget leftToRight = new FlowLayoutWidget();

						extrusionDescription = new TextWidget(LocalizedString.Get("Extrusion Multiplier"));
                        extrusionDescription.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                        leftToRight.AddChild(extrusionDescription);
                        extrusionRatioSlider = new Slider(new Vector2(), 300, minExtrutionRatio, maxExtrusionRatio);
                        extrusionRatioSlider.Margin = new BorderDouble(5, 0);
                        extrusionRatioSlider.Value = PrinterCommunication.Instance.ExtrusionRatio;
                        extrusionRatioSlider.View.BackgroundColor = new RGBA_Bytes();
                        extrusionRatioSlider.ValueChanged += (sender, e) =>
                        {
                            PrinterCommunication.Instance.ExtrusionRatio = extrusionRatioSlider.Value;
                        };
                        PrinterCommunication.Instance.ExtrusionRatioChanged.RegisterEvent(ExtrusionRatioChanged_Event, ref unregisterEvents);
                        extrusionValue.EditComplete += (sender, e) =>
                        {
                            extrusionRatioSlider.Value = extrusionValue.Value;
                        };
                        leftToRight.AddChild(extrusionRatioSlider);
                        tuningRatiosLayout.AddChild(leftToRight);
                        leftToRight.AddChild(extrusionValue);
                        extrusionValue.Margin = new BorderDouble(0, 0, 5, 0);
                        textImageButtonFactory.FixedHeight = (int)extrusionValue.Height + 1;
						leftToRight.AddChild(textImageButtonFactory.Generate(LocalizedString.Get("Set")));
                    }

                    feedRateDescription.Width = extrusionDescription.Width;
                    feedRateDescription.MinimumSize = new Vector2(extrusionDescription.Width, feedRateDescription.MinimumSize.y);
                    feedRateLeftToRight.HAnchor = HAnchor.FitToChildren;
                    feedRateLeftToRight.VAnchor = VAnchor.FitToChildren;
                }

                adjustmentControlsGroupBox.AddChild(tuningRatiosLayout);
            }

            tuningAdjustmentControlsContainer = new DisableableWidget();
            tuningAdjustmentControlsContainer.AddChild(adjustmentControlsGroupBox);
            controlsTopToBottomLayout.AddChild(tuningAdjustmentControlsContainer);
        }

        void ExtrusionRatioChanged_Event(object sender, EventArgs e)
        {
            extrusionRatioSlider.Value = PrinterCommunication.Instance.ExtrusionRatio;
            extrusionValue.Value = ((int)(PrinterCommunication.Instance.ExtrusionRatio * 100 + .5)) / 100.0;
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }

            base.OnClosed(e);
        }

        void FeedRateRatioChanged_Event(object sender, EventArgs e)
        {
            feedRateRatioSlider.Value = PrinterCommunication.Instance.FeedRateRatio;
            feedRateValue.Value = ((int)(PrinterCommunication.Instance.FeedRateRatio * 100 + .5)) / 100.0;
        }

		TextWidget printLevelingStatusLabel;

        private GuiWidget CreatePrintLevelingControlsContainer()
        {
            GroupBox printLevelingControlsContainer;
			printLevelingControlsContainer = new GroupBox(LocalizedString.Get("Automatic Calibration"));

            printLevelingControlsContainer.Margin = new BorderDouble(0);
            printLevelingControlsContainer.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            printLevelingControlsContainer.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            printLevelingControlsContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            printLevelingControlsContainer.Height = 68;

            {
                FlowLayoutWidget buttonBar = new FlowLayoutWidget();
                buttonBar.HAnchor |= HAnchor.ParentLeftRight;
                buttonBar.VAnchor |= Agg.UI.VAnchor.ParentCenter;
                buttonBar.Margin = new BorderDouble(0, 0, 0, 0);
                buttonBar.Padding = new BorderDouble(0);

                this.textImageButtonFactory.FixedHeight = TallButtonHeight;

				Button runPrintLevelingButton = textImageButtonFactory.Generate(LocalizedString.Get("CONFIGURE"));
                runPrintLevelingButton.Margin = new BorderDouble(left:6);
                runPrintLevelingButton.VAnchor = VAnchor.ParentCenter;
                runPrintLevelingButton.Click += new ButtonBase.ButtonEventHandler(runPrintLeveling_Click);

                Agg.Image.ImageBuffer levelingImage = new Agg.Image.ImageBuffer();
				ImageIO.LoadImageData(Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath,"Icons", "PrintStatusControls", "leveling-24x24.png"), levelingImage);
                ImageWidget levelingIcon = new ImageWidget(levelingImage);
				levelingIcon.Margin = new BorderDouble (right: 6);

				enablePrintLevelingButton = textImageButtonFactory.Generate(LocalizedString.Get("ENABLE"));
				enablePrintLevelingButton.Margin = new BorderDouble(left:6);
				enablePrintLevelingButton.VAnchor = VAnchor.ParentCenter;
				enablePrintLevelingButton.Click += new ButtonBase.ButtonEventHandler(enablePrintLeveling_Click);

				disablePrintLevelingButton = textImageButtonFactory.Generate(LocalizedString.Get("DISABLE"));
				disablePrintLevelingButton.Margin = new BorderDouble(left:6);
				disablePrintLevelingButton.VAnchor = VAnchor.ParentCenter;
				disablePrintLevelingButton.Click += new ButtonBase.ButtonEventHandler(disablePrintLeveling_Click);

				CheckBox doLevelingCheckBox = new CheckBox(LocalizedString.Get("Enable Automatic Print Leveling"));
                doLevelingCheckBox.Margin = new BorderDouble(left: 3);
                doLevelingCheckBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                doLevelingCheckBox.VAnchor = VAnchor.ParentCenter;
                doLevelingCheckBox.Checked = ActivePrinterProfile.Instance.DoPrintLeveling;

				printLevelingStatusLabel = new TextWidget ("");
				printLevelingStatusLabel.AutoExpandBoundsToText = true;
				printLevelingStatusLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				printLevelingStatusLabel.VAnchor = VAnchor.ParentCenter;

				GuiWidget hSpacer = new GuiWidget ();
				hSpacer.HAnchor = HAnchor.ParentLeftRight;

                buttonBar.AddChild(levelingIcon);
				//buttonBar.AddChild(doLevelingCheckBox);
				buttonBar.AddChild (printLevelingStatusLabel);
				buttonBar.AddChild (hSpacer);
				buttonBar.AddChild(enablePrintLevelingButton);
				buttonBar.AddChild(disablePrintLevelingButton);
                buttonBar.AddChild(runPrintLevelingButton);
                doLevelingCheckBox.CheckedStateChanged += (sender, e) =>
                {
                    ActivePrinterProfile.Instance.DoPrintLeveling = doLevelingCheckBox.Checked;
                };
                ActivePrinterProfile.Instance.DoPrintLevelingChanged.RegisterEvent((sender, e) =>
                {
					SetPrintLevelButtonVisiblity();

                }, ref unregisterEvents);

                printLevelingControlsContainer.AddChild(buttonBar);
            }
			SetPrintLevelButtonVisiblity ();
            return printLevelingControlsContainer;
        }

        private void OpenPrintLevelWizard()
        {
            if (printLevelWizardWindow == null)
            {
                printLevelWizardWindow = new PrintLevelWizardWindow();
                printLevelWizardWindow.Closed += (sender, e) =>
                {
                    printLevelWizardWindow = null;
                };
                printLevelWizardWindow.ShowAsSystemWindow();
            }
            else 
            {
                printLevelWizardWindow.BringToFront();
            }
        }

        private GuiWidget CreateTerminalControlsContainer()
        {
            GroupBox terminalControlsContainer;
			terminalControlsContainer = new GroupBox(LocalizedString.Get("Communications"));

            terminalControlsContainer.Margin = new BorderDouble(0);
            terminalControlsContainer.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            terminalControlsContainer.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            terminalControlsContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            terminalControlsContainer.Height = 68;

            OutputScrollWindow.HookupPrinterOutput();

            {
                FlowLayoutWidget buttonBar = new FlowLayoutWidget();
                buttonBar.HAnchor |= HAnchor.ParentLeftRight;
                buttonBar.VAnchor |= Agg.UI.VAnchor.ParentCenter;
				buttonBar.Margin = new BorderDouble(3, 0, 3, 6);
                buttonBar.Padding = new BorderDouble(0);

                this.textImageButtonFactory.FixedHeight = TallButtonHeight;

				Agg.Image.ImageBuffer terminalImage = new Agg.Image.ImageBuffer();
				ImageIO.LoadImageData(Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath,"Icons", "PrintStatusControls", "terminal-24x24.png"), terminalImage);
				ImageWidget terminalIcon = new ImageWidget(terminalImage);
				terminalIcon.Margin = new BorderDouble (right: 6);

				Button showTerminal = textImageButtonFactory.Generate(LocalizedString.Get("SHOW TERMINAL"));
                showTerminal.Margin = new BorderDouble(0);
                showTerminal.Click += (sender, e) =>
                {
                    OutputScrollWindow.Show();
                };

				//buttonBar.AddChild(terminalIcon);
                buttonBar.AddChild(showTerminal);

                terminalControlsContainer.AddChild(buttonBar);
            }

            return terminalControlsContainer;
        }

        private GuiWidget CreateSdCardManagerContainer()
        {
            GroupBox terminalControlsContainer;
            terminalControlsContainer = new GroupBox("SD Card Printing");

            terminalControlsContainer.Margin = new BorderDouble(top: 10);
            terminalControlsContainer.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            terminalControlsContainer.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            terminalControlsContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            terminalControlsContainer.Height = 68;

            {
                FlowLayoutWidget buttonBar = new FlowLayoutWidget();
                buttonBar.HAnchor |= HAnchor.ParentLeftRight;
                buttonBar.VAnchor |= Agg.UI.VAnchor.ParentCenter;
                buttonBar.Margin = new BorderDouble(3, 0, 3, 6);
                buttonBar.Padding = new BorderDouble(0);

                this.textImageButtonFactory.FixedHeight = TallButtonHeight;

                Button showSDPrintingPannel = textImageButtonFactory.Generate("SD CARD MANAGER");
                showSDPrintingPannel.Margin = new BorderDouble(left: 10);
                showSDPrintingPannel.Click += (sender, e) =>
                {
                    SDCardManager.Show();
                };
                buttonBar.AddChild(showSDPrintingPannel);

                terminalControlsContainer.AddChild(buttonBar);
            }

            return terminalControlsContainer;
        }

        private void SetDisplayAttributes()
        {
            //this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            
            this.textImageButtonFactory.normalFillColor = RGBA_Bytes.White;
            this.textImageButtonFactory.disabledFillColor = RGBA_Bytes.White;

            this.textImageButtonFactory.FixedHeight = TallButtonHeight;
            this.textImageButtonFactory.fontSize = 11;

            this.textImageButtonFactory.disabledTextColor = RGBA_Bytes.DarkGray;
            this.textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
            this.textImageButtonFactory.normalTextColor = RGBA_Bytes.Black;
            this.textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
        }

        private void SetVisibleControls()
        {
            if (ActivePrinterProfile.Instance.ActivePrinter == null)
            {
                // no printer selected
                extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                bedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                movementControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                fanControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                tuningAdjustmentControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
				terminalCommunicationsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                sdCardManagerContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                macroControls.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
            }
            else // we at least have a printer selected
            {
                switch (PrinterCommunication.Instance.CommunicationState)
                {
                    case PrinterCommunication.CommunicationStates.Disconnecting:
                    case PrinterCommunication.CommunicationStates.ConnectionLost:
                    case PrinterCommunication.CommunicationStates.Disconnected:
                    case PrinterCommunication.CommunicationStates.AttemptingToConnect:
                    case PrinterCommunication.CommunicationStates.FailedToConnect:
                        extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
                        bedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
                        movementControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
                        fanControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        tuningAdjustmentControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        terminalCommunicationsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        sdCardManagerContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        macroControls.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
                        break;

                    case PrinterCommunication.CommunicationStates.FinishedPrint:
                    case PrinterCommunication.CommunicationStates.Connected:
                        extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        bedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        movementControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        fanControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        terminalCommunicationsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        sdCardManagerContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        macroControls.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        tuningAdjustmentControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        break;

                    case PrinterCommunication.CommunicationStates.PreparingToPrint:
                    case PrinterCommunication.CommunicationStates.Printing:
                        switch (PrinterCommunication.Instance.PrintingState)
                        {
                            case PrinterCommunication.DetailedPrintingState.HomingAxis:
                            case PrinterCommunication.DetailedPrintingState.HeatingBed:
                            case PrinterCommunication.DetailedPrintingState.HeatingExtruder:
                            case PrinterCommunication.DetailedPrintingState.Printing:
                                extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                                bedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                                movementControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
                                fanControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                                tuningAdjustmentControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                                terminalCommunicationsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                                sdCardManagerContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                                macroControls.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                                break;

                            default:
                                throw new NotImplementedException();
                        }
                        break;

                    case PrinterCommunication.CommunicationStates.Paused:
                        extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        bedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        movementControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        fanControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        tuningAdjustmentControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        terminalCommunicationsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        sdCardManagerContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        macroControls.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        private FlowLayoutWidget GetHomeButtonBar()
        {
            FlowLayoutWidget homeButtonBar = new FlowLayoutWidget();
            homeButtonBar.HAnchor = HAnchor.ParentLeftRight;
            homeButtonBar.Margin = new BorderDouble(3, 0, 3, 6);
            homeButtonBar.Padding = new BorderDouble(0);

            string homeIconFile = "icon_home_white_24x24.png";
            string fileAndPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, homeIconFile);
            ImageBuffer helpIconImage = new ImageBuffer();
            ImageIO.LoadImageData(fileAndPath, helpIconImage);
            ImageWidget homeIconImageWidget = new ImageWidget(helpIconImage);
            homeIconImageWidget.Margin = new BorderDouble(0, 0, 6, 0);
            homeIconImageWidget.OriginRelativeParent += new Vector2(0, 2);
            RGBA_Bytes oldColor = this.textImageButtonFactory.normalFillColor;
            textImageButtonFactory.normalFillColor = new RGBA_Bytes(190, 190, 190);
			homeAllButton = textImageButtonFactory.Generate(LocalizedString.Get("ALL"));
            this.textImageButtonFactory.normalFillColor = oldColor;
            homeAllButton.Margin = new BorderDouble(0, 0, 6, 0);
            homeAllButton.Click += new ButtonBase.ButtonEventHandler(homeAll_Click);

            textImageButtonFactory.FixedWidth = (int)homeAllButton.Width;
            homeXButton = textImageButtonFactory.Generate("X", centerText: true);
            homeXButton.Margin = new BorderDouble(0, 0, 6, 0);
            homeXButton.Click += new ButtonBase.ButtonEventHandler(homeXButton_Click);

            homeYButton = textImageButtonFactory.Generate("Y", centerText: true);
            homeYButton.Margin = new BorderDouble(0, 0, 6, 0);
            homeYButton.Click += new ButtonBase.ButtonEventHandler(homeYButton_Click);

            homeZButton = textImageButtonFactory.Generate("Z", centerText: true);
            homeZButton.Margin = new BorderDouble(0, 0, 6, 0);
            homeZButton.Click += new ButtonBase.ButtonEventHandler(homeZButton_Click);

            textImageButtonFactory.normalFillColor = RGBA_Bytes.White;
            textImageButtonFactory.FixedWidth = 0;

            GuiWidget spacer = new GuiWidget();
            spacer.HAnchor = HAnchor.ParentLeftRight;

			disableMotors = textImageButtonFactory.Generate(LocalizedString.Get("UNLOCK"));
            disableMotors.Margin = new BorderDouble(0);
            disableMotors.Click += new ButtonBase.ButtonEventHandler(disableMotors_Click);

            GuiWidget spacerReleaseShow = new GuiWidget(10, 0);

            homeButtonBar.AddChild(homeIconImageWidget);
            homeButtonBar.AddChild(homeAllButton);
            homeButtonBar.AddChild(homeXButton);
            homeButtonBar.AddChild(homeYButton);
            homeButtonBar.AddChild(homeZButton);
            homeButtonBar.AddChild(spacer);
            homeButtonBar.AddChild(disableMotors);
            homeButtonBar.AddChild(spacerReleaseShow);

            return homeButtonBar;
        }

        private FlowLayoutWidget GetManualMoveBar()
        {
            FlowLayoutWidget manualMoveBar = new FlowLayoutWidget();
            manualMoveBar.HAnchor = HAnchor.ParentLeftRight;
            manualMoveBar.Margin = new BorderDouble(3, 0, 3, 6);
            manualMoveBar.Padding = new BorderDouble(0);

            TextWidget xMoveLabel = new TextWidget("X:");
            xMoveLabel.Margin = new BorderDouble(0, 0, 6, 0);
            xMoveLabel.VAnchor = VAnchor.ParentCenter;

            MHTextEditWidget xMoveEdit = new MHTextEditWidget("0");
            xMoveEdit.Margin = new BorderDouble(0, 0, 6, 0);
            xMoveEdit.VAnchor = VAnchor.ParentCenter;

            TextWidget yMoveLabel = new TextWidget("Y:");
            yMoveLabel.Margin = new BorderDouble(0, 0, 6, 0);
            yMoveLabel.VAnchor = VAnchor.ParentCenter;

            MHTextEditWidget yMoveEdit = new MHTextEditWidget("0");
            yMoveEdit.Margin = new BorderDouble(0, 0, 6, 0);
            yMoveEdit.VAnchor = VAnchor.ParentCenter;

            TextWidget zMoveLabel = new TextWidget("Z:");
            zMoveLabel.Margin = new BorderDouble(0, 0, 6, 0);
            zMoveLabel.VAnchor = VAnchor.ParentCenter;

            MHTextEditWidget zMoveEdit = new MHTextEditWidget("0");
            zMoveEdit.Margin = new BorderDouble(0, 0, 6, 0);
            zMoveEdit.VAnchor = VAnchor.ParentCenter;

            manualMove = textImageButtonFactory.Generate("MOVE TO");
            manualMove.Margin = new BorderDouble(0, 0, 6, 0);
            manualMove.Click += new ButtonBase.ButtonEventHandler(disableMotors_Click);

            GuiWidget spacer = new GuiWidget();
            spacer.HAnchor = HAnchor.ParentLeftRight;

            manualMoveBar.AddChild(xMoveLabel);
            manualMoveBar.AddChild(xMoveEdit);

            manualMoveBar.AddChild(yMoveLabel);
            manualMoveBar.AddChild(yMoveEdit);

            manualMoveBar.AddChild(zMoveLabel);
            manualMoveBar.AddChild(zMoveEdit);

            manualMoveBar.AddChild(manualMove);
            
            manualMoveBar.AddChild(spacer);

            return manualMoveBar;
        }

        event EventHandler unregisterEvents;
        private void AddHandlers()
        {
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(onThemeChanged, ref unregisterEvents);
            PrinterCommunication.Instance.ConnectionStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
            PrinterCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
        }

        private void onPrinterStatusChanged(object sender, EventArgs e)
        {
            SetVisibleControls();
            this.Invalidate();
        }

        private void onThemeChanged(object sender, EventArgs e)
        {
            //this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
            //SetVisibleControls();
            //this.Invalidate();
        }

		void enablePrintLeveling_Click(object sender, MouseEventArgs mouseEvent)
		{
			ActivePrinterProfile.Instance.DoPrintLeveling = true;
		}

		void disablePrintLeveling_Click(object sender, MouseEventArgs mouseEvent)
		{
			ActivePrinterProfile.Instance.DoPrintLeveling = false;
		}

		void SetPrintLevelButtonVisiblity()
		{
			enablePrintLevelingButton.Visible = !ActivePrinterProfile.Instance.DoPrintLeveling;
			disablePrintLevelingButton.Visible = ActivePrinterProfile.Instance.DoPrintLeveling;

			if (ActivePrinterProfile.Instance.DoPrintLeveling) {
				printLevelingStatusLabel.Text = LocalizedString.Get ("Automatic Print Leveling (enabled)");
			}
			else
			{
				printLevelingStatusLabel.Text = LocalizedString.Get ("Automatic Print Leveling (disabled)");
			}
		}


        void disableMotors_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrinterCommunication.Instance.ReleaseMotors();
        }

        void homeXButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrinterCommunication.Instance.HomeAxis(PrinterCommunication.Axis.X);
        }

        void homeYButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrinterCommunication.Instance.HomeAxis(PrinterCommunication.Axis.Y);
        }

        void homeZButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrinterCommunication.Instance.HomeAxis(PrinterCommunication.Axis.Z);
        }

        void homeAll_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrinterCommunication.Instance.HomeAxis(PrinterCommunication.Axis.XYZ);
        }

        public override void OnClosing(out bool CancelClose)
        {
            base.OnClosing(out CancelClose);
        }

        void runPrintLeveling_Click(object sender, MouseEventArgs mouseEvent)
        {
            OpenPrintLevelWizard();
        }
    }
}
