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
	public class SliceSettingsRow : SettingsRow
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

		private static Popover activePopover = null;

		private SettingsContext settingsContext;

		private PrinterConfig printer;
		private SliceSettingData settingData;

		private GuiWidget dataArea;
		private GuiWidget unitsArea;
		private GuiWidget restoreArea;
		private GuiWidget restoreButton = null;

		private ValidationWrapper validationWrapper;

		public SliceSettingsRow(PrinterConfig printer, SettingsContext settingsContext, SliceSettingData settingData, ThemeConfig theme, bool fullRowSelect = false)
			: base (settingData.PresentationName.Localize(), settingData.HelpText.Localize(), theme, fullRowSelect: fullRowSelect)
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

				if (settingData.ShowAsOverride)
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

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Right)
			{
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

				if (SettingIsOem())
				{
					return;
				}

				// show a right click menu ('Set as Default' & 'Help')
				var popupMenu = new PopupMenu(ApplicationController.Instance.MenuTheme);

				var setAsDefaultMenuItem = popupMenu.CreateMenuItem("Set as Default".Localize());
				setAsDefaultMenuItem.Focus();
				setAsDefaultMenuItem.Enabled = !SettingIsOem(); // check if the settings is already the default
				setAsDefaultMenuItem.Click += (s, e) =>
				{
					// we may want to ask if we should save this
					// figure out what the current setting is and save it to the oem layer, than update the display
					var settingName = settingData.SlicerConfigName;
					printer.Settings.OemLayer[settingName] = printer.Settings.GetValue(settingName);
					UpdateStyle();
					printer.Settings.Save();
				};

				var sourceEvent = mouseEvent.Position;
				var systemWindow = this.Parents<SystemWindow>().FirstOrDefault();
				this.Parents<SystemWindow>().FirstOrDefault().ToolTipManager.Clear();
				systemWindow.ShowPopup(
					new MatePoint(this)
					{
						Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
						AltMate = new MateOptions(MateEdge.Left, MateEdge.Top)
					},
					new MatePoint(popupMenu)
					{
						Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
						AltMate = new MateOptions(MateEdge.Right, MateEdge.Top)
					},
					altBounds: new RectangleDouble(sourceEvent.X + 1, sourceEvent.Y + 1, sourceEvent.X + 1, sourceEvent.Y + 1));
			}

			base.OnClick(mouseEvent);
		}

		public UIField UIField { get; internal set; }

		protected override void ExtendPopover(SliceSettingsPopover popover)
		{
			string mapsTo = "";

			if (printer.Settings.Slicer.Exports.TryGetValue(settingData.SlicerConfigName, out ExportField exportField))
			{
				mapsTo = " -> " + exportField.OuputName;

				var settings = printer.Settings;

				if (settingData.Converter is ValueConverter mappedSetting
					&& mappedSetting is AsPercentOfReferenceOrDirect percentReference)
				{
					string settingValue = settings.GetValue(settingData.SlicerConfigName);
					string referencedSetting = settings.GetValue(percentReference.ReferencedSetting);

					double.TryParse(referencedSetting, out double referencedValue);

					var theme = AppContext.Theme;

					var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
					{
						Margin = new BorderDouble(top: 8),
						HAnchor = HAnchor.Stretch
					};

					if (settingValue.Contains("%")
						&& PrinterSettings.SettingsData.TryGetValue(percentReference.ReferencedSetting, out SliceSettingData referencedSettingData))
					{
						column.AddChild(
							new TextWidget(
								string.Format("{0}: {1} ({2})", "Percentage of".Localize(), referencedSettingData.PresentationName, referencedSetting),
								textColor: theme.TextColor,
								pointSize: theme.DefaultFontSize - 1));

						settingValue = settingValue.Replace("%", "").Trim();

						if (int.TryParse(settingValue, out int percent))
						{
							double ratio = (double)percent / 100;

							string line = string.Format(
										"{0}% of {1} is {2:0.##}",
										percent,
										referencedValue,
										settings.ResolveValue(settingData.SlicerConfigName));

							column.AddChild(new TextWidget(line, textColor: theme.TextColor, pointSize: theme.DefaultFontSize - 1));

							popover.AddChild(column);
						}
					}
				}
			}

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

					errorsPanel.AddChild(
						new WrappedTextWidget(item.Error, pointSize: theme.DefaultFontSize - 1, textColor: Color.Red)
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
			if (settingsContext.ContainsKey(settingData.SlicerConfigName))
			{
				switch (settingsContext.ViewFilter)
				{
					case NamedSettingsLayers.All:
						if (settingData.ShowAsOverride)
						{
							var defaultCascade = printer.Settings.GetDefaultLayerCascade();
							var firstParentValue = printer.Settings.GetValueAndLayerName(settingData.SlicerConfigName, defaultCascade.Skip(1));
							var (currentValue, layerName) = printer.Settings.GetValueAndLayerName(settingData.SlicerConfigName, defaultCascade);

							if (printer.Settings.IsOverride(settingData.SlicerConfigName))
							{
								if (firstParentValue.Item1 == currentValue)
								{
									if (layerName.StartsWith("Material"))
									{
										this.HighlightColor = theme.PresetColors.MaterialPreset;
									}
									else if (layerName.StartsWith("Quality"))
									{
										this.HighlightColor = theme.PresetColors.QualityPreset;
									}
									else
									{
										this.HighlightColor = Color.Transparent;
									}

									if (restoreButton != null)
									{
										restoreButton.Visible = false;
									}
								}
								else
								{
									this.HighlightColor = theme.PresetColors.UserOverride;
									if (restoreButton != null) restoreButton.Visible = true;
								}
							}
							else
							{
								this.HighlightColor = Color.Transparent;
								if (restoreButton != null) restoreButton.Visible = false;
							}
						}
						break;
					case NamedSettingsLayers.Material:
						this.HighlightColor = theme.PresetColors.MaterialPreset;
						if (restoreButton != null) restoreButton.Visible = true;
						break;
					case NamedSettingsLayers.Quality:
						this.HighlightColor = theme.PresetColors.QualityPreset;
						if (restoreButton != null) restoreButton.Visible = true;
						break;
				}
			}
			else if (settingsContext.IsPrimarySettingsView)
			{
				bool isOverride = printer.Settings.IsOverride(settingData.SlicerConfigName);

				if (isOverride && printer.Settings.SettingExistsInLayer(settingData.SlicerConfigName, NamedSettingsLayers.Material))
				{
					this.HighlightColor =theme.PresetColors.MaterialPreset;
				}
				else if (isOverride && printer.Settings.SettingExistsInLayer(settingData.SlicerConfigName, NamedSettingsLayers.Quality))
				{
					this.HighlightColor = theme.PresetColors.QualityPreset;
				}
				else
				{
					this.HighlightColor = Color.Transparent;
				}

				if (restoreButton != null) restoreButton.Visible = false;
			}
			else
			{
				if (restoreButton != null) restoreButton.Visible = false;
				this.HighlightColor = Color.Transparent;
			}
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

				exclamation = AggContext.StaticData.LoadIcon("exclamation.png");

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
