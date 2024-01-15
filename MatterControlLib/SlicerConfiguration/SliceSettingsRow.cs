﻿/*
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
using Markdig.Agg;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration.MappingClasses;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SliceSettingsRow : SettingsRow, ISetableIgnoredPopupChild
	{
		private IEnumerable<SettingsValidationError> validationErrors;

		private static Dictionary<string, Func<PrinterSettings, GuiWidget>> extendedInfo = new Dictionary<string, Func<PrinterSettings, GuiWidget>>()
		{
#if DEBUG
			{ "perimeter_start_end_overlap", (settings) =>
				{

					var theme = AppContext.Theme;

					var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
					{
						Margin = new BorderDouble(top: 8),
						HAnchor = HAnchor.Stretch
					};

					var markdown = new MarkdownWidget(theme);
					markdown.HAnchor = HAnchor.Stretch;
					markdown.VAnchor = VAnchor.Fit;
					markdown.Markdown = "**Hello From Markdown**\r\n\r\n ![xxx](https://gravit.io/assets/home/oldusermessagebg.png)";

					column.AddChild(markdown);


					return column;
				}
			}
#endif
		};

		private SettingsContext settingsContext;

		private PrinterConfig printer;
		private SliceSettingData settingData;

		private GuiWidget dataArea;
		private GuiWidget unitsArea;
		private GuiWidget restoreArea;
		private GuiWidget restoreButton = null;

		private ValidationWrapper validationWrapper;

		public SliceSettingsRow(PrinterConfig printer, SettingsContext settingsContext, SliceSettingData settingData, ThemeConfig theme, bool fullRowSelect = false)
			: base(settingData.PresentationName.Localize(), settingData.HelpText.Localize(), theme, fullRowSelect: fullRowSelect)
		{
			this.printer = printer;
			this.settingData = settingData;
			this.settingsContext = settingsContext;

			using (this.LayoutLock())
			{
				this.AddChild(dataArea = new FlowLayoutWidget
				{
					VAnchor = VAnchor.Fit | VAnchor.Center,
					DebugShowBounds = debugLayout
				});

				this.AddChild(unitsArea = new GuiWidget()
				{
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Fit | VAnchor.Center,
					Width = 50 * GuiWidget.DeviceScale,
					DebugShowBounds = debugLayout
				});

				// Populate unitsArea as appropriate
				// List elements contain list values in the field which normally contains label details, skip generation of invalid labels
				if (settingData.DataEditType != SliceSettingData.DataEditTypes.LIST
					&& settingData.DataEditType != SliceSettingData.DataEditTypes.HARDWARE_PRESENT)
				{
					unitsArea.AddChild(
						new WrappedTextWidget(settingData.Units.Localize(), pointSize: theme.FontSize8, textColor: theme.TextColor)
						{
							Margin = new BorderDouble(5, 0),
						});
				}

				restoreArea = new GuiWidget()
				{
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Fit | VAnchor.Center,
					Width = 20 * GuiWidget.DeviceScale,
					DebugShowBounds = debugLayout
				};
				this.AddChild(restoreArea);

				this.Name = settingData.SlicerConfigName + " Row";

				if (settingData.ShowAsOverride
					&& settingsContext.ViewFilter != NamedSettingsLayers.OEMSettings)
				{
					restoreButton = theme.CreateSmallResetButton();
					restoreButton.HAnchor = HAnchor.Right;
					restoreButton.Margin = 0;
					restoreButton.Name = "Restore " + settingData.SlicerConfigName;
					restoreButton.ToolTipText = "Restore Default".Localize();
					restoreButton.Click += (sender, e) =>
					{
						// Revert the user override
						settingsContext.ClearValue(settingData.SlicerConfigName);
					};

					restoreArea.AddChild(restoreButton);

					restoreArea.Selectable = true;
				}
			}

			this.PerformLayout();
		}

		public void UpdateValidationState(List<ValidationError> errors)
		{
			var fieldErrors = errors.OfType<SettingsValidationError>().Where(e => e.CanonicalSettingsName == this.settingData.SlicerConfigName);
			if (fieldErrors.Any())
			{
				validationErrors = fieldErrors;
				this.ContentValid = false;
			}
			else
			{
				validationErrors = Enumerable.Empty<SettingsValidationError>();
				this.ContentValid = true;
			}
		}

		public SettingsValidationError ValidationEror { get; set; }

		public Color HighlightColor
		{
			get => overrideIndicator.BackgroundColor;
			set
			{
				if (overrideIndicator.BackgroundColor != value)
				{
					overrideIndicator.BackgroundColor = value;
				}
			}
		}

		public bool ContentValid
		{
			get => validationWrapper?.ContentValid ?? true;
			set
			{
				if (validationWrapper != null
					&& validationWrapper.ContentValid != value)
				{
					validationWrapper.ContentValid = value;

					// ShowPopever performs bounds validation and should have no effect if mouse is not in bounds
					this.ShowPopover(this);
				}
			}
		}

		public bool KeepMenuOpen { get; set; } = false;

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			OpenRightClickMenu(printer, settingData, this, this, mouseEvent);

			base.OnClick(mouseEvent);
		}

		public static void OpenRightClickMenu(PrinterConfig printer,
			SliceSettingData settingData,
			SliceSettingsRow sliceSettingsRow,
			ISetableIgnoredPopupChild ignoredPopupChild,
			MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button != MouseButtons.Right)
			{
				return;
			}

			ignoredPopupChild.KeepMenuOpen = true;

			bool SettingIsOem()
			{
				if (printer.Settings.OemLayer.TryGetValue(settingData.SlicerConfigName, out string oemValue))
				{
					return printer.Settings.GetValue(settingData.SlicerConfigName) == oemValue;
				}

				if (printer.Settings.BaseLayer.TryGetValue(settingData.SlicerConfigName, out string baseValue))
				{
					return printer.Settings.GetValue(settingData.SlicerConfigName) == baseValue;
				}

				return false;
			}

			bool SettingIsSameAsLayer(PrinterSettingsLayer layer)
			{
				if (layer != null
					&& layer.TryGetValue(settingData.SlicerConfigName, out string presetValue))
				{
					return printer.Settings.GetValue(settingData.SlicerConfigName) == presetValue;
				}

				return false;
			}

			// show a right click menu ('Set as Default' & 'Help')
			var menuTheme = ApplicationController.Instance.MenuTheme;
			var popupMenu = new PopupMenu(menuTheme);
			var settingName = settingData.SlicerConfigName;

			var enableClearUser = printer.Settings.UserLayer.ContainsKey(settingName);
			var enableClearMaterial = printer.Settings.MaterialLayer?.ContainsKey(settingName) == true;
			var enableClearQuality = printer.Settings.QualityLayer?.ContainsKey(settingName) == true;
			// put in clear layer menu items
			{
				var clearSettingMenuItem = popupMenu.CreateMenuItem("Clear Override".Localize());
				clearSettingMenuItem.Enabled = enableClearUser;
				clearSettingMenuItem.Click += (s, e) =>
				{
					new SettingsContext(printer,
						new PrinterSettingsLayer[] { printer.Settings.UserLayer },
						NamedSettingsLayers.User).ClearValue(settingName);
					sliceSettingsRow.UpdateStyle();
					printer.Settings.Save();
				};
			}

			// quality
			if (enableClearQuality)
			{
				var clearSettingMenuItem = popupMenu.CreateMenuItem("Clear Quality Setting".Localize());
				clearSettingMenuItem.Enabled = enableClearQuality;
				clearSettingMenuItem.Click += (s, e) =>
				{
					new SettingsContext(printer,
						new PrinterSettingsLayer[] { printer.Settings.QualityLayer },
						NamedSettingsLayers.Quality).ClearValue(settingName);
					sliceSettingsRow.UpdateStyle();
					printer.Settings.Save();
				};
			}

			// material
			if (enableClearMaterial)
			{
				var clearSettingMenuItem = popupMenu.CreateMenuItem("Clear Material Setting".Localize());
				clearSettingMenuItem.Enabled = enableClearMaterial;
				clearSettingMenuItem.Click += (s, e) =>
				{
					new SettingsContext(printer,
						new PrinterSettingsLayer[] { printer.Settings.MaterialLayer },
						NamedSettingsLayers.Material).ClearValue(settingName);
					sliceSettingsRow.UpdateStyle();
					printer.Settings.Save();
				};
			}

			var canSaveDefault = !SettingIsOem();
			var isPrinterSetting = PrinterSettings.Layout.AllPrinterSettings.ContainsKey(settingName);
			var canSaveQuality = !isPrinterSetting && printer.Settings.QualityLayer != null && !SettingIsSameAsLayer(printer.Settings.QualityLayer);
			var canSaveMaterial = !isPrinterSetting && printer.Settings.MaterialLayer != null && !SettingIsSameAsLayer(printer.Settings.MaterialLayer);

			if (canSaveDefault || canSaveQuality || canSaveMaterial)
			{
				popupMenu.CreateSeparator();

				popupMenu.CreateSubMenu("Save to", menuTheme, (subMenu) =>
				{
					// add menu to set default
					{
						var setAsDefaultMenuItem = subMenu.CreateMenuItem("Printer Default".Localize());
						setAsDefaultMenuItem.Enabled = canSaveDefault; // check if the settings is already the default
						setAsDefaultMenuItem.Click += (s, e) =>
						{
							printer.Settings.OemLayer[settingName] = printer.Settings.GetValue(settingName);
							sliceSettingsRow.UpdateStyle();
							printer.Settings.Save();
						};
					}

					// add menu item to set quality
					{
						var qualitySettingName = printer.Settings.QualityLayer?.Name;
						var setAsQualityMenuItem = subMenu.CreateMenuItem(!string.IsNullOrEmpty(qualitySettingName) ?
							"Quality Setting '{0}'".Localize().FormatWith(qualitySettingName) :
							"Quality Setting".Localize());
						setAsQualityMenuItem.Enabled = canSaveQuality;
						setAsQualityMenuItem.Click += (s, e) =>
						{
							printer.Settings.QualityLayer[settingName] = printer.Settings.GetValue(settingName);
							printer.Settings.UserLayer.Remove(settingName);
							sliceSettingsRow.UpdateStyle();
							printer.Settings.Save();
						};
					}

					// add menu item to set material
					{
						var materialSettingName = printer.Settings.MaterialLayer?.Name;
						var setAsMaterialMenuItem = subMenu.CreateMenuItem(!string.IsNullOrEmpty(materialSettingName) ?
							"Material Setting '{0}'".Localize().FormatWith(materialSettingName) :
							"Material Setting".Localize());
						setAsMaterialMenuItem.Enabled = canSaveMaterial;
						setAsMaterialMenuItem.Click += (s, e) =>
						{
							printer.Settings.MaterialLayer[settingName] = printer.Settings.GetValue(settingName);
							printer.Settings.UserLayer.Remove(settingName);
							sliceSettingsRow.UpdateStyle();
							printer.Settings.Save();
						};
					}
				});
			}

			popupMenu.ShowMenu(sliceSettingsRow, mouseEvent);

			popupMenu.Closed += (s, e) => ignoredPopupChild.KeepMenuOpen = false;
		}

		public UIField UIField { get; internal set; }

		protected override void ExtendPopover(ClickablePopover popover)
		{
			string mapsTo = "";

            throw new NotImplementedException();


            if (extendedInfo.TryGetValue(settingData.SlicerConfigName, out Func<PrinterSettings, GuiWidget> extender))
			{
				if (extender.Invoke(printer.Settings) is GuiWidget widget)
				{
					popover.AddChild(widget);
				}
			}

			if (validationErrors?.Any() == true)
			{
				var errorsPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					Padding = theme.DefaultContainerPadding / 2,
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit
				};
				popover.AddChild(errorsPanel);

				foreach (var item in validationErrors)
				{
					var errorPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
					{
						Margin = new BorderDouble(0, 5)
					};
					errorsPanel.AddChild(errorPanel);

					var text = item.Error;
					if (!string.IsNullOrEmpty(item.ValueDetails))
					{
						text += "\n\n" + item.ValueDetails;
					}

					errorsPanel.AddChild(
						new WrappedTextWidget(text, pointSize: theme.DefaultFontSize - 1, textColor: Color.Red)
						{
							Margin = new BorderDouble(bottom: 3)
						});

					if (item.Details is string details
						&& !string.IsNullOrWhiteSpace(details))
					{
						errorsPanel.AddChild(new WrappedTextWidget(details, pointSize: theme.DefaultFontSize - 1, textColor: Color.Red));
					}
				}
			}

#if DEBUG
			popover.AddChild(new TextWidget(settingData.SlicerConfigName + mapsTo, pointSize: theme.DefaultFontSize - 1, textColor: AppContext.Theme.TextColor)
			{
				Margin = new BorderDouble(top: 10)
			});
#endif
		}

		public void UpdateStyle()
		{
			var data = GetStyleData(printer, theme, settingsContext, settingData.SlicerConfigName, settingData.ShowAsOverride);

			if (this.HighlightColor != data.highlightColor)
			{
				this.HighlightColor = data.highlightColor;
			}

			if (restoreButton != null)
			{
				restoreButton.Visible = data.showRestoreButton;
			}
		}

		public static (Color highlightColor, bool showRestoreButton) GetStyleData(PrinterConfig printer,
			ThemeConfig theme,
			SettingsContext settingsContext,
			string key,
			bool showAsOverride)
		{
			var settings = printer.Settings;
			var highlightColor = Color.Transparent;
			var showRestoreButton = false;

			if (settingsContext.ContainsKey(key))
			{
				switch (settingsContext.ViewFilter)
				{
					case NamedSettingsLayers.All:
						if (showAsOverride)
						{
							var defaultCascade = settings.DefaultLayerCascade;
							var firstParentValue = settings.GetValueAndLayerName(key, defaultCascade.Skip(1));
							var (currentValue, layerName) = settings.GetValueAndLayerName(key, defaultCascade);

							if (settings.IsOverride(key, defaultCascade))
							{
								if (firstParentValue.currentValue == currentValue)
								{
									if (layerName.StartsWith("Material"))
									{
										highlightColor = theme.PresetColors.MaterialPreset;
									}
									else if (layerName.StartsWith("Quality"))
									{
										highlightColor = theme.PresetColors.QualityPreset;
									}
									else if (layerName.StartsWith("Scene"))
									{
										highlightColor = theme.PresetColors.ScenePreset;
									}
									else
									{
										highlightColor = Color.Transparent;
									}

									showRestoreButton = false;
								}
								else
								{
									highlightColor = theme.PresetColors.UserOverride;
									showRestoreButton = true;
								}
							}
							else
							{
								highlightColor = Color.Transparent;
								showRestoreButton = false;
							}
						}

						break;
					case NamedSettingsLayers.Material:
						highlightColor = theme.PresetColors.MaterialPreset;
						showRestoreButton = true;
						break;
					case NamedSettingsLayers.Quality:
						highlightColor = theme.PresetColors.QualityPreset;
						showRestoreButton = true;
						break;
					case NamedSettingsLayers.Scene:
						highlightColor = theme.PresetColors.ScenePreset;
						showRestoreButton = true;
						break;
				}
			}
			else if (settingsContext.IsPrimarySettingsView)
			{
				var defalutValue = settings.BaseLayer[key];
				if (settings.OemLayer?.ContainsKey(key) == true)
				{
					defalutValue = settings.OemLayer[key];
				}

				if (settings.SettingExistsInLayer(key, NamedSettingsLayers.User)
					&& settings.UserLayer[key] != defalutValue)
				{
					highlightColor = theme.PresetColors.UserOverride;
				}
				else if (settings.SettingExistsInLayer(key, NamedSettingsLayers.Scene))
				{
					highlightColor = theme.PresetColors.ScenePreset;
				}
				else if (settings.SettingExistsInLayer(key, NamedSettingsLayers.Material))
				{
					highlightColor = theme.PresetColors.MaterialPreset;
				}
				else if (settings.SettingExistsInLayer(key, NamedSettingsLayers.Quality))
				{
					highlightColor = theme.PresetColors.QualityPreset;
				}
				else
				{
					highlightColor = Color.Transparent;
				}

				showRestoreButton = false;
			}
			else
			{
				showRestoreButton = false;
				highlightColor = Color.Transparent;
			}

			return (highlightColor, showRestoreButton);
		}

		public void AddContent(GuiWidget content)
		{
			validationWrapper = new ValidationWrapper();
			dataArea.AddChild(validationWrapper);

			validationWrapper.AddChild(content);
		}

		/// <summary>
		/// Wraps UIFields and conditionally displays validation error hints when validation errors occur
		/// </summary>
		private class ValidationWrapper : GuiWidget
		{
			private bool _contentValid = true;
			private ImageBuffer exclamation;

			public ValidationWrapper()
			{
				this.VAnchor = VAnchor.Fit;
				this.HAnchor = HAnchor.Fit;
				this.Padding = new BorderDouble(left: 5);

				exclamation = StaticData.Instance.LoadIcon("exclamation.png", 4, 12);

				this.Border = new BorderDouble(bottom: 1);
			}

			public bool ContentValid
			{
				get => _contentValid;
				set
				{
					if (_contentValid != value)
					{
						_contentValid = value;
						this.Invalidate();
					}
				}
			}

			public override Color BorderColor
			{
				get => (this.ContentValid) ? base.BorderColor : Color.Red;
				set => base.BorderColor = value;
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				base.OnDraw(graphics2D);

				if (!this.ContentValid)
				{
					graphics2D.Render(exclamation, this.LocalBounds.Left, this.LocalBounds.Top - exclamation.Height);
				}
			}
		}
	}
}
