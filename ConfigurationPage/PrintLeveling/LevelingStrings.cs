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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class LevelingStrings
	{
		public string HomingPageStepText = "Homing The Printer".Localize();
		public string initialPrinterSetupStepText = "Initial Printer Setup".Localize();
		private string doneLine1 = "Congratulations!";
		private string doneLine1b = "Auto Print Leveling is now configured and enabled.".Localize();
		private string doneLine2 = "Remove the paper".Localize();
		private string doneLine3 = "If you need to recalibrate the printer in the future, the print leveling controls can be found under: Controls, Calibration";
		private string doneLine3b = "Click 'Done' to close this window.".Localize();
		private int stepNumber = 1;
		private string welcomeLine1 = "Welcome to the print leveling wizard. Here is a quick overview on what we are going to do.".Localize();
		private string selectMaterial = "Select the material you are printing".Localize();
		private string heatTheBed = "Heat the bed".Localize();
		private string sampelAtPoints = "Sample the bed at {0} points".Localize();
		private string turnOnLeveling = "Turn auto leveling on".Localize();
		private string timeToDone = "We should be done in approximately {0} minutes.".Localize();
		public string CleanExtruder => "Be sure the tip of the extruder is clean and the bed is clear.".Localize();
		public string ClickNext => "Click 'Next' to continue.".Localize();

		PrinterSettings printerSettings;
		public LevelingStrings(PrinterSettings printerSettings)
		{
			this.printerSettings = printerSettings;
		}

		public string DoneInstructions
		{
			get
			{
				if (printerSettings.Helpers.UseZProbe())
				{
					return $"{doneLine1} {doneLine1b}\n\n{doneLine3}\n\n{doneLine3b}";
				}
				else
				{
					return $"{doneLine1} {doneLine1b}\n\n\t• {doneLine2}\n\n{doneLine3}\n\n{doneLine3b}";
				}
			}
		}

		public string HomingPageInstructions(bool useZProbe, bool heatBed)
		{
			string line1 = "The printer should now be 'homing'.".Localize();
			if (heatBed)
			{
				line1 += " " + "Once it is finished homing we will heat the bed.".Localize();
			}
			if (useZProbe)
			{
				return line1;
			}
			else
			{
				string line2 = "To complete the next few steps you will need".Localize();
				string line3 = "A standard sheet of paper".Localize();
				string line4 = "We will use this paper to measure the distance between the extruder and the bed.".Localize();
				return $"{line1}\n\n{line2}:\n\n\t• {line3}\n\n{line4}\n\n{ClickNext}";
			}
		}

		string setZHeightLower = "Press [Z-] until there is resistance to moving the paper".Localize();
		string setZHeightRaise = "Press [Z+] once to release the paper".Localize();
		string setZHeightNext = "Finally click 'Next' to continue.".Localize();

		public string CoarseInstruction2
		{
			get
			{
				string setZHeightCourseInstructTextOne = "Place the paper under the extruder".Localize();
				string setZHeightCourseInstructTextTwo = "Using the above controls".Localize();
				return string.Format("\t• {0}\n\t• {1}\n\t• {2}\n\t• {3}\n\n{4}", setZHeightCourseInstructTextOne, setZHeightCourseInstructTextTwo, setZHeightLower, setZHeightRaise, setZHeightNext);
			}
		}

		public string FineInstruction1 => "We will now refine our measurement of the extruder height at this position.".Localize();
		public string FineInstruction2
		{
			get
			{
				return string.Format("\t• {0}\n\t• {1}\n\n{2}", setZHeightLower, setZHeightRaise, setZHeightNext);
			}
		}

		public string UltraFineInstruction1 => "We will now finalize our measurement of the extruder height at this position.".Localize();

		public string GetStepString(int totalSteps)
		{
			return $"{"Step".Localize()} {stepNumber++} {"of".Localize()} {totalSteps}:";
		}

		public string WelcomeText(int numberOfSteps, int numberOfMinutes)
		{
			if (printerSettings.GetValue<bool>(SettingsKey.has_heated_bed))
			{
				return "{0}\n\n\t• {1}\n\t• {2}\n\t• {3}\n\t• {4}\n\t• {5}\n\n{6}\n\n{7}".FormatWith(
					this.welcomeLine1,
					this.selectMaterial,
					"Home the printer".Localize(),
					this.heatTheBed,
					this.WelcomeLine5(numberOfSteps),
					this.turnOnLeveling,
					this.WelcomeLine7(numberOfMinutes),
					this.ClickNext);
			}
			else
			{
				return "{0}\n\n\t• {1}\n\t• {2}\n\t• {3}\n\n{4}\n\n{5}".FormatWith(
					this.welcomeLine1,
					"Home the printer".Localize(),
					this.WelcomeLine5(numberOfSteps),
					this.turnOnLeveling,
					this.WelcomeLine7(numberOfMinutes),
					this.ClickNext);
			}
		}

		private string WelcomeLine5(int numberOfPoints)
		{
			return sampelAtPoints.FormatWith(numberOfPoints);
		}

		private string WelcomeLine7(int numberOfMinutes)
		{
			return timeToDone.FormatWith(numberOfMinutes);
		}
	}
}