/*
Copyright (c) 2018, John Lewin
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public partial class ApplicationSettingsPage : DialogPage
	{
		public ApplicationSettingsPage()
			: base("Close".Localize())
		{
			this.AlwaysOnTopOfMain = true;
			this.WindowTitle = this.HeaderText = "MatterControl " + "Settings".Localize();
			this.WindowSize = new Vector2(700 * GuiWidget.DeviceScale, 600 * GuiWidget.DeviceScale);

			contentRow.Padding = theme.DefaultContainerPadding;
			contentRow.Padding = 0;
			contentRow.BackgroundColor = Color.Transparent;
			GuiWidget settingsColumn;

			{
				var settingsAreaScrollBox = new ScrollableWidget(true);
				settingsAreaScrollBox.ScrollArea.HAnchor |= HAnchor.Stretch;
				settingsAreaScrollBox.AnchorAll();
				settingsAreaScrollBox.BackgroundColor = theme.MinimalShade;
				contentRow.AddChild(settingsAreaScrollBox);

				settingsColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					HAnchor = HAnchor.MaxFitOrStretch
				};

				settingsAreaScrollBox.AddChild(settingsColumn);
			}

			AddGeneralPannel(settingsColumn);

			AddUsserOptionsPannel(settingsColumn);

			AddAdvancedPannel(settingsColumn);

			// Enforce consistent SectionWidget spacing and last child borders
			foreach (var section in settingsColumn.Children<SectionWidget>())
			{
				section.Margin = new BorderDouble(0, 10, 0, 0);

				if (section.ContentPanel.Children.LastOrDefault() is SettingsItem lastRow)
				{
					// If we're in a contentPanel that has SettingsItems...

					// Clear the last items bottom border
					lastRow.Border = lastRow.Border.Clone(bottom: 0);

					// Set a common margin on the parent container
					section.ContentPanel.Margin = new BorderDouble(2, 0);
				}
			}
		}

		private void AddGeneralPannel(GuiWidget settingsColumn)
		{
			var generalPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};

			var configureIcon = StaticData.Instance.LoadIcon("fa-cog_16.png", 16, 16).SetToColor(theme.TextColor);

			var generalSection = new SectionWidget("General".Localize(), generalPanel, theme, expandingContent: false)
			{
				Name = "General Section",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};
			settingsColumn.AddChild(generalSection);

			theme.ApplyBoxStyle(generalSection);

			// Print Notifications
			var configureNotificationsButton = new IconButton(configureIcon, theme)
			{
				Name = "Configure Notification Settings Button",
				ToolTipText = "Configure Notifications".Localize(),
				Margin = new BorderDouble(left: 6),
				VAnchor = VAnchor.Center
			};
			configureNotificationsButton.Click += (s, e) =>
			{
				if (ApplicationController.ChangeToPrintNotification != null)
				{
					UiThread.RunOnIdle(() =>
					{
						ApplicationController.ChangeToPrintNotification(this.DialogWindow);
					});
				}
			};

			this.AddSettingsRow(
				new SettingsItem(
					"Notifications".Localize(),
					theme,
					new SettingsItem.ToggleSwitchConfig()
					{
						Checked = UserSettings.Instance.get(UserSettingsKey.PrintNotificationsEnabled) == "true",
						ToggleAction = (itemChecked) =>
						{
							UserSettings.Instance.set(UserSettingsKey.PrintNotificationsEnabled, itemChecked ? "true" : "false");
						}
					},
					configureNotificationsButton,
					StaticData.Instance.LoadIcon("notify-24x24.png", 16, 16).SetToColor(theme.TextColor)),
				generalPanel);

			// LanguageControl
			var languageSelector = new LanguageSelector(theme);
			languageSelector.SelectionChanged += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					string languageCode = languageSelector.SelectedValue;
					if (languageCode != UserSettings.Instance.get(UserSettingsKey.Language))
					{
						UserSettings.Instance.set(UserSettingsKey.Language, languageCode);

						if (languageCode == "L10N")
						{
#if DEBUG
							AppContext.Platform.GenerateLocalizationValidationFile();
#endif
						}

						ApplicationController.Instance.ResetTranslationMap();
						ApplicationController.Instance.ReloadAll().ConfigureAwait(false);
					}
				});
			};

			this.AddSettingsRow(new SettingsItem("Language".Localize(), languageSelector, theme), generalPanel);

			// ThumbnailRendering
			var thumbnailsModeDropList = new MHDropDownList("", theme, maxHeight: 200 * GuiWidget.DeviceScale);
			thumbnailsModeDropList.AddItem("Flat".Localize(), "orthographic");
			thumbnailsModeDropList.AddItem("3D".Localize(), "raytraced");

			thumbnailsModeDropList.SelectedValue = UserSettings.Instance.ThumbnailRenderingMode;
			thumbnailsModeDropList.SelectionChanged += (s, e) =>
			{
				string thumbnailRenderingMode = thumbnailsModeDropList.SelectedValue;
				if (thumbnailRenderingMode != UserSettings.Instance.ThumbnailRenderingMode)
				{
					UserSettings.Instance.ThumbnailRenderingMode = thumbnailRenderingMode;

					UiThread.RunOnIdle(() =>
					{
						// Ask if the user they would like to rebuild their thumbnails
						StyledMessageBox.ShowMessageBox(
							(bool rebuildThumbnails) =>
							{
								if (rebuildThumbnails)
								{
									string[] thumbnails = new string[]
									{
										ApplicationController.CacheablePath(
											Path.Combine("Thumbnails", "Content"), ""),
										ApplicationController.CacheablePath(
											Path.Combine("Thumbnails", "Library"), "")
									};
									foreach (var directoryToRemove in thumbnails)
									{
										try
										{
											if (Directory.Exists(directoryToRemove))
											{
												Directory.Delete(directoryToRemove, true);
											}
										}
										catch (Exception)
										{
											GuiWidget.BreakInDebugger();
										}

										Directory.CreateDirectory(directoryToRemove);
									}

									ApplicationController.Instance.Library.NotifyContainerChanged();
								}
							},
							"You are switching to a different thumbnail rendering mode. If you want, your current thumbnails can be removed and recreated in the new style. You can switch back and forth at any time. There will be some processing overhead while the new thumbnails are created.\n\nDo you want to rebuild your existing thumbnails now?".Localize(),
							"Rebuild Thumbnails Now".Localize(),
							StyledMessageBox.MessageType.YES_NO,
							"Rebuild".Localize());
					});
				}
			};

			this.AddSettingsRow(
				new SettingsItem(
					"Thumbnails".Localize(),
					thumbnailsModeDropList,
					theme),
				generalPanel);

			// TextSize
			if (!double.TryParse(UserSettings.Instance.get(UserSettingsKey.ApplicationTextSize), out double currentTextSize))
			{
				currentTextSize = 1.0;
			}

			double sliderThumbWidth = 10 * GuiWidget.DeviceScale;
			double sliderWidth = 100 * GuiWidget.DeviceScale;
			var textSizeSlider = new SolidSlider(default(Vector2), sliderThumbWidth, theme, .7, 2.5)
			{
				Name = "Text Size Slider",
				Margin = new BorderDouble(5, 0),
				Value = currentTextSize,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Center,
				TotalWidthInPixels = sliderWidth,
			};
			theme.ApplySliderStyle(textSizeSlider);

			var optionalContainer = new FlowLayoutWidget()
			{
				VAnchor = VAnchor.Center | VAnchor.Fit,
				HAnchor = HAnchor.Fit
			};

			TextWidget sectionLabel = null;

			var textSizeApplyButton = new TextButton("Apply".Localize(), theme)
			{
				VAnchor = VAnchor.Center,
				BackgroundColor = theme.SlightShade,
				Visible = false,
				Margin = new BorderDouble(right: 6)
			};
			textSizeApplyButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				GuiWidget.DeviceScale = textSizeSlider.Value;
				ApplicationController.Instance.ReloadAll().ConfigureAwait(false);
			});
			optionalContainer.AddChild(textSizeApplyButton);

			textSizeSlider.ValueChanged += (s, e) =>
			{
				double textSizeNew = textSizeSlider.Value;
				UserSettings.Instance.set(UserSettingsKey.ApplicationTextSize, textSizeNew.ToString("0.0"));
				sectionLabel.Text = "Text Size".Localize() + $" : {textSizeNew:0.0}";
				textSizeApplyButton.Visible = textSizeNew != currentTextSize;
			};

			var textSizeRow = new SettingsItem(
					"Text Size".Localize() + $" : {currentTextSize:0.0}",
					textSizeSlider,
					theme,
					optionalContainer);

			sectionLabel = textSizeRow.Children<TextWidget>().FirstOrDefault();

			this.AddSettingsRow(textSizeRow, generalPanel);

			var themeSection = CreateThemePanel(theme);
			settingsColumn.AddChild(themeSection);
			theme.ApplyBoxStyle(themeSection);
		}

		private void AddAdvancedPannel(GuiWidget settingsColumn)
		{
			var advancedPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);

			var advancedSection = new SectionWidget("Advanced".Localize(), advancedPanel, theme, serializationKey: "ApplicationSettings-Advanced", expanded: false)
			{
				Name = "Advanced Section",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = 0
			};
			settingsColumn.AddChild(advancedSection);

			theme.ApplyBoxStyle(advancedSection);

			// Touch Screen Mode
			this.AddSettingsRow(
				new SettingsItem(
					"Touch Screen Mode".Localize(),
					theme,
					new SettingsItem.ToggleSwitchConfig()
					{
						Checked = ApplicationSettings.Instance.get(ApplicationSettingsKey.ApplicationDisplayMode) == "touchscreen",
						ToggleAction = (itemChecked) =>
						{
							string displayMode = itemChecked ? "touchscreen" : "responsive";
							if (displayMode != ApplicationSettings.Instance.get(ApplicationSettingsKey.ApplicationDisplayMode))
							{
								ApplicationSettings.Instance.set(ApplicationSettingsKey.ApplicationDisplayMode, displayMode);
								UiThread.RunOnIdle(() => ApplicationController.Instance.ReloadAll().ConfigureAwait(false));
							}
						}
					}),
				advancedPanel);

			AddUserBoolToggle(advancedPanel,
				"Enable Socketeer Client".Localize(),
				UserSettingsKey.ApplicationUseSocketeer,
				true,
				false);

			AddUserBoolToggle(advancedPanel,
				"Utilize High Res Monitors".Localize(),
				UserSettingsKey.ApplicationUseHeigResDisplays,
				true,
				false);

			var openCacheButton = new IconButton(StaticData.Instance.LoadIcon("fa-link_16.png", 16, 16).SetToColor(theme.TextColor), theme)
			{
				ToolTipText = "Open Folder".Localize(),
			};
			openCacheButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				Process.Start(ApplicationDataStorage.ApplicationUserDataPath);
			});

			this.AddSettingsRow(
				new SettingsItem(
					"Application Storage".Localize(),
					openCacheButton,
					theme),
				advancedPanel);

			var clearCacheButton = new HoverIconButton(StaticData.Instance.LoadIcon("remove.png", 16, 16).SetToColor(theme.TextColor), theme)
			{
				ToolTipText = "Clear Cache".Localize(),
			};
			clearCacheButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				CacheDirectory.DeleteCacheData();
			});

			this.AddSettingsRow(
				new SettingsItem(
					"Application Cache".Localize(),
					clearCacheButton,
					theme),
				advancedPanel);

#if DEBUG
			var configureIcon = StaticData.Instance.LoadIcon("fa-cog_16.png", 16, 16).SetToColor(theme.TextColor);

			var configurePluginsButton = new IconButton(configureIcon, theme)
			{
				ToolTipText = "Configure Plugins".Localize(),
				Margin = 0
			};
			configurePluginsButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					DialogWindow.Show<PluginsPage>();
				});
			};

			this.AddSettingsRow(
				new SettingsItem(
					"Plugins".Localize(),
					configurePluginsButton,
					theme),
				advancedPanel);
#endif

			var gitHubPat = UserSettings.Instance.get("GitHubPat");
			if (gitHubPat == null)
			{
				gitHubPat = "";
			}
			var accessToken = new ThemedTextEditWidget(gitHubPat, theme, pixelWidth: 350, messageWhenEmptyAndNotSelected: "Enter Person Access Token".Localize())
			{
				HAnchor = HAnchor.Absolute,
				Margin = new BorderDouble(5),
				Name = "GitHubPat Edit Field"
			};
			accessToken.ActualTextEditWidget.EnterPressed += (s, e) =>
			{
				UserSettings.Instance.set("GitHubPat", accessToken.ActualTextEditWidget.Text);
			};
			accessToken.Closed += (s, e) =>
			{
				UserSettings.Instance.set("GitHubPat", accessToken.ActualTextEditWidget.Text);
			};
			this.AddSettingsRow(
				new SettingsItem(
					"GitHub Personal Access Token".Localize(),
					accessToken,
					theme)
				{
					ToolTipText = "This is used to increase the number of downloads allowed when browsing GitHub repositories".Localize(),
				},
				advancedPanel);

			advancedPanel.Children<SettingsItem>().First().Border = new BorderDouble(0, 1);
		}

		private void AddUsserOptionsPannel(GuiWidget settingsColumn)
		{
			var optionsPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);

			var optionsSection = new SectionWidget("Options".Localize(), optionsPanel, theme, serializationKey: "ApplicationSettings-Options", expanded: false)
			{
				Name = "Options Section",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = 0
			};
			settingsColumn.AddChild(optionsSection);

			theme.ApplyBoxStyle(optionsSection);

			AddUserBoolToggle(optionsPanel,
				"Show Ratings Dialog After Print".Localize(),
				UserSettingsKey.CollectPrintHistoryData,
				false,
				false);

			AddUserBoolToggle(optionsPanel,
				"Show Welcome Message".Localize(),
				UserSettingsKey.ShownWelcomeMessage,
				false,
				false);

			optionsPanel.Children<SettingsItem>().First().Border = new BorderDouble(0, 1);
		}

		private void AddUserBoolToggle(FlowLayoutWidget panelToAddTo, string title, string boolKey, bool requiresRestart, bool reloadAll)
		{
			this.AddSettingsRow(
				new SettingsItem(
					title,
					theme,
					new SettingsItem.ToggleSwitchConfig()
					{
						Checked = UserSettings.Instance.get(boolKey) != "false",
						ToggleAction = (itemChecked) =>
						{
							string boolValue = itemChecked ? "true" : "false";
							if (boolValue != UserSettings.Instance.get(boolKey))
							{
								UserSettings.Instance.set(boolKey, boolValue);
								if (requiresRestart)
								{
									StyledMessageBox.ShowMessageBox(
										"To apply settings changes you need to restart MatterControl.".Localize(),
										"Restart Required".Localize());
								}

								if (reloadAll)
								{
									UiThread.RunOnIdle(() => ApplicationController.Instance.ReloadAll().ConfigureAwait(false));
								}
							}
						}
					}),
				panelToAddTo);
		}

		private void AddApplicationBoolToggle(FlowLayoutWidget advancedPanel, string title, string boolKey, bool requiresRestart, bool reloadAll)
		{
			this.AddSettingsRow(
				new SettingsItem(
					title,
					theme,
					new SettingsItem.ToggleSwitchConfig()
					{
						Checked = ApplicationSettings.Instance.get(boolKey) == "true",
						ToggleAction = (itemChecked) =>
						{
							string boolValue = itemChecked ? "true" : "false";
							if (boolValue != UserSettings.Instance.get(boolKey))
							{
								ApplicationSettings.Instance.set(boolKey, boolValue);
								if (requiresRestart)
								{
									StyledMessageBox.ShowMessageBox(
										"To finish changing your monitor settings you need to restart MatterControl. If after changing your fonts are too small you can adjust Text Size.".Localize(),
										"Restart Required".Localize());
								}

								if (reloadAll)
								{
									UiThread.RunOnIdle(() => ApplicationController.Instance.ReloadAll().ConfigureAwait(false));
								}
							}
						}
					}),
				advancedPanel);
		}

		public static SectionWidget CreateThemePanel(ThemeConfig theme)
		{
			var accentButtons = new ThemeColorPanel.AccentColorsWidget(AppContext.ThemeSet, 16)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Center | VAnchor.Fit,
				Margin = new BorderDouble(right: theme.DefaultContainerPadding)
			};

			var themeColorPanel = new ThemeColorPanel(theme, accentButtons)
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(10, 10, 10, 2)
			};

			accentButtons.ThemeColorPanel = themeColorPanel;

			var themeSection = new SectionWidget("Theme".Localize(), themeColorPanel, theme, accentButtons, expanded: true, expandingContent: false)
			{
				Name = "Theme Section",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = 0
			};

			themeSection.SetNonExpandableIcon(StaticData.Instance.LoadIcon("theme.png", 16, 16));

			return themeSection;
		}

		private void AddSettingsRow(GuiWidget widget, GuiWidget container)
		{
			container.AddChild(widget);
			widget.Padding = widget.Padding.Clone(right: 10);
		}
	}
}
