/*
Copyright (c) 2018, Lars Brubaker
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

using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class ProbeWizardControl : WizardControl
	{
		protected PrinterConfig printer;
		ThemeConfig theme;

		public ProbeWizardControl(PrinterConfig printer, ThemeConfig theme)
		{
			this.theme = theme;
			this.printer = printer;
		}

		protected override IEnumerator<WizardControlPage> Pages
		{
			get
			{
				LevelingStrings levelingStrings = new LevelingStrings(printer.Settings);
				List<ProbePosition> autoProbePositions = new List<ProbePosition>(3);
				List<ProbePosition> manualProbePositions = new List<ProbePosition>(3);
				autoProbePositions.Add(new ProbePosition());
				manualProbePositions.Add(new ProbePosition());
				int totalSteps = 3;

				this.doneButton.Visible = false;

				// make a welocme page if this is the first time calibrating the probe
				if (!printer.Settings.GetValue<bool>(SettingsKey.probe_has_been_calibrated))
				{
					string part1 = "Congratulations on connecting to your printer. Before starting your first print we need to run a simple calibration procedure.".Localize();
					string part2 = "The next few screens will walk your through calibrating your printer.".Localize();
					string requiredPageInstructions = $"{part1}\n\n{part2}";
					yield return new FirstPageInstructions(printer, levelingStrings.initialPrinterSetupStepText, requiredPageInstructions, theme);
				}

				// show what steps will be taken
				var CalibrateProbeWelcomText = "{0}\n\n\t• {1}\n\t• {2}\n\t• {3}\n\n{4}\n\n{5}".FormatWith(
					"Welcome to the probe calibration wizard. Here is a quick overview on what we are going to do.".Localize(),
					"Home the printer".Localize(),
					"Probe the bed at the center".Localize(),
					"Manually measure the extruder at the center".Localize(),
					"We should be done in less than 1 minute.".Localize(),
					levelingStrings.ClickNext);

				yield return new FirstPageInstructions(printer,
					"Probe Calibration Overview".Localize(), CalibrateProbeWelcomText, theme);

				// add in the material select page
				var instruction1 = "The hot end needs to be heated to ensure it is clean.".Localize();
				var instruction2 = "Please select the material you will be printing, so we can heat the printer before calibrating.".Localize();
				yield return new SelectMaterialPage(printer, "Select Material".Localize(), $"{instruction1}\n\n{instruction2}", theme);

				// add in the homing printer page
				yield return new HomePrinterPage(printer, this,
					levelingStrings.HomingPageStepText,
					levelingStrings.HomingPageInstructions(true, false),
					false, theme);

				string heatingInstructions = "";
				double targetHotendTemp = 0;

				targetHotendTemp = printer.Settings.Helpers.ExtruderTemperature(0);
				heatingInstructions += $"Waiting for the hotend to heat to {targetHotendTemp}.".Localize() + "\n"
					+ "This will ensure no filament is stuck to the tip.".Localize() + "\n"
					+ "\n"
					+ "Warning! The tip of the extrude will be HOT!".Localize() + "\n"
					+ "Avoid contact with your skin.".Localize();

				yield return new WaitForTempPage(printer, this,
					"Waiting For Printer To Heat".Localize(), heatingInstructions,
					0, targetHotendTemp,
					theme);

				string lowPrecisionLabel = "Low Precision".Localize();
				string medPrecisionLabel = "Medium Precision".Localize();
				string highPrecisionLabel = "High Precision".Localize();

				double startProbeHeight = printer.Settings.GetValue<double>(SettingsKey.print_leveling_probe_start);
				Vector2 probePosition = printer.Settings.GetValue<Vector2>(SettingsKey.print_center);

				int i = 0;
				// do the automatic probing of the center position
				var stepString = $"{"Step".Localize()} {i + 1} {"of".Localize()} 3:";
				yield return new AutoProbeFeedback(printer, this,
					new Vector3(probePosition, startProbeHeight),
					$"{stepString} {"Position".Localize()} {i + 1} - {"Auto Calibrate".Localize()}",
					autoProbePositions, i, theme);

				// do the manual prob of the same position
				yield return new GetCoarseBedHeight(printer, this, new Vector3(probePosition, startProbeHeight),
					string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), "Position".Localize(), i + 1, lowPrecisionLabel), manualProbePositions, i, levelingStrings, theme);
				yield return new GetFineBedHeight(printer, this, string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), "Position".Localize(), i + 1, medPrecisionLabel), manualProbePositions, i, levelingStrings, theme);
				yield return new GetUltraFineBedHeight(printer, this, string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), "Position".Localize(), i + 1, highPrecisionLabel), manualProbePositions, i, levelingStrings, theme);

				this.cancelButton.Visible = false;
				this.doneButton.Visible = true;
				yield return new CalibrateProbeLastPagelInstructions(printer, this,
					"Done".Localize(),
					"Your Probe is now calibrated.".Localize() + "\n\n\t• " + "Remove the paper".Localize() + "\n\n" + "Click 'Done' to close this window.".Localize(),
					autoProbePositions,
					manualProbePositions, theme);
			}
		}
	}

	public class ProbeCalibrationWizard : SystemWindow
	{
		protected PrinterConfig printer;

		public ProbeCalibrationWizard(PrinterConfig printer, ThemeConfig theme)
			: base(500, 370)
		{
			this.printer = printer;
			AlwaysOnTopOfMain = true;

			string printLevelWizardTitle = ApplicationController.Instance.ProductName;
			string printLevelWizardTitleFull = "Probe Calibration Wizard".Localize();
			Title = string.Format("{0} - {1}", printLevelWizardTitle, printLevelWizardTitleFull);

			AddChild(new ProbeWizardControl(printer, theme));
		}

		private static SystemWindow probeCalibrationWizardWindow;

		public static bool UsingZProbe(PrinterConfig printer)
		{
			// we have a probe that we are using and we have not done leveling yet
			return printer.Settings.GetValue<bool>(SettingsKey.has_z_probe)
				&& printer.Settings.GetValue<bool>(SettingsKey.use_z_probe);
		}

		public static bool NeedsToBeRun(PrinterConfig printer)
		{
			// we have a probe that we are using and we have not done leveling yet
			return UsingZProbe(printer) && !printer.Settings.GetValue<bool>(SettingsKey.probe_has_been_calibrated);
		}

		public static void ShowProbeCalibrationWizard(PrinterConfig printer, ThemeConfig theme)
		{
			if (probeCalibrationWizardWindow == null)
			{
				// turn off print leveling
				PrintLevelingStream.AllowLeveling = false;

				probeCalibrationWizardWindow = new ProbeCalibrationWizard(printer, theme);

				probeCalibrationWizardWindow.ShowAsSystemWindow();

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
			else
			{
				probeCalibrationWizardWindow.BringToFront();
			}
		}
	}
}