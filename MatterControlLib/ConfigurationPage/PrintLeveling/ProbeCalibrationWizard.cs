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

using MatterHackers.Agg;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class ProbeCalibrationWizard : PrinterSetupWizard
	{
		public ProbeCalibrationWizard(PrinterConfig printer)
			: base(printer)
		{
		}

		public static bool NeedsToBeRun(PrinterConfig printer)
		{
			// we have a probe that we are using and we have not done leveling yet
			return UsingZProbe(printer) && !printer.Settings.GetValue<bool>(SettingsKey.probe_has_been_calibrated);
		}

		public static void Start(PrinterConfig printer, ThemeConfig theme)
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

		public static bool UsingZProbe(PrinterConfig printer)
		{
			var required = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print);

			// we have a probe that we are using and we have not done leveling yet
			return (required || printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled))
				&& printer.Settings.GetValue<bool>(SettingsKey.has_z_probe)
				&& printer.Settings.GetValue<bool>(SettingsKey.use_z_probe);
		}

		protected override IEnumerator<PrinterSetupWizardPage> GetWizardSteps()
		{
			var levelingStrings = new LevelingStrings(printer.Settings);
			var autoProbePositions = new List<ProbePosition>(3);
			var manualProbePositions = new List<ProbePosition>(3);

			autoProbePositions.Add(new ProbePosition());
			manualProbePositions.Add(new ProbePosition());

			int totalSteps = 3;

			// make a welcome page if this is the first time calibrating the probe
			if (!printer.Settings.GetValue<bool>(SettingsKey.probe_has_been_calibrated))
			{
				yield return new PrinterSetupWizardPage(
					this,
					levelingStrings.InitialPrinterSetupStepText,
					string.Format(
						"{0}\n\n{1}",
						"Congratulations on connecting to your printer. Before starting your first print we need to run a simple calibration procedure.".Localize(),
						"The next few screens will walk your through calibrating your printer.".Localize()));
			}

			// show what steps will be taken
			yield return new PrinterSetupWizardPage(
				this,
				"Probe Calibration Overview".Localize(),
				string.Format(
					"{0}\n\n\t• {1}\n\t• {2}\n\t• {3}\n\n{4}\n\n{5}",
					"Welcome to the probe calibration wizard. Here is a quick overview on what we are going to do.".Localize(),
					"Home the printer".Localize(),
					"Probe the bed at the center".Localize(),
					"Manually measure the extruder at the center".Localize(),
					"We should be done in less than five minutes.".Localize(),
					"Click 'Next' to continue.".Localize()));

			// add in the homing printer page
			yield return new HomePrinterPage(
				this,
				"Homing The Printer".Localize(),
				levelingStrings.HomingPageInstructions(true, false),
				false);

			double targetHotendTemp = 0;

			targetHotendTemp = printer.Settings.Helpers.ExtruderTemperature(0);

			if (LevelingValidation.NeedsToBeRun(printer))
			{
				// start heating up the bed as that will be needed next
				var bedTemperature = printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed) ?
					printer.Settings.GetValue<double>(SettingsKey.bed_temperature)
					: 0;
				if (bedTemperature > 0)
				{
					printer.Connection.TargetBedTemperature = bedTemperature;
				}
			}

			yield return new WaitForTempPage(
				this,
				"Waiting For Printer To Heat".Localize(),
				"Waiting for the hotend to heat to ".Localize() + targetHotendTemp + "°C.\n"
					+ "This will ensure that no filament is stuck to your nozzle.".Localize() + "\n"
					+ "\n"
					+ "Warning! The tip of the nozzle will be HOT!".Localize() + "\n"
					+ "Avoid contact with your skin.".Localize(),
				0,
				targetHotendTemp);

			double startProbeHeight = printer.Settings.GetValue<double>(SettingsKey.print_leveling_probe_start);
			Vector2 probePosition = LevelingPlan.ProbeOffsetSamplePosition(printer);

			int i = 0;

			// do the automatic probing of the center position
			yield return new AutoProbeFeedback(
				this,
				new Vector3(probePosition, startProbeHeight),
				$"{"Step".Localize()} {i + 1} {"of".Localize()} 3: {"Position".Localize()} {i + 1} - {"Auto Calibrate".Localize()}",
				autoProbePositions,
				i);

			// show what steps will be taken
			yield return new PrinterSetupWizardPage(
				this,
				"Measure the nozzle offset".Localize(),
				"{0}:\n\n\t• {1}\n\n{2}\n\n{3}".FormatWith(
					"To complete the next few steps you will need".Localize(),
					"A standard sheet of paper".Localize(),
					"We will use this paper to measure the distance between the nozzle and the bed.".Localize(),
					"Click 'Next' to continue.".Localize()));

			// do the manual prob of the same position
			yield return new GetCoarseBedHeight(
				this,
				new Vector3(probePosition, startProbeHeight),
				string.Format(
					"{0} {1} {2} - {3}",
					levelingStrings.GetStepString(totalSteps),
					"Position".Localize(),
					i + 1,
					"Low Precision".Localize()),
				manualProbePositions,
				i,
				levelingStrings);

			yield return new GetFineBedHeight(
				this,
				string.Format(
					"{0} {1} {2} - {3}",
					levelingStrings.GetStepString(totalSteps),
					"Position".Localize(),
					i + 1,
					"Medium Precision".Localize()),
				manualProbePositions,
				i,
				levelingStrings);

			yield return new GetUltraFineBedHeight(
				this,
				string.Format(
					"{0} {1} {2} - {3}",
					levelingStrings.GetStepString(totalSteps),
					"Position".Localize(),
					i + 1,
					"High Precision".Localize()),
				manualProbePositions,
				i,
				levelingStrings);

			yield return new CalibrateProbeLastPagelInstructions(
				this,
				"Done".Localize(),
				autoProbePositions,
				manualProbePositions);
		}
	}
}