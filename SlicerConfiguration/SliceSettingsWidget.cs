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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SliceSettingsWidget : GuiWidget
	{
		private static List<string> settingToReloadUiWhenChanged = new List<string>()
		{
			"extruder_count",
			"extruders_share_temperature",
			"has_fan",
			"has_heated_bed",
			"has_sd_card_reader",
			"center_part_on_bed",
			"has_hardware_leveling",
			"include_firmware_updater",
			"print_leveling_required_to_print",
			"show_reset_connection",
		};

		private TextImageButtonFactory buttonFactory = new TextImageButtonFactory();
		private SliceSettingsDetailControl sliceSettingsDetailControl;

		private TabControl categoryTabs;
		private AltGroupBox noConnectionMessageContainer;
		private SettingsControlBar settingsControlBar;
		private string activeMaterialPreset;
		private string activeQualityPreset;
		private bool presetChanged = false;

		private Button revertButton;
		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

		public SliceSettingsWidget()
		{
			int minSettingNameWidth = (int)(190 * TextWidget.GlobalPointSizeScaleRatio + .5);
			buttonFactory.FixedHeight = 20 * TextWidget.GlobalPointSizeScaleRatio;
			buttonFactory.fontSize = 10;
			buttonFactory.normalFillColor = RGBA_Bytes.White;
			buttonFactory.normalTextColor = RGBA_Bytes.DarkGray;

			FlowLayoutWidget pageTopToBottomLayout = new FlowLayoutWidget(FlowDirection.TopToBottom, vAnchor: Agg.UI.VAnchor.ParentTop);
			pageTopToBottomLayout.AnchorAll();
			pageTopToBottomLayout.Padding = new BorderDouble(3, 0);
			this.AddChild(pageTopToBottomLayout);

			settingsControlBar = new SettingsControlBar()
			{
				HAnchor = HAnchor.ParentLeftRight,
				BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay,
				Padding = new BorderDouble(8, 12, 8, 8)
			};
			this.activeMaterialPreset = settingsControlBar.activeMaterialPreset;
			this.activeQualityPreset = settingsControlBar.activeQualityPreset;

			pageTopToBottomLayout.AddChild(settingsControlBar);

			noConnectionMessageContainer = new AltGroupBox(new TextWidget(LocalizedString.Get("No Printer Selected"), pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor));
			noConnectionMessageContainer.Margin = new BorderDouble(top: 10);
			noConnectionMessageContainer.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
			noConnectionMessageContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			noConnectionMessageContainer.Height = 90;

			string noConnectionString = LocalizedString.Get("No printer is currently selected. Please select a printer to edit slice settings.");
			noConnectionString += "\n\n" + LocalizedString.Get("NOTE: You need to select a printer, but do not need to connect to it.");
			TextWidget noConnectionMessage = new TextWidget(noConnectionString, pointSize: 10);
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

				TabControl sideTabs = CreateSideTabsAndPages(minSettingNameWidth, category);
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

			// Make sure we are on the right tab when we create this view
			{
				string settingsName = "SliceSettingsWidget_CurrentTab";
				string selectedTab = UserSettings.Instance.get(settingsName);
				categoryTabs.SelectTab(selectedTab);

				categoryTabs.TabBar.TabIndexChanged += (object sender, EventArgs e) =>
				{
					UserSettings.Instance.set(settingsName, categoryTabs.TabBar.SelectedTabName);
				};
			}

			this.AnchorAll();
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

		private event EventHandler unregisterEvents;

		private void AddHandlers()
		{
			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
			ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(APP_onPrinterStatusChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
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

		private int tabIndexForItem = 0;

		private TabControl CreateSideTabsAndPages(int minSettingNameWidth, OrganizerCategory category)
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

				//Side Tabs
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
							bool settingShouldBeShown = CheckIfShouldBeShown(settingInfo);

							if (ActivePrinterProfile.Instance.ActiveSliceEngine.MapContains(settingInfo.SlicerConfigName)
								&& settingShouldBeShown)
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
							string groupBoxLabel = subGroupTitle.Localize();
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

					// Make sure we have the right scroll position when we create this view
					// This code is not working yet. Scroll widgets get a scroll event when the tab becomes visible that is always reseting them.
					// So it is not usefull to enable this and in fact makes the tabs inconsistently scrolled. It is just here for reference. // 2015 04 16, LBB
					if (false)
					{
						string settingsScrollPosition = "SliceSettingsWidget_{0}_{1}_ScrollPosition".FormatWith(category.Name, group.Name);

						UiThread.RunOnIdle(() =>
						{
							int scrollPosition = UserSettings.Instance.Fields.GetInt(settingsScrollPosition, -100000);
							if (scrollPosition != -100000)
							{
								scrollOnGroupTab.ScrollPosition = new Vector2(0, scrollPosition);
							}
						});

						scrollOnGroupTab.ScrollPositionChanged += (object sender, EventArgs e) =>
						{
							if (scrollOnGroupTab.CanSelect)
							{
								UserSettings.Instance.Fields.SetInt(settingsScrollPosition, (int)scrollOnGroupTab.ScrollPosition.y);
							}
						};
					}
				}
			}

			// Make sure we are on the right tab when we create this view
			{
				string settingsTypeName = "SliceSettingsWidget_{0}_CurrentTab".FormatWith(category.Name);
				string selectedTab = UserSettings.Instance.get(settingsTypeName);
				groupTabs.SelectTab(selectedTab);

				groupTabs.TabBar.TabIndexChanged += (object sender, EventArgs e) =>
				{
					UserSettings.Instance.set(settingsTypeName, groupTabs.TabBar.SelectedTabName);
				};
			}

			return groupTabs;
		}

		private static bool CheckIfShouldBeShown(OrganizerSettingsData settingInfo)
		{
			bool settingShouldBeShown = true;
			if (settingInfo.ShowIfSet != null
				&& settingInfo.ShowIfSet != "")
			{
				string showValue = "0";
				string checkName = settingInfo.ShowIfSet;
				if (checkName.StartsWith("!"))
				{
					showValue = "1";
					checkName = checkName.Substring(1);
				}
				string sliceSettingValue = ActiveSliceSettings.Instance.GetActiveValue(checkName);
				if (sliceSettingValue == showValue)
				{
					settingShouldBeShown = false;
				}
			}

			return settingShouldBeShown;
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

				foreach (KeyValuePair<string, SliceSetting> item in ActiveSliceSettings.Instance.DefaultSettings)
				{
					if (!SliceSettingsOrganizer.Instance.Contains(UserLevel, item.Key))
					{
						OrganizerSettingsData settingInfo = new OrganizerSettingsData(item.Key, item.Key, OrganizerSettingsData.DataEditTypes.STRING);
						if (ActivePrinterProfile.Instance.ActiveSliceEngine.MapContains(settingInfo.SlicerConfigName))
						{
							GuiWidget controlsForThisSetting = CreateSettingInfoUIControls(settingInfo, minSettingNameWidth, 0);
							topToBottomSettings.AddChild(controlsForThisSetting);
							count++;
						}
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

		private static Dictionary<string, RootedObjectEventHandler> functionsToCallOnChange = new Dictionary<string, RootedObjectEventHandler>();
		private bool revertVisible;

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

		private GuiWidget CreateSettingInfoUIControls(OrganizerSettingsData settingData, double minSettingNameWidth, int extruderIndex)
		{
			GuiWidget container = new GuiWidget();
			FlowLayoutWidget leftToRightLayout = new FlowLayoutWidget();

			bool isQualityPreset = false;
			bool isMaterialPreset = false;

			RGBA_Bytes qualityOverlayColor = new RGBA_Bytes(255, 255, 0, 108);
			RGBA_Bytes materialOverlayColor = new RGBA_Bytes(255, 127, 0, 108);
			RGBA_Bytes userSettingOverlayColor = new RGBA_Bytes(0, 0, 255, 108);

			this.textImageButtonFactory.normalFillColor = RGBA_Bytes.Transparent;
			this.textImageButtonFactory.FixedHeight = 15 * TextWidget.GlobalPointSizeScaleRatio;
			this.textImageButtonFactory.fontSize = 8;
			this.textImageButtonFactory.borderWidth = 1;
			this.textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
			this.textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

			this.textImageButtonFactory.disabledTextColor = RGBA_Bytes.Gray;
			this.textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.textImageButtonFactory.normalTextColor = ActiveTheme.Instance.SecondaryTextColor;
			this.textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.HAnchor = HAnchor.ParentLeftRight;

			revertButton = textImageButtonFactory.Generate("Revert".ToUpper());
			revertButton.HAnchor = HAnchor.ParentRight;
			revertButton.VAnchor = VAnchor.ParentCenter;
			revertButton.Margin = new BorderDouble(0, 0, 10, 0);

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
					TextWidget settingName = new TextWidget(convertedNewLines, pointSize: 10);
					settingName.TextColor = ActiveTheme.Instance.PrimaryTextColor;
					settingName.VAnchor = Agg.UI.VAnchor.ParentCenter;

					settingName.Width = minSettingNameWidth;
					leftToRightLayout.AddChild(settingName);
				}

				if (ActiveSliceSettings.Instance.SettingExistsInLayer(settingData.SlicerConfigName, 3))
				{
					isMaterialPreset = true;
				}
				else if (ActiveSliceSettings.Instance.SettingExistsInLayer(settingData.SlicerConfigName, 2))
				{
					isQualityPreset = true;
				}

				switch (settingData.DataEditType)
				{
					case OrganizerSettingsData.DataEditTypes.INT:
						{
							int currentValue = 0;
							int.TryParse(sliceSettingValue, out currentValue);
							MHNumberEdit intEditWidget = new MHNumberEdit(currentValue, pixelWidth: intEditWidth, tabIndex: tabIndexForItem++);
							intEditWidget.ToolTipText = settingData.HelpText;
							intEditWidget.ActuallNumberEdit.EnterPressed += (sender, e) =>
							{
								presetChanged = true;
								SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString());
								CreateSliceSettingContainer(container, settingData);
								CallEventsOnSettingsChange(settingData);
							};
							intEditWidget.SelectAllOnFocus = true;

							leftToRightLayout.AddChild(intEditWidget);
							leftToRightLayout.AddChild(getSettingInfoData(settingData));
						}
						break;

					case OrganizerSettingsData.DataEditTypes.DOUBLE:
						{
							double currentValue = 0;
							double.TryParse(sliceSettingValue, out currentValue);
							MHNumberEdit doubleEditWidget = new MHNumberEdit(currentValue, allowNegatives: true, allowDecimals: true, pixelWidth: doubleEditWidth, tabIndex: tabIndexForItem++);
							doubleEditWidget.ToolTipText = settingData.HelpText;
							doubleEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
							{
								presetChanged = true;
								CreateSliceSettingContainer(container, settingData);
								SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString());
								CallEventsOnSettingsChange(settingData);
							};
							doubleEditWidget.SelectAllOnFocus = true;
							leftToRightLayout.AddChild(doubleEditWidget);
							leftToRightLayout.AddChild(getSettingInfoData(settingData));
						}
						break;

					case OrganizerSettingsData.DataEditTypes.POSITIVE_DOUBLE:
						{
							const string multiValuesAreDiffernt = "-";
							FlowLayoutWidget content = new FlowLayoutWidget();

							MHNumberEdit doubleEditWidget = new MHNumberEdit(0, allowDecimals: true, pixelWidth: doubleEditWidth, tabIndex: tabIndexForItem++);
							doubleEditWidget.ToolTipText = settingData.HelpText;
							doubleEditWidget.Name = settingData.PresentationName + " Textbox";

							double currentValue = 0;
							bool ChangesMultipleOtherSettings = settingData.SetSettingsOnChange.Count > 0;
							if (ChangesMultipleOtherSettings)
							{
								bool allTheSame = true;
								string setting = ActiveSliceSettings.Instance.GetActiveValue(settingData.SetSettingsOnChange[0]);
								for (int i = 1; i < settingData.SetSettingsOnChange.Count; i++)
								{
									string nextSetting = ActiveSliceSettings.Instance.GetActiveValue(settingData.SetSettingsOnChange[i]);
									if (setting != nextSetting)
									{
										allTheSame = false;
										break;
									}
								}

								if (allTheSame && setting.EndsWith("mm"))
								{
									double.TryParse(setting.Substring(0, setting.Length - 2), out currentValue);
									doubleEditWidget.ActuallNumberEdit.Value = currentValue;
								}
								else
								{
									doubleEditWidget.ActuallNumberEdit.InternalNumberEdit.Text = multiValuesAreDiffernt;
								}
							}
							else // just set the setting nomrmaly
							{
								double.TryParse(sliceSettingValue, out currentValue);
								doubleEditWidget.ActuallNumberEdit.Value = currentValue;
							}
							doubleEditWidget.ActuallNumberEdit.InternalTextEditWidget.MarkAsStartingState();

							doubleEditWidget.ActuallNumberEdit.EnterPressed += (sender, e) =>
							{
								presetChanged = true;
								NumberEdit numberEdit = (NumberEdit)sender;
								// If this setting sets other settings, then do that.
								if (ChangesMultipleOtherSettings
									&& numberEdit.Text != multiValuesAreDiffernt)
								{
									foreach (string setting in settingData.SetSettingsOnChange)
									{
										SaveSetting(setting, numberEdit.Value.ToString() + "mm");
									}
								}
								CreateSliceSettingContainer(container, settingData);

								// also always save to the local setting
								SaveSetting(settingData.SlicerConfigName, numberEdit.Value.ToString());

								CallEventsOnSettingsChange(settingData);
							};
							doubleEditWidget.SelectAllOnFocus = true;
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
							doubleEditWidget.ToolTipText = settingData.HelpText;
							doubleEditWidget.ActuallNumberEdit.EnterPressed += (sender, e) =>
							 {
								 presetChanged = true;
								 CreateSliceSettingContainer(container, settingData);
								 SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString());
								 CallEventsOnSettingsChange(settingData);
							 };
							doubleEditWidget.SelectAllOnFocus = true;
							leftToRightLayout.AddChild(doubleEditWidget);
							leftToRightLayout.AddChild(getSettingInfoData(settingData));
						}
						break;

					case OrganizerSettingsData.DataEditTypes.DOUBLE_OR_PERCENT:
						{
							FlowLayoutWidget content = new FlowLayoutWidget();

							MHTextEditWidget stringEdit = new MHTextEditWidget(sliceSettingValue, pixelWidth: doubleEditWidth - 2, tabIndex: tabIndexForItem++);
							stringEdit.ToolTipText = settingData.HelpText;
							stringEdit.ActualTextEditWidget.EnterPressed += (sender, e) =>
							{
								presetChanged = true;
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
								CreateSliceSettingContainer(container, settingData);
								SaveSetting(settingData.SlicerConfigName, textEditWidget.Text);
								CallEventsOnSettingsChange(settingData);
							};
							stringEdit.SelectAllOnFocus = true;

							stringEdit.ActualTextEditWidget.InternalTextEditWidget.AllSelected += (sender, e) =>
							{
								// select evrything up to the % (if present)
								InternalTextEditWidget textEditWidget = (InternalTextEditWidget)sender;
								int percentIndex = textEditWidget.Text.IndexOf("%");
								if (percentIndex != -1)
								{
									textEditWidget.SetSelection(0, percentIndex - 1);
								}
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

					case OrganizerSettingsData.DataEditTypes.INT_OR_MM:
						{
							FlowLayoutWidget content = new FlowLayoutWidget();

							MHTextEditWidget stringEdit = new MHTextEditWidget(sliceSettingValue, pixelWidth: doubleEditWidth - 2, tabIndex: tabIndexForItem++);
							stringEdit.ToolTipText = settingData.HelpText;

							string startingText = stringEdit.Text;
							stringEdit.ActualTextEditWidget.EnterPressed += (sender, e) =>
							{
								presetChanged = true;
								TextEditWidget textEditWidget = (TextEditWidget)sender;
								// only validate when we lose focus
								if (!textEditWidget.ContainsFocus)
								{
									string text = textEditWidget.Text;
									text = text.Trim();
									bool isMm = text.Contains("mm");
									if (isMm)
									{
										text = text.Substring(0, text.IndexOf("mm"));
									}
									double result;
									double.TryParse(text, out result);
									text = result.ToString();
									if (isMm)
									{
										text += "mm";
									}
									else
									{
										result = (int)result;
										text = result.ToString();
									}
									textEditWidget.Text = text;
									startingText = stringEdit.Text;
								}
								CreateSliceSettingContainer(container, settingData);
								SaveSetting(settingData.SlicerConfigName, textEditWidget.Text);
								CallEventsOnSettingsChange(settingData);

								// make sure we are still looking for the final validation before saving.
								if (textEditWidget.ContainsFocus)
								{
									UiThread.RunOnIdle(() =>
									{
										string currentText = textEditWidget.Text;
										int cursorIndex = textEditWidget.InternalTextEditWidget.CharIndexToInsertBefore;
										textEditWidget.Text = startingText;
										textEditWidget.InternalTextEditWidget.MarkAsStartingState();
										textEditWidget.Text = currentText;
										textEditWidget.InternalTextEditWidget.CharIndexToInsertBefore = cursorIndex;
									});
								}
							};
							stringEdit.SelectAllOnFocus = true;

							stringEdit.ActualTextEditWidget.InternalTextEditWidget.AllSelected += (sender, e) =>
							{
								// select evrything up to the mm (if present)
								InternalTextEditWidget textEditWidget = (InternalTextEditWidget)sender;
								int mMIndex = textEditWidget.Text.IndexOf("mm");
								if (mMIndex != -1)
								{
									textEditWidget.SetSelection(0, mMIndex - 1);
								}
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
							checkBoxWidget.Name = settingData.PresentationName + " Checkbox";
							checkBoxWidget.ToolTipText = settingData.HelpText;
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
							stringEdit.ToolTipText = settingData.HelpText;
							stringEdit.ActualTextEditWidget.EnterPressed += (sender, e) =>
							{
								presetChanged = true;
								CreateSliceSettingContainer(container, settingData);
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
								presetChanged = true;
								CreateSliceSettingContainer(container, settingData);
								SaveSetting(settingData.SlicerConfigName, ((TextEditWidget)sender).Text.Replace("\n", "\\n"));
								CallEventsOnSettingsChange(settingData);
							};

							leftToRightLayout.AddChild(stringEdit);
						}
						break;

					case OrganizerSettingsData.DataEditTypes.LIST:
						{
							StyledDropDownList selectableOptions = new StyledDropDownList("None", maxHeight: 200);
							selectableOptions.ToolTipText = settingData.HelpText;
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
									presetChanged = true;
									MenuItem menuItem = ((MenuItem)sender);
									CreateSliceSettingContainer(container, settingData);
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
							checkBoxWidget.Name = settingData.PresentationName + " Checkbox";
							checkBoxWidget.ToolTipText = settingData.HelpText;
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
							xEditWidget.ToolTipText = settingData.HelpText;
							xEditWidget.Margin = new BorderDouble(0, 0, 60, 0);
							double currentYValue = 0;
							double.TryParse(xyValueStrings[1], out currentYValue);
							MHNumberEdit yEditWidget = new MHNumberEdit(currentYValue, allowDecimals: true, pixelWidth: vectorXYEditWidth, tabIndex: tabIndexForItem++);
							yEditWidget.ToolTipText = settingData.HelpText;

							xEditWidget.ActuallNumberEdit.EnterPressed += (sender, e) =>
							{
								presetChanged = true;
								CreateSliceSettingContainer(container, settingData);
								SaveSetting(settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "," + yEditWidget.ActuallNumberEdit.Value.ToString());
								CallEventsOnSettingsChange(settingData);
							};
							xEditWidget.SelectAllOnFocus = true;
							leftToRightLayout.AddChild(xEditWidget);

							yEditWidget.ActuallNumberEdit.EnterPressed += (sender, e) =>
							{
								presetChanged = true;
								CreateSliceSettingContainer(container, settingData);
								SaveSetting(settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "," + yEditWidget.ActuallNumberEdit.Value.ToString());
								CallEventsOnSettingsChange(settingData);
							};
							yEditWidget.SelectAllOnFocus = true;
							leftToRightLayout.AddChild(yEditWidget);
						}
						break;

					case OrganizerSettingsData.DataEditTypes.OFFSET2:
						{
							Vector2 offset = ActiveSliceSettings.Instance.GetOffset(extruderIndex);
							MHNumberEdit xEditWidget = new MHNumberEdit(offset.x, allowDecimals: true, allowNegatives: true, pixelWidth: vectorXYEditWidth, tabIndex: tabIndexForItem++);
							xEditWidget.ToolTipText = settingData.HelpText;
							MHNumberEdit yEditWidget = new MHNumberEdit(offset.y, allowDecimals: true, allowNegatives: true, pixelWidth: vectorXYEditWidth, tabIndex: tabIndexForItem++);
							yEditWidget.ToolTipText = settingData.HelpText;
							{
								xEditWidget.ActuallNumberEdit.EnterPressed += (sender, e) =>
								{
									presetChanged = true;
									CreateSliceSettingContainer(container, settingData);
									int extruderIndexLocal = extruderIndex;
									SaveCommaSeparatedIndexSetting(extruderIndexLocal, settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "x" + yEditWidget.ActuallNumberEdit.Value.ToString());
									CallEventsOnSettingsChange(settingData);
								};
								xEditWidget.SelectAllOnFocus = true;
								xEditWidget.Margin = new BorderDouble(0, 0, 60, 0);
								leftToRightLayout.AddChild(xEditWidget);
							}
							{
								yEditWidget.ActuallNumberEdit.EnterPressed += (sender, e) =>
								{
									presetChanged = true;
									CreateSliceSettingContainer(container, settingData);
									int extruderIndexLocal = extruderIndex;
									SaveCommaSeparatedIndexSetting(extruderIndexLocal, settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "x" + yEditWidget.ActuallNumberEdit.Value.ToString());
									CallEventsOnSettingsChange(settingData);
								};
								yEditWidget.SelectAllOnFocus = true;
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
			CreateSliceSettingContainer(container, settingData);
			container.AddChild(leftToRightLayout);

			return container;
		}

		private void CreateSliceSettingContainer(GuiWidget container, OrganizerSettingsData settingData)
		{
			//Initialize all widgets to be added to container
			RGBA_Bytes materialSettingBackgroundColor = new RGBA_Bytes(255, 127, 0, 108);
			RGBA_Bytes userSettingBackgroundColor = new RGBA_Bytes(68, 95, 220, 108);
			RGBA_Bytes qualitySettingBackgroundColor = new RGBA_Bytes(255, 255, 0, 108);

			var presetLabel = container.Children<TextWidget>().FirstOrDefault();

			Button revertButton = textImageButtonFactory.Generate("Revert".ToUpper());
			revertButton.HAnchor = HAnchor.ParentRight;
			revertButton.VAnchor = VAnchor.ParentCenter;
			revertButton.Margin = new BorderDouble(0, 0, 10, 0);

			revertButton.Click += (sender, e) =>
			{
				presetChanged = false;
				var revertButtonTest = container.Children<Button>().FirstOrDefault();
				if (ActiveSliceSettings.Instance.SettingExistsInLayer(settingData.SlicerConfigName, 3))
				{
					presetChanged = false;
					container.BackgroundColor = materialSettingBackgroundColor;
					revertButton.Visible = false;
					presetLabel.Visible = true;
				}
				if (ActiveSliceSettings.Instance.SettingExistsInLayer(settingData.SlicerConfigName, 2))
				{
					presetChanged = false;
					container.BackgroundColor = qualitySettingBackgroundColor;
					revertButton.Visible = false;
					presetLabel.Visible = true;
				}
			};

			container.AddChild(revertButton);

			if (ActiveSliceSettings.Instance.SettingExistsInLayer(settingData.SlicerConfigName, 3))
			{
				if (!presetChanged)
				{
					container.BackgroundColor = materialSettingBackgroundColor;
					container.AddChild(GetOverrideNameWidget(this.activeMaterialPreset));
					revertButton.Visible = false;
				}
				else
				{
					container.BackgroundColor = userSettingBackgroundColor;
					presetLabel.Visible = false;
					revertButton.Visible = true;
				}
			}
			else if (ActiveSliceSettings.Instance.SettingExistsInLayer(settingData.SlicerConfigName, 2))
			{
				if (!presetChanged)
				{
					container.BackgroundColor = qualitySettingBackgroundColor;
					container.AddChild(GetOverrideNameWidget(this.activeQualityPreset));
					revertButton.Visible = false;
				}
				else
				{
					container.BackgroundColor = userSettingBackgroundColor;
					presetLabel.Visible = false;
					revertButton.Visible = true;
				}
			}
			else
			{
				revertButton.Visible = false;
			}
		}

		private static TextWidget GetOverrideNameWidget(string presetName)
		{
			return new TextWidget(presetName)
			{
				HAnchor = HAnchor.ParentRight,
				VAnchor = VAnchor.ParentBottom,
				TextColor = ActiveTheme.Instance.SecondaryTextColor,
				Margin = new BorderDouble(0, 0, 5, 0),
				AutoExpandBoundsToText = true,
				PointSize = 8,
			};
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
					internalTextWidget.OnEditComplete(null);
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
					else if (i == extruderIndexLocal)
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

	internal class SettingPresetClick : ClickWidget
	{
		public RGBA_Bytes OverlayColor;

		public override void OnDraw(Graphics2D graphics2D)
		{
			RoundedRect rect = new RoundedRect(LocalBounds, 0);
			graphics2D.Render(rect, new RGBA_Bytes(OverlayColor, 220));
			base.OnDraw(graphics2D);
		}
	}

	internal class SettingPresetOverlay : GuiWidget
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

	internal class SliceSettingListControl : ScrollableWidget
	{
		private FlowLayoutWidget topToBottomItemList;

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