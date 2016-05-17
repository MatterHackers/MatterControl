/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class NoSettingsWidget : FlowLayoutWidget
	{
		public NoSettingsWidget() : base (FlowDirection.TopToBottom, vAnchor: VAnchor.ParentTop)
		{
			this.AnchorAll();
			this.Padding = new BorderDouble(3, 0);

			var noConnectionMessageContainer = new AltGroupBox(new TextWidget(LocalizedString.Get("No Printer Selected"), pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor));
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
			this.AddChild(noConnectionMessageContainer);
		}
	}

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

		private TabControl topCategoryTabs;
		private AltGroupBox noConnectionMessageContainer;
		internal SettingsControlBar settingsControlBar;

		private TextImageButtonFactory textImageButtonFactory;

		private List<SettingsLayer> layerCascade = null;
		private SettingsLayer persistenceLayer = null;

		private NamedSettingsLayers viewFilter;

		public SliceSettingsWidget(List<SettingsLayer> layerCascade = null, NamedSettingsLayers viewFilter = NamedSettingsLayers.All)
		{
			this.layerCascade = layerCascade;
			this.viewFilter = viewFilter;

			// The last layer of the layerFilters is the target persistence layer
			persistenceLayer = layerCascade?.First() ?? ActiveSliceSettings.Instance.UserLayer;

			textImageButtonFactory = new TextImageButtonFactory();
			textImageButtonFactory.normalFillColor = RGBA_Bytes.Transparent;
			textImageButtonFactory.FixedHeight = 15 * GuiWidget.DeviceScale;
			textImageButtonFactory.fontSize = 8;
			textImageButtonFactory.borderWidth = 1;
			textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
			textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

			this.textImageButtonFactory.disabledTextColor = RGBA_Bytes.Gray;
			this.textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.textImageButtonFactory.normalTextColor = ActiveTheme.Instance.SecondaryTextColor;
			this.textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

			buttonFactory.FixedHeight = 20 * GuiWidget.DeviceScale;
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

			pageTopToBottomLayout.AddChild(settingsControlBar);

			noConnectionMessageContainer = new AltGroupBox(new TextWidget("No Printer Selected".Localize(), pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor));
			noConnectionMessageContainer.Margin = new BorderDouble(top: 10);
			noConnectionMessageContainer.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
			noConnectionMessageContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			noConnectionMessageContainer.Height = 90;

			string noConnectionString = "No printer is currently selected. Please select a printer to edit slice settings.".Localize();
			noConnectionString += "\n\n" + "NOTE: You need to select a printer, but do not need to connect to it.".Localize();
			TextWidget noConnectionMessage = new TextWidget(noConnectionString, pointSize: 10);
			noConnectionMessage.Margin = new BorderDouble(5);
			noConnectionMessage.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			noConnectionMessage.VAnchor = VAnchor.ParentCenter;

			noConnectionMessageContainer.AddChild(noConnectionMessage);
			pageTopToBottomLayout.AddChild(noConnectionMessageContainer);

			topCategoryTabs = new TabControl();
			topCategoryTabs.TabBar.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
			topCategoryTabs.Margin = new BorderDouble(top: 8);
			topCategoryTabs.AnchorAll();

			sliceSettingsDetailControl = new SliceSettingsDetailControl();

			List<TabBar> sideTabBarsListForLayout = new List<TabBar>();
			for (int topCategoryIndex = 0; topCategoryIndex < SliceSettingsOrganizer.Instance.UserLevels[UserLevel].CategoriesList.Count; topCategoryIndex++)
			{
				OrganizerCategory category = SliceSettingsOrganizer.Instance.UserLevels[UserLevel].CategoriesList[topCategoryIndex];
				string categoryPageLabel = category.Name.Localize();
				TabPage categoryPage = new TabPage(categoryPageLabel);
				SimpleTextTabWidget textTabWidget = new SimpleTextTabWidget(categoryPage, category.Name + " Tab", 16,
						ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes());
				categoryPage.AnchorAll();
				topCategoryTabs.AddTab(textTabWidget);

				TabControl sideTabs = CreateSideTabsAndPages(category);
				sideTabBarsListForLayout.Add(sideTabs.TabBar);

				categoryPage.AddChild(sideTabs);
			}

			topCategoryTabs.TabBar.AddChild(new HorizontalSpacer());
			topCategoryTabs.TabBar.AddChild(sliceSettingsDetailControl);

			if (sliceSettingsDetailControl.SelectedValue == "Advanced" && ActiveSliceSettings.Instance.ActiveSliceEngineType() == SlicingEngineTypes.Slic3r)
			{
				TabPage extraSettingsPage = new TabPage("Other");
				SimpleTextTabWidget extraSettingsTextTabWidget = new SimpleTextTabWidget(extraSettingsPage, "Other Tab", 16,
						ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes());
				extraSettingsPage.AnchorAll();
				int count;
				TabControl extraSettingsSideTabs = CreateExtraSettingsSideTabsAndPages(topCategoryTabs, out count);
				if (count > 0)
				{
					topCategoryTabs.AddTab(extraSettingsTextTabWidget);
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

			// check if there is only one left side tab. If so hide the left tabs and expand the content.
			foreach(var tabList in sideTabBarsListForLayout)
			{
				if(tabList.CountVisibleChildren() == 1)
				{
					tabList.MinimumSize = new Vector2(0, 0);
					tabList.Width = 0;
				}
			}

			pageTopToBottomLayout.AddChild(topCategoryTabs);
			AddHandlers();
			SetVisibleControls();

			// Make sure we are on the right tab when we create this view
			{
				string settingsName = "SliceSettingsWidget_CurrentTab";
				string selectedTab = UserSettings.Instance.get(settingsName);
				topCategoryTabs.SelectTab(selectedTab);

				topCategoryTabs.TabBar.TabIndexChanged += (object sender, EventArgs e) =>
				{
					UserSettings.Instance.set(settingsName, topCategoryTabs.TabBar.SelectedTabName);
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
			index = topCategoryTabs.SelectedTabIndex;
			name = topCategoryTabs.SelectedTabName;
		}

		public void CurrentlyActiveGroup(out int index, out string name)
		{
			index = 0;
			name = "";

			TabPage currentPage = topCategoryTabs.GetActivePage();
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
			ActiveSliceSettings.ActivePrinterChanged.RegisterEvent(APP_onPrinterStatusChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
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
			if (ActiveSliceSettings.Instance != null)
			{
				topCategoryTabs.Visible = true;
				settingsControlBar.Visible = true;
				noConnectionMessageContainer.Visible = false;
			}
			else
			{
				topCategoryTabs.Visible = false;
				settingsControlBar.Visible = false;
				noConnectionMessageContainer.Visible = true;
			}
		}

		private int tabIndexForItem = 0;

		private TabControl CreateSideTabsAndPages(OrganizerCategory category)
		{
			TabControl leftSideGroupTabs = new TabControl(Orientation.Vertical);
			leftSideGroupTabs.Margin = new BorderDouble(0, 0, 0, 5);
			leftSideGroupTabs.TabBar.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
			foreach (OrganizerGroup group in category.GroupsList)
			{
				tabIndexForItem = 0;
				string groupTabLabel = group.Name.Localize();
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
						numberOfCopies = ActiveSliceSettings.Instance.ExtruderCount();
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

						this.HAnchor = HAnchor.ParentLeftRight;

						foreach (OrganizerSettingsData settingInfo in subGroup.SettingDataList)
						{
							bool settingShouldBeShown = CheckIfShouldBeShown(settingInfo);

							if (ActiveSliceSettings.Instance.ActiveSliceEngine().MapContains(settingInfo.SlicerConfigName)
								&& settingShouldBeShown)
							{
								addedSettingToSubGroup = true;
								GuiWidget controlsForThisSetting = CreateSettingInfoUIControls(settingInfo, copyIndex);
								topToBottomSettings.AddChild(controlsForThisSetting);

								GuiWidget helpBox = AddInHelpText(topToBottomSettings, settingInfo);
								if (!sliceSettingsDetailControl.ShowingHelp)
								{
									helpBox.Visible = false;
								}
								sliceSettingsDetailControl.ShowHelpChanged += (s, e) =>
								{
									helpBox.Visible = sliceSettingsDetailControl.ShowingHelp;
								};
								topToBottomSettings.AddChild(helpBox);
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
					leftSideGroupTabs.AddTab(groupTabWidget);

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
				leftSideGroupTabs.SelectTab(selectedTab);

				leftSideGroupTabs.TabBar.TabIndexChanged += (object sender, EventArgs e) =>
				{
					UserSettings.Instance.set(settingsTypeName, leftSideGroupTabs.TabBar.SelectedTabName);
				};
			}

			return leftSideGroupTabs;
		}

		private bool CheckIfShouldBeShown(OrganizerSettingsData settingInfo)
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
				string sliceSettingValue = GetActiveValue(checkName, layerCascade);
				if (sliceSettingValue == showValue)
				{
					settingShouldBeShown = false;
				}
			}

			return settingShouldBeShown;
		}

		private GuiWidget AddInHelpText(FlowLayoutWidget topToBottomSettings, OrganizerSettingsData settingInfo)
		{
			FlowLayoutWidget allText = new FlowLayoutWidget(FlowDirection.TopToBottom);
			allText.HAnchor = HAnchor.ParentLeftRight;
			double textRegionWidth = 380 * GuiWidget.DeviceScale;
			allText.Margin = new BorderDouble(0);
			allText.Padding = new BorderDouble(5);
			allText.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;

			double helpPointSize = 10;

			GuiWidget helpWidget = new WrappedTextWidget(settingInfo.HelpText, textRegionWidth, pointSize: helpPointSize, textColor: RGBA_Bytes.White);
			helpWidget.Margin = new BorderDouble(5, 0, 0, 0);
			//helpWidget.HAnchor = HAnchor.ParentLeft;
			allText.AddChild(helpWidget);

			allText.MinimumSize = new Vector2(0, allText.MinimumSize.y);
			return allText;
		}

		private TabControl CreateExtraSettingsSideTabsAndPages(TabControl categoryTabs, out int count)
		{
			int rightContentWidth = (int)(280 * GuiWidget.DeviceScale + .5);
			count = 0;
			TabControl leftSideGroupTabs = new TabControl(Orientation.Vertical);
			leftSideGroupTabs.Margin = new BorderDouble(0, 0, 0, 5);
			leftSideGroupTabs.TabBar.BorderColor = RGBA_Bytes.White;
			{
				TabPage groupTabPage = new TabPage("Extra Settings");
				SimpleTextTabWidget groupTabWidget = new SimpleTextTabWidget(groupTabPage, "Extra Settings Tab", 14,
				   ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes());
				leftSideGroupTabs.AddTab(groupTabWidget);

				FlowLayoutWidget subGroupLayoutTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
				subGroupLayoutTopToBottom.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
				subGroupLayoutTopToBottom.VAnchor = VAnchor.FitToChildren;

				FlowLayoutWidget topToBottomSettings = new FlowLayoutWidget(FlowDirection.TopToBottom);
				topToBottomSettings.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;

				this.HAnchor = HAnchor.ParentLeftRight;

				foreach (var keyValue in ActiveSliceSettings.Instance.BaseLayer)
				{
					if (!SliceSettingsOrganizer.Instance.Contains(UserLevel, keyValue.Key))
					{
						OrganizerSettingsData settingInfo = new OrganizerSettingsData(keyValue.Key, keyValue.Key, OrganizerSettingsData.DataEditTypes.STRING);
						if (ActiveSliceSettings.Instance.ActiveSliceEngine().MapContains(settingInfo.SlicerConfigName))
						{
							GuiWidget controlsForThisSetting = CreateSettingInfoUIControls(settingInfo, 0);
							topToBottomSettings.AddChild(controlsForThisSetting);
							count++;
						}
					}
				}

				AltGroupBox groupBox = new AltGroupBox("Extra".Localize());
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
			return leftSideGroupTabs;
		}

		private GuiWidget GetExtraSettingsWidget(OrganizerSettingsData settingData)
		{
			var nameHolder = new GuiWidget(HAnchor.ParentLeftRight, VAnchor.FitToChildren | VAnchor.ParentCenter)
			{
				Padding = new BorderDouble(5, 0),
				HAnchor = HAnchor.ParentLeftRight,
			};

			nameHolder.AddChild(new WrappedTextWidget(settingData.ExtraSettings.Localize(), 0, pointSize: 8, textColor: ActiveTheme.Instance.PrimaryTextColor));

			return nameHolder;
		}

		public static RootedObjectEventHandler SettingChanged = new RootedObjectEventHandler();

		private void OnSettingsChanged(OrganizerSettingsData settingData)
		{
			SettingChanged.CallEvents(this, new StringEventArgs(settingData.SlicerConfigName));

			if (settingToReloadUiWhenChanged.Contains(settingData.SlicerConfigName))
			{
				ApplicationController.Instance.ReloadAll(null, null);
			}
		}

		private class SettingsRow : FlowLayoutWidget
		{
			public string SettingsKey { get; set; }
			public string SettingsValue { get; set; }

			/// <summary>
			/// Gets or sets the delegate to be invoked when the settings values need to be refreshed. The implementation should 
			/// take the passed in text value and update its editor to reflect the latest value
			/// </summary>
			public Action<string> ValueChanged { get; set; }
			public Action UpdateStyle { get; set; }

			public SettingsRow()
			{
				Margin = new BorderDouble(0, 2);
				Padding = new BorderDouble(3);
				HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			}

			public void RefreshValue(IEnumerable<SettingsLayer> layerFilters)
			{
				string latestValue = GetActiveValue(this.SettingsKey, layerFilters);
				//if(latestValue != SettingsValue)
				{
					ValueChanged?.Invoke(latestValue);
				}

				UpdateStyle?.Invoke();

				SettingsValue = latestValue;
			}
		}

		private static readonly RGBA_Bytes materialSettingBackgroundColor = new RGBA_Bytes(255, 127, 0, 108);
		private static readonly RGBA_Bytes userSettingBackgroundColor = new RGBA_Bytes(68, 95, 220, 108);
		private static readonly RGBA_Bytes qualitySettingBackgroundColor = new RGBA_Bytes(255, 255, 0, 108);

		private static string GetActiveValue(string slicerConfigName, IEnumerable<SettingsLayer> layerCascade)
		{
			return ActiveSliceSettings.Instance.GetActiveValue(slicerConfigName, layerCascade);
		}

		private GuiWidget CreateSettingInfoUIControls(OrganizerSettingsData settingData, double rightContentWidth, int extruderIndex)
		{
			GuiWidget container = new GuiWidget();

			string sliceSettingValue = GetActiveValue(settingData.SlicerConfigName, layerCascade);

			GuiWidget nameArea = new GuiWidget(HAnchor.ParentLeftRight, VAnchor.FitToChildren | VAnchor.ParentCenter);
			var dataArea = new FlowLayoutWidget();
			GuiWidget unitsArea = new GuiWidget(HAnchor.AbsolutePosition, VAnchor.FitToChildren | VAnchor.ParentCenter)
			{
				Width = 50 * GuiWidget.DeviceScale,
			};
			GuiWidget restoreArea = new GuiWidget(HAnchor.AbsolutePosition, VAnchor.FitToChildren | VAnchor.ParentCenter)
			{
				Width = 30 * GuiWidget.DeviceScale,
			};

			var settingsRow = new SettingsRow()
			{
				SettingsKey = settingData.SlicerConfigName,
				SettingsValue = sliceSettingValue,
			};
			settingsRow.AddChild(nameArea);
			settingsRow.AddChild(dataArea);
			settingsRow.AddChild(unitsArea);
			settingsRow.AddChild(restoreArea);

			if (!ActiveSliceSettings.Instance.KnownSettings.Contains(settingData.SlicerConfigName))
			{
				// the setting we think we are adding is not in the config.ini it may have been deprecated
				TextWidget settingName = new TextWidget(String.Format("Setting '{0}' not found in config.ini", settingData.SlicerConfigName));
				settingName.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				//settingName.MinimumSize = new Vector2(minSettingNameWidth, settingName.MinimumSize.y);
				nameArea.AddChild(settingName);
				nameArea.BackgroundColor = RGBA_Bytes.Red;
			}
			else
			{
				int intEditWidth = (int)(60 * GuiWidget.DeviceScale + .5);
				int doubleEditWidth = (int)(60 * GuiWidget.DeviceScale + .5);
				int vectorXYEditWidth = (int)(60 * GuiWidget.DeviceScale + .5);
				int multiLineEditHeight = (int)(120 * GuiWidget.DeviceScale + .5);

				if (settingData.DataEditType != OrganizerSettingsData.DataEditTypes.MULTI_LINE_TEXT)
				{
					var nameHolder = new GuiWidget(HAnchor.ParentLeftRight, VAnchor.FitToChildren | VAnchor.ParentCenter)
					{
						Padding = new BorderDouble(0, 0, 5, 0),
						HAnchor = HAnchor.ParentLeftRight,
					};

					nameHolder.AddChild(new WrappedTextWidget(settingData.PresentationName.Localize(), 0, pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor));

					nameArea.AddChild(nameHolder);
				}

				switch (settingData.DataEditType)
				{
					case OrganizerSettingsData.DataEditTypes.INT:
						{
							FlowLayoutWidget content = new FlowLayoutWidget();
							int currentValue;
							int.TryParse(sliceSettingValue, out currentValue);

							var intEditWidget = new MHNumberEdit(currentValue, pixelWidth: intEditWidth, tabIndex: tabIndexForItem++)
							{
								ToolTipText = settingData.HelpText,
								SelectAllOnFocus = true,
								Name = settingData.PresentationName + " Edit",
							};
							intEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
							{
								SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString());
								settingsRow.UpdateStyle();

								OnSettingsChanged(settingData);
							};

							content.AddChild(intEditWidget);
							unitsArea.AddChild(GetExtraSettingsWidget(settingData));

							if (settingData.QuickMenuSettings.Count > 0)
							{
								dataArea.AddChild(CreateQuickMenu(settingData, content, intEditWidget.ActuallNumberEdit.InternalTextEditWidget));
							}
							else
							{
								dataArea.AddChild(content);
							}

							settingsRow.ValueChanged = (text) =>
							{
								intEditWidget.Text = text;
								OnSettingsChanged(settingData);
							};
						}
						break;

					case OrganizerSettingsData.DataEditTypes.DOUBLE:
						{
							double currentValue;
							double.TryParse(sliceSettingValue, out currentValue);

							var doubleEditWidget = new MHNumberEdit(currentValue, allowNegatives: true, allowDecimals: true, pixelWidth: doubleEditWidth, tabIndex: tabIndexForItem++)
							{
								ToolTipText = settingData.HelpText,
								SelectAllOnFocus = true
							};
							doubleEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
							{
								SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString());
								settingsRow.UpdateStyle();

								OnSettingsChanged(settingData);
							};
							dataArea.AddChild(doubleEditWidget);
							unitsArea.AddChild(GetExtraSettingsWidget(settingData));

							settingsRow.ValueChanged = (text) =>
							{
								double currentValue2 = 0;
								double.TryParse(text, out currentValue2);
								doubleEditWidget.ActuallNumberEdit.Value = currentValue2;
								OnSettingsChanged(settingData);
							};
						}
						break;

					case OrganizerSettingsData.DataEditTypes.POSITIVE_DOUBLE:
						{
							const string multiValuesAreDiffernt = "-";
							FlowLayoutWidget content = new FlowLayoutWidget();

							var doubleEditWidget = new MHNumberEdit(0, allowDecimals: true, pixelWidth: doubleEditWidth, tabIndex: tabIndexForItem++)
							{
								ToolTipText = settingData.HelpText,
								Name = settingData.PresentationName + " Textbox",
								SelectAllOnFocus = true
							};

							double currentValue;
							bool ChangesMultipleOtherSettings = settingData.SetSettingsOnChange.Count > 0;
							if (ChangesMultipleOtherSettings)
							{
								bool allTheSame = true;
								string setting = GetActiveValue(settingData.SetSettingsOnChange[0], layerCascade);
								for (int i = 1; i < settingData.SetSettingsOnChange.Count; i++)
								{
									string nextSetting = GetActiveValue(settingData.SetSettingsOnChange[i], layerCascade);
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
							else // just set the setting normally
							{
								double.TryParse(sliceSettingValue, out currentValue);
								doubleEditWidget.ActuallNumberEdit.Value = currentValue;
							}
							doubleEditWidget.ActuallNumberEdit.InternalTextEditWidget.MarkAsStartingState();
							
							doubleEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
							{
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

								// also always save to the local setting
								SaveSetting(settingData.SlicerConfigName, numberEdit.Value.ToString());
								settingsRow.UpdateStyle();
								OnSettingsChanged(settingData);
							};
							content.AddChild(doubleEditWidget);
							unitsArea.AddChild(GetExtraSettingsWidget(settingData));

							if (settingData.QuickMenuSettings.Count > 0)
							{
								dataArea.AddChild(CreateQuickMenu(settingData, content, doubleEditWidget.ActuallNumberEdit.InternalTextEditWidget));
							}
							else
							{
								dataArea.AddChild(content);
							}

							settingsRow.ValueChanged = (text) =>
							{
								double currentValue2 = 0;
								double.TryParse(text, out currentValue2);
								doubleEditWidget.ActuallNumberEdit.Value = currentValue2;
								OnSettingsChanged(settingData);
							};
						}
						break;

					case OrganizerSettingsData.DataEditTypes.OFFSET:
						{
							double currentValue;
							double.TryParse(sliceSettingValue, out currentValue);
							var doubleEditWidget = new MHNumberEdit(currentValue, allowDecimals: true, allowNegatives: true, pixelWidth: doubleEditWidth, tabIndex: tabIndexForItem++)
							{
								ToolTipText = settingData.HelpText,
								SelectAllOnFocus = true

							};
							doubleEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
							{
								SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString());
								settingsRow.UpdateStyle();
								OnSettingsChanged(settingData);
							};
							dataArea.AddChild(doubleEditWidget);
							unitsArea.AddChild(GetExtraSettingsWidget(settingData));

							settingsRow.ValueChanged = (text) =>
							{
								double currentValue2;
								double.TryParse(text, out currentValue2);
								doubleEditWidget.ActuallNumberEdit.Value = currentValue2;
								OnSettingsChanged(settingData);
							};
						}
						break;

					case OrganizerSettingsData.DataEditTypes.DOUBLE_OR_PERCENT:
						{
							FlowLayoutWidget content = new FlowLayoutWidget();

							var stringEdit = new MHTextEditWidget(sliceSettingValue, pixelWidth: doubleEditWidth - 2, tabIndex: tabIndexForItem++)
							{
								ToolTipText = settingData.HelpText,
								SelectAllOnFocus = true
							};
							stringEdit.ActualTextEditWidget.EditComplete += (sender, e) =>
							{
								var textEditWidget = (TextEditWidget)sender;
								string text = textEditWidget.Text.Trim();

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
								settingsRow.UpdateStyle();

								OnSettingsChanged(settingData);
							};

							stringEdit.ActualTextEditWidget.InternalTextEditWidget.AllSelected += (sender, e) =>
							{
								// select everything up to the % (if present)
								InternalTextEditWidget textEditWidget = (InternalTextEditWidget)sender;
								int percentIndex = textEditWidget.Text.IndexOf("%");
								if (percentIndex != -1)
								{
									textEditWidget.SetSelection(0, percentIndex - 1);
								}
							};

							content.AddChild(stringEdit);
							unitsArea.AddChild(GetExtraSettingsWidget(settingData));

							if (settingData.QuickMenuSettings.Count > 0)
							{
								dataArea.AddChild(CreateQuickMenu(settingData, content, stringEdit.ActualTextEditWidget.InternalTextEditWidget));
							}
							else
							{
								dataArea.AddChild(content);
							}

							settingsRow.ValueChanged = (text) =>
							{
								stringEdit.Text = text;
								OnSettingsChanged(settingData);
							};
						}
						break;

					case OrganizerSettingsData.DataEditTypes.INT_OR_MM:
						{
							FlowLayoutWidget content = new FlowLayoutWidget();

							var stringEdit = new MHTextEditWidget(sliceSettingValue, pixelWidth: doubleEditWidth - 2, tabIndex: tabIndexForItem++)
							{
								ToolTipText = settingData.HelpText,
								SelectAllOnFocus = true
							};

							string startingText = stringEdit.Text;
							stringEdit.ActualTextEditWidget.EditComplete += (sender, e) =>
							{
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
								SaveSetting(settingData.SlicerConfigName, textEditWidget.Text);
								settingsRow.UpdateStyle();
								OnSettingsChanged(settingData);

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

							stringEdit.ActualTextEditWidget.InternalTextEditWidget.AllSelected += (sender, e) =>
							{
								// select everything up to the mm (if present)
								InternalTextEditWidget textEditWidget = (InternalTextEditWidget)sender;
								int mMIndex = textEditWidget.Text.IndexOf("mm");
								if (mMIndex != -1)
								{
									textEditWidget.SetSelection(0, mMIndex - 1);
								}
							};

							content.AddChild(stringEdit);
							unitsArea.AddChild(GetExtraSettingsWidget(settingData));

							if (settingData.QuickMenuSettings.Count > 0)
							{
								dataArea.AddChild(CreateQuickMenu(settingData, content, stringEdit.ActualTextEditWidget.InternalTextEditWidget));
							}
							else
							{
								dataArea.AddChild(content);
							}

							settingsRow.ValueChanged = (text) =>
							{
								stringEdit.Text = text;
								OnSettingsChanged(settingData);
							};
						}
						break;

					case OrganizerSettingsData.DataEditTypes.CHECK_BOX:
						{
							var checkBoxWidget = new CheckBox("")
							{
								Name = settingData.PresentationName + " Checkbox",
								ToolTipText = settingData.HelpText,
								VAnchor = Agg.UI.VAnchor.ParentBottom,
								TextColor = ActiveTheme.Instance.PrimaryTextColor,
								Checked = sliceSettingValue == "1"
							};
							checkBoxWidget.Click += (sender, e) =>
							{
								bool isChecked = ((CheckBox)sender).Checked;
								SaveSetting(settingData.SlicerConfigName, isChecked ? "1" : "0");
								settingsRow.UpdateStyle();

								OnSettingsChanged(settingData);
							};

							dataArea.AddChild(checkBoxWidget);

							settingsRow.ValueChanged = (text) =>
							{
								checkBoxWidget.Checked = text == "1";
								OnSettingsChanged(settingData);
							};
						}
						break;

					case OrganizerSettingsData.DataEditTypes.STRING:
						{
							var stringEdit = new MHTextEditWidget(sliceSettingValue, pixelWidth: 120, tabIndex: tabIndexForItem++)
							{
								Name = settingData.PresentationName + " Edit",
							};
							stringEdit.ToolTipText = settingData.HelpText;
							
							stringEdit.ActualTextEditWidget.EditComplete += (sender, e) =>
							{
								SaveSetting(settingData.SlicerConfigName, ((TextEditWidget)sender).Text);
								settingsRow.UpdateStyle();

								OnSettingsChanged(settingData);
							};

							dataArea.AddChild(stringEdit);

							settingsRow.ValueChanged = (text) =>
							{
								stringEdit.Text = text;
								OnSettingsChanged(settingData);
							};
						}
						break;

					case OrganizerSettingsData.DataEditTypes.MULTI_LINE_TEXT:
						{
							string convertedNewLines = sliceSettingValue.Replace("\\n", "\n");
							var stringEdit = new MHTextEditWidget(convertedNewLines, pixelWidth: 320, pixelHeight: multiLineEditHeight, multiLine: true, tabIndex: tabIndexForItem++)
							{
								HAnchor = HAnchor.ParentLeftRight,
							};
							
							stringEdit.ActualTextEditWidget.EditComplete += (sender, e) =>
							{
								SaveSetting(settingData.SlicerConfigName, ((TextEditWidget)sender).Text.Replace("\n", "\\n"));
								settingsRow.UpdateStyle();

								OnSettingsChanged(settingData);
							};

							nameArea.HAnchor = HAnchor.AbsolutePosition;
							nameArea.Width = 0;
							dataArea.AddChild(stringEdit);
							dataArea.HAnchor = HAnchor.ParentLeftRight;

							settingsRow.ValueChanged = (text) =>
							{
								stringEdit.Text = text.Replace("\\n", "\n");
								OnSettingsChanged(settingData);
							};
						}
						break;

					case OrganizerSettingsData.DataEditTypes.COM_PORT:
						{
							// The COM_PORT control is unique in its approach to the SlicerConfigName. It uses "MatterControl.ComPort" settings name to
							// bind to a context that will place it in the SliceSetting view but it binds its values to a machine
							// specific dictionary key that is not exposed in the UI. At runtime we lookup and store to "MatterControl.<machine>.ComPort"
							// ensuring that a single printer can be shared across different devices and we'll select the correct ComPort in each case
							var selectableOptions = new StyledDropDownList("None", maxHeight: 200)
							{
								ToolTipText = settingData.HelpText,
								Margin = new BorderDouble()
							};

							selectableOptions.Click += (s, e) =>
							{
								AddComMenuItems(settingData, settingsRow, selectableOptions);
							};

							AddComMenuItems(settingData, settingsRow, selectableOptions);

							dataArea.AddChild(selectableOptions);

							settingsRow.ValueChanged = (text) =>
							{
								// Lookup the machine specific comport value rather than the passed in text value
								selectableOptions.SelectedLabel = ActiveSliceSettings.Instance.ComPort();
								OnSettingsChanged(settingData);
							};
						}
						break;

					case OrganizerSettingsData.DataEditTypes.LIST:
						{
							var selectableOptions = new StyledDropDownList("None", maxHeight: 200)
							{
								ToolTipText = settingData.HelpText,
								Margin = new BorderDouble()
							};

							foreach (string listItem in settingData.ExtraSettings.Split(','))
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

									settingsRow.UpdateStyle();

									OnSettingsChanged(settingData);
								};
							}

							dataArea.AddChild(selectableOptions);

							settingsRow.ValueChanged = (text) =>
							{
								selectableOptions.SelectedLabel = text;
								OnSettingsChanged(settingData);
							};
						}
						break;

					case OrganizerSettingsData.DataEditTypes.HARDWARE_PRESENT:
						{
							var checkBoxWidget = new CheckBox("")
							{
								Name = settingData.PresentationName + " Checkbox",
								ToolTipText = settingData.HelpText,
								VAnchor = Agg.UI.VAnchor.ParentBottom,
								TextColor = ActiveTheme.Instance.PrimaryTextColor,
								Checked = sliceSettingValue == "1"
							};

							checkBoxWidget.CheckedStateChanged += (sender, e) =>
							{
								bool isChecked = ((CheckBox)sender).Checked;
								SaveSetting(settingData.SlicerConfigName, isChecked ? "1" : "0");

								settingsRow.UpdateStyle();

								OnSettingsChanged(settingData);
							};

							dataArea.AddChild(checkBoxWidget);

							settingsRow.ValueChanged = (text) =>
							{
								checkBoxWidget.Checked = text == "1";
								OnSettingsChanged(settingData);
							};
						}
						break;

					case OrganizerSettingsData.DataEditTypes.VECTOR2:
						{
							string[] xyValueStrings = sliceSettingValue.Split(',');
							if (xyValueStrings.Length != 2)
							{
								xyValueStrings = new string[] { "0", "0" };
							}

							double currentXValue;
							double.TryParse(xyValueStrings[0], out currentXValue);

							var xEditWidget = new MHNumberEdit(currentXValue, allowDecimals: true, pixelWidth: vectorXYEditWidth, tabIndex: tabIndexForItem++)
							{
								ToolTipText = settingData.HelpText,
								SelectAllOnFocus = true
							};

							double currentYValue;
							double.TryParse(xyValueStrings[1], out currentYValue);

							var yEditWidget = new MHNumberEdit(currentYValue, allowDecimals: true, pixelWidth: vectorXYEditWidth, tabIndex: tabIndexForItem++)
							{
								ToolTipText = settingData.HelpText,
								SelectAllOnFocus = true,
								Margin = new BorderDouble(20, 0, 0, 0),
							};

							xEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
							{
								SaveSetting(settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "," + yEditWidget.ActuallNumberEdit.Value.ToString());

								settingsRow.UpdateStyle();

								OnSettingsChanged(settingData);
							};
							dataArea.AddChild(xEditWidget);
							dataArea.AddChild(new TextWidget("x", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor)
							{
								VAnchor = VAnchor.ParentCenter,
								Margin = new BorderDouble(5, 0),
							});

							yEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
							{
								SaveSetting(settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "," + yEditWidget.ActuallNumberEdit.Value.ToString());

								settingsRow.UpdateStyle();

								OnSettingsChanged(settingData);
							};
							dataArea.AddChild(yEditWidget);
							var yLabel = new GuiWidget(HAnchor.ParentLeftRight, VAnchor.FitToChildren | VAnchor.ParentCenter)
							{
								Padding = new BorderDouble(5, 0),
								HAnchor = HAnchor.ParentLeftRight,
							};
							yLabel.AddChild(new WrappedTextWidget("y", 0, pointSize: 9, textColor: ActiveTheme.Instance.PrimaryTextColor));
							unitsArea.AddChild(yLabel);

							settingsRow.ValueChanged = (text) =>
							{
								double currentValue2;
								string[] xyValueStrings2 = text.Split(',');
								if (xyValueStrings2.Length != 2)
								{
									xyValueStrings2 = new string[] { "0", "0" };
								}

								double.TryParse(xyValueStrings2[0], out currentValue2);
								xEditWidget.ActuallNumberEdit.Value = currentValue2;

								double.TryParse(xyValueStrings2[1], out currentValue2);
								yEditWidget.ActuallNumberEdit.Value = currentValue2;

								OnSettingsChanged(settingData);
							};

						}
						break;

					case OrganizerSettingsData.DataEditTypes.OFFSET2:
						{
							Vector2 offset = ActiveSliceSettings.Instance.ExtruderOffset(extruderIndex);

							var xEditWidget = new MHNumberEdit(offset.x, allowDecimals: true, allowNegatives: true, pixelWidth: vectorXYEditWidth, tabIndex: tabIndexForItem++)
							{
								ToolTipText = settingData.HelpText,
								SelectAllOnFocus = true,
							};

							var yEditWidget = new MHNumberEdit(offset.y, allowDecimals: true, allowNegatives: true, pixelWidth: vectorXYEditWidth, tabIndex: tabIndexForItem++)
							{
								ToolTipText = settingData.HelpText,
								SelectAllOnFocus = true,
								Margin = new BorderDouble(20, 0, 0, 0),
							};

							xEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
							{
								int extruderIndexLocal = extruderIndex;
								SaveCommaSeparatedIndexSetting(extruderIndexLocal, settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "x" + yEditWidget.ActuallNumberEdit.Value.ToString());

								settingsRow.UpdateStyle();

								OnSettingsChanged(settingData);
							};
							dataArea.AddChild(xEditWidget);
							dataArea.AddChild(new TextWidget("x", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor)
							{
								VAnchor = VAnchor.ParentCenter,
								Margin = new BorderDouble(5, 0),
							});

							yEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
							{
								int extruderIndexLocal = extruderIndex;
								SaveCommaSeparatedIndexSetting(extruderIndexLocal, settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "x" + yEditWidget.ActuallNumberEdit.Value.ToString());

								settingsRow.UpdateStyle();

								OnSettingsChanged(settingData);
							};
							dataArea.AddChild(yEditWidget);
							var yLabel = new GuiWidget(HAnchor.ParentLeftRight, VAnchor.FitToChildren | VAnchor.ParentCenter)
							{
								Padding = new BorderDouble(5, 0),
								HAnchor = HAnchor.ParentLeftRight,
							};
							yLabel.AddChild(new WrappedTextWidget("y", 0, pointSize: 9, textColor: ActiveTheme.Instance.PrimaryTextColor));
							unitsArea.AddChild(yLabel);

							settingsRow.ValueChanged = (text) =>
							{
								Vector2 offset2 = ActiveSliceSettings.Instance.ExtruderOffset(extruderIndex);
								xEditWidget.ActuallNumberEdit.Value = offset2.x;
								yEditWidget.ActuallNumberEdit.Value = offset2.y;
								OnSettingsChanged(settingData);
							};

						}
						break;

					default:
						var missingSetting = new TextWidget(String.Format("Missing the setting for '{0}'.", settingData.DataEditType.ToString()))
						{
							TextColor = ActiveTheme.Instance.PrimaryTextColor,
							BackgroundColor = RGBA_Bytes.Red
						};
						dataArea.AddChild(missingSetting);
						break;
				}
			}

			container.HAnchor = HAnchor.ParentLeftRight;
			container.VAnchor = VAnchor.FitToChildren;

			var restoreButton = new Button(new ButtonViewStates(new ImageWidget(restoreNormal), new ImageWidget(restoreHover), new ImageWidget(restorePressed), new ImageWidget(restoreNormal)))
			{
				Name = "Restore " + settingData.SlicerConfigName,
				VAnchor = VAnchor.ParentCenter,
				Margin = new BorderDouble(0, 0, 5, 0),
				ToolTipText = "Restore Default".Localize()
			};

			restoreButton.Click += (sender, e) =>
			{
				// Revert the user override 
				if (persistenceLayer == null)
				{
					ActiveSliceSettings.Instance.ClearValue(settingData.SlicerConfigName);
				}
				else
				{
					ActiveSliceSettings.Instance.ClearValue(settingData.SlicerConfigName, persistenceLayer);
				}

				settingsRow.RefreshValue(layerCascade);
			};

			restoreArea.AddChild(restoreButton);

			container.AddChild(settingsRow);

			// Define the UpdateStyle implementation
			settingsRow.UpdateStyle = () =>
			{
				if (persistenceLayer.ContainsKey(settingData.SlicerConfigName))
				{
					switch (this.viewFilter)
					{
						case NamedSettingsLayers.All:
							settingsRow.BackgroundColor = userSettingBackgroundColor;
							break;
						case NamedSettingsLayers.Material:
							settingsRow.BackgroundColor = materialSettingBackgroundColor;
							break;
						case NamedSettingsLayers.Quality:
							settingsRow.BackgroundColor = qualitySettingBackgroundColor;
							break;
					}

					restoreButton.Visible = true;
				}
				else if (layerCascade == null)
				{
					if (ActiveSliceSettings.Instance.SettingExistsInLayer(settingData.SlicerConfigName, NamedSettingsLayers.Material))
					{
						settingsRow.BackgroundColor = materialSettingBackgroundColor;
					}
					else if (ActiveSliceSettings.Instance.SettingExistsInLayer(settingData.SlicerConfigName, NamedSettingsLayers.Quality))
					{
						settingsRow.BackgroundColor = qualitySettingBackgroundColor;
					}
					else
					{
						settingsRow.BackgroundColor = RGBA_Bytes.Transparent;
					}

					restoreButton.Visible = false;
				}
				else
				{
					restoreButton.Visible = false;
					settingsRow.BackgroundColor = RGBA_Bytes.Transparent;
				}
			};

			// Invoke the UpdateStyle implementation
			settingsRow.UpdateStyle();

			return container;
		}

		private void AddComMenuItems(OrganizerSettingsData settingData, SettingsRow settingsRow, StyledDropDownList selectableOptions)
		{
			selectableOptions.MenuItems.Clear();
			string machineSpecificComPortValue = ActiveSliceSettings.Instance.ComPort();
			foreach (string listItem in FrostedSerialPort.GetPortNames())
			{
				MenuItem newItem = selectableOptions.AddItem(listItem);
				if (newItem.Text == machineSpecificComPortValue)
				{
					selectableOptions.SelectedLabel = machineSpecificComPortValue;
				}

				newItem.Selected += (sender, e) =>
				{
					MenuItem menuItem = ((MenuItem)sender);

					// Directly set the ComPort
					if (persistenceLayer == null)
					{
						ActiveSliceSettings.Instance.SetComPort(menuItem.Text);
					}
					else
					{
						ActiveSliceSettings.Instance.SetComPort(menuItem.Text, persistenceLayer);
					}

					settingsRow.UpdateStyle();

					OnSettingsChanged(settingData);
				};
			}
		}

		static ImageBuffer restoreNormal = EnsureRestoreButtonImages();
		static ImageBuffer restoreHover;
		static ImageBuffer restorePressed;

		static ImageBuffer EnsureRestoreButtonImages()
		{
			int size = (int)(16 * GuiWidget.DeviceScale);

			restoreNormal = ColorCirle(size, new RGBA_Bytes(128, 128, 128));
			if (OsInformation.OperatingSystem == OSType.Android)
			{
				restoreNormal = ColorCirle(size, new RGBA_Bytes(200, 0, 0));
			}
			restoreHover = ColorCirle(size, new RGBA_Bytes(200, 0, 0));
			restorePressed = ColorCirle(size, new RGBA_Bytes(255, 0, 0));

			return restoreNormal;
		}

		private static ImageBuffer ColorCirle(int size, RGBA_Bytes color)
		{
			ImageBuffer imageBuffer = new ImageBuffer(size, size, 32, new BlenderBGRA());
			Graphics2D normalGraphics = imageBuffer.NewGraphics2D();
			Vector2 center = new Vector2(size / 2.0, size / 2.0);
			normalGraphics.Circle(center, size / 2.0, color);
			normalGraphics.Line(center + new Vector2(-size / 4.0, -size / 4.0), center + new Vector2(size / 4.0, size / 4.0), RGBA_Bytes.White, 2 * GuiWidget.DeviceScale);
			normalGraphics.Line(center + new Vector2(-size / 4.0, size / 4.0), center + new Vector2(size / 4.0, -size / 4.0), RGBA_Bytes.White, 2 * GuiWidget.DeviceScale);

			return imageBuffer;
		}

		private GuiWidget CreateQuickMenu(OrganizerSettingsData settingData, GuiWidget content, InternalTextEditWidget internalTextWidget)
		{
			string sliceSettingValue = GetActiveValue(settingData.SlicerConfigName, layerCascade);
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
					OnSettingsChanged(settingData);
					internalTextWidget.Text = valueLocal;
					internalTextWidget.OnEditComplete(null);
				};
			}

			// put in the custom menu to allow direct editing
			MenuItem customMenueItem = selectableOptions.AddItem("Custom");

			totalContent.AddChild(selectableOptions);
			content.VAnchor = VAnchor.ParentCenter;
			totalContent.AddChild(content);

			SettingChanged.RegisterEvent((sender, e) =>
			{
				bool foundSetting = false;
				foreach (QuickMenuNameValue nameValue in settingData.QuickMenuSettings)
				{
					string localName = nameValue.MenuName;
					string newSliceSettingValue = GetActiveValue(settingData.SlicerConfigName, layerCascade);
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
			}, ref unregisterEvents);

			return totalContent;
		}

		private void SaveCommaSeparatedIndexSetting(int extruderIndexLocal, string slicerConfigName, string newSingleValue)
		{
			string[] settings = GetActiveValue(slicerConfigName, layerCascade).Split(',');
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

		private void SaveSetting(string name, string value)
		{
			if (persistenceLayer == null)
			{
				ActiveSliceSettings.Instance.SetActiveValue(name, value);
			}
			else
			{
				ActiveSliceSettings.Instance.SetActiveValue(name, value, persistenceLayer);
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
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