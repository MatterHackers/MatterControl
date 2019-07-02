/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.Text.RegularExpressions;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SliceSettingsWidget : FlowLayoutWidget
	{
		internal PresetsToolbar settingsControlBar;
		internal SettingsContext settingsContext;

		private PrinterConfig printer;

		public SliceSettingsWidget(PrinterConfig printer, SettingsContext settingsContext, ThemeConfig theme)
			: base (FlowDirection.TopToBottom)
		{
			this.printer = printer;
			this.settingsContext = settingsContext;

			settingsControlBar = new PresetsToolbar(printer, theme)
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(8, 12, 8, 8)
			};

			using (this.LayoutLock())
			{
				this.AddChild(settingsControlBar);

				this.AddChild(
					new SliceSettingsTabView(
						settingsContext,
						"SliceSettings",
						printer,
						PrinterSettings.Layout.SliceSettings,
						theme,
						isPrimarySettingsView: true,
						justMySettingsTitle: "My Modified Settings".Localize(),
						databaseMRUKey: UserSettingsKey.SliceSettingsWidget_CurrentTab));
			}

			this.AnchorAll();
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
	}

	public class SliceSettingsTabView : SimpleTabs
	{
		// Sanitize group names for use as keys in db fields
		private static Regex nameSanitizer = new Regex("[^a-zA-Z0-9-]", RegexOptions.Compiled);

		private int tabIndexForItem = 0;
		private Dictionary<string, UIField> allUiFields = new Dictionary<string, UIField>();
		private ThemeConfig theme;
		private PrinterConfig printer;
		private SettingsContext settingsContext;
		private bool isPrimarySettingsView;

		private SearchInputBox searchPanel;
		private int groupPanelCount = 0;
		private List<(GuiWidget widget, SliceSettingData settingData)> settingsRows;
		private TextWidget filteredItemsHeading;
		private Action<PopupMenu> externalExtendMenu;
		private string scopeName;

		public SliceSettingsTabView(SettingsContext settingsContext, string scopeName, PrinterConfig printer, SettingsLayout.SettingsSection settingsSection, ThemeConfig theme, bool isPrimarySettingsView, string databaseMRUKey, string justMySettingsTitle, Action<PopupMenu> extendPopupMenu = null)
			: base (theme)
		{
			using (this.LayoutLock())
			{
				this.VAnchor = VAnchor.Stretch;
				this.HAnchor = HAnchor.Stretch;
				this.externalExtendMenu = extendPopupMenu;
				this.scopeName = scopeName;

				var overflowBar = this.TabBar as OverflowBar;
				overflowBar.ExtendOverflowMenu = this.ExtendOverflowMenu;

				var overflowButton = this.TabBar.RightAnchorItem;
				overflowButton.Name = "Slice Settings Overflow Menu";

				this.TabBar.Padding = this.TabBar.Margin.Clone(right: theme.ToolbarPadding.Right);

				searchPanel = new SearchInputBox(theme)
				{
					Visible = false,
					BackgroundColor = theme.TabBarBackground,
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
				searchPanel.AddChild(filteredItemsHeading = new TextWidget(justMySettingsTitle, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
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

				var tabContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					VAnchor = VAnchor.Fit,
					HAnchor = HAnchor.Stretch,
				};

				scrollable.AddChild(tabContainer);

				this.AddChild(scrollable);

				// Force TopToBottom flowlayout contained in scrollable as AddChild target
				this.TabContainer = tabContainer;

				this.theme = theme;
				this.printer = printer;
				this.settingsContext = settingsContext;
				this.isPrimarySettingsView = isPrimarySettingsView;

				this.TabBar.BackgroundColor = theme.TabBarBackground;

				tabIndexForItem = 0;

				this.settingsRows = new List<(GuiWidget, SliceSettingData)>();

				allUiFields = new Dictionary<string, UIField>();

				var errors = printer.ValidateSettings(settingsContext);

				// Loop over categories creating a tab for each
				foreach (var category in settingsSection.Categories)
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

					using (categoryPanel.LayoutLock())
					{
						// Loop over all groups in this tab and add their content
						bool hasVisibleSection = false;

						foreach (var group in category.Groups)
						{
							if (group.Name == "Connection")
							{
								categoryPanel.AddChild(
									this.CreateOemProfileInfoRow());
							}

							var groupSection = this.CreateGroupSection(group, errors);

							groupSection.Name = group.Name + " Panel";

							if (groupSection.Descendants<SliceSettingsRow>().Any())
							{
								categoryPanel.AddChild(groupSection);
							}

							hasVisibleSection = hasVisibleSection || groupSection.Checkbox.Checked;
						}

						if (!hasVisibleSection
							&& categoryPanel.Children.OfType<SectionWidget>().FirstOrDefault() is SectionWidget sectionWidget)
						{
							sectionWidget.Checkbox.Checked = true;
						}

						if (categoryPanel.Descendants<SliceSettingsRow>().Any())
						{
							this.AddTab(
								new ToolTab(
									category.Name,
									category.Name.Localize(),
									this,
									categoryPanel,
									theme,
									hasClose: false,
									pointSize: theme.DefaultFontSize)
								{
									Name = category.Name + " Tab",
									InactiveTabColor = Color.Transparent,
									ActiveTabColor = theme.BackgroundColor
								});
						}
					}

					categoryPanel.PerformLayout();
				}

				this.TabBar.AddChild(new HorizontalSpacer());

				var searchButton = theme.CreateSearchButton();
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
					&& tabIndex < this.TabCount)
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

				// Register listeners
				printer.Settings.SettingChanged += Printer_SettingChanged;
			}

			this.PerformLayout();
		}

		public enum ExpansionMode { Expanded, Collapsed }

		public void ForceExpansionMode(ExpansionMode expansionMode)
		{
			bool firstItem = true;
			foreach (var sectionWidget in this.ActiveTab.TabContent.Descendants<SectionWidget>().Reverse())
			{
				if (firstItem)
				{
					sectionWidget.Checkbox.Checked = true;
					firstItem = false;
				}
				else
				{
					sectionWidget.Checkbox.Checked = expansionMode == ExpansionMode.Expanded;
				}
			}
		}

		private void ExtendOverflowMenu(PopupMenu popupMenu)
		{
			popupMenu.CreateMenuItem("View Just My Settings".Localize()).Click += (s, e) =>
			{
				this.FilterToOverrides();
			};

			popupMenu.CreateSeparator();

			popupMenu.CreateMenuItem("Expand All".Localize()).Click += (s, e) =>
			{
				this.ForceExpansionMode(ExpansionMode.Expanded);
			};

			popupMenu.CreateMenuItem("Collapse All".Localize()).Click += (s, e) =>
			{
				this.ForceExpansionMode(ExpansionMode.Collapsed);
			};

			externalExtendMenu?.Invoke(popupMenu);
		}

		public Dictionary<string, UIField> UIFields => allUiFields;

		// Known sections which have toggle fields that enabled/disable said feature/section
		private Dictionary<string, string> toggleSwitchSectionKeys = new Dictionary<string, string>
		{
			{ "Skirt", SettingsKey.create_skirt },
			{ "Raft", SettingsKey.create_raft },
			{ "Brim", SettingsKey.create_brim },
			{ "Retraction", SettingsKey.enable_retractions },
			{ "Fan", SettingsKey.enable_fan },
		};

		public SectionWidget CreateGroupSection(SettingsLayout.Group group, List<ValidationError> errors)
		{
			var groupPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Fit,
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(6, 4, 6, 0),
				Name = "GroupPanel" + groupPanelCount++
			};

			string userSettingsKey = string.Format(
				"{0}_{1}_{2}",
				scopeName,
				nameSanitizer.Replace(group.Category.Name, ""),
				nameSanitizer.Replace(group.Name, ""));

			UIField uiField = null;

			if (toggleSwitchSectionKeys.TryGetValue(group.Name, out string toggleFieldKey))
			{
				var settingData = PrinterSettings.SettingsData[toggleFieldKey];
				uiField = CreateToggleFieldForSection(settingData);
			}

			var sectionWidget = new SectionWidget(group.Name.Localize(), groupPanel, theme, serializationKey: userSettingsKey, rightAlignedContent: uiField?.Content);
			theme.ApplyBoxStyle(sectionWidget);

			bool firstRow = true;
			GuiWidget settingsRow = null;

			var presetsView = (settingsContext.ViewFilter == NamedSettingsLayers.Material || settingsContext.ViewFilter == NamedSettingsLayers.Quality);
			var ignoredPresets = new HashSet<string> { SettingsKey.temperature2, SettingsKey.temperature3 };

			using (groupPanel.LayoutLock())
			{
				foreach (var subGroup in group.SubGroups)
				{
					// Add SettingRows for subgroup
					foreach (SliceSettingData settingData in subGroup.Settings)
					{
						// Note: tab sections may disappear if / when they are empty, as controlled by:
						// settingShouldBeShown / addedSettingToSubGroup / needToAddSubGroup
						bool settingShouldBeShown = !(presetsView && ignoredPresets.Contains(settingData.SlicerConfigName))
							&& CheckIfShouldBeShown(settingData, settingsContext);

						if (printer.Settings.IsActive(settingData.SlicerConfigName)
							&& settingShouldBeShown)
						{
							settingsRow = CreateItemRow(settingData, errors);

							if (firstRow)
							{
								// First row needs top and bottom border
								settingsRow.Border = new BorderDouble(0, 1);

								firstRow = false;
							}

							this.settingsRows.Add((settingsRow, settingData));

							groupPanel.AddChild(settingsRow);
						}
					}
				}
			}

			groupPanel.PerformLayout();

			// Hide border on last item in group
			if (settingsRow != null)
			{
				settingsRow.BorderColor = Color.Transparent;
			}

			return sectionWidget;
		}

		public override void OnLoad(EventArgs args)
		{
			systemWindow = this.Parents<SystemWindow>().FirstOrDefault();

			base.OnLoad(args);
		}

		private UIField CreateToggleFieldForSection(SliceSettingData settingData)
		{
			bool useDefaultSavePattern = false;

			string sliceSettingValue = settingsContext.GetValue(settingData.SlicerConfigName);

			// Create toggle field for key
			UIField uiField = new ToggleboxField(theme)
			{
				HelpText = settingData.HelpText,
				Name = $"{settingData.PresentationName} Field"
			};
			uiField.Initialize(tabIndexForItem++);

			uiField.ValueChanged += (s, e) =>
			{
				if (e.UserInitiated)
				{
					ICheckbox checkbox = uiField.Content as ICheckbox;
					string checkedKey = (checkbox.Checked) ? "OnValue" : "OffValue";

					// Linked settings should be updated in all cases (user clicked checkbox, user clicked clear)
					foreach (var setSettingsData in settingData.SetSettingsOnChange)
					{
						if (setSettingsData.TryGetValue(checkedKey, out string targetValue))
						{
							settingsContext.SetValue(setSettingsData["TargetSetting"], targetValue);
						}
					}

					// Store actual field value
					settingsContext.SetValue(settingData.SlicerConfigName, uiField.Value);
				}
			};

			if (allUiFields != null)
			{
				allUiFields[settingData.SlicerConfigName] = uiField;
			}

			uiField.SetValue(sliceSettingValue, userInitiated: false);

			// Second ValueChanged listener defined after SetValue to ensure it's unaffected by initial change
			uiField.ValueChanged += (s, e) =>
			{
				if (useDefaultSavePattern
					&& e.UserInitiated)
				{
					settingsContext.SetValue(settingData.SlicerConfigName, uiField.Value);
				}
			};

			uiField.Content.Margin = uiField.Content.Margin.Clone(right: 15);
			//uiField.Content.ToolTipText = settingData.HelpText;
			uiField.Content.ToolTipText = "";
			uiField.HelpText = "";

			return uiField;
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
				if (printer.Settings?.OemLayer != null)
				{
					string fromCreatedDate = printer.Settings.OemLayer.ValueOrDefault(SettingsKey.created_date);
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
					BackgroundColor = theme.SlightShade,
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

				using (row.LayoutLock())
				{
					row.AddChild(new TextWidget(title, pointSize: 9)
					{
						Margin = new BorderDouble(0, 4, 10, 4),
						TextColor = theme.TextColor,
					});

					row.AddChild(new HorizontalSpacer());

					row.AddChild(new TextWidget(lastUpdateTime, pointSize: 9)
					{
						Margin = new BorderDouble(0, 4, 10, 4),
						TextColor = theme.TextColor,
					});
				}

				row.PerformLayout();

				dataArea.AddChild(row);
			}

			return dataArea;
		}

		internal GuiWidget CreateItemRow(SliceSettingData settingData, List<ValidationError> errors)
		{
			return CreateItemRow(settingData, settingsContext, printer, theme, ref tabIndexForItem, allUiFields, errors);
		}

		public static GuiWidget CreateItemRow(SliceSettingData settingData, SettingsContext settingsContext, PrinterConfig printer, ThemeConfig theme, ref int tabIndexForItem, Dictionary<string, UIField> fieldCache = null, List<ValidationError> errors = null)
		{
			string sliceSettingValue = settingsContext.GetValue(settingData.SlicerConfigName);

			UIField uiField = null;

			bool useDefaultSavePattern = true;
			bool placeFieldInDedicatedRow = false;

			bool fullRowSelect = settingData.DataEditType == SliceSettingData.DataEditTypes.CHECK_BOX;
			var settingsRow = new SliceSettingsRow(printer, settingsContext, settingData, theme, fullRowSelect: fullRowSelect);

			switch (settingData.DataEditType)
			{
				case SliceSettingData.DataEditTypes.INT:

					var intField = new IntField(theme);
					uiField = intField;

					if (settingData.SlicerConfigName == "extruder_count")
					{
						intField.MaxValue = 4;
						intField.MinValue = 0;
					}

					break;

				case SliceSettingData.DataEditTypes.DOUBLE:
				case SliceSettingData.DataEditTypes.OFFSET:
					uiField = new DoubleField(theme);
					break;

				case SliceSettingData.DataEditTypes.SLICE_ENGINE:
					uiField = new SliceEngineField(printer, theme);
					break;

				case SliceSettingData.DataEditTypes.POSITIVE_DOUBLE:
					if (settingData.SetSettingsOnChange.Count > 0)
					{
						uiField = new BoundDoubleField(settingsContext, settingData, theme);
					}
					else
					{
						uiField = new PositiveDoubleField(theme);
					}
					break;

				case SliceSettingData.DataEditTypes.DOUBLE_OR_PERCENT:
					uiField = new DoubleOrPercentField(theme);
					break;

				case SliceSettingData.DataEditTypes.INT_OR_MM:
					uiField = new IntOrMmField(theme);
					break;

				case SliceSettingData.DataEditTypes.CHECK_BOX:
					uiField = new ToggleboxField(theme);
					useDefaultSavePattern = false;
					uiField.ValueChanged += (s, e) =>
					{
						if (e.UserInitiated)
						{
							ICheckbox checkbox = uiField.Content as ICheckbox;
							string checkedKey = (checkbox.Checked) ? "OnValue" : "OffValue";

							// Linked settings should be updated in all cases (user clicked checkbox, user clicked clear)
							foreach (var setSettingsData in settingData.SetSettingsOnChange)
							{
								if (setSettingsData.TryGetValue(checkedKey, out string targetValue))
								{
									settingsContext.SetValue(setSettingsData["TargetSetting"], targetValue);
								}
							}

							// Store actual field value
							settingsContext.SetValue(settingData.SlicerConfigName, uiField.Value);
						}
					};
					break;

				case SliceSettingData.DataEditTypes.READONLY_STRING:
					uiField = new ReadOnlyTextField(theme);
					break;

				case SliceSettingData.DataEditTypes.STRING:
				case SliceSettingData.DataEditTypes.WIDE_STRING:
					uiField = new TextField(theme);
					break;

				case SliceSettingData.DataEditTypes.MULTI_LINE_TEXT:
					uiField = new MultilineStringField(theme);
					placeFieldInDedicatedRow = true;
					break;

				case SliceSettingData.DataEditTypes.MARKDOWN_TEXT:
#if !__ANDROID__
					uiField = new MarkdownEditField(theme, settingData.PresentationName);
#endif
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
					uiField = new ListField(theme)
					{
						ListItems = settingData.ListValues.Split(',').ToList()
					};
					break;

				case SliceSettingData.DataEditTypes.HARDWARE_PRESENT:
					uiField = new ToggleboxField(theme);
					break;

				case SliceSettingData.DataEditTypes.VECTOR2:
					uiField = new Vector2Field(theme);
					break;

				case SliceSettingData.DataEditTypes.VECTOR3:
					uiField = new Vector3Field(theme);
					break;

				case SliceSettingData.DataEditTypes.VECTOR4:
					uiField = new Vector4Field(theme);
					break;

				case SliceSettingData.DataEditTypes.BOUNDS:
					uiField = new BoundsField(theme);
					break;

				case SliceSettingData.DataEditTypes.OFFSET3:
					if (settingData.SlicerConfigName == "extruder_offset")
					{
						placeFieldInDedicatedRow = true;
						uiField = new ExtruderOffsetField(printer, settingsContext, settingData.SlicerConfigName, theme.TextColor, theme);
					}
					else
					{
						uiField = new Vector3Field(theme);
					}
					break;
#if !__ANDROID__
				case SliceSettingData.DataEditTypes.IP_LIST:
					uiField = new IpAddessField(printer, theme);
					break;
#endif

				default:
					// Missing Setting
					settingsRow.AddContent(new TextWidget($"Missing the setting for '{settingData.DataEditType}'.")
					{
						TextColor = theme.TextColor,
						BackgroundColor = Color.Red
					});
					break;
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

				// Disable ToolTipText on UIFields in favor of popovers
				uiField.Content.ToolTipText = "";

				// make sure the undo data goes back to the initial value after a change
				if(uiField.Content is MHTextEditWidget textWidget)
				{
					textWidget.ActualTextEditWidget.InternalTextEditWidget.ClearUndoHistory();
				}
				else if (uiField.Content is MHNumberEdit numberWidget)
				{
					numberWidget.ActuallNumberEdit.InternalTextEditWidget.ClearUndoHistory();
				}

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
				if (settingData.QuickMenuSettings.Count > 0
					&& settingData.SlicerConfigName == "baud_rate")
				{
					var dropMenu = new DropMenuWrappedField(uiField, settingData, theme.TextColor, theme, printer);
					dropMenu.Initialize(tabIndexForItem);

					settingsRow.AddContent(dropMenu.Content);
				}
				else
				{
					if (!placeFieldInDedicatedRow)
					{
						settingsRow.AddContent(uiField.Content);
						settingsRow.ActionWidget = uiField.Content;
					}
				}
			}

			settingsRow.UIField = uiField;
			uiField.Row = settingsRow;

			if (errors?.Any() == true)
			{
				settingsRow.UpdateValidationState(errors);
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
						Padding = new BorderDouble(bottom: 10),
					};
					contentWrapper.AddChild(uiField.Content);

					row.AddChild(contentWrapper);

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

		public void FilterToOverrides()
		{
			foreach (var item in this.settingsRows)
			{
				item.widget.Visible = printer.Settings.IsOverride(item.settingData.SlicerConfigName);
			}

			filteredItemsHeading.Visible = true;
			searchPanel.searchInput.Visible = false;
			this.TabBar.Visible = false;
			searchPanel.Visible = true;

			this.ShowFilteredView();
		}

		List<SectionWidget> widgetsThatWereExpanded = new List<SectionWidget>();
		private SystemWindow systemWindow;

		private void Printer_SettingChanged(object s, StringEventArgs stringEvent)
		{
			var errors = printer.ValidateSettings(settingsContext);

			if (stringEvent != null)
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

					// Some fields are hosted outside of SettingsRows (e.g. Section Headers like Brim) and should skip validation updates
					uifield.Row?.UpdateValidationState(errors);
				}
			}
		}

		private void ShowFilteredView()
		{
			widgetsThatWereExpanded.Clear();

			// Show any section with visible SliceSettingsRows
			foreach (var section in this.Descendants<SectionWidget>())
			{
				// HACK: Include parent visibility in mix as complex fields that return wrapped SliceSettingsRows will be visible and their parent will be hidden
				section.Visible = section.Descendants<SliceSettingsRow>().Any(row => row.Visible && row.Parent.Visible);
				if (!section.Checkbox.Checked)
				{
					widgetsThatWereExpanded.Add(section);
					section.Checkbox.Checked = true;
				}
			}

			// Show all tab containers
			foreach (var tab in this.AllTabs)
			{
				tab.TabContent.Visible = true;
			}

			// Pull focus after filter
			this.Focus();
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Settings.SettingChanged -= Printer_SettingChanged;

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

			foreach (var section in widgetsThatWereExpanded)
			{
				section.Checkbox.Checked = false;
			}

			this.TabBar.Visible = true;
		}
	}
}
