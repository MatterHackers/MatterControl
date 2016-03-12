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

using System.Collections.Generic;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public abstract class SliceEngineMapping
	{
		private string engineName;

		/// <summary>
		/// These application level settings will appear in all slice engine Slice Settings panels but are 
		/// not used or passed to the active slice engine and are simply tagging along with the existing
		/// settings infrastructure
		/// </summary>
		protected HashSet<string> applicationLevelSettings = new HashSet<string>()
		{
			"bed_shape",
			"bed_size",
			"bed_temperature",
			"build_height",
			"cancel_gcode",
			"connect_gcode",
			"has_fan",
			"has_hardware_leveling",
			"has_heated_bed",
			"has_power_control",
			"has_sd_card_reader",
			"manual_probe_paper_width",
			"pause_gcode",
			"print_leveling_method",
			"print_leveling_required_to_print",
			"print_leveling_solution",
			"resume_gcode",
			"support_material_threshold",
			"temperature",
			"z_can_be_negative"

			// TODO: Determine if these are MatterSlice only values or if it was an error that they were missing from Cura
			/*
				"bed_remove_part_temperature" - !cura
				"extruder_count" !cura
				"extruder_wipe_temperature" !cura
				"extruders_share_temperature" !cura
				"heat_extruder_before_homing" !cura
				"include_firmware_updater" !cura (but seems like it should be)
				"layer_to_pause" !cura (but seems like it should be)
				"show_reset_connection" !cura (seems like it should be)
				"solid_shell" !cura
			*/
		};

		public SliceEngineMapping(string engineName)
		{
			this.engineName = engineName;
		}

		public string Name { get { return engineName; } }

		public abstract bool MapContains(string canonicalSettingsName);
	}
}