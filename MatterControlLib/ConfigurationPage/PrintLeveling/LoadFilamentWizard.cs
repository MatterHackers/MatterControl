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
#if !__ANDROID__
using Markdig.Agg;
#endif
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class LoadFilamentWizard : PrinterSetupWizard
	{
		private bool onlyLoad;

		public static void Start(PrinterConfig printer, ThemeConfig theme, bool onlyLoad)
		{
			// turn off print leveling
			var levelingContext = new LoadFilamentWizard(printer, onlyLoad)
			{
				WindowTitle = $"{ApplicationController.Instance.ProductName} - " + "Load Filament Wizard".Localize()
			};

			var loadFilamentWizardWindow = DialogWindow.Show(new LevelingWizardRootPage(levelingContext)
			{
				WindowTitle = levelingContext.WindowTitle
			});
			loadFilamentWizardWindow.Closed += (s, e) =>
			{
				printer.Connection.TurnOffBedAndExtruders(TurnOff.AfterDelay);
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

			var title = "Load Material".Localize();
			var instructions = "Please select the material you want to load.".Localize();

			// select the material
			yield return new SelectMaterialPage(this, title, instructions, onlyLoad);

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

#if !__ANDROID__
						var markdownText = printer.Settings.GetValue(SettingsKey.trim_filament_markdown);
						var markdownWidget = new MarkdownWidget(theme);
						markdownWidget.Markdown = markdownText = markdownText.Replace("\\n", "\n");
						trimFilamentPage.ContentRow.AddChild(markdownWidget);
#endif
					}
				};
				yield return trimFilamentPage;
			}

			// show the instert filament page
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
#if !__ANDROID__
						var markdownText = printer.Settings.GetValue(SettingsKey.insert_filament_markdown);
						var markdownWidget = new MarkdownWidget(theme);
						markdownWidget.Markdown = markdownText = markdownText.Replace("\\n", "\n");
						insertFilamentPage.ContentRow.AddChild(markdownWidget);
#endif

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
									runningGCodeCommands.Continue = false;
								}
							}
						},
						.1);
					},
					BecomingInactive = () =>
					{
						if (runningGCodeCommands != null
							&& runningGCodeCommands.Continue)
						{
							runningGCodeCommands.Continue = false;
						}
					}
				};
				insertFilamentPage.Closed += (s, e) =>
				{
					if (runningGCodeCommands != null
						&& runningGCodeCommands.Continue)
					{
						runningGCodeCommands.Continue = false;
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
						var holder = new FlowLayoutWidget();
						var progressBar = new ProgressBar((int)(150 * GuiWidget.DeviceScale), (int)(15 * GuiWidget.DeviceScale))
						{
							FillColor = theme.PrimaryAccentColor,
							BorderColor = ActiveTheme.Instance.PrimaryTextColor,
							BackgroundColor = Color.White,
							Margin = new BorderDouble(3, 0, 0, 10),
						};
						var progressBarText = new TextWidget("", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor)
						{
							AutoExpandBoundsToText = true,
							Margin = new BorderDouble(5, 0, 0, 0),
						};
						holder.AddChild(progressBar);
						holder.AddChild(progressBarText);
						loadingFilamentPage.ContentRow.AddChild(holder);

						// Allow extrusion at any temperature. S0 only works on Marlin S1 works on repetier and marlin
						printer.Connection.QueueLine("M302 S1");
						// start heating up the extruder
						printer.Connection.SetTargetHotendTemperature(0, printer.Settings.GetValue<double>(SettingsKey.temperature));

						var loadingSpeed = printer.Settings.GetValue<double>(SettingsKey.load_filament_speed);
						var loadLength = Math.Max(1, printer.Settings.GetValue<double>(SettingsKey.load_filament_length));
						var remainingLength = loadLength;
						var maxSingleExtrudeLength = 10;

						var runningTime = Stopwatch.StartNew();
						runningGCodeCommands = UiThread.SetInterval(() =>
						{
							if (printer.Connection.NumQueuedCommands == 0)
							{
								if (remainingLength > 0)
								{
									var thisExtrude = Math.Min(remainingLength, maxSingleExtrudeLength);
									var currentE = printer.Connection.CurrentExtruderDestination;
									printer.Connection.QueueLine("G1 {0}{1:0.###} F{2}".FormatWith(PrinterCommunication.PrinterConnection.Axis.E, currentE + thisExtrude, loadingSpeed * 60));
									remainingLength -= thisExtrude;
									progressBar.RatioComplete = 1 - remainingLength / loadLength;
									progressBarText.Text = $"Filament Loaded: {progressBar.RatioComplete * 100:0}%";
								}
							}

							if (runningGCodeCommands.Continue
								&& remainingLength <= 0)
							{
								runningGCodeCommands.Continue = false;
								loadingFilamentPage.NextButton.InvokeClick();
							}
						},
						.1);
					},
					BecomingInactive = () =>
					{
						if (runningGCodeCommands.Continue)
						{
							runningGCodeCommands.Continue = false;
						}
					}
				};
				loadingFilamentPage.Closed += (s, e) =>
				{
					if (runningGCodeCommands.Continue)
					{
						runningGCodeCommands.Continue = false;
					}
				};

				yield return loadingFilamentPage;
			}

			// wait for extruder to heat
			{
				double targetHotendTemp = printer.Settings.Helpers.ExtruderTemperature(0);
				yield return new WaitForTempPage(
					this,
					"Waiting For Printer To Heat".Localize(),
					"Waiting for the hotend to heat to ".Localize() + targetHotendTemp + ".\n"
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
						runningCleanPage.ShowWizardFinished();
#if !__ANDROID__
						var markdownText = printer.Settings.GetValue(SettingsKey.running_clean_markdown);
						var markdownWidget = new MarkdownWidget(theme);
						markdownWidget.Markdown = markdownText = markdownText.Replace("\\n", "\n");
						runningCleanPage.ContentRow.AddChild(markdownWidget);
#endif

						var runningTime = Stopwatch.StartNew();
						runningGCodeCommands = UiThread.SetInterval(() =>
						{
							if (printer.Connection.NumQueuedCommands == 0)
							{
								printer.Connection.MoveRelative(PrinterCommunication.PrinterConnection.Axis.E, .35, 140);
								int secondsToRun = 90;
								if (runningTime.ElapsedMilliseconds > secondsToRun * 1000)
								{
									runningGCodeCommands.Continue = false;
								}
							}
						},
						.1);
					},
					BecomingInactive = () =>
					{
						if (runningGCodeCommands.Continue)
						{
							runningGCodeCommands.Continue = false;
						}
					}
				};
				runningCleanPage.Closed += (s, e) =>
				{
					if (runningGCodeCommands.Continue)
					{
						runningGCodeCommands.Continue = false;
						printer.Settings.SetValue(SettingsKey.filament_has_been_loaded, "1");
					}
				};

				yield return runningCleanPage;
			}
		}
	}
}
