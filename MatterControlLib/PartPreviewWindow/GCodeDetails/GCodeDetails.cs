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

using System;
using MatterControl.Printing;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public static class GCodeDetails
	{
		public static string LayerTime(this GCodeFile loadedGCode, int activeLayerIndex)
		{
			return loadedGCode.InstructionTime(activeLayerIndex, activeLayerIndex + 1);
		}

		public static string LayerTimeToHere(this GCodeFile loadedGCode, int activeLayerIndex)
		{
			return loadedGCode.InstructionTime(0, activeLayerIndex + 1);
		}

		public static string LayerTimeFromeHere(this GCodeFile loadedGCode, int activeLayerIndex)
		{
			return loadedGCode.InstructionTime(activeLayerIndex + 1, int.MaxValue);
		}

		public static string EstimatedPrintTime(this GCodeFile loadedGCode)
		{
			if (loadedGCode == null || loadedGCode.LayerCount == 0)
			{
				return "---";
			}

			return SecondsToTime(loadedGCode.Instruction(0).SecondsToEndFromHere);
		}

		private static string InstructionTime(this GCodeFile loadedGCode, int startLayer, int endLayer)
		{
			if (loadedGCode == null || loadedGCode.LayerCount == 0)
			{
				return "---";
			}

			int startInstruction = loadedGCode.GetFirstLayerInstruction(startLayer);
			int endInstruction = loadedGCode.GetFirstLayerInstruction(endLayer);
			var secondsToEndFromStart = loadedGCode.Instruction(startInstruction).SecondsToEndFromHere;
			var secondsToEndFromEnd = loadedGCode.Instruction(endInstruction).SecondsToEndFromHere;
			return SecondsToTime(secondsToEndFromStart - secondsToEndFromEnd);
		}

		public static string SecondsToTime(double seconds)
		{
			int secondsRemaining = (int)seconds;
			int hoursRemaining = (int)(secondsRemaining / (60 * 60));
			int minutesRemaining = (int)(secondsRemaining / 60 - hoursRemaining * 60);

			secondsRemaining = secondsRemaining % 60;

			if (hoursRemaining > 0)
			{
				return $"{hoursRemaining} h, {minutesRemaining} min";
			}
			else if (minutesRemaining > 10)
			{
				return $"{minutesRemaining} min";
			}
			else
			{
				return $"{minutesRemaining} min {secondsRemaining} s";
			}
		}

		public static double EstimatedPrintSeconds(this GCodeFile loadedGCode)
		{
			if (loadedGCode == null || loadedGCode.LayerCount == 0)
			{
				return 0;
			}

			return loadedGCode.Instruction(0).SecondsToEndFromHere;
		}

		public static string FilamentUsed(this GCodeFile loadedGCode, PrinterConfig printer)
		{
			return string.Format("{0:0.0} mm", loadedGCode.GetFilamentUsedMm(printer.Settings.GetValue<double>(SettingsKey.filament_diameter)));
		}

		public static string FilamentVolume(this GCodeFile loadedGCode, PrinterConfig printer)
		{
			return string.Format("{0:0.00} cm³", loadedGCode.GetFilamentCubicMm(printer.Settings.GetValue<double>(SettingsKey.filament_diameter)) / 1000);
		}

		public static string EstimatedMass(this GCodeFile loadedGCode, PrinterConfig printer)
		{
			var totalMass = TotalMass(loadedGCode, printer);
			return totalMass <= 0 ? "Unknown" : string.Format("{0:0.00} g", totalMass);
		}

		public static string EstimatedCost(this GCodeFile loadedGCode, PrinterConfig printer)
		{
			var totalMass = TotalCost(loadedGCode, printer);
			return totalMass <= 0 ? "Unknown" : string.Format("${0:0.00}", totalMass);
		}

		public static double TotalMass(this GCodeFile loadedGCode, PrinterConfig printer)
		{
			double filamentDiameter = printer.Settings.GetValue<double>(SettingsKey.filament_diameter);
			double filamentDensity = printer.Settings.GetValue<double>(SettingsKey.filament_density);

			return loadedGCode.GetFilamentWeightGrams(filamentDiameter, filamentDensity);
		}

		public static double GetLayerHeight(this GCodeFile loadedGCode, int layerIndex)
		{
			if (loadedGCode == null || loadedGCode.LayerCount == 0)
			{
				return 0;
			}

			return loadedGCode.GetLayerHeight(layerIndex);
		}

		internal static object GetLayerTop(this GCodeFile loadedGCode, int layerIndex)
		{
			if (loadedGCode == null || loadedGCode.LayerCount == 0)
			{
				return 0;
			}

			return loadedGCode.GetLayerTop(layerIndex);
		}

		public static string GetLayerFanSpeeds(this GCodeFile loadedGCode, int activeLayerIndex)
		{
			if (loadedGCode == null || loadedGCode.LayerCount == 0)
			{
				return "---";
			}

			int startInstruction = loadedGCode.GetFirstLayerInstruction(activeLayerIndex);
			if(activeLayerIndex == 0)
			{
				startInstruction = 0;
			}
			int endInstruction = loadedGCode.GetFirstLayerInstruction(activeLayerIndex + 1);

			string separator = "";
			string fanSpeeds = "";
			for (int i = startInstruction; i < endInstruction; i++)
			{
				var line = loadedGCode.Instruction(i).Line;
				if (line.StartsWith("M107")) // fan off
				{
					fanSpeeds += separator + "Off";
					separator = ", ";
				}
				else if(line.StartsWith("M106")) // fan on
				{
					double speed = 0;
					if (GCodeFile.GetFirstNumberAfter("M106", line, ref speed, 0, ""))
					{
						fanSpeeds += separator + $"{speed/255*100:0}%";
						separator = ", ";
					}
				}
			}

			return fanSpeeds;
		}

		public static double TotalCost(this GCodeFile loadedGCode, PrinterConfig printer)
		{
			double filamentCost = printer.Settings.GetValue<double>(SettingsKey.filament_cost);
			return loadedGCode.TotalMass(printer) / 1000 * filamentCost;
		}
	}
}