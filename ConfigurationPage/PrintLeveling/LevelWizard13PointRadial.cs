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
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Text;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class LevelWizard13PointRadial : LevelWizardBase
	{
		private string pageOneStepText = "Print Leveling Overview".Localize();
		private string pageOneInstructionsTextOne = LocalizedString.Get("Welcome to the print leveling wizard. Here is a quick overview on what we are going to do.");
		private string pageOneInstructionsTextTwo = LocalizedString.Get("'Home' the printer");
		private string pageOneInstructionsTextThree = LocalizedString.Get("Sample the bed at seven points");
		private string pageOneInstructionsTextFour = LocalizedString.Get("Turn auto leveling on");
		private string pageOneInstructionsText5 = LocalizedString.Get("You should be done in about 6 minutes.");
		private string pageOneInstructionsText6 = LocalizedString.Get("Note: Be sure the tip of the extrude is clean.");
		private string pageOneInstructionsText7 = LocalizedString.Get("Click 'Next' to continue.");

		static readonly int numberOfRadialSamples = 12;

		public LevelWizard13PointRadial(LevelWizardBase.RuningState runningState)
			: base(500, 370, (numberOfRadialSamples + 1)*3)
		{
			bool allowLessThanZero = ActiveSliceSettings.Instance.GetActiveValue("z_can_be_negative") == "1";
			string printLevelWizardTitle = LocalizedString.Get("MatterControl");
			string printLevelWizardTitleFull = LocalizedString.Get("Print Leveling Wizard");
			Title = string.Format("{0} - {1}", printLevelWizardTitle, printLevelWizardTitleFull);
			ProbePosition[] probePositions = new ProbePosition[numberOfRadialSamples+1];
			for (int i = 0; i < probePositions.Length; i++)
			{
				probePositions[i] = new ProbePosition();
			}

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

			double bedRadius = Math.Min(ActiveSliceSettings.Instance.BedSize.x, ActiveSliceSettings.Instance.BedSize.y) / 2;

			double startProbeHeight = 5;
			for (int i = 0; i < numberOfRadialSamples+1; i++)
			{
				Vector2 probePosition = GetPrintLevelPositionToSample(i, bedRadius);
				printLevelWizard.AddPage(new GetCoarseBedHeight(printLevelWizard, new Vector3(probePosition, startProbeHeight), string.Format("{0} {1} {2} - {3}", GetStepString(), positionLabel, i + 1, lowPrecisionLabel), probePositions[i], allowLessThanZero));
				printLevelWizard.AddPage(new GetFineBedHeight(string.Format("{0} {1} {2} - {3}", GetStepString(), positionLabel, i + 1, medPrecisionLabel), probePositions[i], allowLessThanZero));
				printLevelWizard.AddPage(new GetUltraFineBedHeight(string.Format("{0} {1} {2} - {3}", GetStepString(), positionLabel, i + 1, highPrecisionLabel), probePositions[i], allowLessThanZero));
			}

			string doneInstructions = string.Format("{0}\n\n\t• {1}\n\n{2}", doneInstructionsText, doneInstructionsTextTwo, doneInstructionsTextThree);
			printLevelWizard.AddPage(new LastPageRadialInstructions("Done".Localize(), doneInstructions, probePositions));
		}

		public static Vector2 GetPrintLevelPositionToSample(int index, double radius)
		{
			if (index < numberOfRadialSamples)
			{
				Vector2 position = new Vector2(radius, 0);
				position.Rotate(MathHelper.Tau / numberOfRadialSamples * index);
				position += ActiveSliceSettings.Instance.BedCenter;
				return position;
			}
			else
			{
				return new Vector2(0, 0);
			}
		}

		public static string ApplyLeveling(string lineBeingSent, Vector3 currentDestination, PrinterMachineInstruction.MovementTypes movementMode)
		{
			if (PrinterConnectionAndCommunication.Instance.ActivePrinter != null
				&& PrinterConnectionAndCommunication.Instance.ActivePrinter.DoPrintLeveling
				&& (lineBeingSent.StartsWith("G0 ") || lineBeingSent.StartsWith("G1 "))
				&& lineBeingSent.Length > 2
				&& lineBeingSent[2] == ' ')
			{
				double extruderDelta = 0;
				GCodeFile.GetFirstNumberAfter("E", lineBeingSent, ref extruderDelta);
				double feedRate = 0;
				GCodeFile.GetFirstNumberAfter("F", lineBeingSent, ref feedRate);

				StringBuilder newLine = new StringBuilder("G1 ");

				if (lineBeingSent.Contains("X") || lineBeingSent.Contains("Y") || lineBeingSent.Contains("Z"))
				{
					PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(ActivePrinterProfile.Instance.ActivePrinter);

					Vector3 outPosition = GetPositionWithZOffset(currentDestination, levelingData);
		
					if (movementMode == PrinterMachineInstruction.MovementTypes.Relative)
					{
						// TODO: this is not correct for 13 point leveling
						Vector3 relativeMove = Vector3.Zero;
						GCodeFile.GetFirstNumberAfter("X", lineBeingSent, ref relativeMove.x);
						GCodeFile.GetFirstNumberAfter("Y", lineBeingSent, ref relativeMove.y);
						GCodeFile.GetFirstNumberAfter("Z", lineBeingSent, ref relativeMove.z);
						outPosition = PrintLevelingPlane.Instance.ApplyLevelingRotation(relativeMove);
					}

					newLine = newLine.Append(String.Format("X{0:0.##} Y{1:0.##} Z{2:0.###}", outPosition.x, outPosition.y, outPosition.z));
				}

				if (extruderDelta != 0)
				{
					newLine = newLine.Append(String.Format(" E{0:0.###}", extruderDelta));
				}

				if (feedRate != 0)
				{
					newLine = newLine.Append(String.Format(" F{0:0.##}", feedRate));
				}

				lineBeingSent = newLine.ToString();

				return lineBeingSent;
			}

			return lineBeingSent;
		}

		public static Vector3 GetPositionWithZOffset(Vector3 currentDestination, PrintLevelingData levelingData)
		{
			Vector2 destinationFromCenter = new Vector2(currentDestination) - ActiveSliceSettings.Instance.BedCenter;

			double angleToPoint = Math.Atan2(destinationFromCenter.y, destinationFromCenter.x);

			if (angleToPoint < 0)
			{
				angleToPoint += MathHelper.Tau;
			}

			double oneSegmentAngle = MathHelper.Tau / numberOfRadialSamples;
			int firstIndex = (int)(angleToPoint / oneSegmentAngle);
			int lastIndex = firstIndex + 1;
			if (lastIndex == numberOfRadialSamples)
			{
				lastIndex = 0;
			}

			Plane currentPlane = new Plane(levelingData.SampledPositions[firstIndex], levelingData.SampledPositions[lastIndex], levelingData.SampledPositions[numberOfRadialSamples]);

			double hitDistance = currentPlane.GetDistanceToIntersection(new Vector3(currentDestination.x, currentDestination.y, 0), Vector3.UnitZ);

			currentDestination.z += hitDistance;
			return currentDestination;
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