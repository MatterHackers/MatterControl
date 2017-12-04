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
using System.IO;
using System.Linq;
using MatterHackers.Agg;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class Slic3rEngineMappings : SliceEngineMapping
	{
		public static readonly Slic3rEngineMappings Instance = new Slic3rEngineMappings();
		private List<MappedSetting> mappedSettings = new List<MappedSetting>();
		private HashSet<string> slic3rSliceSettingNames;

		// Singleton use only - prevent external construction
		private Slic3rEngineMappings() : base("Slic3r")
		{
			foreach (var key in PrinterSettings.KnownSettings.Where(k => !k.StartsWith("MatterControl.")))
			{
				mappedSettings.Add(new MappedSetting(key, key));
			}

			string[] hiddenSettings =
			{
				"cool_extruder_lift",
				"support_material_create_internal_support",
				"support_material_create_perimeter",
				"min_extrusion_before_retract",
				"support_material_xy_distance",
				"support_material_z_distance",
				SettingsKey.print_center,
				SettingsKey.expand_thin_walls,
				SettingsKey.merge_overlapping_lines,
				SettingsKey.fill_thin_gaps,
				SettingsKey.infill_overlap_perimeter,
				"support_type",
				"infill_type",
				"create_raft",
				"z_gap",
				"gcode_output_type",
				"raft_extra_distance_around_part",
				"output_only_first_layer",
				"raft_air_gap",
				"support_air_gap",
				"repair_outlines_extensive_stitching",
				"repair_outlines_keep_open",
				"complete_objects",
				"output_filename_format",
				"support_material_percent",
				"post_process",
				"extruder_clearance_height",
				"extruder_clearance_radius",
				"wipe_shield_distance",
				SettingsKey.heat_extruder_before_homing,
				"extruders_share_temperature",
				"solid_shell",
				"retractWhenChangingIslands",
				SettingsKey.perimeter_start_end_overlap,
				SettingsKey.bed_shape,
			};

			foreach(string key in hiddenSettings)
			{
				for (int i = mappedSettings.Count - 1; i >= 0; i--)
				{
					if (mappedSettings[i].CanonicalSettingsName == key)
					{
						mappedSettings.RemoveAt(i);
					}
				}
			}

			mappedSettings.Add(new Slice3rBedShape(SettingsKey.bed_shape));
			slic3rSliceSettingNames = new HashSet<string>(mappedSettings.Select(m => m.CanonicalSettingsName));
		}

		public static void WriteSliceSettingsFile(string outputFilename)
		{
			using (StreamWriter sliceSettingsFile = new StreamWriter(outputFilename))
			{
				foreach (MappedSetting mappedSetting in Instance.mappedSettings)
				{
					if (mappedSetting.Value != null)
					{
						sliceSettingsFile.WriteLine("{0} = {1}".FormatWith(mappedSetting.ExportedName, mappedSetting.Value));
					}
				}
			}
		}

		public override bool MapContains(string canonicalSettingsName)
		{
			return slic3rSliceSettingNames.Contains(canonicalSettingsName)
				|| base.applicationLevelSettings.Contains(canonicalSettingsName);
		}
	}
}