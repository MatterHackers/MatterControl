/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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

using MatterHackers.Localizations;

namespace MatterControl.Printing.PrintLeveling
{
	public class LevelingStrings
	{
		private int stepNumber = 1;

		public LevelingStrings()
		{
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
					"A sheet of paper".Localize(),
					"We will use this paper to measure the distance between the nozzle and the bed.".Localize(),
					"Click 'Next' to continue.".Localize());
			}
		}

		public string CoarseInstruction2 => string.Format(
			"\t• {0}\n\t• {1}\n{2}",
			"Place the paper under the extruder".Localize(),
			"Using the above controls".Localize(),
			this.FineInstruction2);

		public string FineInstruction2 => string.Format(
			"\t• {0}\n\t• {1}\n\n{2}",
			"Press [Z-] until there is resistance to moving the paper".Localize(),
			"Press [Z+] once to release the paper".Localize(),
			"Finally click 'Next' to continue.".Localize());

		public string GetStepString(int totalSteps)
		{
			return $"{"Step".Localize()} {stepNumber++} {"of".Localize()} {totalSteps}:";
		}
	}
}