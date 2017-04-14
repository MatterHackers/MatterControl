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

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class LevelWizard3Point : LevelWizardBase
	{
		private LevelingStrings levelingStrings = new LevelingStrings();

		public LevelWizard3Point(LevelWizardBase.RuningState runningState)
			: base(500, 370, 9)
		{
			string printLevelWizardTitle = "MatterControl".Localize();
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
				printLevelWizard.AddPage(new FirstPageInstructions(levelingStrings.initialPrinterSetupStepText, requiredPageInstructions));
			}

			printLevelWizard.AddPage(new FirstPageInstructions(levelingStrings.OverviewText, levelingStrings.WelcomeText(3, 3)));

			printLevelWizard.AddPage(new HomePrinterPage(levelingStrings.homingPageStepText, levelingStrings.homingPageInstructions));

			string positionLabel = "Position".Localize();
			string autoCalibrateLabel = "Auto Calibrate".Localize();
			string lowPrecisionLabel = "Low Precision".Localize();
			string medPrecisionLabel = "Medium Precision".Localize();
			string highPrecisionLabel = "High Precision".Localize();

			bool allowLessThanZero = ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.z_can_be_negative);

			double startProbeHeight = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.print_leveling_probe_start);

			for (int i = 0; i < 3; i++)
			{
				Vector2 probePosition = LevelWizardBase.GetPrintLevelPositionToSample(i);

				if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.use_g30_for_bed_probe))
				{
					var stepString = string.Format("{0} {1} {2} {3}:", levelingStrings.stepTextBeg, i + 1, levelingStrings.stepTextEnd, 3);
					printLevelWizard.AddPage(new AutoProbeFeedback(printLevelWizard, new Vector3(probePosition, startProbeHeight), string.Format("{0} {1} {2} - {3}", stepString, positionLabel, i + 1, autoCalibrateLabel), probePositions, i, allowLessThanZero));
				}
				else
				{
					printLevelWizard.AddPage(new GetCoarseBedHeight(printLevelWizard, new Vector3(probePosition, startProbeHeight), string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), positionLabel, i + 1, lowPrecisionLabel), probePositions, i, allowLessThanZero));
					printLevelWizard.AddPage(new GetFineBedHeight(printLevelWizard, string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), positionLabel, i + 1, medPrecisionLabel), probePositions, i, allowLessThanZero));
					printLevelWizard.AddPage(new GetUltraFineBedHeight(printLevelWizard, string.Format("{0} {1} {2} - {3}", levelingStrings.GetStepString(totalSteps), positionLabel, i + 1, highPrecisionLabel), probePositions, i, allowLessThanZero));
				}
			}

			printLevelWizard.AddPage(new LastPage3PointInstructions(printLevelWizard, "Done".Localize(), levelingStrings.DoneInstructions, probePositions));
		}

		public static string ApplyLeveling(string lineBeingSent, Vector3 currentDestination, PrinterMachineInstruction.MovementTypes movementMode)
		{
			var settings = ActiveSliceSettings.Instance;
			if (settings?.GetValue<bool>(SettingsKey.print_leveling_enabled) == true
				&& (lineBeingSent.StartsWith("G0 ") || lineBeingSent.StartsWith("G1 ")))
			{
				lineBeingSent = PrintLevelingPlane.Instance.ApplyLeveling(currentDestination, movementMode, lineBeingSent);
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