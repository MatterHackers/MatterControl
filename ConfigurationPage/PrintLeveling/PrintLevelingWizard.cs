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
using MatterHackers.Agg;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class PrintLevelingWizard : LevelingWizard
	{
		private LevelingPlan levelingPlan;

		public PrintLevelingWizard(LevelingPlan levelingPlan, PrinterConfig printer)
			: base (printer)
		{
			this.levelingPlan = levelingPlan;
		}

		protected override IEnumerator<LevelingWizardPage> GetWizardSteps()
		{
			var probePositions = new List<ProbePosition>(levelingPlan.ProbeCount);
			for (int j = 0; j < levelingPlan.ProbeCount; j++)
			{
				probePositions.Add(new ProbePosition());
			}

			var levelingStrings = new LevelingStrings(printer.Settings);

			// If no leveling data has been calculated
			bool showWelcomeScreen = printer.Settings.Helpers.GetPrintLevelingData().SampledPositions.Count == 0
				&& !ProbeCalibrationWizard.UsingZProbe(printer);

			if (showWelcomeScreen)
			{
				yield return new LevelingWizardPage(
					this,
					levelingStrings.InitialPrinterSetupStepText,
					string.Format(
						"{0}\n\n{1}",
						"Congratulations on connecting to your printer. Before starting your first print we need to run a simple calibration procedure.".Localize(),
						"The next few screens will walk your through calibrating your printer.".Localize()));
			}

			// To make sure the bed is at the correct temp, put in a filament selection page.
			bool hasHeatedBed = printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed);
			bool useZProbe = printer.Settings.Helpers.UseZProbe();
			int zProbeSamples = printer.Settings.GetValue<int>(SettingsKey.z_probe_samples);

			var secondsPerManualSpot = 10 * 3;
			var secondsPerAutomaticSpot = 3 * zProbeSamples;
			var secondsToCompleteWizard = levelingPlan.ProbeCount * (useZProbe ? secondsPerAutomaticSpot : secondsPerManualSpot);
			secondsToCompleteWizard += (hasHeatedBed ? 60 * 3 : 0);

			yield return new LevelingWizardPage(
				this,
				"Print Leveling Overview".Localize(),
				levelingStrings.WelcomeText(levelingPlan.ProbeCount, (int)Math.Round(secondsToCompleteWizard / 60.0)));

			// If we need to heat the bed or the extruder, select the current material
			if (hasHeatedBed || !useZProbe)
			{
				yield return new SelectMaterialPage(
					this,
					"Select Material".Localize(),
					"Please select the material you will be printing with, so we can accurately calibrate the printer.".Localize());
			}

			yield return new HomePrinterPage(
				this,
				"Homing The Printer".Localize(),
				levelingStrings.HomingPageInstructions(useZProbe, hasHeatedBed),
				useZProbe);

			// figure out the heating requirements
			double targetBedTemp = 0;
			double targetHotendTemp = 0;
			if (hasHeatedBed)
			{
				targetBedTemp = printer.Settings.GetValue<double>(SettingsKey.bed_temperature);
			}

			if (!useZProbe)
			{
				targetHotendTemp = printer.Settings.Helpers.ExtruderTemperature(0);
			}

			if (targetBedTemp > 0 || targetHotendTemp > 0)
			{
				string heatingInstructions = "";
				if (targetBedTemp > 0 && targetHotendTemp > 0)
				{
					// heating both the bed and the hotend
					heatingInstructions = "Waiting for the bed to heat to ".Localize() + targetBedTemp +"\n"
						+ "and the hotend to heat to ".Localize() + targetHotendTemp + ".\n"
						+ "\n"
						+ "This will improve the accuracy of print leveling".Localize()
						+ "and ensure no filament is stuck to the tip of the extruder.".Localize() + "\n"
						+ "\n"
						+ "Warning! The tip of the nozzle will be HOT!".Localize() + "\n"
						+ "Avoid contact with your skin.".Localize();
				}
				else if (targetBedTemp > 0)
				{
					// only heating the bed
					heatingInstructions = "Waiting for the bed to heat to ".Localize() + targetBedTemp + ".\n"
						+ "This will improve the accuracy of print leveling.".Localize();
				}
				else // targetHotendTemp > 0
				{
					// only heating the hotend
					heatingInstructions += "Waiting for the hotend to heat to ".Localize() + targetHotendTemp + ".\n"
						+ "This will ensure no filament is stuck to the tip.".Localize() + "\n"
						+ "\n"
						+ "Warning! The tip of the nozzle will be HOT!".Localize() + "\n"
						+ "Avoid contact with your skin.".Localize();
				}

				yield return new WaitForTempPage(
					this,
					"Waiting For Printer To Heat".Localize(),
					heatingInstructions,
					targetBedTemp, targetHotendTemp);
			}

			string positionLabel = "Position".Localize();
			string autoCalibrateLabel = "Auto Calibrate".Localize();
			string lowPrecisionLabel = "Low Precision".Localize();
			string medPrecisionLabel = "Medium Precision".Localize();
			string highPrecisionLabel = "High Precision".Localize();

			double bedRadius = Math.Min(printer.Settings.GetValue<Vector2>(SettingsKey.bed_size).X, printer.Settings.GetValue<Vector2>(SettingsKey.bed_size).Y) / 2;

			double startProbeHeight = printer.Settings.GetValue<double>(SettingsKey.print_leveling_probe_start);
			int i = 0;
			foreach (var goalProbePosition in levelingPlan.GetPrintLevelPositionToSample())
			{
				var validProbePosition = EnsureInPrintBounds(printer.Settings, goalProbePosition);

				if (printer.Settings.Helpers.UseZProbe())
				{
					yield return new AutoProbeFeedback(
						this,
						new Vector3(validProbePosition, startProbeHeight),
						string.Format(
							"{0} {1} {2} - {3}",
							$"{"Step".Localize()} {i + 1} {"of".Localize()} {levelingPlan.ProbeCount}:",
							positionLabel,
							i + 1,
							autoCalibrateLabel),
						probePositions,
						i);
				}
				else
				{
					yield return new GetCoarseBedHeight(
						this,
						new Vector3(validProbePosition, startProbeHeight),
						string.Format(
							"{0} {1} {2} - {3}",
							levelingStrings.GetStepString(levelingPlan.TotalSteps),
							positionLabel,
							i + 1,
							lowPrecisionLabel),
						probePositions,
						i,
						levelingStrings);

					yield return new GetFineBedHeight(
						this,
						string.Format(
							"{0} {1} {2} - {3}",
							levelingStrings.GetStepString(levelingPlan.TotalSteps),
							positionLabel,
							i + 1,
							medPrecisionLabel),
						probePositions,
						i,
						levelingStrings);

					yield return new GetUltraFineBedHeight(
						this,
						string.Format(
							"{0} {1} {2} - {3}",
							levelingStrings.GetStepString(levelingPlan.TotalSteps),
							positionLabel,
							i + 1,
							highPrecisionLabel),
						probePositions,
						i,
						levelingStrings);
				}
				i++;
			}

			yield return new LastPageInstructions(
				this,
				"Print Leveling Wizard".Localize(),
				string.Format(
					"{0}! {1}\n\n{2}\n{3}\n\n{4}",
					"Congratulations".Localize(),
					"Print Leveling is now configured and enabled.".Localize(),
					useZProbe ? "" : $"\t• {"Remove the paper".Localize()}\n",
					"If you need to recalibrate the printer in the future, the print leveling controls can be found under: Controls, Calibration".Localize(),
					"Click 'Done' to close this window.".Localize()),
				probePositions);
		}

		private Vector2 EnsureInPrintBounds(PrinterSettings printerSettings, Vector2 probePosition)
		{
			// check that the position is within the printing arrea and if not move it back in
			if (printerSettings.Helpers.UseZProbe())
			{
				var probeOffset = printer.Settings.GetValue<Vector2>(SettingsKey.z_probe_xy_offset);
				var actualNozzlePosition = probePosition - probeOffset;

				// clamp this to the bed bounds
				Vector2 bedSize = printer.Settings.GetValue<Vector2>(SettingsKey.bed_size);
				Vector2 printCenter = printer.Settings.GetValue<Vector2>(SettingsKey.print_center);
				RectangleDouble bedBounds = new RectangleDouble(printCenter - bedSize / 2, printCenter + bedSize / 2);
				Vector2 adjustedPosition = bedBounds.Clamp(actualNozzlePosition);

				// and push it back into the probePosition
				probePosition = adjustedPosition + probeOffset;
			}

			return probePosition;
		}
	}

	// this class is so that it is not passed by value
	public class ProbePosition
	{
		public Vector3 position;
	}
}