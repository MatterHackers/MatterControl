/*
Copyright (c) 2017, Kevin Pope, John Lewin
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
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class ApplicationSettingsWidget : FlowLayoutWidget, IIgnoredPopupChild
	{
		public static Action OpenPrintNotification = null;

		private string cannotRestartWhilePrintIsActiveMessage = "Oops! You cannot restart while a print is active.".Localize();
		private string cannotRestartWhileActive = "Unable to restart".Localize();

		private TextImageButtonFactory buttonFactory;

		private RGBA_Bytes menuTextColor = RGBA_Bytes.Black;

		public ApplicationSettingsWidget(TextImageButtonFactory buttonFactory)
			: base(FlowDirection.TopToBottom)
		{
			this.buttonFactory = buttonFactory;
			this.HAnchor = HAnchor.ParentLeftRight;
			this.VAnchor = VAnchor.FitToChildren;
			this.Padding = 15;

			if (UserSettings.Instance.IsTouchScreen)
			{
				this.AddSettingsRow(this.GetUpdateControl());
			}

			// Camera Monitoring
			bool hasCamera = true || ApplicationSettings.Instance.get(ApplicationSettingsKey.HardwareHasCamera) == "true";

			var previewButton = buttonFactory.Generate("Preview".Localize().ToUpper());
			previewButton.Click += (s, e) =>
			{
				MatterControlApplication.Instance.OpenCameraPreview();
			};

			this.AddSettingsRow(
				new SettingsItem(
					"Camera Monitoring".Localize(),
					buttonFactory,
					new SettingsItem.ToggleSwitchConfig()
					{
						Checked = ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.publish_bed_image),
						ToggleAction = (itemChecked) =>
						{
							ActiveSliceSettings.Instance.SetValue(SettingsKey.publish_bed_image, itemChecked ? "1" : "0");
						}
					},
					previewButton,
					StaticData.Instance.LoadIcon("camera-24x24.png", 24, 24))
			);

			// Print Notifications
			var configureNotificationsButton = buttonFactory.Generate("Configure".Localize().ToUpper());
			configureNotificationsButton.Name = "Configure Notification Settings Button";
			configureNotificationsButton.Margin = new BorderDouble(left: 6);
			configureNotificationsButton.VAnchor = VAnchor.ParentCenter;
			configureNotificationsButton.Click += (s, e) =>
			{
				if (OpenPrintNotification != null)
				{
					UiThread.RunOnIdle(OpenPrintNotification);
				}
			};

			this.AddSettingsRow(
				new SettingsItem(
					"Notifications".Localize(),
					buttonFactory,
					new SettingsItem.ToggleSwitchConfig()
					{
						Checked = UserSettings.Instance.get("PrintNotificationsEnabled") == "true",
						ToggleAction = (itemChecked) =>
						{
							UserSettings.Instance.set("PrintNotificationsEnabled", itemChecked ? "true" : "false");
						}
					},
					configureNotificationsButton,
					StaticData.Instance.LoadIcon("notify-24x24.png")));

			// LanguageControl
			var languageSelector = new LanguageSelector()
			{
				TextColor = menuTextColor
			};
			languageSelector.SelectionChanged += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					string languageCode = languageSelector.SelectedValue;
					if (languageCode != UserSettings.Instance.get("Language"))
					{
						UserSettings.Instance.set("Language", languageCode);

						if (languageCode == "L10N")
						{
							GenerateLocalizationValidationFile();
						}

						LocalizedString.ResetTranslationMap();
						ApplicationController.Instance.ReloadAll();
					}
				});
			};

			this.AddSettingsRow(
				new SettingsItem(
					"Language".Localize(),
					buttonFactory,
					languageSelector));

#if !__ANDROID__
			{
				// ThumbnailRendering
				var thumbnailsModeDropList = new DropDownList("", maxHeight: 200)
				{
					TextColor = menuTextColor,
				};
				thumbnailsModeDropList.AddItem("Flat".Localize(), "orthographic");
				thumbnailsModeDropList.AddItem("3D".Localize(), "raytraced");

				var acceptableUpdateFeedTypeValues = new List<string>() { "orthographic", "raytraced" };
				string currentThumbnailRenderingMode = UserSettings.Instance.get(UserSettingsKey.ThumbnailRenderingMode);

				if (acceptableUpdateFeedTypeValues.IndexOf(currentThumbnailRenderingMode) == -1)
				{
					if (!UserSettings.Instance.IsTouchScreen)
					{
						UserSettings.Instance.set(UserSettingsKey.ThumbnailRenderingMode, "orthographic");
					}
					else
					{
						UserSettings.Instance.set(UserSettingsKey.ThumbnailRenderingMode, "raytraced");
					}
				}

				thumbnailsModeDropList.SelectedValue = UserSettings.Instance.get(UserSettingsKey.ThumbnailRenderingMode);
				thumbnailsModeDropList.SelectionChanged += (s, e) =>
				{
					string thumbnailRenderingMode = thumbnailsModeDropList.SelectedValue;
					if (thumbnailRenderingMode != UserSettings.Instance.get(UserSettingsKey.ThumbnailRenderingMode))
					{
						UserSettings.Instance.set(UserSettingsKey.ThumbnailRenderingMode, thumbnailRenderingMode);

						// Ask if the user would like to rebuild all their thumbnails
						Action<bool> removeThumbnails = (bool shouldRebuildThumbnails) =>
						{
							if (shouldRebuildThumbnails)
							{
								string directoryToRemove = PartThumbnailWidget.ThumbnailsPath;
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
							}

							ApplicationController.Instance.ReloadAll();
						};

						UiThread.RunOnIdle(() =>
						{
							StyledMessageBox.ShowMessageBox(removeThumbnails, rebuildThumbnailsMessage, rebuildThumbnailsTitle, StyledMessageBox.MessageType.YES_NO, "Rebuild".Localize());
						});
					}
				};

				this.AddSettingsRow(
					new SettingsItem(
						"Thumbnails".Localize(),
						buttonFactory,
						thumbnailsModeDropList));

				// Touch Screen Mode
				this.AddSettingsRow(
					new SettingsItem(
						"Touch Screen Mode".Localize(),
						buttonFactory,
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
						}));

				// TextSize
				this.AddSettingsRow(this.GetTextSizeControl());
			}
			#endif
			
			if (UserSettings.Instance.IsTouchScreen)
			{
				this.AddSettingsRow(this.GetModeControl());
			}

			this.AddSettingsRow(this.GetThemeControl());
		}

		private void AddSettingsRow(GuiWidget widget)
		{
			this.AddChild(widget);
			this.AddChild(new HorizontalLine(50));
		}

		private FlowLayoutWidget GetThemeControl()
		{
			FlowLayoutWidget buttonRow = new FlowLayoutWidget(FlowDirection.TopToBottom);
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Margin = new BorderDouble(0, 6);

			TextWidget settingLabel = new TextWidget("Theme".Localize());
			settingLabel.AutoExpandBoundsToText = true;
			settingLabel.TextColor = menuTextColor;
			settingLabel.HAnchor = HAnchor.ParentLeft;

			FlowLayoutWidget colorSelectorContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			colorSelectorContainer.HAnchor = HAnchor.ParentLeftRight;
			colorSelectorContainer.Margin = new BorderDouble(top: 4);

			GuiWidget currentColorThemeBorder = new GuiWidget();

			currentColorThemeBorder.VAnchor = VAnchor.ParentBottomTop;
			currentColorThemeBorder.Padding = new BorderDouble(5);
			currentColorThemeBorder.Width = 80;
			currentColorThemeBorder.BackgroundColor = RGBA_Bytes.LightGray;

			GuiWidget currentColorTheme = new GuiWidget();
			currentColorTheme.HAnchor = HAnchor.ParentLeftRight;
			currentColorTheme.VAnchor = VAnchor.ParentBottomTop;
			currentColorTheme.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

			currentColorThemeBorder.AddChild(currentColorTheme);

			ThemeColorSelectorWidget themeSelector = new ThemeColorSelectorWidget(colorToChangeTo: currentColorTheme);
			themeSelector.Margin = new BorderDouble(right: 5);

			colorSelectorContainer.AddChild(themeSelector);
			colorSelectorContainer.AddChild(currentColorThemeBorder);

			buttonRow.AddChild(settingLabel);
			buttonRow.AddChild(colorSelectorContainer);

			return buttonRow;
		}

		private FlowLayoutWidget GetTextSizeControl()
		{
			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Margin = new BorderDouble(top: 4);

			double currentTexSize = 1.0;
			if (!double.TryParse(UserSettings.Instance.get(UserSettingsKey.ApplicationTextSize), out currentTexSize))
			{
				currentTexSize = 1.0;
			}

			TextWidget settingsLabel = new TextWidget("Text Size".Localize() + $" : {currentTexSize:0.0}")
			{
				AutoExpandBoundsToText = true
			};
			settingsLabel.AutoExpandBoundsToText = true;
			settingsLabel.TextColor = menuTextColor;
			settingsLabel.VAnchor = VAnchor.ParentTop;

			double sliderThumbWidth = 10 * GuiWidget.DeviceScale;
			double sliderWidth = 100 * GuiWidget.DeviceScale;
			var textSizeSlider = new SolidSlider(new Vector2(), sliderThumbWidth, .7, 1.4)
			{
				Name = "Text Size Slider",
				Margin = new BorderDouble(5, 0),
				Value = currentTexSize,
				HAnchor = HAnchor.ParentLeftRight,
				TotalWidthInPixels = sliderWidth,
			};

			FlowLayoutWidget optionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			optionsContainer.Margin = new BorderDouble(bottom: 6);
			optionsContainer.AddChild(textSizeSlider);
			optionsContainer.Width = 200 * GuiWidget.DeviceScale;

			string currentTextModeType = UserSettings.Instance.get(UserSettingsKey.ApplicationTextSize);

			Button textSizeControlApplyButton = buttonFactory.Generate("Apply".Localize());
			textSizeControlApplyButton.VAnchor = VAnchor.ParentCenter;
			textSizeControlApplyButton.Visible = false;
			textSizeControlApplyButton.Margin = new BorderDouble(right: 6);
			textSizeControlApplyButton.Click += (s, e) =>
			{
				GuiWidget.DeviceScale = textSizeSlider.Value;
				ApplicationController.Instance.ReloadAll();
			};

			textSizeSlider.ValueChanged += (s, e) =>
			{
				double textSizeNew = ((SolidSlider)s).Value;
				UserSettings.Instance.set(UserSettingsKey.ApplicationTextSize, textSizeNew.ToString("0.0"));
				settingsLabel.Text = "Text Size".Localize() + $" : {textSizeNew:0.0}";
				textSizeControlApplyButton.Visible = textSizeNew != currentTexSize;
			};

			string currentTexSizeString = UserSettings.Instance.get(UserSettingsKey.ApplicationTextSize);

			buttonRow.AddChild(settingsLabel);
			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(textSizeControlApplyButton);
			buttonRow.AddChild(optionsContainer);
			return buttonRow;
		}

		private FlowLayoutWidget GetModeControl()
		{
			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Margin = new BorderDouble(top: 4);

			TextWidget settingsLabel = new TextWidget("Interface Mode".Localize());
			settingsLabel.AutoExpandBoundsToText = true;
			settingsLabel.TextColor = menuTextColor;
			settingsLabel.VAnchor = VAnchor.ParentTop;

			FlowLayoutWidget optionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			optionsContainer.Margin = new BorderDouble(bottom: 6);

			DropDownList interfaceModeDropList = new DropDownList("Standard", maxHeight: 200);
			interfaceModeDropList.HAnchor = HAnchor.ParentLeftRight;

			optionsContainer.AddChild(interfaceModeDropList);
			optionsContainer.Width = 200;

			MenuItem standardModeDropDownItem = interfaceModeDropList.AddItem("Standard".Localize(), "True");
			MenuItem advancedModeDropDownItem = interfaceModeDropList.AddItem("Advanced".Localize(), "False");

			interfaceModeDropList.SelectedValue = UserSettings.Instance.Fields.IsSimpleMode.ToString();
			interfaceModeDropList.SelectionChanged += (sender, e) =>
			{
				string isSimpleMode = ((DropDownList)sender).SelectedValue;
				if (isSimpleMode == "True")
				{
					UserSettings.Instance.Fields.IsSimpleMode = true;
				}
				else
				{
					UserSettings.Instance.Fields.IsSimpleMode = false;
				}
				ApplicationController.Instance.ReloadAll();
			};

			buttonRow.AddChild(settingsLabel);
			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(optionsContainer);
			return buttonRow;
		}

		private FlowLayoutWidget GetUpdateControl()
		{
			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Margin = new BorderDouble(top: 4);

			Button configureUpdateFeedButton = buttonFactory.Generate("Configure".Localize().ToUpper());
			configureUpdateFeedButton.Margin = new BorderDouble(left: 6);
			configureUpdateFeedButton.VAnchor = VAnchor.ParentCenter;

			TextWidget settingsLabel = new TextWidget("Update Notification Feed".Localize());
			settingsLabel.AutoExpandBoundsToText = true;
			settingsLabel.TextColor = menuTextColor;
			settingsLabel.VAnchor = VAnchor.ParentTop;

			FlowLayoutWidget optionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			optionsContainer.Margin = new BorderDouble(bottom: 6);

			var releaseOptionsDropList = new DropDownList("Development", maxHeight: 200);
			releaseOptionsDropList.HAnchor = HAnchor.ParentLeftRight;

			optionsContainer.AddChild(releaseOptionsDropList);
			optionsContainer.Width = 200;

			MenuItem releaseOptionsDropDownItem = releaseOptionsDropList.AddItem("Stable".Localize(), "release");
			releaseOptionsDropDownItem.Selected += (s, e) => UpdateControlData.Instance.CheckForUpdateUserRequested();

			MenuItem preReleaseDropDownItem = releaseOptionsDropList.AddItem("Beta".Localize(), "pre-release");
			preReleaseDropDownItem.Selected += (s, e) => UpdateControlData.Instance.CheckForUpdateUserRequested();

			MenuItem developmentDropDownItem = releaseOptionsDropList.AddItem("Alpha".Localize(), "development");
			developmentDropDownItem.Selected += (s, e) => UpdateControlData.Instance.CheckForUpdateUserRequested();

			List<string> acceptableUpdateFeedTypeValues = new List<string>() { "release", "pre-release", "development" };
			string currentUpdateFeedType = UserSettings.Instance.get(UserSettingsKey.UpdateFeedType);

			if (acceptableUpdateFeedTypeValues.IndexOf(currentUpdateFeedType) == -1)
			{
				UserSettings.Instance.set(UserSettingsKey.UpdateFeedType, "release");
			}

			releaseOptionsDropList.SelectedValue = UserSettings.Instance.get(UserSettingsKey.UpdateFeedType);
			releaseOptionsDropList.SelectionChanged += (sender, e) =>
			{
				string releaseCode = releaseOptionsDropList.SelectedValue;
				if (releaseCode != UserSettings.Instance.get(UserSettingsKey.UpdateFeedType))
				{
					UserSettings.Instance.set(UserSettingsKey.UpdateFeedType, releaseCode);
				}
			};

			buttonRow.AddChild(settingsLabel);
			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(optionsContainer);
			return buttonRow;
		}
		
		private string rebuildThumbnailsMessage = "You are switching to a different thumbnail rendering mode. If you want, your current thumbnails can be removed and recreated in the new style. You can switch back and forth at any time. There will be some processing overhead while the new thumbnails are created.\n\nDo you want to rebuild your existing thumbnails now?".Localize();
		private string rebuildThumbnailsTitle = "Rebuild Thumbnails Now".Localize();

		private void RestartApplication()
		{
			UiThread.RunOnIdle(() =>
			{
				// Iterate to the top SystemWindow
				GuiWidget parent = this;
				while (parent.Parent != null)
				{
					parent = parent.Parent;
				}

				// MatterControlApplication is the root child on the SystemWindow object
				MatterControlApplication app = parent.Children[0] as MatterControlApplication;
#if !__ANDROID__
				app.RestartOnClose = true;
				app.Close();
#else
                // Re-initialize and load
                LocalizedString.ResetTranslationMap();
                ApplicationController.Instance.MainView = new TouchscreenView();
                app.RemoveAllChildren();
                app.AnchorAll();
#endif
			});
		}

		[Conditional("DEBUG")]
		private void GenerateLocalizationValidationFile()
		{
			char currentChar = 'A';

			string outputPath = StaticData.Instance.MapPath(Path.Combine("Translations", "L10N", "Translation.txt"));

			// Ensure the output directory exists
			Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

			using (var outstream = new StreamWriter(outputPath))
			{
				foreach (var line in File.ReadAllLines(StaticData.Instance.MapPath(Path.Combine("Translations", "Master.txt"))))
				{
					if (line.StartsWith("Translated:"))
					{
						var pos = line.IndexOf(':');
						var segments = new string[]
						{
							line.Substring(0, pos),
							line.Substring(pos + 1),
						};

						outstream.WriteLine("{0}:{1}", segments[0], new string(segments[1].ToCharArray().Select(c => c == ' ' ? ' ' : currentChar).ToArray()));

						if (currentChar++ == 'Z')
						{
							currentChar = 'A';
						}
					}
					else
					{
						outstream.WriteLine(line);
					}
				}
			}
		}
	}
}