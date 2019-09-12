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
using MatterControl.Printing.PrintLeveling;
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
		private RoundedToggleSwitch printLevelingSwitch;
		private PrinterConfig printer;

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

				if (!this.ReturnedToHomePage)
				{
					contentRow.AddChild(
						new WrappedTextWidget(
							@"Select the calibration task on the left to continue".Replace("\r\n", "\n"),
							pointSize: theme.DefaultFontSize,
							textColor: theme.TextColor));
				}

				contentRow.BackgroundColor = Color.Transparent;

				foreach (var stage in this.Stages.Where(s => s.Enabled && s.Visible))
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

						widget = column;
					}

					if (stage is PrintLevelingWizard levelingWizard)
					{
						PrintLevelingData levelingData = printer.Settings.Helpers.PrintLevelingData;

						// Always show leveling option if printer does not have hardware leveling
						if (!printer.Settings.GetValue<bool>(SettingsKey.has_hardware_leveling))
						{
							var positions = levelingData.SampledPositions;

							var column = CreateColumn(theme);

							column.AddChild(
								new ValueTag(
									"Leveling Solution".Localize(),
									printer.Settings.GetValue(SettingsKey.print_leveling_solution),
									new BorderDouble(12, 5, 2, 5),
									5,
									11)
								{
									Margin = new BorderDouble(bottom: 4),
									MinimumSize = new Vector2(125, 0)
								});

							var row = new FlowLayoutWidget()
							{
								VAnchor = VAnchor.Fit,
								HAnchor = HAnchor.Fit
							};

							// Only show Edit button if data initialized
							if (levelingData?.SampledPositions.Count() > 0)
							{
								var editButton = new IconButton(AggContext.StaticData.LoadIcon("icon_edit.png", 16, 16, theme.InvertIcons), theme)
								{
									Name = "Edit Leveling Data Button",
									ToolTipText = "Edit Leveling Data".Localize(),
								};

								editButton.Click += (s, e) =>
								{
									DialogWindow.Show(new EditLevelingSettingsPage(printer, theme));
								};

								row.AddChild(editButton);
							}

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
									printer.Settings.PrintLevelingEnabledChanged -= this.Settings_PrintLevelingEnabledChanged;
								};

								// TODO: Why is this listener conditional? If the leveling changes somehow, shouldn't we be updated the UI to reflect that?
								// Register listeners
								printer.Settings.PrintLevelingEnabledChanged += this.Settings_PrintLevelingEnabledChanged;

								row.AddChild(printLevelingSwitch);
							}

							rightWidget = row;

							// Only visualize leveling data if initialized
							if (levelingData?.SampledPositions.Count() > 0)
							{
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
								column.AddChild(probeWidget);
							}

							widget = column;
						}
					}

					if (stage is XyCalibrationWizard xyWizard)
					{
						var column = CreateColumn(theme);
						column.FlowDirection = FlowDirection.LeftToRight;

						var hotendOffset = printer.Settings.Helpers.ExtruderOffset(1);

						var tool2Column = new FlowLayoutWidget(FlowDirection.TopToBottom);
						column.AddChild(tool2Column);

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

						widget = column;
					}

					if (stage.SetupRequired)
					{
						var column = CreateColumn(theme);
						column.AddChild(new TextWidget("Setup Required".Localize(), pointSize: theme.DefaultFontSize, textColor: theme.TextColor));

						widget = column;
					}
					else if (stage is LoadFilamentWizard filamentWizard)
					{
						widget.Margin = new BorderDouble(left: theme.DefaultContainerPadding);
					}

					var section = new SectionWidget(stage.Title, widget, theme, rightAlignedContent: rightWidget, expandingContent: false);
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

		private void Settings_PrintLevelingEnabledChanged(object sender, EventArgs e)
		{
			if (printLevelingSwitch != null)
			{
				printLevelingSwitch.Checked = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled);
			}
		}

		private static FlowLayoutWidget CreateColumn(ThemeConfig theme)
		{
			return new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(theme.DefaultContainerPadding, theme.DefaultContainerPadding, theme.DefaultContainerPadding, 4)
			};
		}

		public bool AutoAdvance { get; set; }

		public string Title { get; } = "Printer Calibration".Localize();

		public Vector2 WindowSize { get; } = new Vector2(1200, 700);

		public IEnumerable<ISetupWizard> Stages { get; }


		public Func<DialogPage> HomePageGenerator { get; }

		public bool ReturnedToHomePage { get; set; } = false;

		public static bool SetupRequired(PrinterConfig printer, bool requiresLoadedFilament)
		{
			if (printer == null)
			{
				return true;
			}

			// TODO: Verify invoked with low frequency
			var printerShim = ApplicationController.Instance.Shim(printer);

			return LevelingValidation.NeedsToBeRun(printerShim) // PrintLevelingWizard
				|| ZCalibrationWizard.NeedsToBeRun(printer)
				|| (requiresLoadedFilament && LoadFilamentWizard.NeedsToBeRun0(printer))
				|| (requiresLoadedFilament && LoadFilamentWizard.NeedsToBeRun1(printer))
				|| XyCalibrationWizard.NeedsToBeRun(printer);
		}
	}
}