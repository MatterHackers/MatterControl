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

		public LevelWizardBase(PrinterConfig printer, RuningState runningState)
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

			if (runningState == LevelWizardBase.RuningState.InitialStartupCalibration)
			{
				string requiredPageInstructions = "{0}\n\n{1}".FormatWith(levelingStrings.requiredPageInstructions1, levelingStrings.requiredPageInstructions2);
				printLevelWizard.AddPage(new FirstPageInstructions(printer, levelingStrings.initialPrinterSetupStepText, requiredPageInstructions));
			}

			printLevelWizard.AddPage(new FirstPageInstructions(printer, levelingStrings.OverviewText, levelingStrings.WelcomeText(ProbeCount, 5)));

			bool useZProbe = printer.Settings.Helpers.UseZProbe();
			if (!useZProbe)
			{
				printLevelWizard.AddPage(new CleanExtruderInstructionPage(printer, "Check Nozzle".Localize(), levelingStrings.CleanExtruder));
			}

			var printerSettings = printer.Settings;

			// To make sure the bed is at the correct temp, put in a filament selection page.
			bool hasHeatedBed = printerSettings.GetValue<bool>(SettingsKey.has_heated_bed);
			if (hasHeatedBed)
			{
				string filamentSelectionPage = "{0}\n\n{1}".FormatWith(levelingStrings.materialPageInstructions1, levelingStrings.materialPageInstructions2);
				printLevelWizard.AddPage(new SelectMaterialPage(printer, levelingStrings.materialStepText, filamentSelectionPage));
			}
			printLevelWizard.AddPage(new HomePrinterPage(printer, printLevelWizard,
				levelingStrings.HomingPageStepText,
				levelingStrings.HomingPageInstructions(useZProbe),
				useZProbe));
			if (hasHeatedBed)
			{
				printLevelWizard.AddPage(new WaitForTempPage(printer, printLevelWizard, levelingStrings));
			}

			string positionLabel = "Position".Localize();
			string autoCalibrateLabel = "Auto Calibrate".Localize();
			string lowPrecisionLabel = "Low Precision".Localize();
			string medPrecisionLabel = "Medium Precision".Localize();
			string highPrecisionLabel = "High Precision".Localize();

			double bedRadius = Math.Min(printerSettings.GetValue<Vector2>(SettingsKey.bed_size).X, printerSettings.GetValue<Vector2>(SettingsKey.bed_size).Y) / 2;

			double startProbeHeight = printerSettings.GetValue<double>(SettingsKey.print_leveling_probe_start);
			int i = 0;
			foreach (var goalProbePosition in GetPrintLevelPositionToSample())
			{
				var validProbePosition = EnsureInPrintBounds(printerSettings, goalProbePosition);

				if (printerSettings.Helpers.UseZProbe())
				{
					var stepString = string.Format("{0} {1} {2} {3}:", levelingStrings.stepTextBeg, i + 1, levelingStrings.stepTextEnd, ProbeCount);
					printLevelWizard.AddPage(new AutoProbeFeedback(printer, printLevelWizard, new Vector3(validProbePosition, startProbeHeight), string.Format("{0} {1} {2} - {3}", stepString, positionLabel, i + 1, autoCalibrateLabel), probePositions, i));
				}
				else
				{
					printLevelWizard.AddPage(new GetCoarseBedHeight(printer, printLevelWizard, new Vector3(validProbePosition, startProbeHeight), string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), positionLabel, i + 1, lowPrecisionLabel), probePositions, i, levelingStrings));
					printLevelWizard.AddPage(new GetFineBedHeight(printer, printLevelWizard, string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), positionLabel, i + 1, medPrecisionLabel), probePositions, i, levelingStrings));
					printLevelWizard.AddPage(new GetUltraFineBedHeight(printer, printLevelWizard, string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), positionLabel, i + 1, highPrecisionLabel), probePositions, i, levelingStrings));
				}
				i++;
			}

			printLevelWizard.AddPage(new LastPagelInstructions(printer, printLevelWizard, "Done".Localize(), levelingStrings.DoneInstructions, probePositions));
		}

		public enum RuningState { InitialStartupCalibration, UserRequestedCalibration }

		public abstract int ProbeCount { get; }
		public int TotalSteps => ProbeCount * 3;
		protected int totalSteps { get; private set; }

		public static void ShowPrintLevelWizard(PrinterConfig printer)
		{
			LevelWizardBase.RuningState runningState = LevelWizardBase.RuningState.UserRequestedCalibration;

			if (printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print))
			{
				// run in the first run state
				runningState = LevelWizardBase.RuningState.InitialStartupCalibration;
			}

			ShowPrintLevelWizard(printer, runningState);
		}

		public static void ShowPrintLevelWizard(PrinterConfig printer, LevelWizardBase.RuningState runningState)
		{
			if (printLevelWizardWindow == null)
			{
				printLevelWizardWindow = LevelWizardBase.CreateAndShowWizard(printer, runningState);
				printLevelWizardWindow.Closed += (sender, e) =>
				{
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

		private static LevelWizardBase CreateAndShowWizard(PrinterConfig printer, LevelWizardBase.RuningState runningState)
		{
			// turn off print leveling
			printer.Settings.Helpers.DoPrintLeveling(false);
			// clear any data that we are going to be acquiring (sampled positions, after z home offset)
			PrintLevelingData levelingData = printer.Settings.Helpers.GetPrintLevelingData();
			levelingData.SampledPositions.Clear();
			printer.Settings.SetValue(SettingsKey.baby_step_z_offset, "0");

			LevelWizardBase printLevelWizardWindow;
			switch (levelingData.LevelingSystem)
			{
				case LevelingSystem.Probe3Points:
					printLevelWizardWindow = new LevelWizard3Point(printer, runningState);
					break;

				case LevelingSystem.Probe7PointRadial:
					printLevelWizardWindow = new LevelWizard7PointRadial(printer, runningState);
					break;

				case LevelingSystem.Probe13PointRadial:
					printLevelWizardWindow = new LevelWizard13PointRadial(printer, runningState);
					break;

				case LevelingSystem.Probe3x3Mesh:
					printLevelWizardWindow = new LevelWizard3x3Mesh(printer, runningState);
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
				var actualNozzlePosition = probePosition + probeOffset;
				// clamp this to the bed bounds

				// and push it back into the probePosition
				probePosition = actualNozzlePosition - probeOffset;
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