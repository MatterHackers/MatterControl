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

namespace MatterHackers.MatterControl.ConfigurationPage
{
    public class ApplicationSettingsWidget : SettingsViewBase
    {
        Button restartButton;
        Button configureUpdateFeedButton;
        Button configureLanguageButton;
        
        public ApplicationSettingsWidget()
            : base("Application Settings")
        {
            mainContainer.AddChild(GetUpdateControl());
            mainContainer.AddChild(new HorizontalLine(separatorLineColor));
            mainContainer.AddChild(GetLanguageControl());
            mainContainer.AddChild(new HorizontalLine(separatorLineColor));
            mainContainer.AddChild(GetDisplayControl());
            mainContainer.AddChild(new HorizontalLine(separatorLineColor));
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

            TextWidget settingLabel = new TextWidget("Theme/Display Options");
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


            TextWidget settingsLabel = new TextWidget("Change Display Mode");
            settingsLabel.AutoExpandBoundsToText = true;
            settingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            settingsLabel.VAnchor = VAnchor.ParentTop;

            FlowLayoutWidget optionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            optionsContainer.Margin = new BorderDouble(bottom: 6);

            StyledDropDownList releaseOptionsDropList = new StyledDropDownList("Development", maxHeight: 200);
            releaseOptionsDropList.HAnchor = HAnchor.ParentLeftRight;

            optionsContainer.AddChild(releaseOptionsDropList);
            optionsContainer.Width = 200;

            MenuItem releaseOptionsDropDownItem = releaseOptionsDropList.AddItem("Normal", "responsive");
            MenuItem preReleaseDropDownItem = releaseOptionsDropList.AddItem("Touchscreen", "touchscreen");

            List<string> acceptableUpdateFeedTypeValues = new List<string>() { "responsive", "touchscreen" };
            string currentUpdateFeedType = UserSettings.Instance.get("ApplicationDisplayMode");

            if (acceptableUpdateFeedTypeValues.IndexOf(currentUpdateFeedType) == -1)
            {
                UserSettings.Instance.set("ApplicationDisplayMode", "responsive");
            }

            releaseOptionsDropList.SelectedValue = UserSettings.Instance.get("ApplicationDisplayMode");
            releaseOptionsDropList.SelectionChanged += new EventHandler(DisplayOptionsDropList_SelectionChanged);

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

            TextWidget settingsLabel = new TextWidget("Update Notification Feed");
            settingsLabel.AutoExpandBoundsToText = true;
            settingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            settingsLabel.VAnchor = VAnchor.ParentTop;

            FlowLayoutWidget optionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            optionsContainer.Margin = new BorderDouble(bottom:6);

            StyledDropDownList releaseOptionsDropList = new StyledDropDownList("Development",maxHeight:200);            
            releaseOptionsDropList.HAnchor = HAnchor.ParentLeftRight;

            optionsContainer.AddChild(releaseOptionsDropList);
            optionsContainer.Width = 200;

            MenuItem releaseOptionsDropDownItem = releaseOptionsDropList.AddItem("Release", "release");
            releaseOptionsDropDownItem.Selected += new EventHandler(FixTabDot);

            MenuItem preReleaseDropDownItem = releaseOptionsDropList.AddItem("Pre-Release", "pre-release");
            preReleaseDropDownItem.Selected += new EventHandler(FixTabDot);

            MenuItem developmentDropDownItem = releaseOptionsDropList.AddItem("Development", "development");
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

            TextWidget settingsLabel = new TextWidget("Language Options");
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

            restartButton = textImageButtonFactory.Generate("Restart");
            restartButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
            restartButton.Visible = false;
            restartButton.Margin = new BorderDouble(right: 6);
            restartButton.Click += (sender, e) =>
            {
                RestartApplication();
            };

            buttonRow.AddChild(settingsLabel);
            buttonRow.AddChild(new HorizontalSpacer());
            buttonRow.AddChild(restartButton);
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


        void FixTabDot(object sender, EventArgs e)
        {
            UpdateControlData.Instance.CheckForUpdateUserRequested();
        }

        private void DisplayOptionsDropList_SelectionChanged(object sender, EventArgs e)
        {
            string releaseCode = ((StyledDropDownList)sender).SelectedValue;
            if (releaseCode != UserSettings.Instance.get("ApplicationDisplayMode"))
            {
                UserSettings.Instance.set("ApplicationDisplayMode", releaseCode);
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
                restartButton.Visible = true;
            }
        }

    }
}