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

using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	// this class is so that it is not passed by value
	public class ProbePosition
	{
		public Vector3 position;
	}

	public class LevelWizardBase : SystemWindow
	{
		public enum RuningState { InitialStartupCalibration, UserRequestedCalibration }

		protected static readonly string initialPrinterSetupStepText = "Initial Printer Setup".Localize();
		protected static readonly string requiredPageInstructions1 = "Congratulations on connecting to your new printer. Before starting your first print we need to run a simple calibration procedure.";
		protected static readonly string requiredPageInstructions2 = "The next few screens will walk your through the print leveling wizard.";

		protected static readonly string homingPageStepText = "Homing The Printer".Localize();
		protected static readonly string homingPageInstructionsTextOne = LocalizedString.Get("The printer should now be 'homing'. Once it is finished homing we will move it to the first point to sample.\n\nTo complete the next few steps you will need");
		protected static readonly string homingPageInstructionsTextTwo = LocalizedString.Get("A standard sheet of paper");
		protected static readonly string homingPageInstructionsTextThree = LocalizedString.Get("We will use this paper to measure the distance between the extruder and the bed.\n\nClick 'Next' to continue.");

		protected static readonly string doneInstructionsText = LocalizedString.Get("Congratulations!\n\nAuto Print Leveling is now configured and enabled.");
		protected static readonly string doneInstructionsTextTwo = LocalizedString.Get("Remove the paper");
		protected static readonly string doneInstructionsTextThree = LocalizedString.Get("To re-calibrate the printer, or to turn off Auto Print Leveling, the print leveling controls can be found under 'Options'->'Calibration'.\n\nClick 'Done' to close this window.");
		protected static readonly string stepTextBeg = LocalizedString.Get("Step");
		protected static readonly string stepTextEnd = LocalizedString.Get("of");

		protected WizardControl printLevelWizard;

		private int totalSteps;
		protected int stepNumber = 1;

		protected string GetStepString()
		{
			return string.Format("{0} {1} {2} {3}:", stepTextBeg, stepNumber++, stepTextEnd, totalSteps);
		}

		public LevelWizardBase(int width, int height, int totalSteps)
			: base(width, height)
		{
			AlwaysOnTopOfMain = true;
			this.totalSteps = totalSteps;
		}

		public static Vector2 GetPrintLevelPositionToSample(int index)
		{
			Vector2 bedSize = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.bed_size);
			Vector2 printCenter = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center);

			switch (ActiveSliceSettings.Instance.GetValue<BedShape>(SettingsKey.bed_shape))
			{
				case BedShape.Circular:
					Vector2 firstPosition = new Vector2(printCenter.x, printCenter.y + (bedSize.y / 2) * .5);
					switch (index)
					{
						case 0:
							return firstPosition;

						case 1:
							return Vector2.Rotate(firstPosition, MathHelper.Tau / 3);

						case 2:
							return Vector2.Rotate(firstPosition, MathHelper.Tau * 2 / 3);

						default:
							throw new IndexOutOfRangeException();
					}

				case BedShape.Rectangular:
				default:
					switch (index)
					{
						case 0:
							return new Vector2(printCenter.x, printCenter.y + (bedSize.y / 2) * .8);

						case 1:
							return new Vector2(printCenter.x - (bedSize.x / 2) * .8, printCenter.y - (bedSize.y / 2) * .8);

						case 2:
							return new Vector2(printCenter.x + (bedSize.x / 2) * .8, printCenter.y - (bedSize.y / 2) * .8);

						default:
							throw new IndexOutOfRangeException();
					}
			}
		}

		private static SystemWindow printLevelWizardWindow;

		public static void ShowPrintLevelWizard()
		{
			LevelWizardBase.RuningState runningState = LevelWizardBase.RuningState.UserRequestedCalibration;

			if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_required_to_print))
			{
				// run in the first run state
				runningState = LevelWizardBase.RuningState.InitialStartupCalibration;
			}

			ShowPrintLevelWizard(runningState);
		}

		public static void ShowPrintLevelWizard(LevelWizardBase.RuningState runningState)
		{
			if (printLevelWizardWindow == null)
			{
				printLevelWizardWindow = LevelWizardBase.CreateAndShowWizard(runningState);
				printLevelWizardWindow.Closed += (sender, e) =>
				{
					printLevelWizardWindow = null;
				};
			}
			else
			{
				printLevelWizardWindow.BringToFront();
			}
		}

		private static LevelWizardBase CreateAndShowWizard(LevelWizardBase.RuningState runningState)
		{
			PrintLevelingData levelingData = ActiveSliceSettings.Instance.Helpers.GetPrintLevelingData();

			LevelWizardBase printLevelWizardWindow;
			switch (levelingData.CurrentPrinterLevelingSystem)
			{
				case PrintLevelingData.LevelingSystem.Probe2Points:
					printLevelWizardWindow = new LevelWizard2Point(runningState);
					break;

				case PrintLevelingData.LevelingSystem.Probe3Points:
					printLevelWizardWindow = new LevelWizard3Point(runningState);
					break;

				case PrintLevelingData.LevelingSystem.Probe7PointRadial:
					printLevelWizardWindow = new LevelWizard7PointRadial(runningState);
					break;

				case PrintLevelingData.LevelingSystem.Probe13PointRadial:
					printLevelWizardWindow = new LevelWizard13PointRadial(runningState);
					break;

				default:
					throw new NotImplementedException();
			}

			printLevelWizardWindow.ShowAsSystemWindow();
			return printLevelWizardWindow;
		}
	}

	public class PrintLevelingInfo
	{
	}
}