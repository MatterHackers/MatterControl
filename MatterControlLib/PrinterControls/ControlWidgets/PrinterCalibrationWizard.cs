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
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class PrinterCalibrationWizard : IStagedSetupWizard
	{
		private PrinterConfig printer;
		private RoundedToggleSwitch printLevelingSwitch;

		public PrinterCalibrationWizard(PrinterConfig printer, ThemeConfig theme)
		{
			var stages = new List<ISetupWizard>()
			{
				new ZCalibrationWizard(printer),
				new PrintLevelingWizard(printer),
				new LoadFilamentWizard(printer, extruderIndex: 0, showAlreadyLoadedButton: true),
				new LoadFilamentWizard(printer, extruderIndex: 1, showAlreadyLoadedButton: true),
				new XyCalibrationWizard(printer, 1)
			};

			this.Stages = stages;
			this.printer = printer;

			this.HomePageGenerator = () =>
			{
				var homePage = new WizardSummaryPage()
				{
					HeaderText = "Printer Setup & Calibration".Localize()
				};

				var contentRow = homePage.ContentRow;

				if (!ReturnedToHomePage)
				{
					if (printer.Connection.IsConnected)
					{
						contentRow.AddChild(
							new WrappedTextWidget(
								@"Select the calibration task to continue".Replace("\r\n", "\n"),
								pointSize: theme.DefaultFontSize,
								textColor: theme.TextColor));
					}
					else
					{
						contentRow.AddChild(
							new WrappedTextWidget(
								@"Connect the printer to complete the calibration tasks.".Replace("\r\n", "\n"),
								pointSize: theme.DefaultFontSize,
								textColor: theme.TextColor));
					}
				}

				contentRow.BackgroundColor = Color.Transparent;

				foreach (var stage in this.Stages.Where(s => s.Visible))
				{
					GuiWidget rightWidget = null;
					var widget = new GuiWidget();

					if (stage is ZCalibrationWizard probeWizard)
					{
						var column = CreateColumn(theme);
						column.FlowDirection = FlowDirection.LeftToRight;

						var offset = printer.Settings.GetValue<Vector3>(SettingsKey.probe_offset);

						column.AddChild(
							new ValueTag(
								"Z Offset".Localize(),
								offset.Z.ToString("0.###"),
								new BorderDouble(12, 5, 2, 5),
								5,
								11)
							{
								Margin = new BorderDouble(bottom: 4),
								MinimumSize = new Vector2(125, 0)
							});

						column.AddChild(new HorizontalSpacer());
						AddRunStageButton("Run Z Calibration".Localize(), theme, stage, column);

						widget = column;
					}

					if (stage is LoadFilamentWizard loadWizard)
					{
						var column = CreateColumn(theme);
						column.FlowDirection = FlowDirection.LeftToRight;
						var lastRow = new FlowLayoutWidget()
						{
							HAnchor = HAnchor.Stretch
						};
						column.AddChild(lastRow);
						lastRow.AddChild(new HorizontalSpacer());
						AddRunStageButton("Load Filament".Localize(), theme, stage, lastRow).Margin = new BorderDouble(10);
						widget = column;
					}

					if (stage is PrintLevelingWizard levelingWizard)
					{
						PrintLevelingData levelingData = printer.Settings.Helpers.PrintLevelingData;

						var column = CreateColumn(theme);

						var lastRow = new FlowLayoutWidget()
						{
							HAnchor = HAnchor.Stretch
						};

						if (levelingData != null
							&& printer.Settings?.GetValue<bool>(SettingsKey.print_leveling_enabled) == true)
						{
							var positions = levelingData.SampledPositions;

							var levelingSolution = printer.Settings.GetValue(SettingsKey.print_leveling_solution);

							column.AddChild(
								new ValueTag(
									"Leveling Solution".Localize(),
									levelingSolution,
									new BorderDouble(12, 5, 2, 5),
									5,
									11)
								{
									Margin = new BorderDouble(bottom: 4),
									MinimumSize = new Vector2(125, 0)
								});

							var editButton = new IconButton(AggContext.StaticData.LoadIcon("icon_edit.png", 16, 16, theme.InvertIcons), theme)
							{
								Name = "Edit Leveling Data Button",
								ToolTipText = "Edit Leveling Data".Localize(),
							};

							editButton.Click += (s, e) =>
							{
								DialogWindow.Show(new EditLevelingSettingsPage(printer, theme));
							};

							var row = new FlowLayoutWidget()
							{
								VAnchor = VAnchor.Fit,
								HAnchor = HAnchor.Fit
							};
							row.AddChild(editButton);

							// only show the switch if leveling can be turned off (it can't if it is required).
							if (!printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print))
							{
								// put in the switch
								printLevelingSwitch = new RoundedToggleSwitch(theme)
								{
									VAnchor = VAnchor.Center,
									Margin = new BorderDouble(theme.DefaultContainerPadding, 0),
									Checked = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled),
									ToolTipText = "Enable Software Leveling".Localize()
								};
								printLevelingSwitch.CheckedStateChanged += (sender, e) =>
								{
									printer.Settings.Helpers.DoPrintLeveling(printLevelingSwitch.Checked);
								};
								printLevelingSwitch.Closed += (s, e) =>
								{
									// Unregister listeners
									printer.Settings.PrintLevelingEnabledChanged -= Settings_PrintLevelingEnabledChanged;
								};

								// TODO: Why is this listener conditional? If the leveling changes somehow, shouldn't we be updated the UI to reflect that?
								// Register listeners
								printer.Settings.PrintLevelingEnabledChanged += Settings_PrintLevelingEnabledChanged;

								row.AddChild(printLevelingSwitch);
							}

							rightWidget = row;

							column.AddChild(lastRow);

							var probeWidget = new ProbePositionsWidget(printer, positions.Select(v => new Vector2(v)).ToList(), theme)
							{
								HAnchor = HAnchor.Absolute,
								VAnchor = VAnchor.Absolute,
								Height = 200,
								Width = 200,
								RenderLevelingData = true,
								RenderProbePath = false,
								SimplePoints = true,
							};
							lastRow.AddChild(probeWidget);
						}
						else
						{
							column.AddChild(lastRow);

							if (!printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print))
							{
								lastRow.AddChild(new WrappedTextWidget(
									@"Print Leveling is an optional feature for this printer that can help improve print quality. If the bed is uneven or cannot be mechanically leveled.".Localize(),
									pointSize: theme.DefaultFontSize,
									textColor: theme.TextColor));
							}
							else if (printer.Settings.GetValue<bool>(SettingsKey.validate_leveling))
							{
								lastRow.AddChild(new WrappedTextWidget(
									@"Print Leveling will run automatically at the start of each print.".Localize(),
									pointSize: theme.DefaultFontSize,
									textColor: theme.TextColor));
							}
							else
							{
								lastRow.AddChild(new HorizontalSpacer());
							}
						}

						lastRow.AddChild(new HorizontalSpacer());
						AddRunStageButton("Run Print Leveling".Localize(), theme, stage, lastRow);

						widget = column;
					}

					if (stage is XyCalibrationWizard xyWizard)
					{
						var row = CreateColumn(theme);
						row.FlowDirection = FlowDirection.LeftToRight;

						var hotendOffset = printer.Settings.Helpers.ExtruderOffset(1);

						var tool2Column = new FlowLayoutWidget(FlowDirection.TopToBottom);
						row.AddChild(tool2Column);

						tool2Column.AddChild(
							new TextWidget("Tool".Localize() + " 2", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
							{
								Margin = new BorderDouble(bottom: 4)
							});

						tool2Column.AddChild(
							new ValueTag(
								"X Offset".Localize(),
								hotendOffset.X.ToString("0.###"),
								new BorderDouble(12, 5, 2, 5),
								5,
								11)
							{
								Margin = new BorderDouble(bottom: 4),
								MinimumSize = new Vector2(125, 0)
							});

						tool2Column.AddChild(
							new ValueTag(
								"Y Offset".Localize(),
								hotendOffset.Y.ToString("0.###"),
								new BorderDouble(12, 5, 2, 5),
								5,
								11)
							{
								MinimumSize = new Vector2(125, 0)
							});

						row.AddChild(new HorizontalSpacer());
						AddRunStageButton("Run Nozzle Alignment".Localize(), theme, stage, row);

						widget = row;
					}

					if (stage is LoadFilamentWizard filamentWizard)
					{
						widget.Margin = new BorderDouble(left: theme.DefaultContainerPadding);
					}

					var sectionName = stage.Title;
					if (stage.SetupRequired)
					{
						sectionName += " - " + "Required".Localize();
					}
					else if (stage.Completed)
					{
						sectionName += " - " + "Completed".Localize();
					}
					else
					{
						sectionName += " - " + "Optional".Localize();
					}


					var section = new SectionWidget(sectionName, widget, theme, rightAlignedContent: rightWidget, expandingContent: false);
					theme.ApplyBoxStyle(section);

					section.Margin = section.Margin.Clone(left: 0);
					section.ShowExpansionIcon = false;

					if (stage.SetupRequired)
					{
						section.BackgroundColor = Color.Red.WithAlpha(30);
					}

					contentRow.AddChild(section);
				}

				return homePage;
			};
		}

		private GuiWidget AddRunStageButton(string title, ThemeConfig theme, ISetupWizard stage, FlowLayoutWidget leftToRight)
		{
			var runStage = leftToRight.AddChild(new TextButton(title, theme)
			{
				VAnchor = VAnchor.Bottom,
				Enabled = printer.Connection.IsConnected && !printer.Connection.Printing && !printer.Connection.Paused,
				BackgroundColor = theme.MinimalShade.WithAlpha(25),
			});

			runStage.Click += (s, e) =>
			{
				// Only allow leftnav when not running SetupWizard
				if (StagedSetupWindow?.ActiveStage == null)
				{
					StagedSetupWindow.ActiveStage = stage;
				}
			};

			return runStage;
		}

		public bool AutoAdvance { get; set; }

		public Func<DialogPage> HomePageGenerator { get; }

		public bool ReturnedToHomePage { get; set; } = false;

		public StagedSetupWindow StagedSetupWindow { get; set; }

		public IEnumerable<ISetupWizard> Stages { get; }

		public string Title { get; } = "Printer Calibration".Localize();

		public Vector2 WindowSize { get; } = new Vector2(1200, 700);

		public static bool SetupRequired(PrinterConfig printer, bool requiresLoadedFilament)
		{
			return printer == null
				|| LevelingValidation.NeedsToBeRun(printer) // PrintLevelingWizard
				|| ZCalibrationWizard.NeedsToBeRun(printer)
				|| (requiresLoadedFilament && LoadFilamentWizard.NeedsToBeRun0(printer))
				|| (requiresLoadedFilament && LoadFilamentWizard.NeedsToBeRun1(printer))
				|| XyCalibrationWizard.NeedsToBeRun(printer);
		}

		private static FlowLayoutWidget CreateColumn(ThemeConfig theme)
		{
			return new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(theme.DefaultContainerPadding, theme.DefaultContainerPadding, theme.DefaultContainerPadding, 4)
			};
		}

		private void Settings_PrintLevelingEnabledChanged(object sender, EventArgs e)
		{
			if (printLevelingSwitch != null)
			{
				printLevelingSwitch.Checked = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled);
			}
		}
	}
}