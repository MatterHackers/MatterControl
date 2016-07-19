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
	public class LevelWizard13PointRadial : LevelWizardRadialBase
    {
		static readonly int numberOfRadialSamples = 12;

		public LevelWizard13PointRadial(LevelWizardBase.RuningState runningState)
			: base(runningState, 500, 370, (numberOfRadialSamples + 1)*3, numberOfRadialSamples)
		{
		}

        public static string ApplyLeveling(string lineBeingSent, Vector3 currentDestination, PrinterMachineInstruction.MovementTypes movementMode)
        {
			var settings = ActiveSliceSettings.Instance;
            if (settings?.GetValue<bool>("print_leveling_enabled") == true
                && (lineBeingSent.StartsWith("G0 ") || lineBeingSent.StartsWith("G1 "))
                && lineBeingSent.Length > 2
                && lineBeingSent[2] == ' ')
            {
                return GetLevelingFunctions(numberOfRadialSamples, settings.Helpers.GetPrintLevelingData(), ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center))
                    .DoApplyLeveling(lineBeingSent, currentDestination, movementMode);
            }

            return lineBeingSent;
        }

        public override Vector2 GetPrintLevelPositionToSample(int index, double radius)
        {
            PrintLevelingData levelingData = ActiveSliceSettings.Instance.Helpers.GetPrintLevelingData();
            return GetLevelingFunctions(numberOfRadialSamples, levelingData, ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center))
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
            if (lineBeingSent.StartsWith("G28")
				|| lineBeingSent.StartsWith("G29"))
			{
                lines.Add("M114");
            }

            return lines;
        }
    }
}