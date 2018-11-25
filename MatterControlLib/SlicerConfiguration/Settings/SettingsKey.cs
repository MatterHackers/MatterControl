/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
	public static class SettingsKey
	{
		public const string active_quality_key = nameof(active_quality_key);
		public const string auto_connect = nameof(auto_connect);
		public const string auto_release_motors = nameof(auto_release_motors);
		public const string baby_step_z_offset = nameof(baby_step_z_offset);
		public const string backup_firmware_before_update = nameof(backup_firmware_before_update);
		public const string baud_rate = nameof(baud_rate);
		public const string bed_remove_part_temperature = nameof(bed_remove_part_temperature);
		public const string bed_shape = nameof(bed_shape);
		public const string bed_size = nameof(bed_size);
		public const string bed_temperature = nameof(bed_temperature);
		public const string build_height = nameof(build_height);
		public const string calibration_files = nameof(calibration_files);
		public const string cancel_gcode = nameof(cancel_gcode);
		public const string com_port = nameof(com_port);
		public const string connect_gcode = nameof(connect_gcode);
		public const string created_date = nameof(created_date);
		public const string default_material_presets = nameof(default_material_presets);
		public const string device_token = nameof(device_token);
		public const string device_type = nameof(device_type);
		public const string enable_line_splitting = nameof(enable_line_splitting);
		public const string enable_network_printing = nameof(enable_network_printing);
		public const string enable_retractions = nameof(enable_retractions);
		public const string enable_sailfish_communication = nameof(enable_sailfish_communication);
		public const string end_gcode = nameof(end_gcode);
		public const string expand_thin_walls = nameof(expand_thin_walls);
		public const string external_perimeter_extrusion_width = nameof(external_perimeter_extrusion_width);
		public const string extruder_count = nameof(extruder_count);
		public const string extruders_share_temperature = nameof(extruders_share_temperature);
		public const string extrusion_ratio = nameof(extrusion_ratio);
		public const string feedrate_ratio = nameof(feedrate_ratio);
		public const string filament_cost = nameof(filament_cost);
		public const string filament_density = nameof(filament_density);
		public const string filament_diameter = nameof(filament_diameter);
		public const string filament_runout_sensor = nameof(filament_runout_sensor);
		public const string fill_density = nameof(fill_density);
		public const string fill_thin_gaps = nameof(fill_thin_gaps);
		public const string first_layer_extrusion_width = nameof(first_layer_extrusion_width);
		public const string first_layer_height = nameof(first_layer_height);
		public const string first_layer_speed = nameof(first_layer_speed);
		public const string g0 = nameof(g0);
		public const string has_fan = nameof(has_fan);
		public const string has_hardware_leveling = nameof(has_hardware_leveling);
		public const string has_heated_bed = nameof(has_heated_bed);
		public const string has_power_control = nameof(has_power_control);
		public const string has_sd_card_reader = nameof(has_sd_card_reader);
		public const string has_z_probe = nameof(has_z_probe);
		public const string has_z_servo = nameof(has_z_servo);
		public const string heat_extruder_before_homing = nameof(heat_extruder_before_homing);
		public const string include_firmware_updater = nameof(include_firmware_updater);
		public const string infill_overlap_perimeter = nameof(infill_overlap_perimeter);
		public const string infill_type = nameof(infill_type);
		public const string insert_filament_markdown2 = nameof(insert_filament_markdown2);
		public const string ip_address = nameof(ip_address);
		public const string ip_port = nameof(ip_port);
		public const string jerk_velocity = nameof(jerk_velocity);
		public const string print_time_estimate_multiplier = nameof(print_time_estimate_multiplier);
		public const string laser_speed_025 = nameof(laser_speed_025);
		public const string laser_speed_100 = nameof(laser_speed_100);
		public const string layer_gcode = nameof(layer_gcode);
		public const string layer_height = nameof(layer_height);
		public const string layer_name = nameof(layer_name);
		public const string layer_to_pause = nameof(layer_to_pause);
		public const string load_filament_length = nameof(load_filament_length);
		public const string make = nameof(make);
		public const string manual_movement_speeds = nameof(manual_movement_speeds);
		public const string max_acceleration = nameof(max_acceleration);
		public const string max_velocity = nameof(max_velocity);
		public const string merge_overlapping_lines = nameof(merge_overlapping_lines);
		public const string min_fan_speed = nameof(min_fan_speed);
		public const string max_fan_speed = nameof(max_fan_speed);
		public const string model = nameof(model);
		public const string nozzle_diameter = nameof(nozzle_diameter);
		public const string number_of_first_layers = nameof(number_of_first_layers);
		public const string oem_profile_token = nameof(oem_profile_token);
		public const string pause_gcode = nameof(pause_gcode);
		public const string perimeter_start_end_overlap = nameof(perimeter_start_end_overlap);
		public const string print_center = nameof(print_center);
		public const string print_leveling_data = nameof(print_leveling_data);
		public const string print_leveling_enabled = nameof(print_leveling_enabled);
		public const string print_leveling_probe_start = nameof(print_leveling_probe_start);
		public const string probe_has_been_calibrated = nameof(probe_has_been_calibrated);
		public const string print_leveling_required_to_print = nameof(print_leveling_required_to_print);
		public const string print_leveling_solution = nameof(print_leveling_solution);
		public const string leveling_sample_points = nameof(leveling_sample_points);
		public const string load_filament_speed = nameof(load_filament_speed);
		public const string probe_offset_sample_point = nameof(probe_offset_sample_point);
		public const string printer_name = nameof(printer_name);
		public const string progress_reporting = nameof(progress_reporting);
		public const string publish_bed_image = nameof(publish_bed_image);
		public const string read_regex = nameof(read_regex);
		public const string recover_first_layer_speed = nameof(recover_first_layer_speed);
		public const string recover_is_enabled = nameof(recover_is_enabled);
		public const string recover_position_before_z_home = nameof(recover_position_before_z_home);
		public const string resume_gcode = nameof(resume_gcode);
		public const string running_clean_markdown2 = nameof(running_clean_markdown2);
		public const string selector_ip_address = nameof(selector_ip_address);
		public const string send_with_checksum = nameof(send_with_checksum);
		public const string filament_has_been_loaded = nameof(filament_has_been_loaded);
		public const string show_reset_connection = nameof(show_reset_connection);
		public const string sla_printer = nameof(sla_printer);
		public const string spiral_vase = nameof(spiral_vase);
		public const string start_gcode = nameof(start_gcode);
		public const string temperature = nameof(temperature);
		public const string temperature1 = nameof(temperature1);
		public const string temperature2 = nameof(temperature2);
		public const string temperature3 = nameof(temperature3);
		public const string top_solid_infill_speed = nameof(top_solid_infill_speed);
		public const string trim_filament_markdown = nameof(trim_filament_markdown);
		public const string unload_filament_length = nameof(unload_filament_length);
		public const string use_z_probe = nameof(use_z_probe);
		public const string validate_layer_height = nameof(validate_layer_height);
		public const string windows_driver = nameof(windows_driver);
		public const string write_regex = nameof(write_regex);
		public const string z_homes_to_max = nameof(z_homes_to_max);
		public const string z_probe_samples = nameof(z_probe_samples);
		public const string z_probe_xy_offset = nameof(z_probe_xy_offset);
		public const string z_probe_z_offset = nameof(z_probe_z_offset);
		public const string z_servo_depolyed_angle = nameof(z_servo_depolyed_angle);
		public const string z_servo_retracted_angle = nameof(z_servo_retracted_angle);
	}
}
