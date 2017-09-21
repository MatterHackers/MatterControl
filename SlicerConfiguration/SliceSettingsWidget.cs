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
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public partial class SliceSettingsWidget : FlowLayoutWidget
	{
		private TabControl primaryTabControl;
		internal PresetsToolbar settingsControlBar;

		private SettingsContext settingsContext;
		private PrinterConfig printer;

		private Dictionary<string, UIField> allUiFields = new Dictionary<string, UIField>();

		private EventHandler unregisterEvents;

		public SliceSettingsWidget(PrinterConfig printer, SettingsContext settingsContext)
			: base (FlowDirection.TopToBottom)
		{
			this.printer = printer;
			this.BackgroundColor = ApplicationController.Instance.Theme.TabBodyBackground;

			this.settingsContext = settingsContext;

			settingsControlBar = new PresetsToolbar(printer)
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(8, 12, 8, 8)
			};

			this.AddChild(settingsControlBar);

			RebuildSliceSettingsTabs();

			ActiveSliceSettings.SettingChanged.RegisterEvent(
				(s, e) =>
				{
					if (e is StringEventArgs stringEvent)
					{
						string settingsKey = stringEvent.Data;
						if (allUiFields.TryGetValue(settingsKey, out UIField field2))
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
			// Close and remove children
			primaryTabControl?.Close();

			primaryTabControl = new TabControl();
			primaryTabControl.TabBar.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
			primaryTabControl.Margin = new BorderDouble(top: 8);
			primaryTabControl.AnchorAll();

			var sideTabBarsListForLayout = new List<TabBar>();

			for (int topCategoryIndex = 0; topCategoryIndex < SliceSettingsOrganizer.Instance.UserLevels[UserLevel].CategoriesList.Count; topCategoryIndex++)
			{
				OrganizerCategory category = SliceSettingsOrganizer.Instance.UserLevels[UserLevel].CategoriesList[topCategoryIndex];

				var categoryPage = new TabPage(category.Name.Localize());
				categoryPage.AnchorAll();

				primaryTabControl.AddTab(new TextTab(
					categoryPage,
					category.Name + " Tab",
					14,
					ActiveTheme.Instance.TabLabelSelected,
					new RGBA_Bytes(),
					ActiveTheme.Instance.TabLabelUnselected,
					new RGBA_Bytes(),
					useUnderlineStyling: true));


				var column = new FlowLayoutWidget(FlowDirection.TopToBottom);
				column.AnchorAll();

				var hline = new HorizontalLine()
				{
					BackgroundColor = ApplicationController.Instance.Theme.SlightShade,
					Height = 4
				};
				column.AddChild(hline);

				TabControl sideTabs = CreateSideTabsAndPages(category, this.ShowHelpControls);
				sideTabBarsListForLayout.Add(sideTabs.TabBar);
				column.AddChild(sideTabs);

				categoryPage.AddChild(column);
			}

			primaryTabControl.TabBar.AddChild(new HorizontalSpacer());

			if (settingsContext.IsPrimarySettingsView)
			{
				var sliceSettingsDetailControl = new SliceSettingsOverflowDropdown(printer, this);
				primaryTabControl.TabBar.AddChild(sliceSettingsDetailControl);
			}

			FindWidestTabAndSetAllMinimumSize(sideTabBarsListForLayout);

			// check if there is only one left side tab. If so hide the left tabs and expand the content.
			foreach (var tabList in sideTabBarsListForLayout)
			{
				if (tabList.CountVisibleChildren() == 1)
				{
					tabList.MinimumSize = new Vector2(0, 0);
					tabList.Width = 0;
				}
			}

			this.AddChild(primaryTabControl);

			// Restore the last selected tab
			primaryTabControl.SelectTab(UserSettings.Instance.get(UserSettingsKey.SliceSettingsWidget_CurrentTab));

			// Store the last selected tab on change
			primaryTabControl.TabBar.TabIndexChanged += (s, e) =>
			{
				if (!string.IsNullOrEmpty(primaryTabControl.TabBar.SelectedTabName)
					&& settingsContext.IsPrimarySettingsView)
				{
					UserSettings.Instance.set(UserSettingsKey.SliceSettingsWidget_CurrentTab, primaryTabControl.TabBar.SelectedTabName);
				}
			};
		}

		private static void FindWidestTabAndSetAllMinimumSize(List<TabBar> sideTabBarsListForLayout)
		{
			double sideTabBarsMinimumWidth = 0;
			foreach (TabBar tabBar in sideTabBarsListForLayout)
			{
				sideTabBarsMinimumWidth = Math.Max(sideTabBarsMinimumWidth, tabBar.Width);
			}
			foreach (TabBar tabBar in sideTabBarsListForLayout)
			{
				tabBar.MinimumSize = new Vector2(sideTabBarsMinimumWidth, tabBar.MinimumSize.y);
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

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private int tabIndexForItem = 0;

		internal bool ShowHelpControls
		{
			get => UserSettings.Instance.get(UserSettingsKey.SliceSettingsShowHelp) == "true";
			set => UserSettings.Instance.set(UserSettingsKey.SliceSettingsShowHelp, value.ToString().ToLower());
		}

		private TabControl CreateSideTabsAndPages(OrganizerCategory category, bool showHelpControls)
		{
			this.HAnchor = HAnchor.Stretch;

			var secondaryTabControl = new TabControl(Orientation.Vertical);
			secondaryTabControl.TabBar.HAnchor = HAnchor.Fit;
			secondaryTabControl.TabBar.BorderColor = RGBA_Bytes.Transparent;
			secondaryTabControl.TabBar.BackgroundColor = ApplicationController.Instance.Theme.SlightShade;

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
					ActiveTheme.Instance.TertiaryBackgroundColor, 
					ActiveTheme.Instance.TabLabelUnselected,
					RGBA_Bytes.Transparent,
					32);
				groupTabWidget.HAnchor = HAnchor.MaxFitOrStretch;

				foreach(var child in groupTabWidget.Children)
				{
					child.HAnchor = HAnchor.MaxFitOrStretch;
					child.Padding = new BorderDouble(10);
				}

				var subGroupLayoutTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
				subGroupLayoutTopToBottom.AnchorAll();

				bool needToAddSubGroup = false;
				foreach (OrganizerSubGroup subGroup in group.SubGroupsList)
				{
					string subGroupTitle = subGroup.Name;

					bool addedSettingToSubGroup = false;

					var topToBottomSettings = new FlowLayoutWidget(FlowDirection.TopToBottom)
					{
						HAnchor = HAnchor.Stretch
					};

					GuiWidget hline = new HorizontalLine(20)
					{
						Margin = new BorderDouble(top: 5)
					};
					topToBottomSettings.AddChild(hline);

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
								CreateItemRow(settingData, ref tabIndexForItem));

							hline = new HorizontalLine(20)
							{
								Margin = 0
							};
							topToBottomSettings.AddChild(hline);

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
							Margin = new BorderDouble(bottom: 8, top: 8),
							Padding = new BorderDouble(left: 4),
						};
						groupBox.AddChild(topToBottomSettings);

						subGroupLayoutTopToBottom.AddChild(groupBox);
					}
				}

				if (needToAddSubGroup)
				{
					SliceSettingListControl scrollOnGroupTab = new SliceSettingListControl();

					subGroupLayoutTopToBottom.VAnchor = VAnchor.Fit;
					subGroupLayoutTopToBottom.HAnchor = HAnchor.Stretch;

					scrollOnGroupTab.AddChild(subGroupLayoutTopToBottom);
					groupTabPage.AddChild(scrollOnGroupTab);
					secondaryTabControl.AddTab(groupTabWidget);
				}

				if (group.Name == "Connection")
				{
					subGroupLayoutTopToBottom.AddChild(SliceSettingsWidget.CreateOemProfileInfoRow(settingsContext, isPrimarySettingsView: true));
				}
			}

			// Make sure we are on the right tab when we create this view
			string settingsTypeName = $"SliceSettingsWidget_{category.Name}_CurrentTab";
			string selectedTab = UserSettings.Instance.get(settingsTypeName);
			secondaryTabControl.SelectTab(selectedTab);

			secondaryTabControl.TabBar.TabIndexChanged += (object sender, EventArgs e) =>
			{
				string selectedTabName = secondaryTabControl.TabBar.SelectedTabName;
				if (!string.IsNullOrEmpty(selectedTabName)
					&& settingsContext.IsPrimarySettingsView)
				{
					UserSettings.Instance.set(settingsTypeName, selectedTabName);
				}
			};

			return secondaryTabControl;
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

		// Creates an information row showing the base OEM profile and its create_date value
		public static GuiWidget CreateOemProfileInfoRow(SettingsContext settingsContext, bool isPrimarySettingsView = false)
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

				string make = settingsContext.GetValue(SettingsKey.make);
				string model = settingsContext.GetValue(SettingsKey.model);

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

		private GuiWidget CreateItemRow(SliceSettingData settingData, ref int tabIndexForItem)
		{
			string sliceSettingValue = settingsContext.GetValue(settingData.SlicerConfigName);

			UIField uiField = null;

			bool useDefaultSavePattern = true;
			bool placeFieldInDedicatedRow = false;

			var settingsRow = new SliceSettingsRow(printer, settingsContext, settingData)
			{
				Margin = new BorderDouble(0, 0),
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
				settingsRow.NameArea.AddChild(
					CreateSettingsLabel(settingData.PresentationName.Localize(), settingData.HelpText));

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
							uiField = new PositiveDoubleField();
						};
						break;

					case SliceSettingData.DataEditTypes.DOUBLE_OR_PERCENT:
						uiField = new DoubleOrPercentField();
						break;

					case SliceSettingData.DataEditTypes.INT_OR_MM:
						uiField = new IntOrMmField();
						break;

					case SliceSettingData.DataEditTypes.CHECK_BOX:
						uiField = new ToggleboxField();
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
						placeFieldInDedicatedRow = true;
						break;

					case SliceSettingData.DataEditTypes.COM_PORT:
						useDefaultSavePattern = false;

						uiField = new ComPortField(printer);
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
						uiField = new ToggleboxField();
						break;

					case SliceSettingData.DataEditTypes.VECTOR2:
						uiField = new Vector2Field();
						break;

					case SliceSettingData.DataEditTypes.OFFSET2:
						placeFieldInDedicatedRow = true;
						uiField = new ExtruderOffsetField(settingsContext, settingData.SlicerConfigName);
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

				uiField.Name = $"{settingData.PresentationName} Field";
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
					var dropMenu = new DropMenuWrappedField(uiField, settingData);
					dropMenu.Initialize(tabIndexForItem);

					settingsRow.AddContent(dropMenu.Content);
				}
				else
				{
					if (!placeFieldInDedicatedRow)
					{
						settingsRow.AddContent(uiField.Content);
					}
				}
			}

			// Invoke the UpdateStyle implementation
			settingsRow.UpdateStyle();

			bool settingShouldEnabled = settingsContext.ParseShowString(settingData.EnableIfSet);
			if (settingShouldEnabled)
			{
				if (placeFieldInDedicatedRow)
				{
					var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
					{
						Name = "column",
						HAnchor = HAnchor.Stretch,
						VAnchor = VAnchor.Fit
					};
					column.AddChild(settingsRow);

					var row = new FlowLayoutWidget()
					{
						Name = "row",
						VAnchor = VAnchor.Fit,
						HAnchor = HAnchor.Stretch,
						BackgroundColor = settingsRow.BackgroundColor
					};
					column.AddChild(row);

					var vline = new VerticalLine()
					{
						BackgroundColor = settingsRow.HighlightColor,
						Margin = new BorderDouble(right: 6, bottom: 2),
						Width = 3,
						VAnchor = VAnchor.Stretch,
						MinimumSize = new Vector2(0, 28),
					};
					row.AddChild(vline);

					var contentWrapper = new GuiWidget
					{
						Name = "contentWrapper",
						HAnchor = HAnchor.Stretch,
						VAnchor = VAnchor.Fit,
						Padding = new BorderDouble(right: 16, bottom: 10),
					};
					contentWrapper.AddChild(uiField.Content);

					row.AddChild(contentWrapper);

					settingsRow.StyleChanged += (s, e) =>
					{
						row.BackgroundColor = settingsRow.BackgroundColor;
						vline.BackgroundColor = settingsRow.HighlightColor;
					};

					return column;
				}
				else
				{
					return settingsRow;
				}
			}
			else
			{
				return new DisableablePanel(settingsRow);
			}
		}

		public static TextWidget CreateSettingsLabel(string label, string helpText)
		{
			return new TextWidget(label, pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				VAnchor = VAnchor.Center,
				EllipsisIfClipped = true,
				AutoExpandBoundsToText = false,
				ToolTipText = helpText,
			};
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
	}
}
