/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public static class SliceSettingsLayouts
	{
		private static (string categoryName, (string groupName, string[] settings)[] groups)[] sliceSettings;
		
		private static (string categoryName, (string groupName, string[] settings)[] groups)[] printerSettings;
		public static (string categoryName, (string groupName, string[] settings)[] groups)[] SliceSettings()
		{
			if (sliceSettings == null)
			{
				sliceSettings = new[]
				{
					("General", new[]
					{
						("General", new[]
						{
							SettingsKey.layer_height,
							SettingsKey.first_layer_height,
							SettingsKey.perimeters,
							SettingsKey.top_solid_layers,
							SettingsKey.bottom_solid_layers,
							SettingsKey.fill_density,
							SettingsKey.infill_type,
							// add settings specific to SLA
							SettingsKey.sla_layer_height,
							SettingsKey.sla_decend_speed,
						}),
						("Normal Layers", new[] // this is for SLA resin printing
						{
							SettingsKey.sla_exposure_time,
							SettingsKey.sla_lift_distance,
							SettingsKey.sla_lift_speed,
							SettingsKey.sla_min_off_time,
						}),
						("Base Layers", new[] // this is for SLA resin printing
						{
							SettingsKey.sla_base_layers,
							SettingsKey.sla_base_exposure_time,
							SettingsKey.sla_base_lift_distance,
							SettingsKey.sla_base_lift_speed,
							SettingsKey.sla_base_min_off_time,
						}),
						("Layers / Surface", new[]
						{
							SettingsKey.avoid_crossing_perimeters,
							SettingsKey.avoid_crossing_max_ratio,
							SettingsKey.external_perimeters_first,
							SettingsKey.perimeter_start_end_overlap,
							SettingsKey.merge_overlapping_lines,
							SettingsKey.seam_placement,
							SettingsKey.expand_thin_walls,
							SettingsKey.coast_at_end_distance,
							SettingsKey.monotonic_solid_infill,
							SettingsKey.fuzzy_thickness,
							SettingsKey.fuzzy_frequency,
						}),
						("Infill", new[]
						{
							SettingsKey.fill_angle,
							SettingsKey.infill_overlap_perimeter,
							SettingsKey.fill_thin_gaps,
						}),
						("Extruder Change", new[]
						{
							SettingsKey.wipe_shield_distance,
							SettingsKey.wipe_tower_size,
							SettingsKey.wipe_tower_perimeters_per_extruder,
						}),
						("Advanced", new[]
						{
							SettingsKey.spiral_vase,
							SettingsKey.layer_to_pause,
						}),
					}),
					("Speed", new[]
					{
						("Laser Speed", new[]
						{
							SettingsKey.laser_speed_025,
							SettingsKey.laser_speed_100,
						}),
						("Infill Speeds", new[]
						{
							SettingsKey.first_layer_speed,
							SettingsKey.infill_speed,
							SettingsKey.top_solid_infill_speed,
							SettingsKey.raft_print_speed,
						}),
						("Perimeter Speeds", new[]
						{
							SettingsKey.perimeter_speed,
							SettingsKey.external_perimeter_speed,
							SettingsKey.perimeter_acceleration,
							SettingsKey.default_acceleration,
						}),
						("Other Speeds", new[]
						{
							SettingsKey.support_material_speed,
							SettingsKey.interface_layer_speed,
							SettingsKey.air_gap_speed,
							SettingsKey.bridge_speed,
							SettingsKey.travel_speed,
							SettingsKey.number_of_first_layers,
							SettingsKey.bridge_over_infill,
							SettingsKey.t1_extrusion_move_speed_multiplier,
						}),
						("Speed Overrides", new []
						{
							SettingsKey.max_print_speed,
						}),
						("Cooling", new[]
						{
							SettingsKey.slowdown_below_layer_time,
							SettingsKey.min_print_speed,
						}),
					}),
					("Adhesion", new[]
					{
						("Bed", new []
						{
							SettingsKey.bed_surface,
						}),
						("Skirt", new[]
						{
							SettingsKey.create_skirt,
							SettingsKey.skirts,
							SettingsKey.skirt_distance,
							SettingsKey.min_skirt_length,
						}),
						("Raft", new[]
						{
							SettingsKey.create_raft,
							SettingsKey.raft_extra_distance_around_part,
							SettingsKey.raft_air_gap,
							SettingsKey.raft_extruder,
							// add settings specific to SLA
							SettingsKey.sla_create_raft,
						}),
						("Brim", new[]
						{
							SettingsKey.create_brim,
							SettingsKey.brims,
							SettingsKey.brims_layers,
							SettingsKey.brim_extruder,
						}),
					}),
					("Support", new[]
					{
						("General", new[]
						{
							SettingsKey.support_material_create_perimeter,
							SettingsKey.support_material_interface_layers,
							SettingsKey.support_material_xy_distance,
							SettingsKey.support_air_gap,
							SettingsKey.support_type,
							SettingsKey.support_material_spacing,
							SettingsKey.support_material_infill_angle,
							SettingsKey.support_material_extruder,
							SettingsKey.support_material_interface_extruder,
						}),
						("Automatic", new[]
						{
							SettingsKey.create_per_layer_support,
							SettingsKey.create_per_layer_internal_support,
							SettingsKey.support_percent,
							SettingsKey.support_grab_distance,
							// add settings specific to SLA
							SettingsKey.sla_auto_support,
						}),
					}),
					("Resin", new[]
					{
						("Properties", new []
						{
							SettingsKey.resin_density,
							SettingsKey.resin_cost,
						}),
					}),
					("Filament", new[]
					{
						("Properties", new[]
						{
							SettingsKey.material_color,
							SettingsKey.material_color_1,
							SettingsKey.material_color_2,
							SettingsKey.material_color_3,
							SettingsKey.filament_diameter,
							SettingsKey.filament_density,
							SettingsKey.filament_cost,
							SettingsKey.temperature,
							SettingsKey.temperature1,
							SettingsKey.temperature2,
							SettingsKey.temperature3,
							SettingsKey.bed_temperature,
							SettingsKey.bed_temperature_blue_tape,
							SettingsKey.bed_temperature_buildtak,
							SettingsKey.bed_temperature_garolite,
							SettingsKey.bed_temperature_glass,
							SettingsKey.bed_temperature_kapton,
							SettingsKey.bed_temperature_pei,
							SettingsKey.bed_temperature_pp,
							SettingsKey.inactive_cool_down,
							SettingsKey.seconds_to_reheat,
						}),
						("Fan", new[]
						{
							SettingsKey.enable_fan,
							SettingsKey.min_fan_speed_layer_time,
							SettingsKey.max_fan_speed_layer_time,
							SettingsKey.min_fan_speed,
							SettingsKey.max_fan_speed,
							SettingsKey.bridge_fan_speed,
							SettingsKey.disable_fan_first_layers,
							SettingsKey.min_fan_speed_absolute,
						}),
						("Retraction", new[]
						{
							SettingsKey.enable_retractions,
							SettingsKey.retract_length,
							SettingsKey.retract_restart_extra,
							SettingsKey.retract_restart_extra_time_to_apply,
							SettingsKey.retract_speed,
							SettingsKey.retract_lift,
							SettingsKey.retract_when_changing_islands,
							SettingsKey.min_extrusion_before_retract,
							SettingsKey.retract_before_travel,
							SettingsKey.retract_before_travel_avoid,
							SettingsKey.retract_length_tool_change,
							SettingsKey.retract_restart_extra_toolchange,
						}),
						("Advanced", new[]
						{
							SettingsKey.extruder_wipe_temperature,
							SettingsKey.bed_remove_part_temperature,
							SettingsKey.extrusion_multiplier,
							SettingsKey.first_layer_extrusion_width,
							SettingsKey.external_perimeter_extrusion_width,
						}),
					}),
				};
			}

			return sliceSettings;
		}

		public static (string categoryName, (string groupName, string[] settings)[] groups)[] PrinterSettings()
		{
			if (printerSettings == null)
			{
				printerSettings = new (string categoryName, (string groupName, string[] settings)[] groups)[]
				{
					("General", new[]
					{
						("General", new[]
						{
							SettingsKey.make,
							SettingsKey.model,
							SettingsKey.auto_connect,
							SettingsKey.baud_rate,
							SettingsKey.com_port,
							SettingsKey.selector_ip_address,
							SettingsKey.ip_address,
							SettingsKey.ip_port,
						}),
						("Bed", new[]
						{
							SettingsKey.bed_size,
							SettingsKey.print_center,
							SettingsKey.build_height,
							SettingsKey.bed_shape,
							// add settings specific to SLA
							SettingsKey.sla_resolution,
							SettingsKey.sla_printable_area_inset,
							SettingsKey.sla_mirror_mode,
						}),
						("Extruders", new[]
						{
							SettingsKey.extruder_count,
							SettingsKey.nozzle_diameter,
							SettingsKey.t0_inset,
							SettingsKey.t1_inset,
							SettingsKey.extruders_share_temperature,
							SettingsKey.extruder_offset,
						}),
					}),
					("Features", new (string groupName, string[] settings)[]
					{
						("Leveling", new[]
						{
							SettingsKey.print_leveling_solution,
							SettingsKey.print_leveling_insets,
							SettingsKey.leveling_sample_points,
							SettingsKey.print_leveling_required_to_print,
						}),
						("Print Recovery", new[]
						{
							SettingsKey.recover_is_enabled,
							SettingsKey.recover_first_layer_speed,
							SettingsKey.recover_position_before_z_home,
						}),
						("Probe", new[]
						{
							SettingsKey.print_leveling_probe_start,
							SettingsKey.use_z_probe,
							SettingsKey.validate_leveling,
							SettingsKey.validation_threshold,
							SettingsKey.z_probe_samples,
							SettingsKey.probe_offset,
							SettingsKey.z_servo_depolyed_angle,
							SettingsKey.z_servo_retracted_angle,
							SettingsKey.measure_probe_offset_conductively,
							SettingsKey.conductive_pad_center,
							SettingsKey.conductive_probe_min_z,
						}),
						("Behavior", new[]
						{
							SettingsKey.slice_engine,
							SettingsKey.heat_extruder_before_homing,
							SettingsKey.auto_release_motors,
							SettingsKey.validate_layer_height,
							SettingsKey.emulate_endstops,
							SettingsKey.send_with_checksum,
							SettingsKey.additional_printing_errors,
							SettingsKey.reset_long_extrusion,
							SettingsKey.output_only_first_layer,
							SettingsKey.g0,
							SettingsKey.progress_reporting,
							SettingsKey.include_firmware_updater,
							SettingsKey.backup_firmware_before_update,
							SettingsKey.enable_firmware_sounds,
						}),
						("Diagnostics", new[]
						{
							SettingsKey.report_runout_sensor_data,
						}),
						("Printer Help", new[]
						{
							SettingsKey.trim_filament_markdown,
							SettingsKey.insert_filament_markdown2,
							SettingsKey.running_clean_markdown2,
							SettingsKey.insert_filament_1_markdown,
							SettingsKey.running_clean_1_markdown,
							SettingsKey.printer_sku,
							SettingsKey.created_date,
						}),
					}),
					("Hardware", new (string groupName, string[] settings)[]
					{
						("Hardware", new[]
						{
							SettingsKey.firmware_type,
							SettingsKey.show_reset_connection,
							SettingsKey.z_homes_to_max,
							SettingsKey.has_fan,
							SettingsKey.has_fan_per_extruder,
							SettingsKey.has_hardware_leveling,
							SettingsKey.has_independent_z_motors,
							SettingsKey.has_heated_bed,
							SettingsKey.has_swappable_bed,
							SettingsKey.has_sd_card_reader,
							SettingsKey.has_power_control,
							SettingsKey.filament_runout_sensor,
							SettingsKey.runout_sensor_check_distance,
							SettingsKey.runout_sensor_trigger_ratio,
							SettingsKey.has_z_probe,
							SettingsKey.has_z_servo,
							SettingsKey.has_conductive_nozzle,
							SettingsKey.has_c_axis,
							SettingsKey.enable_network_printing,
							SettingsKey.enable_sailfish_communication,
							SettingsKey.max_acceleration,
							SettingsKey.max_velocity,
							SettingsKey.jerk_velocity,
							SettingsKey.print_time_estimate_multiplier,
							SettingsKey.load_filament_length,
							SettingsKey.unload_filament_length,
							SettingsKey.load_filament_speed,
						}),
					}),
					("G-Code", new[]
					{
						("Printer Control", new[]
						{
							SettingsKey.start_gcode,
							SettingsKey.end_gcode,
							SettingsKey.layer_gcode,
							SettingsKey.connect_gcode,
							SettingsKey.clear_bed_gcode,
						}),
						("User Control", new[]
						{
							SettingsKey.cancel_gcode,
							SettingsKey.pause_gcode,
							SettingsKey.resume_gcode,
						}),
						("Multi-Extruder", new[]
						{
							SettingsKey.before_toolchange_gcode,
							SettingsKey.toolchange_gcode,
							SettingsKey.before_toolchange_gcode_1,
							SettingsKey.toolchange_gcode_1,
							SettingsKey.before_toolchange_gcode_2,
							SettingsKey.toolchange_gcode_2,
							SettingsKey.before_toolchange_gcode_3,
							SettingsKey.toolchange_gcode_3,
						}),
						("Filters", new[]
						{
							SettingsKey.write_regex,
							SettingsKey.read_regex,
						}),
					}),
					};
			}

			return printerSettings;
		}

		public static bool ContainesKey((string categoryName, (string groupName, string[] settings)[] groups)[] settingsGrouping, string key)
        {
			foreach (var category in settingsGrouping)
			{
				foreach (var group in category.groups)
				{
					foreach (var setting in group.settings)
					{
						if (setting == key)
						{
							return true;
						}
					}
				}
			}

			return false;
		}

		public static (int index, string category, string group, string key) GetLayout(string key)
		{
			// find the setting in SliceSettings()
			var settings = SliceSettings();
			var index = 0;
			foreach (var category in settings)
			{
				foreach (var group in category.groups)
				{
					foreach (var setting in group.settings)
					{
						if (setting == key)
                        {
							return (index, category.categoryName, group.groupName, key);
                        }
						// increment after every setting
						index++;
					}
				}
			}

			return (-1, "", "", key);
		}
	}
}