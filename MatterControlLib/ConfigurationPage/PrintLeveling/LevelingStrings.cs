/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class LevelingStrings
	{
		private int stepNumber = 1;

		// Private shared localized strings
		private string welcomeLine1 = "Welcome to the print leveling wizard. Here is a quick overview on what we are going to do.".Localize();
		private string selectMaterial = "Select the material you are printing".Localize();
		private string heatTheBed = "Heat the bed".Localize();
		private string sampelAtPoints = "Sample the bed at {0} points".Localize();
		private string turnOnLeveling = "Turn auto leveling on".Localize();
		private string timeToDone = "We should be done in approximately {0} minutes.".Localize();
		private string setZHeightLower = "Press [Z-] until there is resistance to moving the paper".Localize();
		private string setZHeightRaise = "Press [Z+] once to release the paper".Localize();
		private string setZHeightNext = "Finally click 'Next' to continue.".Localize();

		private PrinterSettings printerSettings;

		public LevelingStrings(PrinterSettings printerSettings)
		{
			this.printerSettings = printerSettings;
		}

		public string HomingPageInstructions(bool useZProbe, bool heatBed)
		{
			string line1 = "The printer should now be 'homing'.".Localize();
			if (heatBed)
			{
				line1 += " Once it is finished homing we will heat the bed.".Localize();
			}

			if (useZProbe)
			{
				return line1;
			}
			else
			{
				return string.Format(
					"{0}\n\n{1}:\n\n\t• {2}\n\n{3}\n\n{4}",
					line1,
					"To complete the next few steps you will need".Localize(),
					"A standard sheet of paper".Localize(),
					"We will use this paper to measure the distance between the nozzle and the bed.".Localize(),
					"Click 'Next' to continue.".Localize());
			}
		}

		public string CoarseInstruction2 => string.Format(
			"\t• {0}\n\t• {1}\n\t• {2}\n\t• {3}\n\n{4}",
			"Place the paper under the extruder".Localize(),
			"Using the above controls".Localize(),
			setZHeightLower,
			setZHeightRaise,
			setZHeightNext);

		public string InitialPrinterSetupStepText => "Initial Printer Setup".Localize();

		public string FineInstruction1 => "We will now refine our measurement of the extruder height at this position.".Localize();

		public string FineInstruction2 => $"\t• {setZHeightLower}\n\t• {setZHeightRaise}\n\n{setZHeightNext}";

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
					"Click 'Next' to continue.".Localize());
			}
			else
			{
				return "{0}\n\n\t• {1}\n\t• {2}\n\t• {3}\n\n{4}\n\n{5}".FormatWith(
					this.welcomeLine1,
					"Home the printer".Localize(),
					this.WelcomeLine5(numberOfSteps),
					this.turnOnLeveling,
					this.WelcomeLine7(numberOfMinutes),
					"Click 'Next' to continue.".Localize());
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