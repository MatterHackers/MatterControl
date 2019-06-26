/*
Copyright (c) 2016, Lars Brubaker
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
using System.Text;
using MatterHackers.Agg;

namespace MatterHackers.MatterControl.SlicerConfiguration.MappingClasses
{
	public class MapStartGCode : InjectGCodeCommands
	{
		private readonly bool escapeNewlineCharacters;

		public MapStartGCode(bool escapeNewlineCharacters)
		{
			this.escapeNewlineCharacters = escapeNewlineCharacters;
		}

		public override string Resolve(string value, PrinterSettings settings)
		{
			System.Diagnostics.Debugger.Break();

			var newStartGCode = new StringBuilder();

			//foreach (string line in PreStartGCode(Slicer.ExtrudersUsed))
			//{
			//	newStartGCode.Append(line + "\n");
			//}

			value = base.Resolve(value, settings);

			newStartGCode.Append(settings.ReplaceMacroValues(value));

			//foreach (string line in PostStartGCode(Slicer.ExtrudersUsed))
			//{
			//	newStartGCode.Append("\n");
			//	newStartGCode.Append(line);
			//}

			if (escapeNewlineCharacters)
			{
				return newStartGCode.ToString().Replace("\n", "\\n");
			}

			return newStartGCode.ToString();
		}

		public List<string> PreStartGCode(PrinterSettings settings, List<bool> extrudersUsed)
		{
			string startGCode = settings.GetValue(SettingsKey.start_gcode);
			string[] startGCodeLines = startGCode.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);

			var preStartGCode = new List<string>
			{
				"; automatic settings before start_gcode"
			};
			AddDefaultIfNotPresent(preStartGCode, "G21", startGCodeLines, "set units to millimeters");
			AddDefaultIfNotPresent(preStartGCode, "M107", startGCodeLines, "fan off");
			double bed_temperature = settings.GetValue<double>(SettingsKey.bed_temperature);
			if (bed_temperature > 0)
			{
				string setBedTempString = string.Format("M140 S{0}", bed_temperature);
				AddDefaultIfNotPresent(preStartGCode, setBedTempString, startGCodeLines, "start heating the bed");
			}

			int numberOfHeatedExtruders = settings.Helpers.HotendCount();

			// Start heating all the extruder that we are going to use.
			for (int hotendIndex = 0; hotendIndex < numberOfHeatedExtruders; hotendIndex++)
			{
				if (extrudersUsed.Count > hotendIndex
					&& extrudersUsed[hotendIndex])
				{
					double materialTemperature = settings.Helpers.ExtruderTargetTemperature(hotendIndex);
					if (materialTemperature != 0)
					{
						string setTempString = "M104 T{0} S{1}".FormatWith(hotendIndex, materialTemperature);
						AddDefaultIfNotPresent(preStartGCode, setTempString, startGCodeLines, $"start heating T{hotendIndex}");
					}
				}
			}

			// If we need to wait for the heaters to heat up before homing then set them to M109 (heat and wait).
			if (settings.GetValue<bool>(SettingsKey.heat_extruder_before_homing))
			{
				for (int hotendIndex = 0; hotendIndex < numberOfHeatedExtruders; hotendIndex++)
				{
					if (extrudersUsed.Count > hotendIndex
						&& extrudersUsed[hotendIndex])
					{
						double materialTemperature = settings.Helpers.ExtruderTargetTemperature(hotendIndex);
						if (materialTemperature != 0)
						{
							string setTempString = "M109 T{0} S{1}".FormatWith(hotendIndex, materialTemperature);
							AddDefaultIfNotPresent(preStartGCode, setTempString, startGCodeLines, $"wait for T{hotendIndex }");
						}
					}
				}
			}

			// If we have bed temp and the start gcode specifies to finish heating the extruders,
			// make sure we also finish heating the bed. This preserves legacy expectation.
			if (bed_temperature > 0
				&& startGCode.Contains("M109"))
			{
				string setBedTempString = string.Format("M190 S{0}", bed_temperature);
				AddDefaultIfNotPresent(preStartGCode, setBedTempString, startGCodeLines, "wait for bed temperature to be reached");
			}

			SwitchToFirstActiveExtruder(extrudersUsed, preStartGCode);
			preStartGCode.Add("; settings from start_gcode");

			return preStartGCode;
		}

		public List<string> PostStartGCode(PrinterSettings settings, List<bool> extrudersUsed)
		{
			string startGCode = settings.GetValue(SettingsKey.start_gcode);
			string[] startGCodeLines = startGCode.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);

			var postStartGCode = new List<string>
			{
				"; automatic settings after start_gcode"
			};

			double bed_temperature = settings.GetValue<double>(SettingsKey.bed_temperature);
			if (bed_temperature > 0
				&& !startGCode.Contains("M109"))
			{
				string setBedTempString = string.Format("M190 S{0}", bed_temperature);
				AddDefaultIfNotPresent(postStartGCode, setBedTempString, startGCodeLines, "wait for bed temperature to be reached");
			}

			int numberOfHeatedExtruders = settings.GetValue<int>(SettingsKey.extruder_count);
			// wait for them to finish
			for (int hotendIndex = 0; hotendIndex < numberOfHeatedExtruders; hotendIndex++)
			{
				if (hotendIndex < extrudersUsed.Count
					&& extrudersUsed[hotendIndex])
				{
					double materialTemperature = settings.Helpers.ExtruderTargetTemperature(hotendIndex);
					if (materialTemperature != 0)
					{
						if (!(hotendIndex == 0 && LineStartsWith(startGCodeLines, "M109 S"))
							&& !LineStartsWith(startGCodeLines, $"M109 T{hotendIndex} S"))
						{
							// always heat the extruders that are used beyond extruder 0
							postStartGCode.Add($"M109 T{hotendIndex} S{materialTemperature} ; Finish heating T{hotendIndex}");
						}
					}
				}
			}

			SwitchToFirstActiveExtruder(extrudersUsed, postStartGCode);
			AddDefaultIfNotPresent(postStartGCode, "G90", startGCodeLines, "use absolute coordinates");
			postStartGCode.Add(string.Format("{0} ; {1}", "G92 E0", "reset the expected extruder position"));
			AddDefaultIfNotPresent(postStartGCode, "M82", startGCodeLines, "use absolute distance for extrusion");

			return postStartGCode;
		}

		private void SwitchToFirstActiveExtruder(List<bool> extrudersUsed, List<string> preStartGCode)
		{
			// make sure we are on the first active extruder
			for (int extruderIndex = 0; extruderIndex < extrudersUsed.Count; extruderIndex++)
			{
				if (extrudersUsed[extruderIndex])
				{
					// set the active extruder to the first one that will be printing
					preStartGCode.Add("T{0} ; {1}".FormatWith(extruderIndex, "set the active extruder to {0}".FormatWith(extruderIndex)));
					// we have set the active extruder so don't set it to any other extruder
					break;
				}
			}
		}
	}
}