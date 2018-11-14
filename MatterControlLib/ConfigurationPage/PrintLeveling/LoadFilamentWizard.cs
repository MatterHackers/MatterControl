/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class LoadFilamentWizard : PrinterSetupWizard
	{
		private bool onlyLoad;
		private static double temperatureAtStart;

		public static void Start(PrinterConfig printer, ThemeConfig theme, bool onlyLoad)
		{
			temperatureAtStart = printer.Connection.GetTargetHotendTemperature(0);

			var levelingContext = new LoadFilamentWizard(printer, onlyLoad);

			if (onlyLoad)
			{
				levelingContext.WindowTitle = $"{ApplicationController.Instance.ProductName} - " + "Load Filament Wizard".Localize();
			}
			else
			{
				levelingContext.WindowTitle = $"{ApplicationController.Instance.ProductName} - " + "Select Filament Wizard".Localize();
			}

			var loadFilamentWizardWindow = DialogWindow.Show(new LevelingWizardRootPage(levelingContext)
			{
				WindowTitle = levelingContext.WindowTitle
			});
			loadFilamentWizardWindow.Closed += (s, e) =>
			{
				printer.Connection.SetTargetHotendTemperature(0, temperatureAtStart);
			};
		}

		public LoadFilamentWizard(PrinterConfig printer, bool onlyLoad)
			: base(printer)
		{
			this.onlyLoad = onlyLoad;
		}

		public static bool NeedsToBeRun(PrinterConfig printer)
		{
			// we have a probe that we are using and we have not done leveling yet
			return !printer.Settings.GetValue<bool>(SettingsKey.filament_has_been_loaded);
		}

		protected override IEnumerator<PrinterSetupWizardPage> GetWizardSteps()
		{
			var levelingStrings = new LevelingStrings(printer.Settings);

			var title = "Select Material".Localize();
			var instructions = "Please select the material you will be printing with.".Localize();

			if (onlyLoad)
			{
				title = "Load Material".Localize();
				instructions = "Please select the material you want to load.".Localize();
			}

			// select the material
			yield return new SelectMaterialPage(this, title, instructions, onlyLoad ? "Load".Localize() : "Select".Localize(), onlyLoad);

			var theme = ApplicationController.Instance.Theme;

			// show the trim filament message
			{
				PrinterSetupWizardPage trimFilamentPage = null;
				trimFilamentPage = new PrinterSetupWizardPage(
					this,
					"Trim Filament".Localize(),
					"")
				{
					BecomingActive = () =>
					{
						// start heating up the extruder
						printer.Connection.SetTargetHotendTemperature(0, printer.Settings.GetValue<double>(SettingsKey.temperature));

						var markdownText = printer.Settings.GetValue(SettingsKey.trim_filament_markdown);
						var markdownWidget = new MarkdownWidget(theme);
						markdownWidget.Markdown = markdownText = markdownText.Replace("\\n", "\n");
						trimFilamentPage.ContentRow.AddChild(markdownWidget);
					}
				};
				yield return trimFilamentPage;
			}

			// show the insert filament page
			{
				RunningInterval runningGCodeCommands = null;
				PrinterSetupWizardPage insertFilamentPage = null;
				insertFilamentPage = new PrinterSetupWizardPage(
					this,
					"Insert Filament".Localize(),
					"")
				{
					BecomingActive = () =>
					{
						var markdownText = printer.Settings.GetValue(SettingsKey.insert_filament_markdown2);
						var markdownWidget = new MarkdownWidget(theme);
						markdownWidget.Markdown = markdownText = markdownText.Replace("\\n", "\n");
						insertFilamentPage.ContentRow.AddChild(markdownWidget);

						// turn off the fan
						printer.Connection.FanSpeed0To255 = 0;
						// Allow extrusion at any temperature. S0 only works on Marlin S1 works on repetier and marlin
						printer.Connection.QueueLine("M302 S1");

						var runningTime = Stopwatch.StartNew();
						runningGCodeCommands = UiThread.SetInterval(() =>
						{
							if (printer.Connection.NumQueuedCommands == 0)
							{
								printer.Connection.MoveRelative(PrinterCommunication.PrinterConnection.Axis.E, .2, 80);
								int secondsToRun = 300;
								if (runningTime.ElapsedMilliseconds > secondsToRun * 1000)
								{
									UiThread.ClearInterval(runningGCodeCommands);
								}
							}
						},
						.1);
					},
					BecomingInactive = () =>
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
				PrinterSetupWizardPage loadingFilamentPage = null;
				loadingFilamentPage = new PrinterSetupWizardPage(
					this,
					"Loading Filament".Localize(),
					"")
				{
					BecomingActive = () =>
					{
						loadingFilamentPage.NextButton.Enabled = false;

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
						loadingFilamentPage.ContentRow.AddChild(holder);

						// Allow extrusion at any temperature. S0 only works on Marlin S1 works on repetier and marlin
						printer.Connection.QueueLine("M302 S1");
						// send a dwel to empty out the current move commands
						printer.Connection.QueueLine("G4 P1");
						// put in a second one to use as a signal for the first being processed
						printer.Connection.QueueLine("G4 P1");
						// start heating up the extruder
						printer.Connection.SetTargetHotendTemperature(0, printer.Settings.GetValue<double>(SettingsKey.temperature));

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
								if(runningTime == null)
								{
									runningTime = Stopwatch.StartNew();
								}

								if (progressBar.RatioComplete < 1)
								{
									var thisExtrude = Math.Min(remainingLengthMm, maxSingleExtrudeLength);
									var currentE = printer.Connection.CurrentExtruderDestination;
									printer.Connection.QueueLine("G1 E{0:0.###} F{1}".FormatWith(currentE + thisExtrude, loadingSpeedMmPerS * 60));
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
								loadingFilamentPage.NextButton.InvokeClick();
							}
						},
						.1);
					},
					BecomingInactive = () =>
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
				double targetHotendTemp = printer.Settings.Helpers.ExtruderTemperature(0);
				yield return new WaitForTempPage(
					this,
					"Waiting For Printer To Heat".Localize(),
					"Waiting for the hotend to heat to ".Localize() + targetHotendTemp + "°C.\n"
						+ "This will ensure that filament is able to flow through the nozzle.".Localize() + "\n"
						+ "\n"
						+ "Warning! The tip of the nozzle will be HOT!".Localize() + "\n"
						+ "Avoid contact with your skin.".Localize(),
					0,
					targetHotendTemp);
			}

			// extrude slowly so that we can prime the extruder
			{
				RunningInterval runningGCodeCommands = null;
				PrinterSetupWizardPage runningCleanPage = null;
				runningCleanPage = new PrinterSetupWizardPage(
					this,
					"Wait For Running Clean".Localize(),
					"")
				{
					BecomingActive = () =>
					{
						var markdownText = printer.Settings.GetValue(SettingsKey.running_clean_markdown2);
						var markdownWidget = new MarkdownWidget(theme);
						markdownWidget.Markdown = markdownText = markdownText.Replace("\\n", "\n");
						runningCleanPage.ContentRow.AddChild(markdownWidget);

						var runningTime = Stopwatch.StartNew();
						runningGCodeCommands = UiThread.SetInterval(() =>
						{
							if (printer.Connection.NumQueuedCommands == 0)
							{
								printer.Connection.MoveRelative(PrinterCommunication.PrinterConnection.Axis.E, .35, 140);
								int secondsToRun = 90;
								if (runningTime.ElapsedMilliseconds > secondsToRun * 1000)
								{
									UiThread.ClearInterval(runningGCodeCommands);
								}
							}
						},
						.1);
					},
					BecomingInactive = () =>
					{
						UiThread.ClearInterval(runningGCodeCommands);
					}
				};
				runningCleanPage.Closed += (s, e) =>
				{
					UiThread.ClearInterval(runningGCodeCommands);
					printer.Settings.SetValue(SettingsKey.filament_has_been_loaded, "1");
				};

				yield return runningCleanPage;
			}

			// put up a success message
			PrinterSetupWizardPage finalPage = null;
			finalPage = new PrinterSetupWizardPage(this, "Success".Localize(), "Success!\n\nYour filament should now be loaded".Localize())
			{
				BecomingActive = () =>
				{
					finalPage.ShowWizardFinished();
				}
			};

			yield return finalPage;
		}
	}
}
