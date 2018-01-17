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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ControlElements;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class NoSettingsWidget : FlowLayoutWidget
	{
		public NoSettingsWidget() : base (FlowDirection.TopToBottom)
		{
			this.AnchorAll();
			this.Padding = new BorderDouble(3, 0);

			var noConnectionMessageContainer = new AltGroupBox(new TextWidget("No Printer Selected".Localize(), pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor));
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
			this.AddChild(noConnectionMessageContainer);
		}
	}

	public class SliceSettingsWidget : GuiWidget
	{
		private TextImageButtonFactory buttonFactory = new TextImageButtonFactory();
		private SliceSettingsDetailControl sliceSettingsDetailControl;

		private TabControl topCategoryTabs;
		private AltGroupBox noConnectionMessageContainer;
		internal SettingsControlBar settingsControlBar;

		private TextImageButtonFactory textImageButtonFactory;

		private List<PrinterSettingsLayer> layerCascade = null;
		private PrinterSettingsLayer persistenceLayer = null;

		private NamedSettingsLayers viewFilter;

		internal static ImageBuffer restoreNormal;
		internal static ImageBuffer restoreHover;
		internal static ImageBuffer restorePressed;

		static SliceSettingsWidget()
		{
			// EnsureRestoreButtonImages
			int size = (int)(16 * GuiWidget.DeviceScale);

			if (OsInformation.OperatingSystem == OSType.Android)
			{
				restoreNormal = ColorCircle(size, new RGBA_Bytes(200, 0, 0));
			}
			else
			{
				restoreNormal = ColorCircle(size, new RGBA_Bytes(128, 128, 128));
			}
			restoreHover = ColorCircle(size, new RGBA_Bytes(200, 0, 0));
			restorePressed = ColorCircle(size, new RGBA_Bytes(255, 0, 0));
		}

		public SliceSettingsWidget(List<PrinterSettingsLayer> layerCascade = null, NamedSettingsLayers viewFilter = NamedSettingsLayers.All)
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

			FlowLayoutWidget pageTopToBottomLayout = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.ParentTop
			};
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

			sliceSettingsDetailControl = new SliceSettingsDetailControl(layerCascade);

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

			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.EnableChanged.RegisterEvent(onPrinterStatusChanged, ref unregisterEvents);

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
						if (layerCascade == null)
						{
							UserSettings.Instance.set(settingsName, selectedTabName);
						}
					}
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

		internal class ExtraSettingTextWidget : MHTextEditWidget
		{
			internal string itemKey { get; set; }

			internal ExtraSettingTextWidget(string itemKey, string itemValue)
				: base(itemValue)
			{
				this.itemKey = itemKey;
			}
		}

		private EventHandler unregisterEvents;

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

		private void APP_onPrinterStatusChanged(object sender, EventArgs e)
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
				noConnectionMessageContainer.Visible = false;
			}
			else
			{
				topCategoryTabs.Visible = false;
				settingsControlBar.Visible = showControlBar;
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
						numberOfCopies = ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count);
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

						foreach (SliceSettingData settingData in subGroup.SettingDataList)
						{
							// Note: tab sections may disappear if they when they are empty, as controlled by:
							// settingShouldBeShown / addedSettingToSubGroup / needToAddSubGroup
							bool settingShouldBeShown = CheckIfShouldBeShown(settingData);

							if (ActiveSliceSettings.Instance.Helpers.ActiveSliceEngine().MapContains(settingData.SlicerConfigName)
								&& settingShouldBeShown)
							{
								addedSettingToSubGroup = true;
								bool addControl;
								GuiWidget controlsForThisSetting = CreateSettingInfoUIControls(
									settingData, 
									layerCascade, 
									persistenceLayer, 
									viewFilter, 
									copyIndex,
									out addControl,
									ref tabIndexForItem);

								if (addControl)
								{
									topToBottomSettings.AddChild(controlsForThisSetting);
									GuiWidget helpBox = AddInHelpText(topToBottomSettings, settingData);
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

				if(group.Name == "Connection")
				{
					subGroupLayoutTopToBottom.AddChild(SliceSettingsWidget.CreatePrinterExtraControls(isPrimaryView: true));
				}
			}

			// Make sure we are on the right tab when we create this view
			{
				string settingsTypeName = "SliceSettingsWidget_{0}_CurrentTab".FormatWith(category.Name);
				string selectedTab = UserSettings.Instance.get(settingsTypeName);
				leftSideGroupTabs.SelectTab(selectedTab);

				leftSideGroupTabs.TabBar.TabIndexChanged += (object sender, EventArgs e) =>
				{
					string selectedTabName = leftSideGroupTabs.TabBar.SelectedTabName;
					if (!string.IsNullOrEmpty(selectedTabName))
					{
						if (layerCascade == null)
						{
							UserSettings.Instance.set(settingsTypeName, selectedTabName);
						}
					}
				};
			}

			return leftSideGroupTabs;
		}

		private bool CheckIfShouldBeShown(SliceSettingData settingData)
		{
			bool settingShouldBeShown = ActiveSliceSettings.Instance.ParseShowString(settingData.ShowIfSet, layerCascade);
			if (viewFilter == NamedSettingsLayers.Material || viewFilter == NamedSettingsLayers.Quality)
			{
				if (!settingData.ShowAsOverride)
				{
					settingShouldBeShown = false;
				}
			}

			return settingShouldBeShown;
		}

		private GuiWidget AddInHelpText(FlowLayoutWidget topToBottomSettings, SliceSettingData settingData)
		{
			FlowLayoutWidget allText = new FlowLayoutWidget(FlowDirection.TopToBottom);
			allText.HAnchor = HAnchor.ParentLeftRight;
			double textRegionWidth = 380 * GuiWidget.DeviceScale;
			allText.Margin = new BorderDouble(0);
			allText.Padding = new BorderDouble(5);
			allText.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;

			double helpPointSize = 10;

			GuiWidget helpWidget = new WrappedTextWidget(settingData.HelpText, pointSize: helpPointSize, textColor: RGBA_Bytes.White);
			helpWidget.Width = textRegionWidth;
			helpWidget.Margin = new BorderDouble(5, 0, 0, 0);
			//helpWidget.HAnchor = HAnchor.ParentLeft;
			allText.AddChild(helpWidget);

			allText.MinimumSize = new Vector2(0, allText.MinimumSize.y);
			return allText;
		}

		private static GuiWidget GetExtraSettingsWidget(SliceSettingData settingData)
		{
			var nameHolder = new GuiWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.FitToChildren | VAnchor.ParentCenter,
				Padding = new BorderDouble(5, 0),
			};

			nameHolder.AddChild(new WrappedTextWidget(settingData.ExtraSettings.Localize(), pointSize: 8, textColor: ActiveTheme.Instance.PrimaryTextColor));

			return nameHolder;
		}

		private class SettingsRow : FlowLayoutWidget
		{
			public string SettingsKey { get; set; }
			public string SettingsValue { get; set; }
			private EventHandler unregisterEvents;

			/// <summary>
			/// Gets or sets the delegate to be invoked when the settings values need to be refreshed. The implementation should 
			/// take the passed in text value and update its editor to reflect the latest value
			/// </summary>
			public Action<string> ValueChanged { get; set; }
			public Action UpdateStyle { get; set; }

			public SettingsRow(IEnumerable<PrinterSettingsLayer> layerCascade)
			{
				Margin = new BorderDouble(0, 2);
				Padding = new BorderDouble(3);
				HAnchor = Agg.UI.HAnchor.ParentLeftRight;

				ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
				{
					if (((StringEventArgs)e).Data == SettingsKey)
					{
						string setting = ActiveSliceSettings.Instance.GetValue(SettingsKey, layerCascade);
						if (SettingsValue != setting
						|| SettingsKey == "com_port")
						{
							SettingsValue = setting;
							ValueChanged?.Invoke(setting);
						}
						UpdateStyle?.Invoke();
					}
				}, ref unregisterEvents);
			}

			public override void OnClosed(ClosedEventArgs e)
			{
				unregisterEvents?.Invoke(this, null);
				base.OnClosed(e);
			}

			public void RefreshValue(IEnumerable<PrinterSettingsLayer> layerFilters)
			{
				string latestValue = GetActiveValue(this.SettingsKey, layerFilters);
				SettingsValue = latestValue;
				UpdateStyle?.Invoke();
				ValueChanged?.Invoke(latestValue);
			}
		}

		private static readonly RGBA_Bytes materialSettingBackgroundColor = new RGBA_Bytes(255, 127, 0, 108);
		private static readonly RGBA_Bytes qualitySettingBackgroundColor = new RGBA_Bytes(255, 255, 0, 108);
		public static readonly RGBA_Bytes userSettingBackgroundColor = new RGBA_Bytes(68, 95, 220, 108);

		private static string GetActiveValue(string slicerConfigName, IEnumerable<PrinterSettingsLayer> layerCascade)
		{
			return ActiveSliceSettings.Instance.GetValue(slicerConfigName, layerCascade);
		}

		public static GuiWidget CreatePrinterExtraControls(bool isPrimaryView = false)
		{
			var dataArea = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
			};

			if (isPrimaryView)
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
					HAnchor = HAnchor.ParentLeftRight
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

			// DELETE_PRINTER:
			{
				// This is a place holder type to allow us to put in the control that will allow the deletion of a printer profile
				TextImageButtonFactory buttonFactory = new TextImageButtonFactory();
				buttonFactory.borderWidth = 1;
				if (ActiveTheme.Instance.IsDarkTheme)
				{
					buttonFactory.normalBorderColor = new RGBA_Bytes(99, 99, 99);
				}
				else
				{
					buttonFactory.normalBorderColor = new RGBA_Bytes(140, 140, 140);
				}

				buttonFactory.normalTextColor = RGBA_Bytes.Red;
				var button = buttonFactory.Generate("Delete Printer".Localize());
				button.Name = "Delete Printer Button";
				button.HAnchor = HAnchor.ParentCenter;
				button.Click += (s, e) =>
				{
					StyledMessageBox.ShowMessageBox((doDelete) =>
					{
						if (doDelete)
						{
							ActiveSliceSettings.Instance.Helpers.SetMarkedForDelete(true);
						}
					}, "Are you sure you want to delete your currently selected printer?".Localize(), "Delete Printer?".Localize(), StyledMessageBox.MessageType.YES_NO, "Delete Printer".Localize());
				};
				dataArea.AddChild(button);
			}

			return dataArea;
		}

		public static GuiWidget CreateSettingControl(string sliceSettingsKey, ref int tabIndex)
		{
			bool addControl;
			GuiWidget settingsRow = CreateSettingInfoUIControls(
				SliceSettingsOrganizer.Instance.GetSettingsData(sliceSettingsKey),
				null,
				ActiveSliceSettings.Instance.UserLayer,
				NamedSettingsLayers.All,
				0,
				out addControl,
				ref tabIndex);

			if (addControl)
			{
				return settingsRow;
			}

			return null;
		}

		private static GuiWidget CreateSettingInfoUIControls(
			SliceSettingData settingData, 
			List<PrinterSettingsLayer> layerCascade, 
			PrinterSettingsLayer persistenceLayer,
			NamedSettingsLayers viewFilter,
			int extruderIndex, 
			out bool addControl, 
			ref int tabIndexForItem)
		{
			addControl = true;

			string sliceSettingValue = GetActiveValue(settingData.SlicerConfigName, layerCascade);

			GuiWidget nameArea = new GuiWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.FitToChildren | VAnchor.ParentCenter
			};
			var dataArea = new FlowLayoutWidget();
			GuiWidget unitsArea = new GuiWidget()
			{
				HAnchor = HAnchor.AbsolutePosition,
				VAnchor = VAnchor.FitToChildren | VAnchor.ParentCenter,
				Width = settingData.ShowAsOverride ? 50 * GuiWidget.DeviceScale : 5,
			};
			GuiWidget restoreArea = new GuiWidget()
			{
				HAnchor = HAnchor.AbsolutePosition,
				VAnchor = VAnchor.FitToChildren | VAnchor.ParentCenter,
				Width = settingData.ShowAsOverride ? 30 * GuiWidget.DeviceScale : 0,
			};

			var settingsRow = new SettingsRow(layerCascade)
			{
				SettingsKey = settingData.SlicerConfigName,
				SettingsValue = sliceSettingValue,
			};
			settingsRow.AddChild(nameArea);
			settingsRow.AddChild(dataArea);
			settingsRow.AddChild(unitsArea);
			settingsRow.AddChild(restoreArea);
			settingsRow.Name = settingData.SlicerConfigName + " Edit Field";

			if (!PrinterSettings.KnownSettings.Contains(settingData.SlicerConfigName))
			{
				// the setting we think we are adding is not in the known settings it may have been deprecated
				TextWidget settingName = new TextWidget(String.Format("Setting '{0}' not found in known settings", settingData.SlicerConfigName));
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

				if (settingData.DataEditType != SliceSettingData.DataEditTypes.MULTI_LINE_TEXT)
				{
					var nameHolder = new GuiWidget()
					{
						Padding = new BorderDouble(0, 0, 5, 0),
						VAnchor = VAnchor.FitToChildren | VAnchor.ParentCenter,
						HAnchor = HAnchor.ParentLeftRight,
					};

					nameHolder.AddChild(new WrappedTextWidget(settingData.PresentationName.Localize(), pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor));

					nameArea.AddChild(nameHolder);
				}

				switch (settingData.DataEditType)
				{
					case SliceSettingData.DataEditTypes.INT:
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
								ActiveSliceSettings.Instance.SetValue(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString(), persistenceLayer);
								settingsRow.UpdateStyle();
							};

							content.AddChild(intEditWidget);
							unitsArea.AddChild(GetExtraSettingsWidget(settingData));

							if (settingData.QuickMenuSettings.Count > 0)
							{
								dataArea.AddChild(CreateQuickMenu(settingData, persistenceLayer, content, intEditWidget.ActuallNumberEdit.InternalTextEditWidget, layerCascade));
							}
							else
							{
								dataArea.AddChild(content);
							}

							settingsRow.ValueChanged = (text) =>
							{
								intEditWidget.Text = text;
							};
						}
						break;

					case SliceSettingData.DataEditTypes.DOUBLE:
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
								ActiveSliceSettings.Instance.SetValue(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString(), persistenceLayer);
								settingsRow.UpdateStyle();
							};
							dataArea.AddChild(doubleEditWidget);
							unitsArea.AddChild(GetExtraSettingsWidget(settingData));

							settingsRow.ValueChanged = (text) =>
							{
								double currentValue2 = 0;
								double.TryParse(text, out currentValue2);
								doubleEditWidget.ActuallNumberEdit.Value = currentValue2;
							};
						}
						break;

					case SliceSettingData.DataEditTypes.POSITIVE_DOUBLE:
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
								string setting = GetActiveValue(settingData.SetSettingsOnChange[0]["TargetSetting"], layerCascade);
								for (int i = 1; i < settingData.SetSettingsOnChange.Count; i++)
								{
									string nextSetting = GetActiveValue(settingData.SetSettingsOnChange[i]["TargetSetting"], layerCascade);
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
									{
										ActiveSliceSettings.Instance.SetValue(settingData.SetSettingsOnChange[0]["TargetSetting"], numberEdit.Value.ToString() + "mm", persistenceLayer);
									}
								}

								// also always save to the local setting
								ActiveSliceSettings.Instance.SetValue(settingData.SlicerConfigName, numberEdit.Value.ToString(), persistenceLayer);
								settingsRow.UpdateStyle();
							};
							content.AddChild(doubleEditWidget);
							unitsArea.AddChild(GetExtraSettingsWidget(settingData));

							if (settingData.QuickMenuSettings.Count > 0)
							{
								dataArea.AddChild(CreateQuickMenu(settingData, persistenceLayer, content, doubleEditWidget.ActuallNumberEdit.InternalTextEditWidget, layerCascade));
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
							};
						}
						break;

					case SliceSettingData.DataEditTypes.OFFSET:
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
								ActiveSliceSettings.Instance.SetValue(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString(), persistenceLayer);
								settingsRow.UpdateStyle();
							};
							dataArea.AddChild(doubleEditWidget);
							unitsArea.AddChild(GetExtraSettingsWidget(settingData));

							settingsRow.ValueChanged = (text) =>
							{
								double currentValue2;
								double.TryParse(text, out currentValue2);
								doubleEditWidget.ActuallNumberEdit.Value = currentValue2;
							};
						}
						break;

					case SliceSettingData.DataEditTypes.DOUBLE_OR_PERCENT:
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
								ActiveSliceSettings.Instance.SetValue(settingData.SlicerConfigName, textEditWidget.Text, persistenceLayer);
								settingsRow.UpdateStyle();
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
								dataArea.AddChild(CreateQuickMenu(settingData, persistenceLayer, content, stringEdit.ActualTextEditWidget.InternalTextEditWidget, layerCascade));
							}
							else
							{
								dataArea.AddChild(content);
							}

							settingsRow.ValueChanged = (text) =>
							{
								stringEdit.Text = text;
							};
						}
						break;

					case SliceSettingData.DataEditTypes.INT_OR_MM:
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
								ActiveSliceSettings.Instance.SetValue(settingData.SlicerConfigName, textEditWidget.Text, persistenceLayer);
								settingsRow.UpdateStyle();

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
								dataArea.AddChild(CreateQuickMenu(settingData, persistenceLayer, content, stringEdit.ActualTextEditWidget.InternalTextEditWidget, layerCascade));
							}
							else
							{
								dataArea.AddChild(content);
							}

							settingsRow.ValueChanged = (text) =>
							{
								stringEdit.Text = text;
							};
						}
						break;

					case SliceSettingData.DataEditTypes.CHECK_BOX:
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
								// SetValue should only be called when the checkbox is clicked. If this code makes its way into checkstatechanged
								// we end up adding a key back into the dictionary after we call .ClearValue, resulting in the blue override bar reappearing after
								// clearing a useroverride with the red x
								ActiveSliceSettings.Instance.SetValue(settingData.SlicerConfigName, checkBoxWidget.Checked ? "1" : "0", persistenceLayer);
							};
							checkBoxWidget.CheckedStateChanged += (s, e) =>
							{
								// Linked settings should be updated in all cases (user clicked checkbox, user clicked clear)
								foreach (var setSettingsData in settingData.SetSettingsOnChange)
								{
									string targetValue;
									if (setSettingsData.TryGetValue(checkBoxWidget.Checked ? "OnValue" : "OffValue", out targetValue))
									{
										ActiveSliceSettings.Instance.SetValue(setSettingsData["TargetSetting"], targetValue, persistenceLayer);
									}
								}

								settingsRow.UpdateStyle();
							};
							dataArea.AddChild(checkBoxWidget);

							settingsRow.ValueChanged = (text) =>
							{
								checkBoxWidget.Checked = text == "1";
							};
						}
						break;

					case SliceSettingData.DataEditTypes.STRING:
						{
							var stringEdit = new MHTextEditWidget(sliceSettingValue, pixelWidth: settingData.ShowAsOverride ? 120 : 200, tabIndex: tabIndexForItem++)
							{
								Name = settingData.PresentationName + " Edit",
							};
							stringEdit.ToolTipText = settingData.HelpText;
							
							stringEdit.ActualTextEditWidget.EditComplete += (sender, e) =>
							{
								ActiveSliceSettings.Instance.SetValue(settingData.SlicerConfigName, ((TextEditWidget)sender).Text, persistenceLayer);
								settingsRow.UpdateStyle();
							};

							dataArea.AddChild(stringEdit);

							settingsRow.ValueChanged = (text) =>
							{
								stringEdit.Text = text;
							};
						}
						break;

					case SliceSettingData.DataEditTypes.MULTI_LINE_TEXT:
						{
							string convertedNewLines = sliceSettingValue.Replace("\\n", "\n");
							var stringEdit = new MHTextEditWidget(convertedNewLines, pixelWidth: 320, pixelHeight: multiLineEditHeight, multiLine: true, tabIndex: tabIndexForItem++, typeFace: ApplicationController.MonoSpacedTypeFace)
							{
								HAnchor = HAnchor.ParentLeftRight,
							};

							stringEdit.DrawFromHintedCache();

							stringEdit.ActualTextEditWidget.EditComplete += (sender, e) =>
							{
								ActiveSliceSettings.Instance.SetValue(settingData.SlicerConfigName, ((TextEditWidget)sender).Text.Replace("\n", "\\n"), persistenceLayer);
								settingsRow.UpdateStyle();
							};

							nameArea.HAnchor = HAnchor.AbsolutePosition;
							nameArea.Width = 0;
							dataArea.AddChild(stringEdit);
							dataArea.HAnchor = HAnchor.ParentLeftRight;

							settingsRow.ValueChanged = (text) =>
							{
								stringEdit.Text = text.Replace("\\n", "\n");
							};
						}
						break;

					case SliceSettingData.DataEditTypes.COM_PORT:
						{
#if __ANDROID__
							addControl = false;
#endif

							EventHandler localUnregisterEvents = null;

							bool canChangeComPort = !PrinterConnectionAndCommunication.Instance.PrinterIsConnected && PrinterConnectionAndCommunication.Instance.CommunicationState != PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect;
							// The COM_PORT control is unique in its approach to the SlicerConfigName. It uses "com_port" settings name to
							// bind to a context that will place it in the SliceSetting view but it binds its values to a machine
							// specific dictionary key that is not exposed in the UI. At runtime we lookup and store to '<machinename>_com_port'
							// ensuring that a single printer can be shared across different devices and we'll select the correct com port in each case
							var selectableOptions = new DropDownList("None".Localize(), maxHeight: 200)
							{
								ToolTipText = settingData.HelpText,
								Margin = new BorderDouble(),
								Name = "Serial Port Dropdown",
								// Prevent droplist interaction when connected
								Enabled = canChangeComPort,
								TextColor = canChangeComPort ? ActiveTheme.Instance.PrimaryTextColor : new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 150),
								BorderColor = canChangeComPort ? ActiveTheme.Instance.SecondaryTextColor : new RGBA_Bytes(ActiveTheme.Instance.SecondaryTextColor, 150),
							};

							selectableOptions.Click += (s, e) =>
							{
								AddComMenuItems(settingData, persistenceLayer, settingsRow, selectableOptions);
							};

							AddComMenuItems(settingData, persistenceLayer, settingsRow, selectableOptions);

							dataArea.AddChild(selectableOptions);

							settingsRow.ValueChanged = (text) =>
							{
								// Lookup the machine specific comport value rather than the passed in text value
								selectableOptions.SelectedLabel = ActiveSliceSettings.Instance.Helpers.ComPort();
							};

							// Prevent droplist interaction when connected
							PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent((s, e) =>
							{
								canChangeComPort = !PrinterConnectionAndCommunication.Instance.PrinterIsConnected && PrinterConnectionAndCommunication.Instance.CommunicationState != PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect;
								selectableOptions.Enabled = canChangeComPort;
								selectableOptions.TextColor = canChangeComPort ? ActiveTheme.Instance.PrimaryTextColor : new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 150);
								selectableOptions.BorderColor = canChangeComPort ? ActiveTheme.Instance.SecondaryTextColor : new RGBA_Bytes(ActiveTheme.Instance.SecondaryTextColor, 150);
							}, ref localUnregisterEvents);

							// Release event listener on close
							selectableOptions.Closed += (s, e) =>
							{
								localUnregisterEvents?.Invoke(null, null);
							};
						}
						break;

					case SliceSettingData.DataEditTypes.LIST:
						{
							var selectableOptions = new DropDownList("None".Localize(), maxHeight: 200)
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
									ActiveSliceSettings.Instance.SetValue(settingData.SlicerConfigName, menuItem.Text, persistenceLayer);

									settingsRow.UpdateStyle();
								};
							}

							dataArea.AddChild(selectableOptions);

							settingsRow.ValueChanged = (text) =>
							{
								selectableOptions.SelectedLabel = text;
							};
						}
						break;

					case SliceSettingData.DataEditTypes.HARDWARE_PRESENT:
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
								ActiveSliceSettings.Instance.SetValue(settingData.SlicerConfigName, isChecked ? "1" : "0", persistenceLayer);

								settingsRow.UpdateStyle();
							};

							dataArea.AddChild(checkBoxWidget);

							settingsRow.ValueChanged = (text) =>
							{
								checkBoxWidget.Checked = text == "1";
							};
						}
						break;

					case SliceSettingData.DataEditTypes.VECTOR2:
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
								ActiveSliceSettings.Instance.SetValue(settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "," + yEditWidget.ActuallNumberEdit.Value.ToString(), persistenceLayer);

								settingsRow.UpdateStyle();
							};
							dataArea.AddChild(xEditWidget);
							dataArea.AddChild(new TextWidget("X", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor)
							{
								VAnchor = VAnchor.ParentCenter,
								Margin = new BorderDouble(5, 0),
							});

							yEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
							{
								ActiveSliceSettings.Instance.SetValue(settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "," + yEditWidget.ActuallNumberEdit.Value.ToString(), persistenceLayer);

								settingsRow.UpdateStyle();
							};
							dataArea.AddChild(yEditWidget);
							var yLabel = new GuiWidget()
							{
								VAnchor = VAnchor.FitToChildren | VAnchor.ParentCenter,
								Padding = new BorderDouble(5, 0),
								HAnchor = HAnchor.ParentLeftRight,
							};
							yLabel.AddChild(new WrappedTextWidget("Y", pointSize: 9, textColor: ActiveTheme.Instance.PrimaryTextColor));
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
							};

						}
						break;

					case SliceSettingData.DataEditTypes.OFFSET2:
						{
							Vector2 offset = ActiveSliceSettings.Instance.Helpers.ExtruderOffset(extruderIndex);

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
								SaveCommaSeparatedIndexSetting(extruderIndexLocal, layerCascade, settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "x" + yEditWidget.ActuallNumberEdit.Value.ToString(), persistenceLayer);

								settingsRow.UpdateStyle();
							};
							dataArea.AddChild(xEditWidget);
							dataArea.AddChild(new TextWidget("X", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor)
							{
								VAnchor = VAnchor.ParentCenter,
								Margin = new BorderDouble(5, 0),
							});

							yEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
							{
								int extruderIndexLocal = extruderIndex;
								SaveCommaSeparatedIndexSetting(extruderIndexLocal, layerCascade, settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "x" + yEditWidget.ActuallNumberEdit.Value.ToString(), persistenceLayer);

								settingsRow.UpdateStyle();
							};
							dataArea.AddChild(yEditWidget);
							var yLabel = new GuiWidget()
							{
								Padding = new BorderDouble(5, 0),
								HAnchor = HAnchor.ParentLeftRight,
								VAnchor = VAnchor.FitToChildren | VAnchor.ParentCenter,
							};
							yLabel.AddChild(new WrappedTextWidget("Y", pointSize: 9, textColor: ActiveTheme.Instance.PrimaryTextColor));
							unitsArea.AddChild(yLabel);

							settingsRow.ValueChanged = (text) =>
							{
								Vector2 offset2 = ActiveSliceSettings.Instance.Helpers.ExtruderOffset(extruderIndex);
								xEditWidget.ActuallNumberEdit.Value = offset2.x;
								yEditWidget.ActuallNumberEdit.Value = offset2.y;
							};
						}
						break;
					case SliceSettingData.DataEditTypes.IP_LIST:
						//ip_selector = new IpAddressField(printer);
						//dataArea.AddChild(ip_selector);

						EventHandler unregisterEvents = null;
						
						bool isEnabled = !PrinterConnectionAndCommunication.Instance.PrinterIsConnected && PrinterConnectionAndCommunication.Instance.CommunicationState != PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect;
						//This setting defaults to Manual
						var selectedMachine = ActiveSliceSettings.Instance.GetValue(SettingsKey.selector_ip_address);
						var dropdownList = new DropDownList(selectedMachine, maxHeight: 200)
						{
							ToolTipText = settingData.HelpText,
							Margin = new BorderDouble(),
							TabIndex = tabIndexForItem,
							MenuItemsPadding = new BorderDouble(10, 4, 10, 6),

							Enabled = isEnabled,
							TextColor = isEnabled ? ActiveTheme.Instance.PrimaryTextColor : new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 150),
							BorderColor = isEnabled ? ActiveTheme.Instance.SecondaryTextColor : new RGBA_Bytes(ActiveTheme.Instance.SecondaryTextColor, 150),

						};

						//Create default option
						MenuItem defaultOption = dropdownList.AddItem("Manual", "127.0.0.1:23");
						defaultOption.Selected += (sender, e) =>
						{
							ActiveSliceSettings.Instance.SetValue(SettingsKey.selector_ip_address, defaultOption.Text);
						};


						// Prevent droplist interaction when connected
						PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent((s, e) =>
						{
							isEnabled = !PrinterConnectionAndCommunication.Instance.PrinterIsConnected && PrinterConnectionAndCommunication.Instance.CommunicationState != PrinterConnectionAndCommunication.CommunicationStates.AttemptingToConnect;
							dropdownList.Enabled = isEnabled;
							dropdownList.TextColor = isEnabled ? ActiveTheme.Instance.PrimaryTextColor : new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 150);
							dropdownList.BorderColor = isEnabled ? ActiveTheme.Instance.SecondaryTextColor : new RGBA_Bytes(ActiveTheme.Instance.SecondaryTextColor, 150);
						}, ref unregisterEvents);

						// Release event listener on close
						dropdownList.Closed += (s, e) =>
						{
							unregisterEvents?.Invoke(null, null);
						};

						// Release event listener on close
						dropdownList.Closed += (s, e) =>
						{
							unregisterEvents?.Invoke(null, null);
						};

						var widget = new FlowLayoutWidget();
						widget.AddChild(dropdownList);
						var buttonFactory = new TextImageButtonFactory();
						buttonFactory.normalFillColor = RGBA_Bytes.Transparent;
						buttonFactory.fontSize = 8;
						buttonFactory.borderWidth = 1;
						buttonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
						buttonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

						buttonFactory.disabledTextColor = RGBA_Bytes.Gray;
						buttonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
						buttonFactory.normalTextColor = ActiveTheme.Instance.SecondaryTextColor;
						buttonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

						buttonFactory.FixedHeight = dropdownList.Height;
						buttonFactory.fontSize = 10;
						buttonFactory.normalTextColor = RGBA_Bytes.DarkGray;
						var refreshButton = buttonFactory.Generate("", StaticData.Instance.LoadIcon("fa-refresh_14.png"));
						refreshButton.Margin = new BorderDouble(left: 5);


						UiThread.RunOnIdle(() =>
						{
							IpAddressField.RebuildMenuItems(refreshButton,dropdownList);
						});
						refreshButton.Click += (s, e) => IpAddressField.RebuildMenuItems(refreshButton, dropdownList);
						widget.AddChild(refreshButton);

						dataArea.AddChild(widget);

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

			Button restoreButton = null;
			if (settingData.ShowAsOverride)
			{
				restoreButton = new Button(new ButtonViewStates(new ImageWidget(restoreNormal), new ImageWidget(restoreHover), new ImageWidget(restorePressed), new ImageWidget(restoreNormal)))
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
			}

			// Define the UpdateStyle implementation
			settingsRow.UpdateStyle = () =>
			{
				if (persistenceLayer.ContainsKey(settingData.SlicerConfigName))
				{
					switch (viewFilter)
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
										settingsRow.BackgroundColor = materialSettingBackgroundColor;
									}
									else if (layerName.StartsWith("Quality"))
									{
										settingsRow.BackgroundColor = qualitySettingBackgroundColor;
									}
									else
									{
										settingsRow.BackgroundColor = RGBA_Bytes.Transparent;
									}

									if (restoreButton != null)
									{
										restoreButton.Visible = false;
									}
								}
								else
								{
									settingsRow.BackgroundColor = userSettingBackgroundColor;
									if (restoreButton != null) restoreButton.Visible = true;
								}
							}
							break;
						case NamedSettingsLayers.Material:
							settingsRow.BackgroundColor = materialSettingBackgroundColor;
							if (restoreButton != null) restoreButton.Visible = true;
							break;
						case NamedSettingsLayers.Quality:
							settingsRow.BackgroundColor = qualitySettingBackgroundColor;
							if (restoreButton != null) restoreButton.Visible = true;
							break;
					}
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

					if (restoreButton != null) restoreButton.Visible = false;
				}
				else
				{
					if (restoreButton != null) restoreButton.Visible = false;
					settingsRow.BackgroundColor = RGBA_Bytes.Transparent;
				}
			};

			// Invoke the UpdateStyle implementation
			settingsRow.UpdateStyle();

			return settingsRow;
		}

		private static void AddComMenuItems(SliceSettingData settingData, PrinterSettingsLayer persistenceLayer, SettingsRow settingsRow, DropDownList selectableOptions)
		{
			selectableOptions.MenuItems.Clear();
			string machineSpecificComPortValue = ActiveSliceSettings.Instance.Helpers.ComPort();
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
						ActiveSliceSettings.Instance.Helpers.SetComPort(menuItem.Text);
					}
					else
					{
						ActiveSliceSettings.Instance.Helpers.SetComPort(menuItem.Text, persistenceLayer);
					}

					settingsRow.UpdateStyle();
				};
			}
		}

		private static ImageBuffer ColorCircle(int size, RGBA_Bytes color)
		{
			ImageBuffer imageBuffer = new ImageBuffer(size, size);
			Graphics2D normalGraphics = imageBuffer.NewGraphics2D();
			Vector2 center = new Vector2(size / 2.0, size / 2.0);
			normalGraphics.Circle(center, size / 2.0, color);
			normalGraphics.Line(center + new Vector2(-size / 4.0, -size / 4.0), center + new Vector2(size / 4.0, size / 4.0), RGBA_Bytes.White, 2 * GuiWidget.DeviceScale);
			normalGraphics.Line(center + new Vector2(-size / 4.0, size / 4.0), center + new Vector2(size / 4.0, -size / 4.0), RGBA_Bytes.White, 2 * GuiWidget.DeviceScale);

			return imageBuffer;
		}

		private static GuiWidget CreateQuickMenu(SliceSettingData settingData, PrinterSettingsLayer persistenceLayer, GuiWidget content, InternalTextEditWidget internalTextWidget, List<PrinterSettingsLayer> layerCascade)
		{
			string sliceSettingValue = GetActiveValue(settingData.SlicerConfigName, layerCascade);
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
					ActiveSliceSettings.Instance.SetValue(settingData.SlicerConfigName, valueLocal, persistenceLayer);
					internalTextWidget.Text = valueLocal;
					internalTextWidget.OnEditComplete(null);
				};
			}

			// put in the custom menu to allow direct editing
			MenuItem customMenueItem = selectableOptions.AddItem("Custom");

			totalContent.AddChild(selectableOptions);
			content.VAnchor = VAnchor.ParentCenter;
			totalContent.AddChild(content);

			EventHandler localUnregisterEvents = null;

			ActiveSliceSettings.SettingChanged.RegisterEvent((sender, e) =>
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
			}, ref localUnregisterEvents);

			totalContent.Closed += (s, e) =>
			{
				localUnregisterEvents?.Invoke(s, null);
			};

			return totalContent;
		}

		private static void SaveCommaSeparatedIndexSetting(int extruderIndexLocal, List<PrinterSettingsLayer> layerCascade, string slicerConfigName, string newSingleValue, PrinterSettingsLayer persistenceLayer)
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
			ActiveSliceSettings.Instance.SetValue(slicerConfigName, newValue, persistenceLayer);
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
