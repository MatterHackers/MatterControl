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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
    public class XYZColors
    {
        public static RGBA_Bytes xColor = new RGBA_Bytes(180, 180, 180);
        public static RGBA_Bytes yColor = new RGBA_Bytes(255, 255, 255);
        public static RGBA_Bytes zColor = new RGBA_Bytes(255, 255, 255);
        public static RGBA_Bytes eColor = new RGBA_Bytes(180, 180, 180);
        public XYZColors()
        {
        }
    }

    public class ManualPrinterControls : GuiWidget
    {
        readonly double minExtrutionRatio = .5;
        readonly double maxExtrusionRatio = 3;
        readonly double minFeedRateRatio = .5;
        readonly double maxFeedRateRatio = 2;
        readonly double TallButtonHeight = 25* TextWidget.GlobalPointSizeScaleRatio;

        Button disableMotors;
        Button manualMove;
        Button homeAllButton;
        Button homeXButton;
        Button homeYButton;
        Button homeZButton;

        DisableableWidget extruderTemperatureControlWidget;
        DisableableWidget bedTemperatureControlWidget;
        DisableableWidget movementControlsContainer;
        DisableableWidget fanControlsContainer;
        DisableableWidget eePromControlsContainer;
        DisableableWidget tuningAdjustmentControlsContainer;
        DisableableWidget terminalCommunicationsContainer;
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
            string presets = "x,3000,y,3000,z,315,e0,150"; // stored x,value,y,value,z,value,e1,value,e2,value,e3,value,...
            if (PrinterConnectionAndCommunication.Instance != null && ActivePrinterProfile.Instance.ActivePrinter != null)
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
                ApplicationController.Instance.ReloadAdvancedControlsPanel();
            }
        }

        static public RootedObjectEventHandler AddPluginControls = new RootedObjectEventHandler();

        EditManualMovementSpeedsWindow editManualMovementSettingsWindow;
        public ManualPrinterControls()
        {
            SetDisplayAttributes();

            HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            VAnchor = Agg.UI.VAnchor.FitToChildren;

            FlowLayoutWidget controlsTopToBottomLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
            controlsTopToBottomLayout.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            controlsTopToBottomLayout.VAnchor = Agg.UI.VAnchor.FitToChildren;
            controlsTopToBottomLayout.Name = "ManualPrinterControls.ControlsContainer";            

            controlsTopToBottomLayout.Margin = new BorderDouble(0);

            AddTemperatureControls(controlsTopToBottomLayout);

            FlowLayoutWidget centerControlsContainer = new FlowLayoutWidget();
            centerControlsContainer.HAnchor = HAnchor.ParentLeftRight;

            AddMovementControls(centerControlsContainer);

            // put in the terminal communications
            {
                FlowLayoutWidget rightColumnContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                AddFanControls(rightColumnContainer);
                rightColumnContainer.Width = 200* TextWidget.GlobalPointSizeScaleRatio;
                rightColumnContainer.VAnchor |= VAnchor.ParentTop;
                centerControlsContainer.AddChild(rightColumnContainer);
            }

            controlsTopToBottomLayout.AddChild(centerControlsContainer);

            macroControls = new DisableableWidget();
            macroControls.AddChild(new MacroControls());
            controlsTopToBottomLayout.AddChild(macroControls);

            AddAdjustmentControls(controlsTopToBottomLayout);

            this.AddChild(controlsTopToBottomLayout);
            AddHandlers();
            SetVisibleControls();

            if (!pluginsQueuedToAdd)
            {
                UiThread.RunOnIdle(AddPlugins);
                pluginsQueuedToAdd = true;
            }
        }

        static bool pluginsQueuedToAdd = false;
        public void AddPlugins(object state)
        {
            AddPluginControls.CallEvents(this, null);
            pluginsQueuedToAdd = false;
        }

        private void AddFanControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
			AltGroupBox fanControlsGroupBox = new AltGroupBox(LocalizedString.Get("Fan Controls"));

            fanControlsGroupBox.Margin = new BorderDouble(0);
            fanControlsGroupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            fanControlsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            fanControlsGroupBox.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
            fanControlsGroupBox.VAnchor = Agg.UI.VAnchor.FitToChildren;

            {
                FlowLayoutWidget fanControlsLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
                fanControlsLayout.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                fanControlsLayout.VAnchor = Agg.UI.VAnchor.FitToChildren;
                fanControlsLayout.Padding = new BorderDouble(3, 5, 3, 0)* TextWidget.GlobalPointSizeScaleRatio;
                {
                    fanControlsLayout.AddChild(CreateFanControls());
                }

                fanControlsGroupBox.AddChild(fanControlsLayout);
            }

            fanControlsContainer = new DisableableWidget();
            fanControlsContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            fanControlsContainer.AddChild(fanControlsGroupBox);

            if (ActiveSliceSettings.Instance.HasFan())
            {
                controlsTopToBottomLayout.AddChild(fanControlsContainer);
            }
        }

        private void AddEePromControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
            AltGroupBox eePromControlsGroupBox = new AltGroupBox(LocalizedString.Get("EEProm Settings"));

			eePromControlsGroupBox.Margin = new BorderDouble(0);
            eePromControlsGroupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            eePromControlsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            eePromControlsGroupBox.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
            eePromControlsGroupBox.VAnchor = Agg.UI.VAnchor.FitToChildren;
            eePromControlsGroupBox.Height = 68* TextWidget.GlobalPointSizeScaleRatio;

            {
				FlowLayoutWidget eePromControlsLayout = new FlowLayoutWidget();
				eePromControlsLayout.HAnchor |= HAnchor.ParentLeftRight;
				eePromControlsLayout.VAnchor |= Agg.UI.VAnchor.ParentCenter;
                eePromControlsLayout.Margin = new BorderDouble(3, 0, 3, 6)* TextWidget.GlobalPointSizeScaleRatio;
				eePromControlsLayout.Padding = new BorderDouble(0);
                {
					Agg.Image.ImageBuffer eePromImage = new Agg.Image.ImageBuffer();
					ImageIO.LoadImageData(Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "Icons", "PrintStatusControls", "leveling-24x24.png"), eePromImage);
					ImageWidget eePromIcon = new ImageWidget(eePromImage);
                    eePromIcon.Margin = new BorderDouble (right: 6)* TextWidget.GlobalPointSizeScaleRatio;

                    Button openEePromWindow = textImageButtonFactory.Generate("Configure".Localize().ToUpper());
                    openEePromWindow.Click += (sender, e) =>
                    {
#if false // This is to force the creation of the repetier window for testing when we don't have repetier firmware.
                        new MatterHackers.MatterControl.EeProm.EePromRepetierWidget();
#else
						switch(PrinterConnectionAndCommunication.Instance.FirmwareType)
                        {
                            case PrinterConnectionAndCommunication.FirmwareTypes.Repetier:
                                new MatterHackers.MatterControl.EeProm.EePromRepetierWidget();
                            break;

                            case PrinterConnectionAndCommunication.FirmwareTypes.Marlin:
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
			AltGroupBox movementControlsGroupBox = new AltGroupBox(textImageButtonFactory.GenerateGroupBoxLabelWithEdit("Movement Controls".Localize(), out editButton));
            editButton.Click += (sender, e) =>
            {
                if (editManualMovementSettingsWindow == null)
                {
                    editManualMovementSettingsWindow = new EditManualMovementSpeedsWindow("Movement Speeds".Localize(), GetMovementSpeedsString(), SetMovementSpeeds);
                    editManualMovementSettingsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { editManualMovementSettingsWindow = null; };
                }
                else
                {
                    editManualMovementSettingsWindow.BringToFront();
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
                manualControlsLayout.Padding = new BorderDouble(3, 5, 3, 0)* TextWidget.GlobalPointSizeScaleRatio;
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
            temperatureControlContainer.Margin = new BorderDouble (top: 10)* TextWidget.GlobalPointSizeScaleRatio;

            extruderTemperatureControlWidget = new DisableableWidget();
            extruderTemperatureControlWidget.AddChild(new ExtruderTemperatureControlWidget());
            temperatureControlContainer.AddChild(extruderTemperatureControlWidget);

            bedTemperatureControlWidget = new DisableableWidget();
            bedTemperatureControlWidget.AddChild(new BedTemperatureControlWidget());

            if (ActiveSliceSettings.Instance.HasHeatedBed())
            {
                temperatureControlContainer.AddChild(bedTemperatureControlWidget);
            }

            controlsTopToBottomLayout.AddChild(temperatureControlContainer);
        }

        EditableNumberDisplay fanSpeedDisplay;
        private GuiWidget CreateFanControls()
        {
            PrinterConnectionAndCommunication.Instance.FanSpeedSet.RegisterEvent(FanSpeedChanged_Event, ref unregisterEvents);

            FlowLayoutWidget leftToRight = new FlowLayoutWidget();
            leftToRight.Padding = new BorderDouble(3, 0, 0, 5)* TextWidget.GlobalPointSizeScaleRatio;

			TextWidget fanSpeedDescription = new TextWidget(LocalizedString.Get("Fan Speed:"), pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor);
            fanSpeedDescription.VAnchor = Agg.UI.VAnchor.ParentCenter;
            leftToRight.AddChild(fanSpeedDescription);

            fanSpeedDisplay = new EditableNumberDisplay(textImageButtonFactory, PrinterConnectionAndCommunication.Instance.FanSpeed0To255.ToString(), "100");
            fanSpeedDisplay.EditComplete += (sender, e) =>
            {
                PrinterConnectionAndCommunication.Instance.FanSpeed0To255 = (int)(fanSpeedDisplay.GetValue() * 255.5 / 100);
            };

            leftToRight.AddChild(fanSpeedDisplay);

            TextWidget fanSpeedPercent = new TextWidget("%", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor);
            fanSpeedPercent.VAnchor = Agg.UI.VAnchor.ParentCenter;
            leftToRight.AddChild(fanSpeedPercent);

            return leftToRight;
        }

        void FanSpeedChanged_Event(object sender, EventArgs e)
        {
            int printerFanSpeed = PrinterConnectionAndCommunication.Instance.FanSpeed0To255;

            fanSpeedDisplay.SetDisplayString(((int)(printerFanSpeed * 100.5 / 255)).ToString());
        }

        private static GuiWidget CreateSeparatorLine()
        {
            GuiWidget topLine = new GuiWidget(10* TextWidget.GlobalPointSizeScaleRatio, 1* TextWidget.GlobalPointSizeScaleRatio);
            topLine.Margin = new BorderDouble(0, 5)* TextWidget.GlobalPointSizeScaleRatio;
            topLine.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            topLine.BackgroundColor = ActiveTheme.Instance.PrimaryTextColor;
            return topLine;
        }

        NumberEdit feedRateValue;
        SolidSlider feedRateRatioSlider;
        SolidSlider extrusionRatioSlider;
        NumberEdit extrusionValue;

        private void AddAdjustmentControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
			AltGroupBox adjustmentControlsGroupBox = new AltGroupBox(LocalizedString.Get("Tuning Adjustment"));
            adjustmentControlsGroupBox.Margin = new BorderDouble(0);
            adjustmentControlsGroupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            adjustmentControlsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            adjustmentControlsGroupBox.HAnchor = Agg.UI.HAnchor.ParentLeftRight;            
            
            {
                FlowLayoutWidget tuningRatiosLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
                tuningRatiosLayout.Margin = new BorderDouble(0, 0, 0, 0)* TextWidget.GlobalPointSizeScaleRatio;
                tuningRatiosLayout.HAnchor = HAnchor.ParentLeftRight;
                tuningRatiosLayout.Padding = new BorderDouble(3, 0, 3, 0)* TextWidget.GlobalPointSizeScaleRatio;

                double sliderWidth;
                if (ActiveTheme.Instance.DisplayMode == ActiveTheme.ApplicationDisplayType.Touchscreen)
                {
                    sliderWidth = 20;
                }
                else
                {
                    sliderWidth = 10;
                }

                TextWidget subheader = new TextWidget("Fine-tune adjustment while actively printing", pointSize: 8, textColor: ActiveTheme.Instance.PrimaryTextColor);
                subheader.Margin = new BorderDouble(bottom:6);
                tuningRatiosLayout.AddChild(subheader);
                TextWidget feedRateDescription;
                {
                    
                    FlowLayoutWidget feedRateLeftToRight;
                    {
                        feedRateValue = new NumberEdit(0, allowDecimals: true, minValue: minFeedRateRatio, maxValue: maxFeedRateRatio, pixelWidth: 40* TextWidget.GlobalPointSizeScaleRatio);
						feedRateValue.Value = ((int)(PrinterConnectionAndCommunication.Instance.FeedRateRatio * 100 + .5)) / 100.0;
					
                        feedRateLeftToRight = new FlowLayoutWidget();
                        feedRateLeftToRight.HAnchor = HAnchor.ParentLeftRight;

						feedRateDescription = new TextWidget(LocalizedString.Get("Speed Multiplier"));
                        feedRateDescription.MinimumSize = new Vector2(140, 0) * TextWidget.GlobalPointSizeScaleRatio;
                        feedRateDescription.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                        feedRateDescription.VAnchor = VAnchor.ParentCenter;
                        feedRateLeftToRight.AddChild(feedRateDescription);
                        feedRateRatioSlider = new SolidSlider(new Vector2(), sliderWidth, minFeedRateRatio, maxFeedRateRatio);
                        feedRateRatioSlider.Margin = new BorderDouble(5, 0);
						feedRateRatioSlider.Value = PrinterConnectionAndCommunication.Instance.FeedRateRatio;
                        feedRateRatioSlider.TotalWidthInPixels = 300;
                        feedRateRatioSlider.View.BackgroundColor = new RGBA_Bytes();
                        feedRateRatioSlider.ValueChanged += (sender, e) =>
                        {
							PrinterConnectionAndCommunication.Instance.FeedRateRatio = feedRateRatioSlider.Value;
                        };
                        PrinterConnectionAndCommunication.Instance.FeedRateRatioChanged.RegisterEvent(FeedRateRatioChanged_Event, ref unregisterEvents);
                        feedRateValue.EditComplete += (sender, e) =>
                        {
							feedRateRatioSlider.Value = feedRateValue.Value;
                        };
                        feedRateLeftToRight.AddChild(feedRateRatioSlider);
                        tuningRatiosLayout.AddChild(feedRateLeftToRight);

                        feedRateLeftToRight.AddChild(feedRateValue);
                        feedRateValue.Margin = new BorderDouble(0, 0, 5, 0);
                        feedRateValue.VAnchor = VAnchor.ParentCenter;
                        textImageButtonFactory.FixedHeight = (int)feedRateValue.Height + 1;
                        textImageButtonFactory.borderWidth = 1;
                        textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor,200);
                        textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

                        Button setFeedRateButton = textImageButtonFactory.Generate(LocalizedString.Get("Set"));
                        setFeedRateButton.VAnchor = VAnchor.ParentCenter;

						feedRateLeftToRight.AddChild(setFeedRateButton);
                    }

                    TextWidget extrusionDescription;
                    {
                        extrusionValue = new NumberEdit(0, allowDecimals: true, minValue: minExtrutionRatio, maxValue: maxExtrusionRatio, pixelWidth: 40* TextWidget.GlobalPointSizeScaleRatio);
						extrusionValue.Value = ((int)(PrinterConnectionAndCommunication.Instance.ExtrusionRatio * 100 + .5)) / 100.0;

                        FlowLayoutWidget leftToRight = new FlowLayoutWidget();
                        leftToRight.HAnchor = HAnchor.ParentLeftRight;
                        leftToRight.Margin = new BorderDouble(top: 10)* TextWidget.GlobalPointSizeScaleRatio;

						extrusionDescription = new TextWidget(LocalizedString.Get("Extrusion Multiplier"));
                        extrusionDescription.MinimumSize = new Vector2(140, 0) * TextWidget.GlobalPointSizeScaleRatio;
                        extrusionDescription.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                        extrusionDescription.VAnchor = VAnchor.ParentCenter;
                        leftToRight.AddChild(extrusionDescription);
                        extrusionRatioSlider = new SolidSlider(new Vector2(), sliderWidth, minExtrutionRatio, maxExtrusionRatio,Orientation.Horizontal);
                        extrusionRatioSlider.TotalWidthInPixels = 300;
                        extrusionRatioSlider.Margin = new BorderDouble(5, 0);
                        extrusionRatioSlider.Value = PrinterConnectionAndCommunication.Instance.ExtrusionRatio;
                        extrusionRatioSlider.View.BackgroundColor = new RGBA_Bytes();
                        extrusionRatioSlider.ValueChanged += (sender, e) =>
                        {
							PrinterConnectionAndCommunication.Instance.ExtrusionRatio = extrusionRatioSlider.Value;
                        };
                        PrinterConnectionAndCommunication.Instance.ExtrusionRatioChanged.RegisterEvent(ExtrusionRatioChanged_Event, ref unregisterEvents);
                        extrusionValue.EditComplete += (sender, e) =>
                        {
                            extrusionRatioSlider.Value = extrusionValue.Value;
                        };
                        leftToRight.AddChild(extrusionRatioSlider);
                        tuningRatiosLayout.AddChild(leftToRight);
                        leftToRight.AddChild(extrusionValue);
                        extrusionValue.Margin = new BorderDouble(0, 0, 5, 0);
                        extrusionValue.VAnchor = VAnchor.ParentCenter;
                        textImageButtonFactory.FixedHeight = (int)extrusionValue.Height + 1;
                        Button setExtrusionButton = textImageButtonFactory.Generate(LocalizedString.Get("Set"));
                        setExtrusionButton.VAnchor = VAnchor.ParentCenter;
						leftToRight.AddChild(setExtrusionButton);
                    }
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
            extrusionRatioSlider.Value = PrinterConnectionAndCommunication.Instance.ExtrusionRatio;
            extrusionValue.Value = ((int)(PrinterConnectionAndCommunication.Instance.ExtrusionRatio * 100 + .5)) / 100.0;
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
            feedRateRatioSlider.Value = PrinterConnectionAndCommunication.Instance.FeedRateRatio;
			feedRateValue.Value = ((int)(PrinterConnectionAndCommunication.Instance.FeedRateRatio * 100 + .5)) / 100.0;
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
				
                macroControls.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
            }
            else // we at least have a printer selected
            {
                switch (PrinterConnectionAndCommunication.Instance.CommunicationState)
                {
                    case PrinterConnectionAndCommunication.CommunicationStates.Disconnecting:
                    case PrinterConnectionAndCommunication.CommunicationStates.ConnectionLost:
                    case PrinterConnectionAndCommunication.CommunicationStates.Disconnected:
                    case PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect:
                    case PrinterConnectionAndCommunication.CommunicationStates.FailedToConnect:
                        extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
                        bedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
                        movementControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
                        fanControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        tuningAdjustmentControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);                        
                        macroControls.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
                        break;

                    case PrinterConnectionAndCommunication.CommunicationStates.FinishedPrint:
                    case PrinterConnectionAndCommunication.CommunicationStates.Connected:
                        extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        bedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        movementControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        fanControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);                        
                        macroControls.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        tuningAdjustmentControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        break;

                    case PrinterConnectionAndCommunication.CommunicationStates.PrintingToSd:
                        extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        bedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        movementControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
                        fanControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        macroControls.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
                        tuningAdjustmentControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        break;

                    case PrinterConnectionAndCommunication.CommunicationStates.PrintingFromSd:
                        extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        bedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        movementControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
                        fanControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        macroControls.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
                        tuningAdjustmentControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        break;

                    case PrinterConnectionAndCommunication.CommunicationStates.PreparingToPrint:
                    case PrinterConnectionAndCommunication.CommunicationStates.PreparingToPrintToSd:
                    case PrinterConnectionAndCommunication.CommunicationStates.Printing:
                        switch (PrinterConnectionAndCommunication.Instance.PrintingState)
                        {
                            case PrinterConnectionAndCommunication.DetailedPrintingState.HomingAxis:
                            case PrinterConnectionAndCommunication.DetailedPrintingState.HeatingBed:
                            case PrinterConnectionAndCommunication.DetailedPrintingState.HeatingExtruder:
                            case PrinterConnectionAndCommunication.DetailedPrintingState.Printing:
                                extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                                bedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                                movementControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
                                fanControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                                tuningAdjustmentControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);                                
                                macroControls.SetEnableLevel(DisableableWidget.EnableLevel.ConfigOnly);
                                break;

                            default:
                                throw new NotImplementedException();
                        }
                        break;

                    case PrinterConnectionAndCommunication.CommunicationStates.Paused:
                        extruderTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        bedTemperatureControlWidget.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        movementControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        fanControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        tuningAdjustmentControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);                        
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
            homeButtonBar.Margin = new BorderDouble(3, 0, 3, 6)* TextWidget.GlobalPointSizeScaleRatio;
            homeButtonBar.Padding = new BorderDouble(0);

            textImageButtonFactory.borderWidth = 1;
            textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
            textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

            string homeIconFile = "icon_home_white_24x24.png";
            string fileAndPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "Icons", homeIconFile);
            ImageBuffer helpIconImage = new ImageBuffer();
            ImageIO.LoadImageData(fileAndPath, helpIconImage);
            ImageWidget homeIconImageWidget = new ImageWidget(helpIconImage);
            homeIconImageWidget.Margin = new BorderDouble(0, 0, 6, 0)* TextWidget.GlobalPointSizeScaleRatio;
            homeIconImageWidget.OriginRelativeParent += new Vector2(0, 2)* TextWidget.GlobalPointSizeScaleRatio;
            RGBA_Bytes oldColor = this.textImageButtonFactory.normalFillColor;
            textImageButtonFactory.normalFillColor = new RGBA_Bytes(180, 180, 180);
			homeAllButton = textImageButtonFactory.Generate(LocalizedString.Get("ALL"));
            this.textImageButtonFactory.normalFillColor = oldColor;
            homeAllButton.Margin = new BorderDouble(0, 0, 6, 0)* TextWidget.GlobalPointSizeScaleRatio;
            homeAllButton.Click += new ButtonBase.ButtonEventHandler(homeAll_Click);

            textImageButtonFactory.FixedWidth = (int)homeAllButton.Width;
            homeXButton = textImageButtonFactory.Generate("X", centerText: true);
            homeXButton.Margin = new BorderDouble(0, 0, 6, 0)* TextWidget.GlobalPointSizeScaleRatio;
            homeXButton.Click += new ButtonBase.ButtonEventHandler(homeXButton_Click);

            homeYButton = textImageButtonFactory.Generate("Y", centerText: true);
            homeYButton.Margin = new BorderDouble(0, 0, 6, 0)* TextWidget.GlobalPointSizeScaleRatio;
            homeYButton.Click += new ButtonBase.ButtonEventHandler(homeYButton_Click);

            homeZButton = textImageButtonFactory.Generate("Z", centerText: true);
            homeZButton.Margin = new BorderDouble(0, 0, 6, 0)* TextWidget.GlobalPointSizeScaleRatio;
            homeZButton.Click += new ButtonBase.ButtonEventHandler(homeZButton_Click);

            textImageButtonFactory.normalFillColor = RGBA_Bytes.White;
            textImageButtonFactory.FixedWidth = 0;

            GuiWidget spacer = new GuiWidget();
            spacer.HAnchor = HAnchor.ParentLeftRight;

			disableMotors = textImageButtonFactory.Generate("Release".Localize().ToUpper());
            disableMotors.Margin = new BorderDouble(0);
            disableMotors.Click += new ButtonBase.ButtonEventHandler(disableMotors_Click);

            GuiWidget spacerReleaseShow = new GuiWidget(10* TextWidget.GlobalPointSizeScaleRatio, 0);

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
            manualMoveBar.Margin = new BorderDouble(3, 0, 3, 6)* TextWidget.GlobalPointSizeScaleRatio;
            manualMoveBar.Padding = new BorderDouble(0);

            TextWidget xMoveLabel = new TextWidget("X:");
            xMoveLabel.Margin = new BorderDouble(0, 0, 6, 0)* TextWidget.GlobalPointSizeScaleRatio;
            xMoveLabel.VAnchor = VAnchor.ParentCenter;

            MHTextEditWidget xMoveEdit = new MHTextEditWidget("0");
            xMoveEdit.Margin = new BorderDouble(0, 0, 6, 0)* TextWidget.GlobalPointSizeScaleRatio;
            xMoveEdit.VAnchor = VAnchor.ParentCenter;

            TextWidget yMoveLabel = new TextWidget("Y:");
            yMoveLabel.Margin = new BorderDouble(0, 0, 6, 0)* TextWidget.GlobalPointSizeScaleRatio;
            yMoveLabel.VAnchor = VAnchor.ParentCenter;

            MHTextEditWidget yMoveEdit = new MHTextEditWidget("0");
            yMoveEdit.Margin = new BorderDouble(0, 0, 6, 0)* TextWidget.GlobalPointSizeScaleRatio;
            yMoveEdit.VAnchor = VAnchor.ParentCenter;

            TextWidget zMoveLabel = new TextWidget("Z:");
            zMoveLabel.Margin = new BorderDouble(0, 0, 6, 0)* TextWidget.GlobalPointSizeScaleRatio;
            zMoveLabel.VAnchor = VAnchor.ParentCenter;

            MHTextEditWidget zMoveEdit = new MHTextEditWidget("0");
            zMoveEdit.Margin = new BorderDouble(0, 0, 6, 0)* TextWidget.GlobalPointSizeScaleRatio;
            zMoveEdit.VAnchor = VAnchor.ParentCenter;

            manualMove = textImageButtonFactory.Generate("MOVE TO");
            manualMove.Margin = new BorderDouble(0, 0, 6, 0)* TextWidget.GlobalPointSizeScaleRatio;
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
            PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
        }

        private void onPrinterStatusChanged(object sender, EventArgs e)
        {
            SetVisibleControls();
			UiThread.RunOnIdle(invalidateWidget);
            
        }
			
		private void invalidateWidget(object state)
		{
			this.Invalidate();
		}

        void disableMotors_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrinterConnectionAndCommunication.Instance.ReleaseMotors();
        }

        void homeXButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrinterConnectionAndCommunication.Instance.HomeAxis(PrinterConnectionAndCommunication.Axis.X);
        }

        void homeYButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrinterConnectionAndCommunication.Instance.HomeAxis(PrinterConnectionAndCommunication.Axis.Y);
        }

        void homeZButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrinterConnectionAndCommunication.Instance.HomeAxis(PrinterConnectionAndCommunication.Axis.Z);
        }

        void homeAll_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrinterConnectionAndCommunication.Instance.HomeAxis(PrinterConnectionAndCommunication.Axis.XYZ);
        }
    }
}
