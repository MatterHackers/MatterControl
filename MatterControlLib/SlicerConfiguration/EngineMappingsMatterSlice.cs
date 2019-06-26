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
using System.IO;
using System.Linq;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class EngineMappingsMatterSlice
	{
		private readonly HashSet<string> matterSliceSettingNames;

		public Dictionary<string, ExportField> Exports { get; }

		// Singleton use only - prevent external construction
		public EngineMappingsMatterSlice(PrinterConfig printer2)
		{
			Exports = new Dictionary<string, ExportField>()
			{
				[SettingsKey.bottom_solid_layers] = new ExportField("numberOfBottomLayers"),
				[SettingsKey.perimeters] = new ExportField("numberOfPerimeters"),
				[SettingsKey.raft_extra_distance_around_part] = new ExportField("raftExtraDistanceAroundPart"),
				[SettingsKey.support_material_interface_layers] = new ExportField("supportInterfaceLayers"),
				[SettingsKey.top_solid_layers] = new ExportField("numberOfTopLayers"),
				[SettingsKey.external_perimeter_extrusion_width] = new ExportField("outsidePerimeterExtrusionWidth"),
				[SettingsKey.external_perimeter_speed] = new ExportField("outsidePerimeterSpeed"),
				[SettingsKey.first_layer_speed] = new ExportField("firstLayerSpeed"),
				[SettingsKey.number_of_first_layers] = new ExportField("numberOfFirstLayers"),
				[SettingsKey.raft_print_speed] = new ExportField("raftPrintSpeed"),
				[SettingsKey.top_solid_infill_speed] = new ExportField("topInfillSpeed"),
				[SettingsKey.first_layer_extrusion_width] = new ExportField("firstLayerExtrusionWidth"),
				[SettingsKey.first_layer_height] = new ExportField("firstLayerThickness"),
				[SettingsKey.end_gcode] = new ExportField("endCode"),
				[SettingsKey.retract_before_travel] = new ExportField("minimumTravelToCauseRetraction"),
				[SettingsKey.retract_length] = new ExportField("retractionOnTravel"),
				[SettingsKey.retract_lift] = new ExportField("retractionZHop"),
				[SettingsKey.retract_restart_extra] = new ExportField("unretractExtraExtrusion"),
				[SettingsKey.retract_restart_extra_time_to_apply] = new ExportField("retractRestartExtraTimeToApply"),
				[SettingsKey.retract_speed] = new ExportField("retractionSpeed"),
				[SettingsKey.bridge_speed] = new ExportField("bridgeSpeed"),
				[SettingsKey.air_gap_speed] = new ExportField("airGapSpeed"),
				[SettingsKey.bottom_infill_speed] = new ExportField("bottomInfillSpeed"),
				[SettingsKey.bridge_over_infill] = new ExportField("bridgeOverInfill"),
				[SettingsKey.extrusion_multiplier] = new ExportField("extrusionMultiplier"),
				[SettingsKey.fill_angle] = new ExportField("infillStartingAngle"),
				[SettingsKey.infill_overlap_perimeter] = new ExportField("infillExtendIntoPerimeter"),
				[SettingsKey.infill_speed] = new ExportField("infillSpeed"),
				[SettingsKey.infill_type] = new ExportField("infillType"),
				[SettingsKey.min_extrusion_before_retract] = new ExportField("minimumExtrusionBeforeRetraction"),
				[SettingsKey.min_print_speed] = new ExportField("minimumPrintingSpeed"),
				[SettingsKey.perimeter_speed] = new ExportField("insidePerimetersSpeed"),
				[SettingsKey.raft_air_gap] = new ExportField("raftAirGap"),
				[SettingsKey.max_acceleration] = new ExportField("maxAcceleration"),
				[SettingsKey.max_velocity] = new ExportField("maxVelocity"),
				[SettingsKey.jerk_velocity] = new ExportField("jerkVelocity"),
				[SettingsKey.print_time_estimate_multiplier] = new ExportField("printTimeEstimateMultiplier"),
				// fan settings
				[SettingsKey.min_fan_speed] = new ExportField("fanSpeedMinPercent"),
				[SettingsKey.coast_at_end_distance] = new ExportField("coastAtEndDistance"),
				[SettingsKey.min_fan_speed_layer_time] = new ExportField("minFanSpeedLayerTime"),
				[SettingsKey.max_fan_speed] = new ExportField("fanSpeedMaxPercent"),
				[SettingsKey.max_fan_speed_layer_time] = new ExportField("maxFanSpeedLayerTime"),
				[SettingsKey.bridge_fan_speed] = new ExportField("bridgeFanSpeedPercent"),
				[SettingsKey.disable_fan_first_layers] = new ExportField("firstLayerToAllowFan"),
				// end fan
				[SettingsKey.retract_length_tool_change] = new ExportField("retractionOnExtruderSwitch"),
				[SettingsKey.retract_restart_extra_toolchange] = new ExportField("unretractExtraOnExtruderSwitch"),
				[SettingsKey.reset_long_extrusion] = new ExportField("resetLongExtrusion"),
				[SettingsKey.slowdown_below_layer_time] = new ExportField("minimumLayerTimeSeconds"),
				[SettingsKey.support_air_gap] = new ExportField("supportAirGap"),
				[SettingsKey.support_material_infill_angle] = new ExportField("supportInfillStartingAngle"),
				[SettingsKey.support_material_spacing] = new ExportField("supportLineSpacing"),
				[SettingsKey.support_material_speed] = new ExportField("supportMaterialSpeed"),
				[SettingsKey.interface_layer_speed] = new ExportField("interfaceLayerSpeed"),
				[SettingsKey.support_material_xy_distance] = new ExportField("supportXYDistanceFromObject"),
				[SettingsKey.support_type] = new ExportField("supportType"),
				[SettingsKey.travel_speed] = new ExportField("travelSpeed"),
				[SettingsKey.wipe_shield_distance] = new ExportField("wipeShieldDistanceFromObject"),
				[SettingsKey.wipe_tower_size] = new ExportField("wipeTowerSize"),
				[SettingsKey.filament_diameter] = new ExportField("filamentDiameter"),
				[SettingsKey.layer_height] = new ExportField("layerThickness"),
				[SettingsKey.nozzle_diameter] = new ExportField("extrusionWidth"),
				[SettingsKey.extruder_count] = new ExportField("extruderCount"),
				[SettingsKey.avoid_crossing_perimeters] = new ExportField("avoidCrossingPerimeters"),
				[SettingsKey.create_raft] = new ExportField("enableRaft"),
				[SettingsKey.external_perimeters_first] = new ExportField("outsidePerimetersFirst"),
				[SettingsKey.output_only_first_layer] = new ExportField("outputOnlyFirstLayer"),
				[SettingsKey.retract_when_changing_islands] = new ExportField("retractWhenChangingIslands"),
				[SettingsKey.support_material_create_perimeter] = new ExportField("generateSupportPerimeter"),
				[SettingsKey.expand_thin_walls] = new ExportField("expandThinWalls"),
				[SettingsKey.merge_overlapping_lines] = new ExportField("MergeOverlappingLines"),
				[SettingsKey.fill_thin_gaps] = new ExportField("fillThinGaps"),
				[SettingsKey.spiral_vase] = new ExportField("continuousSpiralOuterPerimeter"),
				[SettingsKey.start_gcode] = new ExportField("startCode"),
				[SettingsKey.layer_gcode] = new ExportField("layerChangeCode"),
				[SettingsKey.fill_density] = new ExportField("infillPercent"),
				[SettingsKey.perimeter_start_end_overlap] = new ExportField("perimeterStartEndOverlapRatio"),
				[SettingsKey.raft_extruder] = new ExportField("raftExtruder"),
				[SettingsKey.support_material_extruder] = new ExportField("supportExtruder"),
				[SettingsKey.support_material_interface_extruder] = new ExportField("supportInterfaceExtruder"),
				// Skirt settings
				[SettingsKey.skirts] = new ExportField("numberOfSkirtLoops"),
				[SettingsKey.skirt_distance] = new ExportField("skirtDistanceFromObject"),
				[SettingsKey.min_skirt_length] = new ExportField("skirtMinLength"),
				// Brim settings
				[SettingsKey.brims] = new ExportField("numberOfBrimLoops")
			};

			matterSliceSettingNames = new HashSet<string>(this.Exports.Select(m => m.Key));
		}

		public string Name => "MatterSlice";

		public void WriteSliceSettingsFile(string outputFilename, IEnumerable<string> rawLines, PrinterSettings settings)
		{
			using (var sliceSettingsFile = new StreamWriter(outputFilename))
			{
				foreach (var (key, exportField) in this.Exports.Select(kvp => (kvp.Key, kvp.Value)))
				{
					string result = settings.ResolveValue(key);
					if (result != null)
					{
						sliceSettingsFile.WriteLine("{0} = {1}", key, result);
					}
				}

				foreach (var line in rawLines)
				{
					sliceSettingsFile.WriteLine(line);
				}
			}
		}

		public bool MapContains(string canonicalSettingsName)
		{
			return matterSliceSettingNames.Contains(canonicalSettingsName)
				|| PrinterSettings.ApplicationLevelSettings.Contains(canonicalSettingsName);
		}
	}
}