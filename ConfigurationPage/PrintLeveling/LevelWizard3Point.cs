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
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System.Collections.Generic;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class LevelWizard3Point : LevelWizardBase
	{
		private LevelingStrings levelingStrings;

		public LevelWizard3Point(PrinterConfig printer, LevelWizardBase.RuningState runningState)
			: base(printer, 500, 370, 9)
		{
			levelingStrings = new LevelingStrings(printer.Settings);
			string printLevelWizardTitle = ApplicationController.Instance.ProductName;
			string printLevelWizardTitleFull = "Print Leveling Wizard".Localize();
			Title = string.Format("{0} - {1}", printLevelWizardTitle, printLevelWizardTitleFull);
			List<ProbePosition> probePositions = new List<ProbePosition>(3);
			probePositions.Add(new ProbePosition());
			probePositions.Add(new ProbePosition());
			probePositions.Add(new ProbePosition());

			printLevelWizard = new WizardControl();
			AddChild(printLevelWizard);

			if (runningState == LevelWizardBase.RuningState.InitialStartupCalibration)
			{
				string requiredPageInstructions = "{0}\n\n{1}".FormatWith(levelingStrings.requiredPageInstructions1, levelingStrings.requiredPageInstructions2);
				printLevelWizard.AddPage(new FirstPageInstructions(printer, levelingStrings.initialPrinterSetupStepText, requiredPageInstructions));
			}

			printLevelWizard.AddPage(new FirstPageInstructions(printer, levelingStrings.OverviewText, levelingStrings.WelcomeText(3, 3)));

			// To make sure the bed is at the correct temp, put in a filament selection page.
			bool hasHeatedBed = printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed);
			if (hasHeatedBed)
			{
				string filamentSelectionPage = "{0}\n\n{1}".FormatWith(levelingStrings.materialPageInstructions1, levelingStrings.materialPageInstructions2);
				printLevelWizard.AddPage(new SelectMaterialPage(printer, levelingStrings.materialStepText, filamentSelectionPage));
			}
			printLevelWizard.AddPage(new HomePrinterPage(printer, printLevelWizard, levelingStrings.homingPageStepText, levelingStrings.homingPageInstructions));
			if (hasHeatedBed)
			{
				printLevelWizard.AddPage(new WaitForTempPage(printer, printLevelWizard, levelingStrings));
			}

			string positionLabel = "Position".Localize();
			string autoCalibrateLabel = "Auto Calibrate".Localize();
			string lowPrecisionLabel = "Low Precision".Localize();
			string medPrecisionLabel = "Medium Precision".Localize();
			string highPrecisionLabel = "High Precision".Localize();

			double startProbeHeight = printer.Settings.GetValue<double>(SettingsKey.print_leveling_probe_start);

			for (int i = 0; i < 3; i++)
			{
				Vector2 probePosition = LevelWizardBase.GetPrintLevelPositionToSample(printer.Settings, i);

				if (printer.Settings.Helpers.UseZProbe())
				{
					var stepString = string.Format("{0} {1} {2} {3}:", levelingStrings.stepTextBeg, i + 1, levelingStrings.stepTextEnd, 3);
					printLevelWizard.AddPage(new AutoProbeFeedback(printer, printLevelWizard, new Vector3(probePosition, startProbeHeight), string.Format("{0} {1} {2} - {3}", stepString, positionLabel, i + 1, autoCalibrateLabel), probePositions, i));
				}
				else
				{
					printLevelWizard.AddPage(new GetCoarseBedHeight(printer, printLevelWizard, new Vector3(probePosition, startProbeHeight), string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), positionLabel, i + 1, lowPrecisionLabel), probePositions, i));
					printLevelWizard.AddPage(new GetFineBedHeight(printer, printLevelWizard, string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), positionLabel, i + 1, medPrecisionLabel), probePositions, i));
					printLevelWizard.AddPage(new GetUltraFineBedHeight(printer, printLevelWizard, string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), positionLabel, i + 1, highPrecisionLabel), probePositions, i));
				}
			}

			printLevelWizard.AddPage(new LastPagelInstructions(printer, printLevelWizard, "Done".Localize(), levelingStrings.DoneInstructions, probePositions));
		}

		public static string ApplyLeveling(PrinterSettings printerSettings, string lineBeingSent, Vector3 currentDestination, PrinterMachineInstruction.MovementTypes movementMode)
		{
			if (printerSettings?.GetValue<bool>(SettingsKey.print_leveling_enabled) == true
				&& (lineBeingSent.StartsWith("G0 ") || lineBeingSent.StartsWith("G1 ")))
			{
				lineBeingSent = PrintLevelingPlane.Instance.ApplyLeveling(currentDestination, lineBeingSent);
			}

			return lineBeingSent;
		}

		public static List<string> ProcessCommand(string lineBeingSent)
		{
			int commentIndex = lineBeingSent.IndexOf(';');
			if (commentIndex > 0) // there is content in front of the ;
			{
				lineBeingSent = lineBeingSent.Substring(0, commentIndex).Trim();
			}
			List<string> lines = new List<string>();
			lines.Add(lineBeingSent);
			if (lineBeingSent.StartsWith("G28")
				|| lineBeingSent.StartsWith("G29"))
			{
				lines.Add("M114");
			}

			return lines;
		}
	}
}