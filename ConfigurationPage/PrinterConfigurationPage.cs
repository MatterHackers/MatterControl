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

            HardwareSettingsWidget hardwareGroupbox = new HardwareSettingsWidget();
            mainLayoutContainer.AddChild(hardwareGroupbox);

            CloudSettingsWidget cloudGroupbox = new CloudSettingsWidget();
            mainLayoutContainer.AddChild(cloudGroupbox);

            ApplicationSettingsWidget applicationGroupbox = new ApplicationSettingsWidget();
            mainLayoutContainer.AddChild(applicationGroupbox);

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

        static EePromMarlinWidget openEePromMarlinWidget = null;
        static EePromRepetierWidget openEePromRepetierWidget = null;
        string noEepromMappingMessage = "Oops! There is no eeprom mapping for your printer's firmware.".Localize();
        string noEepromMappingTitle = "Warning - No EEProm Mapping".Localize();
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
					Agg.Image.ImageBuffer eePromImage = StaticData.Instance.LoadIcon(Path.Combine("PrintStatusControls", "leveling-24x24.png"));
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
                                    StyledMessageBox.ShowMessageBox(null, noEepromMappingMessage, noEepromMappingTitle, StyledMessageBox.MessageType.OK);
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

        event EventHandler unregisterEvents;
        private void AddHandlers()
        {
            PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
        }

        private void onPrinterStatusChanged(object sender, EventArgs e)
        {
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
