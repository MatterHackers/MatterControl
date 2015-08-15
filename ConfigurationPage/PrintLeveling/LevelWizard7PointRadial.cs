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
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Text;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
    public class RadialLevlingFunctions
    {
        public int NumberOfRadialSamples { get; set; }
        public PrintLevelingData LevelingData
        {
            get; set;
        }
        public Vector2 BedCenter
        {
            get; set;
        }

        Vector3 lastDestinationWithLevelingApplied = new Vector3();

        public RadialLevlingFunctions(int numberOfRadialSamples, PrintLevelingData levelingData, Vector2 bedCenter)
        {
            this.LevelingData = levelingData;
            this.BedCenter = bedCenter;
            this.NumberOfRadialSamples = numberOfRadialSamples;
        }

        public Vector2 GetPrintLevelPositionToSample(int index, double radius)
        {
            Vector2 bedCenter = ActiveSliceSettings.Instance.BedCenter;
            if (index < NumberOfRadialSamples)
            {
                Vector2 position = new Vector2(radius, 0);
                position.Rotate(MathHelper.Tau / NumberOfRadialSamples * index);
                position += bedCenter;
                return position;
            }
            else
            {
                return bedCenter;
            }
        }

        public Vector3 GetPositionWithZOffset(Vector3 currentDestination)
        {
            Vector2 destinationFromCenter = new Vector2(currentDestination) - BedCenter;

            double angleToPoint = Math.Atan2(destinationFromCenter.y, destinationFromCenter.x);

            if (angleToPoint < 0)
            {
                angleToPoint += MathHelper.Tau;
            }

            double oneSegmentAngle = MathHelper.Tau / NumberOfRadialSamples;
            int firstIndex = (int)(angleToPoint / oneSegmentAngle);
            int lastIndex = firstIndex + 1;
            if (lastIndex == NumberOfRadialSamples)
            {
                lastIndex = 0;
            }

            Plane currentPlane = new Plane(LevelingData.SampledPositions[firstIndex], LevelingData.SampledPositions[lastIndex], LevelingData.SampledPositions[NumberOfRadialSamples]);

            double hitDistance = currentPlane.GetDistanceToIntersection(new Vector3(currentDestination.x, currentDestination.y, 0), Vector3.UnitZ);

            currentDestination.z += hitDistance;
            return currentDestination;
        }

        public string DoApplyLeveling(string lineBeingSent, Vector3 currentDestination,
            PrinterMachineInstruction.MovementTypes movementMode)
        {
            double extruderDelta = 0;
            GCodeFile.GetFirstNumberAfter("E", lineBeingSent, ref extruderDelta);
            double feedRate = 0;
            GCodeFile.GetFirstNumberAfter("F", lineBeingSent, ref feedRate);

            StringBuilder newLine = new StringBuilder("G1 ");

            if (lineBeingSent.Contains("X") || lineBeingSent.Contains("Y") || lineBeingSent.Contains("Z"))
            {
                Vector3 outPosition = GetPositionWithZOffset(currentDestination);

				if (movementMode == PrinterMachineInstruction.MovementTypes.Relative)
				{
					Vector3 delta = outPosition - lastDestinationWithLevelingApplied;
					lastDestinationWithLevelingApplied = outPosition;
					outPosition -= delta;
				}
				else
				{
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
    }

    public abstract class LevelWizardRadialBase : LevelWizardBase
    {
        protected string pageOneStepText = "Print Leveling Overview".Localize();
        protected string pageOneInstructionsTextOne = LocalizedString.Get("Welcome to the print leveling wizard. Here is a quick overview on what we are going to do.");
        protected string pageOneInstructionsTextTwo = LocalizedString.Get("'Home' the printer");
        protected string pageOneInstructionsTextThree = LocalizedString.Get("Sample the bed at seven points");
        protected string pageOneInstructionsTextFour = LocalizedString.Get("Turn auto leveling on");
        protected string pageOneInstructionsText5 = LocalizedString.Get("You should be done in about 5 minutes.");
        protected string pageOneInstructionsText6 = LocalizedString.Get("Note: Be sure the tip of the extrude is clean.");
        protected string pageOneInstructionsText7 = LocalizedString.Get("Click 'Next' to continue.");

        public LevelWizardRadialBase(LevelWizardBase.RuningState runningState, int width, int height, int totalSteps, int numberOfRadialSamples)
			: base(width, height, totalSteps)
		{
            bool allowLessThanZero = ActiveSliceSettings.Instance.GetActiveValue("z_can_be_negative") == "1";
            string printLevelWizardTitle = LocalizedString.Get("MatterControl");
            string printLevelWizardTitleFull = LocalizedString.Get("Print Leveling Wizard");
            Title = string.Format("{0} - {1}", printLevelWizardTitle, printLevelWizardTitleFull);
            ProbePosition[] probePositions = new ProbePosition[numberOfRadialSamples + 1];
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
            for (int i = 0; i < numberOfRadialSamples + 1; i++)
            {
                Vector2 probePosition = GetPrintLevelPositionToSample(i, bedRadius);
                printLevelWizard.AddPage(new GetCoarseBedHeight(printLevelWizard, new Vector3(probePosition, startProbeHeight), string.Format("{0} {1} {2} - {3}", GetStepString(), positionLabel, i + 1, lowPrecisionLabel), probePositions[i], allowLessThanZero));
                printLevelWizard.AddPage(new GetFineBedHeight(string.Format("{0} {1} {2} - {3}", GetStepString(), positionLabel, i + 1, medPrecisionLabel), probePositions[i], allowLessThanZero));
                printLevelWizard.AddPage(new GetUltraFineBedHeight(string.Format("{0} {1} {2} - {3}", GetStepString(), positionLabel, i + 1, highPrecisionLabel), probePositions[i], allowLessThanZero));
            }

            string doneInstructions = string.Format("{0}\n\n\t• {1}\n\n{2}", doneInstructionsText, doneInstructionsTextTwo, doneInstructionsTextThree);
            printLevelWizard.AddPage(new LastPageRadialInstructions("Done".Localize(), doneInstructions, probePositions));
        }

        static RadialLevlingFunctions currentLevelingFunctions = null;
        public static RadialLevlingFunctions GetLevelingFunctions(int numberOfRadialSamples, PrintLevelingData levelingData, Vector2 bedCenter)
        {
            if (currentLevelingFunctions == null
                || currentLevelingFunctions.NumberOfRadialSamples != numberOfRadialSamples
                || currentLevelingFunctions.BedCenter != bedCenter
                || currentLevelingFunctions.LevelingData != levelingData)
            {
                currentLevelingFunctions = new RadialLevlingFunctions(numberOfRadialSamples, levelingData, bedCenter);
            }

            return currentLevelingFunctions;
        }

        public abstract Vector2 GetPrintLevelPositionToSample(int index, double radius);
    }

    public class LevelWizard7PointRadial : LevelWizardRadialBase
    {
        static readonly int numberOfRadialSamples = 6;

        public LevelWizard7PointRadial(LevelWizardBase.RuningState runningState)
			: base(runningState, 500, 370, 21, numberOfRadialSamples)
		{
		}

        public static string ApplyLeveling(string lineBeingSent, Vector3 currentDestination, PrinterMachineInstruction.MovementTypes movementMode)
        {
            Printer activePrinter = PrinterConnectionAndCommunication.Instance.ActivePrinter;
            if (activePrinter != null
                && activePrinter.DoPrintLeveling
                && (lineBeingSent.StartsWith("G0 ") || lineBeingSent.StartsWith("G1 "))
                && lineBeingSent.Length > 2
                && lineBeingSent[2] == ' ')
            {
                PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(activePrinter);
                return GetLevelingFunctions(numberOfRadialSamples, levelingData, ActiveSliceSettings.Instance.BedCenter)
                    .DoApplyLeveling(lineBeingSent, currentDestination, movementMode);
            }

            return lineBeingSent;
        }

        public override Vector2 GetPrintLevelPositionToSample(int index, double radius)
        {
            Printer activePrinter = PrinterConnectionAndCommunication.Instance.ActivePrinter;
            PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(activePrinter);
            return GetLevelingFunctions(numberOfRadialSamples, levelingData, ActiveSliceSettings.Instance.BedCenter)
                .GetPrintLevelPositionToSample(index, radius);
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