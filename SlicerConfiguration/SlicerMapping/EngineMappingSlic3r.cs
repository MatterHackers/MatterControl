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
	public class Slic3rEngineMappings : SliceEngineMapping
	{
		public static readonly Slic3rEngineMappings Instance = new Slic3rEngineMappings();

		private List<string> hiddenSettings = null;

		// Singleton use only - prevent external construction
		private Slic3rEngineMappings() : base ("Slic3r")
		{
			hiddenSettings = new List<string>();
			hiddenSettings.Add("cool_extruder_lift");
			hiddenSettings.Add("support_material_create_internal_support");
			hiddenSettings.Add("support_material_create_perimeter");
			hiddenSettings.Add("min_extrusion_before_retract");
			hiddenSettings.Add("support_material_xy_distance");
			hiddenSettings.Add("support_material_z_distance");
			hiddenSettings.Add(SettingsKey.center_part_on_bed);
			hiddenSettings.Add(SettingsKey.expand_thin_walls);
			hiddenSettings.Add(SettingsKey.merge_overlapping_lines);
			hiddenSettings.Add(SettingsKey.fill_thin_gaps);
			hiddenSettings.Add("infill_overlap_perimeter");
			hiddenSettings.Add("support_type");
			hiddenSettings.Add("infill_type");
			hiddenSettings.Add("create_raft");
			hiddenSettings.Add("z_gap");
			hiddenSettings.Add(SettingsKey.bottom_clip_amount);
			hiddenSettings.Add("gcode_output_type");
			hiddenSettings.Add("raft_extra_distance_around_part");
			hiddenSettings.Add("output_only_first_layer");
			hiddenSettings.Add("raft_air_gap");
			hiddenSettings.Add("support_air_gap");
			hiddenSettings.Add("repair_outlines_extensive_stitching");
			hiddenSettings.Add("repair_outlines_keep_open");
			hiddenSettings.Add("complete_objects");
			hiddenSettings.Add("output_filename_format");
			hiddenSettings.Add("support_material_percent");
			hiddenSettings.Add("post_process");
			hiddenSettings.Add("extruder_clearance_height");
			hiddenSettings.Add("extruder_clearance_radius");
			hiddenSettings.Add("wipe_shield_distance");
			hiddenSettings.Add(SettingsKey.heat_extruder_before_homing);
			hiddenSettings.Add("extruders_share_temperature");
			hiddenSettings.Add("print_leveling_method");
			hiddenSettings.Add("solid_shell");
			hiddenSettings.Add("retractWhenChangingIslands");
			hiddenSettings.Add(SettingsKey.perimeter_start_end_overlap);
		}

		public override bool MapContains(string key)
		{
			// Visible items are anything not in the hiddenSettings set
			return !hiddenSettings.Contains(key);
		}
	}
}