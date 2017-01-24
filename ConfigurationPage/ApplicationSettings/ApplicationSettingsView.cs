/*
Copyright (c) 2014, Kevin Pope
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
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintHistory;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class ApplicationSettingsWidget : SettingsViewBase
	{
		private Button languageRestartButton;
		private Button configureUpdateFeedButton;
		public DropDownList releaseOptionsDropList;
		private string cannotRestartWhilePrintIsActiveMessage;
		private string cannotRestartWhileActive;

		public ApplicationSettingsWidget()
			: base("Application".Localize())
		{
			cannotRestartWhilePrintIsActiveMessage = "Oops! You cannot restart while a print is active.".Localize();
			cannotRestartWhileActive = "Unable to restart".Localize();
			if (UserSettings.Instance.IsTouchScreen)
			{
				mainContainer.AddChild(new HorizontalLine(separatorLineColor));
			}

			if (UserSettings.Instance.IsTouchScreen)
			{
				mainContainer.AddChild(GetUpdateControl());
				mainContainer.AddChild(new HorizontalLine(separatorLineColor));
			}
			
			mainContainer.AddChild(new HorizontalLine(separatorLineColor));
			mainContainer.AddChild(GetLanguageControl());
			mainContainer.AddChild(new HorizontalLine(separatorLineColor));
			GuiWidget sliceEngineControl = GetSliceEngineControl();
			if (ActiveSliceSettings.Instance != null)
			{
				mainContainer.AddChild(sliceEngineControl);
				mainContainer.AddChild(new HorizontalLine(separatorLineColor));
			}


			#if !__ANDROID__
			{
				mainContainer.AddChild(GetThumbnailRenderingControl());
				mainContainer.AddChild(new HorizontalLine(separatorLineColor));

				mainContainer.AddChild(GetDisplayControl());
				mainContainer.AddChild(new HorizontalLine(separatorLineColor));
			}
			#endif
			
			if (UserSettings.Instance.IsTouchScreen)
			{
				mainContainer.AddChild(GetModeControl());
				mainContainer.AddChild(new HorizontalLine(separatorLineColor));
			}

			mainContainer.AddChild(GetClearHistoryControl());
			mainContainer.AddChild(new HorizontalLine(separatorLineColor));

			mainContainer.AddChild(GetThemeControl());

			AddChild(mainContainer);

			AddHandlers();
		}

		private FlowLayoutWidget GetClearHistoryControl()
		{
			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Margin = new BorderDouble(3, 4);

			TextWidget clearHistoryLabel = new TextWidget("Clear Print History".Localize());
			clearHistoryLabel.AutoExpandBoundsToText = true;
			clearHistoryLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			clearHistoryLabel.VAnchor = VAnchor.ParentCenter;

			Button clearHistoryButton = textImageButtonFactory.Generate("Remove All".Localize().ToUpper());
			clearHistoryButton.Click += clearHistoryButton_Click;

			//buttonRow.AddChild(eePromIcon);
			buttonRow.AddChild(clearHistoryLabel);
			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(clearHistoryButton);

			return buttonRow;
		}

		private void clearHistoryButton_Click(object sender, EventArgs e)
		{
			PrintHistoryData.Instance.ClearHistory();
		}

		private void SetDisplayAttributes()
		{
			//this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			this.Margin = new BorderDouble(2, 4, 2, 0);
			this.textImageButtonFactory.normalFillColor = RGBA_Bytes.White;
			this.textImageButtonFactory.disabledFillColor = RGBA_Bytes.White;

			this.textImageButtonFactory.FixedHeight = TallButtonHeight;
			this.textImageButtonFactory.fontSize = 11;

			this.textImageButtonFactory.disabledTextColor = RGBA_Bytes.DarkGray;
			this.textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.textImageButtonFactory.normalTextColor = RGBA_Bytes.Black;
			this.textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

			this.linkButtonFactory.fontSize = 11;
		}

		private FlowLayoutWidget GetThemeControl()
		{
			FlowLayoutWidget buttonRow = new FlowLayoutWidget(Agg.UI.FlowDirection.TopToBottom);
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Margin = new BorderDouble(0, 6);

			TextWidget settingLabel = new TextWidget("Theme".Localize());
			settingLabel.AutoExpandBoundsToText = true;
			settingLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			settingLabel.HAnchor = Agg.UI.HAnchor.ParentLeft;

			FlowLayoutWidget colorSelectorContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			colorSelectorContainer.HAnchor = HAnchor.ParentLeftRight;
			colorSelectorContainer.Margin = new BorderDouble(top: 4);

			GuiWidget currentColorThemeBorder = new GuiWidget();

			currentColorThemeBorder.VAnchor = VAnchor.ParentBottomTop;
			currentColorThemeBorder.Padding = new BorderDouble(5);
			currentColorThemeBorder.Width = 80;
			currentColorThemeBorder.BackgroundColor = RGBA_Bytes.White;

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

		private FlowLayoutWidget GetDisplayControl()
		{
			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Margin = new BorderDouble(top: 4);

			TextWidget settingsLabel = new TextWidget("Display Mode".Localize());
			settingsLabel.AutoExpandBoundsToText = true;
			settingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			settingsLabel.VAnchor = VAnchor.ParentTop;

			Button displayControlRestartButton = textImageButtonFactory.Generate("Restart".Localize());
			displayControlRestartButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
			displayControlRestartButton.Visible = false;
			displayControlRestartButton.Margin = new BorderDouble(right: 6);
			displayControlRestartButton.Click += (sender, e) =>
			{
				if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting)
				{
					StyledMessageBox.ShowMessageBox(null, cannotRestartWhilePrintIsActiveMessage, cannotRestartWhileActive);
				}
				else
				{
					RestartApplication();
				}
			};

			FlowLayoutWidget optionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			optionsContainer.Margin = new BorderDouble(bottom: 6);

			DropDownList interfaceOptionsDropList = new DropDownList("Development", maxHeight: 200);
			interfaceOptionsDropList.HAnchor = HAnchor.ParentLeftRight;

			optionsContainer.AddChild(interfaceOptionsDropList);
			optionsContainer.Width = 200;

			interfaceOptionsDropList.AddItem("Normal".Localize(), "responsive");
			interfaceOptionsDropList.AddItem("Touchscreen".Localize(), "touchscreen");

			List<string> acceptableUpdateFeedTypeValues = new List<string>() { "responsive", "touchscreen" };
			string currentDisplayModeType = UserSettings.Instance.get(UserSettingsKey.ApplicationDisplayMode);

			if (acceptableUpdateFeedTypeValues.IndexOf(currentDisplayModeType) == -1)
			{
				UserSettings.Instance.set(UserSettingsKey.ApplicationDisplayMode, "responsive");
			}

			interfaceOptionsDropList.SelectedValue = UserSettings.Instance.get(UserSettingsKey.ApplicationDisplayMode);
			interfaceOptionsDropList.SelectionChanged += (sender, e) =>
			{
				string displayMode = ((DropDownList)sender).SelectedValue;
				if (displayMode != UserSettings.Instance.get(UserSettingsKey.ApplicationDisplayMode))
				{
					UserSettings.Instance.set(UserSettingsKey.ApplicationDisplayMode, displayMode);
					displayControlRestartButton.Visible = true;
				}
			};

			buttonRow.AddChild(settingsLabel);
			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(displayControlRestartButton);
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
			settingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
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
			interfaceModeDropList.SelectionChanged += new EventHandler(InterfaceModeDropList_SelectionChanged);

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

			configureUpdateFeedButton = textImageButtonFactory.Generate("Configure".Localize().ToUpper());
			configureUpdateFeedButton.Margin = new BorderDouble(left: 6);
			configureUpdateFeedButton.VAnchor = VAnchor.ParentCenter;

			TextWidget settingsLabel = new TextWidget("Update Notification Feed".Localize());
			settingsLabel.AutoExpandBoundsToText = true;
			settingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			settingsLabel.VAnchor = VAnchor.ParentTop;

			FlowLayoutWidget optionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			optionsContainer.Margin = new BorderDouble(bottom: 6);

			releaseOptionsDropList = new DropDownList("Development", maxHeight: 200);
			releaseOptionsDropList.HAnchor = HAnchor.ParentLeftRight;

			optionsContainer.AddChild(releaseOptionsDropList);
			optionsContainer.Width = 200;

			MenuItem releaseOptionsDropDownItem = releaseOptionsDropList.AddItem("Stable".Localize(), "release");
			releaseOptionsDropDownItem.Selected += new EventHandler(FixTabDot);

			MenuItem preReleaseDropDownItem = releaseOptionsDropList.AddItem("Beta".Localize(), "pre-release");
			preReleaseDropDownItem.Selected += new EventHandler(FixTabDot);

			MenuItem developmentDropDownItem = releaseOptionsDropList.AddItem("Alpha".Localize(), "development");
			developmentDropDownItem.Selected += new EventHandler(FixTabDot);

			List<string> acceptableUpdateFeedTypeValues = new List<string>() { "release", "pre-release", "development" };
			string currentUpdateFeedType = UserSettings.Instance.get(UserSettingsKey.UpdateFeedType);

			if (acceptableUpdateFeedTypeValues.IndexOf(currentUpdateFeedType) == -1)
			{
				UserSettings.Instance.set(UserSettingsKey.UpdateFeedType, "release");
			}

			releaseOptionsDropList.SelectedValue = UserSettings.Instance.get(UserSettingsKey.UpdateFeedType);
			releaseOptionsDropList.SelectionChanged += new EventHandler(ReleaseOptionsDropList_SelectionChanged);

			buttonRow.AddChild(settingsLabel);
			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(optionsContainer);
			return buttonRow;
		}
		
		private FlowLayoutWidget GetLanguageControl()
		{
			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Margin = new BorderDouble(top: 4);

			TextWidget settingsLabel = new TextWidget("Language".Localize());
			settingsLabel.AutoExpandBoundsToText = true;
			settingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			settingsLabel.VAnchor = VAnchor.ParentTop;

			FlowLayoutWidget controlsContainer = new FlowLayoutWidget();
			controlsContainer.HAnchor = HAnchor.ParentLeftRight;

			FlowLayoutWidget optionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			optionsContainer.Margin = new BorderDouble(bottom: 6);

			LanguageSelector languageSelector = new LanguageSelector();
			languageSelector.SelectionChanged += new EventHandler(LanguageDropList_SelectionChanged);
			languageSelector.HAnchor = HAnchor.ParentLeftRight;

			optionsContainer.AddChild(languageSelector);
			optionsContainer.Width = 200;

			languageRestartButton = textImageButtonFactory.Generate("Restart".Localize());
			languageRestartButton.VAnchor = Agg.UI.VAnchor.ParentCenter;
			languageRestartButton.Visible = false;
			languageRestartButton.Margin = new BorderDouble(right: 6);

			languageRestartButton.Click += (sender, e) =>
			{
				if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting)
				{
					StyledMessageBox.ShowMessageBox(null, cannotRestartWhilePrintIsActiveMessage, cannotRestartWhileActive);
				}
				else
				{
					RestartApplication();
				}
			};

			buttonRow.AddChild(settingsLabel);
			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(languageRestartButton);
			buttonRow.AddChild(optionsContainer);
			return buttonRow;
		}

		private FlowLayoutWidget GetSliceEngineControl()
		{
			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Margin = new BorderDouble(top: 4);

			TextWidget settingsLabel = new TextWidget("Slice Engine".Localize());
			settingsLabel.AutoExpandBoundsToText = true;
			settingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			settingsLabel.VAnchor = VAnchor.ParentTop;

			FlowLayoutWidget controlsContainer = new FlowLayoutWidget();
			controlsContainer.HAnchor = HAnchor.ParentLeftRight;

			FlowLayoutWidget optionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			optionsContainer.Margin = new BorderDouble(bottom: 6);

			var settings = ActiveSliceSettings.Instance;

			// Reset active slicer to MatterSlice when multi-extruder is detected and MatterSlice is not already set
			if (settings?.GetValue<int>(SettingsKey.extruder_count) > 1 
				&& settings.Helpers.ActiveSliceEngineType() != SlicingEngineTypes.MatterSlice)
			{
				settings.Helpers.ActiveSliceEngineType(SlicingEngineTypes.MatterSlice);
				ApplicationController.Instance.ReloadAll();
			} 

			optionsContainer.AddChild(new SliceEngineSelector("Slice Engine".Localize()));
			optionsContainer.Width = 200;

			buttonRow.AddChild(settingsLabel);
			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(optionsContainer);
			return buttonRow;
		}

		private FlowLayoutWidget GetThumbnailRenderingControl()
		{
			FlowLayoutWidget buttonRow = new FlowLayoutWidget();
			buttonRow.HAnchor = HAnchor.ParentLeftRight;
			buttonRow.Margin = new BorderDouble(top: 4);

			TextWidget settingsLabel = new TextWidget("Thumbnail Rendering".Localize());
			settingsLabel.AutoExpandBoundsToText = true;
			settingsLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			settingsLabel.VAnchor = VAnchor.ParentTop;

			FlowLayoutWidget optionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			optionsContainer.Margin = new BorderDouble(bottom: 6);

			DropDownList interfaceOptionsDropList = new DropDownList("Development", maxHeight: 200);
			interfaceOptionsDropList.HAnchor = HAnchor.ParentLeftRight;

			optionsContainer.AddChild(interfaceOptionsDropList);
			optionsContainer.Width = 200;

			interfaceOptionsDropList.AddItem("Flat".Localize(), "orthographic");
			interfaceOptionsDropList.AddItem("3D".Localize(), "raytraced");

			List<string> acceptableUpdateFeedTypeValues = new List<string>() { "orthographic", "raytraced" };
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

			interfaceOptionsDropList.SelectedValue = UserSettings.Instance.get(UserSettingsKey.ThumbnailRenderingMode);
			interfaceOptionsDropList.SelectionChanged += (sender, e) =>
			{
				string thumbnailRenderingMode = ((DropDownList)sender).SelectedValue;
				if (thumbnailRenderingMode != UserSettings.Instance.get(UserSettingsKey.ThumbnailRenderingMode))
				{
					UserSettings.Instance.set(UserSettingsKey.ThumbnailRenderingMode, thumbnailRenderingMode);

					// Ask if the user would like to rebuild all their thumbnails
					Action<bool> removeThumbnails = (bool shouldRebuildThumbnails) =>
					{
						if (shouldRebuildThumbnails)
						{
							string directoryToRemove = PartThumbnailWidget.ThumbnailPath();
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

			buttonRow.AddChild(settingsLabel);
			buttonRow.AddChild(new HorizontalSpacer());
			buttonRow.AddChild(optionsContainer);
			return buttonRow;
		}

		private static string rebuildThumbnailsMessage = "You are switching to a different thumbnail rendering mode. If you want, your current thumbnails can be removed and recreated in the new style. You can switch back and forth at any time. There will be some processing overhead while the new thumbnails are created.\n\nDo you want to rebuild your existing thumbnails now?".Localize();
		private static string rebuildThumbnailsTitle = "Rebuild Thumbnails Now".Localize();

		private void AddHandlers()
		{
		}

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

		private void FixTabDot(object sender, EventArgs e)
		{
			UpdateControlData.Instance.CheckForUpdateUserRequested();
		}

		private void InterfaceModeDropList_SelectionChanged(object sender, EventArgs e)
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
		}

		private void ReleaseOptionsDropList_SelectionChanged(object sender, EventArgs e)
		{
			string releaseCode = ((DropDownList)sender).SelectedValue;
			if (releaseCode != UserSettings.Instance.get(UserSettingsKey.UpdateFeedType))
			{
				UserSettings.Instance.set(UserSettingsKey.UpdateFeedType, releaseCode);
			}
		}

		private void LanguageDropList_SelectionChanged(object sender, EventArgs e)
		{
			string languageCode = ((Agg.UI.DropDownList)sender).SelectedValue;
			if (languageCode != UserSettings.Instance.get("Language"))
			{
				UserSettings.Instance.set("Language", languageCode);
				languageRestartButton.Visible = true;

				if(languageCode == "L10N")
				{
					GenerateLocalizationValidationFile();
				}
			}
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