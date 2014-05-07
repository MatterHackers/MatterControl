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

﻿using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
    public class SliceSettingsWidget : GuiWidget
    {
        const string SliceSettingsShowHelpEntry = "SliceSettingsShowHelp";
        const string SliceSettingsLevelEntry = "SliceSettingsLevel";
        TextImageButtonFactory buttonFactory = new TextImageButtonFactory();

        public class UiState
        {
            public class WhichItem
            {
                public int index = 0;
                public string name = "";
            }

            public WhichItem selectedCategory = new WhichItem();
            public WhichItem selectedGroup = new WhichItem();

            public UiState()
            {
            }

            public UiState(SliceSettingsWidget settingsToCopy)
            {
                if (settingsToCopy != null)
                {
                    settingsToCopy.CurrentlyActiveCategory(out selectedCategory.index, out selectedCategory.name);
                    settingsToCopy.CurrentlyActiveGroup(out selectedGroup.index, out selectedGroup.name);
                }
            }
        }

        TabControl categoryTabs;
        GroupBox noConnectionMessageContainer;
        FlowLayoutWidget settingsControlBar;
        CheckBox showHelpBox;
        CheckBox showAllDetails;

        public SliceSettingsWidget(UiState uiState)
        {
            int minSettingNameWidth = 220;
            buttonFactory.FixedHeight = 20;
            buttonFactory.fontSize = 10;
            buttonFactory.normalFillColor = RGBA_Bytes.White;
            buttonFactory.normalTextColor = RGBA_Bytes.DarkGray;

            showHelpBox = new CheckBox(0, 0, LocalizedString.Get("Show Help"), textSize: 10);
            showHelpBox.Checked = UserSettings.Instance.get(SliceSettingsShowHelpEntry) == "true";

            showAllDetails = new CheckBox(0, 0, LocalizedString.Get("Show All Settings"), textSize: 10);
            showAllDetails.Checked = UserSettings.Instance.get(SliceSettingsLevelEntry) == "Advanced";

            FlowLayoutWidget pageTopToBottomLayout = new FlowLayoutWidget(FlowDirection.TopToBottom, vAnchor: Agg.UI.VAnchor.ParentTop);
            pageTopToBottomLayout.AnchorAll();
            pageTopToBottomLayout.Padding = new BorderDouble(3, 0);
            this.AddChild(pageTopToBottomLayout);

            settingsControlBar = new SettingsControlBar();
            pageTopToBottomLayout.AddChild(settingsControlBar);

            noConnectionMessageContainer = new GroupBox(LocalizedString.Get("No Printer Selected"));
            noConnectionMessageContainer.Margin = new BorderDouble(top: 10);
            noConnectionMessageContainer.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            noConnectionMessageContainer.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            noConnectionMessageContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            noConnectionMessageContainer.Height = 80;

            TextWidget noConnectionMessage = new TextWidget(LocalizedString.Get("No printer is currently selected. Select printer to edit slice settings."));
            noConnectionMessage.Margin = new BorderDouble(5);
            noConnectionMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            noConnectionMessage.VAnchor = VAnchor.ParentCenter;

            noConnectionMessageContainer.AddChild(noConnectionMessage);
            pageTopToBottomLayout.AddChild(noConnectionMessageContainer);

            categoryTabs = new TabControl();
            categoryTabs.TabBar.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            categoryTabs.Margin = new BorderDouble(top: 8);
            categoryTabs.AnchorAll();
            List<TabBar> sideTabBarsListForLayout = new List<TabBar>();
            for (int categoryIndex = 0; categoryIndex < SliceSettingsOrganizer.Instance.UserLevels[UserLevel].CategoriesList.Count; categoryIndex++)
            {
                OrganizerCategory category = SliceSettingsOrganizer.Instance.UserLevels[UserLevel].CategoriesList[categoryIndex];
                string categoryPageLabel = LocalizedString.Get(category.Name);
                TabPage categoryPage = new TabPage(categoryPageLabel);
                SimpleTextTabWidget textTabWidget = new SimpleTextTabWidget(categoryPage, 16,
                        ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes());
                categoryPage.AnchorAll();
                categoryTabs.AddTab(textTabWidget);

                TabControl sideTabs = CreateSideTabsAndPages(minSettingNameWidth, category, uiState);
                sideTabBarsListForLayout.Add(sideTabs.TabBar);

                categoryPage.AddChild(sideTabs);
            }

            if (showAllDetails.Checked && ActivePrinterProfile.Instance.ActiveSliceEngineType == ActivePrinterProfile.SlicingEngineTypes.Slic3r)
            {
                TabPage extraSettingsPage = new TabPage("Other");
                SimpleTextTabWidget extraSettingsTextTabWidget = new SimpleTextTabWidget(extraSettingsPage, 16,
                        ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes());
                extraSettingsPage.AnchorAll();
                int count;
                TabControl extraSettingsSideTabs = CreateExtraSettingsSideTabsAndPages(minSettingNameWidth, categoryTabs, out count);
                if (count > 0)
                {
                    categoryTabs.AddTab(extraSettingsTextTabWidget);
                    sideTabBarsListForLayout.Add(extraSettingsSideTabs.TabBar);
                    extraSettingsPage.AddChild(extraSettingsSideTabs);
                }
            }

            double sideTabBarsMinimumWidth = 0;
            foreach (TabBar tabBar in sideTabBarsListForLayout)
            {
                sideTabBarsMinimumWidth = Math.Max(sideTabBarsMinimumWidth, tabBar.Width);
            }
            foreach (TabBar tabBar in sideTabBarsListForLayout)
            {
                tabBar.MinimumSize = new Vector2(sideTabBarsMinimumWidth, tabBar.MinimumSize.y);
            }

            // space before checkboxes (hold the right aligned)
            {
                GuiWidget hSpacer = new GuiWidget();
                hSpacer.HAnchor = HAnchor.ParentLeftRight;

                categoryTabs.TabBar.AddChild(hSpacer);
            }

            // add in the ability to turn on and off all details settings
            {
                showAllDetails.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                showAllDetails.Margin = new BorderDouble(right: 8);
                showAllDetails.VAnchor = VAnchor.ParentCenter;
                showAllDetails.Cursor = Cursors.Hand;
                showAllDetails.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(RebuildSlicerSettings);

                categoryTabs.TabBar.AddChild(showAllDetails);
            }

            // add in the ability to turn on and off help text
            {
                showHelpBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                showHelpBox.Margin = new BorderDouble(right: 3);
                showHelpBox.VAnchor = VAnchor.ParentCenter;
                showHelpBox.Cursor = Cursors.Hand;
                showHelpBox.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(RebuildSlicerSettings);

                categoryTabs.TabBar.AddChild(showHelpBox);
            }

            pageTopToBottomLayout.AddChild(categoryTabs);
            AddHandlers();
            SetVisibleControls();

            if (!categoryTabs.SelectTab(uiState.selectedCategory.name))
            {
                categoryTabs.SelectTab(uiState.selectedCategory.index);
            }
        }

        public bool ShowingHelp
        {
            get { return showHelpBox.Checked; }
        }

        public string UserLevel
        {
            get 
            {
                if (showAllDetails.Checked)
                {
                    return "Advanced";
                }

                return "Beginner"; 
            }
        }

        public void CurrentlyActiveCategory(out int index, out string name)
        {
            index = categoryTabs.SelectedTabIndex;
            name = categoryTabs.SelectedTabName;
        }

        public void CurrentlyActiveGroup(out int index, out string name)
        {
            index = 0;
            name = "";

            TabPage currentPage = categoryTabs.GetActivePage();
            TabControl currentGroup = null;

            if (currentPage.Children.Count > 0)
            {
                currentGroup = currentPage.Children[0] as TabControl;
            }
            if (currentGroup != null)
            {
                index = currentGroup.SelectedTabIndex;
                name = currentGroup.SelectedTabName;
            }
        }

        void RebuildSlicerSettings(object sender, EventArgs e)
        {
            UserSettings.Instance.set(SliceSettingsShowHelpEntry, showHelpBox.Checked.ToString().ToLower());
            if(showAllDetails.Checked)
            {
                UserSettings.Instance.set(SliceSettingsLevelEntry, "Advanced");
            }
            else
            {
                UserSettings.Instance.set(SliceSettingsLevelEntry, "Beginner");
            }
            
            CheckBox checkBox = sender as CheckBox;
            if (checkBox != null)
            {
                ApplicationWidget.Instance.ReloadAdvancedControlsPanel();
            }
        }

        internal class ExtraSettingTextWidget : MHTextEditWidget
        {
            internal string itemKey { get; set; }
            internal ExtraSettingTextWidget(string itemKey, string itemValue)
                : base(itemValue)
            {
                this.itemKey = itemKey;
            }
        }

        event EventHandler unregisterEvents;
        private void AddHandlers()
        {
            PrinterCommunication.Instance.ConnectionStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(APP_onPrinterStatusChanged, ref unregisterEvents);
            PrinterCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }

            base.OnClosed(e);
        }

        private void onPrinterStatusChanged(object sender, EventArgs e)
        {
            SetVisibleControls();
            this.Invalidate();
        }

        private void APP_onPrinterStatusChanged(object sender, EventArgs e)
        {
            SetVisibleControls();
            this.Invalidate();
        }

        private void SetVisibleControls()
        {
            if (ActivePrinterProfile.Instance.ActivePrinter != null)
            {
                categoryTabs.Visible = true;
                settingsControlBar.Visible = true;
                noConnectionMessageContainer.Visible = false;
            }
            else
            {
                categoryTabs.Visible = false;
                settingsControlBar.Visible = false;
                noConnectionMessageContainer.Visible = true;
            }
        }

        int tabIndexForItem = 0;
        private TabControl CreateSideTabsAndPages(int minSettingNameWidth, OrganizerCategory category, UiState uiState)
        {
            TabControl groupTabs = new TabControl(Orientation.Vertical);
            groupTabs.Margin = new BorderDouble(0, 0, 0, 5);
            groupTabs.TabBar.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            foreach (OrganizerGroup group in category.GroupsList)
            {
                tabIndexForItem = 0;
				string groupTabLabel = LocalizedString.Get (group.Name);
				TabPage groupTabPage = new TabPage(groupTabLabel);
                groupTabPage.HAnchor = HAnchor.ParentLeftRight;

                SimpleTextTabWidget groupTabWidget = new SimpleTextTabWidget(groupTabPage, 14,
                   ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes());
                groupTabWidget.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

                FlowLayoutWidget subGroupLayoutTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
                subGroupLayoutTopToBottom.AnchorAll();

                bool needToAddSubGroup = false;
                foreach (OrganizerSubGroup subGroup in group.SubGroupsList)
                {
                    bool addedSettingToSubGroup = false;
                    FlowLayoutWidget topToBottomSettings = new FlowLayoutWidget(FlowDirection.TopToBottom);
                    topToBottomSettings.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

                    foreach (OrganizerSettingsData settingInfo in subGroup.SettingDataList)
                    {
                        if (ActivePrinterProfile.Instance.ActiveSliceEngine.MapContains(settingInfo.SlicerConfigName))
                        {
                            addedSettingToSubGroup = true;
                            GuiWidget controlsForThisSetting = CreateSettingInfoUIControls(settingInfo, minSettingNameWidth);
                            topToBottomSettings.AddChild(controlsForThisSetting);

                            if (showHelpBox.Checked)
                            {
                                AddInHelpText(topToBottomSettings, settingInfo);
                            }
                        }
                    }

                    if (addedSettingToSubGroup)
                    {
                        needToAddSubGroup = true;
						string groupBoxLabel = LocalizedString.Get (subGroup.Name);
						GroupBox groupBox = new GroupBox (groupBoxLabel);
                        groupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                        groupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
                        groupBox.AddChild(topToBottomSettings);
                        groupBox.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

                        subGroupLayoutTopToBottom.AddChild(groupBox);
                    }
                }

                if (needToAddSubGroup)
                {
                    SliceSettingListControl scrollOnGroupTab = new SliceSettingListControl();

                    subGroupLayoutTopToBottom.VAnchor = VAnchor.FitToChildren;
                    subGroupLayoutTopToBottom.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                    
                    //subGroupLayoutTopToBottom.DebugShowBounds = true;
                    //scrollOnGroupTab.DebugShowBounds = true;
                    scrollOnGroupTab.AddChild(subGroupLayoutTopToBottom);
                    groupTabPage.AddChild(scrollOnGroupTab);
                    groupTabs.AddTab(groupTabWidget);
                }
            }

            if (!groupTabs.SelectTab(uiState.selectedGroup.name))
            {
                groupTabs.SelectTab(uiState.selectedGroup.index);
            }
            return groupTabs;
        }

        private void AddInHelpText(FlowLayoutWidget topToBottomSettings, OrganizerSettingsData settingInfo)
        {
            FlowLayoutWidget allText = new FlowLayoutWidget(FlowDirection.TopToBottom);
            double textRegionWidth = 380;
            allText.Margin = new BorderDouble(3);
            allText.Padding = new BorderDouble(5);
            allText.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;

            double helpPointSize = 10;

            EnglishTextWrapping wrapper = new EnglishTextWrapping(helpPointSize);
            string[] wrappedText = wrapper.WrapText(settingInfo.HelpText, textRegionWidth - allText.Padding.Width);
            foreach(string line in wrappedText)
            {
                GuiWidget helpWidget = new TextWidget(line, pointSize: helpPointSize, textColor: RGBA_Bytes.White);
                helpWidget.Margin = new BorderDouble(5, 0, 0, 0);
                helpWidget.HAnchor = HAnchor.ParentLeft;
                allText.AddChild(helpWidget);
            }

            allText.MinimumSize = new Vector2(textRegionWidth, allText.MinimumSize.y);
            if (wrappedText.Length > 0)
            {
                topToBottomSettings.AddChild(allText);
            }
        }

        private TabControl CreateExtraSettingsSideTabsAndPages(int minSettingNameWidth, TabControl categoryTabs, out int count)
        {
            count = 0;
            TabControl sideTabs = new TabControl(Orientation.Vertical);
            sideTabs.Margin = new BorderDouble(0, 0, 0, 5);
            sideTabs.TabBar.BorderColor = RGBA_Bytes.White;
            {
                TabPage groupTabPage = new TabPage("Extra Settings");
                SimpleTextTabWidget groupTabWidget = new SimpleTextTabWidget(groupTabPage, 14,
                   ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes());
                sideTabs.AddTab(groupTabWidget);

                FlowLayoutWidget subGroupLayoutTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
                subGroupLayoutTopToBottom.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
                subGroupLayoutTopToBottom.VAnchor = VAnchor.FitToChildren;

                FlowLayoutWidget topToBottomSettings = new FlowLayoutWidget(FlowDirection.TopToBottom);
                topToBottomSettings.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;

                foreach (KeyValuePair<string, DataStorage.SliceSetting> item in ActiveSliceSettings.Instance.DefaultSettings)
                {
                    if (!SliceSettingsOrganizer.Instance.Contains(UserLevel, item.Key))
                    {
                        OrganizerSettingsData settingInfo = new OrganizerSettingsData(item.Key, item.Key, OrganizerSettingsData.DataEditTypes.STRING);
                        GuiWidget controlsForThisSetting = CreateSettingInfoUIControls(settingInfo, minSettingNameWidth);
                        topToBottomSettings.AddChild(controlsForThisSetting);
                        count++;
                    }
                }

                GroupBox groupBox = new GroupBox(LocalizedString.Get("Extra"));
                groupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                groupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
                groupBox.AddChild(topToBottomSettings);
                groupBox.VAnchor = VAnchor.FitToChildren;
                groupBox.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;

                subGroupLayoutTopToBottom.AddChild(groupBox);

                SliceSettingListControl scrollOnGroupTab = new SliceSettingListControl();
                scrollOnGroupTab.AnchorAll();
                scrollOnGroupTab.AddChild(subGroupLayoutTopToBottom);
                groupTabPage.AddChild(scrollOnGroupTab);
            }
            return sideTabs;
        }

        private TextWidget getSettingInfoData(OrganizerSettingsData settingData)
        {
            string extraSettings = settingData.ExtraSettings;
            extraSettings = extraSettings.Replace("\\n", "\n");
            TextWidget dataTypeInfo = new TextWidget(extraSettings, pointSize:10);
            dataTypeInfo.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            dataTypeInfo.Margin = new BorderDouble(5, 0);
            return dataTypeInfo;
        }

        private GuiWidget CreateSettingInfoUIControls(OrganizerSettingsData settingData, double minSettingNameWidth)
        {
            GuiWidget container = new GuiWidget();            
            FlowLayoutWidget leftToRightLayout = new FlowLayoutWidget();

            bool addQualityOverlay = false;
            bool addMaterialOverlay = false;

            RGBA_Bytes qualityOverlayColor = new RGBA_Bytes(255, 255, 0, 40);
            RGBA_Bytes materialOverlayColor = new RGBA_Bytes(255, 127, 0, 40);

            if (ActiveSliceSettings.Instance.Contains(settingData.SlicerConfigName))
            {
                int intEditWidth = 60;
                int doubleEditWidth = 60;
                int vectorXYEditWidth = 60;
                int multiLineEditHeight = 60;

                              

                string sliceSettingValue = ActiveSliceSettings.Instance.GetActiveValue(settingData.SlicerConfigName);
                leftToRightLayout.Margin = new BorderDouble(0, 2);
                leftToRightLayout.Padding = new BorderDouble(3);
                leftToRightLayout.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

                if (settingData.DataEditType != OrganizerSettingsData.DataEditTypes.MULTI_LINE_TEXT)
                {
                    string convertedNewLines = settingData.PresentationName.Replace("\\n ", "\n");
                    convertedNewLines = convertedNewLines.Replace("\\n", "\n");
					convertedNewLines = LocalizedString.Get (convertedNewLines);
                    TextWidget settingName = new TextWidget(convertedNewLines);
                    settingName.TextColor = ActiveTheme.Instance.PrimaryTextColor;

                    if (ActiveSliceSettings.Instance.SettingExistsInLayer(settingData.SlicerConfigName, 3))
                    {
                        addMaterialOverlay = true; 
                    }
                    else if (ActiveSliceSettings.Instance.SettingExistsInLayer(settingData.SlicerConfigName, 2))
                    {
                        addQualityOverlay = true;
                    }
                    
                    settingName.Width = minSettingNameWidth;
                    //settingName.MinimumSize = new Vector2(minSettingNameWidth, settingName.MinimumSize.y);
                    leftToRightLayout.AddChild(settingName);
                }
                
                switch (settingData.DataEditType)
                {
                    case OrganizerSettingsData.DataEditTypes.INT:
                        {
                            int currentValue = 0;
                            int.TryParse(sliceSettingValue, out currentValue);
                            MHNumberEdit intEditWidget = new MHNumberEdit(currentValue, pixelWidth: intEditWidth, tabIndex: tabIndexForItem++);
                            intEditWidget.ActuallNumberEdit.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString()); };
                            leftToRightLayout.AddChild(intEditWidget);
                            leftToRightLayout.AddChild(getSettingInfoData(settingData));
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.DOUBLE:
                        {
                            double currentValue = 0;
                            double.TryParse(sliceSettingValue, out currentValue);
                            MHNumberEdit doubleEditWidget = new MHNumberEdit(currentValue, allowNegatives: true, allowDecimals: true, pixelWidth: doubleEditWidth, tabIndex: tabIndexForItem++);
                            doubleEditWidget.ActuallNumberEdit.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString()); };
                            leftToRightLayout.AddChild(doubleEditWidget);
                            leftToRightLayout.AddChild(getSettingInfoData(settingData));
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.POSITIVE_DOUBLE:
                        {
                            double currentValue = 0;
                            double.TryParse(sliceSettingValue, out currentValue);
                            MHNumberEdit doubleEditWidget = new MHNumberEdit(currentValue, allowDecimals: true, pixelWidth: doubleEditWidth, tabIndex: tabIndexForItem++);
                            doubleEditWidget.ActuallNumberEdit.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString()); };
                            leftToRightLayout.AddChild(doubleEditWidget);
                            leftToRightLayout.AddChild(getSettingInfoData(settingData));
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.OFFSET:
                        {
                            double currentValue = 0;
                            double.TryParse(sliceSettingValue, out currentValue);
                            MHNumberEdit doubleEditWidget = new MHNumberEdit(currentValue, allowDecimals: true, allowNegatives: true, pixelWidth: doubleEditWidth, tabIndex: tabIndexForItem++);
                            doubleEditWidget.ActuallNumberEdit.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString()); };
                            leftToRightLayout.AddChild(doubleEditWidget);
                            leftToRightLayout.AddChild(getSettingInfoData(settingData));
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.DOUBLE_OR_PERCENT:
                        {
                            MHTextEditWidget stringEdit = new MHTextEditWidget(sliceSettingValue, pixelWidth: 60, tabIndex: tabIndexForItem++);
                            stringEdit.ActualTextEditWidget.EditComplete += (sender, e) =>
                            {
                                TextEditWidget textEditWidget = (TextEditWidget)sender;
                                string text = textEditWidget.Text;
                                text = text.Trim();
                                bool isPercent = text.Contains("%");
                                if(isPercent)
                                {
                                    text = text.Substring(0, text.IndexOf("%"));
                                }
                                double result;
                                double.TryParse(text, out result);
                                text = result.ToString();
                                if (isPercent)
                                {
                                    text += "%";
                                }
                                textEditWidget.Text = text;
                                SaveSetting(settingData.SlicerConfigName, textEditWidget.Text);
                            };

                            leftToRightLayout.AddChild(stringEdit);
                            leftToRightLayout.AddChild(getSettingInfoData(settingData));
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.CHECK_BOX:
                        {
                            CheckBox checkBoxWidget = new CheckBox("");
                            checkBoxWidget.VAnchor = Agg.UI.VAnchor.ParentBottom;
                            checkBoxWidget.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                            checkBoxWidget.Checked = (sliceSettingValue == "1");
                            checkBoxWidget.CheckedStateChanged += (sender, e) =>
                            {
                                if (((CheckBox)sender).Checked)
                                {
                                    SaveSetting(settingData.SlicerConfigName, "1");
                                }
                                else
                                {
                                    SaveSetting(settingData.SlicerConfigName, "0");
                                }
                            };
                            leftToRightLayout.AddChild(checkBoxWidget);
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.STRING:
                        {
                            MHTextEditWidget stringEdit = new MHTextEditWidget(sliceSettingValue, pixelWidth: 120, tabIndex: tabIndexForItem++);
                            stringEdit.ActualTextEditWidget.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, ((TextEditWidget)sender).Text); };
                            leftToRightLayout.AddChild(stringEdit);
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.MULTI_LINE_TEXT:
                        {
                            string convertedNewLines = sliceSettingValue.Replace("\\n", "\n");
                            MHTextEditWidget stringEdit = new MHTextEditWidget(convertedNewLines, pixelWidth: 320, pixelHeight: multiLineEditHeight, multiLine: true, tabIndex: tabIndexForItem++);
                            stringEdit.ActualTextEditWidget.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, ((TextEditWidget)sender).Text.Replace("\n", "\\n")); };
                            leftToRightLayout.AddChild(stringEdit);
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.LIST:
                        {
                            StyledDropDownList selectableOptions = new StyledDropDownList("None", maxHeight: 200);
                            selectableOptions.Margin = new BorderDouble();
                            
                            string[] listItems = settingData.ExtraSettings.Split(',');
                            foreach (string listItem in listItems)
                            {
                                MenuItem newItem = selectableOptions.AddItem(listItem);
                                if (newItem.Text == sliceSettingValue)
                                {
                                    selectableOptions.SelectedLabel = sliceSettingValue;
                                }

                                newItem.Selected += (sender, e) =>
                                {
                                    MenuItem menuItem = ((MenuItem)sender);
                                    SaveSetting(settingData.SlicerConfigName, menuItem.Text);
                                };
                            }
                            leftToRightLayout.AddChild(selectableOptions);
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.VECTOR2:
                        {
                            string[] xyValueStrings = sliceSettingValue.Split(',');
                            if (xyValueStrings.Length != 2)
                            {
                                xyValueStrings = new string[] { "0", "0" };
                            }
                                double currentXValue = 0;
                                double.TryParse(xyValueStrings[0], out currentXValue);
                                MHNumberEdit xEditWidget = new MHNumberEdit(currentXValue, allowDecimals: true, pixelWidth: vectorXYEditWidth, tabIndex: tabIndexForItem++);

                                double currentYValue = 0;
                                double.TryParse(xyValueStrings[1], out currentYValue);
                                MHNumberEdit yEditWidget = new MHNumberEdit(currentYValue, allowDecimals: true, pixelWidth: vectorXYEditWidth, tabIndex: tabIndexForItem++);
                            {
                                xEditWidget.ActuallNumberEdit.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "," + yEditWidget.ActuallNumberEdit.Value.ToString()); };
                                leftToRightLayout.AddChild(xEditWidget);
                                TextWidget xText = new TextWidget("x");
                                xText.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                                xText.Margin = new BorderDouble(5, 0);
                                leftToRightLayout.AddChild(xText);
                            }
                            {
                                yEditWidget.ActuallNumberEdit.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "," + yEditWidget.ActuallNumberEdit.Value.ToString()); };
                                leftToRightLayout.AddChild(yEditWidget);
                                TextWidget yText = new TextWidget("y");
                                yText.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                                yText.Margin = new BorderDouble(5, 0);
                                leftToRightLayout.AddChild(yText);
                            }
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.OFFSET2:
                        {
                            string[] xyValueStrings = sliceSettingValue.Split('x');
                            if (xyValueStrings.Length != 2)
                            {
                                xyValueStrings = new string[] { "0", "0" };
                            }
                            double currentXValue = 0;
                            double.TryParse(xyValueStrings[0], out currentXValue);
                            MHNumberEdit xEditWidget = new MHNumberEdit(currentXValue, allowDecimals: true, allowNegatives: true, pixelWidth: vectorXYEditWidth, tabIndex: tabIndexForItem++);

                            double currentYValue = 0;
                            double.TryParse(xyValueStrings[1], out currentYValue);
                            MHNumberEdit yEditWidget = new MHNumberEdit(currentYValue, allowDecimals: true, allowNegatives: true, pixelWidth: vectorXYEditWidth, tabIndex: tabIndexForItem++);
                            {
                                xEditWidget.ActuallNumberEdit.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "x" + yEditWidget.ActuallNumberEdit.Value.ToString()); };
                                leftToRightLayout.AddChild(xEditWidget);
                                TextWidget xText = new TextWidget("x");
                                xText.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                                xText.Margin = new BorderDouble(5, 0);
                                leftToRightLayout.AddChild(xText);
                            }
                            {
                                yEditWidget.ActuallNumberEdit.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "x" + yEditWidget.ActuallNumberEdit.Value.ToString()); };
                                leftToRightLayout.AddChild(yEditWidget);
                                TextWidget yText = new TextWidget("y");
                                yText.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                                yText.Margin = new BorderDouble(5, 0);
                                leftToRightLayout.AddChild(yText);
                            }
                        }
                        break;

                    default:
                        TextWidget missingSetting = new TextWidget(String.Format("Missing the setting for '{0}'.", settingData.DataEditType.ToString()));
                        missingSetting.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                        missingSetting.BackgroundColor = RGBA_Bytes.Red;
                        leftToRightLayout.AddChild(missingSetting);
                        break;
                }
            }
            else // the setting we think we are adding is not in the config.ini it may have been depricated
            {
                TextWidget settingName = new TextWidget(String.Format("Setting '{0}' not found in config.ini", settingData.SlicerConfigName));
                settingName.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                settingName.MinimumSize = new Vector2(minSettingNameWidth, settingName.MinimumSize.y);
                leftToRightLayout.AddChild(settingName);
                leftToRightLayout.BackgroundColor = RGBA_Bytes.Red;
            }

            container.HAnchor = HAnchor.ParentLeftRight;
            container.VAnchor = VAnchor.FitToChildren;

            container.AddChild(leftToRightLayout);

            if (addQualityOverlay || addMaterialOverlay)
            {              
                SettingPresetOverlay overlay = new SettingPresetOverlay();
                overlay.HAnchor = HAnchor.ParentLeftRight;
                overlay.VAnchor = Agg.UI.VAnchor.ParentBottomTop;

                SettingPresetClick clickToEdit = new SettingPresetClick();
                clickToEdit.HAnchor = HAnchor.ParentLeftRight;
                clickToEdit.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
                clickToEdit.Visible = false;

                Button editButton = buttonFactory.Generate("Edit Preset".Localize().ToUpper());
                editButton.HAnchor = Agg.UI.HAnchor.ParentCenter;
                editButton.VAnchor = Agg.UI.VAnchor.ParentCenter;

                clickToEdit.AddChild(editButton);

                if (addQualityOverlay)
                {
                    overlay.OverlayColor = qualityOverlayColor;
                    clickToEdit.OverlayColor = qualityOverlayColor;
                    editButton.Click += (sender, e) =>
                    {
                        if (ApplicationWidget.Instance.EditSlicePresetsWindow == null)
                        {
                            ApplicationWidget.Instance.EditSlicePresetsWindow = new SlicePresetsWindow(ReloadOptions, "Quality", "quality", false, ActivePrinterProfile.Instance.ActiveQualitySettingsID);
                            ApplicationWidget.Instance.EditSlicePresetsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { ApplicationWidget.Instance.EditSlicePresetsWindow = null; };
                        }
                        else
                        {
                            ApplicationWidget.Instance.EditSlicePresetsWindow.ChangeToSlicePresetFromID(ActivePrinterProfile.Instance.ActiveQualitySettingsID);
                            ApplicationWidget.Instance.EditSlicePresetsWindow.BringToFront();
                        }
                    };
                }
                else if (addMaterialOverlay)
                {
                    overlay.OverlayColor = materialOverlayColor;
                    clickToEdit.OverlayColor = materialOverlayColor;
                    editButton.Click += (sender, e) =>
                    {                        
                        if (ApplicationWidget.Instance.EditSlicePresetsWindow == null)
                        {
                            ApplicationWidget.Instance.EditSlicePresetsWindow = new SlicePresetsWindow(ReloadOptions, "Material", "material", false, ActivePrinterProfile.Instance.GetMaterialSetting(1));
                            ApplicationWidget.Instance.EditSlicePresetsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { ApplicationWidget.Instance.EditSlicePresetsWindow = null; };
                        }
                        else
                        {
                            ApplicationWidget.Instance.EditSlicePresetsWindow.ChangeToSlicePresetFromID(ActivePrinterProfile.Instance.GetMaterialSetting(1));
                            ApplicationWidget.Instance.EditSlicePresetsWindow.BringToFront();
                        }                        
                    };
                }                

                container.MouseEnterBounds += (sender, e) =>
                {
                    UiThread.RunOnIdle((state) =>
                    {
                        overlay.Visible = false;
                        clickToEdit.Visible = true;
                    });
                };

                container.MouseLeaveBounds += (sender, e) =>
                {
                    UiThread.RunOnIdle((state) =>
                    {
                        overlay.Visible = true;
                        clickToEdit.Visible = false;
                    });
                };

                container.AddChild(overlay);
                container.AddChild(clickToEdit);    
            }



            return container;
        }

        protected void ReloadOptions(object sender, EventArgs e)
        {
            ApplicationWidget.Instance.ReloadAdvancedControlsPanel();
        }

        private void SaveSetting(string slicerConfigName, string value)
        {
            //Hacky solution prevents saves when no printer is loaded
            if (ActivePrinterProfile.Instance.ActivePrinter != null)
            {
                SliceSettingsLayerSelector.Instance.SaveSetting(slicerConfigName, value);
            }
        }

        public override void OnDraw(Graphics2D graphics2D)
        {            
            base.OnDraw(graphics2D);
        }
    }

    class SettingPresetClick : ClickWidget
    {
        public RGBA_Bytes OverlayColor;

        public override void OnDraw(Graphics2D graphics2D)
        {
            
            RoundedRect rect = new RoundedRect(LocalBounds, 0);
            graphics2D.Render(rect, new RGBA_Bytes(OverlayColor, 220));
            base.OnDraw(graphics2D);

        }
    }

    class SettingPresetOverlay : GuiWidget
    {
        public RGBA_Bytes OverlayColor;
        
        public override void OnDraw(Graphics2D graphics2D)
        {
            base.OnDraw(graphics2D);
            RoundedRect rect = new RoundedRect(LocalBounds,0);
            graphics2D.Render(rect,new RGBA_Bytes(OverlayColor, 50));
            graphics2D.Render(new Stroke(rect, 3),OverlayColor);
        }
    }

    class SliceSettingListControl : ScrollableWidget
    {
        FlowLayoutWidget topToBottomItemList;

        public SliceSettingListControl()
        {
            this.AnchorAll();
            this.AutoScroll = true;
            this.ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;

            topToBottomItemList = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottomItemList.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            topToBottomItemList.Margin = new BorderDouble(top: 3);

            base.AddChild(topToBottomItemList);
        }

        public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
        {
            FlowLayoutWidget itemHolder = new FlowLayoutWidget();
            itemHolder.Margin = new BorderDouble(0, 0, 0, 0);
            itemHolder.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            itemHolder.AddChild(child);
            itemHolder.VAnchor = VAnchor.FitToChildren;

            topToBottomItemList.AddChild(itemHolder, indexInChildrenList);
        }
    }
}
