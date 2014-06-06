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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Font;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
    // this class is so that it is not passed by value
    public class ProbePosition
    {
        public Vector3 position;
    }

    public class LevelWizardBase : SystemWindow
    {
        protected static readonly string initialPrinterSetupStepText = "Initial Printer Setup".Localize();
        protected static readonly string requiredPageInstructions1 = "Congratulations on setting up your new printer. Before starting your first print we need to run a simple calibration procedure.";
        protected static readonly string requiredPageInstructions2 = "The next few screens will walk your through the print leveling wizard.";

        protected static readonly string homingPageStepText = "Homing The Printer".Localize();
        protected static readonly string homingPageInstructionsTextOne = LocalizedString.Get("The printer should now be 'homing'. Once it is finished homing we will move it to the first point to sample.\n\nTo complete the next few steps you will need");
        protected static readonly string homingPageInstructionsTextTwo = LocalizedString.Get("A standard sheet of paper");
        protected static readonly string homingPageInstructionsTextThree = LocalizedString.Get("We will use this paper to measure the distance between the extruder and the bed.\n\nClick 'Next' to continue.");

        protected static readonly string doneInstructionsText = LocalizedString.Get("Congratulations!\n\nAuto Print Leveling is now configured and enabled.");
        protected static readonly string doneInstructionsTextTwo = LocalizedString.Get("Remove the paper");
        protected static readonly string doneInstructionsTextThree = LocalizedString.Get("If in the future you need to re-calibrate your printer, or you wish to turn Auto Print Leveling off, you can find the print leveling controls in 'Advanced Settings'->'Configuration'.\n\nClick 'Done' to close this window.");
        protected static readonly string stepTextBeg = LocalizedString.Get("Step");
        protected static readonly string stepTextEnd = LocalizedString.Get("of");

        protected WizardControl printLevelWizard;

        int totalSteps;
        protected int stepNumber = 1;

        protected string GetStepString()
        {
            return string.Format("{0} {1} {2} {3}:", stepTextBeg, stepNumber++, stepTextEnd, totalSteps);
        }

        public LevelWizardBase(int width, int height, int totalSteps)
            : base(width, height)
        {
            this.totalSteps = totalSteps;
        }

        public enum RuningState { InitialStartupCalibration, UserRequestedCalibration }
        public static LevelWizardBase CreateAndShowWizard(RuningState runningState)
        {
            LevelWizardBase printLevelWizardWindow;
            if (ActivePrinterProfile.Instance.ActivePrinter.PrintLevelingType != null
                && ActivePrinterProfile.Instance.ActivePrinter.PrintLevelingType != ""
                && ActivePrinterProfile.Instance.ActivePrinter.PrintLevelingType == "2Point")
            {
                printLevelWizardWindow = new LevelWizard2Point(runningState);
            }
            else
            {
                printLevelWizardWindow = new LevelWizard3Point(runningState);
            }
            printLevelWizardWindow.ShowAsSystemWindow();
            return printLevelWizardWindow;
        }
    }
}
