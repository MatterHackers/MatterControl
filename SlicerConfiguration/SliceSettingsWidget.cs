/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.SetupWizard;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{ 
	public class SliceSettingsWidget : FlowLayoutWidget
	{
		internal PresetsToolbar settingsControlBar;

		internal SettingsContext settingsContext;
		private PrinterConfig printer;
		private Color textColor;
		private SliceSettingsTabView sliceSettingsTabView;

		private EventHandler unregisterEvents;

		private ThemeConfig theme;

		public SliceSettingsWidget(PrinterConfig printer, SettingsContext settingsContext, ThemeConfig theme)
			: base (FlowDirection.TopToBottom)
		{
			this.theme = theme;
			this.printer = printer;
			this.textColor = ActiveTheme.Instance.PrimaryTextColor;
			this.BackgroundColor = ApplicationController.Instance.Theme.TabBodyBackground;

			this.settingsContext = settingsContext;

			settingsControlBar = new PresetsToolbar(printer)
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(8, 12, 8, 8)
			};

			this.AddChild(settingsControlBar);

			this.RebuildSliceSettingsTabs();

			ApplicationController.Instance.ShowHelpChanged += ShowHelp_Changed;

			this.AnchorAll();
		}

		private void ShowHelp_Changed(object sender, EventArgs e)
		{
			this.RebuildSliceSettingsTabs();
		}

		private void RebuildSliceSettingsTabs()
		{
			// Close and remove children
			sliceSettingsTabView?.Close();

			this.AddChild(
				sliceSettingsTabView = new SliceSettingsTabView(
					settingsContext,
					printer,
					this.UserLevel,
					theme,
					isPrimarySettingsView: true,
					databaseMRUKey: UserSettingsKey.SliceSettingsWidget_CurrentTab,
					extendPopupMenu: this.ExtendOverflowMenu));
		}

		private void ExtendOverflowMenu(PopupMenu popupMenu)
		{
			popupMenu.CreateHorizontalLine();
			PopupMenu.MenuItem menuItem;

			menuItem = popupMenu.CreateMenuItem("Export".Localize());
			menuItem.Click += (s, e) =>
			{
				DialogWindow.Show<ExportSettingsPage>();
			};

			menuItem = popupMenu.CreateMenuItem("Restore Settings".Localize());
			menuItem.Click += (s, e) =>
			{
				DialogWindow.Show<PrinterProfileHistoryPage>();
			};
			menuItem.Enabled = !string.IsNullOrEmpty(AuthenticationData.Instance.ActiveSessionUsername);

			menuItem = popupMenu.CreateMenuItem("Reset to Defaults".Localize());
			menuItem.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					StyledMessageBox.ShowMessageBox(
						revertSettings =>
						{
							if (revertSettings)
							{
								bool onlyReloadSliceSettings = true;
								if (printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print)
								&& printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled))
								{
									onlyReloadSliceSettings = false;
								}

								printer.Settings.ClearUserOverrides();
								printer.Settings.Save();

								if (onlyReloadSliceSettings)
								{
									printer?.Bed.GCodeRenderer?.Clear3DGCode();
								}
								else
								{
									ApplicationController.Instance.ReloadAll();
								}
							}
						},
						"Resetting to default values will remove your current overrides and restore your original printer settings.\nAre you sure you want to continue?".Localize(),
						"Revert Settings".Localize(),
						StyledMessageBox.MessageType.YES_NO);
				});
			};
		}

		public string UserLevel { get; } = "Advanced";

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
			ApplicationController.Instance.ShowHelpChanged -= ShowHelp_Changed;
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}

	public class SliceSettingsTabView : SimpleTabs
	{
		private int tabIndexForItem = 0;
		private Dictionary<string, UIField> allUiFields = new Dictionary<string, UIField>();
		private ThemeConfig theme;
		private PrinterConfig printer;
		private SettingsContext settingsContext;
		private bool isPrimarySettingsView;
		private bool showSubGroupHeadings = false;

		private SearchInputBox searchPanel;
		private int groupPanelCount = 0;
		private List<(GuiWidget widget, SliceSettingData settingData)> settingsRows;
		private TextWidget filteredItemsHeading;
		private ScrollableWidget scrollable;
		private EventHandler unregisterEvents;
		private Action<PopupMenu> externalExtendMenu;

		public SliceSettingsTabView(SettingsContext settingsContext, PrinterConfig printer, string UserLevel, ThemeConfig theme, bool isPrimarySettingsView, string databaseMRUKey, Action<PopupMenu> extendPopupMenu = null)
			: base (theme)
		{
			this.VAnchor = VAnchor.Stretch;
			this.HAnchor = HAnchor.Stretch;
			this.externalExtendMenu = extendPopupMenu;

			var overflowBar = this.TabBar as OverflowBar;
			overflowBar.ExtendOverflowMenu = this.ExtendOverflowMenu;

			var overflowButton = this.TabBar.RightAnchorItem;
			overflowButton.Name = "Slice Settings Overflow Menu";


			this.TabBar.Padding = this.TabBar.Margin.Clone(right: theme.ToolbarPadding.Right);

			searchPanel = new SearchInputBox()
			{
				Visible = false,
				BackgroundColor = theme.ActiveTabBarBackground,
				MinimumSize = new Vector2(0, this.TabBar.Height)
			};

			searchPanel.searchInput.Margin = new BorderDouble(3, 0);
			searchPanel.searchInput.ActualTextEditWidget.EnterPressed += (s, e) =>
			{
				var filter = searchPanel.searchInput.Text.Trim();

				foreach (var item in this.settingsRows)
				{
					var metaData = item.settingData;

					// Show matching items
					item.widget.Visible = metaData.SlicerConfigName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
						|| metaData.HelpText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
				}

				this.ShowFilteredView();
			};
			searchPanel.ResetButton.Click += (s, e) =>
			{
				searchPanel.Visible = false;
				searchPanel.searchInput.Text = "";

				this.ClearFilter();
			};

			// Add heading for My Settings view
			searchPanel.AddChild(filteredItemsHeading = new TextWidget("My Modified Settings", pointSize: theme.DefaultFontSize, textColor: theme.Colors.PrimaryTextColor)
			{
				Margin = new BorderDouble(left: 10),
				HAnchor = HAnchor.Left,
				VAnchor = VAnchor.Center,
				Visible = false
			}, 0);

			this.AddChild(searchPanel, 0);

			var scrollable = new ScrollableWidget(true)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			scrollable.ScrollArea.HAnchor = HAnchor.Stretch;
			//scrollable.ScrollArea.VAnchor = VAnchor.Fit;

			var tabContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Fit,
				HAnchor = HAnchor.Stretch,
				//DebugShowBounds = true,
				//MinimumSize = new Vector2(200, 200)
			};

			scrollable.AddChild(tabContainer);

			this.AddChild(scrollable);

			// Force TopToBottom flowlayout contained in scrollable as AddChild target
			this.TabContainer = tabContainer;

			this.theme = theme;
			this.printer = printer;
			this.settingsContext = settingsContext;
			this.isPrimarySettingsView = isPrimarySettingsView;

			this.TabBar.BackgroundColor = theme.ActiveTabBarBackground;

			tabIndexForItem = 0;

			var userLevel = SettingsOrganizer.Instance.UserLevels[UserLevel];

			this.settingsRows = new List<(GuiWidget, SliceSettingData)>();

			allUiFields = new Dictionary<string, UIField>();

			// Loop over categories creating a tab for each
			foreach (var category in userLevel.Categories)
			{
				if (category.Name == "Printer"
					&& (settingsContext.ViewFilter == NamedSettingsLayers.Material || settingsContext.ViewFilter == NamedSettingsLayers.Quality))
				{
					continue;
				}

				var categoryPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					VAnchor = VAnchor.Fit,
					HAnchor = HAnchor.Stretch,
				};

				// Loop over all groups in this tab and add their content
				foreach (var group in category.Groups)
				{
					if (group.Name == "Connection")
					{
						categoryPanel.AddChild(
							this.CreateOemProfileInfoRow());
					}

					categoryPanel.AddChild(
						this.CreateGroupContent(group));
				}

				this.AddTab(
					new ToolTab(
						category.Name.Localize(),
						this,
						categoryPanel,
						theme,
						hasClose: false,
						pointSize: theme.DefaultFontSize)
					{
						Name = category.Name + " Tab",
						InactiveTabColor = Color.Transparent,
						ActiveTabColor = theme.ActiveTabColor
					});
			}

			this.TabBar.AddChild(new HorizontalSpacer());

			var searchButton = ApplicationController.Instance.Theme.CreateSearchButton();
			searchButton.Click += (s, e) =>
			{
				filteredItemsHeading.Visible = false;
				searchPanel.searchInput.Visible = true;

				searchPanel.Visible = true;
				searchPanel.searchInput.Focus();
				this.TabBar.Visible = false;
			};

			this.TabBar.AddChild(searchButton);

			searchButton.VAnchor = VAnchor.Center;

			searchButton.VAnchorChanged += (s, e) => Console.WriteLine();

			// Restore the last selected tab
			if (int.TryParse(UserSettings.Instance.get(databaseMRUKey), out int tabIndex)
				&& tabIndex >= 0
				&& tabIndex < this.TabCount - 1)
			{
				this.SelectedTabIndex = tabIndex;
			}
			else
			{
				this.SelectedTabIndex = 0;
			}

			// Store the last selected tab on change
			this.ActiveTabChanged += (s, e) =>
			{
				if (settingsContext.IsPrimarySettingsView)
				{
					UserSettings.Instance.set(databaseMRUKey, this.SelectedTabIndex.ToString());
				}
			};

			ActiveSliceSettings.SettingChanged.RegisterEvent(
				(s, e) =>
				{
					if (e is StringEventArgs stringEvent)
					{
						string settingsKey = stringEvent.Data;
						if (this.allUiFields.TryGetValue(settingsKey, out UIField uifield))
						{
							string currentValue = settingsContext.GetValue(settingsKey);
							if (uifield.Value != currentValue
								|| settingsKey == "com_port")
							{
								uifield.SetValue(
									currentValue,
									userInitiated: false);
							}
						}
					}
				},
				ref unregisterEvents);
		}

		private void ExtendOverflowMenu(PopupMenu popupMenu)
		{
			popupMenu.CreateMenuItem("View Just My Settings".Localize()).Click += (s, e) =>
			{
				this.FilterToOverrides();
			};

			popupMenu.CreateHorizontalLine();

			popupMenu.CreateBoolMenuItem(
				"Show Help".Localize(),
				() => ApplicationController.Instance.ShowHelpControls,
				(value) => ApplicationController.Instance.ShowHelpControls = value);

			externalExtendMenu?.Invoke(popupMenu);
		}

		public Dictionary<string, UIField> UIFields => allUiFields;

		public FlowLayoutWidget CreateGroupContent(SettingsOrganizer.Group group)
		{
			var groupPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Fit,
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(6, 4, 6, 0),
				Name = "GroupPanel" + groupPanelCount++
			};

			var sectionWidget = new SectionWidget(group.Name, groupPanel, theme).ApplyBoxStyle();
			
			foreach (var subGroup in group.SubGroups)
			{
				var subGroupPanel = this.AddSettingRowsForSubgroup(subGroup);
				if (subGroupPanel != null)
				{
					if (showSubGroupHeadings)
					{
						var headingColor = theme.Colors.PrimaryTextColor.AdjustLightness(ActiveTheme.Instance.IsDarkTheme ? 0.5 : 2.8).ToColor();

						// Section heading
						groupPanel.AddChild(new TextWidget("  " + subGroup.Name.Localize(), textColor: headingColor, pointSize: theme.FontSize10)
						{
							Margin = new BorderDouble(left: 8, top: 6, bottom: 4),
						});
					}

					groupPanel.AddChild(subGroupPanel);
				}
			}

			return sectionWidget;
		}

		private GuiWidget AddSettingRowsForSubgroup(SettingsOrganizer.SubGroup subGroup)
		{
			var topToBottomSettings = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
			};

			topToBottomSettings.AddChild(new HorizontalLine(20));

			HorizontalLine lastLine = null;

			foreach (SliceSettingData settingData in subGroup.Settings)
			{
				// Note: tab sections may disappear if / when they are empty, as controlled by:
				// settingShouldBeShown / addedSettingToSubGroup / needToAddSubGroup
				bool settingShouldBeShown = CheckIfShouldBeShown(settingData, settingsContext);

				if (EngineMappingsMatterSlice.Instance.MapContains(settingData.SlicerConfigName)
					&& settingShouldBeShown)
				{
					var settingsRow = CreateItemRow(settingData);

					this.settingsRows.Add((settingsRow, settingData));

					topToBottomSettings.AddChild(settingsRow);

					topToBottomSettings.AddChild(lastLine = new HorizontalLine(20)
					{
						Margin = 0
					});

					if (ApplicationController.Instance.ShowHelpControls)
					{
						topToBottomSettings.AddChild(AddInHelpText(topToBottomSettings, settingData));
					}
				}
			}

			lastLine?.Close();

			return (topToBottomSettings.Children.Count == 1) ? null : topToBottomSettings;
		}

		private static bool CheckIfShouldBeShown(SliceSettingData settingData, SettingsContext settingsContext)
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

		private static GuiWidget AddInHelpText(FlowLayoutWidget topToBottomSettings, SliceSettingData settingData)
		{
			double textRegionWidth = 380 * GuiWidget.DeviceScale;
			double helpPointSize = 10;

			var allText = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(0),
				Padding = new BorderDouble(5),
			};

			allText.AddChild(
				new WrappedTextWidget(settingData.HelpText, pointSize: helpPointSize, textColor: Color.White)
				{
					Width = textRegionWidth,
					Margin = new BorderDouble(5, 0, 0, 0)
				});

			allText.MinimumSize = new Vector2(0, allText.MinimumSize.Y);
			return allText;
		}

		// Creates an information row showing the base OEM profile and its create_date value
		public GuiWidget CreateOemProfileInfoRow()
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

		internal GuiWidget CreateItemRow(SliceSettingData settingData)
		{
			return CreateItemRow(settingData, settingsContext, printer, theme.Colors.PrimaryTextColor, theme, ref tabIndexForItem, allUiFields);
		}

		public static GuiWidget CreateItemRow(SliceSettingData settingData, SettingsContext settingsContext, PrinterConfig printer, Color textColor, ThemeConfig theme, ref int tabIndexForItem, Dictionary<string, UIField> fieldCache = null)
		{
			string sliceSettingValue = settingsContext.GetValue(settingData.SlicerConfigName);

			UIField uiField = null;

			bool useDefaultSavePattern = true;
			bool placeFieldInDedicatedRow = false;

			var settingsRow = new SliceSettingsRow(printer, settingsContext, settingData, textColor)
			{
				Padding = new BorderDouble(left: 6),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};

			if (!PrinterSettings.KnownSettings.Contains(settingData.SlicerConfigName))
			{
				// the setting we think we are adding is not in the known settings it may have been deprecated
				TextWidget settingName = new TextWidget(String.Format("Setting '{0}' not found in known settings", settingData.SlicerConfigName));
				settingName.TextColor = textColor;
				settingsRow.NameArea.AddChild(settingName);
				settingsRow.NameArea.BackgroundColor = Color.Red;
			}
			else
			{
				settingsRow.NameArea.AddChild(
					CreateSettingsLabel(settingData.PresentationName.Localize(), settingData.HelpText, textColor));

				switch (settingData.DataEditType)
				{
					case SliceSettingData.DataEditTypes.INT:

						var intField = new IntField();
						uiField = intField;

						if (settingData.SlicerConfigName == "extruder_count")
						{
							intField.MaxValue = 4;
							intField.MinValue = 0;
						}

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
						uiField = new ToggleboxField(textColor);
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
					case SliceSettingData.DataEditTypes.WIDE_STRING:
						uiField = new TextField();
						break;

					case SliceSettingData.DataEditTypes.MULTI_LINE_TEXT:
						uiField = new MultilineStringField();
						placeFieldInDedicatedRow = true;
						break;

					case SliceSettingData.DataEditTypes.COM_PORT:
						useDefaultSavePattern = false;

						sliceSettingValue = printer.Settings.Helpers.ComPort();

						uiField = new ComPortField(printer, theme);
						uiField.ValueChanged += (s, e) =>
						{
							if (e.UserInitiated)
							{
								printer.Settings.Helpers.SetComPort(uiField.Value);
							}
						};

						break;

					case SliceSettingData.DataEditTypes.LIST:
						uiField = new ListField()
						{
							ListItems = settingData.ListValues.Split(',').ToList()
						};
						break;

					case SliceSettingData.DataEditTypes.HARDWARE_PRESENT:
						uiField = new ToggleboxField(textColor);
						break;

					case SliceSettingData.DataEditTypes.VECTOR2:
						uiField = new Vector2Field();
						break;

					case SliceSettingData.DataEditTypes.OFFSET2:
						placeFieldInDedicatedRow = true;
						uiField = new ExtruderOffsetField(settingsContext, settingData.SlicerConfigName, textColor);
						break;
#if !__ANDROID__
					case SliceSettingData.DataEditTypes.IP_LIST:
						uiField = new IpAddessField(printer);
						break;
#endif

					default:
						// Missing Setting
						settingsRow.AddContent(new TextWidget(String.Format("Missing the setting for '{0}'.", settingData.DataEditType.ToString()))
						{
							TextColor = textColor,
							BackgroundColor = Color.Red
						});
						break;
				}
			}

			if (uiField != null)
			{
				if (fieldCache != null)
				{
					fieldCache[settingData.SlicerConfigName] = uiField;
				}

				uiField.HelpText = settingData.HelpText;

				uiField.Name = $"{settingData.PresentationName} Field";
				uiField.Initialize(tabIndexForItem++);

				if (settingData.DataEditType == SliceSettingData.DataEditTypes.WIDE_STRING)
				{
					uiField.Content.HAnchor = HAnchor.Stretch;
					placeFieldInDedicatedRow = true;
				}

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
				//if (settingData.QuickMenuSettings.Count > 0)
				//{
				//	var dropMenu = new DropMenuWrappedField(uiField, settingData, textColor);
				//	dropMenu.Initialize(tabIndexForItem);

				//	settingsRow.AddContent(dropMenu.Content);
				//}
				//else
				{
					if (!placeFieldInDedicatedRow)
					{
						settingsRow.AddContent(uiField.Content);
					}
				}
			}

			// Invoke the UpdateStyle implementation
			settingsRow.UpdateStyle();

			bool settingEnabled = settingsContext.ParseShowString(settingData.EnableIfSet);
			if (settingEnabled
				|| settingsContext.ViewFilter == NamedSettingsLayers.Material
				|| settingsContext.ViewFilter == NamedSettingsLayers.Quality)
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
						MinimumSize = new Vector2(0, 28),
						BackgroundColor = settingsRow.BackgroundColor,
						Border = settingsRow.Border,
						Padding = settingsRow.Padding,
						Margin = settingsRow.Margin,
					};
					column.AddChild(row);

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
						row.BorderColor = settingsRow.HighlightColor;
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
				settingsRow.Enabled = false;
				return settingsRow;
			}
		}

		public static GuiWidget CreateSettingsLabel(string label, string helpText, Color textColor)
		{
			return new WrappedTextWidget(label, pointSize: 10, textColor: textColor)
			{
				VAnchor = VAnchor.Center | VAnchor.Fit,
				ToolTipText = helpText,
				Margin = new BorderDouble(0, 5, 5, 5),
			};
		}

		public void FilterToOverrides()
		{
			var defaultCascade = printer.Settings.defaultLayerCascade;

			foreach (var item in this.settingsRows)
			{
				var settingData = item.settingData;

				var (currentValue, layerName) = printer.Settings.GetValueAndLayerName(settingData.SlicerConfigName, defaultCascade);

				item.widget.Visible = layerName != "Oem" && layerName != "Base";
			}

			filteredItemsHeading.Visible = true;
			searchPanel.searchInput.Visible = false;
			this.TabBar.Visible = false;
			searchPanel.Visible = true;

			this.ShowFilteredView();
		}

		private void ShowFilteredView()
		{
			// Show any section with visible SliceSettingsRows
			foreach (var section in this.Descendants<SectionWidget>())
			{
				// HACK: Include parent visibility in mix as complex fields that return wrapped SliceSettingsRows will be visible and their parent will be hidden
				section.Visible = section.Descendants<SliceSettingsRow>().Any(row => row.Visible && row.Parent.Visible);
			}

			// Show all tab containers
			foreach (var tab in this.AllTabs)
			{
				tab.TabContent.Visible = true;
			}
		}

		protected override void OnActiveTabChanged()
		{
			if (scrollable != null)
			{
				scrollable.ScrollPosition = new Vector2(0, -scrollable.ScrollArea.Height);
			}
			base.OnActiveTabChanged();
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public void ClearFilter()
		{
			foreach (var item in this.settingsRows)
			{
				// Show matching items
				item.widget.Visible = true;
			}

			foreach (var tab in this.AllTabs)
			{
				tab.TabContent.Visible = (tab == this.ActiveTab);
			}

			foreach (var section in this.Descendants<SectionWidget>())
			{
				// HACK: Include parent visibility in mix as complex fields that return wrapped SliceSettingsRows will be visible and their parent will be hidden
				section.Visible = section.Descendants<SliceSettingsRow>().Any(row => row.Visible && row.Parent.Visible);
			}

			this.TabBar.Visible = true;
		}
	}
}
