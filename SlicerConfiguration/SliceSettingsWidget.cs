/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class NoSettingsWidget : FlowLayoutWidget
	{
		public NoSettingsWidget() : base(FlowDirection.TopToBottom)
		{
			this.AnchorAll();
			this.Padding = new BorderDouble(3, 0);

			var noConnectionMessageContainer = new AltGroupBox(new WrappedTextWidget("No Printer Selected".Localize(), pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor))
			{
				Margin = new BorderDouble(top: 10),
				BorderColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.Stretch,
				Height = 90
			};
			string noConnectionString = "No printer is currently selected. Please select a printer to edit slice settings.".Localize();
			noConnectionString += "\n\n" + "NOTE: You need to select a printer, but do not need to connect to it.".Localize();
			var noConnectionMessage = new WrappedTextWidget(noConnectionString, pointSize: 10)
			{
				Margin = new BorderDouble(5),
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.Stretch
			};
			noConnectionMessageContainer.AddChild(noConnectionMessage);
			this.AddChild(noConnectionMessageContainer);
		}
	}

	public class SliceSettingsWidget : GuiWidget
	{
		private TabControl topCategoryTabs;
		internal SettingsControlBar settingsControlBar;
		private FlowLayoutWidget pageTopToBottomLayout;

		private SettingsContext settingsContext;

		private PrinterConnection printerConnection;

		private Dictionary<string, IUIField> allUiFields = new Dictionary<string, IUIField>();

		private EventHandler unregisterEvents;

		public SliceSettingsWidget(PrinterConnection printerConnection, SettingsContext settingsContext)
		{
			this.printerConnection = printerConnection;
			this.BackgroundColor = ApplicationController.Instance.Theme.TabBodyBackground;

			this.settingsContext = settingsContext;

			pageTopToBottomLayout = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Top,
				Padding = 0
			};
			pageTopToBottomLayout.AnchorAll();
			this.AddChild(pageTopToBottomLayout);

			settingsControlBar = new SettingsControlBar(printerConnection)
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(8, 12, 8, 8)
			};

			pageTopToBottomLayout.AddChild(settingsControlBar);

			string noConnectionString = "No printer is currently selected. Please select a printer to edit slice settings.".Localize();
			noConnectionString += "\n\n" + "NOTE: You need to select a printer, but do not need to connect to it.".Localize();
			var noConnectionMessage = new WrappedTextWidget(noConnectionString, pointSize: 10)
			{
				Margin = new BorderDouble(5),
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.Stretch
			};
			printerConnection.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
			printerConnection.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);

			RebuildSliceSettingsTabs();

			ActiveSliceSettings.SettingChanged.RegisterEvent(
				(s, e) =>
				{
					if (e is StringEventArgs stringEvent)
					{
						string settingsKey = stringEvent.Data;
						if (allUiFields.TryGetValue(settingsKey, out IUIField field2))
						{
							string currentValue = settingsContext.GetValue(settingsKey);
							if (field2.Value != currentValue
								|| settingsKey == "com_port")
							{
								field2.SetValue(
									currentValue,
									userInitiated: false);
							}
						}
					}
				},
				ref unregisterEvents);

			this.AnchorAll();
		}

		internal void RebuildSliceSettingsTabs()
		{
			if (topCategoryTabs != null)
			{
				// Close and remove children
				topCategoryTabs.Close();
			}

			topCategoryTabs = new TabControl();
			topCategoryTabs.TabBar.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
			topCategoryTabs.Margin = new BorderDouble(top: 8);
			topCategoryTabs.AnchorAll();

			var sideTabBarsListForLayout = new List<TabBar>();

			// Cache results from database read for the duration of this function
			bool showHelpControls = this.ShowHelpControls;

			for (int topCategoryIndex = 0; topCategoryIndex < SliceSettingsOrganizer.Instance.UserLevels[UserLevel].CategoriesList.Count; topCategoryIndex++)
			{
				OrganizerCategory category = SliceSettingsOrganizer.Instance.UserLevels[UserLevel].CategoriesList[topCategoryIndex];

				var categoryPage = new TabPage(category.Name.Localize());
				categoryPage.AnchorAll();

				topCategoryTabs.AddTab(new TextTab(
					categoryPage,
					category.Name + " Tab",
					14,
					ActiveTheme.Instance.TabLabelSelected,
					new RGBA_Bytes(),
					ActiveTheme.Instance.TabLabelUnselected,
					new RGBA_Bytes(),
					useUnderlineStyling: true));

				TabControl sideTabs = CreateSideTabsAndPages(category, showHelpControls);
				sideTabBarsListForLayout.Add(sideTabs.TabBar);

				categoryPage.AddChild(sideTabs);
			}

			topCategoryTabs.TabBar.AddChild(new HorizontalSpacer());

			if (settingsContext.IsPrimarySettingsView)
			{
				var sliceSettingsDetailControl = new SliceSettingsOverflowDropdown(this);
				topCategoryTabs.TabBar.AddChild(sliceSettingsDetailControl);
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
			foreach (var tabList in sideTabBarsListForLayout)
			{
				if (tabList.CountVisibleChildren() == 1)
				{
					tabList.MinimumSize = new Vector2(0, 0);
					tabList.Width = 0;
				}
			}

			pageTopToBottomLayout.AddChild(topCategoryTabs);

			SetVisibleControls();

			// Make sure we are on the right tab when we create this view
			{
				string settingsName = "SliceSettingsWidget_CurrentTab";
				string selectedTab = UserSettings.Instance.get(settingsName);
				topCategoryTabs.SelectTab(selectedTab);

				topCategoryTabs.TabBar.TabIndexChanged += (object sender, EventArgs e) =>
				{
					string selectedTabName = topCategoryTabs.TabBar.SelectedTabName;
					if (!string.IsNullOrEmpty(selectedTabName))
					{
						if (settingsContext.IsPrimarySettingsView)
						{
							UserSettings.Instance.set(settingsName, selectedTabName);
						}
					}
				};
			}
		}

		public string UserLevel
		{
			get
			{
				// Preset windows that are not the primary view should be in Advanced mode
				if (!settingsContext.IsPrimarySettingsView)
				{
					return "Advanced";
				}

				string settingsLevel = UserSettings.Instance.get(UserSettingsKey.SliceSettingsLevel);
				if (!string.IsNullOrEmpty(settingsLevel)
					&& SliceSettingsOrganizer.Instance.UserLevels.ContainsKey(settingsLevel))
				{
					return settingsLevel;
				}

				return "Simple";
			}
		}

		// TODO: This should just proxy to settingsControlBar.Visible. Having local state and pushing values on event listeners seems off
		private bool showControlBar = true;
		public bool ShowControlBar
		{
			get { return showControlBar; }
			set
			{
				settingsControlBar.Visible = value;
				showControlBar = value;
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

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private void onPrinterStatusChanged(object sender, EventArgs e)
		{
			SetVisibleControls();
			this.Invalidate();
		}

		private void SetVisibleControls()
		{
			if (ActiveSliceSettings.Instance.PrinterSelected)
			{
				topCategoryTabs.Visible = true;
				settingsControlBar.Visible = showControlBar;
			}
			else
			{
				topCategoryTabs.Visible = false;
				settingsControlBar.Visible = showControlBar;
			}
		}

		private int tabIndexForItem = 0;

		// Cache show help at construction time - rebuild SliceSettingsWidget on value changed
		internal bool ShowHelpControls
		{
			get
			{
				return UserSettings.Instance.get(UserSettingsKey.SliceSettingsShowHelp) == "true";
			}
			set
			{
				UserSettings.Instance.set(UserSettingsKey.SliceSettingsShowHelp, value.ToString().ToLower());
			}
		}

		private TabControl CreateSideTabsAndPages(OrganizerCategory category, bool showHelpControls)
		{
			this.HAnchor = HAnchor.Stretch;

			TabControl leftSideGroupTabs = new TabControl(Orientation.Vertical);
			leftSideGroupTabs.Margin = new BorderDouble(0, 0, 0, 5);
			leftSideGroupTabs.TabBar.BorderColor = ActiveTheme.Instance.PrimaryTextColor;

			foreach (OrganizerGroup group in category.GroupsList)
			{
				tabIndexForItem = 0;

				var groupTabPage = new TabPage(group.Name.Localize())
				{
					HAnchor = HAnchor.Stretch
				};

				//Side Tabs
				var groupTabWidget = new TextTab(
					groupTabPage, 
					group.Name + " Tab", 
					14,
					ActiveTheme.Instance.TabLabelSelected, 
					new RGBA_Bytes(), 
					ActiveTheme.Instance.TabLabelUnselected,
					new RGBA_Bytes(),
					32);

				groupTabWidget.HAnchor = HAnchor.Stretch;

				var subGroupLayoutTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
				subGroupLayoutTopToBottom.AnchorAll();

				bool needToAddSubGroup = false;
				foreach (OrganizerSubGroup subGroup in group.SubGroupsList)
				{
					string subGroupTitle = subGroup.Name;
					int numberOfCopies = 1;

					if (subGroup.Name == "Extruder X")
					{
						numberOfCopies = ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count);
					}

					for (int copyIndex = 0; copyIndex < numberOfCopies; copyIndex++)
					{
						if (subGroup.Name == "Extruder X")
						{
							subGroupTitle = "{0} {1}".FormatWith("Extruder".Localize(), copyIndex + 1);
						}

						bool addedSettingToSubGroup = false;

						var topToBottomSettings = new FlowLayoutWidget(FlowDirection.TopToBottom)
						{
							HAnchor = HAnchor.Stretch
						};

						foreach (SliceSettingData settingData in subGroup.SettingDataList)
						{
							// Note: tab sections may disappear if / when they are empty, as controlled by:
							// settingShouldBeShown / addedSettingToSubGroup / needToAddSubGroup
							bool settingShouldBeShown = CheckIfShouldBeShown(settingData);

							if (EngineMappingsMatterSlice.Instance.MapContains(settingData.SlicerConfigName)
								&& settingShouldBeShown)
							{
								addedSettingToSubGroup = true;
								topToBottomSettings.AddChild(
									CreateSettingInfoUIControls(
										settingData,
										copyIndex,
										ref tabIndexForItem));

								if (showHelpControls)
								{
									topToBottomSettings.AddChild(AddInHelpText(topToBottomSettings, settingData));
								}
							}
						}

						if (addedSettingToSubGroup)
						{
							needToAddSubGroup = true;

							var groupBox = new AltGroupBox(subGroupTitle.Localize())
							{
								TextColor = ActiveTheme.Instance.PrimaryTextColor,
								BorderColor = ActiveTheme.Instance.PrimaryTextColor,
								HAnchor = HAnchor.Stretch,
								Margin = new BorderDouble(bottom: 3),
								Padding = new BorderDouble(left: 4),
							};
							groupBox.AddChild(topToBottomSettings);

							subGroupLayoutTopToBottom.AddChild(groupBox);
						}
					}
				}

				if (needToAddSubGroup)
				{
					SliceSettingListControl scrollOnGroupTab = new SliceSettingListControl();

					subGroupLayoutTopToBottom.VAnchor = VAnchor.Fit;
					subGroupLayoutTopToBottom.HAnchor = HAnchor.Stretch;

					scrollOnGroupTab.AddChild(subGroupLayoutTopToBottom);
					groupTabPage.AddChild(scrollOnGroupTab);
					leftSideGroupTabs.AddTab(groupTabWidget);

					// Make sure we have the right scroll position when we create this view
					// This code is not working yet. Scroll widgets get a scroll event when the tab becomes visible that is always reseting them.
					// So it is not useful to enable this and in fact makes the tabs inconsistently scrolled. It is just here for reference. // 2015 04 16, LBB
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

				if (group.Name == "Connection")
				{
					subGroupLayoutTopToBottom.AddChild(SliceSettingsWidget.CreatePrinterExtraControls(isPrimarySettingsView: true));
				}
			}

			// Make sure we are on the right tab when we create this view
			string settingsTypeName = $"SliceSettingsWidget_{category.Name}_CurrentTab";
			string selectedTab = UserSettings.Instance.get(settingsTypeName);
			leftSideGroupTabs.SelectTab(selectedTab);

			leftSideGroupTabs.TabBar.TabIndexChanged += (object sender, EventArgs e) =>
			{
				string selectedTabName = leftSideGroupTabs.TabBar.SelectedTabName;
				if (!string.IsNullOrEmpty(selectedTabName)
					&& settingsContext.IsPrimarySettingsView)
				{
					UserSettings.Instance.set(settingsTypeName, selectedTabName);
				}
			};

			return leftSideGroupTabs;
		}

		private bool CheckIfShouldBeShown(SliceSettingData settingData)
		{
			bool settingShouldBeShown = settingsContext.ParseShowString(settingData.ShowIfSet);
			if (settingsContext.ViewFilter == NamedSettingsLayers.Material || settingsContext.ViewFilter == NamedSettingsLayers.Quality)
			{
				if (!settingData.ShowAsOverride)
				{
					settingShouldBeShown = false;
				}
			}

			return settingShouldBeShown;
		}

		private bool CheckIfEnabled(SliceSettingData settingData)
		{
			return settingsContext.ParseShowString(settingData.EnableIfSet);
		}

		private GuiWidget AddInHelpText(FlowLayoutWidget topToBottomSettings, SliceSettingData settingData)
		{
			FlowLayoutWidget allText = new FlowLayoutWidget(FlowDirection.TopToBottom);
			allText.HAnchor = HAnchor.Stretch;
			double textRegionWidth = 380 * GuiWidget.DeviceScale;
			allText.Margin = new BorderDouble(0);
			allText.Padding = new BorderDouble(5);
			allText.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;

			double helpPointSize = 10;

			GuiWidget helpWidget = new WrappedTextWidget(settingData.HelpText, pointSize: helpPointSize, textColor: RGBA_Bytes.White);
			helpWidget.Width = textRegionWidth;
			helpWidget.Margin = new BorderDouble(5, 0, 0, 0);
			//helpWidget.HAnchor = HAnchor.Left;
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
				TextTab groupTabWidget = new TextTab(groupTabPage, "Extra Settings Tab", 14,
				   ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes());
				leftSideGroupTabs.AddTab(groupTabWidget);

				FlowLayoutWidget subGroupLayoutTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
				subGroupLayoutTopToBottom.HAnchor = HAnchor.MaxFitOrStretch;
				subGroupLayoutTopToBottom.VAnchor = VAnchor.Fit;

				FlowLayoutWidget topToBottomSettings = new FlowLayoutWidget(FlowDirection.TopToBottom);
				topToBottomSettings.HAnchor = HAnchor.MaxFitOrStretch;

				this.HAnchor = HAnchor.Stretch;

				foreach (var keyValue in ActiveSliceSettings.Instance.BaseLayer)
				{
					if (!SliceSettingsOrganizer.Instance.Contains(UserLevel, keyValue.Key))
					{
						SliceSettingData settingData = new SliceSettingData(keyValue.Key, keyValue.Key, SliceSettingData.DataEditTypes.STRING);
						if (EngineMappingsMatterSlice.Instance.MapContains(settingData.SlicerConfigName))
						{
							GuiWidget controlsForThisSetting = CreateSettingInfoUIControls(
								settingData,
								0,
								ref tabIndexForItem);

							topToBottomSettings.AddChild(controlsForThisSetting);

							count++;
						}
					}
				}

				AltGroupBox groupBox = new AltGroupBox("Extra".Localize());
				groupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				groupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
				groupBox.AddChild(topToBottomSettings);
				groupBox.VAnchor = VAnchor.Fit;
				groupBox.HAnchor = HAnchor.MaxFitOrStretch;

				subGroupLayoutTopToBottom.AddChild(groupBox);

				SliceSettingListControl scrollOnGroupTab = new SliceSettingListControl();
				scrollOnGroupTab.AnchorAll();
				scrollOnGroupTab.AddChild(subGroupLayoutTopToBottom);
				groupTabPage.AddChild(scrollOnGroupTab);
			}
			return leftSideGroupTabs;
		}

		private static GuiWidget GetExtraSettingsWidget(SliceSettingData settingData)
		{
			// List elements contain list values in the field which normally contains label details, skip generation of invalid labels
			if (settingData.DataEditType == SliceSettingData.DataEditTypes.LIST
				|| settingData.DataEditType == SliceSettingData.DataEditTypes.HARDWARE_PRESENT)
			{
				return null;
			}

			var nameHolder = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit | VAnchor.Center,
				Padding = new BorderDouble(5, 0),
			};
			nameHolder.AddChild(new WrappedTextWidget(settingData.ExtraSettings.Localize(), pointSize: 8, textColor: ActiveTheme.Instance.PrimaryTextColor));

			return nameHolder;
		}

		private class SettingsRow : FlowLayoutWidget
		{
			private SettingsContext settingsContext;

			private SliceSettingData settingData;

			private GuiWidget dataArea;
			private GuiWidget unitsArea;
			private GuiWidget restoreArea;
			private Button restoreButton = null;
			private VerticalLine vline;

			private const bool debugLayout = false;

			public SettingsRow(SettingsContext settingsContext, SliceSettingData settingData)
			{
				this.settingData = settingData;
				this.settingsContext = settingsContext;

				vline = new VerticalLine()
				{
					BackgroundColor = RGBA_Bytes.Transparent,
					Margin = new BorderDouble(right: 6, left: 2),
					Width = 6,
					VAnchor = VAnchor.Stretch | VAnchor.Center,
					// TODO: Without minimumsize vline collapses to some value that's 50-60% of the expected VAnchor.Stretch value
					MinimumSize = new Vector2(0, 25),
					DebugShowBounds = debugLayout
				};
				this.AddChild(vline);
				
				this.NameArea = new GuiWidget()
				{
					MinimumSize = new Vector2(50, 0),
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit | VAnchor.Center,
					DebugShowBounds = debugLayout
				};
				this.AddChild(this.NameArea);

				dataArea = new FlowLayoutWidget
				{
					VAnchor = VAnchor.Fit | VAnchor.Center,
					DebugShowBounds = debugLayout
				};
				this.AddChild(dataArea);

				unitsArea = new GuiWidget()
				{
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Fit | VAnchor.Center,
					Width = settingData.ShowAsOverride ? 50 * GuiWidget.DeviceScale : 5,
					DebugShowBounds = debugLayout


				};
				this.AddChild(unitsArea);

				restoreArea = new GuiWidget()
				{
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Fit | VAnchor.Center,
					Width = settingData.ShowAsOverride ? 20 * GuiWidget.DeviceScale : 5,
					DebugShowBounds = debugLayout
				};
				this.AddChild(restoreArea);

				this.Name = settingData.SlicerConfigName + " Edit Field";

				if (settingData.ShowAsOverride)
				{
					restoreButton = ApplicationController.Instance.Theme.CreateSmallResetButton();
					restoreButton.HAnchor = HAnchor.Right;
					restoreButton.Margin = 0;
					restoreButton.Name = "Restore " + settingData.SlicerConfigName;
					restoreButton.ToolTipText = "Restore Default".Localize();
					restoreButton.Click += (sender, e) =>
					{
						// Revert the user override 
						settingsContext.ClearValue(settingData.SlicerConfigName);
					};

					restoreArea.AddChild(restoreButton);
				}

				var extraInfo = GetExtraSettingsWidget(settingData);
				if (extraInfo != null)
				{
					unitsArea.AddChild(extraInfo);
				}
			}

			public GuiWidget NameArea { get; }

			public void UpdateStyle()
			{
				if (settingsContext.ContainsKey(settingData.SlicerConfigName))
				{
					switch (settingsContext.ViewFilter)
					{
						case NamedSettingsLayers.All:
							if (settingData.ShowAsOverride)
							{
								var defaultCascade = ActiveSliceSettings.Instance.defaultLayerCascade;
								var firstParentValue = ActiveSliceSettings.Instance.GetValueAndLayerName(settingData.SlicerConfigName, defaultCascade.Skip(1));
								var currentValueAndLayerName = ActiveSliceSettings.Instance.GetValueAndLayerName(settingData.SlicerConfigName, defaultCascade);

								var currentValue = currentValueAndLayerName.Item1;
								var layerName = currentValueAndLayerName.Item2;

								if (firstParentValue.Item1 == currentValue)
								{
									if (layerName.StartsWith("Material"))
									{
										this.vline.BackgroundColor= materialSettingBackgroundColor;
									}
									else if (layerName.StartsWith("Quality"))
									{
										this.vline.BackgroundColor= qualitySettingBackgroundColor;
									}
									else
									{
										this.vline.BackgroundColor= RGBA_Bytes.Transparent;
									}

									if (restoreButton != null)
									{
										restoreButton.Visible = false;
									}
								}
								else
								{
									this.vline.BackgroundColor= userSettingBackgroundColor;
									if (restoreButton != null) restoreButton.Visible = true;
								}
							}
							break;
						case NamedSettingsLayers.Material:
							this.vline.BackgroundColor= materialSettingBackgroundColor;
							if (restoreButton != null) restoreButton.Visible = true;
							break;
						case NamedSettingsLayers.Quality:
							this.vline.BackgroundColor= qualitySettingBackgroundColor;
							if (restoreButton != null) restoreButton.Visible = true;
							break;
					}
				}
				else if (settingsContext.IsPrimarySettingsView)
				{
					if (ActiveSliceSettings.Instance.SettingExistsInLayer(settingData.SlicerConfigName, NamedSettingsLayers.Material))
					{
						this.vline.BackgroundColor= materialSettingBackgroundColor;
					}
					else if (ActiveSliceSettings.Instance.SettingExistsInLayer(settingData.SlicerConfigName, NamedSettingsLayers.Quality))
					{
						this.vline.BackgroundColor= qualitySettingBackgroundColor;
					}
					else
					{
						this.vline.BackgroundColor= RGBA_Bytes.Transparent;
					}

					if (restoreButton != null) restoreButton.Visible = false;
				}
				else
				{
					if (restoreButton != null) restoreButton.Visible = false;
					this.vline.BackgroundColor= RGBA_Bytes.Transparent;
				}
			}

			public void AddContent(GuiWidget content)
			{
				dataArea.AddChild(content);
			}
		}

		private static readonly RGBA_Bytes materialSettingBackgroundColor = new RGBA_Bytes(255, 127, 0, 108);
		private static readonly RGBA_Bytes qualitySettingBackgroundColor = new RGBA_Bytes(255, 255, 0, 108);
		public static readonly RGBA_Bytes userSettingBackgroundColor = new RGBA_Bytes(68, 95, 220, 108);

		// Creates an information row showing the base OEM profile and its create_date value
		public static GuiWidget CreatePrinterExtraControls(bool isPrimarySettingsView = false)
		{
			var dataArea = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
			};

			if (isPrimarySettingsView)
			{
				// OEM_LAYER_DATE:
				string lastUpdateTime = "March 1, 2016";
				if (ActiveSliceSettings.Instance?.OemLayer != null)
				{
					string fromCreatedDate = ActiveSliceSettings.Instance.OemLayer.ValueOrDefault(SettingsKey.created_date);
					try
					{
						if (!string.IsNullOrEmpty(fromCreatedDate))
						{
							DateTime time = Convert.ToDateTime(fromCreatedDate).ToLocalTime();
							lastUpdateTime = time.ToString("MMMM d, yyyy h:mm tt");
						}
					}
					catch
					{
					}
				}

				var row = new FlowLayoutWidget()
				{
					BackgroundColor = ActiveTheme.Instance.TertiaryBackgroundColor,
					Padding = new BorderDouble(5),
					Margin = new BorderDouble(3, 20, 3, 0),
					HAnchor = HAnchor.Stretch,
				};

				string make = ActiveSliceSettings.Instance.GetValue(SettingsKey.make);
				string model = ActiveSliceSettings.Instance.GetValue(SettingsKey.model);

				string title = $"{make} {model}";
				if (title == "Other Other")
				{
					title = "Custom Profile".Localize();
				}

				row.AddChild(new TextWidget(title, pointSize: 9)
				{
					Margin = new BorderDouble(0, 4, 10, 4),
					TextColor = ActiveTheme.Instance.PrimaryTextColor,
				});

				row.AddChild(new HorizontalSpacer());

				row.AddChild(new TextWidget(lastUpdateTime, pointSize: 9)
				{
					Margin = new BorderDouble(0, 4, 10, 4),
					TextColor = ActiveTheme.Instance.PrimaryTextColor,
				});

				dataArea.AddChild(row);
			}

			return dataArea;
		}

		private GuiWidget CreateSettingInfoUIControls(
			SliceSettingData settingData,
			int extruderIndex,
			ref int tabIndexForItem)
		{
			string sliceSettingValue = settingsContext.GetValue(settingData.SlicerConfigName);

			IUIField uiField = null;

			bool useDefaultSavePattern = true;

			var settingsRow = new SettingsRow(settingsContext, settingData)
			{
				Margin = new BorderDouble(0, 2),
				Padding = new BorderDouble(0, 0, 10, 0),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};

			if (!PrinterSettings.KnownSettings.Contains(settingData.SlicerConfigName))
			{
				// the setting we think we are adding is not in the known settings it may have been deprecated
				TextWidget settingName = new TextWidget(String.Format("Setting '{0}' not found in known settings", settingData.SlicerConfigName));
				settingName.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				settingsRow.NameArea.AddChild(settingName);
				settingsRow.NameArea.BackgroundColor = RGBA_Bytes.Red;
			}
			else
			{
				if (settingData.DataEditType != SliceSettingData.DataEditTypes.MULTI_LINE_TEXT)
				{
					settingsRow.NameArea.AddChild(
						// TODO: Layout stops working when WrappedText is used...
						//new WrappedTex/tWidget(settingData.PresentationName.Localize(), pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor)
						new TextWidget(settingData.PresentationName.Localize(), pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor)
						{
							// TODO: Fit fails to work on standard TextWidget controls and clips descenders
							// VAnchor = VAnchor.Fit | VAnchor.Center
							VAnchor = VAnchor.Center
						});
				}

				switch (settingData.DataEditType)
				{
					case SliceSettingData.DataEditTypes.INT:
						uiField = new IntField();
						break;

					case SliceSettingData.DataEditTypes.DOUBLE:
					case SliceSettingData.DataEditTypes.OFFSET:
						uiField = new DoubleField();
						break;

					case SliceSettingData.DataEditTypes.POSITIVE_DOUBLE:
						if (settingData.SetSettingsOnChange.Count > 0)
						{
							uiField = new BoundDoubleField(settingsContext, settingData);
						}
						else
						{
							uiField = new DoubleField();
						};
						break;

					case SliceSettingData.DataEditTypes.DOUBLE_OR_PERCENT:
						uiField = new DoubleOrPercentField();
						break;

					case SliceSettingData.DataEditTypes.INT_OR_MM:
						uiField = new ValueOrUnitsField();
						break;

					case SliceSettingData.DataEditTypes.CHECK_BOX:
						uiField = new CheckboxField();
						useDefaultSavePattern = false;
						uiField.ValueChanged += (s, e) =>
						{
							if (e.UserInitiated)
							{
								// Linked settings should be updated in all cases (user clicked checkbox, user clicked clear)
								foreach (var setSettingsData in settingData.SetSettingsOnChange)
								{
									string targetValue;

									if (uiField.Content is CheckBox checkbox)
									{
										if (setSettingsData.TryGetValue(checkbox.Checked ? "OnValue" : "OffValue", out targetValue))
										{
											settingsContext.SetValue(setSettingsData["TargetSetting"], targetValue);
										}
									}
								}

								// Store actual field value
								settingsContext.SetValue(settingData.SlicerConfigName, uiField.Value);
							}
						};

						break;

					case SliceSettingData.DataEditTypes.STRING:
						uiField = new TextField();
						break;

					case SliceSettingData.DataEditTypes.MULTI_LINE_TEXT:
						uiField = new MultilineStringField();
						break;
					case SliceSettingData.DataEditTypes.COM_PORT:
						useDefaultSavePattern = false;

						uiField = new ComPortField();
						uiField.ValueChanged += (s, e) =>
						{
							if (e.UserInitiated)
							{
								settingsContext.SetComPort(uiField.Value);
							}
						};

						break;

					case SliceSettingData.DataEditTypes.LIST:
						uiField = new ListField()
						{
							ListItems = settingData.ExtraSettings.Split(',').ToList()
						};
						break;

					case SliceSettingData.DataEditTypes.HARDWARE_PRESENT:
						uiField = new CheckboxField();
						break;

					case SliceSettingData.DataEditTypes.VECTOR2:
						uiField = new Vector2Field();
						break;

					case SliceSettingData.DataEditTypes.OFFSET2:
						useDefaultSavePattern = false;

						uiField = new ExtruderOffsetField()
						{
							ExtruderIndex = extruderIndex
						};
						uiField.ValueChanged += (s, e) =>
						{
							if (e.UserInitiated
								&& s is ExtruderOffsetField extruderOffset)
							{
								SaveCommaSeparatedIndexSetting(extruderOffset.ExtruderIndex, settingsContext, settingData.SlicerConfigName, extruderOffset.Value.Replace(",", "x"));
							}
						};

						break;

					default:
						// Missing Setting
						settingsRow.AddContent(new TextWidget(String.Format("Missing the setting for '{0}'.", settingData.DataEditType.ToString()))
						{
							TextColor = ActiveTheme.Instance.PrimaryTextColor,
							BackgroundColor = RGBA_Bytes.Red
						});
						break;
				}
			}

			if (uiField != null)
			{
				allUiFields[settingData.SlicerConfigName] = uiField;

				uiField.Initialize(tabIndexForItem++);

				uiField.SetValue(sliceSettingValue, userInitiated: false);

				uiField.ValueChanged += (s, e) =>
				{
					if (useDefaultSavePattern
						&& e.UserInitiated)
					{
						settingsContext.SetValue(settingData.SlicerConfigName, uiField.Value);
					}

					settingsRow.UpdateStyle();
				};

				// After initializing the field, wrap with dropmenu if applicable
				if (settingData.QuickMenuSettings.Count > 0)
				{
					uiField = new DropMenuWrappedField(uiField, settingData);
					uiField.Initialize(tabIndexForItem);
				}

				settingsRow.AddContent(uiField.Content);
			}

			// Invoke the UpdateStyle implementation
			settingsRow.UpdateStyle();

			bool settingShouldEnabled = settingsContext.ParseShowString(settingData.EnableIfSet);
			if (settingShouldEnabled)
			{
				return settingsRow;
			}
			else
			{
				var holder = new GuiWidget()
				{
					VAnchor = VAnchor.Fit,
					HAnchor = HAnchor.Stretch
				};
				holder.AddChild(settingsRow);

				var disable = new GuiWidget()
				{
					VAnchor = VAnchor.Stretch,
					HAnchor = HAnchor.Stretch,
					BackgroundColor = new RGBA_Bytes(ActiveTheme.Instance.TertiaryBackgroundColor, 200)
				};
				holder.AddChild(disable);

				return holder;
			}
		}

		public static GuiWidget CreateQuickMenu(SliceSettingData settingData, SettingsContext settingsContext, GuiWidget content, InternalTextEditWidget internalTextWidget)
		{
			string sliceSettingValue =settingsContext.GetValue(settingData.SlicerConfigName);
			FlowLayoutWidget totalContent = new FlowLayoutWidget();

			DropDownList selectableOptions = new DropDownList("Custom", maxHeight: 200);
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
					settingsContext.SetValue(settingData.SlicerConfigName, valueLocal);
					internalTextWidget.Text = valueLocal;
					internalTextWidget.OnEditComplete(null);
				};
			}

			// put in the custom menu to allow direct editing
			MenuItem customMenueItem = selectableOptions.AddItem("Custom");

			totalContent.AddChild(selectableOptions);
			content.VAnchor = VAnchor.Center;
			totalContent.AddChild(content);

			EventHandler localUnregisterEvents = null;

			ActiveSliceSettings.SettingChanged.RegisterEvent((sender, e) =>
			{
				bool foundSetting = false;
				foreach (QuickMenuNameValue nameValue in settingData.QuickMenuSettings)
				{
					string localName = nameValue.MenuName;
					string newSliceSettingValue = settingsContext.GetValue(settingData.SlicerConfigName);
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
			}, ref localUnregisterEvents);

			totalContent.Closed += (s, e) =>
			{
				localUnregisterEvents?.Invoke(s, null);
			};

			return totalContent;
		}

		public static void SaveCommaSeparatedIndexSetting(int extruderIndexLocal, SettingsContext settingsContext, string slicerConfigName, string newSingleValue)
		{
			string[] settings = settingsContext.GetValue(slicerConfigName).Split(',');
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
			settingsContext.SetValue(slicerConfigName, newValue);
		}
	}

	internal class SliceSettingListControl : ScrollableWidget
	{
		private FlowLayoutWidget topToBottomItemList;

		public SliceSettingListControl()
		{
			this.AnchorAll();
			this.AutoScroll = true;
			this.ScrollArea.HAnchor |= HAnchor.Stretch;

			topToBottomItemList = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottomItemList.HAnchor = HAnchor.MaxFitOrStretch;
			topToBottomItemList.Margin = new BorderDouble(top: 3);

			base.AddChild(topToBottomItemList);
		}

		public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
		{
			FlowLayoutWidget itemHolder = new FlowLayoutWidget();
			itemHolder.Margin = new BorderDouble(0, 0, 0, 0);
			itemHolder.HAnchor = HAnchor.MaxFitOrStretch;
			itemHolder.AddChild(child);
			itemHolder.VAnchor = VAnchor.Fit;

			topToBottomItemList.AddChild(itemHolder, indexInChildrenList);
		}
	}
}
