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
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SliceSettingsWidget : FlowLayoutWidget
	{
		private SimpleTabs primaryTabControl;
		internal PresetsToolbar settingsControlBar;

		internal SettingsContext settingsContext;
		private PrinterConfig printer;
		private Color textColor;
		private Dictionary<string, UIField> allUiFields = new Dictionary<string, UIField>();

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

			var rightItem = (settingsContext.IsPrimarySettingsView) ? new SliceSettingsOverflowMenu(printer, this) : new GuiWidget();

			primaryTabControl = new SimpleTabs(rightItem)
			{
				Margin = new BorderDouble(top: 8),
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Stretch,
				MinimumSize = new Vector2(200, 200),
			};
			primaryTabControl.TabBar.BackgroundColor = theme.ActiveTabBarBackground;

			for (int topCategoryIndex = 0; topCategoryIndex < SettingsOrganizer.Instance.UserLevels[UserLevel].Categories.Count; topCategoryIndex++)
			{
				var category = SettingsOrganizer.Instance.UserLevels[UserLevel].Categories[topCategoryIndex];
				if (category.Name == "Printer"
					&& (settingsContext.ViewFilter == NamedSettingsLayers.Material || settingsContext.ViewFilter == NamedSettingsLayers.Quality))
				{
					continue;
				}

				var content = CreateSideTabsAndPages(category, this.ShowHelpControls);
				content.BackgroundColor = theme.ActiveTabColor;

				primaryTabControl.AddTab(
					new ToolTab(category.Name.Localize(),
						primaryTabControl,
						content,
						theme,
						hasClose: false,
						pointSize: theme.DefaultFontSize)
					{
						Name = category.Name + " Tab",
						InactiveTabColor = Color.Transparent,
						ActiveTabColor = theme.ActiveTabColor
					});
			}

			primaryTabControl.TabBar.AddChild(new HorizontalSpacer());

			this.AddChild(primaryTabControl);

			// Restore the last selected tab
			if (int.TryParse(UserSettings.Instance.get(UserSettingsKey.SliceSettingsWidget_CurrentTab), out int tabIndex)
				&& tabIndex >= 0
				&& tabIndex < primaryTabControl.TabCount - 1)
			{
				primaryTabControl.SelectedTabIndex = tabIndex;
			}
			else
			{
				primaryTabControl.SelectedTabIndex = 0;
			}

			// Store the last selected tab on change
			primaryTabControl.ActiveTabChanged += (s, e) =>
			{
				if (settingsContext.IsPrimarySettingsView)
				{
					UserSettings.Instance.set(UserSettingsKey.SliceSettingsWidget_CurrentTab, primaryTabControl.SelectedTabIndex.ToString());
				}
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
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private int tabIndexForItem = 0;

		internal bool ShowHelpControls
		{
			get => UserSettings.Instance.get(UserSettingsKey.SliceSettingsShowHelp) == "true";
			set => UserSettings.Instance.set(UserSettingsKey.SliceSettingsShowHelp, value.ToString().ToLower());
		}

		private GuiWidget CreateSideTabsAndPages(SettingsOrganizer.Category category, bool showHelpControls)
		{
			var oemAndUserContext = new SettingsContext(
						printer,
						null,
						NamedSettingsLayers.MHBaseSettings | NamedSettingsLayers.OEMSettings | NamedSettingsLayers.User);

			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Fit,
				HAnchor = HAnchor.Stretch,
				Padding = new BorderDouble(10, 0, 12, 10),
			};

			bool isFirstSection = true;

			foreach (var group in category.Groups)
			{
				tabIndexForItem = 0;

				if (group.Name == "Connection")
				{
					column.AddChild(
						SliceSettingsWidget.CreateOemProfileInfoRow(settingsContext, isPrimarySettingsView: true));
				}

				column.AddChild(
					CreateGroupContent(group, oemAndUserContext, showHelpControls, textColor, column, expanded: isFirstSection));

				isFirstSection = false;
			}

			var scrollable = new ScrollableWidget(true);
			scrollable.AnchorAll();
			scrollable.ScrollArea.HAnchor = HAnchor.Stretch;
			scrollable.AddChild(column);

			return scrollable;
		}

		public FlowLayoutWidget CreateGroupContent(SettingsOrganizer.Group group, SettingsContext oemAndUserContext, bool showHelpControls, Color textColor, GuiWidget parent, bool expanded = true)
		{
			var groupPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Fit,
				HAnchor = HAnchor.Stretch,
			};

			string groupName = (group.Name.Contains("!hidden")) ? "" : group.Name;

			var sectionWidget = new SectionWidget(groupName, groupPanel, theme, expanded: expanded);
			theme.BoxStyleSectionWidget(sectionWidget);
			sectionWidget.Margin = new BorderDouble(bottom: 10);

			if (string.IsNullOrEmpty(groupName))
			{
				// If not title will be display, sync the left and top padding values
				parent.Padding = parent.Padding.Clone(top: parent.Padding.Left);
			}

			groupPanel.Padding = 0;

			var zebraColor = theme.MinimalShade;

			var headingColor = textColor.AdjustLightness(ActiveTheme.Instance.IsDarkTheme ? 0.5 : 2.8).ToColor();

			foreach (var subGroup in group.SubGroups)
			{
				var section = AddSettingRowsForSubgroup(subGroup, oemAndUserContext, showHelpControls);
				if (section != null)
				{
					//zebraColor = (zebraColor == Color.Transparent) ? zebraColor = theme.MinimalShade : Color.Transparent;
					zebraColor = Color.Transparent;

					var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
					{
						HAnchor = HAnchor.Stretch,
						BackgroundColor = zebraColor,
					};

					if (false && !subGroup.Name.Contains("!hidden"))
					{
						// Section heading
						column.AddChild(new TextWidget("  " + subGroup.Name.Localize(), textColor: headingColor, pointSize: theme.FontSize10)
						{
							Margin = new BorderDouble(left: 8, top: 6, bottom: 4),
						});
					}
					column.AddChild(section);

					groupPanel.AddChild(column);
				}
			}

			return sectionWidget;
		}

		private GuiWidget AddSettingRowsForSubgroup(SettingsOrganizer.SubGroup subGroup, SettingsContext oemAndUserContext, bool showHelpControls)
		{
			var topToBottomSettings = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
			};

			topToBottomSettings.AddChild(new HorizontalLine(20)
			{
			});

			foreach (SliceSettingData settingData in subGroup.Settings)
			{
				// Note: tab sections may disappear if / when they are empty, as controlled by:
				// settingShouldBeShown / addedSettingToSubGroup / needToAddSubGroup
				bool settingShouldBeShown = CheckIfShouldBeShown(settingData, oemAndUserContext);

				if (EngineMappingsMatterSlice.Instance.MapContains(settingData.SlicerConfigName)
					&& settingShouldBeShown)
				{
					topToBottomSettings.AddChild(
						CreateItemRow(settingData, ref tabIndexForItem, theme));

					topToBottomSettings.AddChild(new HorizontalLine(20)
					{
						Margin = 0
					});

					if (showHelpControls)
					{
						topToBottomSettings.AddChild(AddInHelpText(topToBottomSettings, settingData));
					}
				}
			}

			return (topToBottomSettings.Children.Count == 1) ? null : topToBottomSettings;
		}

		private bool CheckIfShouldBeShown(SliceSettingData settingData, SettingsContext settingsContext)
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

		private GuiWidget AddInHelpText(FlowLayoutWidget topToBottomSettings, SliceSettingData settingData)
		{
			double textRegionWidth = 380 * GuiWidget.DeviceScale;
			double helpPointSize = 10;

			var allText = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(0),
				Padding = new BorderDouble(5),
				BackgroundColor = textColor
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
		
		internal GuiWidget CreateItemRow(SliceSettingData settingData, ref int tabIndexForItem, ThemeConfig theme)
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
				Margin = new BorderDouble(right: 4),
				Padding = new BorderDouble(12, 0, 10, 0),
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
					var dropMenu = new DropMenuWrappedField(uiField, settingData, textColor);
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
				return new DisableablePanel(settingsRow);
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
	}
}
