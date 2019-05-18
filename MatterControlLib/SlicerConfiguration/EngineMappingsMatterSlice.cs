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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.MatterControl.SlicerConfiguration.MappingClasses;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class EngineMappingsMatterSlice
	{
		/// <summary>
		/// Application level settings control MatterControl behaviors but aren't used or passed through to the slice engine. Putting settings
		/// in this list ensures they show up for all slice engines and the lack of a MappedSetting for the engine guarantees that it won't pass
		/// through into the slicer config file.
		/// </summary>
		private readonly HashSet<string> applicationLevelSettings = new HashSet<string>()
		{
			SettingsKey.enable_fan,
			SettingsKey.extruder_wipe_temperature,
			SettingsKey.extruders_share_temperature,
			SettingsKey.first_layer_bed_temperature,
			SettingsKey.g0,
			SettingsKey.layer_to_pause,
			SettingsKey.selector_ip_address,
			SettingsKey.solid_shell,
			SettingsKey.z_homes_to_max,
			// TODO: merge the items below into the list above after some validation - setting that weren't previously mapped to Cura but probably should be.
			SettingsKey.auto_connect,
			SettingsKey.auto_release_motors,
			SettingsKey.backup_firmware_before_update,
			SettingsKey.baud_rate,
			SettingsKey.bed_remove_part_temperature,
			SettingsKey.bed_shape,
			SettingsKey.bed_size,
			SettingsKey.bed_temperature,
			SettingsKey.before_toolchange_gcode,
			SettingsKey.before_toolchange_gcode_1,
			SettingsKey.toolchange_gcode,
			SettingsKey.toolchange_gcode_1,
			SettingsKey.build_height,
			SettingsKey.cancel_gcode,
			SettingsKey.com_port,
			SettingsKey.connect_gcode,
			SettingsKey.created_date,
			SettingsKey.emulate_endstops,
			SettingsKey.enable_line_splitting,
			SettingsKey.enable_network_printing,
			SettingsKey.enable_retractions,
			SettingsKey.enable_sailfish_communication,
			SettingsKey.filament_cost,
			SettingsKey.filament_density,
			SettingsKey.filament_has_been_loaded,
			SettingsKey.filament_1_has_been_loaded,
			SettingsKey.filament_runout_sensor,
			SettingsKey.has_fan,
			SettingsKey.has_hardware_leveling,
			SettingsKey.has_heated_bed,
			SettingsKey.has_power_control,
			SettingsKey.has_sd_card_reader,
			SettingsKey.has_z_probe,
			SettingsKey.has_z_servo,
			SettingsKey.heat_extruder_before_homing,
			SettingsKey.inactive_cool_down,
			SettingsKey.include_firmware_updater,
			SettingsKey.insert_filament_markdown2,
			SettingsKey.insert_filament_1_markdown,
			SettingsKey.ip_address,
			SettingsKey.ip_port,
			SettingsKey.laser_speed_025,
			SettingsKey.laser_speed_100,
			SettingsKey.level_x_carriage_markdown,
			SettingsKey.leveling_sample_points,
			SettingsKey.load_filament_length,
			SettingsKey.load_filament_speed,
			SettingsKey.make,
			SettingsKey.model,
			SettingsKey.t0_inset,
			SettingsKey.t1_inset,
			SettingsKey.number_of_first_layers,
			SettingsKey.extruder_offset,
			SettingsKey.pause_gcode,
			SettingsKey.print_center,
			SettingsKey.print_leveling_probe_start,
			SettingsKey.print_leveling_required_to_print,
			SettingsKey.print_leveling_solution,
			SettingsKey.print_time_estimate_multiplier,
			SettingsKey.printer_name,
			SettingsKey.probe_has_been_calibrated,
			SettingsKey.probe_offset,
			SettingsKey.probe_offset_sample_point,
			SettingsKey.progress_reporting,
			SettingsKey.read_regex,
			SettingsKey.recover_first_layer_speed,
			SettingsKey.recover_is_enabled,
			SettingsKey.recover_position_before_z_home,
			SettingsKey.resume_gcode,
			SettingsKey.running_clean_markdown2,
			SettingsKey.running_clean_1_markdown,
			SettingsKey.seconds_to_reheat,
			SettingsKey.send_with_checksum,
			SettingsKey.show_reset_connection,
			SettingsKey.sla_printer,
			SettingsKey.t1_extrusion_move_speed_multiplier,
			SettingsKey.temperature,
			SettingsKey.temperature1,
			SettingsKey.temperature2,
			SettingsKey.temperature3,
			SettingsKey.trim_filament_markdown,
			SettingsKey.unload_filament_length,
			SettingsKey.use_z_probe,
			SettingsKey.validate_layer_height,
			SettingsKey.write_regex,
			SettingsKey.xy_offsets_have_been_calibrated,
			SettingsKey.z_offset,
			SettingsKey.z_probe_samples,
			SettingsKey.z_servo_depolyed_angle,
			SettingsKey.z_servo_retracted_angle,
		};

		public List<MappedSetting> MappedSettings { get; private set; }

		private readonly HashSet<string> matterSliceSettingNames;

		// Singleton use only - prevent external construction
		public EngineMappingsMatterSlice(PrinterConfig printer)
		{
			MappedSettings = new List<MappedSetting>()
			{
				new AsCountOrDistance(printer, SettingsKey.bottom_solid_layers, "numberOfBottomLayers", SettingsKey.layer_height),
				new AsCountOrDistance(printer, SettingsKey.perimeters, "numberOfPerimeters", SettingsKey.nozzle_diameter),
				new AsCountOrDistance(printer, SettingsKey.raft_extra_distance_around_part, "raftExtraDistanceAroundPart", SettingsKey.nozzle_diameter),
				new AsCountOrDistance(printer, SettingsKey.support_material_interface_layers, "supportInterfaceLayers", SettingsKey.layer_height),
				new AsCountOrDistance(printer, SettingsKey.top_solid_layers, "numberOfTopLayers", SettingsKey.layer_height),
				new AsPercentOfReferenceOrDirect(printer, SettingsKey.external_perimeter_extrusion_width, "outsidePerimeterExtrusionWidth", SettingsKey.nozzle_diameter),
				new OverrideSpeedOnSlaPrinters(printer, SettingsKey.external_perimeter_speed, "outsidePerimeterSpeed", SettingsKey.perimeter_speed),
				new AsPercentOfReferenceOrDirect(printer, SettingsKey.first_layer_speed, "firstLayerSpeed", SettingsKey.infill_speed),
				new AsCountOrDistance(printer, SettingsKey.number_of_first_layers, "numberOfFirstLayers", SettingsKey.layer_height),
				new AsPercentOfReferenceOrDirect(printer, SettingsKey.raft_print_speed, "raftPrintSpeed", SettingsKey.infill_speed),
				new OverrideSpeedOnSlaPrinters(printer, SettingsKey.top_solid_infill_speed, "topInfillSpeed", SettingsKey.infill_speed),
				new AsPercentOfReferenceOrDirect(printer, SettingsKey.first_layer_extrusion_width, "firstLayerExtrusionWidth", SettingsKey.nozzle_diameter),
				new AsPercentOfReferenceOrDirect(printer, SettingsKey.first_layer_height, "firstLayerThickness", SettingsKey.layer_height),
				new GCodeForSlicer(printer, SettingsKey.end_gcode, "endCode"),
				new MapFirstValue(printer, SettingsKey.retract_before_travel, "minimumTravelToCauseRetraction"),
				new RetractionLength(printer, SettingsKey.retract_length, "retractionOnTravel"),
				new MapFirstValue(printer, SettingsKey.retract_lift, "retractionZHop"),
				new MapFirstValue(printer, SettingsKey.retract_restart_extra, "unretractExtraExtrusion"),
				new MapFirstValue(printer, SettingsKey.retract_restart_extra_time_to_apply, "retractRestartExtraTimeToApply"),
				new MapFirstValue(printer, SettingsKey.retract_speed, "retractionSpeed"),
				new OverrideSpeedOnSlaPrinters(printer, SettingsKey.bridge_speed, "bridgeSpeed", SettingsKey.infill_speed),
				new OverrideSpeedOnSlaPrinters(printer, SettingsKey.air_gap_speed, "airGapSpeed", SettingsKey.infill_speed),
				new OverrideSpeedOnSlaPrinters(printer, SettingsKey.bottom_infill_speed, "bottomInfillSpeed", SettingsKey.infill_speed),
				new MappedToBoolString(printer, SettingsKey.bridge_over_infill, "bridgeOverInfill"),
				new AsPercentOrDirect(printer, SettingsKey.extrusion_multiplier, "extrusionMultiplier"),
				new MappedSetting(printer, SettingsKey.fill_angle, "infillStartingAngle"),
				new AsPercentOfReferenceOrDirect(printer, SettingsKey.infill_overlap_perimeter, "infillExtendIntoPerimeter", SettingsKey.nozzle_diameter, change0ToReference: false),
				new OverrideSpeedOnSlaPrinters(printer, SettingsKey.infill_speed, "infillSpeed", SettingsKey.infill_speed),
				new MappedSetting(printer, SettingsKey.infill_type, "infillType"),
				new MappedSetting(printer, SettingsKey.min_extrusion_before_retract, "minimumExtrusionBeforeRetraction"),
				new MappedSetting(printer, SettingsKey.min_print_speed, "minimumPrintingSpeed"),
				new OverrideSpeedOnSlaPrinters(printer, SettingsKey.perimeter_speed, "insidePerimetersSpeed", SettingsKey.infill_speed),
				new MappedSetting(printer, SettingsKey.raft_air_gap, "raftAirGap"),
				new MappedSetting(printer, SettingsKey.max_acceleration, "maxAcceleration"),
				new MappedSetting(printer, SettingsKey.max_velocity, "maxVelocity"),
				new MappedSetting(printer, SettingsKey.jerk_velocity, "jerkVelocity"),
				new ScaledSingleNumber(printer, SettingsKey.print_time_estimate_multiplier, "printTimeEstimateMultiplier", .01),
				// fan settings
				new MappedFanSpeedSetting(printer, SettingsKey.min_fan_speed, "fanSpeedMinPercent"),
				new MappedSetting(printer, SettingsKey.coast_at_end_distance, "coastAtEndDistance"),
				new MappedSetting(printer, SettingsKey.min_fan_speed_layer_time, "minFanSpeedLayerTime"),
				new MappedFanSpeedSetting(printer, SettingsKey.max_fan_speed, "fanSpeedMaxPercent"),
				new MappedSetting(printer, SettingsKey.max_fan_speed_layer_time, "maxFanSpeedLayerTime"),
				new MappedFanSpeedSetting(printer, SettingsKey.bridge_fan_speed, "bridgeFanSpeedPercent"),
				new MappedSetting(printer, SettingsKey.disable_fan_first_layers, "firstLayerToAllowFan"),
				// end fan
				new MappedSetting(printer, SettingsKey.retract_length_tool_change, "retractionOnExtruderSwitch"),
				new MappedSetting(printer, SettingsKey.retract_restart_extra_toolchange, "unretractExtraOnExtruderSwitch"),
				new MappedToBoolString(printer, SettingsKey.reset_long_extrusion, "resetLongExtrusion"),
				new MappedSetting(printer, SettingsKey.slowdown_below_layer_time, "minimumLayerTimeSeconds"),
				new MappedSetting(printer, SettingsKey.support_air_gap, "supportAirGap"),
				new MappedSetting(printer, SettingsKey.support_material_infill_angle, "supportInfillStartingAngle"),
				new MappedSetting(printer, SettingsKey.support_material_spacing, "supportLineSpacing"),
				new OverrideSpeedOnSlaPrinters(printer, SettingsKey.support_material_speed, "supportMaterialSpeed", "infill_speed"),
				new OverrideSpeedOnSlaPrinters(printer, SettingsKey.interface_layer_speed, "interfaceLayerSpeed", "infill_speed"),
				new MappedSetting(printer, SettingsKey.support_material_xy_distance, "supportXYDistanceFromObject"),
				new MappedSetting(printer, SettingsKey.support_type, "supportType"),
				new MappedSetting(printer, SettingsKey.travel_speed, "travelSpeed"),
				new MappedSetting(printer, SettingsKey.wipe_shield_distance, "wipeShieldDistanceFromObject"),
				new MappedSetting(printer, SettingsKey.wipe_tower_size, "wipeTowerSize"),
				new MappedSetting(printer, SettingsKey.filament_diameter, "filamentDiameter"),
				new MappedSetting(printer, SettingsKey.layer_height, "layerThickness"),
				new MappedSetting(printer, SettingsKey.nozzle_diameter, "extrusionWidth"),
				new MappedSetting(printer, SettingsKey.extruder_count, "extruderCount"),
				new MappedToBoolString(printer, SettingsKey.avoid_crossing_perimeters, "avoidCrossingPerimeters"),
				new MappedToBoolString(printer, SettingsKey.create_raft, "enableRaft"),
				new MappedToBoolString(printer, SettingsKey.external_perimeters_first, "outsidePerimetersFirst"),
				new MappedToBoolString(printer, SettingsKey.output_only_first_layer, "outputOnlyFirstLayer"),
				new MappedToBoolString(printer, SettingsKey.retract_when_changing_islands, "retractWhenChangingIslands"),
				new MappedToBoolString(printer, SettingsKey.support_material_create_perimeter, "generateSupportPerimeter"),
				new MappedToBoolString(printer, SettingsKey.expand_thin_walls, "expandThinWalls"),
				new MappedToBoolString(printer, SettingsKey.merge_overlapping_lines, "MergeOverlappingLines"),
				new MappedToBoolString(printer, SettingsKey.fill_thin_gaps, "fillThinGaps"),
				new MappedToBoolString(printer, SettingsKey.spiral_vase, "continuousSpiralOuterPerimeter"),
				new MapStartGCode(printer, SettingsKey.start_gcode, "startCode", true),
				new MapLayerChangeGCode(printer, SettingsKey.layer_gcode, "layerChangeCode"),
				new ScaledSingleNumber(printer, SettingsKey.fill_density, "infillPercent", 100),
				new ScaledSingleNumber(printer, SettingsKey.perimeter_start_end_overlap, "perimeterStartEndOverlapRatio", .01),
				new ValuePlusConstant(printer, SettingsKey.raft_extruder, "raftExtruder", -1),
				new ValuePlusConstant(printer, SettingsKey.support_material_extruder, "supportExtruder", -1),
				new ValuePlusConstant(printer, SettingsKey.support_material_interface_extruder, "supportInterfaceExtruder", -1),
				// Skirt settings
				new MappedSkirtLoopsSetting(printer, SettingsKey.skirts, "numberOfSkirtLoops", SettingsKey.nozzle_diameter),
				new MappedSetting(printer, SettingsKey.skirt_distance, "skirtDistanceFromObject"),
				new SkirtLengthMapping(printer, SettingsKey.min_skirt_length, "skirtMinLength"),
				// Brim settings
				new MappedBrimLoopsSetting(printer, "brims", "numberOfBrimLoops", SettingsKey.nozzle_diameter),
			};

			matterSliceSettingNames = new HashSet<string>(MappedSettings.Select(m => m.CanonicalSettingsName));
		}

		public string Name => "MatterSlice";

		public void WriteSliceSettingsFile(string outputFilename, IEnumerable<string> rawLines)
		{
			using (var sliceSettingsFile = new StreamWriter(outputFilename))
			{
				foreach (MappedSetting mappedSetting in MappedSettings)
				{
					if (mappedSetting.Value != null)
					{
						sliceSettingsFile.WriteLine("{0} = {1}".FormatWith(mappedSetting.ExportedName, mappedSetting.Value));
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
				|| applicationLevelSettings.Contains(canonicalSettingsName);
		}
	}
}