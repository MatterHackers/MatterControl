﻿/*
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

    public class DisablablableWidget : GuiWidget
    {
        public GuiWidget disableOverlay;

        public DisablablableWidget()
        {
            HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            VAnchor = Agg.UI.VAnchor.FitToChildren;

            disableOverlay = new GuiWidget(HAnchor.ParentLeftRight, VAnchor.ParentBottomTop);
            disableOverlay.Visible = false;
            base.AddChild(disableOverlay);
        }

        public enum EnableLevel { Disabled, ConfigOnly, Enabled };

        public void SetEnableLevel(EnableLevel enabledLevel)
        {
            disableOverlay.BackgroundColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryAccentColor, 160);
            switch (enabledLevel)
            {
                case EnableLevel.Disabled:
                    disableOverlay.Margin = new BorderDouble(0);
                    disableOverlay.Visible = true;
                    break;

                case EnableLevel.ConfigOnly:
                    disableOverlay.Margin = new BorderDouble(10, 10, 10, 15);
                    disableOverlay.Visible = true;
                    break;

                case EnableLevel.Enabled:
                    disableOverlay.Visible = false;
                    break;
            }
        }

        public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
        {
            if (indexInChildrenList == -1)
            {
                // put it under the disableOverlay
                base.AddChild(childToAdd, Children.Count - 1);
            }
            else
            {
                base.AddChild(childToAdd, indexInChildrenList);
            }
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

        DisablablableWidget extruderTemperatureControlWidget;
        DisablablableWidget bedTemperatureControlWidget;
        DisablablableWidget movementControlsContainer;
        DisablablableWidget fanControlsContainer;
        DisablablableWidget tuningAdjustmentControlsContainer;
        DisablablableWidget terminalCommunicationsContainer;
        DisablablableWidget sdCardManagerContainer;
        DisablablableWidget printLevelContainer;
        DisablablableWidget macroControls;

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
                MainSlidePanel.Instance.ReloadBackPanel();
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

            controlsTopToBottomLayout.Padding = new BorderDouble(3, 0);

            terminalCommunicationsContainer = new DisablablableWidget();
            terminalCommunicationsContainer.AddChild(CreateTerminalControlsContainer());
            controlsTopToBottomLayout.AddChild(terminalCommunicationsContainer);

            AddTemperatureControls(controlsTopToBottomLayout);
            AddMovementControls(controlsTopToBottomLayout);

            printLevelContainer = new DisablablableWidget();
            printLevelContainer.AddChild(CreatePrintLevelingControlsContainer());
            controlsTopToBottomLayout.AddChild(printLevelContainer);

            sdCardManagerContainer = new DisablablableWidget();
            sdCardManagerContainer.AddChild(CreateSdCardManagerContainer());
            if (false)// || ActivePrinterProfile.Instance.ActivePrinter == null || ActivePrinterProfile.Instance.ActivePrinter.GetFeatures().HasSdCard())
            {
                controlsTopToBottomLayout.AddChild(sdCardManagerContainer);
            }

            macroControls = new DisablablableWidget();
            macroControls.AddChild(new MacroControls());
            controlsTopToBottomLayout.AddChild(macroControls);

            PutInFanControls(controlsTopToBottomLayout);
            AddAdjustmentControls(controlsTopToBottomLayout);

            this.AddChild(controlsTopToBottomLayout);
            AddHandlers();
            SetVisibleControls();
        }

        private void PutInFanControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
			GroupBox fanControlsGroupBox = new GroupBox(new LocalizedString("Fan Controls").Translated);

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

            fanControlsContainer = new DisablablableWidget();
            fanControlsContainer.AddChild(fanControlsGroupBox);

            if (ActivePrinterProfile.Instance.ActivePrinter == null
                || ActivePrinterProfile.Instance.ActivePrinter.GetFeatures().HasFan())
            {
                controlsTopToBottomLayout.AddChild(fanControlsContainer);
            }
        }

        private void AddMovementControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
            Button editButton;
			GroupBox movementControlsGroupBox = new GroupBox(textImageButtonFactory.GenerateGroupBoxLableWithEdit(new LocalizedString("Movement Controls").Translated, out editButton));
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

            movementControlsContainer = new DisablablableWidget();
            movementControlsContainer.AddChild(movementControlsGroupBox);
            controlsTopToBottomLayout.AddChild(movementControlsContainer);
        }

        private void AddTemperatureControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
            FlowLayoutWidget temperatureControlContainer = new FlowLayoutWidget();
            temperatureControlContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

            extruderTemperatureControlWidget = new DisablablableWidget();
            extruderTemperatureControlWidget.AddChild(new ExtruderTemperatureControlWidget());
            temperatureControlContainer.AddChild(extruderTemperatureControlWidget);

            bedTemperatureControlWidget = new DisablablableWidget();
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

			TextWidget fanSpeedDescription = new TextWidget(new LocalizedString("Fan Speed:").Translated, textColor: RGBA_Bytes.White);
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
            topLine.BackgroundColor = RGBA_Bytes.White;
            return topLine;
        }

        NumberEdit feedRateValue;
        Slider feedRateRatioSlider;
        Slider extrusionRatioSlider;
        NumberEdit extrusionValue;
        PrintLevelWizardWindow printLevelWizardWindow;

        private void AddAdjustmentControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
			GroupBox adjustmentControlsGroupBox = new GroupBox(new LocalizedString("Tuning Adjustment (while printing)").Translated);
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

						feedRateDescription = new TextWidget(new LocalizedString("Speed Multiplier").Translated);
                        feedRateDescription.TextColor = RGBA_Bytes.White;
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
						feedRateLeftToRight.AddChild(textImageButtonFactory.Generate(new LocalizedString("Set").Translated));
                    }

                    TextWidget extrusionDescription;
                    {
                        extrusionValue = new NumberEdit(1, allowDecimals: true, minValue: minExtrutionRatio, maxValue: maxExtrusionRatio, pixelWidth: 40);

                        FlowLayoutWidget leftToRight = new FlowLayoutWidget();

						extrusionDescription = new TextWidget(new LocalizedString("Extrusion Multiplier").Translated);
                        extrusionDescription.TextColor = RGBA_Bytes.White;
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
						leftToRight.AddChild(textImageButtonFactory.Generate(new LocalizedString("Set").Translated));
                    }

                    feedRateDescription.Width = extrusionDescription.Width;
                    feedRateDescription.MinimumSize = new Vector2(extrusionDescription.Width, feedRateDescription.MinimumSize.y);
                    feedRateLeftToRight.HAnchor = HAnchor.FitToChildren;
                    feedRateLeftToRight.VAnchor = VAnchor.FitToChildren;
                }

                adjustmentControlsGroupBox.AddChild(tuningRatiosLayout);
            }

            tuningAdjustmentControlsContainer = new DisablablableWidget();
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

        private GuiWidget CreatePrintLevelingControlsContainer()
        {
            GroupBox printLevelingControlsContainer;
			printLevelingControlsContainer = new GroupBox(new LocalizedString("Automatic Calibration").Translated);

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

				Button runPrintLevelingButton = textImageButtonFactory.Generate(new LocalizedString("CONFIGURE").Translated);
                runPrintLevelingButton.Margin = new BorderDouble(left:6);
                runPrintLevelingButton.VAnchor = VAnchor.ParentCenter;
                runPrintLevelingButton.Click += new ButtonBase.ButtonEventHandler(runPrintLeveling_Click);

				CheckBox doLevelingCheckBox = new CheckBox(new LocalizedString("Enable Automatic Print Leveling").Translated);
                doLevelingCheckBox.Margin = new BorderDouble(left: 3);
                doLevelingCheckBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                doLevelingCheckBox.VAnchor = VAnchor.ParentCenter;
                doLevelingCheckBox.Checked = ActivePrinterProfile.Instance.DoPrintLeveling;

                buttonBar.AddChild(doLevelingCheckBox);
                buttonBar.AddChild(runPrintLevelingButton);
                doLevelingCheckBox.CheckedStateChanged += (sender, e) =>
                {
                    ActivePrinterProfile.Instance.DoPrintLeveling = doLevelingCheckBox.Checked;
                };
                ActivePrinterProfile.Instance.DoPrintLevelingChanged.RegisterEvent((sender, e) =>
                {
                    doLevelingCheckBox.Checked = ActivePrinterProfile.Instance.DoPrintLeveling;
                    if (doLevelingCheckBox.Checked && ActivePrinterProfile.Instance.ActivePrinter.PrintLevelingProbePositions == null)
                    {
                        //OpenPrintLevelWizard();
                    }

                }, ref unregisterEvents);

                printLevelingControlsContainer.AddChild(buttonBar);
            }

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
			terminalControlsContainer = new GroupBox(new LocalizedString("Printer Communications").Translated);

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

				Button showTerminal = textImageButtonFactory.Generate(new LocalizedString("SHOW TERMINAL").Translated);
                showTerminal.Margin = new BorderDouble(0);
                showTerminal.Click += (sender, e) =>
                {
                    OutputScrollWindow.Show();
                };
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
            this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
            
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
                extruderTemperatureControlWidget.SetEnableLevel(DisablablableWidget.EnableLevel.Disabled);
                bedTemperatureControlWidget.SetEnableLevel(DisablablableWidget.EnableLevel.Disabled);
                movementControlsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Disabled);
                fanControlsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Disabled);
                tuningAdjustmentControlsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Disabled);
                terminalCommunicationsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                printLevelContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Disabled);
                sdCardManagerContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Disabled);
                macroControls.SetEnableLevel(DisablablableWidget.EnableLevel.Disabled);
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
                        extruderTemperatureControlWidget.SetEnableLevel(DisablablableWidget.EnableLevel.ConfigOnly);
                        bedTemperatureControlWidget.SetEnableLevel(DisablablableWidget.EnableLevel.ConfigOnly);
                        movementControlsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.ConfigOnly);
                        fanControlsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Disabled);
                        tuningAdjustmentControlsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Disabled);
                        terminalCommunicationsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                        printLevelContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Disabled);
                        sdCardManagerContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Disabled);
                        macroControls.SetEnableLevel(DisablablableWidget.EnableLevel.ConfigOnly);
                        break;

                    case PrinterCommunication.CommunicationStates.FinishedPrint:
                    case PrinterCommunication.CommunicationStates.Connected:
                        extruderTemperatureControlWidget.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                        bedTemperatureControlWidget.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                        movementControlsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                        fanControlsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                        terminalCommunicationsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                        printLevelContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                        sdCardManagerContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                        macroControls.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                        tuningAdjustmentControlsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Disabled);
                        break;

                    case PrinterCommunication.CommunicationStates.PreparingToPrint:
                    case PrinterCommunication.CommunicationStates.Printing:
                        switch (PrinterCommunication.Instance.PrintingState)
                        {
                            case PrinterCommunication.DetailedPrintingState.HomingAxis:
                            case PrinterCommunication.DetailedPrintingState.HeatingBed:
                            case PrinterCommunication.DetailedPrintingState.HeatingExtruder:
                            case PrinterCommunication.DetailedPrintingState.Printing:
                                extruderTemperatureControlWidget.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                                bedTemperatureControlWidget.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                                movementControlsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.ConfigOnly);
                                fanControlsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                                tuningAdjustmentControlsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                                terminalCommunicationsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                                printLevelContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Disabled);
                                sdCardManagerContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                                macroControls.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                                break;

                            default:
                                throw new NotImplementedException();
                        }
                        break;

                    case PrinterCommunication.CommunicationStates.Paused:
                        extruderTemperatureControlWidget.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                        bedTemperatureControlWidget.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                        movementControlsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                        fanControlsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                        tuningAdjustmentControlsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                        terminalCommunicationsContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                        printLevelContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Disabled);
                        sdCardManagerContainer.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
                        macroControls.SetEnableLevel(DisablablableWidget.EnableLevel.Enabled);
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
            ImageBMPIO.LoadImageData(fileAndPath, helpIconImage);
            ImageWidget homeIconImageWidget = new ImageWidget(helpIconImage);
            homeIconImageWidget.Margin = new BorderDouble(0, 0, 6, 0);
            homeIconImageWidget.OriginRelativeParent += new Vector2(0, 2);
            RGBA_Bytes oldColor = this.textImageButtonFactory.normalFillColor;
            textImageButtonFactory.normalFillColor = new RGBA_Bytes(190, 190, 190);
			homeAllButton = textImageButtonFactory.Generate(new LocalizedString("ALL").Translated);
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

			disableMotors = textImageButtonFactory.Generate(new LocalizedString("UNLOCK").Translated);
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
            this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
            SetVisibleControls();
            this.Invalidate();
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
