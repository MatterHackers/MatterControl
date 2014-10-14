/*
Copyright (c) 2014, Kevin Pope
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
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.EeProm;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
    public class PrinterConfigurationScrollWidget : ScrollableWidget
    {
        public PrinterConfigurationScrollWidget()
            : base(true)
        {
            ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
            AnchorAll();
			AddChild(new PrinterConfigurationWidget());
        }
    }
    
    public class PrinterConfigurationWidget : GuiWidget
    {
        readonly int TallButtonHeight = 25;

		Button enablePrintLevelingButton;
		Button disablePrintLevelingButton;

        DisableableWidget eePromControlsContainer;
        DisableableWidget terminalCommunicationsContainer;        
        DisableableWidget printLevelingContainer;
		

        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		LinkButtonFactory linkButtonFactory = new LinkButtonFactory();

        public PrinterConfigurationWidget()
        {
            SetDisplayAttributes();

            HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            VAnchor = Agg.UI.VAnchor.FitToChildren;

            FlowLayoutWidget mainLayoutContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            mainLayoutContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            mainLayoutContainer.VAnchor = Agg.UI.VAnchor.FitToChildren;
            mainLayoutContainer.Padding = new BorderDouble(top: 10);

            //// setup the terminal controls
            //FlowLayoutWidget terminalControls = new FlowLayoutWidget();
            //terminalControls.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            //terminalControls.Padding = new BorderDouble(0);
            //terminalControls.Margin = new BorderDouble(right: 2);
            //AddTerminalControls(terminalControls);            

            //// add other elements on the same line as terminal controls
            //AddEePromControls(terminalControls);
            //AddReleaseOptions(terminalControls);

            //mainLayoutContainer.AddChild(terminalControls);

            //// put in print leveling controls
            //AddPrintLevelingControls(mainLayoutContainer);

            // put in cloud monitor control
            //AddCloudMonitorControls(mainLayoutContainer);            

            

            HardwareSettingsWidget hardwareGroupbox = new HardwareSettingsWidget();
            mainLayoutContainer.AddChild(hardwareGroupbox);

            CloudSettingsWidget cloudGroupbox = new CloudSettingsWidget();
            mainLayoutContainer.AddChild(cloudGroupbox);

            ApplicationSettingsWidget applicationGroupbox = new ApplicationSettingsWidget();
            mainLayoutContainer.AddChild(applicationGroupbox);

            // put in the theme and language controls
            //FlowLayoutWidget uiSettingsControls = new FlowLayoutWidget();
            //uiSettingsControls.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            //AddThemeControls(uiSettingsControls);
            //AddLanguageControls(uiSettingsControls);
            //mainLayoutContainer.AddChild(uiSettingsControls);

            AddChild(mainLayoutContainer);

            AddHandlers();
            //SetVisibleControls();
        }        

        private void AddThemeControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
            DisableableWidget container = new DisableableWidget();   
            
            AltGroupBox themeControlsGroupBox = new AltGroupBox(LocalizedString.Get("Theme Settings"));
            themeControlsGroupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            themeControlsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            themeControlsGroupBox.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
		    themeControlsGroupBox.VAnchor = Agg.UI.VAnchor.FitToChildren;
            themeControlsGroupBox.Height = 78;   

			FlowLayoutWidget colorSelectorContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			colorSelectorContainer.HAnchor = HAnchor.ParentLeftRight;

			GuiWidget currentColorThemeBorder = new GuiWidget();
			currentColorThemeBorder.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			currentColorThemeBorder.VAnchor = VAnchor.ParentBottomTop;
			currentColorThemeBorder.Margin = new BorderDouble (top: 2, bottom: 2);
			currentColorThemeBorder.Padding = new BorderDouble(4);
			currentColorThemeBorder.BackgroundColor = RGBA_Bytes.White;

			GuiWidget currentColorTheme = new GuiWidget();
			currentColorTheme.HAnchor = HAnchor.ParentLeftRight;
			currentColorTheme.VAnchor = VAnchor.ParentBottomTop;
			currentColorTheme.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

			ThemeColorSelectorWidget themeSelector = new ThemeColorSelectorWidget(colorToChangeTo: currentColorTheme);
			themeSelector.Margin = new BorderDouble(right: 5);

			themeControlsGroupBox.AddChild(colorSelectorContainer);
			colorSelectorContainer.AddChild(themeSelector);
			colorSelectorContainer.AddChild(currentColorThemeBorder);
			currentColorThemeBorder.AddChild(currentColorTheme);
            container.AddChild(themeControlsGroupBox);
            controlsTopToBottomLayout.AddChild(container);
        }

        Button restartButton;

        private void AddLanguageControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
            DisableableWidget container = new DisableableWidget();
            
            AltGroupBox languageControlsGroupBox = new AltGroupBox(LocalizedString.Get("Language Settings"));
            languageControlsGroupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            languageControlsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            languageControlsGroupBox.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            languageControlsGroupBox.VAnchor = Agg.UI.VAnchor.FitToChildren;
            languageControlsGroupBox.Height = 78;

            FlowLayoutWidget controlsContainer = new FlowLayoutWidget();
            controlsContainer.HAnchor = HAnchor.ParentLeftRight;

            LanguageSelector languageSelector = new LanguageSelector();

            languageSelector.Margin = new BorderDouble(0);
            languageSelector.SelectionChanged += new EventHandler(LanguageDropList_SelectionChanged);

            TextWidget experimentalWidget = new TextWidget("Experimental", pointSize:10);
            experimentalWidget.VAnchor = Agg.UI.VAnchor.ParentCenter;
            experimentalWidget.Margin = new BorderDouble(left: 4);
            experimentalWidget.TextColor = ActiveTheme.Instance.SecondaryAccentColor;

            restartButton = textImageButtonFactory.Generate("Restart");
            restartButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
            restartButton.Visible = false;
            restartButton.Click += (sender, e) =>
            {
                RestartApplication();
            };

            controlsContainer.AddChild(languageSelector);
            controlsContainer.AddChild(experimentalWidget);
            controlsContainer.AddChild(new HorizontalSpacer());
            controlsContainer.AddChild(restartButton);

            languageControlsGroupBox.AddChild(controlsContainer);

            container.AddChild(languageControlsGroupBox);

            controlsTopToBottomLayout.AddChild(container);
        }

        private void RestartApplication()
        {
            UiThread.RunOnIdle((state) =>
            {
                //horrible hack - to be replaced
                GuiWidget parent = this;
                while (parent as MatterControlApplication == null)
                {
                    parent = parent.Parent;
                }
                MatterControlApplication app = parent as MatterControlApplication;
                app.RestartOnClose = true;
                app.Close();
            });
        }

        private void LanguageDropList_SelectionChanged(object sender, EventArgs e)
        {
            string languageCode = ((DropDownList)sender).SelectedLabel;
            if (languageCode != UserSettings.Instance.get("Language"))
            {
                UserSettings.Instance.set("Language", languageCode);
                restartButton.Visible = true;
            }
        }
			
		public void AddReleaseOptions(FlowLayoutWidget controlsTopToBottom)
		{
			AltGroupBox releaseOptionsGroupBox = new AltGroupBox(LocalizedString.Get("Update Feed"));
            
            releaseOptionsGroupBox.Margin = new BorderDouble(0);
            releaseOptionsGroupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            releaseOptionsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            releaseOptionsGroupBox.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            releaseOptionsGroupBox.VAnchor = Agg.UI.VAnchor.ParentTop;
            releaseOptionsGroupBox.Height = 68;

			FlowLayoutWidget controlsContainer = new FlowLayoutWidget();
			controlsContainer.HAnchor |= HAnchor.ParentCenter;

            AnchoredDropDownList releaseOptionsDropList = new AnchoredDropDownList("Development");			
            releaseOptionsDropList.Margin = new BorderDouble (0, 3);
                       
			MenuItem releaseOptionsDropDownItem = releaseOptionsDropList.AddItem("Release", "release");
            releaseOptionsDropDownItem.Selected += new EventHandler(FixTabDot);
            
            MenuItem preReleaseDropDownItem = releaseOptionsDropList.AddItem("Pre-Release", "pre-release");
            preReleaseDropDownItem.Selected += new EventHandler(FixTabDot);
			
            MenuItem developmentDropDownItem = releaseOptionsDropList.AddItem("Development", "development");
            developmentDropDownItem.Selected += new EventHandler(FixTabDot);

            releaseOptionsDropList.MinimumSize = new Vector2(releaseOptionsDropList.LocalBounds.Width, releaseOptionsDropList.LocalBounds.Height); 

			List<string> acceptableUpdateFeedTypeValues = new List<string> (){ "release", "pre-release", "development" };
			string currentUpdateFeedType = UserSettings.Instance.get ("UpdateFeedType");

			if (acceptableUpdateFeedTypeValues.IndexOf (currentUpdateFeedType) == -1) 
			{
				UserSettings.Instance.set ("UpdateFeedType", "release");
			}

			releaseOptionsDropList.SelectedValue = UserSettings.Instance.get ("UpdateFeedType");

			releaseOptionsDropList.SelectionChanged += new EventHandler (ReleaseOptionsDropList_SelectionChanged);

			controlsContainer.AddChild(releaseOptionsDropList);
			releaseOptionsGroupBox.AddChild(controlsContainer);
			controlsTopToBottom.AddChild(releaseOptionsGroupBox);
		}

        void FixTabDot(object sender, EventArgs e)
        {
            UpdateControlData.Instance.CheckForUpdateUserRequested();
        }

		private void ReleaseOptionsDropList_SelectionChanged(object sender, EventArgs e)
		{
            string releaseCode = ((AnchoredDropDownList)sender).SelectedValue;
			if(releaseCode != UserSettings.Instance.get("UpdateFeedType"))
			{
                UserSettings.Instance.set("UpdateFeedType", releaseCode);
			}
		}

        private void AddTerminalControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
            AltGroupBox terminalControlsContainer;
            terminalControlsContainer = new AltGroupBox(LocalizedString.Get("Communications"));

            terminalControlsContainer.Margin = new BorderDouble(0);
            terminalControlsContainer.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            terminalControlsContainer.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            terminalControlsContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            terminalControlsContainer.Height = 80;

            OutputScrollWindow.HookupPrinterOutput();

            {
                FlowLayoutWidget buttonBar = new FlowLayoutWidget();
                buttonBar.HAnchor |= HAnchor.ParentCenter;
                buttonBar.VAnchor |= Agg.UI.VAnchor.ParentCenter;
                buttonBar.Margin = new BorderDouble(3, 0, 3, 6);
                buttonBar.Padding = new BorderDouble(0);

                this.textImageButtonFactory.FixedHeight = TallButtonHeight;

                Agg.Image.ImageBuffer terminalImage = new Agg.Image.ImageBuffer();
                ImageIO.LoadImageData(Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "Icons", "PrintStatusControls", "terminal-24x24.png"), terminalImage);
                ImageWidget terminalIcon = new ImageWidget(terminalImage);
                terminalIcon.Margin = new BorderDouble(right: 6);

                Button showTerminal = textImageButtonFactory.Generate("Show Terminal".Localize().ToUpper());
                showTerminal.Margin = new BorderDouble(0);
                showTerminal.Click += (sender, e) =>
                {
                    OutputScrollWindow.Show();
                };

                //buttonBar.AddChild(terminalIcon);
                buttonBar.AddChild(showTerminal);

                terminalControlsContainer.AddChild(buttonBar);
            }

            terminalCommunicationsContainer = new DisableableWidget();
            terminalCommunicationsContainer.AddChild(terminalControlsContainer);

            controlsTopToBottomLayout.AddChild(terminalCommunicationsContainer);
        }       

        private static GuiWidget CreateSeparatorLine()
        {
            GuiWidget topLine = new GuiWidget(10, 1);
            topLine.Margin = new BorderDouble(0, 5);
            topLine.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            topLine.BackgroundColor = RGBA_Bytes.White;
            return topLine;
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }

            base.OnClosed(e);
        }

        
        //private void AddCloudMonitorControls(FlowLayoutWidget controlsTopToBottomLayout)
        //{
        //    cloudMonitorContainer = new DisableableWidget();
        //    cloudMonitorContainer.AddChild(CreateCloudMonitorControls());
        //    controlsTopToBottomLayout.AddChild(cloudMonitorContainer);
        //}
        
        //private GuiWidget CreateCloudMonitorControls()
        //{
        //    AltGroupBox cloudMonitorContainer = new AltGroupBox(LocalizedString.Get("Cloud Services"));
            
        //    cloudMonitorContainer.Margin = new BorderDouble(0);
        //    cloudMonitorContainer.TextColor = ActiveTheme.Instance.PrimaryTextColor;
        //    cloudMonitorContainer.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
        //    cloudMonitorContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
        //    cloudMonitorContainer.Height = 68;

        //    {
        //        FlowLayoutWidget buttonBar = new FlowLayoutWidget();
        //        buttonBar.HAnchor |= HAnchor.ParentLeftRight;
        //        buttonBar.VAnchor |= Agg.UI.VAnchor.ParentCenter;
        //        buttonBar.Margin = new BorderDouble(0, 0, 0, 0);
        //        buttonBar.Padding = new BorderDouble(0);

        //        this.textImageButtonFactory.FixedHeight = TallButtonHeight;               

        //        Agg.Image.ImageBuffer cloudMonitorImage = new Agg.Image.ImageBuffer();
        //        ImageIO.LoadImageData(Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "Icons", "PrintStatusControls", "cloud-24x24.png"), cloudMonitorImage);
        //        if (!ActiveTheme.Instance.IsDarkTheme)
        //        {
        //            InvertLightness.DoInvertLightness(cloudMonitorImage);
        //        }

        //        ImageWidget cloudMonitoringIcon = new ImageWidget(cloudMonitorImage);
        //        cloudMonitoringIcon.Margin = new BorderDouble(right: 6);

        //        enableCloudMonitorButton = textImageButtonFactory.Generate("Enable".Localize().ToUpper());
        //        enableCloudMonitorButton.Margin = new BorderDouble(left: 6);
        //        enableCloudMonitorButton.VAnchor = VAnchor.ParentCenter;
        //        enableCloudMonitorButton.Click += new ButtonBase.ButtonEventHandler(enableCloudMonitor_Click);

        //        disableCloudMonitorButton = textImageButtonFactory.Generate("Disable".Localize().ToUpper());
        //        disableCloudMonitorButton.Margin = new BorderDouble(left: 6);
        //        disableCloudMonitorButton.VAnchor = VAnchor.ParentCenter;
        //        disableCloudMonitorButton.Click += new ButtonBase.ButtonEventHandler(disableCloudMonitor_Click);

        //        cloudMonitorInstructionsLink = linkButtonFactory.Generate("More Info".Localize().ToUpper());
        //        cloudMonitorInstructionsLink.VAnchor = VAnchor.ParentCenter;
        //        cloudMonitorInstructionsLink.Click += new ButtonBase.ButtonEventHandler(goCloudMonitoringInstructionsButton_Click);
        //        cloudMonitorInstructionsLink.Margin = new BorderDouble (left: 6);

        //        goCloudMonitoringWebPageButton = linkButtonFactory.Generate("View Status".Localize().ToUpper());
        //        goCloudMonitoringWebPageButton.VAnchor = VAnchor.ParentCenter;
        //        goCloudMonitoringWebPageButton.Click += new ButtonBase.ButtonEventHandler(goCloudMonitoringWebPageButton_Click);
        //        goCloudMonitoringWebPageButton.Margin = new BorderDouble(left: 6);

        //        cloudMonitorStatusLabel = new TextWidget("");
        //        cloudMonitorStatusLabel.AutoExpandBoundsToText = true;
        //        cloudMonitorStatusLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
        //        cloudMonitorStatusLabel.VAnchor = VAnchor.ParentCenter;

        //        GuiWidget hSpacer = new GuiWidget();
        //        hSpacer.HAnchor = HAnchor.ParentLeftRight;

        //        buttonBar.AddChild(cloudMonitoringIcon);
        //        buttonBar.AddChild(cloudMonitorStatusLabel);
        //        buttonBar.AddChild (cloudMonitorInstructionsLink);
        //        buttonBar.AddChild(goCloudMonitoringWebPageButton);
        //        buttonBar.AddChild(hSpacer);                
        //        buttonBar.AddChild(enableCloudMonitorButton);
        //        buttonBar.AddChild(disableCloudMonitorButton);

        //        cloudMonitorContainer.AddChild(buttonBar);
        //    }
        //    SetCloudButtonVisiblity();
        //    return cloudMonitorContainer;
        //}
			
		

        static EePromMarlinWidget openEePromMarlinWidget = null;
        static EePromRepetierWidget openEePromRepetierWidget = null;
        string noEepromMappingMessage = "Oops! There is no eeprom mapping for your printer's firmware.".Localize();
        string noEepromMappingTitle = "Warning no eeprom mapping".Localize();
        string groupBoxTitle = "EEProm Settings".Localize();
        private void AddEePromControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
            AltGroupBox eePromControlsGroupBox = new AltGroupBox(groupBoxTitle);
            
			eePromControlsGroupBox.Margin = new BorderDouble(0);
            eePromControlsGroupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            eePromControlsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            eePromControlsGroupBox.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            eePromControlsGroupBox.VAnchor = Agg.UI.VAnchor.FitToChildren;
			eePromControlsGroupBox.Height = 68;
            {
				FlowLayoutWidget eePromControlsLayout = new FlowLayoutWidget();
                eePromControlsLayout.HAnchor |= HAnchor.ParentCenter;
				eePromControlsLayout.VAnchor |= Agg.UI.VAnchor.ParentCenter;
				eePromControlsLayout.Margin = new BorderDouble(3, 0, 3, 6);
				eePromControlsLayout.Padding = new BorderDouble(0);
                {
					Agg.Image.ImageBuffer eePromImage = new Agg.Image.ImageBuffer();
					ImageIO.LoadImageData(Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath,"Icons", "PrintStatusControls", "leveling-24x24.png"), eePromImage);
					ImageWidget eePromIcon = new ImageWidget(eePromImage);
					eePromIcon.Margin = new BorderDouble (right: 6);

                    Button openEePromWindow = textImageButtonFactory.Generate("Configure".Localize().ToUpper());
                    openEePromWindow.Click += (sender, e) =>
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
                                UiThread.RunOnIdle((state) =>
                                {
                                    StyledMessageBox.ShowMessageBox(noEepromMappingMessage, noEepromMappingTitle, StyledMessageBox.MessageType.OK);
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

		TextWidget printLevelingStatusLabel;
        
        private void AddPrintLevelingControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
            printLevelingContainer = new DisableableWidget();
            printLevelingContainer.AddChild(CreatePrintLevelingControlsContainer());
            controlsTopToBottomLayout.AddChild(printLevelingContainer);
        }

        EditLevelingSettingsWindow editLevelingSettingsWindow;
        private GuiWidget CreatePrintLevelingControlsContainer()
        {
            Button editButton;
            AltGroupBox printLevelingControlsContainer = new AltGroupBox(textImageButtonFactory.GenerateGroupBoxLabelWithEdit(LocalizedString.Get("Automatic Calibration"), out editButton));
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

                Agg.Image.ImageBuffer levelingImage = new Agg.Image.ImageBuffer();
				ImageIO.LoadImageData(Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath,"Icons", "PrintStatusControls", "leveling-24x24.png"), levelingImage);
                if (!ActiveTheme.Instance.IsDarkTheme)
                {
                    InvertLightness.DoInvertLightness(levelingImage);
                }
                
                ImageWidget levelingIcon = new ImageWidget(levelingImage);
				levelingIcon.Margin = new BorderDouble (right: 6);

                enablePrintLevelingButton = textImageButtonFactory.Generate("Enable".Localize().ToUpper());
				enablePrintLevelingButton.Margin = new BorderDouble(left:6);
				enablePrintLevelingButton.VAnchor = VAnchor.ParentCenter;
				enablePrintLevelingButton.Click += new ButtonBase.ButtonEventHandler(enablePrintLeveling_Click);

                disablePrintLevelingButton = textImageButtonFactory.Generate("Disable".Localize().ToUpper());
				disablePrintLevelingButton.Margin = new BorderDouble(left:6);
				disablePrintLevelingButton.VAnchor = VAnchor.ParentCenter;
				disablePrintLevelingButton.Click += new ButtonBase.ButtonEventHandler(disablePrintLeveling_Click);

				printLevelingStatusLabel = new TextWidget ("");
				printLevelingStatusLabel.AutoExpandBoundsToText = true;
				printLevelingStatusLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				printLevelingStatusLabel.VAnchor = VAnchor.ParentCenter;

				GuiWidget hSpacer = new GuiWidget ();
				hSpacer.HAnchor = HAnchor.ParentLeftRight;

                buttonBar.AddChild(levelingIcon);
				buttonBar.AddChild (printLevelingStatusLabel);
				buttonBar.AddChild (hSpacer);
				buttonBar.AddChild(enablePrintLevelingButton);
				buttonBar.AddChild(disablePrintLevelingButton);
                buttonBar.AddChild(runPrintLevelingButton);
                ActivePrinterProfile.Instance.DoPrintLevelingChanged.RegisterEvent((sender, e) =>
                {
					SetPrintLevelButtonVisiblity();

                }, ref unregisterEvents);

                printLevelingControlsContainer.AddChild(buttonBar);
            }
			SetPrintLevelButtonVisiblity ();
            return printLevelingControlsContainer;
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

			this.linkButtonFactory.fontSize = 11;
        }

        private void SetVisibleControls()
        {
            return;
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
			
        event EventHandler unregisterEvents;
        private void AddHandlers()
        {
            PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
        }

        private void onPrinterStatusChanged(object sender, EventArgs e)
        {
            SetVisibleControls();
            this.Invalidate();
        }



        //public delegate void OpenNotificationFormWindow(object state);
        //public static OpenNotificationFormWindow openPrintNotificationFunction = null;
        //void configureNotificationSettingsButton_Click(object sender, MouseEventArgs mouseEvent)
        //{
        //    if(openPrintNotificationFunction != null)
        //    {
        //        UiThread.RunOnIdle ((state) => 
        //        {
        //                openPrintNotificationFunction(null);
        //        });
        //    }
        //}

        void enablePrintLeveling_Click(object sender, MouseEventArgs mouseEvent)
        {
            ActivePrinterProfile.Instance.DoPrintLeveling = true;
        }

        void disablePrintLeveling_Click(object sender, MouseEventArgs mouseEvent)
        {
            ActivePrinterProfile.Instance.DoPrintLeveling = false;
        }

        //void SetCloudButtonVisiblity()
        //{
        //    bool cloudMontitorEnabled = (PrinterSettings.Instance.get("CloudMonitorEnabled") == "true");
        //    enableCloudMonitorButton.Visible = !cloudMontitorEnabled;
        //    disableCloudMonitorButton.Visible = cloudMontitorEnabled;
        //    goCloudMonitoringWebPageButton.Visible = cloudMontitorEnabled;


        //    if (cloudMontitorEnabled)
        //    {
        //        cloudMonitorStatusLabel.Text = LocalizedString.Get("Cloud Monitoring (enabled)");
        //    }
        //    else
        //    {
        //        cloudMonitorStatusLabel.Text = LocalizedString.Get("Cloud Monitoring (disabled)");
        //    }
        //}

		void SetPrintLevelButtonVisiblity()
		{
			enablePrintLevelingButton.Visible = !ActivePrinterProfile.Instance.DoPrintLeveling;
			disablePrintLevelingButton.Visible = ActivePrinterProfile.Instance.DoPrintLeveling;

			if (ActivePrinterProfile.Instance.DoPrintLeveling) {
                printLevelingStatusLabel.Text = LocalizedString.Get("Automatic Print Leveling (enabled)");
			}
			else
			{
                printLevelingStatusLabel.Text = LocalizedString.Get("Automatic Print Leveling (disabled)");
			}
		}
    }
}
