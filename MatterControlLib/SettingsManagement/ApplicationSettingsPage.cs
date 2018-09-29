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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public partial class ApplicationSettingsPage : DialogPage
	{
		public ApplicationSettingsPage()
		{
			this.AlwaysOnTopOfMain = true;
			this.WindowTitle = this.HeaderText = "MatterControl " + "Settings".Localize();
			this.WindowSize = new Vector2(500 * GuiWidget.DeviceScale, 500 * GuiWidget.DeviceScale);

			contentRow.Padding = contentRow.Padding.Clone(top: 0);

			var generalPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};
			contentRow.AddChild(generalPanel);

			var configureIcon = AggContext.StaticData.LoadIcon("fa-cog_16.png", 16, 16, theme.InvertIcons);

#if __ANDROID__
			// Camera Monitoring
			bool hasCamera = true || ApplicationSettings.Instance.get(ApplicationSettingsKey.HardwareHasCamera) == "true";

			var previewButton = new IconButton(configureIcon, theme)
			{
				ToolTipText = "Configure Camera View".Localize()
			};
			previewButton.Click += (s, e) =>
			{
				AppContext.Platform.OpenCameraPreview();
			};

			this.AddSettingsRow(
				new SettingsItem(
					"Camera Monitoring".Localize(),
					theme,
					new SettingsItem.ToggleSwitchConfig()
					{
						Checked = ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.publish_bed_image),
						ToggleAction = (itemChecked) =>
						{
							ActiveSliceSettings.Instance.SetValue(SettingsKey.publish_bed_image, itemChecked ? "1" : "0");
						}
					},
					previewButton,
					AggContext.StaticData.LoadIcon("camera-24x24.png", 24, 24))
			);
#endif
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
				if (ApplicationSettingsWidget.OpenPrintNotification != null)
				{
					UiThread.RunOnIdle(() =>
					{
						ApplicationSettingsWidget.OpenPrintNotification(this.DialogWindow);
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
					AggContext.StaticData.LoadIcon("notify-24x24.png")),
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
						ApplicationController.Instance.ReloadAll();
					}
				});
			};

			this.AddSettingsRow(new SettingsItem("Language".Localize(), languageSelector, theme), generalPanel);

#if !__ANDROID__
			// ThumbnailRendering
			var thumbnailsModeDropList = new DropDownList("", theme.Colors.PrimaryTextColor, maxHeight: 200, pointSize: theme.DefaultFontSize)
			{
				BorderColor = theme.GetBorderColor(75)
			};
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
#endif

			// TextSize
			if (!double.TryParse(UserSettings.Instance.get(UserSettingsKey.ApplicationTextSize), out double currentTextSize))
			{
				currentTextSize = 1.0;
			}

			double sliderThumbWidth = 10 * GuiWidget.DeviceScale;
			double sliderWidth = 100 * GuiWidget.DeviceScale;
			var textSizeSlider = new SolidSlider(new Vector2(), sliderThumbWidth, .7, 1.4)
			{
				Name = "Text Size Slider",
				Margin = new BorderDouble(5, 0),
				Value = currentTextSize,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Center,
				TotalWidthInPixels = sliderWidth,
			};

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
			textSizeApplyButton.Click += (s, e) =>
			{
				GuiWidget.DeviceScale = textSizeSlider.Value;
				ApplicationController.Instance.ReloadAll();
			};
			optionalContainer.AddChild(textSizeApplyButton);

			textSizeSlider.ValueChanged += (s, e) =>
			{
				double textSizeNew = textSizeSlider.Value;
				UserSettings.Instance.set(UserSettingsKey.ApplicationTextSize, textSizeNew.ToString("0.0"));
				sectionLabel.Text = "Text Size".Localize() + $" : {textSizeNew:0.0}";
				textSizeApplyButton.Visible = textSizeNew != currentTextSize;
			};

			var section = new SettingsItem(
					"Text Size".Localize() + $" : {currentTextSize:0.0}",
					textSizeSlider,
					theme,
					optionalContainer);

			sectionLabel = section.Children<TextWidget>().FirstOrDefault();

			this.AddSettingsRow(section, generalPanel);

			var advancedPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(2, 0)
			};

			var sectionWidget = new SectionWidget("Advanced", advancedPanel, theme, serializationKey: "ApplicationSettings-Advanced", expanded: false)
			{
				Name = "Advanced Section",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = 0
			};
			contentRow.AddChild(sectionWidget);

			theme.ApplyBoxStyle(sectionWidget);

			sectionWidget.Margin = new BorderDouble(0, 10);

			// Touch Screen Mode
			this.AddSettingsRow(
				new SettingsItem(
					"Touch Screen Mode".Localize(),
					theme,
					new SettingsItem.ToggleSwitchConfig()
					{
						Checked = UserSettings.Instance.get(UserSettingsKey.ApplicationDisplayMode) == "touchscreen",
						ToggleAction = (itemChecked) =>
						{
							string displayMode = itemChecked ? "touchscreen" : "responsive";
							if (displayMode != UserSettings.Instance.get(UserSettingsKey.ApplicationDisplayMode))
							{
								UserSettings.Instance.set(UserSettingsKey.ApplicationDisplayMode, displayMode);
								ApplicationController.Instance.ReloadAll();
							}
						}
					}),
				advancedPanel);

			var openCacheButton = new IconButton(AggContext.StaticData.LoadIcon("fa-link_16.png", 16, 16, theme.InvertIcons), theme)
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

			var removeHoveredIcon = AggContext.StaticData.LoadIcon("remove.png", 16, 16, theme.InvertIcons);
			var removeNormalIcon = new ImageBuffer(removeHoveredIcon);

			//removeNormalIcon.NewGraphics2D().Render(removeHoveredIcon.ToGrayscale(), 0, 0);
			ApplicationController.Instance.MakeGrayscale(removeNormalIcon);

			var clearCacheButton = new IconButton(removeNormalIcon, removeHoveredIcon, theme)
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

			advancedPanel.Children<SettingsItem>().First().Border = new BorderDouble(0, 1);
		}

		private void AddSettingsRow(GuiWidget widget, GuiWidget container)
		{
			container.AddChild(widget);
			widget.Padding = widget.Padding.Clone(right: 10);
		}

		private class IgnoredFlowLayout : FlowLayoutWidget, IIgnoredPopupChild
		{
			public IgnoredFlowLayout()
				: base(FlowDirection.TopToBottom)
			{
			}

			public bool KeepMenuOpen()
			{
				return false;
			}
		}
	}
}
