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
using MatterHackers.MatterControl.SlicerConfiguration.MappingClasses;
using MatterHackers.VectorMath;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class EngineMappingsMatterSlice
	{
		/// <summary>
		/// Application level settings control MatterControl behaviors but aren't used or passed through to the slice engine. Putting settings
		/// in this list ensures they show up for all slice engines and the lack of a MappedSetting for the engine guarantees that it won't pass
		/// through into the slicer config file
		/// </summary>
		protected HashSet<string> applicationLevelSettings = new HashSet<string>()
		{
			"enable_fan",
			"extruder_wipe_temperature",
			"extruders_share_temperature",
			"g0",
			"layer_to_pause",
			"selector_ip_address",
			"solid_shell",
			"z_homes_to_max",
			// TODO: merge the items below into the list above after some validation - setting that weren't previously mapped to Cura but probably should be.
			SettingsKey.auto_connect,
			SettingsKey.auto_release_motors,
			SettingsKey.backup_firmware_before_update,
			SettingsKey.baud_rate,
			SettingsKey.bed_remove_part_temperature,
			SettingsKey.bed_shape,
			SettingsKey.bed_size,
			SettingsKey.bed_temperature,
			SettingsKey.build_height,
			SettingsKey.cancel_gcode,
			SettingsKey.com_port,
			SettingsKey.connect_gcode,
			SettingsKey.created_date,
			SettingsKey.enable_line_splitting,
			SettingsKey.enable_network_printing,
			SettingsKey.enable_retractions,
			SettingsKey.enable_sailfish_communication,
			SettingsKey.filament_cost,
			SettingsKey.filament_density,
			SettingsKey.filament_has_been_loaded,
			SettingsKey.filament_runout_sensor,
			SettingsKey.has_fan,
			SettingsKey.has_hardware_leveling,
			SettingsKey.has_heated_bed,
			SettingsKey.has_power_control,
			SettingsKey.has_sd_card_reader,
			SettingsKey.has_z_probe,
			SettingsKey.has_z_servo,
			SettingsKey.heat_extruder_before_homing,
			SettingsKey.include_firmware_updater,
			SettingsKey.insert_filament_markdown2,
			SettingsKey.ip_address,
			SettingsKey.ip_port,
			SettingsKey.laser_speed_025,
			SettingsKey.laser_speed_100,
			SettingsKey.leveling_sample_points,
			SettingsKey.load_filament_length,
			SettingsKey.load_filament_speed,
			SettingsKey.make,
			SettingsKey.model,
			SettingsKey.number_of_first_layers,
			SettingsKey.pause_gcode,
			SettingsKey.print_center,
			SettingsKey.print_leveling_probe_start,
			SettingsKey.print_leveling_required_to_print,
			SettingsKey.print_leveling_solution,
			SettingsKey.print_time_estimate_multiplier,
			SettingsKey.printer_name,
			SettingsKey.probe_has_been_calibrated,
			SettingsKey.probe_offset_sample_point,
			SettingsKey.progress_reporting,
			SettingsKey.read_regex,
			SettingsKey.recover_first_layer_speed,
			SettingsKey.recover_is_enabled,
			SettingsKey.recover_position_before_z_home,
			SettingsKey.resume_gcode,
			SettingsKey.running_clean_markdown2,
			SettingsKey.send_with_checksum,
			SettingsKey.show_reset_connection,
			SettingsKey.sla_printer,
			SettingsKey.temperature,
			SettingsKey.trim_filament_markdown,
			SettingsKey.unload_filament_length,
			SettingsKey.use_z_probe,
			SettingsKey.validate_layer_height,
			SettingsKey.write_regex,
			SettingsKey.z_probe_samples,
			SettingsKey.z_probe_xy_offset,
			SettingsKey.z_probe_z_offset,
			SettingsKey.z_servo_depolyed_angle,
			SettingsKey.z_servo_retracted_angle,
		};

		public List<MappedSetting> MappedSettings { get; private set; }
		private HashSet<string> matterSliceSettingNames;

		// Singleton use only - prevent external construction
		public EngineMappingsMatterSlice(PrinterConfig printer)
		{
			MappedSettings = new List<MappedSetting>()
			{
				new AsCountOrDistance(printer, "bottom_solid_layers", "numberOfBottomLayers", SettingsKey.layer_height),
				new AsCountOrDistance(printer, "perimeters", "numberOfPerimeters", SettingsKey.nozzle_diameter),
				new AsCountOrDistance(printer, "raft_extra_distance_around_part", "raftExtraDistanceAroundPart", SettingsKey.nozzle_diameter),
				new AsCountOrDistance(printer, "support_material_interface_layers", "supportInterfaceLayers", SettingsKey.layer_height),
				new AsCountOrDistance(printer, "top_solid_layers", "numberOfTopLayers", SettingsKey.layer_height),
				new AsPercentOfReferenceOrDirect(printer, SettingsKey.external_perimeter_extrusion_width, "outsidePerimeterExtrusionWidth", SettingsKey.nozzle_diameter),
				new OverrideSpeedOnSlaPrinters(printer, "external_perimeter_speed", "outsidePerimeterSpeed", "perimeter_speed"),
				new AsPercentOfReferenceOrDirect(printer, SettingsKey.first_layer_speed, "firstLayerSpeed", "infill_speed"),
				new AsCountOrDistance(printer, SettingsKey.number_of_first_layers, "numberOfFirstLayers", SettingsKey.layer_height),
				new AsPercentOfReferenceOrDirect(printer, "raft_print_speed", "raftPrintSpeed", "infill_speed"),
				new OverrideSpeedOnSlaPrinters(printer, SettingsKey.top_solid_infill_speed, "topInfillSpeed", "infill_speed"),
				new AsPercentOfReferenceOrDirect(printer, SettingsKey.first_layer_extrusion_width, "firstLayerExtrusionWidth", SettingsKey.nozzle_diameter),
				new AsPercentOfReferenceOrDirect(printer, SettingsKey.first_layer_height, "firstLayerThickness", SettingsKey.layer_height),
				new ExtruderOffsets(printer, "extruder_offset", "extruderOffsets"),
				new GCodeForSlicer(printer, SettingsKey.end_gcode, "endCode"),
				new GCodeForSlicer(printer, "before_toolchange_gcode", "beforeToolchangeCode"),
				new GCodeForSlicer(printer, "toolchange_gcode", "toolChangeCode"),
				new GCodeForSlicer(printer, "before_toolchange_gcode_1", "beforeToolchangeCode1"),
				new GCodeForSlicer(printer, "toolchange_gcode_1", "toolChangeCode1"),
				new MapFirstValue(printer, "retract_before_travel", "minimumTravelToCauseRetraction"),
				new RetractionLength(printer, "retract_length", "retractionOnTravel"),
				new MapFirstValue(printer, "retract_lift", "retractionZHop"),
				new MapFirstValue(printer, "retract_restart_extra", "unretractExtraExtrusion"),
				new MapFirstValue(printer, "retract_restart_extra_time_to_apply", "retractRestartExtraTimeToApply"),
				new MapFirstValue(printer, "retract_speed", "retractionSpeed"),
				new OverrideSpeedOnSlaPrinters(printer, "bridge_speed", "bridgeSpeed", "infill_speed"),
				new OverrideSpeedOnSlaPrinters(printer, "air_gap_speed", "airGapSpeed", "infill_speed"),
				new OverrideSpeedOnSlaPrinters(printer, "bottom_infill_speed", "bottomInfillSpeed", "infill_speed"),
				new MappedToBoolString(printer, "bridge_over_infill", "bridgeOverInfill"),
				new MappedSetting(printer, "extrusion_multiplier", "extrusionMultiplier"),
				new MappedSetting(printer, "fill_angle", "infillStartingAngle"),
				new AsPercentOfReferenceOrDirect(printer, SettingsKey.infill_overlap_perimeter, "infillExtendIntoPerimeter", SettingsKey.nozzle_diameter, change0ToReference: false),
				new OverrideSpeedOnSlaPrinters(printer, "infill_speed", "infillSpeed", "infill_speed"),
				new MappedSetting(printer, SettingsKey.infill_type, "infillType"),
				new MappedSetting(printer, "min_extrusion_before_retract", "minimumExtrusionBeforeRetraction"),
				new MappedSetting(printer, "min_print_speed", "minimumPrintingSpeed"),
				new OverrideSpeedOnSlaPrinters(printer, "perimeter_speed", "insidePerimetersSpeed", "infill_speed"),
				new MappedSetting(printer, "raft_air_gap", "raftAirGap"),
				new MappedSetting(printer, SettingsKey.max_acceleration, "maxAcceleration"),
				new MappedSetting(printer, SettingsKey.max_velocity, "maxVelocity"),
				new MappedSetting(printer, SettingsKey.jerk_velocity, "jerkVelocity"),
				new ScaledSingleNumber(printer, SettingsKey.print_time_estimate_multiplier, "printTimeEstimateMultiplier", .01),
				// fan settings
				new MappedFanSpeedSetting(printer, SettingsKey.min_fan_speed, "fanSpeedMinPercent"),
				new MappedSetting(printer, "coast_at_end_distance", "coastAtEndDistance"),
				new MappedSetting(printer, "min_fan_speed_layer_time", "minFanSpeedLayerTime"),
				new MappedFanSpeedSetting(printer, SettingsKey.max_fan_speed, "fanSpeedMaxPercent"),
				new MappedSetting(printer, "max_fan_speed_layer_time", "maxFanSpeedLayerTime"),
				new MappedFanSpeedSetting(printer, "bridge_fan_speed", "bridgeFanSpeedPercent"),
				new MappedSetting(printer, "disable_fan_first_layers", "firstLayerToAllowFan"),
				// end fan
				new MappedSetting(printer, "retract_length_tool_change", "retractionOnExtruderSwitch"),
				new MappedSetting(printer, "retract_restart_extra_toolchange", "unretractExtraOnExtruderSwitch"),
				new MappedToBoolString(printer, "reset_long_extrusion", "resetLongExtrusion"),
				new MappedSetting(printer, "slowdown_below_layer_time", "minimumLayerTimeSeconds"),
				new MappedSetting(printer, "support_air_gap", "supportAirGap"),
				new MappedSetting(printer, "support_material_infill_angle", "supportInfillStartingAngle"),
				new MappedSetting(printer, "support_material_percent", "supportPercent"),
				new MappedSetting(printer, "support_material_spacing", "supportLineSpacing"),
				new OverrideSpeedOnSlaPrinters(printer, "support_material_speed", "supportMaterialSpeed", "infill_speed"),
				new MappedSetting(printer, "support_material_xy_distance", "supportXYDistanceFromObject"),
				new MappedSetting(printer, "support_type", "supportType"),
				new MappedSetting(printer, "travel_speed", "travelSpeed"),
				new MappedSetting(printer, "wipe_shield_distance", "wipeShieldDistanceFromObject"),
				new MappedSetting(printer, "wipe_tower_size", "wipeTowerSize"),
				new MappedSetting(printer, "z_offset", "zOffset"),
				new MappedSetting(printer, SettingsKey.filament_diameter, "filamentDiameter"),
				new MappedSetting(printer, SettingsKey.layer_height, "layerThickness"),
				new MappedSetting(printer, SettingsKey.nozzle_diameter, "extrusionWidth"),
				new MappedSetting(printer, "extruder_count", "extruderCount"),
				new MappedToBoolString(printer, "avoid_crossing_perimeters", "avoidCrossingPerimeters"),
				new MappedToBoolString(printer, "create_raft", "enableRaft"),
				new MappedToBoolString(printer, "external_perimeters_first", "outsidePerimetersFirst"),
				new MappedToBoolString(printer, "output_only_first_layer", "outputOnlyFirstLayer"),
				new MappedToBoolString(printer, "retract_when_changing_islands", "retractWhenChangingIslands"),
				new MappedToBoolString(printer, "support_material", "generateSupport"),
				new MappedToBoolString(printer, "support_material_create_internal_support", "generateInternalSupport"),
				new MappedToBoolString(printer, "support_material_create_perimeter", "generateSupportPerimeter"),
				new MappedToBoolString(printer, SettingsKey.expand_thin_walls, "expandThinWalls"),
				new MappedToBoolString(printer, SettingsKey.merge_overlapping_lines, "MergeOverlappingLines"),
				new MappedToBoolString(printer, SettingsKey.fill_thin_gaps, "fillThinGaps"),
				new MappedToBoolString(printer, SettingsKey.spiral_vase, "continuousSpiralOuterPerimeter"),
				new MapStartGCode(printer, SettingsKey.start_gcode, "startCode", true),
				new MapLayerChangeGCode(printer, "layer_gcode", "layerChangeCode"),
				new ScaledSingleNumber(printer, "fill_density", "infillPercent", 100),
				new ScaledSingleNumber(printer, SettingsKey.perimeter_start_end_overlap, "perimeterStartEndOverlapRatio", .01),
				new SupportExtrusionWidth(printer, "support_material_extrusion_width","supportExtrusionPercent"),
				new ValuePlusConstant(printer, "raft_extruder", "raftExtruder", -1),
				new ValuePlusConstant(printer, "support_material_extruder", "supportExtruder", -1),
				new ValuePlusConstant(printer, "support_material_interface_extruder", "supportInterfaceExtruder", -1),
				// Skirt settings
				new MappedSkirtLoopsSetting(printer, "skirts", "numberOfSkirtLoops", SettingsKey.nozzle_diameter),
				new MappedSetting(printer, "skirt_distance", "skirtDistanceFromObject"),
				new SkirtLengthMapping(printer, "min_skirt_length", "skirtMinLength"),
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

				foreach(var line in rawLines)
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