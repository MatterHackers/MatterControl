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
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PrintPopupMenu : PopupMenuButton
	{
		private PrinterConfig printer;
		private Dictionary<string, UIField> allUiFields = new Dictionary<string, UIField>();
		private SettingsContext settingsContext;

		public PrintPopupMenu(PrinterConfig printer, ThemeConfig theme)
			: base(theme)
		{
			this.printer = printer;
			this.DrawArrow = true;
			this.BackgroundColor = theme.ToolbarButtonBackground;
			this.HoverColor = theme.ToolbarButtonHover;
			this.MouseDownColor = theme.ToolbarButtonDown;
			this.Name = "PrintPopupMenu";
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;

			settingsContext = new SettingsContext(printer, null, NamedSettingsLayers.All);

			this.PopupHAnchor = HAnchor.Fit;
			this.PopupVAnchor = VAnchor.Fit;
			this.MakeScrollable = false;

			this.DynamicPopupContent = () =>
			{
				var menuTheme = ApplicationController.Instance.MenuTheme;

				int tabIndex = 0;

				allUiFields.Clear();

				var printPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					Padding = theme.DefaultContainerPadding,
					BackgroundColor = menuTheme.BackgroundColor
				};

				printPanel.AddChild(new TextWidget("Options".Localize(), textColor: menuTheme.TextColor, pointSize: theme.DefaultFontSize)
				{
					HAnchor = HAnchor.Left
				});

				var optionsPanel = new IgnoredFlowLayout()
				{
					Name = "PrintPopupMenu Panel",
					HAnchor = HAnchor.Fit | HAnchor.Left,
					VAnchor = VAnchor.Fit,
					Padding = 5,
					MinimumSize = new Vector2(400, 65),
				};
				printPanel.AddChild(optionsPanel);

				foreach (var key in new[] { SettingsKey.layer_height, SettingsKey.fill_density, SettingsKey.create_raft })
				{
					var settingsData = PrinterSettings.SettingsData[key];
					var row = SliceSettingsTabView.CreateItemRow(settingsData, settingsContext, printer, menuTheme, ref tabIndex, allUiFields);

					if (row is SliceSettingsRow settingsRow)
					{
						settingsRow.ArrowDirection = ArrowDirection.Left;
					}

					optionsPanel.AddChild(row);
				}

				var subPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					Margin = new BorderDouble(2, 0)
				};

				bool anySettingOverridden = false;
				anySettingOverridden |= printer.Settings.GetValue<bool>(SettingsKey.spiral_vase);
				anySettingOverridden |= !string.IsNullOrWhiteSpace(printer.Settings.GetValue(SettingsKey.layer_to_pause));

				var sectionWidget = new SectionWidget("Advanced".Localize(), subPanel, menuTheme, expanded: anySettingOverridden)
				{
					Name = "Advanced Section",
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit,
					Margin = 0
				};
				printPanel.AddChild(sectionWidget);

				foreach (var key in new[] { SettingsKey.spiral_vase, SettingsKey.layer_to_pause })
				{
					var advancedRow = SliceSettingsTabView.CreateItemRow(
						PrinterSettings.SettingsData[key],
						settingsContext,
						printer,
						menuTheme,
						ref tabIndex,
						allUiFields);

					if (advancedRow is SliceSettingsRow settingsRow)
					{
						settingsRow.ArrowDirection = ArrowDirection.Left;
					}

					subPanel.AddChild(advancedRow);
				}

				menuTheme.ApplyBoxStyle(sectionWidget);

				sectionWidget.Margin = new BorderDouble(0, 10);
				sectionWidget.ContentPanel.Children<SettingsRow>().First().Border = new BorderDouble(0, 1);
				sectionWidget.ContentPanel.Children<SettingsRow>().Last().Border = 0;

				var printerReadyToTakeCommands = printer.Connection.CommunicationState == PrinterCommunication.CommunicationStates.FinishedPrint
					|| printer.Connection.CommunicationState == PrinterCommunication.CommunicationStates.Connected;

				// add the start print button
				var setupRow = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.Stretch
				};

				// Perform validation before popup
				var errors = printer.Validate();

				// Enable print option when no validation Errors exists
				var printEnabled = !errors.Any(err => err.ErrorLevel == ValidationErrorLevel.Error);

				var startPrintButton = new TextButton("Start Print".Localize(), menuTheme)
				{
					Name = "Start Print Button",
					Enabled = printEnabled
				};

				startPrintButton.Click += (s, e) =>
				{
					// Exit if the bed is not GCode and the bed has no printable items
					if (!printer.Bed.EditContext.IsGGCodeSource
						&& !printer.PrintableItems(printer.Bed.Scene).Any())
					{
						return;
					}

					UiThread.RunOnIdle(async () =>
					{
						// Save any pending changes before starting print operation
						await ApplicationController.Instance.Tasks.Execute("Saving Changes".Localize(), printer, printer.Bed.SaveChanges);

						await ApplicationController.Instance.PrintPart(
							printer.Bed.EditContext,
							printer,
							null,
							CancellationToken.None);
					});

					this.CloseMenu();
				};

				var hasErrors = errors.Any(e => e.ErrorLevel == ValidationErrorLevel.Error);
				var hasWarnings = errors.Any(e => e.ErrorLevel == ValidationErrorLevel.Warning
					&& UserSettings.Instance.get($"Ignore_{e.ID}") != "true");

				var hasErrorsOrWarnings = hasErrors || hasWarnings;
				if (hasErrorsOrWarnings)
				{
					string label = hasErrors ? "Action Required".Localize() : "Action Recommended".Localize();

					setupRow.AddChild(new TextWidget(label, textColor: hasErrors ? Color.Red : theme.PrimaryAccentColor, pointSize: theme.DefaultFontSize)
					{
						VAnchor = VAnchor.Bottom,
						AutoExpandBoundsToText = true,
					});
				}

				setupRow.AddChild(new HorizontalSpacer());
				setupRow.AddChild(startPrintButton);

				printPanel.AddChild(setupRow);

				if (printEnabled)
				{
					theme.ApplyPrimaryActionStyle(startPrintButton);
				}
				else
				{
					startPrintButton.BackgroundColor = theme.MinimalShade;
				}

				if (hasErrorsOrWarnings)
				{
					var errorsPanel = new ValidationErrorsPanel(errors, menuTheme);

					// Conditional layout for right or bottom errors panel alignment
					var layoutStyle = FlowDirection.TopToBottom;

					if (layoutStyle == FlowDirection.LeftToRight)
					{
						errorsPanel.HAnchor = HAnchor.Absolute;
						errorsPanel.VAnchor = VAnchor.Fit | VAnchor.Top;
						errorsPanel.BackgroundColor = theme.ResolveColor(menuTheme.BackgroundColor, theme.PrimaryAccentColor.WithAlpha(30));
						errorsPanel.Width = 350;

						errorsPanel.Load += (s, e) =>
						{
							errorsPanel.Parent.BackgroundColor = Color.Transparent;
						};
					}
					else
					{
						errorsPanel.HAnchor = HAnchor.Stretch;
						errorsPanel.VAnchor = VAnchor.Fit;
						errorsPanel.Margin = 3;
					}

					// Instead of the typical case where the print panel is returned, wrap and append validation errors panel
					var errorsContainer = new FlowLayoutWidget(layoutStyle)
					{
						HAnchor = HAnchor.Fit,
						VAnchor = VAnchor.Fit,
						BackgroundColor = layoutStyle == FlowDirection.TopToBottom ? printPanel.BackgroundColor : Color.Transparent
					};

					// Clear bottom padding
					printPanel.Padding = printPanel.Padding.Clone(bottom: 2);

					errorsContainer.AddChild(printPanel);
					errorsContainer.AddChild(errorsPanel);

					return errorsContainer;
				}

				return printPanel;
			};

			this.AddChild(new TextButton("Print".Localize(), theme)
			{
				Selectable = false,
				Padding = theme.TextButtonPadding.Clone(right: 5)
			});

			// Register listeners
			printer.Settings.SettingChanged += Printer_SettingChanged;
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Settings.SettingChanged -= Printer_SettingChanged;

			base.OnClosed(e);
		}

		private void Printer_SettingChanged(object s, StringEventArgs stringEvent)
		{
			if (stringEvent != null)
			{
				string settingsKey = stringEvent.Data;
				if (allUiFields.TryGetValue(settingsKey, out UIField uifield))
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
		}

		private class IgnoredFlowLayout : FlowLayoutWidget, IIgnoredPopupChild
		{
			public IgnoredFlowLayout()
				: base(FlowDirection.TopToBottom)
			{
			}

			public bool KeepMenuOpen => false;
		}
	}
}