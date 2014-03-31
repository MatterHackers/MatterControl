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
using System.Reflection;
using System.IO.Ports;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Image;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.Localizations;
using MatterHackers.MatterControl;

namespace MatterHackers.MatterControl
{
    public class ConfigurationPage : ScrollableWidget
    {
        public ConfigurationPage()
            : base(true)
        {
            ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
            AnchorAll();
            AddChild(new ConfigurationWidget());
        }
    }
    
    public class ConfigurationWidget : GuiWidget
    {
        readonly int TallButtonHeight = 25;

		Button enablePrintLevelingButton;
		Button disablePrintLevelingButton;

        DisableableWidget eePromControlsContainer;
        DisableableWidget printLevelContainer;

        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

        public ConfigurationWidget()
        {
            SetDisplayAttributes();

            HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            VAnchor = Agg.UI.VAnchor.FitToChildren;

            FlowLayoutWidget mainLayoutContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            mainLayoutContainer.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            mainLayoutContainer.VAnchor = Agg.UI.VAnchor.FitToChildren;
            mainLayoutContainer.Padding = new BorderDouble(3, 0, 3, 10);
            
            AddEePromControls(mainLayoutContainer);
            AddPrintLevelingControls(mainLayoutContainer);

            FlowLayoutWidget settingsControls = new FlowLayoutWidget();
            settingsControls.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

            AddThemeControls(settingsControls);
            AddLanguageControls(settingsControls);            

            mainLayoutContainer.AddChild(settingsControls);

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

            ThemeColorSelectorWidget themeSelector = new ThemeColorSelectorWidget();
            themeControlsGroupBox.AddChild(themeSelector);

            container.AddChild(themeControlsGroupBox);

            controlsTopToBottomLayout.AddChild(container);
        }

        Button restartButton;
        Dictionary<string, string> languageDict;

        private void AddLanguageControls(FlowLayoutWidget controlsTopToBottomLayout)
        {
            CreateLanguageDict();
            
            DisableableWidget container = new DisableableWidget();
            
            
            GroupBox languageControlsGroupBox = new GroupBox(LocalizedString.Get("Language Settings"));
            languageControlsGroupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            languageControlsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            languageControlsGroupBox.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            languageControlsGroupBox.VAnchor = Agg.UI.VAnchor.FitToChildren;
            languageControlsGroupBox.Height = 78;


            FlowLayoutWidget controlsContainer = new FlowLayoutWidget();
            controlsContainer.HAnchor = HAnchor.ParentLeftRight;

            string languageCode = UserSettings.Instance.get("Language");
            string languageVerbose = "Default";

            foreach(KeyValuePair<string, string> entry in languageDict)
            {
                if (languageCode == entry.Value)
                {
                    languageVerbose = entry.Key;
                }
            }

            LanguageSelector languageSelector = new LanguageSelector(languageVerbose);
            foreach (KeyValuePair<string, string> entry in languageDict)
            {
                languageSelector.AddItem(entry.Key,entry.Value);
            }

            languageSelector.Margin = new BorderDouble(0);
            languageSelector.SelectionChanged += new EventHandler(LanguageDropList_SelectionChanged);


            restartButton = textImageButtonFactory.Generate("Restart");
            restartButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
            restartButton.Visible = false;
            restartButton.Click += (sender, e) =>
            {
                RestartApplication();
            };

            controlsContainer.AddChild(languageSelector);
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
                MatterControlApplication app = (MatterControlApplication)this.Parent.Parent.Parent.Parent.Parent.Parent.Parent.Parent.Parent;
                app.RestartOnClose = true;
                app.Close();
            });
        }

        private void CreateLanguageDict()
        {
            languageDict = new Dictionary<string, string>();
            languageDict["Default"] = "EN";
            languageDict["English"] = "EN";
            languageDict["Español"] = "ES";
            languageDict["Français"] = "FR";
            languageDict["Deutsch"] = "DE";
        }

        private void LanguageDropList_SelectionChanged(object sender, EventArgs e)
        {
            string languageCode = ((DropDownList)sender).SelectedValue;
            if (languageCode != UserSettings.Instance.get("Language"))
            {
                UserSettings.Instance.set("Language", languageCode);
                restartButton.Visible = true;
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
                if (!ActiveTheme.Instance.IsDarkTheme)
                {
                    InvertLightness.DoInvertLightness(levelingImage);
                }
                
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
                printLevelContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
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
                        eePromControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        printLevelContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                        break;

                    case PrinterCommunication.CommunicationStates.FinishedPrint:
                    case PrinterCommunication.CommunicationStates.Connected:
                        eePromControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        printLevelContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        break;

                    case PrinterCommunication.CommunicationStates.PreparingToPrint:
                    case PrinterCommunication.CommunicationStates.Printing:
                        switch (PrinterCommunication.Instance.PrintingState)
                        {
                            case PrinterCommunication.DetailedPrintingState.HomingAxis:
                            case PrinterCommunication.DetailedPrintingState.HeatingBed:
                            case PrinterCommunication.DetailedPrintingState.HeatingExtruder:
                            case PrinterCommunication.DetailedPrintingState.Printing:

                                eePromControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                                printLevelContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
                                break;

                            default:
                                throw new NotImplementedException();
                        }
                        break;

                    case PrinterCommunication.CommunicationStates.Paused:
                        eePromControlsContainer.SetEnableLevel(DisableableWidget.EnableLevel.Enabled);
                        printLevelContainer.SetEnableLevel(DisableableWidget.EnableLevel.Disabled);
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

        void runPrintLeveling_Click(object sender, MouseEventArgs mouseEvent)
        {
            OpenPrintLevelWizard();
        }
    }
}
