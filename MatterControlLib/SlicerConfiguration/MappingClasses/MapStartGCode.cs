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

using MatterHackers.Agg;
using System;
using System.Collections.Generic;
using System.Text;

namespace MatterHackers.MatterControl.SlicerConfiguration.MappingClasses
{
	public class MapStartGCode : InjectGCodeCommands
	{
		private bool escapeNewlineCharacters;

		public MapStartGCode(PrinterConfig printer, string canonicalSettingsName, string exportedName, bool escapeNewlineCharacters)
			: base(printer, canonicalSettingsName, exportedName)
		{
			this.escapeNewlineCharacters = escapeNewlineCharacters;
		}

		public override string Value
		{
			get
			{
				StringBuilder newStartGCode = new StringBuilder();
				foreach (string line in PreStartGCode(Slicer.extrudersUsed))
				{
					newStartGCode.Append(line + "\n");
				}

				newStartGCode.Append(printer.ReplaceMacroValues(base.Value));

				foreach (string line in PostStartGCode(Slicer.extrudersUsed))
				{
					newStartGCode.Append("\n");
					newStartGCode.Append(line);
				}

				if (escapeNewlineCharacters)
				{
					return newStartGCode.ToString().Replace("\n", "\\n");
				}

				return newStartGCode.ToString();
			}
		}

		public List<string> PostStartGCode(List<bool> extrudersUsed)
		{
			string startGCode = printer.Settings.GetValue(SettingsKey.start_gcode);
			string[] postStartGCodeLines = startGCode.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);

			List<string> postStartGCode = new List<string>();
			postStartGCode.Add("; automatic settings after start_gcode");

			int numberOfHeatedExtruders = printer.Settings.GetValue<int>(SettingsKey.extruder_count);

			// don't set extruder 0 to heating if we already waited for it to reach temp
			if (printer.Settings.GetValue(SettingsKey.heat_extruder_before_homing) != "1")
			{
				if (extrudersUsed[0])
				{
					double materialTemperature = printer.Settings.Helpers.ExtruderTemperature(0);
					if (materialTemperature != 0)
					{
						string setTempString = $"M109 T0 S{materialTemperature}";
						AddDefaultIfNotPresent(postStartGCode, setTempString, postStartGCodeLines, string.Format("wait for extruder {0} to reach temperature", 1));
					}
				}
			}

			if (extrudersUsed.Count > 1)
			{
				// start all the extruders heating
				for (int extruderIndex0Based = 1; extruderIndex0Based < numberOfHeatedExtruders; extruderIndex0Based++)
				{
					if (extruderIndex0Based < extrudersUsed.Count
						&& extrudersUsed[extruderIndex0Based])
					{
						double materialTemperature = printer.Settings.Helpers.ExtruderTemperature(extruderIndex0Based);
						if (materialTemperature != 0)
						{
							// always heat the extruders that are used beyond extruder 0
							postStartGCode.Add($"M104 T{extruderIndex0Based} S{materialTemperature} ; Start heating extruder{extruderIndex0Based + 1}");
						}
					}
				}

				// wait for them to finish
				for (int extruderIndex0Based = 1; extruderIndex0Based < numberOfHeatedExtruders; extruderIndex0Based++)
				{
					if (extruderIndex0Based < extrudersUsed.Count
						&& extrudersUsed[extruderIndex0Based])
					{
						double materialTemperature = printer.Settings.Helpers.ExtruderTemperature(extruderIndex0Based);
						if (materialTemperature != 0)
						{
							// always heat the extruders that are used beyond extruder 0
							postStartGCode.Add($"M109 T{extruderIndex0Based} S{materialTemperature} ; Finish heating extruder{extruderIndex0Based + 1}");
						}
					}
				}
			}

