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
	public class LevelWizard7PointRadial : LevelWizardBase
	{
		private string pageOneStepText = "Print Leveling Overview".Localize();
		private string pageOneInstructionsTextOne = LocalizedString.Get("Welcome to the print leveling wizard. Here is a quick overview on what we are going to do.");
		private string pageOneInstructionsTextTwo = LocalizedString.Get("'Home' the printer");
		private string pageOneInstructionsTextThree = LocalizedString.Get("Sample the bed at seven points");
		private string pageOneInstructionsTextFour = LocalizedString.Get("Turn auto leveling on");
		private string pageOneInstructionsText5 = LocalizedString.Get("You should be done in about 5 minutes.");
		private string pageOneInstructionsText6 = LocalizedString.Get("Note: Be sure the tip of the extrude is clean.");
		private string pageOneInstructionsText7 = LocalizedString.Get("Click 'Next' to continue.");

		public LevelWizard7PointRadial(LevelWizardBase.RuningState runningState)
			: base(500, 370, 9)
		{
			bool allowLessThanZero = ActiveSliceSettings.Instance.GetActiveValue("z_can_be_negative") == "1";
			string printLevelWizardTitle = LocalizedString.Get("MatterControl");
			string printLevelWizardTitleFull = LocalizedString.Get("Print Leveling Wizard");
			Title = string.Format("{0} - {1}", printLevelWizardTitle, printLevelWizardTitleFull);
			ProbePosition[] probePositions = new ProbePosition[7];
			probePositions[0] = new ProbePosition();
			probePositions[1] = new ProbePosition();
			probePositions[2] = new ProbePosition();
			probePositions[3] = new ProbePosition();
			probePositions[4] = new ProbePosition();
			probePositions[5] = new ProbePosition();
			probePositions[6] = new ProbePosition();

			printLevelWizard = new WizardControl();
			AddChild(printLevelWizard);

			if (runningState == LevelWizardBase.RuningState.InitialStartupCalibration)
			{
				string requiredPageInstructions = "{0}\n\n{1}".FormatWith(requiredPageInstructions1, requiredPageInstructions2);
				printLevelWizard.AddPage(new FirstPageInstructions(initialPrinterSetupStepText, requiredPageInstructions));
			}

			string pageOneInstructions = string.Format("{0}\n\n\t• {1}\n\t• {2}\n\t• {3}\n\n{4}\n\n{5}\n\n{6}", pageOneInstructionsTextOne, pageOneInstructionsTextTwo, pageOneInstructionsTextThree, pageOneInstructionsTextFour, pageOneInstructionsText5, pageOneInstructionsText6, pageOneInstructionsText7);
			printLevelWizard.AddPage(new FirstPageInstructions(pageOneStepText, pageOneInstructions));

			string homingPageInstructions = string.Format("{0}:\n\n\t• {1}\n\n{2}", homingPageInstructionsTextOne, homingPageInstructionsTextTwo, homingPageInstructionsTextThree);
			printLevelWizard.AddPage(new HomePrinterPage(homingPageStepText, homingPageInstructions));

			string positionLabel = LocalizedString.Get("Position");
			string lowPrecisionLabel = LocalizedString.Get("Low Precision");
			string medPrecisionLabel = LocalizedString.Get("Medium Precision");
			string highPrecisionLabel = LocalizedString.Get("High Precision");

			double bedRadius = ActiveSliceSettings.Instance.BedSize.x / 2;

			double startProbeHeight = 5;
			for (int i = 0; i < 7; i++)
			{
				Vector2 probePosition = GetPrintLevelPositionToSample(i, bedRadius);
				printLevelWizard.AddPage(new GetCoarseBedHeight(printLevelWizard, new Vector3(probePosition, startProbeHeight), string.Format("{0} {1} {2} - {3}", GetStepString(), positionLabel, i+1, lowPrecisionLabel), probePositions[i], allowLessThanZero));
				printLevelWizard.AddPage(new GetFineBedHeight(string.Format("{0} {1} {2} - {3}", GetStepString(), positionLabel, i+1, medPrecisionLabel), probePositions[i], allowLessThanZero));
				printLevelWizard.AddPage(new GetUltraFineBedHeight(string.Format("{0} {1} {2} - {3}", GetStepString(), positionLabel, i+1, highPrecisionLabel), probePositions[i], allowLessThanZero));
			}

			string doneInstructions = string.Format("{0}\n\n\t• {1}\n\n{2}", doneInstructionsText, doneInstructionsTextTwo, doneInstructionsTextThree);
			printLevelWizard.AddPage(new LastPage7PointRadialInstructions("Done".Localize(), doneInstructions, probePositions));
		}

		public static Vector2 GetPrintLevelPositionToSample(int index, double radius)
		{
			if(index < 6)
			{
				Vector2 postion = new Vector2(radius, 0);
				postion.Rotate(MathHelper.Tau / 6 * index);
				return postion;
			}
			else
			{
				return new Vector2(0, 0);
			}
		}


		internal static string ApplyLeveling(string lineBeingSent, Vector3 currentDestination, PrinterMachineInstruction.MovementTypes movementMode)
		{
			throw new System.NotImplementedException();
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
			if (lineBeingSent.StartsWith("G28"))
			{
				lines.Add("M114");
			}

			return lines;
		}
	}
}