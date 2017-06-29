/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.GCodeVisualizer;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GCodeDetails
	{
		private GCodeFile loadedGCode;

		public GCodeDetails(GCodeFile loadedGCode)
		{
			this.loadedGCode = loadedGCode;
		}

		public string EstimatedPrintTime
		{
			get
			{
				if (loadedGCode == null)
				{
					return "---";
				}

				int secondsRemaining = (int)loadedGCode.Instruction(0).secondsToEndFromHere;
				int hoursRemaining = (int)(secondsRemaining / (60 * 60));
				int minutesRemaining = (int)((secondsRemaining + 30) / 60 - hoursRemaining * 60); // +30 for rounding

				secondsRemaining = secondsRemaining % 60;

				if (hoursRemaining > 0)
				{
					return $"{hoursRemaining} h, {minutesRemaining} min";
				}
				else
				{
					return $"{minutesRemaining} min";
				}
			}
		}

		public string FilamentUsed => string.Format("{0:0.0} mm", loadedGCode.GetFilamentUsedMm(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.filament_diameter)));

		public string FilamentVolume => string.Format("{0:0.00} cm³", loadedGCode.GetFilamentCubicMm(ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.filament_diameter)) / 1000);

		public string EstimatedMass => this.TotalMass <= 0 ? "Unknown" : string.Format("{0:0.00} g", this.TotalMass);

		public string EstimatedCost => this.TotalCost <= 0 ? "Unknown" : string.Format("${0:0.00}", this.TotalCost);

		public double TotalMass
		{
			get
			{
				double filamentDiameter = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.filament_diameter);
				double filamentDensity = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.filament_density);

				return loadedGCode.GetFilamentWeightGrams(filamentDiameter, filamentDensity);
			}
		}

		public double TotalCost
		{
			get
			{
				double filamentCost = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.filament_cost);
				return this.TotalMass / 1000 * filamentCost;
			}
		}
	}
}