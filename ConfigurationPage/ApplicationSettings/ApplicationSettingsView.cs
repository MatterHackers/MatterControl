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
using MatterHackers.MatterControl.EeProm;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ConfigurationPage
{
    public class ApplicationSettingsWidget : SettingsViewBase
    {
		Button languageRestartButton;
        Button configureUpdateFeedButton;
		Button displayControlRestartButton;
        
        public ApplicationSettingsWidget()
			: base(LocalizedString.Get("Application Settings"))
        {
            mainContainer.AddChild(GetUpdateControl());
            mainContainer.AddChild(new HorizontalLine(separatorLineColor));
            mainContainer.AddChild(GetLanguageControl());
            mainContainer.AddChild(new HorizontalLine(separatorLineColor));
            GuiWidget sliceEngineControl = GetSliceEngineControl();
            if (sliceEngineControl != null
                &&  ActivePrinterProfile.Instance.ActivePrinter != null)
            {
                mainContainer.AddChild(sliceEngineControl);
                mainContainer.AddChild(new HorizontalLine(separatorLineColor));
            }
            
			//Disabled for now (KP)
			//mainContainer.AddChild(GetDisplayControl());
            //mainContainer.AddChild(new HorizontalLine(separatorLineColor));

#if __ANDROID__
			mainContainer.AddChild(GetModeControl());
			mainContainer.AddChild(new HorizontalLine(separatorLineColor));
#endif

            mainContainer.AddChild(GetThemeControl()); 
            
            AddChild(mainContainer);

            AddHandlers();
        }

        private void SetDisplayAttributes()
        {
            //this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            this.Margin = new BorderDouble(2, 4, 2, 0);
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

        private FlowLayoutWidget GetThemeControl()
        {
            FlowLayoutWidget buttonRow = new FlowLayoutWidget(Agg.UI.FlowDirection.TopToBottom);
            buttonRow.HAnchor = HAnchor.ParentLeftRight;
            buttonRow.Margin = new BorderDouble(0, 6);

			string settingLabelBeggining = LocalizedString.Get("Theme");
			string settingLabelEnd = LocalizedString.Get("Display Options");
			string settingLabelFull = String.Format("{0}/{1}", settingLabelBeggining, settingLabelEnd);
			TextWidget settingLabel = new TextWidget(settingLabelFull);//"Theme/Display Options"
            settingLabel.AutoExpandBoundsToText = true;
            settingLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            settingLabel.HAnchor = Agg.UI.HAnchor.ParentLeft;
            

            FlowLayoutWidget colorSelectorContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            colorSelectorContainer.HAnchor = HAnchor.ParentLeftRight;
            colorSelectorContainer.Margin = new BorderDouble(top: 4);

            GuiWidget currentColorThemeBorder = new GuiWidget();
            
            currentColorThemeBorder.VAnchor = VAnchor.ParentBottomTop;
            currentColorThemeBorder.Padding = new BorderDouble(5);
            currentColorThemeBorder.Width = 80;
            currentColorThemeBorder.BackgroundColor = RGBA_Bytes.White;

            GuiWidget currentColorTheme = new GuiWidget();
            currentColorTheme.HAnchor = HAnchor.ParentLeftRight;
            currentColorTheme.VAnchor = VAnchor.ParentBottomTop;
            currentColorTheme.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

            currentColorThemeBorder.AddChild(currentColorTheme);

            ThemeColorSelectorWidget themeSelector = new ThemeColorSelectorWidget(colorToChangeTo: currentColorTheme);
            themeSelector.Margin = new BorderDouble(right: 5);            

            colorSelectorContainer.AddChild(themeSelector);
            colorSelectorContainer.AddChild(currentColorThemeBorder);            

            buttonRow.AddChild(settingLabel);
            buttonRow.AddChild(colorSelectorContainer);

            return buttonRow;
        }

        private FlowLayoutWidget GetDisplayControl()
        {
            FlowLayoutWidget buttonRow = new FlowLayoutWidget();
            buttonRow.HAnchor = HAnchor.ParentLeftRight;
            buttonRow.Margin = new BorderDouble(top: 4);

			TextWidget settingsLabel = new TextWidget(LocalizedString.Get("Change Display Mode"));
            settingsLabel.AutoExpandBoundsToText = true;
            settingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            settingsLabel.VAnchor = VAnchor.ParentTop;

			displayControlRestartButton = textImageButtonFactory.Generate("Restart");
			displayControlRestartButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
			displayControlRestartButton.Visible = false;
			displayControlRestartButton.Margin = new BorderDouble(right: 6);
			displayControlRestartButton.Click += (sender, e) =>
			{
				RestartApplication();
			};

            FlowLayoutWidget optionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            optionsContainer.Margin = new BorderDouble(bottom: 6);

            StyledDropDownList interfaceOptionsDropList = new StyledDropDownList("Development", maxHeight: 200);
            interfaceOptionsDropList.HAnchor = HAnchor.ParentLeftRight;

            optionsContainer.AddChild(interfaceOptionsDropList);
            optionsContainer.Width = 200;

			MenuItem responsizeOptionsDropDownItem = interfaceOptionsDropList.AddItem(LocalizedString.Get("Normal"), "responsive");
            MenuItem touchscreenOptionsDropDownItem = interfaceOptionsDropList.AddItem(LocalizedString.Get("Touchscreen"), "touchscreen");

            List<string> acceptableUpdateFeedTypeValues = new List<string>() { "responsive", "touchscreen" };
            string currentUpdateFeedType = UserSettings.Instance.get("ApplicationDisplayMode");

            if (acceptableUpdateFeedTypeValues.IndexOf(currentUpdateFeedType) == -1)
            {
                UserSettings.Instance.set("ApplicationDisplayMode", "responsive");
            }

            interfaceOptionsDropList.SelectedValue = UserSettings.Instance.get("ApplicationDisplayMode");
            interfaceOptionsDropList.SelectionChanged += new EventHandler(DisplayOptionsDropList_SelectionChanged);

            buttonRow.AddChild(settingsLabel);
            buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(displayControlRestartButton);
            buttonRow.AddChild(optionsContainer);
            return buttonRow;
        }

		private FlowLayoutWidget GetModeControl()
		{
			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Margin = new BorderDouble(top: 4);

			TextWidget settingsLabel = new TextWidget(LocalizedString.Get("Interface Mode"));
			settingsLabel.AutoExpandBoundsToText = true;
			settingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			settingsLabel.VAnchor = VAnchor.ParentTop;

			FlowLayoutWidget optionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			optionsContainer.Margin = new BorderDouble(bottom: 6);

			StyledDropDownList interfaceModeDropList = new StyledDropDownList("Standard", maxHeight: 200);
			interfaceModeDropList.HAnchor = HAnchor.ParentLeftRight;

			optionsContainer.AddChild(interfaceModeDropList);
			optionsContainer.Width = 200;

			MenuItem standardModeDropDownItem = interfaceModeDropList.AddItem(LocalizedString.Get("Standard"), "True");
			MenuItem advancedModeDropDownItem = interfaceModeDropList.AddItem(LocalizedString.Get("Advanced"), "False");

            interfaceModeDropList.SelectedValue = UserSettings.Instance.Fields.IsSimpleMode.ToString();
			interfaceModeDropList.SelectionChanged += new EventHandler(InterfaceModeDropList_SelectionChanged);

			buttonRow.AddChild(settingsLabel);
			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(optionsContainer);
			return buttonRow;
		}

        private FlowLayoutWidget GetUpdateControl()
        {
            FlowLayoutWidget buttonRow = new FlowLayoutWidget();
            buttonRow.HAnchor = HAnchor.ParentLeftRight;
            buttonRow.Margin = new BorderDouble(top: 4);

            configureUpdateFeedButton = textImageButtonFactory.Generate("Configure".Localize().ToUpper());
            configureUpdateFeedButton.Margin = new BorderDouble(left: 6);
            configureUpdateFeedButton.VAnchor = VAnchor.ParentCenter;

			TextWidget settingsLabel = new TextWidget(LocalizedString.Get("Update Notification Feed"));
            settingsLabel.AutoExpandBoundsToText = true;
            settingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            settingsLabel.VAnchor = VAnchor.ParentTop;

            FlowLayoutWidget optionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            optionsContainer.Margin = new BorderDouble(bottom:6);

            StyledDropDownList releaseOptionsDropList = new StyledDropDownList("Development",maxHeight:200);            
            releaseOptionsDropList.HAnchor = HAnchor.ParentLeftRight;

            optionsContainer.AddChild(releaseOptionsDropList);
            optionsContainer.Width = 200;

			MenuItem releaseOptionsDropDownItem = releaseOptionsDropList.AddItem(LocalizedString.Get("Stable"), "release");
            releaseOptionsDropDownItem.Selected += new EventHandler(FixTabDot);

			MenuItem preReleaseDropDownItem = releaseOptionsDropList.AddItem(LocalizedString.Get("Beta"), "pre-release");
            preReleaseDropDownItem.Selected += new EventHandler(FixTabDot);

			MenuItem developmentDropDownItem = releaseOptionsDropList.AddItem(LocalizedString.Get("Alpha"), "development");
            developmentDropDownItem.Selected += new EventHandler(FixTabDot);
            
            List<string> acceptableUpdateFeedTypeValues = new List<string>() { "release", "pre-release", "development" };
            string currentUpdateFeedType = UserSettings.Instance.get("UpdateFeedType");

            if (acceptableUpdateFeedTypeValues.IndexOf(currentUpdateFeedType) == -1)
            {
                UserSettings.Instance.set("UpdateFeedType", "release");
            }

            releaseOptionsDropList.SelectedValue = UserSettings.Instance.get("UpdateFeedType");
            releaseOptionsDropList.SelectionChanged += new EventHandler(ReleaseOptionsDropList_SelectionChanged);

            buttonRow.AddChild(settingsLabel);
            buttonRow.AddChild(new HorizontalSpacer());
            buttonRow.AddChild(optionsContainer);
            return buttonRow;
        }

        private FlowLayoutWidget GetLanguageControl()
        {
            FlowLayoutWidget buttonRow = new FlowLayoutWidget();
            buttonRow.HAnchor = HAnchor.ParentLeftRight;
            buttonRow.Margin = new BorderDouble(top: 4);

			TextWidget settingsLabel = new TextWidget(LocalizedString.Get("Language Options"));
            settingsLabel.AutoExpandBoundsToText = true;
            settingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            settingsLabel.VAnchor = VAnchor.ParentTop;

            FlowLayoutWidget controlsContainer = new FlowLayoutWidget();
            controlsContainer.HAnchor = HAnchor.ParentLeftRight;

            FlowLayoutWidget optionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);            
            optionsContainer.Margin = new BorderDouble(bottom: 6);

            LanguageSelector languageSelector = new LanguageSelector();
            languageSelector.SelectionChanged += new EventHandler(LanguageDropList_SelectionChanged);
            languageSelector.HAnchor = HAnchor.ParentLeftRight;

            optionsContainer.AddChild(languageSelector);
            optionsContainer.Width = 200;

			languageRestartButton = textImageButtonFactory.Generate("Restart");
			languageRestartButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
			languageRestartButton.Visible = false;
			languageRestartButton.Margin = new BorderDouble(right: 6);
			languageRestartButton.Click += (sender, e) =>
            {
                RestartApplication();
            };

            buttonRow.AddChild(settingsLabel);
            buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(languageRestartButton);
            buttonRow.AddChild(optionsContainer);
            return buttonRow;
        }

        private FlowLayoutWidget GetSliceEngineControl()
        {
            FlowLayoutWidget buttonRow = new FlowLayoutWidget();
            buttonRow.HAnchor = HAnchor.ParentLeftRight;
            buttonRow.Margin = new BorderDouble(top: 4);

            TextWidget settingsLabel = new TextWidget("Slice Engine".Localize());
            settingsLabel.AutoExpandBoundsToText = true;
            settingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            settingsLabel.VAnchor = VAnchor.ParentTop;

            FlowLayoutWidget controlsContainer = new FlowLayoutWidget();
            controlsContainer.HAnchor = HAnchor.ParentLeftRight;

            FlowLayoutWidget optionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            optionsContainer.Margin = new BorderDouble(bottom: 6);

            if (ActiveSliceSettings.Instance.ExtruderCount > 1)
            {
                // Reset active slicer to MatterSlice when multi-extruder is detected and MatterSlice is not already set
                if (ActivePrinterProfile.Instance.ActiveSliceEngineType != ActivePrinterProfile.SlicingEngineTypes.MatterSlice)
                {
                    ActivePrinterProfile.Instance.ActiveSliceEngineType = ActivePrinterProfile.SlicingEngineTypes.MatterSlice;
                }

                // don't let the silce engine change if dual extrusion
                return null;
            }

            optionsContainer.AddChild(new SliceEngineSelector("Slice Engine".Localize()));
            optionsContainer.Width = 200;

            buttonRow.AddChild(settingsLabel);
            buttonRow.AddChild(new HorizontalSpacer());
            buttonRow.AddChild(optionsContainer);
            return buttonRow;
        }

        private void AddHandlers()
        {
        }

        private void RestartApplication()
        {
            UiThread.RunOnIdle((state) =>
            {
                // Iterate to the top SystemWindow
                GuiWidget parent = this;
                while (parent.Parent != null)
                {
                    parent = parent.Parent;
                }

                // MatterControlApplication is the root child on the SystemWindow object
                MatterControlApplication app = parent.Children[0] as MatterControlApplication;
#if !__ANDROID__ 
                app.RestartOnClose = true;
                app.Close();
#else
                // Re-initialize and load
                LocalizedString.ResetTranslationMap();
                ApplicationController.Instance.MainView = new CompactApplicationView();
                app.RemoveAllChildren();
                app.AddChild(new SoftKeyboardContentOffset(ApplicationController.Instance.MainView, SoftKeyboardContentOffset.AndroidKeyboardOffset));
                app.AnchorAll();
#endif
            });
        }

        void FixTabDot(object sender, EventArgs e)
        {
            UpdateControlData.Instance.CheckForUpdateUserRequested();
        }

		private void InterfaceModeDropList_SelectionChanged(object sender, EventArgs e)
		{
			string isSimpleMode = ((StyledDropDownList)sender).SelectedValue;
            if (isSimpleMode == "True")
            {
                UserSettings.Instance.Fields.IsSimpleMode = true;
            }
            else
            {
                UserSettings.Instance.Fields.IsSimpleMode = false;
            }
            ActiveTheme.Instance.ReloadThemeSettings();
        }

        private void DisplayOptionsDropList_SelectionChanged(object sender, EventArgs e)
        {
            string releaseCode = ((StyledDropDownList)sender).SelectedValue;
            if (releaseCode != UserSettings.Instance.get("ApplicationDisplayMode"))
            {
                UserSettings.Instance.set("ApplicationDisplayMode", releaseCode);
				displayControlRestartButton.Visible = true;
            }
        }

        private void ReleaseOptionsDropList_SelectionChanged(object sender, EventArgs e)
        {
            string releaseCode = ((StyledDropDownList)sender).SelectedValue;
            if (releaseCode != UserSettings.Instance.get("UpdateFeedType"))
            {
                UserSettings.Instance.set("UpdateFeedType", releaseCode);

            }
        }

        private void LanguageDropList_SelectionChanged(object sender, EventArgs e)
        {
            string languageCode = ((DropDownList)sender).SelectedLabel;
            if (languageCode != UserSettings.Instance.get("Language"))
            {
                UserSettings.Instance.set("Language", languageCode);
				languageRestartButton.Visible = true;
            }
        }

    }
}