			SwitchToFirstActiveExtruder(extrudersUsed, postStartGCodeLines, postStartGCode);
			AddDefaultIfNotPresent(postStartGCode, "G90", postStartGCodeLines, "use absolute coordinates");
			postStartGCode.Add(string.Format("{0} ; {1}", "G92 E0", "reset the expected extruder position"));
			AddDefaultIfNotPresent(postStartGCode, "M82", postStartGCodeLines, "use absolute distance for extrusion");

			return postStartGCode;
		}

		public List<string> PreStartGCode(List<bool> extrudersUsed)
		{
			string startGCode = printer.Settings.GetValue(SettingsKey.start_gcode);
			string[] preStartGCodeLines = startGCode.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);

			List<string> preStartGCode = new List<string>();
			preStartGCode.Add("; automatic settings before start_gcode");
			AddDefaultIfNotPresent(preStartGCode, "G21", preStartGCodeLines, "set units to millimeters");
			AddDefaultIfNotPresent(preStartGCode, "M107", preStartGCodeLines, "fan off");
			double bed_temperature = printer.Settings.GetValue<double>(SettingsKey.bed_temperature);
			if (bed_temperature > 0)
			{
				string setBedTempString = string.Format("M190 S{0}", bed_temperature);
				AddDefaultIfNotPresent(preStartGCode, setBedTempString, preStartGCodeLines, "wait for bed temperature to be reached");
			}

			int numberOfHeatedExtruders = printer.Settings.Helpers.NumberOfHotends();

			// Start heating all the extruder that we are going to use.
			for (int extruderIndex0Based = 0; extruderIndex0Based < numberOfHeatedExtruders; extruderIndex0Based++)
			{
				if (extrudersUsed.Count > extruderIndex0Based
					&& extrudersUsed[extruderIndex0Based])
				{
					double materialTemperature = printer.Settings.Helpers.ExtruderTemperature(extruderIndex0Based);
					if (materialTemperature != 0)
					{
						string setTempString = "M104 T{0} S{1}".FormatWith(extruderIndex0Based, materialTemperature);
						AddDefaultIfNotPresent(preStartGCode, setTempString, preStartGCodeLines, string.Format("start heating extruder {0}", extruderIndex0Based + 1));
					}
				}
			}

			// If we need to wait for the heaters to heat up before homing then set them to M109 (heat and wait).
			if (printer.Settings.GetValue(SettingsKey.heat_extruder_before_homing) == "1")
			{
				for (int extruderIndex0Based = 0; extruderIndex0Based < numberOfHeatedExtruders; extruderIndex0Based++)
				{
					if (extrudersUsed.Count > extruderIndex0Based
						&& extrudersUsed[extruderIndex0Based])
					{
						double materialTemperature = printer.Settings.Helpers.ExtruderTemperature(extruderIndex0Based);
						if (materialTemperature != 0)
						{
							string setTempString = "M109 T{0} S{1}".FormatWith(extruderIndex0Based, materialTemperature);
							AddDefaultIfNotPresent(preStartGCode, setTempString, preStartGCodeLines, string.Format("wait for extruder {0}", extruderIndex0Based + 1));
						}
					}
				}
			}

			SwitchToFirstActiveExtruder(extrudersUsed, preStartGCodeLines, preStartGCode);
			preStartGCode.Add("; settings from start_gcode");

			return preStartGCode;
		}

		private void SwitchToFirstActiveExtruder(List<bool> extrudersUsed, string[] preStartGCodeLines, List<string> preStartGCode)
		{
			// make sure we are on the first active extruder
			for (int extruderIndex = 0; extruderIndex < extrudersUsed.Count; extruderIndex++)
			{
				if (extrudersUsed[extruderIndex])
				{
					// set the active extruder to the first one that will be printing
					AddDefaultIfNotPresent(preStartGCode, "T{0}".FormatWith(extruderIndex), preStartGCodeLines, "set the active extruder to {0}".FormatWith(extruderIndex));
					break; // then break so we don't set it to a different ones
				}
			}
		}
	}
}