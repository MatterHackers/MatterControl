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
using System.Diagnostics;
using Markdig.Agg;
using MatterControl.Printing;
using MatterControl.Printing.PrintLeveling;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class LoadFilamentWizard : PrinterSetupWizard
	{
		private readonly bool showAlreadyLoadedButton;

		private int extruderIndex;

		public LoadFilamentWizard(PrinterConfig printer, int extruderIndex, bool showAlreadyLoadedButton)
			: base(printer)
		{
			this.showAlreadyLoadedButton = showAlreadyLoadedButton;
			if (printer.Settings.GetValue<int>(SettingsKey.extruder_count) == 1)
			{
				this.Title = "Load Filament".Localize();
			}
			else
			{
				this.Title = "Load Extruder".Localize() + $" {extruderIndex + 1}";
			}

			this.extruderIndex = extruderIndex;
		}

		public override bool Completed
		{
			get
			{
				if (extruderIndex == 0)
				{
					return printer.Settings.GetValue<bool>(SettingsKey.filament_has_been_loaded);
				}
				else
				{
					return printer.Settings.GetValue<bool>(SettingsKey.filament_1_has_been_loaded);
				}
			}
		}

		public double TemperatureAtStart { get; private set; }

		public override bool Visible
		{
			get
			{
				if (extruderIndex == 0)
				{
					return !printer.Settings.GetValue<bool>(SettingsKey.filament_has_been_loaded);
				}
				else
				{
					return !printer.Settings.GetValue<bool>(SettingsKey.filament_1_has_been_loaded)
						&& printer.Settings.GetValue<int>(SettingsKey.extruder_count) > 1;
				}
			}
		}

		public override bool Enabled => true;

		public override void Dispose()
		{
			printer.Connection.SetTargetHotendTemperature(extruderIndex, this.TemperatureAtStart);
		}

		public static bool NeedsToBeRun0(PrinterConfig printer)
		{
			return !printer.Settings.GetValue<bool>(SettingsKey.filament_has_been_loaded);
		}

		public static bool NeedsToBeRun1(PrinterConfig printer)
		{
			var extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);
			return extruderCount > 1
				&& !printer.Settings.GetValue<bool>(SettingsKey.filament_1_has_been_loaded)
				&& Slicer.T1OrGreaterUsed(printer);
		}

		public override bool SetupRequired
		{
			// Defer to NeedsToBeRun methods for status
			get => (extruderIndex == 0) ? NeedsToBeRun0(printer) : NeedsToBeRun1(printer);
		}

		protected override IEnumerator<WizardPage> GetPages()
		{
			// Initialize - store startup temp and extruder index
			this.TemperatureAtStart = printer.Connection.GetTargetHotendTemperature(extruderIndex);

			var extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);

			var levelingStrings = new LevelingStrings();

			var title = "Load Material".Localize();
			var instructions = "Please select the material you want to load.".Localize();

			if (extruderCount > 1)
			{
				title = "Load Extruder {0}".Localize().FormatWith(extruderIndex + 1);
				instructions = "Please select the material you want to load into extruder {0}.".Localize().FormatWith(extruderIndex + 1);
			}

			// select the material
			yield return new SelectMaterialPage(this, title, instructions, "Select".Localize(), extruderIndex, true, showAlreadyLoadedButton)
			{
				WindowTitle = Title
			};

			var theme = ApplicationController.Instance.Theme;

			// show the trim filament message
			{
				var trimFilamentPage = new WizardPage(this, "Trim Filament".Localize(), "")
				{
					PageLoad = (page) =>
					{
						// start heating up the extruder
						printer.Connection.SetTargetHotendTemperature(extruderIndex, printer.Settings.GetValue<double>(SettingsKey.temperature));

						var markdownText = printer.Settings.GetValue(SettingsKey.trim_filament_markdown);
						var markdownWidget = new MarkdownWidget(theme);
						markdownWidget.Markdown = markdownText = markdownText.Replace("\\n", "\n");
						page.ContentRow.AddChild(markdownWidget);
					}
				};
				yield return trimFilamentPage;
			}

			if (extruderCount > 1)
			{
				// reset the extruder that was active
				printer.Connection.QueueLine($"T{extruderIndex}");
			}

			// reset the extrusion amount so this is easier to debug
			printer.Connection.QueueLine("G92 E0");

			// show the insert filament page
			{
				RunningInterval runningGCodeCommands = null;
				var insertFilamentPage = new WizardPage(this, "Insert Filament".Localize(), "")
				{
					PageLoad = (page) =>
					{
						var markdownText = printer.Settings.GetValue(SettingsKey.insert_filament_markdown2);

						if (extruderIndex == 1)
						{
							markdownText = printer.Settings.GetValue(SettingsKey.insert_filament_1_markdown);
						}

						var markdownWidget = new MarkdownWidget(theme);
						markdownWidget.Markdown = markdownText = markdownText.Replace("\\n", "\n");
						page.ContentRow.AddChild(markdownWidget);

						// turn off the fan
						printer.Connection.FanSpeed0To255 = 0;
						// Allow extrusion at any temperature. S0 only works on Marlin S1 works on repetier and marlin
						printer.Connection.QueueLine("M302 S1");

						int maxSecondsToStartLoading = 300;
						var runningTime = Stopwatch.StartNew();
						runningGCodeCommands = UiThread.SetInterval(() =>
						{
							if (printer.Connection.NumQueuedCommands == 0)
							{
								if (false)
								{
									// Quiet mode
									printer.Connection.MoveRelative(PrinterAxis.E, 1, 80);
									printer.Connection.QueueLine("G4 P1"); // empty buffer - allow for cancel
								}
								else
								{
									// Pulse mode
									printer.Connection.MoveRelative(PrinterAxis.E, 1, 150);
									printer.Connection.QueueLine("G4 P10"); // empty buffer - allow for cancel
								}

								if (runningTime.ElapsedMilliseconds > maxSecondsToStartLoading * 1000)
								{
									UiThread.ClearInterval(runningGCodeCommands);
								}
							}
						},
						.1);
					},
					PageClose = () =>
					{
						if (runningGCodeCommands != null)
						{
							UiThread.ClearInterval(runningGCodeCommands);
						}
					}
				};
				insertFilamentPage.Closed += (s, e) =>
				{
					if (runningGCodeCommands != null)
					{
						UiThread.ClearInterval(runningGCodeCommands);
					}
				};

				yield return insertFilamentPage;
			}

			// show the loading filament progress bar
			{
				RunningInterval runningGCodeCommands = null;
				var loadingFilamentPage = new WizardPage(this, "Loading Filament".Localize(), "")
				{
					PageLoad = (page) =>
					{
						page.NextButton.Enabled = false;

						// add the progress bar
						var holder = new FlowLayoutWidget()
						{
							Margin = new BorderDouble(3, 0, 0, 10),
						};
						var progressBar = new ProgressBar((int)(150 * GuiWidget.DeviceScale), (int)(15 * GuiWidget.DeviceScale))
						{
							FillColor = theme.PrimaryAccentColor,
							BorderColor = theme.TextColor,
							BackgroundColor = Color.White,
							VAnchor = VAnchor.Center,
						};
						var progressBarText = new TextWidget("", pointSize: 10, textColor: theme.TextColor)
						{
							AutoExpandBoundsToText = true,
							Margin = new BorderDouble(5, 0, 0, 0),
							VAnchor = VAnchor.Center,
						};
						holder.AddChild(progressBar);
						holder.AddChild(progressBarText);
						page.ContentRow.AddChild(holder);

						// Allow extrusion at any temperature. S0 only works on Marlin S1 works on repetier and marlin
						printer.Connection.QueueLine("M302 S1");
						// send a dwell to empty out the current move commands
						printer.Connection.QueueLine("G4 P1");
						// put in a second one to use as a signal for the first being processed
						printer.Connection.QueueLine("G4 P1");
						// start heating up the extruder
						printer.Connection.SetTargetHotendTemperature(extruderIndex, printer.Settings.GetValue<double>(SettingsKey.temperature));

						var loadingSpeedMmPerS = printer.Settings.GetValue<double>(SettingsKey.load_filament_speed);
						var loadLengthMm = Math.Max(1, printer.Settings.GetValue<double>(SettingsKey.load_filament_length));
						var remainingLengthMm = loadLengthMm;
						var maxSingleExtrudeLength = 20;

						Stopwatch runningTime = null;
						var expectedTimeS = loadLengthMm / loadingSpeedMmPerS;

						runningGCodeCommands = UiThread.SetInterval(() =>
						{
							if (printer.Connection.NumQueuedCommands == 0)
							{
								if (runningTime == null)
								{
									runningTime = Stopwatch.StartNew();
								}

								if (progressBar.RatioComplete < 1
									|| remainingLengthMm >= .001)
								{
									var thisExtrude = Math.Min(remainingLengthMm, maxSingleExtrudeLength);
									var currentE = printer.Connection.CurrentExtruderDestination;
									printer.Connection.QueueLine("G1 E{0:0.###} F{1}".FormatWith(currentE + thisExtrude, loadingSpeedMmPerS * 60));
									// make sure we wait for this command to finish so we can cancel the unload at any time without delay
									printer.Connection.QueueLine("G4 P1");
									remainingLengthMm -= thisExtrude;
									var elapsedSeconds = runningTime.Elapsed.TotalSeconds;
									progressBar.RatioComplete = Math.Min(1, elapsedSeconds / expectedTimeS);
									progressBarText.Text = $"Loading Filament: {Math.Max(0, expectedTimeS - elapsedSeconds):0}";
								}
							}

							if (progressBar.RatioComplete == 1
								&& remainingLengthMm <= .001)
							{
								UiThread.ClearInterval(runningGCodeCommands);
								page.NextButton.InvokeClick();
							}
						},
						.1);
					},
					PageClose = () =>
					{
						UiThread.ClearInterval(runningGCodeCommands);
					}
				};
				loadingFilamentPage.Closed += (s, e) =>
				{
					UiThread.ClearInterval(runningGCodeCommands);
				};

				yield return loadingFilamentPage;
			}

			// wait for extruder to heat
			{
				var targetHotendTemp = printer.Settings.Helpers.ExtruderTargetTemperature(extruderIndex);
				var temps = new double[4];
				temps[extruderIndex] = targetHotendTemp;
				yield return new WaitForTempPage(
					this,
					"Waiting For Printer To Heat".Localize(),
					"Waiting for the hotend to heat to ".Localize() + targetHotendTemp + "°C.\n"
						+ "This will ensure that filament is able to flow through the nozzle.".Localize() + "\n"
						+ "\n"
						+ "Warning! The tip of the nozzle will be HOT!".Localize() + "\n"
						+ "Avoid contact with your skin.".Localize(),
					0,
					temps);
			}

			// extrude slowly so that we can prime the extruder
			{
				RunningInterval runningGCodeCommands = null;
				var runningCleanPage = new WizardPage(this, "Wait For Running Clean".Localize(), "")
				{
					PageLoad = (page) =>
					{
						var markdownText = printer.Settings.GetValue(SettingsKey.running_clean_markdown2);

						if (extruderIndex == 1)
						{
							markdownText = printer.Settings.GetValue(SettingsKey.running_clean_1_markdown);
						}

						var markdownWidget = new MarkdownWidget(theme);
						markdownWidget.Markdown = markdownText = markdownText.Replace("\\n", "\n");
						page.ContentRow.AddChild(markdownWidget);

						var runningTime = Stopwatch.StartNew();
						runningGCodeCommands = UiThread.SetInterval(() =>
						{
							if (printer.Connection.NumQueuedCommands == 0)
							{
								if (false)
								{
									// Quite mode
									printer.Connection.MoveRelative(PrinterAxis.E, 2, 140);
									printer.Connection.QueueLine("G4 P1"); // empty buffer - allow for cancel
								}
								else
								{
									// Pulse mode
									printer.Connection.MoveRelative(PrinterAxis.E, 2, 150);
									printer.Connection.QueueLine("G4 P10"); // empty buffer - allow for cancel
								}

								int secondsToRun = 90;
								if (runningTime.ElapsedMilliseconds > secondsToRun * 1000)
								{
									UiThread.ClearInterval(runningGCodeCommands);
								}
							}
						},
						.1);
					},
					PageClose = () =>
					{
						UiThread.ClearInterval(runningGCodeCommands);
					}
				};

				yield return runningCleanPage;
			}

			// put up a success message
			yield return new DoneLoadingPage(this, extruderIndex);
		}
	}

	public class DoneLoadingPage : WizardPage
	{
		private int extruderIndex;

		public DoneLoadingPage(PrinterSetupWizard setupWizard, int extruderIndex)
			: base(setupWizard, "Filament Loaded".Localize(), "Success!\n\nYour filament should now be loaded".Localize())
		{
			this.extruderIndex = extruderIndex;

			if (printer.Connection.Paused)
			{
				var resumePrintingButton = new TextButton("Resume Printing".Localize(), theme)
				{
					Name = "Resume Printing Button",
					BackgroundColor = theme.MinimalShade,
				};
				resumePrintingButton.Click += (s, e) =>
				{
					printer.Connection.Resume();
					this.DialogWindow.ClosePage();
				};

				this.AcceptButton = resumePrintingButton;
				this.AddPageAction(resumePrintingButton);
			}
		}

		public override void OnLoad(EventArgs args)
		{
			switch (extruderIndex)
			{
				case 0:
					printer.Settings.SetValue(SettingsKey.filament_has_been_loaded, "1");
					break;

				case 1:
					printer.Settings.SetValue(SettingsKey.filament_1_has_been_loaded, "1");
					break;
			}

			this.ShowWizardFinished();

			base.OnLoad(args);
		}
	}
}
