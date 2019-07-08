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
using MatterControl.Printing;
using MatterControl.Printing.PrintLeveling;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class UnloadFilamentWizard : PrinterSetupWizard
	{
		private int extruderIndex;

		public UnloadFilamentWizard(PrinterConfig printer, int extruderIndex)
			: base(printer)
		{
			this.Title = "Unload Filament".Localize();
			this.extruderIndex = extruderIndex;
		}

		public override bool SetupRequired => false;

		public override bool Visible => true;

		public override bool Enabled => true;

		public override void Dispose()
		{
			printer.Connection.TurnOffBedAndExtruders(TurnOff.AfterDelay);
		}

		protected override IEnumerator<WizardPage> GetPages()
		{
			var extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);

			var levelingStrings = new LevelingStrings();

			var title = "Unload Material".Localize();
			var instructions = "Please select the material you want to unload.".Localize();
			if (extruderCount > 1)
			{
				instructions = "Please select the material you want to unload from extruder {0}.".Localize().FormatWith(extruderIndex + 1);
			}

			// select the material
			yield return new SelectMaterialPage(this, title, instructions, "Unload".Localize(), extruderIndex, false, false)
			{
				WindowTitle = Title
			};

			var theme = ApplicationController.Instance.Theme;

			// wait for extruder to heat
			{
				var targetHotendTemp = printer.Settings.Helpers.ExtruderTargetTemperature(extruderIndex);
				var temps = new double[4];
				temps[extruderIndex] = targetHotendTemp;
				yield return new WaitForTempPage(
					this,
					"Waiting For Printer To Heat".Localize(),
					(extruderCount > 1 ? "Waiting for hotend {0} to heat to ".Localize().FormatWith(extruderIndex + 1) : "Waiting for the hotend to heat to ".Localize()) + targetHotendTemp + "°C.\n"
						+ "This will ensure that filament is able to flow through the nozzle.".Localize() + "\n"
						+ "\n"
						+ "Warning! The tip of the nozzle will be HOT!".Localize() + "\n"
						+ "Avoid contact with your skin.".Localize(),
					0,
					temps);
			}

			// show the unloading filament progress bar
			{
				int extruderPriorToUnload = printer.Connection.ActiveExtruderIndex;

				RunningInterval runningGCodeCommands = null;
				var unloadingFilamentPage = new WizardPage(this, "Unloading Filament".Localize(), "")
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

						if (extruderCount > 1)
						{
							// reset the extruder that was active
							printer.Connection.QueueLine($"T{extruderIndex}");
						}

						// reset the extrusion amount so this is easier to debug
						printer.Connection.QueueLine("G92 E0");

						// Allow extrusion at any temperature. S0 only works on Marlin S1 works on repetier and marlin
						printer.Connection.QueueLine("M302 S1");
						// send a dwell to empty out the current move commands
						printer.Connection.QueueLine("G4 P1");
						// put in a second one to use as a signal for the first being processed
						printer.Connection.QueueLine("G4 P1");
						// start heating up the extruder
						if (extruderIndex == 0)
						{
							printer.Connection.SetTargetHotendTemperature(0, printer.Settings.GetValue<double>(SettingsKey.temperature));
						}
						else
						{
							printer.Connection.SetTargetHotendTemperature(extruderIndex, printer.Settings.GetValue<double>(SettingsKey.temperature + extruderIndex.ToString()));
						}

						var loadingSpeedMmPerS = printer.Settings.GetValue<double>(SettingsKey.load_filament_speed);
						var unloadLengthMm = Math.Max(1, printer.Settings.GetValue<double>(SettingsKey.unload_filament_length));
						var remainingLengthMm = unloadLengthMm;
						var maxSingleExtrudeLength = 20;

						Stopwatch runningTime = null;
						var expectedTimeS = unloadLengthMm / loadingSpeedMmPerS;

						double currentE = 0;

						runningGCodeCommands = UiThread.SetInterval(() =>
						{
							// wait until the printer has processed all our commands (including G92)
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
									currentE = printer.Connection.CurrentExtruderDestination;
									printer.Connection.QueueLine("G1 E{0:0.###} F{1}".FormatWith(currentE - thisExtrude, loadingSpeedMmPerS * 60));
									// make sure we wait for this command to finish so we can cancel the unload at any time without delay
									printer.Connection.QueueLine("G4 P1");
									remainingLengthMm -= thisExtrude;
									var elapsedSeconds = runningTime.Elapsed.TotalSeconds;
									progressBar.RatioComplete = Math.Min(1, elapsedSeconds / expectedTimeS);
									progressBarText.Text = $"Unloading Filament: {Math.Max(0, expectedTimeS - elapsedSeconds):0}";
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
				unloadingFilamentPage.Closed += (s, e) =>
				{
					UiThread.ClearInterval(runningGCodeCommands);
					if (extruderCount > 1)
					{
						// reset the extruder that was active
						printer.Connection.QueueLine($"T{extruderPriorToUnload}");
					}
					printer.Connection.QueueLine("G92 E0");
				};

				yield return unloadingFilamentPage;
			}

			// put up a success message
			yield return new DoneUnloadingPage(this, extruderIndex);
		}
	}

	public class DoneUnloadingPage : WizardPage
	{
		public DoneUnloadingPage(PrinterSetupWizard setupWizard, int extruderIndex)
			: base(setupWizard, "Filament Unloaded".Localize(), "Success!\n\nYour filament should now be unloaded".Localize())
		{
			var loadFilamentButton = new TextButton("Load Filament".Localize(), theme)
			{
				Name = "Load Filament",
				BackgroundColor = theme.MinimalShade,
			};
			loadFilamentButton.Click += (s, e) =>
			{
				this.DialogWindow.ClosePage();

				DialogWindow.Show(
					new LoadFilamentWizard(printer, extruderIndex, showAlreadyLoadedButton: false));
			};

			this.AcceptButton = loadFilamentButton;
			this.AddPageAction(loadFilamentButton);
		}

		public override void OnLoad(EventArgs args)
		{
			this.ShowWizardFinished();
			base.OnLoad(args);
		}
	}
}
