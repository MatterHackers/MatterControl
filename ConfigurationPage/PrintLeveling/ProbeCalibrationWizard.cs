/*
Copyright (c) 2014, Lars Brubaker
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
	public class ProbeCalibrationWizard : SystemWindow
	{
		protected WizardControl printLevelWizard;

		protected int totalSteps { get; private set; }
		protected PrinterConfig printer;

		public ProbeCalibrationWizard(PrinterConfig printer, ThemeConfig theme)
			: base(500, 370)
		{
			this.printer = printer;
			AlwaysOnTopOfMain = true;
			this.totalSteps = 3;

			LevelingStrings levelingStrings = new LevelingStrings(printer.Settings);
			string printLevelWizardTitle = ApplicationController.Instance.ProductName;
			string printLevelWizardTitleFull = "Probe Calibration Wizard".Localize();
			Title = string.Format("{0} - {1}", printLevelWizardTitle, printLevelWizardTitleFull);
			List<ProbePosition> autoProbePositions = new List<ProbePosition>(3);
			List<ProbePosition> manualProbePositions = new List<ProbePosition>(3);
			autoProbePositions.Add(new ProbePosition());
			manualProbePositions.Add(new ProbePosition());

			printLevelWizard = new WizardControl();
			AddChild(printLevelWizard);

			if (printer.Settings.GetValue<bool>(SettingsKey.probe_has_been_calibrated))
			{
				string part1 = "Congratulations on connecting to your printer. Before starting your first print we need to run a simple calibration procedure.".Localize();
				string part2 = "The next few screens will walk your through calibrating your printer.".Localize();
				string requiredPageInstructions = $"{part1}\n\n{part2}";
				printLevelWizard.AddPage(new FirstPageInstructions(printer, levelingStrings.initialPrinterSetupStepText, requiredPageInstructions, theme));
			}

			var CalibrateProbeWelcomText = "{0}\n\n\t• {1}\n\t• {2}\n\t• {3}\n\n{4}\n\n{5}".FormatWith(
				"Welcome to the probe calibration wizard. Here is a quick overview on what we are going to do.".Localize(),
				"Home the printer".Localize(),
				"Probe the bed at the center".Localize(),
				"Manually measure the extruder at the center".Localize(),
				"We should be done in less than 1 minute.".Localize(),
				levelingStrings.ClickNext);

			printLevelWizard.AddPage(new FirstPageInstructions(printer,
				"Probe Calibration Overview".Localize(), CalibrateProbeWelcomText, theme));

			printLevelWizard.AddPage(new CleanExtruderInstructionPage(printer, "Check Nozzle".Localize(), levelingStrings.CleanExtruder, theme));

			bool useZProbe = printer.Settings.Helpers.UseZProbe();
			printLevelWizard.AddPage(new HomePrinterPage(printer, printLevelWizard, 
				levelingStrings.HomingPageStepText, 
				levelingStrings.HomingPageInstructions(useZProbe, false),
				false, theme));

			string lowPrecisionLabel = "Low Precision".Localize();
			string medPrecisionLabel = "Medium Precision".Localize();
			string highPrecisionLabel = "High Precision".Localize();

			double startProbeHeight = printer.Settings.GetValue<double>(SettingsKey.print_leveling_probe_start);
			Vector2 probePosition = printer.Settings.GetValue<Vector2>(SettingsKey.print_center);

			int i = 0;
			// do the automatic probing of the center position
			var stepString = $"{"Step".Localize()} {i + 1} {"of".Localize()} 3:";
			printLevelWizard.AddPage(new AutoProbeFeedback(printer, printLevelWizard, 
				new Vector3(probePosition, startProbeHeight), 
				$"{stepString} {"Position".Localize()} {i + 1} - {"Auto Calibrate".Localize()}", 
				autoProbePositions, i, theme));

			// do the manual prob of the same position
			printLevelWizard.AddPage(new GetCoarseBedHeight(printer, printLevelWizard, new Vector3(probePosition, startProbeHeight), 
				string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), "Position".Localize(), i + 1, lowPrecisionLabel), manualProbePositions, i, levelingStrings, theme));
			printLevelWizard.AddPage(new GetFineBedHeight(printer, printLevelWizard, string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), "Position".Localize(), i + 1, medPrecisionLabel), manualProbePositions, i, levelingStrings, theme));
			printLevelWizard.AddPage(new GetUltraFineBedHeight(printer, printLevelWizard, string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), "Position".Localize(), i + 1, highPrecisionLabel), manualProbePositions, i, levelingStrings, theme));

			printLevelWizard.AddPage(new CalibrateProbeLastPagelInstructions(printer, printLevelWizard, 
				"Done".Localize(),
				"Your Probe is now calibrated.".Localize()  + "\n\n\t• " + "Remove the paper".Localize() + "\n\n" + "Click 'Done' to close this window.".Localize(), 
				autoProbePositions, 
				manualProbePositions, theme));
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