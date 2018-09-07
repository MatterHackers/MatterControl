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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public abstract class LevelingWizard
	{
		private IEnumerator<LevelingWizardPage> pages;

		protected abstract IEnumerator<LevelingWizardPage> GetWizardSteps();

		public string WindowTitle { get; internal set; }

		protected PrinterConfig printer;

		public PrinterConfig Printer => printer;

		public LevelingWizard(PrinterConfig printer)
		{
			this.printer = printer;
			this.pages = this.GetWizardSteps();
		}

		public void ShowNextPage(DialogWindow dialogWindow)
		{
			UiThread.RunOnIdle(() =>
			{
				// Shutdown active page
				pages.Current?.PageIsBecomingInactive();
				pages.Current?.Close();

				// Advance
				pages.MoveNext();

				pages.Current?.PageIsBecomingActive();

				dialogWindow.ChangeToPage(pages.Current);
			});
		}

		public static void ShowPrintLevelWizard(PrinterConfig printer, ThemeConfig theme)
		{
			// turn off print leveling
			PrintLevelingStream.AllowLeveling = false;

			// clear any data that we are going to be acquiring (sampled positions, after z home offset)
			var levelingData = new PrintLevelingData()
			{
				LevelingSystem = printer.Settings.GetValue<LevelingSystem>(SettingsKey.print_leveling_solution)
			};

			printer.Settings.SetValue(SettingsKey.baby_step_z_offset, "0");

			LevelingPlan levelingPlan;

			switch (levelingData.LevelingSystem)
			{
				case LevelingSystem.Probe3Points:
					levelingPlan = new LevelWizard3Point(printer);
					break;

				case LevelingSystem.Probe7PointRadial:
					levelingPlan = new LevelWizard7PointRadial(printer);
					break;

				case LevelingSystem.Probe13PointRadial:
					levelingPlan = new LevelWizard13PointRadial(printer);
					break;

				case LevelingSystem.Probe100PointRadial:
					levelingPlan = new LevelWizard100PointRadial(printer);
					break;

				case LevelingSystem.Probe3x3Mesh:
					levelingPlan = new LevelWizardMesh(printer, 3, 3);
					break;

				case LevelingSystem.Probe5x5Mesh:
					levelingPlan = new LevelWizardMesh(printer, 5, 5);
					break;

				case LevelingSystem.Probe10x10Mesh:
					levelingPlan = new LevelWizardMesh(printer, 10, 10);
					break;

				case LevelingSystem.ProbeCustom:
					levelingPlan = new LevelWizardCustom(printer);
					break;

				default:
					throw new NotImplementedException();
			}

			var levelingContext = new PrintLevelingWizard(levelingPlan, printer)
			{
				WindowTitle = $"{ApplicationController.Instance.ProductName} - " + "Print Leveling Wizard".Localize()
			};

			var printLevelWizardWindow = DialogWindow.Show(new LevelingWizardRootPage(levelingContext)
			{
				WindowTitle = levelingContext.WindowTitle
			});

			printLevelWizardWindow.Closed += (s, e) =>
			{
				// If leveling was on when we started, make sure it is on when we are done.
				PrintLevelingStream.AllowLeveling = true;

				printLevelWizardWindow = null;

				// make sure we raise the probe on close
				if (printer.Settings.GetValue<bool>(SettingsKey.has_z_probe)
					&& printer.Settings.GetValue<bool>(SettingsKey.use_z_probe)
					&& printer.Settings.GetValue<bool>(SettingsKey.has_z_servo))
				{
					// make sure the servo is retracted
					var servoRetract = printer.Settings.GetValue<double>(SettingsKey.z_servo_retracted_angle);
					printer.Connection.QueueLine($"M280 P0 S{servoRetract}");
				}
			};
		}

		public static void ShowProbeCalibrationWizard(PrinterConfig printer, ThemeConfig theme)
		{
			// turn off print leveling
			PrintLevelingStream.AllowLeveling = false;

			var levelingContext = new ProbeCalibrationWizard(printer)
			{
				WindowTitle = $"{ApplicationController.Instance.ProductName} - " + "Probe Calibration Wizard".Localize()
			};

			var probeCalibrationWizardWindow = DialogWindow.Show(new LevelingWizardRootPage(levelingContext)
			{
				WindowTitle = levelingContext.WindowTitle
			});
			probeCalibrationWizardWindow.Closed += (s, e) =>
			{
				// If leveling was on when we started, make sure it is on when we are done.
				PrintLevelingStream.AllowLeveling = true;

				probeCalibrationWizardWindow = null;

				// make sure we raise the probe on close
				if (printer.Settings.GetValue<bool>(SettingsKey.has_z_probe)
					&& printer.Settings.GetValue<bool>(SettingsKey.use_z_probe)
					&& printer.Settings.GetValue<bool>(SettingsKey.has_z_servo))
				{
					// make sure the servo is retracted
					var servoRetract = printer.Settings.GetValue<double>(SettingsKey.z_servo_retracted_angle);
					printer.Connection.QueueLine($"M280 P0 S{servoRetract}");
				}
			};
		}
	}
}