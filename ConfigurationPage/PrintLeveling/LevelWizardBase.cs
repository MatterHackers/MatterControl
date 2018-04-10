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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public abstract class LevelWizardBase : SystemWindow
	{
		protected PrinterConfig printer;
		protected WizardControl printLevelWizard;
		private static SystemWindow printLevelWizardWindow;
		private LevelingStrings levelingStrings;

		public LevelWizardBase(PrinterConfig printer, ThemeConfig theme)
			: base(500, 370)
		{
			levelingStrings = new LevelingStrings(printer.Settings);
			this.printer = printer;
			AlwaysOnTopOfMain = true;

			levelingStrings = new LevelingStrings(printer.Settings);
			string printLevelWizardTitle = ApplicationController.Instance.ProductName;
			string printLevelWizardTitleFull = "Print Leveling Wizard".Localize();
			Title = string.Format("{0} - {1}", printLevelWizardTitle, printLevelWizardTitleFull);
			List<ProbePosition> probePositions = new List<ProbePosition>(ProbeCount);
			for (int j = 0; j < ProbeCount; j++)
			{
				probePositions.Add(new ProbePosition());
			}

			printLevelWizard = new WizardControl();
			AddChild(printLevelWizard);

			// If no leveling data has been calculated
			bool showWelcomeScreen = printer.Settings.Helpers.GetPrintLevelingData().SampledPositions.Count == 0
				&& !ProbeCalibrationWizard.UsingZProbe(printer);

			if (showWelcomeScreen)
			{
				string part1 = "Congratulations on connecting to your printer. Before starting your first print we need to run a simple calibration procedure.".Localize();
				string part2 = "The next few screens will walk your through calibrating your printer.".Localize();
				string requiredPageInstructions = $"{part1}\n\n{part2}";
				printLevelWizard.AddPage(new FirstPageInstructions(printer, levelingStrings.initialPrinterSetupStepText, requiredPageInstructions, theme));
			}

			// To make sure the bed is at the correct temp, put in a filament selection page.
			bool hasHeatedBed = printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed);
			bool useZProbe = printer.Settings.Helpers.UseZProbe();
			int zProbeSamples = printer.Settings.GetValue<int>(SettingsKey.z_probe_samples);

			var secondsPerManualSpot = 10 * 3;
			var secondsPerAutomaticSpot = 3 * zProbeSamples;
			var secondsToCompleteWizard = ProbeCount * (useZProbe ? secondsPerAutomaticSpot : secondsPerManualSpot);
			secondsToCompleteWizard += (hasHeatedBed ? 60 * 3 : 0);
			printLevelWizard.AddPage(new FirstPageInstructions(printer,
				"Print Leveling Overview".Localize(),
				levelingStrings.WelcomeText(ProbeCount, (int)Math.Round(secondsToCompleteWizard / 60.0)), theme));

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

			// If we need to heat the bed or the extruder, select the current material
			if (targetBedTemp > 0 || targetHotendTemp > 0)
			{
				var instruction1 = "";
				if (targetBedTemp > 0 && targetHotendTemp > 0)
				{
					// heating both the bed and the hotend
					instruction1 = "To ensure accurate calibration both the bed and the hotend need to be heated.".Localize();
				}
				else if (targetBedTemp > 0)
				{
					// only heating the bed
					instruction1 = "The temperature of the bed can have a significant effect on the quality of leveling.".Localize();
				}
				else // targetHotendTemp > 0
				{
					// only heating the hotend
					instruction1 += "The hot end needs to be heated to ensure it is clean.".Localize();
				}

				var instruction2 = "Please select the material you will be printing, so we can heat the printer before calibrating.".Localize();

				printLevelWizard.AddPage(new SelectMaterialPage(printer, "Select Material".Localize(), $"{instruction1}\n\n{instruction2}", theme));
			}

			printLevelWizard.AddPage(new HomePrinterPage(printer, printLevelWizard,
				levelingStrings.HomingPageStepText,
				levelingStrings.HomingPageInstructions(useZProbe, hasHeatedBed),
				useZProbe, theme));

			if (targetBedTemp > 0 || targetHotendTemp > 0)
			{
				string heatingInstructions = "";
				if (targetBedTemp > 0 && targetHotendTemp > 0)
				{
					// heating both the bed and the hotend
					heatingInstructions = $"Waiting for the bed to heat to {targetBedTemp}".Localize() + "\n"
						+ $"and the hotend to heat to {targetHotendTemp}.".Localize() + "\n"
						+ "\n"
						+ "This will improve the accuracy of print leveling".Localize()
						+ "and ensure no filament is stuck to the tip of the extruder.".Localize() + "\n"
						+ "\n"
						+ "Warning! The tip of the extrude will be HOT!".Localize() + "\n"
						+ "Avoid contact with your skin.".Localize();
				}
				else if (targetBedTemp > 0)
				{
					// only heating the bed
					heatingInstructions = $"Waiting for the bed to heat to {targetBedTemp}.".Localize() + "\n"
						+ "This will improve the accuracy of print leveling.".Localize();
				}
				else // targetHotendTemp > 0
				{
					// only heating the hotend
					heatingInstructions += $"Waiting for the hotend to heat to {targetHotendTemp}.".Localize() + "\n"
						+ "This will ensure no filament is stuck to the tip.".Localize() + "\n"
						+ "\n"
						+ "Warning! The tip of the extrude will be HOT!".Localize() + "\n"
						+ "Avoid contact with your skin.".Localize();
				}

				printLevelWizard.AddPage(new WaitForTempPage(printer, printLevelWizard,
					"Waiting For Printer To Heat".Localize(), heatingInstructions,
					targetBedTemp, targetHotendTemp,
					theme));
			}

			string positionLabel = "Position".Localize();
			string autoCalibrateLabel = "Auto Calibrate".Localize();
			string lowPrecisionLabel = "Low Precision".Localize();
			string medPrecisionLabel = "Medium Precision".Localize();
			string highPrecisionLabel = "High Precision".Localize();

			double bedRadius = Math.Min(printer.Settings.GetValue<Vector2>(SettingsKey.bed_size).X, printer.Settings.GetValue<Vector2>(SettingsKey.bed_size).Y) / 2;

			double startProbeHeight = printer.Settings.GetValue<double>(SettingsKey.print_leveling_probe_start);
			int i = 0;
			foreach (var goalProbePosition in GetPrintLevelPositionToSample())
			{
				var validProbePosition = EnsureInPrintBounds(printer.Settings, goalProbePosition);

				if (printer.Settings.Helpers.UseZProbe())
				{
					var stepString = $"{"Step".Localize()} {i + 1} {"of".Localize()} {ProbeCount}:";
					printLevelWizard.AddPage(new AutoProbeFeedback(printer, printLevelWizard, new Vector3(validProbePosition, startProbeHeight), string.Format("{0} {1} {2} - {3}", stepString, positionLabel, i + 1, autoCalibrateLabel), probePositions, i, theme));
				}
				else
				{
					printLevelWizard.AddPage(new GetCoarseBedHeight(printer, printLevelWizard, new Vector3(validProbePosition, startProbeHeight), string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(TotalSteps), positionLabel, i + 1, lowPrecisionLabel), probePositions, i, levelingStrings, theme));
					printLevelWizard.AddPage(new GetFineBedHeight(printer, printLevelWizard, string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(TotalSteps), positionLabel, i + 1, medPrecisionLabel), probePositions, i, levelingStrings, theme));
					printLevelWizard.AddPage(new GetUltraFineBedHeight(printer, printLevelWizard, string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(TotalSteps), positionLabel, i + 1, highPrecisionLabel), probePositions, i, levelingStrings, theme));
				}
				i++;
			}

			printLevelWizard.AddPage(new LastPagelInstructions(printer, printLevelWizard, "Done".Localize(), levelingStrings.DoneInstructions, probePositions, theme));
		}

		public abstract int ProbeCount { get; }
		public int TotalSteps => ProbeCount * 3;

		public static void ShowPrintLevelWizard(PrinterConfig printer, ThemeConfig theme)
		{
			if (printLevelWizardWindow == null)
			{
				// turn off print leveling
				PrintLevelingStream.AllowLeveling = false;

				printLevelWizardWindow = LevelWizardBase.CreateAndShowWizard(printer, theme);

				printLevelWizardWindow.Closed += (sender, e) =>
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
			else
			{
				printLevelWizardWindow.BringToFront();
			}
		}

		public abstract IEnumerable<Vector2> GetPrintLevelPositionToSample();

		private static LevelWizardBase CreateAndShowWizard(PrinterConfig printer, ThemeConfig theme)
		{
			// clear any data that we are going to be acquiring (sampled positions, after z home offset)
			PrintLevelingData levelingData = new PrintLevelingData()
			{
				LevelingSystem = printer.Settings.GetValue<LevelingSystem>(SettingsKey.print_leveling_solution)
			};

			printer.Settings.SetValue(SettingsKey.baby_step_z_offset, "0");

			LevelWizardBase printLevelWizardWindow;
			switch (levelingData.LevelingSystem)
			{
				case LevelingSystem.Probe3Points:
					printLevelWizardWindow = new LevelWizard3Point(printer, theme);
					break;

				case LevelingSystem.Probe7PointRadial:
					printLevelWizardWindow = new LevelWizard7PointRadial(printer, theme);
					break;

				case LevelingSystem.Probe13PointRadial:
					printLevelWizardWindow = new LevelWizard13PointRadial(printer, theme);
					break;

				case LevelingSystem.Probe3x3Mesh:
					printLevelWizardWindow = new LevelWizard3x3Mesh(printer, theme);
					break;

				case LevelingSystem.Probe5x5Mesh:
					printLevelWizardWindow = new LevelWizard5x5Mesh(printer, theme);
					break;

				default:
					throw new NotImplementedException();
			}

			printLevelWizardWindow.ShowAsSystemWindow();
			return printLevelWizardWindow;
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
				RectangleDouble bedBounds = new RectangleDouble(printCenter - bedSize/2, printCenter + bedSize/2);
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