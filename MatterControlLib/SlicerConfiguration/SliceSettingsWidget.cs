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
using System.Diagnostics;
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
		private readonly PresetsToolbar settingsControlBar;

		public SettingsContext SettingsContext { get; private set; }

		private readonly PrinterConfig printer;

		public SliceSettingsWidget(PrinterConfig printer, SettingsContext settingsContext, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.printer = printer;
			this.SettingsContext = settingsContext;

			settingsControlBar = new PresetsToolbar(printer, theme)
			{
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(8, 12, 8, 8)
			};

			using (this.LayoutLock())
			{
				this.AddChild(settingsControlBar);

				var settingsSection = PrinterSettings.Layout.SlicingSections[0];
				switch (UserSettings.Instance.get(UserSettingsKey.SliceSettingsViewDetail))
				{
					case "Simple":
						settingsSection = PrinterSettings.Layout.SlicingSections[0];
						break;

					case "Intermediate":
						settingsSection = PrinterSettings.Layout.SlicingSections[1];
						break;

					case "Advanced":
						settingsSection = PrinterSettings.Layout.SlicingSections[2];
						break;
				}

				this.AddChild(
					new SliceSettingsTabView(
						settingsContext,
						"SliceSettings",
						printer,
						settingsSection,
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
			get
			{
				return showControlBar;
			}

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
		private static readonly Regex NameSanitizer = new Regex("[^a-zA-Z0-9-]", RegexOptions.Compiled);

		private int tabIndexForItem = 0;
		private readonly Dictionary<string, UIField> allUiFields = new Dictionary<string, UIField>();
		private readonly ThemeConfig theme;
		private readonly PrinterConfig printer;
		private readonly SettingsContext settingsContext;
		private readonly bool isPrimarySettingsView;

		private readonly TextEditWithInlineCancel settingsNameEdit;
		private int groupPanelCount = 0;
		private readonly List<(GuiWidget widget, SliceSettingData settingData)> settingsRows;
		private readonly TextWidget filteredItemsHeading;
		private readonly Action<PopupMenu> externalExtendMenu;
		private readonly string scopeName;

		public SliceSettingsTabView(SettingsContext settingsContext,
			string scopeName,
			PrinterConfig printer,
			SettingsLayout.SettingsSection settingsSection,
			ThemeConfig theme,
			bool isPrimarySettingsView,
			string databaseMRUKey,
			string justMySettingsTitle,
			Action<PopupMenu> extendPopupMenu = null)
			: base(theme)
		{
			using (this.LayoutLock())
			{
				this.VAnchor = VAnchor.Stretch;
				this.HAnchor = HAnchor.Stretch;
				this.externalExtendMenu = extendPopupMenu;
				this.scopeName = scopeName;

				var overflowBar = this.TabBar as OverflowBar;
				overflowBar.ToolTipText = "Settings View Options".Localize();
				overflowBar.ExtendOverflowMenu = this.ExtendOverflowMenu;

				this.TabBar.RightAnchorItem.Name = "Slice Settings Overflow Menu";

				this.TabBar.Padding = this.TabBar.Margin.Clone(right: theme.ToolbarPadding.Right);

				settingsNameEdit = new TextEditWithInlineCancel(theme, "name".Localize())
				{
					Visible = false,
					BackgroundColor = theme.TabBarBackground,
					MinimumSize = new Vector2(0, this.TabBar.Height)
				};

				settingsNameEdit.TextEditWidget.Margin = new BorderDouble(3, 0);
				settingsNameEdit.TextEditWidget.ActualTextEditWidget.EnterPressed += (s, e) =>
				{
					var filter = settingsNameEdit.TextEditWidget.Text.Trim();

					foreach (var (widget, settingData) in this.settingsRows)
					{
						var metaData = settingData;

						// Show matching items
						widget.Visible = metaData.SlicerConfigName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
							|| metaData.HelpText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
					}

					this.ShowFilteredView();
				};

				settingsNameEdit.ResetButton.Click += (s, e) =>
				{
					settingsNameEdit.Visible = false;
					settingsNameEdit.TextEditWidget.Text = "";

					this.ClearFilter();
				};

				// Add heading for My Settings view
				settingsNameEdit.AddChild(filteredItemsHeading = new TextWidget(justMySettingsTitle, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
				{
					Margin = new BorderDouble(left: 10),
					HAnchor = HAnchor.Left,
					VAnchor = VAnchor.Center,
					Visible = false
				}, 0);

				this.AddChild(settingsNameEdit, 0);

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

				if (settingsSection.Name == "Slice Simple"
					&& UserSettings.Instance.get(UserSettingsKey.SliceSettingsMoreClicked) != "true")
				{
					var button = new TextButton("More".Localize(), theme, 8)
					{
						Margin = new BorderDouble(5, 0),
						Padding = new BorderDouble(7, 3),
						VAnchor = VAnchor.Fit | VAnchor.Center,
						HAnchor = HAnchor.Fit,
						BackgroundColor = new Color(theme.AccentMimimalOverlay, 50),
						HoverColor = theme.AccentMimimalOverlay,
						BorderColor = theme.PrimaryAccentColor,
						RenderOutline = true,
						ToolTipText = "Open Settings View Options".Localize()
					};

					bool menuWasOpenOnMoreDown = false;
					button.MouseDown += (s, e) =>
					{
						if (this.TabBar.RightAnchorItem is OverflowBar.OverflowMenuButton menuButton)
						{
							menuWasOpenOnMoreDown = menuButton.MenuVisible;
						}
					};

					button.Click += (s, e) =>
					{
						if (!menuWasOpenOnMoreDown)
						{
							this.TabBar.RightAnchorItem.InvokeClick();
						}
					};

					button.RoundRadius = button.Height / 2;

					this.TabBar.AddChild(button);
				}

				this.TabBar.AddChild(new HorizontalSpacer());

				var searchButton = theme.CreateSearchButton();
				searchButton.Click += (s, e) =>
				{
					filteredItemsHeading.Visible = false;
					settingsNameEdit.TextEditWidget.Visible = true;

					settingsNameEdit.Visible = true;
					settingsNameEdit.TextEditWidget.Focus();
					this.TabBar.Visible = false;
				};

				this.TabBar.AddChild(searchButton);

				searchButton.VAnchor = VAnchor.Center;

				searchButton.VAnchorChanged += (s, e) => Console.WriteLine();

				// Restore the last selected tab
				this.SelectedTabKey = UserSettings.Instance.get(databaseMRUKey);

				// Store the last selected tab on change
				this.ActiveTabChanged += (s, e) =>
				{
					if (settingsContext.IsPrimarySettingsView)
					{
						UserSettings.Instance.set(databaseMRUKey, this.SelectedTabKey);
					}
				};

				// Register listeners
				printer.Settings.SettingChanged += Printer_SettingChanged;
			}

			this.PerformLayout();
		}

		private void ExtendOverflowMenu(PopupMenu popupMenu)
		{
			var menu = popupMenu.CreateMenuItem("View Just My Settings".Localize());
			menu.ToolTipText = "Show all settings that are not the printer default".Localize();
			menu.Click += (s, e) =>
			{
				switch (settingsContext.ViewFilter)
				{
					case NamedSettingsLayers.All:
						this.FilterToOverrides(printer.Settings.DefaultLayerCascade);
						break;
					case NamedSettingsLayers.Material:
						this.FilterToOverrides(printer.Settings.MaterialLayerCascade);
						break;
					case NamedSettingsLayers.Quality:
						this.FilterToOverrides(printer.Settings.QualityLayerCascade);
						break;
				}
			};

			if (settingsContext.ViewFilter == NamedSettingsLayers.All)
			{
				popupMenu.CreateSeparator();

				void SetDetail(string level, bool changingTo)
				{
					UiThread.RunOnIdle(() =>
					{
						if (changingTo)
						{
							UserSettings.Instance.set(UserSettingsKey.SliceSettingsViewDetail, level);
							ApplicationController.Instance.ReloadSettings(printer);
							UserSettings.Instance.set(UserSettingsKey.SliceSettingsMoreClicked, "true");
						}
					});
				}

				int GetMenuIndex()
				{
					switch(UserSettings.Instance.get(UserSettingsKey.SliceSettingsViewDetail))
					{
						case "Simple":
							return 0;

						case "Intermediate":
							return 1;

						case "Advanced":
							return 2;
					}

					return 0;
				}

				var menuItem = popupMenu.CreateBoolMenuItem("Simple".Localize(),
					() => GetMenuIndex() == 0,
					(value) => SetDetail("Simple", value));
				menuItem.ToolTipText = "Show only the most important settings";

				menuItem = popupMenu.CreateBoolMenuItem("Intermediate".Localize(),
					() => GetMenuIndex() == 1,
					(value) => SetDetail("Intermediate", value));
				menuItem.ToolTipText = "Show commonly changed settings";

				menuItem = popupMenu.CreateBoolMenuItem("Advanced".Localize(),
					() => GetMenuIndex() == 2,
					(value) => SetDetail("Advanced", value));
				menuItem.ToolTipText = "Show all available settings";
			}

			externalExtendMenu?.Invoke(popupMenu);
		}

		public Dictionary<string, UIField> UIFields => allUiFields;

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
				NameSanitizer.Replace(group.Category.Name, ""),
				NameSanitizer.Replace(group.Name, ""));

			UIField uiField = null;

			var sectionName = group.Name.Localize();

			var sectionWidget = new SectionWidget(sectionName, groupPanel, theme, serializationKey: userSettingsKey, defaultExpansion: true, rightAlignedContent: uiField?.Content);
			theme.ApplyBoxStyle(sectionWidget);

			bool firstRow = true;
			GuiWidget settingsRow = null;

			var presetsView = settingsContext.ViewFilter == NamedSettingsLayers.Material || settingsContext.ViewFilter == NamedSettingsLayers.Quality;
			var ignoredPresets = new HashSet<string> { SettingsKey.temperature2, SettingsKey.temperature3 };

			using (groupPanel.LayoutLock())
			{
				// Add SettingRows for subgroup
				foreach (SliceSettingData settingData in group.Settings)
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

			void UpdateStyle(object s, StringEventArgs e)
			{
				if (e.Data == settingData.SlicerConfigName)
				{
					settingsRow.UpdateStyle();
				}
			}

			printer.Settings.SettingChanged += UpdateStyle;

			settingsRow.Closed += (s, e) => printer.Settings.SettingChanged -= UpdateStyle;

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

				case SliceSettingData.DataEditTypes.COLOR:
					uiField = new ColorField(theme);
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
							string checkedKey = checkbox.Checked ? "OnValue" : "OffValue";

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
						uiField = new ExtruderOffsetField(printer, settingsContext, theme);
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
				uiField.ClearUndoHistory();

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

		public void FilterToOverrides(IEnumerable<PrinterSettingsLayer> layers)
		{
			foreach (var item in this.settingsRows)
			{
				item.widget.Visible = printer.Settings.IsOverride(item.settingData.SlicerConfigName, layers);
			}

			filteredItemsHeading.Visible = true;
			settingsNameEdit.TextEditWidget.Visible = false;
			this.TabBar.Visible = false;
			settingsNameEdit.Visible = true;

			this.ShowFilteredView();
		}

		private readonly List<SectionWidget> widgetsThatWereExpanded = new List<SectionWidget>();
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
				tab.TabContent.Visible = tab == this.ActiveTab;
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
