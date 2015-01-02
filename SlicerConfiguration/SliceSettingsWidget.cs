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
//#define DO_IN_PLACE_EDIT

using System;
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SliceSettingsSaveBar :FlowLayoutWidget
	{
		TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

		Button saveButton;
		Button revertbutton;

		public SliceSettingsSaveBar()
		{
			textImageButtonFactory.FixedWidth = 80 * TextWidget.GlobalPointSizeScaleRatio;
			textImageButtonFactory.fontSize = (int)(10 * TextWidget.GlobalPointSizeScaleRatio);

			this.textImageButtonFactory.normalFillColor = RGBA_Bytes.Transparent;
			this.textImageButtonFactory.disabledFillColor = RGBA_Bytes.White;


			this.textImageButtonFactory.borderWidth = 1;
			this.textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 180);
			this.textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 180);

			this.textImageButtonFactory.disabledTextColor = RGBA_Bytes.DarkGray;
			this.textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.textImageButtonFactory.normalTextColor = ActiveTheme.Instance.SecondaryTextColor;
			this.textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;


			this.Margin = new BorderDouble(left:2);
			this.HAnchor = HAnchor.ParentLeftRight;
			this.BackgroundColor = ActiveTheme.Instance.TransparentLightOverlay;
			this.Padding = new BorderDouble(5);

			string unsavedMessageText = "Unsaved Changes".Localize();
			TextWidget unsavedMessage = new TextWidget("{0}:".FormatWith(unsavedMessageText),pointSize:10 * TextWidget.GlobalPointSizeScaleRatio);
			unsavedMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			unsavedMessage.VAnchor = VAnchor.ParentCenter;

			revertbutton = textImageButtonFactory.Generate(LocalizedString.Get("Revert").ToUpper(),centerText:true);
			revertbutton.VAnchor = VAnchor.ParentCenter;
			revertbutton.Visible = true;
			revertbutton.Margin = new BorderDouble(5, 0, 0, 0);
			revertbutton.Click += new EventHandler(revertbutton_Click);	

			this.textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 255);
			this.textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 255);
			this.textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;

			saveButton = textImageButtonFactory.Generate(LocalizedString.Get("Save").ToUpper(),centerText:true);
			saveButton.VAnchor = VAnchor.ParentCenter;
			saveButton.Visible = true;
			saveButton.Margin = new BorderDouble(5, 0, 5, 0);
			saveButton.Click += new EventHandler(saveButton_Click);

			this.AddChild(new HorizontalSpacer());
			this.AddChild(unsavedMessage);
			this.AddChild(saveButton);
			this.AddChild(revertbutton);
			this.AddChild(new HorizontalSpacer());
		}

		void saveButton_Click(object sender, EventArgs mouseEvent)
		{
			ActiveSliceSettings.Instance.CommitChanges();
		}

		void revertbutton_Click(object sender, EventArgs mouseEvent)
		{
			ActiveSliceSettings.Instance.LoadAllSettings();
			ApplicationController.Instance.ReloadAdvancedControlsPanel();
		}
	}


	public class SliceSettingsDetailControl : FlowLayoutWidget
    {
        const string SliceSettingsShowHelpEntry = "SliceSettingsShowHelp";
        const string SliceSettingsLevelEntry = "SliceSettingsLevel";

        CheckBox showHelpBox;
        StyledDropDownList settingsDetailSelector;

		public DropDownMenu sliceOptionsMenuDropList;
		private TupleList<string, Func<bool>> slicerOptionsMenuItems;

        public SliceSettingsDetailControl()
        {  
			showHelpBox = new CheckBox(0, 0, LocalizedString.Get("Show Help"), textSize: 10);
            showHelpBox.Checked = UserSettings.Instance.get(SliceSettingsShowHelpEntry) == "true";
			// add in the ability to turn on and off help text
			{
				showHelpBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				showHelpBox.Margin = new BorderDouble(right: 3);
				showHelpBox.VAnchor = VAnchor.ParentCenter;
				showHelpBox.Cursor = Cursors.Hand;
				showHelpBox.CheckedStateChanged += new CheckBox.CheckedStateChangedEventHandler(RebuildSlicerSettings);

				this.AddChild(showHelpBox);
			}

            settingsDetailSelector = new StyledDropDownList("Simple", maxHeight: 200);
            settingsDetailSelector.AddItem(LocalizedString.Get("Simple"), "Simple");
            settingsDetailSelector.AddItem(LocalizedString.Get("Intermediate"), "Intermediate");
            settingsDetailSelector.AddItem(LocalizedString.Get("Advanced"), "Advanced");
            if (UserSettings.Instance.get(SliceSettingsLevelEntry) != null
                && SliceSettingsOrganizer.Instance.UserLevels.ContainsKey(UserSettings.Instance.get(SliceSettingsLevelEntry)))
            {
                settingsDetailSelector.SelectedValue = UserSettings.Instance.get(SliceSettingsLevelEntry);
            }

            settingsDetailSelector.SelectionChanged += new EventHandler(SettingsDetail_SelectionChanged);
            settingsDetailSelector.VAnchor = VAnchor.ParentCenter;
            settingsDetailSelector.Margin = new BorderDouble(5, 3);

            this.AddChild(settingsDetailSelector);
			this.AddChild(GetSliceOptionsMenuDropList());
        }

		DropDownMenu GetSliceOptionsMenuDropList()
		{
			if (sliceOptionsMenuDropList == null)
			{
				sliceOptionsMenuDropList = new DropDownMenu(LocalizedString.Get("Options  "));
				sliceOptionsMenuDropList.HoverColor = new RGBA_Bytes(0, 0, 0, 50);
				sliceOptionsMenuDropList.NormalColor = new RGBA_Bytes(0, 0, 0, 0);
				sliceOptionsMenuDropList.BorderColor = new RGBA_Bytes(0, 0, 0, 0);
				sliceOptionsMenuDropList.BackgroundColor = new RGBA_Bytes(0, 0, 0, 0);
				sliceOptionsMenuDropList.BorderWidth = 1;
				sliceOptionsMenuDropList.VAnchor |= VAnchor.ParentCenter;
				sliceOptionsMenuDropList.BorderColor = ActiveTheme.Instance.SecondaryTextColor;
				sliceOptionsMenuDropList.SelectionChanged += new EventHandler(MenuDropList_SelectionChanged);

				SetMenuItems();
			}

			return sliceOptionsMenuDropList;
		}

		void MenuDropList_SelectionChanged(object sender, EventArgs e)
		{
			string menuSelection = ((DropDownMenu)sender).SelectedValue;
			foreach (Tuple<string, Func<bool>> item in slicerOptionsMenuItems)
			{
				// if the menu we select is this one
				if (item.Item1 == menuSelection)
				{
					// call its function
					item.Item2();
				}
			}
		}

		void SetMenuItems()
		{
			string importTxt = LocalizedString.Get("Import");
			string importTxtFull = string.Format("{0}", importTxt);
			string exportTxt = LocalizedString.Get("Export");
			string exportTxtFull = string.Format("{0}", exportTxt);
			//Set the name and callback function of the menu items
			slicerOptionsMenuItems = new TupleList<string, Func<bool>> 
			{
				{importTxtFull, ImportQueueMenu_Click},
				{exportTxtFull, ExportQueueMenu_Click},
			};

			//Add the menu items to the menu itself
			foreach (Tuple<string, Func<bool>> item in slicerOptionsMenuItems)
			{
				sliceOptionsMenuDropList.AddItem(item.Item1);
			}
		}

		bool ImportQueueMenu_Click()
		{
			UiThread.RunOnIdle((state) =>
				{
					ActiveSliceSettings.Instance.LoadSettingsFromIni(state);
				});
			return true;
		}

		bool ExportQueueMenu_Click()
		{
			UiThread.RunOnIdle((state) =>
				{
					ActiveSliceSettings.Instance.SaveAs();
				});
			return true;
		}

        private void SettingsDetail_SelectionChanged(object sender, EventArgs e)
        {
            RebuildSlicerSettings(null, null);
        }

        void RebuildSlicerSettings(object sender, EventArgs e)
        {
            UserSettings.Instance.set(SliceSettingsShowHelpEntry, showHelpBox.Checked.ToString().ToLower());
            UserSettings.Instance.set(SliceSettingsLevelEntry, settingsDetailSelector.SelectedValue);

            ApplicationController.Instance.ReloadAdvancedControlsPanel();
        }

        public string SelectedValue 
        {
            get { return settingsDetailSelector.SelectedValue; }
        }

        public bool ShowingHelp
        {
            get { return showHelpBox.Checked; }
        }
    }

    public class SliceSettingsWidget : GuiWidget
    {
        static List<string> settingToReloadUiWhenChanged = new List<string>() 
        {
            "has_fan", 
            "has_heated_bed", 
            "has_sd_card_reader",
            "extruder_count",
            "extruders_share_temperature",
        };

        TextImageButtonFactory buttonFactory = new TextImageButtonFactory();
        SliceSettingsDetailControl sliceSettingsDetailControl;


        TabControl categoryTabs;
        AltGroupBox noConnectionMessageContainer;
        FlowLayoutWidget settingsControlBar;
		FlowLayoutWidget settingsSaveBar;

        public SliceSettingsWidget(SliceSettingsWidgetUiState uiState)
        {
            int minSettingNameWidth = (int)(190 * TextWidget.GlobalPointSizeScaleRatio + .5);
            buttonFactory.FixedHeight = 20;
            buttonFactory.fontSize = 10;
            buttonFactory.normalFillColor = RGBA_Bytes.White;
            buttonFactory.normalTextColor = RGBA_Bytes.DarkGray;

            FlowLayoutWidget pageTopToBottomLayout = new FlowLayoutWidget(FlowDirection.TopToBottom, vAnchor: Agg.UI.VAnchor.ParentTop);
            pageTopToBottomLayout.AnchorAll();
            pageTopToBottomLayout.Padding = new BorderDouble(3, 0);
            this.AddChild(pageTopToBottomLayout);

            settingsControlBar = new SettingsControlBar();
            pageTopToBottomLayout.AddChild(settingsControlBar);

			settingsSaveBar = new SliceSettingsSaveBar();
			settingsSaveBar.Visible = false;
			pageTopToBottomLayout.AddChild(settingsSaveBar);

            noConnectionMessageContainer = new AltGroupBox(new TextWidget(LocalizedString.Get("No Printer Selected"), pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor));
            noConnectionMessageContainer.Margin = new BorderDouble(top: 10);
            noConnectionMessageContainer.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            noConnectionMessageContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            noConnectionMessageContainer.Height = 90;

            string noConnectionString = LocalizedString.Get("No printer is currently selected. Please select a printer to edit slice settings.");
            noConnectionString += "\n\n" + LocalizedString.Get("NOTE: You need to select a printer, but do not need to connect to it.");
            TextWidget noConnectionMessage = new TextWidget(noConnectionString, pointSize:10);
            noConnectionMessage.Margin = new BorderDouble(5);
            noConnectionMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            noConnectionMessage.VAnchor = VAnchor.ParentCenter;

            noConnectionMessageContainer.AddChild(noConnectionMessage);
            pageTopToBottomLayout.AddChild(noConnectionMessageContainer);

            categoryTabs = new TabControl();
            categoryTabs.TabBar.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            categoryTabs.Margin = new BorderDouble(top: 8);
            categoryTabs.AnchorAll();

            sliceSettingsDetailControl = new SliceSettingsDetailControl();

            List<TabBar> sideTabBarsListForLayout = new List<TabBar>();
            for (int categoryIndex = 0; categoryIndex < SliceSettingsOrganizer.Instance.UserLevels[UserLevel].CategoriesList.Count; categoryIndex++)
            {
                OrganizerCategory category = SliceSettingsOrganizer.Instance.UserLevels[UserLevel].CategoriesList[categoryIndex];
                string categoryPageLabel = LocalizedString.Get(category.Name);
                TabPage categoryPage = new TabPage(categoryPageLabel);
                SimpleTextTabWidget textTabWidget = new SimpleTextTabWidget(categoryPage, category.Name + " Tab", 16,
                        ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes());
                categoryPage.AnchorAll();
                categoryTabs.AddTab(textTabWidget);

                TabControl sideTabs = CreateSideTabsAndPages(minSettingNameWidth, category, uiState);
                sideTabBarsListForLayout.Add(sideTabs.TabBar);

                categoryPage.AddChild(sideTabs);
            }

            categoryTabs.TabBar.AddChild(new HorizontalSpacer());
            categoryTabs.TabBar.AddChild(sliceSettingsDetailControl);

            if (sliceSettingsDetailControl.SelectedValue == "Advanced" && ActivePrinterProfile.Instance.ActiveSliceEngineType == ActivePrinterProfile.SlicingEngineTypes.Slic3r)
            {
                TabPage extraSettingsPage = new TabPage("Other");
                SimpleTextTabWidget extraSettingsTextTabWidget = new SimpleTextTabWidget(extraSettingsPage, "Other Tab", 16,
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

            if (sideTabBarsListForLayout.Count == 1)
            {
                sideTabBarsListForLayout[0].MinimumSize = new Vector2(0, 0);
                sideTabBarsListForLayout[0].Width = 0;
            }

            pageTopToBottomLayout.AddChild(categoryTabs);
            AddHandlers();
            SetVisibleControls();

            if (!categoryTabs.SelectTab(uiState.selectedCategory.name))
            {
                categoryTabs.SelectTab(uiState.selectedCategory.index);
            }
			this.AnchorAll();
			SetStatusDisplay();
        }


		void onCommitStatusChanged(object sender, EventArgs e)
		{
			SetStatusDisplay();
		}


		void SetStatusDisplay()
		{            
			if (ActiveSliceSettings.Instance.HasUncommittedChanges)
			{   
				settingsSaveBar.Visible = true;                
			}
			else
			{
				settingsSaveBar.Visible = false;               
			}         
		}

        public string UserLevel
        {
            get
            {
                if (SliceSettingsOrganizer.Instance.UserLevels.ContainsKey(sliceSettingsDetailControl.SelectedValue))
                {
                    return sliceSettingsDetailControl.SelectedValue;
                }

                return "Simple";
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
            PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(APP_onPrinterStatusChanged, ref unregisterEvents);
            PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
			ActiveSliceSettings.Instance.CommitStatusChanged.RegisterEvent(onCommitStatusChanged, ref unregisterEvents);
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
        private TabControl CreateSideTabsAndPages(int minSettingNameWidth, OrganizerCategory category, SliceSettingsWidgetUiState uiState)
        {
            TabControl groupTabs = new TabControl(Orientation.Vertical);
            groupTabs.Margin = new BorderDouble(0, 0, 0, 5);
            groupTabs.TabBar.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            foreach (OrganizerGroup group in category.GroupsList)
            {
                tabIndexForItem = 0;
                string groupTabLabel = LocalizedString.Get(group.Name);
                TabPage groupTabPage = new TabPage(groupTabLabel);
                groupTabPage.HAnchor = HAnchor.ParentLeftRight;

                SimpleTextTabWidget groupTabWidget = new SimpleTextTabWidget(groupTabPage, group.Name + " Tab", 14,
                   ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes());
                groupTabWidget.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

                FlowLayoutWidget subGroupLayoutTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
                subGroupLayoutTopToBottom.AnchorAll();

                bool needToAddSubGroup = false;
                foreach (OrganizerSubGroup subGroup in group.SubGroupsList)
                {
                    string subGroupTitle = subGroup.Name;
                    int numberOfCopies = 1;
                    if (subGroup.Name == "Extruder X")
                    {
                        numberOfCopies = ActiveSliceSettings.Instance.ExtruderCount;
                    }

                    for (int copyIndex = 0; copyIndex < numberOfCopies; copyIndex++)
                    {
                        if (subGroup.Name == "Extruder X")
                        {
                            subGroupTitle = "{0} {1}".FormatWith("Extruder".Localize(), copyIndex + 1);
                        }

                        bool addedSettingToSubGroup = false;
                        FlowLayoutWidget topToBottomSettings = new FlowLayoutWidget(FlowDirection.TopToBottom);
                        topToBottomSettings.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

                        foreach (OrganizerSettingsData settingInfo in subGroup.SettingDataList)
                        {
                            if (ActivePrinterProfile.Instance.ActiveSliceEngine.MapContains(settingInfo.SlicerConfigName))
                            {
                                addedSettingToSubGroup = true;
                                GuiWidget controlsForThisSetting = CreateSettingInfoUIControls(settingInfo, minSettingNameWidth, copyIndex);
                                topToBottomSettings.AddChild(controlsForThisSetting);

                                if (sliceSettingsDetailControl.ShowingHelp)
                                {
                                    AddInHelpText(topToBottomSettings, settingInfo);
                                }
                            }
                        }

                        if (addedSettingToSubGroup)
                        {
                            needToAddSubGroup = true;
                            string groupBoxLabel = subGroupTitle;
                            AltGroupBox groupBox = new AltGroupBox(groupBoxLabel);
                            groupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                            groupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
                            groupBox.AddChild(topToBottomSettings);
                            groupBox.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                            groupBox.Margin = new BorderDouble(3, 3, 3, 0);

                            subGroupLayoutTopToBottom.AddChild(groupBox);
                        }
                    }
                }

                if (needToAddSubGroup)
                {
                    SliceSettingListControl scrollOnGroupTab = new SliceSettingListControl();

                    subGroupLayoutTopToBottom.VAnchor = VAnchor.FitToChildren;
                    subGroupLayoutTopToBottom.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

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

        public class WrappedTextWidget : GuiWidget
        {
            String unwrappedText;
            TextWidget textWidget;
            double pointSize;
            public WrappedTextWidget(string text, double startingWidth, 
                double pointSize = 12, Justification justification = Justification.Left, 
                RGBA_Bytes textColor = new RGBA_Bytes(), bool ellipsisIfClipped = true, bool underline = false, RGBA_Bytes backgroundColor = new RGBA_Bytes())
            {
                this.pointSize = pointSize;
                textWidget = new TextWidget(text, 0, 0, pointSize, justification, textColor, ellipsisIfClipped, underline, backgroundColor);
                textWidget.AutoExpandBoundsToText = true;
                textWidget.HAnchor = HAnchor.ParentLeft;
                textWidget.VAnchor = VAnchor.ParentCenter;
                unwrappedText = text;
                HAnchor = HAnchor.ParentLeftRight;
                VAnchor = VAnchor.FitToChildren;
                AddChild(textWidget);

                Width = startingWidth;
            }

            public override void OnBoundsChanged(EventArgs e)
            {
                AdjustTextWrap();
                base.OnBoundsChanged(e);
            }

            private void AdjustTextWrap()
            {
                if (textWidget != null)
                {
                    if (Width > 0)
                    {
                        EnglishTextWrapping wrapper = new EnglishTextWrapping(pointSize);
                        string wrappedMessage = wrapper.InsertCRs(unwrappedText, Width);
                        textWidget.Text = wrappedMessage;
                    }
                }
            }
        }

        private void AddInHelpText(FlowLayoutWidget topToBottomSettings, OrganizerSettingsData settingInfo)
        {
            FlowLayoutWidget allText = new FlowLayoutWidget(FlowDirection.TopToBottom);
            allText.HAnchor = HAnchor.ParentLeftRight;
            double textRegionWidth = 380 * TextWidget.GlobalPointSizeScaleRatio;
            allText.Margin = new BorderDouble(3);
            allText.Padding = new BorderDouble(5);
            allText.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;

            double helpPointSize = 10;
            
            GuiWidget helpWidget = new WrappedTextWidget(settingInfo.HelpText, textRegionWidth, pointSize: helpPointSize, textColor: RGBA_Bytes.White);
            helpWidget.Margin = new BorderDouble(5, 0, 0, 0);
            //helpWidget.HAnchor = HAnchor.ParentLeft;
            allText.AddChild(helpWidget);

            allText.MinimumSize = new Vector2(textRegionWidth, allText.MinimumSize.y);
            topToBottomSettings.AddChild(allText);
        }

        private TabControl CreateExtraSettingsSideTabsAndPages(int minSettingNameWidth, TabControl categoryTabs, out int count)
        {
            count = 0;
            TabControl sideTabs = new TabControl(Orientation.Vertical);
            sideTabs.Margin = new BorderDouble(0, 0, 0, 5);
            sideTabs.TabBar.BorderColor = RGBA_Bytes.White;
            {
                TabPage groupTabPage = new TabPage("Extra Settings");
                SimpleTextTabWidget groupTabWidget = new SimpleTextTabWidget(groupTabPage, "Extra Settings Tab", 14,
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
                        GuiWidget controlsForThisSetting = CreateSettingInfoUIControls(settingInfo, minSettingNameWidth, 0);
                        topToBottomSettings.AddChild(controlsForThisSetting);
                        count++;
                    }
                }

                AltGroupBox groupBox = new AltGroupBox(LocalizedString.Get("Extra"));
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
            TextWidget dataTypeInfo = new TextWidget(extraSettings, pointSize: 8);
            dataTypeInfo.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            dataTypeInfo.Margin = new BorderDouble(5, 0);
            return dataTypeInfo;
        }

        static Dictionary<string, RootedObjectEventHandler> functionsToCallOnChange = new Dictionary<string, RootedObjectEventHandler>();
        public static void RegisterForSettingsChange(string settingName, EventHandler functionToCallOnChange, ref EventHandler functionThatWillBeCalledToUnregisterEvent)
        {
            if (!functionsToCallOnChange.ContainsKey(settingName))
            {
                functionsToCallOnChange.Add(settingName, new RootedObjectEventHandler());
            }

            RootedObjectEventHandler rootedEvent = functionsToCallOnChange[settingName];
            rootedEvent.RegisterEvent(functionToCallOnChange, ref functionThatWillBeCalledToUnregisterEvent);
        }

        private void CallEventsOnSettingsChange(OrganizerSettingsData settingData)
        {
            foreach (KeyValuePair<string, RootedObjectEventHandler> keyValue in functionsToCallOnChange)
            {
                if (keyValue.Key == settingData.SlicerConfigName)
                {
                    keyValue.Value.CallEvents(null, null);
                }
            }

            if (settingToReloadUiWhenChanged.Contains(settingData.SlicerConfigName))
            {
                ApplicationController.Instance.ReloadAll(null, null);
            }
        }

#if DO_IN_PLACE_EDIT        
        public static int SettingsIndexBeingEdited = 0;
#endif
        private GuiWidget CreateSettingInfoUIControls(OrganizerSettingsData settingData, double minSettingNameWidth, int extruderIndex)
        {
            GuiWidget container = new GuiWidget();
            FlowLayoutWidget leftToRightLayout = new FlowLayoutWidget();

            bool addQualityOverlay = false;
            bool addMaterialOverlay = false;

            RGBA_Bytes qualityOverlayColor = new RGBA_Bytes(255, 255, 0, 40);
            RGBA_Bytes materialOverlayColor = new RGBA_Bytes(255, 127, 0, 40);


            if (ActiveSliceSettings.Instance.Contains(settingData.SlicerConfigName))
            {
                int intEditWidth = (int)(60 * TextWidget.GlobalPointSizeScaleRatio + .5);
                int doubleEditWidth = (int)(60 * TextWidget.GlobalPointSizeScaleRatio + .5);
                if (settingData.QuickMenuSettings.Count > 0)
                {
                    doubleEditWidth = (int)(35 * TextWidget.GlobalPointSizeScaleRatio + .5);
                }
                int vectorXYEditWidth = (int)(60 * TextWidget.GlobalPointSizeScaleRatio + .5);
                int multiLineEditHeight = (int)(60 * TextWidget.GlobalPointSizeScaleRatio + .5);

                string sliceSettingValue = ActiveSliceSettings.Instance.GetActiveValue(settingData.SlicerConfigName);
                leftToRightLayout.Margin = new BorderDouble(0, 2);
                leftToRightLayout.Padding = new BorderDouble(3);
                leftToRightLayout.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

                if (settingData.DataEditType != OrganizerSettingsData.DataEditTypes.MULTI_LINE_TEXT)
                {
                    string convertedNewLines = settingData.PresentationName.Replace("\\n ", "\n");
                    convertedNewLines = convertedNewLines.Replace("\\n", "\n");
                    convertedNewLines = LocalizedString.Get(convertedNewLines);
                    TextWidget settingName = new TextWidget(convertedNewLines, pointSize:10);
                    settingName.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                    settingName.VAnchor = Agg.UI.VAnchor.ParentCenter;

#if DO_IN_PLACE_EDIT
                    if (SettingsIndexBeingEdited != 0)
                    {
                        if (ActiveSliceSettings.Instance.SettingExistsInLayer(settingData.SlicerConfigName, SettingsIndexBeingEdited))
                        {
                            CheckBox removeFromSettingCheckBox = new CheckBox("");
                            removeFromSettingCheckBox.Checked = true;
                            removeFromSettingCheckBox.VAnchor = VAnchor.ParentCenter;
                            leftToRightLayout.AddChild(removeFromSettingCheckBox);
                        }
                        else
                        {
                            CheckBox addToSettingCheckBox = new CheckBox("");
                            addToSettingCheckBox.VAnchor = VAnchor.ParentCenter;
                            leftToRightLayout.AddChild(addToSettingCheckBox);
                        }
                    }
#endif

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
                            intEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
                            {
                                SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString());
                                CallEventsOnSettingsChange(settingData);
                            };
                            leftToRightLayout.AddChild(intEditWidget);
                            leftToRightLayout.AddChild(getSettingInfoData(settingData));
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.DOUBLE:
                        {
                            double currentValue = 0;
                            double.TryParse(sliceSettingValue, out currentValue);
                            MHNumberEdit doubleEditWidget = new MHNumberEdit(currentValue, allowNegatives: true, allowDecimals: true, pixelWidth: doubleEditWidth, tabIndex: tabIndexForItem++);
                            doubleEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
                            {
                                SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString());
                                CallEventsOnSettingsChange(settingData);
                            };
                            leftToRightLayout.AddChild(doubleEditWidget);
                            leftToRightLayout.AddChild(getSettingInfoData(settingData));
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.POSITIVE_DOUBLE:
                        {
                            FlowLayoutWidget content = new FlowLayoutWidget();

                            double currentValue = 0;
                            double.TryParse(sliceSettingValue, out currentValue);
                            MHNumberEdit doubleEditWidget = new MHNumberEdit(currentValue, allowDecimals: true, pixelWidth: doubleEditWidth, tabIndex: tabIndexForItem++);
                            doubleEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
                            {
                                SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString());
                                CallEventsOnSettingsChange(settingData);
                            };

                            content.AddChild(doubleEditWidget);
                            content.AddChild(getSettingInfoData(settingData));

                            if (settingData.QuickMenuSettings.Count > 0)
                            {
                                leftToRightLayout.AddChild(CreateQuickMenu(settingData, content, doubleEditWidget.ActuallNumberEdit.InternalTextEditWidget));
                            }
                            else
                            {
                                leftToRightLayout.AddChild(content);
                            }
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.OFFSET:
                        {
                            double currentValue = 0;
                            double.TryParse(sliceSettingValue, out currentValue);
                            MHNumberEdit doubleEditWidget = new MHNumberEdit(currentValue, allowDecimals: true, allowNegatives: true, pixelWidth: doubleEditWidth, tabIndex: tabIndexForItem++);
                            doubleEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
                            {
                                SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString());
                                CallEventsOnSettingsChange(settingData);
                            };
                            leftToRightLayout.AddChild(doubleEditWidget);
                            leftToRightLayout.AddChild(getSettingInfoData(settingData));
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.DOUBLE_OR_PERCENT:
                        {
                            FlowLayoutWidget content = new FlowLayoutWidget();
                            
                            MHTextEditWidget stringEdit = new MHTextEditWidget(sliceSettingValue, pixelWidth: doubleEditWidth - 2, tabIndex: tabIndexForItem++);
                            stringEdit.ActualTextEditWidget.EditComplete += (sender, e) =>
                            {
                                TextEditWidget textEditWidget = (TextEditWidget)sender;
                                string text = textEditWidget.Text;
                                text = text.Trim();
                                bool isPercent = text.Contains("%");
                                if (isPercent)
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
                                CallEventsOnSettingsChange(settingData);
                            };

                            content.AddChild(stringEdit);
                            content.AddChild(getSettingInfoData(settingData));

                            if (settingData.QuickMenuSettings.Count > 0)
                            {
                                leftToRightLayout.AddChild(CreateQuickMenu(settingData, content, stringEdit.ActualTextEditWidget.InternalTextEditWidget));
                            }
                            else
                            {
                                leftToRightLayout.AddChild(content);
                            }
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
                                CallEventsOnSettingsChange(settingData);
                            };
                            leftToRightLayout.AddChild(checkBoxWidget);
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.STRING:
                        {
                            MHTextEditWidget stringEdit = new MHTextEditWidget(sliceSettingValue, pixelWidth: 120, tabIndex: tabIndexForItem++);
                            stringEdit.ActualTextEditWidget.EditComplete += (sender, e) =>
                            {
                                SaveSetting(settingData.SlicerConfigName, ((TextEditWidget)sender).Text);
                                CallEventsOnSettingsChange(settingData);
                            };
                            leftToRightLayout.AddChild(stringEdit);
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.MULTI_LINE_TEXT:
                        {
                            string convertedNewLines = sliceSettingValue.Replace("\\n", "\n");
                            MHTextEditWidget stringEdit = new MHTextEditWidget(convertedNewLines, pixelWidth: 320, pixelHeight: multiLineEditHeight, multiLine: true, tabIndex: tabIndexForItem++);
                            stringEdit.ActualTextEditWidget.EditComplete += (sender, e) =>
                            {
                                SaveSetting(settingData.SlicerConfigName, ((TextEditWidget)sender).Text.Replace("\n", "\\n"));
                                CallEventsOnSettingsChange(settingData);
                            };
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
                                    CallEventsOnSettingsChange(settingData);
                                };
                            }
                            leftToRightLayout.AddChild(selectableOptions);
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.HARDWARE_PRESENT:
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
                                    // Now show all of the settings that this control is associated with.
                                }
                                else
                                {
                                    SaveSetting(settingData.SlicerConfigName, "0");
                                    // Now hide all of the settings that this control is associated with.
                                }
                                CallEventsOnSettingsChange(settingData);
                            };
                            leftToRightLayout.AddChild(checkBoxWidget);
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

                            xEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
                            {
                                SaveSetting(settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "," + yEditWidget.ActuallNumberEdit.Value.ToString());
                                CallEventsOnSettingsChange(settingData);
                            };

                            leftToRightLayout.AddChild(xEditWidget);
                            leftToRightLayout.AddChild(new HorizontalSpacer());

                            yEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
                                {
                                    SaveSetting(settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "," + yEditWidget.ActuallNumberEdit.Value.ToString());
                                    CallEventsOnSettingsChange(settingData);
                                };
                            leftToRightLayout.AddChild(yEditWidget);
                        }
                        break;

                    case OrganizerSettingsData.DataEditTypes.OFFSET2:
                        {
                            Vector2 offset = ActiveSliceSettings.Instance.GetOffset(extruderIndex);
                            MHNumberEdit xEditWidget = new MHNumberEdit(offset.x, allowDecimals: true, allowNegatives: true, pixelWidth: vectorXYEditWidth, tabIndex: tabIndexForItem++);
                            MHNumberEdit yEditWidget = new MHNumberEdit(offset.y, allowDecimals: true, allowNegatives: true, pixelWidth: vectorXYEditWidth, tabIndex: tabIndexForItem++);
                            {
                                xEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
                                {
                                    int extruderIndexLocal = extruderIndex;
                                    SaveCommaSeparatedIndexSetting(extruderIndexLocal, settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "x" + yEditWidget.ActuallNumberEdit.Value.ToString());
                                    CallEventsOnSettingsChange(settingData);
                                };
                                leftToRightLayout.AddChild(xEditWidget);
                                leftToRightLayout.AddChild(new HorizontalSpacer());
                            }
                            {
                                yEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
                                {
                                    int extruderIndexLocal = extruderIndex;
                                    SaveCommaSeparatedIndexSetting(extruderIndexLocal, settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "x" + yEditWidget.ActuallNumberEdit.Value.ToString());
                                    CallEventsOnSettingsChange(settingData);
                                };
                                leftToRightLayout.AddChild(yEditWidget);
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
                        if (ApplicationController.Instance.EditQualityPresetsWindow == null)
                        {
                            ApplicationController.Instance.EditQualityPresetsWindow = new SlicePresetsWindow(ReloadOptions, "Quality", "quality", false, ActivePrinterProfile.Instance.ActiveQualitySettingsID);
                            ApplicationController.Instance.EditQualityPresetsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { ApplicationController.Instance.EditQualityPresetsWindow = null; };
                        }
                        else
                        {
                            ApplicationController.Instance.EditQualityPresetsWindow.ChangeToSlicePresetFromID(ActivePrinterProfile.Instance.ActiveQualitySettingsID);
                            ApplicationController.Instance.EditQualityPresetsWindow.BringToFront();
                        }
                    };
                }
                else if (addMaterialOverlay)
                {
                    overlay.OverlayColor = materialOverlayColor;
                    clickToEdit.OverlayColor = materialOverlayColor;
                    editButton.Click += (sender, e) =>
                    {
                        if (ApplicationController.Instance.EditMaterialPresetsWindow == null)
                        {
                            ApplicationController.Instance.EditMaterialPresetsWindow = new SlicePresetsWindow(ReloadOptions, "Material", "material", false, ActivePrinterProfile.Instance.GetMaterialSetting(1));
                            ApplicationController.Instance.EditMaterialPresetsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { ApplicationController.Instance.EditMaterialPresetsWindow = null; };
                        }
                        else
                        {
                            ApplicationController.Instance.EditMaterialPresetsWindow.ChangeToSlicePresetFromID(ActivePrinterProfile.Instance.GetMaterialSetting(1));
                            ApplicationController.Instance.EditMaterialPresetsWindow.BringToFront();
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

        private GuiWidget CreateQuickMenu(OrganizerSettingsData settingData, GuiWidget content, InternalTextEditWidget internalTextWidget)
        {
            string sliceSettingValue = ActiveSliceSettings.Instance.GetActiveValue(settingData.SlicerConfigName); 
            FlowLayoutWidget totalContent = new FlowLayoutWidget();

            StyledDropDownList selectableOptions = new StyledDropDownList("Custom", maxHeight: 200);
            selectableOptions.Margin = new BorderDouble(0, 0, 10, 0);

            foreach (QuickMenuNameValue nameValue in settingData.QuickMenuSettings)
            {
                string valueLocal = nameValue.Value;

                MenuItem newItem = selectableOptions.AddItem(nameValue.MenuName);
                if (sliceSettingValue == valueLocal)
                {
                    selectableOptions.SelectedLabel = nameValue.MenuName;
                }

                newItem.Selected += (sender, e) =>
                {
                    SaveSetting(settingData.SlicerConfigName, valueLocal);
                    CallEventsOnSettingsChange(settingData);
                    internalTextWidget.Text = valueLocal;
                };
            }

            // put in the custom menu to allow direct editing
            MenuItem customMenueItem = selectableOptions.AddItem("Custom");

            totalContent.AddChild(selectableOptions);
            content.VAnchor = VAnchor.ParentCenter;
            totalContent.AddChild(content);

            internalTextWidget.EditComplete += (sender, e) =>
            {
                bool foundSetting = false;
                foreach (QuickMenuNameValue nameValue in settingData.QuickMenuSettings)
                {
                    string localName = nameValue.MenuName;
                    string newSliceSettingValue = ActiveSliceSettings.Instance.GetActiveValue(settingData.SlicerConfigName);
                    if (newSliceSettingValue == nameValue.Value)
                    {
                        selectableOptions.SelectedLabel = localName;
                        foundSetting = true;
                        break;
                    }
                }

                if (!foundSetting)
                {
                    selectableOptions.SelectedLabel = "Custom";
                }
            };

            return totalContent;
        }

        private void SaveCommaSeparatedIndexSetting(int extruderIndexLocal, string slicerConfigName, string newSingleValue)
        {
            string[] settings = ActiveSliceSettings.Instance.GetActiveValue(slicerConfigName).Split(',');
            if (settings.Length > extruderIndexLocal)
            {
                settings[extruderIndexLocal] = newSingleValue;
            }
            else
            {
                string[] newSettings = new string[extruderIndexLocal + 1];
                for (int i = 0; i < extruderIndexLocal + 1; i++)
                {
                    newSettings[i] = "";
                    if (i < settings.Length)
                    {
                        newSettings[i] = settings[i];
                    }
                    else if(i == extruderIndexLocal)
                    {
                        newSettings[i] = newSingleValue;
                    }
                }

                settings = newSettings;
            }

            string newValue = string.Join(",", settings);
            SaveSetting(slicerConfigName, newValue);
        }

        protected void ReloadOptions(object sender, EventArgs e)
        {
            ApplicationController.Instance.ReloadAdvancedControlsPanel();
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
            RoundedRect rect = new RoundedRect(LocalBounds, 0);
            graphics2D.Render(rect, new RGBA_Bytes(OverlayColor, 50));
            graphics2D.Render(new Stroke(rect, 3), OverlayColor);
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
