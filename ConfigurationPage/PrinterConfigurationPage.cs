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
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
    public class PrinterConfigurationPage : ScrollableWidget
    {
        public PrinterConfigurationPage()
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
        DisableableWidget printLevelContainer;

        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

        public PrinterConfigurationWidget()
        {
            SetDisplayAttributes();

            HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            VAnchor = Agg.UI.VAnchor.FitToChildren;

            FlowLayoutWidget mainLayoutContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            mainLayoutContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            mainLayoutContainer.VAnchor = Agg.UI.VAnchor.FitToChildren;
            mainLayoutContainer.Padding = new BorderDouble(3, 0, 3, 10);

            FlowLayoutWidget terminalControls = new FlowLayoutWidget();
            terminalControls.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            terminalControls.Padding = new BorderDouble(right: 15);

            FlowLayoutWidget settingsControls = new FlowLayoutWidget();
            settingsControls.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

            AddThemeControls(settingsControls);
            AddLanguageControls(settingsControls);            

			//FlowLayoutWidget releaseControls = new FlowLayoutWidget();
			//releaseControls.Margin = new BorderDouble (right: 10);
			//releaseControls.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

            
            AddTerminalControls(terminalControls);
            AddEePromControls(terminalControls);
            AddReleaseOptions(terminalControls);
            
            mainLayoutContainer.AddChild(terminalControls);
            AddPrintLevelingControls(mainLayoutContainer);
            mainLayoutContainer.AddChild(settingsControls);
			//mainLayoutContainer.AddChild(releaseControls);

            AddChild(mainLayoutContainer);

            AddHandlers();
            SetVisibleControls();
        }

        private void AddThemeControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
            DisableableWidget container = new DisableableWidget();   
            
            GroupBox themeControlsGroupBox = new GroupBox(LocalizedString.Get("Theme Settings"));
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
            
            GroupBox languageControlsGroupBox = new GroupBox(LocalizedString.Get("Language Settings"));
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
			GroupBox releaseOptionsGroupBox = new GroupBox(LocalizedString.Get("Update Feed"));
            
            releaseOptionsGroupBox.Margin = new BorderDouble(0);
            releaseOptionsGroupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            releaseOptionsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            releaseOptionsGroupBox.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
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

        string noEepromMappingMessage = "Oops! There is no eeprom mapping for your printer's firmware.".Localize();
        string noEepromMappingTitle = "Warning no eeprom mapping".Localize();
        string groupBoxTitle = "EEProm Settings".Localize();
        private void AddEePromControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
            GroupBox eePromControlsGroupBox = new GroupBox(groupBoxTitle);
            
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
			
        private static GuiWidget CreateSeparatorLine()
        {
            GuiWidget topLine = new GuiWidget(10, 1);
            topLine.Margin = new BorderDouble(0, 5);
            topLine.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            topLine.BackgroundColor = RGBA_Bytes.White;
            return topLine;
        }

        SystemWindow printLevelWizardWindow;        

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }

            base.OnClosed(e);
        }

		TextWidget printLevelingStatusLabel;
        private void AddPrintLevelingControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
            printLevelContainer = new DisableableWidget();
            printLevelContainer.AddChild(CreatePrintLevelingControlsContainer());
            controlsTopToBottomLayout.AddChild(printLevelContainer);
        }

        EditLevelingSettingsWindow editLevelingSettingsWindow;
        private GuiWidget CreatePrintLevelingControlsContainer()
        {
            Button editButton;
            GroupBox printLevelingControlsContainer = new GroupBox(textImageButtonFactory.GenerateGroupBoxLabelWithEdit(LocalizedString.Get("Automatic Calibration"), out editButton));
            editButton.Click += (sender, e) =>
            {
                if (editLevelingSettingsWindow == null)
                {
                    editLevelingSettingsWindow = new EditLevelingSettingsWindow();
                    editLevelingSettingsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { editLevelingSettingsWindow = null; };
                }
                else
                {
                    editLevelingSettingsWindow.BringToFront();
                }
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
                    OpenPrintLevelWizard();
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

        public void OpenPrintLevelWizard()
        {
            if (printLevelWizardWindow == null)
            {
                printLevelWizardWindow = LevelWizardBase.CreateAndShowWizard(LevelWizardBase.RuningState.UserRequestedCalibration);
                printLevelWizardWindow.Closed += (sender, e) =>
                {
                    printLevelWizardWindow = null;
                };
            }
            else 
            {
                printLevelWizardWindow.BringToFront();
            }
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
                eePromControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                terminalCommunicationsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                printLevelContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
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
                        eePromControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        printLevelContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        terminalCommunicationsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        break;

                    case PrinterConnectionAndCommunication.CommunicationStates.FinishedPrint:
                    case PrinterConnectionAndCommunication.CommunicationStates.Connected:
                        eePromControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        printLevelContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        terminalCommunicationsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        break;

                    case PrinterConnectionAndCommunication.CommunicationStates.PrintingFromSd:
                        eePromControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        printLevelContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
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
                                printLevelContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                                terminalCommunicationsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                                break;

                            default:
                                throw new NotImplementedException();
                        }
                        break;

                    case PrinterConnectionAndCommunication.CommunicationStates.Paused:
                        eePromControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        printLevelContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
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
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(onThemeChanged, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
        }

        private void onPrinterStatusChanged(object sender, EventArgs e)
        {
            SetVisibleControls();
            this.Invalidate();
        }

        private void onThemeChanged(object sender, EventArgs e)
		{	
			//this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
			// SetVisibleControls();
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
                printLevelingStatusLabel.Text = LocalizedString.Get("Automatic Print Leveling (enabled)");
			}
			else
			{
                printLevelingStatusLabel.Text = LocalizedString.Get("Automatic Print Leveling (disabled)");
			}
		}

        public override void OnClosing(out bool CancelClose)
        {
            base.OnClosing(out CancelClose);
        }
    }
}
