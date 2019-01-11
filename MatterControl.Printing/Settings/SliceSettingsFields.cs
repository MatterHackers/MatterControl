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

using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Localizations;
using static MatterHackers.MatterControl.SlicerConfiguration.SliceSettingData;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public static class SliceSettingsExtensions
	{
		// Add extension method to List<QuickMenuNameValue> for simplified collection initializer without type names
		// e.g.  QuickMenuSettings = { { "PLA", "1.24" }, { "PET", "1.27" }, { "ABS","1.04" } },
		public static void Add(this List<QuickMenuNameValue> list, string menuName, string value)
		{
			list.Add(new QuickMenuNameValue()
			{
				MenuName = menuName,
				Value = value
			});
		}
	}

	public static class SliceSettingsFields
	{
		// Apparently VisualStudio fails to format code in collection initializers  (which is the entirely of this file). After too much time
		// trying to work around the issue I realized I could temporarily switch the statement to look like params args to a function and work around this issue...
		//public void DummyMethod(params [] SliceSettingData)
		//{
		//}

		//public void GetSettings()
		//{
		//	this.DummyMethod(

		public static IEnumerable<SliceSettingData> AllSettings()
		{
			return new []
			{
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.avoid_crossing_perimeters,
					PresentationName = "Avoid Crossing Perimeters",
					HelpText = "Forces the slicer to attempt to avoid having the perimeter line cross over existing perimeter lines. This can help with oozing or strings.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bed_shape,
					PresentationName = "Bed Shape",
					HelpText = "The shape of the physical print bed.",
					DataEditType = DataEditTypes.LIST,
					ListValues = "rectangular,circular",
					DefaultValue = "rectangular",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bed_size,
					PresentationName = "Bed Size",
					HelpText = "The X and Y values of the size of the print bed, in millimeters. For printers with a circular bed, these values are the diameters on the X and Y axes.",
					DataEditType = DataEditTypes.VECTOR2,
					Units = "mm",
					DefaultValue = "200,200"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bed_temperature,
					PresentationName = "Bed Temperature",
					HelpText = "The temperature to which the bed will be set for the duration of the print. Set to 0 to disable.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C",
					ShowIfSet = "has_heated_bed",
					DefaultValue = "70"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.load_filament_length,
					PresentationName = "Load Filament Length",
					HelpText = "The amount of filament to insert into the printer when loading.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm",
					ShowIfSet = "!sla_printer",
					DefaultValue = "20"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.load_filament_speed,
					PresentationName = "Filament Speed",
					HelpText = "The speed to run filament into and out of the printer.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm/s",
					ShowIfSet = "!sla_printer",
					DefaultValue = "80"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.unload_filament_time,
					PresentationName = "Unload Filament Time",
					HelpText = "The time it will take to unload the filament",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "s",
					ShowIfSet = "!sla_printer",
					DefaultValue = "5"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.unload_filament_length,
					PresentationName = "Unload Filament Length",
					HelpText = "The amount of filament to remove from the printer while unloading.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm",
					ShowIfSet = "!sla_printer",
					DefaultValue = "70"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.trim_filament_markdown,
					PresentationName = "Trim Filament Page",
					HelpText = "The Markdown that will be shown on the Trim Filament page.",
					DataEditType = DataEditTypes.MARKDOWN_TEXT,
					ShowIfSet = "!sla_printer",
					DefaultValue = "Trim the end of the filament to ensure a good load.  \n![](https://www.matterhackers.com/r/c3zLyf)  \nMake sure you trim it at a slight angle"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.insert_filament_markdown2,
					PresentationName = "Insert Filament Page",
					HelpText = "The Markdown that will be shown on the Insert Filament page.",
					DataEditType = DataEditTypes.MARKDOWN_TEXT,
					ShowIfSet = "!sla_printer",
					DefaultValue = "* Insert filament into the extruder until you feel it start to feed\\n  * Make sure the filament is all the way into the extruder\\n  * Hold the filament for several seconds until it catches\\n  * Test that it is inserted by gently pulling down, there should be some resistance  \\n* Click 'Next'  \\n![Load Filament](https://www.matterhackers.com/r/Ipj4Bb)"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.running_clean_markdown2,
					PresentationName = "Clean Filament Page",
					HelpText = "The Markdown that will be shown on the Clean Filament page.",
					DataEditType = DataEditTypes.MARKDOWN_TEXT,
					ShowIfSet = "!sla_printer",
					DefaultValue = "In a few seconds filament should be coming out of the extruder\\n* Wait for the new filament to be coming out with no trace of the previous filament\\n* Click 'Next' when the new filament is running cleanly"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bottom_solid_layers,
					PresentationName = "Bottom Solid Layers",
					HelpText = "The number of layers or the distance in millimeters to solid fill on the bottom surface(s) of the object. Add mm to the end of the number to specify distance in millimeters.",
					DataEditType = DataEditTypes.INT_OR_MM,
					Units = "count or mm",
					DefaultValue = "1mm"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.layer_to_pause,
					PresentationName = "Layer(s) To Pause",
					HelpText = "The layer(s) at which the print will pause, allowing for a change in filament. Printer is paused before starting the given layer. Leave blank to disable. To pause on multiple layers, separate the layer numbers with semicolons. For example: \"16; 37\".",
					DataEditType = DataEditTypes.STRING,
					ShowIfSet = "!sla_printer",
					ResetAtEndOfPrint = true,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bridge_speed,
					PresentationName = "Bridges",
					HelpText = "The speed at which bridging between walls will print.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm/s",
					ShowIfSet = "!sla_printer",
					DefaultValue = "20"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.air_gap_speed,
					PresentationName = "Air Gap",
					HelpText = "The speed at which the air gap layer will print.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm/s",
					ShowIfSet = "!sla_printer",
					DefaultValue = "15"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bottom_infill_speed,
					PresentationName = "Bottom Solid Infill",
					HelpText = "The speed at which the bottom solid layers will print. Can be set explicitly or as a percentage of the Infill speed. Use 0 to match infill speed.",
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm/s or %",
					ShowIfSet = "!sla_printer",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.build_height,
					PresentationName = "Build Height",
					HelpText = "The height of the printer's printable volume, in millimeters. Controls the height of the visual print area displayed in 3D View.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm",
					DefaultValue = "0",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.cancel_gcode,
					PresentationName = "Cancel G-Code",
					HelpText = "G-Code to run when a print is canceled.",
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					ShowIfSet = "!sla_printer",
					DefaultValue = "",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.complete_objects,
					PresentationName = "Complete Individual Objects",
					HelpText = "Each individual part is printed to completion then the nozzle is lowered back to the bed and the next part is printed.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.connect_gcode,
					PresentationName = "On Connect G-Code",
					HelpText = "G-Code to run upon successful connection to a printer. This can be useful to set settings specific to a given printer.",
					ShowIfSet = "!sla_printer",
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.cool_extruder_lift,
					PresentationName = "Enable Extruder Lift",
					HelpText = "Moves the nozzle up and off the part to allow cooling.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.cooling,
					PresentationName = "Enable Auto Cooling",
					HelpText = "Turns on and off all cooling settings (all settings below this one).",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.create_raft,
					PresentationName = "Create Raft",
					HelpText = "Creates a raft under the printed part. Useful to prevent warping when printing ABS (and other warping-prone plastics) as it helps parts adhere to the bed.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "NEVER_SHOW",
					DefaultValue = "0",
					ReloadUiWhenChanged = true
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.raft_extra_distance_around_part,
					PresentationName = "Expand Distance",
					HelpText = "The extra distance the raft will extend around the edge of the part.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm",
					ShowIfSet = "!sla_printer",
					EnableIfSet = "create_raft",
					DefaultValue = "5"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.raft_air_gap,
					PresentationName = "Air Gap",
					HelpText = "The distance between the top of the raft and the bottom of the model. 0.6 mm is a good starting point for PLA and 0.4 mm is a good starting point for ABS. Lower values give a smoother surface, higher values make the print easier to remove.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm",
					ShowIfSet = "!sla_printer",
					EnableIfSet = "create_raft",
					DefaultValue = ".2"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.raft_print_speed,
					PresentationName = "Raft",
					HelpText = "The speed at which the layers of the raft (other than the first layer) will print. This can be set explicitly or as a percentage of the Infill speed.",
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm/s or %",
					ShowIfSet = "!sla_printer",
					DefaultValue = "100%"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.end_gcode,
					PresentationName = "End G-Code",
					HelpText = "G-Code to be run at the end of all automatic output (the very end of the G-Code commands).",
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = "M104 S0 ; turn off temperature\\nG28 X0 ; home X axis\\nM84 ; disable motors"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.external_perimeter_speed,
					PresentationName = "Outside Perimeter",
					HelpText = "The speed at which outside, external, or the otherwise visible perimeters will print.",
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					ShowIfSet = "!sla_printer",
					Units = "mm/s or %",
					DefaultValue = "70%"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.external_perimeters_first,
					PresentationName = "External Perimeters First",
					HelpText = "Forces external perimeters to be printed first. By default, they will print last.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.extruder_count,
					PresentationName = "Extruder Count",
					HelpText = "The number of extruders the printer has.",
					DataEditType = DataEditTypes.INT,
					DefaultValue = "1",
					ShowIfSet = "!sla_printer",
					ReloadUiWhenChanged = true
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.extruder_offset,
					PresentationName = "Nozzle Offsets",
					HelpText = "The offset of each nozzle relative to the first nozzle. Only useful for multiple extruder machines.",
					DataEditType = DataEditTypes.OFFSET3,
					Units = "mm",
					ShowIfSet = "!sla_printer",
					DefaultValue = "0x0,0x0,0x0,0x0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.baby_step_z_offset,
					PresentationName = "Baby Step Offset",
					HelpText = "The z offset to apply to improve the first layer adhesion.",
					DataEditType = DataEditTypes.DOUBLE,
					Units = "mm",
					DefaultValue = "0",
					ShowIfSet = "!sla_printer",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.extruders_share_temperature,
					PresentationName = "Share Temperature",
					HelpText = "Used to specify if more than one extruder share a common heater cartridge.",
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0",
					ShowIfSet = "!sla_printer",
					ReloadUiWhenChanged = true
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.heat_extruder_before_homing,
					PresentationName = "Heat Before Homing",
					HelpText = "Forces the printer to heat the nozzle before homing.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.merge_overlapping_lines,
					PresentationName = "Merge Overlapping Lines",
					HelpText = "Detect perimeters that cross over themselves and combine them.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.expand_thin_walls,
					PresentationName = "Expand Thin Walls",
					HelpText = "Detects sections of the model that would be too thin to print and expands them to make them printable.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.extrusion_multiplier,
					PresentationName = "Extrusion Multiplier",
					HelpText = "All extrusions are multiplied by this value. Increasing it above 1 will increase the amount of filament being extruded (1.1 is a good max value); decreasing it will decrease the amount being extruded (.9 is a good minimum value).",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					ShowIfSet = "!sla_printer",
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.filament_cost,
					PresentationName = "Cost",
					HelpText = "The price of one kilogram of filament. Used for estimating the cost of a print in the Layer View.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "$/kg",
					DefaultValue = "0",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.filament_density,
					PresentationName = "Density",
					HelpText = "Material density. Only used for estimating mass in the Layer View.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "g/cm³",
					DefaultValue = "1.24",
					RebuildGCodeOnChange = false,
					QuickMenuSettings = { { "PLA", "1.24" }, { "PET", "1.27" }, { "ABS","1.04" } },
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.filament_diameter,
					PresentationName = "Diameter",
					HelpText = "The actual diameter of the filament used for printing.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					ShowIfSet = "!sla_printer",
					Units = "mm",
					DefaultValue = "3"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.fill_angle,
					PresentationName = "Starting Angle",
					HelpText = "The angle of the infill, measured from the X axis. Not used when bridging.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°",
					DefaultValue = "45"
				},
				new SliceSettingData()
				{
					QuickMenuSettings = { { "Light", "10%" }, { "Standard", "30%" }, { "Heavy", "90%" } },
					SlicerConfigName = SettingsKey.fill_density,
					PresentationName = "Fill Density",
					HelpText = "The amount of infill material to generate, expressed as a ratio or a percentage.",
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					DefaultValue = "0.4"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.fill_pattern,
					PresentationName = "Fill Pattern",
					HelpText = "The geometric shape of the support structure for the inside of parts.",
					DataEditType = DataEditTypes.LIST,
					ListValues = "rectilinear,line,grid,concentric,honeycomb,hilbertcurve,achimedeancords,octagramspiral,3dhoneycomb",
					DefaultValue = "honeycomb"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.fill_thin_gaps,
					PresentationName = "Fill Thin Gaps",
					HelpText = "Detect gaps between perimeters that are too thin to fill with normal infill and attempt to fill them.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.first_layer_extrusion_width,
					PresentationName = "First Layer",
					HelpText = "A modifier of the width of the extrusion for the first layer of the print. A value greater than 100% can help with adhesion to the print bed.",
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm or %",
					ShowIfSet = "!sla_printer",
					DefaultValue = "100%"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.first_layer_height,
					PresentationName = "First Layer Thickness",
					HelpText = "The thickness of the first layer. A first layer taller than the default layer thickness can ensure good adhesion to the build plate.",
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm or %",
					DefaultValue = "0.3",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.first_layer_speed,
					PresentationName = "Initial Layer Speed",
					HelpText = "The speed at which the nozzle will move when printing the initial layers. If expressed as a percentage the Infill speed is modified.",
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm/s or %",
					DefaultValue = "30%"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.number_of_first_layers,
					PresentationName = "Initial Layers",
					HelpText = "The number of layers to consider as the beginning of the print. These will print at initial layer speed.",
					DataEditType = DataEditTypes.INT_OR_MM,
					Units = "layers or mm",
					ShowIfSet = "sla_printer",
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.recover_first_layer_speed,
					PresentationName = "Recover Layer Speed",
					HelpText = "The speed at which the nozzle will move when recovering a failed print, for 1 layer.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm/s",
					ShowIfSet = "!has_hardware_leveling",
					DefaultValue = "10",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.recover_is_enabled,
					PresentationName = "Enable Recovery",
					HelpText = "When this is checked MatterControl will attempt to recover a print in the event of a failure, such as lost connection or lost power.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!has_hardware_leveling",
					DefaultValue = "0",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.validate_layer_height,
					PresentationName = "Validate Layer Height",
					HelpText = "Checks before each print that the layer height is less than the nozzle diameter (important for filament adhesion)",
					DataEditType = DataEditTypes.CHECK_BOX,
					Units = "",
					ShowAsOverride = true,
					DefaultValue = "1",
					ShowIfSet = "!sla_printer",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.z_homes_to_max,
					PresentationName = "Home Z Max",
					HelpText = "Indicates that the Z axis homes the hot end away from the bed (z-max homing)",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!has_hardware_leveling",
					DefaultValue = "0",
					ReloadUiWhenChanged = true,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.recover_position_before_z_home,
					PresentationName = "XY Homing Position",
					HelpText = "The X and Y position of the hot end that minimizes the chance of colliding with the parts on the bed.",
					DataEditType = DataEditTypes.VECTOR2,
					Units = "mm",
					ShowIfSet = "!has_hardware_leveling&!	",
					DefaultValue = "0,0",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.first_layer_temperature,
					PresentationName = "Extrude First Layer",
					HelpText = "The temperature to which the nozzle will be heated before printing the first layer of a part. The printer will wait until this temperature has been reached before printing.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C",
					ShowIfSet = "!sla_printer",
					DefaultValue = "205"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.auto_release_motors,
					PresentationName = "Auto Release Motors",
					HelpText = "Turn off motor current at end of print or after cancel print.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.g0,
					PresentationName = "Use G0",
					HelpText = "Use G0 for moves rather than G1.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.gcode_flavor,
					PresentationName = "G-Code Flavor",
					HelpText = "The version of G-Code the printer's firmware communicates with. Some firmware use different G and M codes. Setting this ensures that the output G-Code will use the correct commands.",
					DataEditType = DataEditTypes.LIST,
					ListValues = "reprap,teacup,makerbot,sailfish,mach3_ecm,no_extrusion",
					DefaultValue = "reprap"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.gcode_output_type,
					PresentationName = "G-Code Output",
					HelpText = "The version of G-Code the printer's firmware communicates with. Some firmware use different G and M codes. Setting this ensures that the output G-Code will use the correct commands.",
					DataEditType = DataEditTypes.LIST,
					ListValues = "REPRAP,ULTIGCODE,BFB,MACH3",
					DefaultValue = "REPRAP"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_z_probe,
					PresentationName = "Has Z Probe",
					HelpText = "The printer has a z probe for measuring bed level.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowAsOverride = true,
					ShowIfSet = "!sla_printer",
					ResetAtEndOfPrint = false,
					DefaultValue = "0",
					ReloadUiWhenChanged = true,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_z_servo,
					PresentationName = "Has Z Servo",
					HelpText = "The printer has a servo for lowering and raising the z probe.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowAsOverride = true,
					ShowIfSet = "has_z_probe",
					ResetAtEndOfPrint = false,
					DefaultValue = "0",
					ReloadUiWhenChanged = true,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_hardware_leveling,
					PresentationName = "Has Hardware Leveling",
					HelpText = "The printer has its own auto bed leveling probe and procedure which can be called using a G29 command during Start G-Code.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "0",
					ReloadUiWhenChanged = true
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_heated_bed,
					PresentationName = "Has Heated Bed",
					HelpText = "The printer has a heated bed.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "1",
					ReloadUiWhenChanged = true
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.sla_printer,
					PresentationName = "Printer is SLA",
					HelpText = "Switch the settings interface to one intended for SLA printers.",
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0",
					ReloadUiWhenChanged = true,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.include_firmware_updater,
					PresentationName = "Show Firmware Updater",
					HelpText = "This will only work on specific hardware. Do not use unless you are sure your printer controller supports this feature",
					DataEditType = DataEditTypes.LIST,
					ListValues = "None,Simple Arduino",
					DefaultValue = "None",
					ShowIfSet = "!sla_printer",
					ReloadUiWhenChanged = true,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.backup_firmware_before_update,
					PresentationName = "Backup Firmware Before Update",
					HelpText = "When upgrading to new firmware, first save a backup of the current firmware.",
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1",
					ShowIfSet = "!sla_printer",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_power_control,
					PresentationName = "Has Power Control",
					HelpText = "The printer has the ability to control the power supply. Enable this function to show the ATX Power Control section on the Controls pane.",
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0",
					ShowIfSet = "!sla_printer",
					ReloadUiWhenChanged = true,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_sd_card_reader,
					PresentationName = "Has SD Card Reader",
					HelpText = "The printer has a SD card reader.",
					DataEditType = DataEditTypes.CHECK_BOX,
					Units = "",
					DefaultValue = "0",
					ShowIfSet = "!sla_printer",
					ReloadUiWhenChanged = true,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.show_reset_connection,
					PresentationName = "Show Reset Connection",
					HelpText = "Shows a button at the right side of the Printer Connection Bar used to reset the USB connection to the printer. This can be used on printers that support it as an emergency stop.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					ReloadUiWhenChanged = true,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.infill_overlap_perimeter,
					PresentationName = "Infill Overlap",
					HelpText = "The amount the infill edge will push into the perimeter. Helps ensure the infill is connected to the edge. This can be expressed as a percentage of the Nozzle Diameter.",
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm or %",
					DefaultValue = "25%",
					QuickMenuSettings = { { "Light", "20%" }, { "Standard", "35%" }, { "Heavy", "75%" } }
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.laser_speed_025,
					PresentationName = "Speed at 0.025 Height",
					HelpText = "The speed to move the laser when the layer height is 0.025mm. Speed will be adjusted linearly at other heights.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm/s",
					ShowIfSet = "sla_printer",
					DefaultValue = "100"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.laser_speed_100,
					PresentationName = "Speed at 0.1 Height",
					HelpText = "The speed to move the laser when the layer height is 0.1mm. Speed will be adjusted linearly at other heights.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					ShowIfSet = "sla_printer",
					Units = "mm/s",
					DefaultValue = "85"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.infill_speed,
					PresentationName = "Infill",
					HelpText = "The speed at which infill will print.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					ShowIfSet = "!sla_printer",
					Units = "mm/s",
					DefaultValue = "60"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.infill_type,
					PresentationName = "Infill Type",
					HelpText = "The geometric shape of the support structure for the inside of parts.",
					DataEditType = DataEditTypes.LIST,
					ShowIfSet = "!sla_printer",
					ListValues = "GRID,TRIANGLES,HEXAGON,LINES,CONCENTRIC",
					DefaultValue = "TRIANGLES"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.print_leveling_solution,
					PresentationName = "Leveling Solution",
					HelpText = "The print leveling algorithm to use.",
					DataEditType = DataEditTypes.LIST,
					ListValues = "3 Point Plane,3x3 Mesh,5x5 Mesh,10x10 Mesh,7 Point Disk,13 Point Disk,100 Point Disk,Custom Points",
					ShowAsOverride = true,
					ShowIfSet = "!has_hardware_leveling",
					DefaultValue = "3 Point Plane",
					ReloadUiWhenChanged = true,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.leveling_sample_points,
					PresentationName = "Sample Points",
					HelpText = "A comma separated list of sample points to probe the bed at. You must specify an x and y position for each point. For example: '20,20,100,180,180,20' will sample the bad at 3 points.",
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = "20,20,100,180,180,20",
					ShowIfSet = "!sla_printer&!has_hardware_leveling&print_leveling_solution=Custom Points",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.probe_offset_sample_point,
					PresentationName = "Probe Offset Sample Point",
					HelpText = "The position to measure the probe offset.",
					Units = "mm",
					DataEditType = DataEditTypes.VECTOR2,
					DefaultValue = "100,100",
					ShowIfSet = "!sla_printer&!has_hardware_leveling&print_leveling_solution=Custom Points&use_z_probe",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.print_leveling_required_to_print,
					PresentationName = "Require Leveling To Print",
					HelpText = "The printer requires print leveling to run correctly.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!has_hardware_leveling",
					DefaultValue = "0",
					ShowAsOverride = true,
					ReloadUiWhenChanged = true,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.filament_runout_sensor,
					PresentationName = "Has Filament Runout Sensor",
					HelpText = "Specifies that the firmware has support for ros_0 endstop reporting on M119. TRIGGERED state defines filament has runout. If runout is detected the printers pause G-Code is run.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowAsOverride = true,
					ShowIfSet = "!sla_printer",
					ResetAtEndOfPrint = false,
					DefaultValue = "0",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.probe_has_been_calibrated,
					PresentationName = "Probe Has Been Calibrated",
					HelpText = "Flag keeping track if probe calibration wizard has been run.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!has_hardware_leveling",
					DefaultValue = "0",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.filament_has_been_loaded,
					PresentationName = "Filament Has Been Loaded",
					HelpText = "Flag for the state of our filament loaded.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "0",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.print_leveling_probe_start,
					PresentationName = "Start Height",
					HelpText = "The starting height (z) of the print head before probing each print level position.",
					DataEditType = DataEditTypes.DOUBLE,
					Units = "mm",
					ShowIfSet = "!has_hardware_leveling",
					DefaultValue = "10",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.z_probe_z_offset,
					PresentationName = "Probe Z Offset",
					HelpText = "The distance the z probe is from the extruder in z. For manual probing, this is thickness of the paper (or other calibration device).",
					DataEditType = DataEditTypes.DOUBLE,
					Units = "mm",
					ShowIfSet = "!has_hardware_leveling",
					DefaultValue = ".1",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.use_z_probe,
					PresentationName = "Use Automatic Z Probe",
					HelpText = "Enable this if your printer has hardware support for G30 (automatic bed probing) and you want to use it rather than manually measuring the probe positions.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowAsOverride = true,
					ShowIfSet = "!has_hardware_leveling&has_z_probe",
					ResetAtEndOfPrint = false,
					RebuildGCodeOnChange = false,
					ReloadUiWhenChanged = true,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.z_probe_xy_offset,
					PresentationName = "Probe XY Offset",
					HelpText = "The distance the z probe is from the extruder in x and y.",
					DataEditType = DataEditTypes.VECTOR2,
					Units = "mm",
					ShowAsOverride = true,
					ShowIfSet = "!has_hardware_leveling&has_z_probe&use_z_probe",
					ResetAtEndOfPrint = false,
					RebuildGCodeOnChange = false,
					DefaultValue = "0,0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.z_probe_samples,
					PresentationName = "Number of Samples",
					HelpText = "The number of times to sample each probe position (results will be averaged).",
					DataEditType = DataEditTypes.INT,
					Units = "",
					ShowAsOverride = true,
					ShowIfSet = "!has_hardware_leveling&has_z_probe&use_z_probe",
					ResetAtEndOfPrint = false,
					RebuildGCodeOnChange = false,
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.z_servo_depolyed_angle,
					PresentationName = "Lower / Deploy",
					HelpText = "This is the angle that lowers or deploys the z probe.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°",
					ShowAsOverride = true,
					ShowIfSet = "!has_hardware_leveling&has_z_probe&use_z_probe&has_z_servo",
					ResetAtEndOfPrint = false,
					RebuildGCodeOnChange = false,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.z_servo_retracted_angle,
					PresentationName = "Raise / Stow",
					HelpText = "This is the angle that raises or stows the z probe.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°",
					ShowAsOverride = true,
					ShowIfSet = "!has_hardware_leveling&has_z_probe&use_z_probe&has_z_servo",
					ResetAtEndOfPrint = false,
					RebuildGCodeOnChange = false,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.layer_gcode,
					PresentationName = "Layer Change G-Code",
					HelpText = "G-Code to be run after the change in Z height for the next layer.",
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = "; LAYER:[layer_num]"
				},
				new SliceSettingData()
				{
					QuickMenuSettings = { { "Fine", "0.1" }, { "Standard", "0.2" }, { "Coarse", "0.3" } },
					SlicerConfigName = SettingsKey.layer_height,
					PresentationName = "Layer Thickness",
					HelpText = "The thickness of each layer of the print, except the first layer. A smaller number will create more layers and more vertical accuracy but also a slower print.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm",
					DefaultValue = "0.4"
				},
				new SliceSettingData()
				{
					QuickMenuSettings = { { "Minimum", "0.3" }, { "Standard", "0.6" }, { "Full", "1" } },
					SetSettingsOnChange = new List<Dictionary<string, string>>
				{
				new Dictionary<string, string>()
				{
				{ "TargetSetting", "bottom_solid_layers" },
				{ "Value", "[current_value]" },
				},
				new Dictionary<string, string>()
				{
				{ "TargetSetting", "top_solid_layers" },
				{ "Value", "[current_value]" }
				},
				new Dictionary<string, string>()
				{
				{ "TargetSetting", "perimeters" },
				{ "Value", "[current_value]" }
				}
				},
					SlicerConfigName = SettingsKey.solid_shell,
					PresentationName = "Width",
					HelpText = "Sets the size of the outer solid surface (perimeter) for the entire print.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.min_extrusion_before_retract,
					PresentationName = "Minimum Extrusion Requiring Retraction",
					HelpText = "The minimum length of filament that must be extruded before a retraction can occur.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm",
					EnableIfSet = SettingsKey.enable_retractions,
					ShowIfSet = "!sla_printer",
					DefaultValue = ".1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_fan,
					PresentationName = "Has Fan",
					HelpText = "The printer has a layer-cooling fan.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "1",
					ReloadUiWhenChanged = true
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.enable_fan,
					PresentationName = "Enable Fan",
					HelpText = "Turn the fan on and off regardless of settings.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ReloadUiWhenChanged = true,
					ShowIfSet = "!sla_printer&has_fan",
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.min_fan_speed_layer_time,
					PresentationName = "Turn on if Below",
					HelpText = "If the time to print a layer is less than this, the fan will turn on at its minimum speed. It will then ramp up to its maximum speed as the layer time decreases.",
					DataEditType = DataEditTypes.INT,
					Units = "seconds",
					ShowIfSet = "has_fan",
					EnableIfSet = SettingsKey.enable_fan,
					DefaultValue = "60"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.max_fan_speed_layer_time,
					PresentationName = "Run Max if Below",
					HelpText = "As the time to print a layer decreases to this, the fan speed will be increased up to its maximum speed.",
					DataEditType = DataEditTypes.INT,
					Units = "seconds",
					ShowIfSet = "has_fan",
					EnableIfSet = SettingsKey.enable_fan,
					DefaultValue = "30"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.min_fan_speed,
					PresentationName = "Minimum Speed",
					HelpText = "The minimum speed at which the layer cooling fan will run, expressed as a percentage of full power.",
					DataEditType = DataEditTypes.INT,
					Units = "%",
					ShowIfSet = "has_fan",
					EnableIfSet = SettingsKey.enable_fan,
					DefaultValue = "35"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.max_fan_speed,
					PresentationName = "Maximum Speed",
					HelpText = "The maximum speed at which the layer cooling fan will run, expressed as a percentage of full power.",
					DataEditType = DataEditTypes.INT,
					Units = "%",
					ShowIfSet = "has_fan",
					EnableIfSet = SettingsKey.enable_fan,
					DefaultValue = "100"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bridge_fan_speed,
					PresentationName = "Bridging Fan Speed",
					HelpText = "The speed at which the layer cooling fan will run when bridging, expressed as a percentage of full power.",
					DataEditType = DataEditTypes.INT,
					Units = "%",
					ShowIfSet = "has_fan",
					EnableIfSet = SettingsKey.enable_fan,
					DefaultValue = "100"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.disable_fan_first_layers,
					PresentationName = "Disable Fan For The First",
					HelpText = "The number of layers for which the layer cooling fan will be forced off at the start of the print.",
					DataEditType = DataEditTypes.INT,
					Units = "layers",
					ShowIfSet = "has_fan",
					EnableIfSet = SettingsKey.enable_fan,
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.min_print_speed,
					PresentationName = "Minimum Print Speed",
					HelpText = "The minimum speed to which the printer will reduce to in order to attempt to make the layer print time long enough to satisfy the minimum layer time.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm/s",
					ShowIfSet = "!sla_printer",
					DefaultValue = "10"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.min_skirt_length,
					PresentationName = "Minimum Extrusion Length",
					HelpText = "The minimum length of filament to use printing the skirt loops. Enough skirt loops will be drawn to use this amount of filament, overriding the value set in Loops if the value in Loops will produce a skirt shorter than this value.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					ShowIfSet = "!sla_printer",
					EnableIfSet = SettingsKey.create_skirt,
					Units = "mm",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.nozzle_diameter,
					PresentationName = "Nozzle Diameter",
					HelpText = "The diameter of the extruder's nozzle.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm",
					DefaultValue = "0.5"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.calibration_files,
					PresentationName = "Calibration Files",
					HelpText = "Sets the models that will be added to the queue when a new printer is created.",
					DataEditType = DataEditTypes.STRING,
					DefaultValue = "Calibration - Box.stl"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.make,
					PresentationName = "Make",
					HelpText = "This is the make (often the manufacturer) of printer this profile is targeting.",
					DataEditType = DataEditTypes.READONLY_STRING,
					DefaultValue = "Undefined"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.model,
					PresentationName = "Model",
					HelpText = "This is the model of printer this profile is targeting.",
					DataEditType = DataEditTypes.READONLY_STRING,
					DefaultValue = "Undefined"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.created_date,
					PresentationName = "Creation Data",
					HelpText = "The date this file was originally created.",
					DataEditType = DataEditTypes.READONLY_STRING,
					DefaultValue = "Undefined"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.output_only_first_layer,
					PresentationName = "First Layer Only",
					HelpText = "Output only the first layer of the print. Especially useful for outputting gcode data for applications like engraving or cutting.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.pause_gcode,
					PresentationName = "Pause G-Code",
					HelpText = "G-Code to run when the printer is paused.",
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					ShowIfSet = "!sla_printer",
					DefaultValue = "",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.perimeter_extrusion_width,
					PresentationName = "Perimeters",
					HelpText = "Leave this as 0 to allow automatic calculation of extrusion width.",
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm or %",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.external_perimeter_extrusion_width,
					PresentationName = "Outside Perimeters",
					HelpText = "A modifier of the width of the extrusion when printing outside perimeters. Can be useful to fine-adjust actual print size when objects print larger or smaller than specified in the digital model.",
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm or %",
					ShowIfSet = "!sla_printer",
					DefaultValue = "100%"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.perimeter_speed,
					PresentationName = "Inside Perimeters",
					HelpText = "The speed at which inside perimeters will print.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					ShowIfSet = "!sla_printer",
					Units = "mm/s",
					DefaultValue = "30"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.perimeter_start_end_overlap,
					PresentationName = "Start End Overlap",
					HelpText = "The distance that a perimeter will overlap itself when it completes its loop, expressed as a percentage of the Nozzle Diameter.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "%",
					DefaultValue = "90",
					QuickMenuSettings = { { "Light", "20" }, { "Standard", "80" }, { "Heavy", "100" } }
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.perimeters,
					PresentationName = "Perimeters",
					HelpText = "The number, or total width, of external shells to create. Add mm to the end of the number to specify width in millimeters.",
					DataEditType = DataEditTypes.INT_OR_MM,
					Units = "count or mm",
					DefaultValue = "3"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.print_center,
					PresentationName = "Print Center",
					HelpText = "The position (X and Y coordinates) of the center of the print bed, in millimeters. Normally this is 1/2 the bed size for Cartesian printers and 0, 0 for Delta printers.",
					DataEditType = DataEditTypes.VECTOR2,
					Units = "mm",
					DefaultValue = "100,100"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.raft_layers,
					PresentationName = "Raft Layers",
					HelpText = "Number of layers to print before printing any parts.",
					DataEditType = DataEditTypes.INT,
					Units = "layers",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.randomize_start,
					PresentationName = "Randomize Starting Points",
					HelpText = "Start each new layer from a different vertex to reduce seams.",
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.resume_gcode,
					PresentationName = "Resume G-Code",
					HelpText = "G-Code to be run when the print resumes after a pause.",
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					ShowIfSet = "!sla_printer",
					DefaultValue = "",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_before_travel,
					PresentationName = "Minimum Travel Requiring Retraction",
					HelpText = "The minimum distance of a non-print move which will trigger a retraction.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm",
					ShowIfSet = "!sla_printer",
					EnableIfSet = SettingsKey.enable_retractions,
					DefaultValue = "20"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.coast_at_end_distance,
					PresentationName = "Coast At End",
					HelpText = "The distance to travel after completing a perimeter to improve seams.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm",
					ShowIfSet = "!sla_printer",
					DefaultValue = "3"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.enable_retractions,
					PresentationName = "Enable Retractions",
					HelpText = "Turn retractions on and off.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "1",
					ReloadUiWhenChanged = true
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_length,
					PresentationName = "Retract Length",
					HelpText = "The distance filament will reverse before each qualifying non-print move",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm",
					DefaultValue = "1",
					ShowIfSet = "!sla_printer",
					EnableIfSet = SettingsKey.enable_retractions
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_length_tool_change,
					PresentationName = "Length on Tool Change",
					HelpText = "When using multiple extruders, the distance filament will reverse before changing to a different extruder.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					ShowIfSet = "!sla_printer&extruder_count>1",
					Units = "mm",
					EnableIfSet = SettingsKey.enable_retractions,
					DefaultValue = "10"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_when_changing_islands,
					PresentationName = "Retract When Changing Islands",
					HelpText = "Force a retraction when moving between islands (distinct parts on the layer).",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					EnableIfSet = SettingsKey.enable_retractions,
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_lift,
					PresentationName = "Z Lift",
					HelpText = "The distance the nozzle will lift after each retraction.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm",
					ShowIfSet = "!sla_printer",
					EnableIfSet = SettingsKey.enable_retractions,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_restart_extra_toolchange,
					PresentationName = "Extra Length After Tool Change",
					HelpText = "Length of extra filament to extrude after a complete tool change (in addition to the re-extrusion of the tool change retraction distance).",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					ShowIfSet = "!sla_printer&extruder_count>1",
					EnableIfSet = SettingsKey.enable_retractions,
					Units = "mm zero to disable",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.reset_long_extrusion,
					PresentationName = "Reset Long Extrusion",
					HelpText = "If the extruder has been running for a long time, it may be reporting values that are too large, this will periodically reset it.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.send_with_checksum,
					PresentationName = "Send With Checksum",
					HelpText = "Calculate and transmit a standard rep-rap checksum for all commands.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_restart_extra,
					PresentationName = "Extra Length On Restart",
					HelpText = "Length of filament to extrude after a complete retraction (in addition to the re-extrusion of the Length on Move distance).",
					DataEditType = DataEditTypes.DOUBLE,
					Units = "mm",
					ShowIfSet = "!sla_printer",
					EnableIfSet = SettingsKey.enable_retractions,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_restart_extra_time_to_apply,
					PresentationName = "Time For Extra Length",
					HelpText = "The time over which to increase the Extra Length On Restart to its maximum value. Below this time only a portion of the extra length will be applied. Leave 0 to apply the entire amount all the time.",
					DataEditType = DataEditTypes.DOUBLE,
					Units = "s",
					ShowIfSet = "!sla_printer",
					EnableIfSet = SettingsKey.enable_retractions,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_speed,
					PresentationName = "Speed",
					HelpText = "The speed at which filament will retract and re-extrude.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm/s",
					ShowIfSet = "!sla_printer",
					EnableIfSet = SettingsKey.enable_retractions,
					DefaultValue = "30"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.repair_outlines_extensive_stitching,
					PresentationName = "Connect Bad Edges",
					HelpText = "Try to connect mesh edges when the actual mesh data is not all the way connected.",
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.repair_outlines_keep_open,
					PresentationName = "Close Polygons",
					HelpText = "Sometime a mesh will not have closed a perimeter. When this is checked these non-closed perimeters while be closed.",
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.resolution,
					PresentationName = "Resolution",
					HelpText = "The minimum feature size to consider from the model. Leave at 0 to use all the model detail.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					QuickMenuSettings = { { "Touching", "0" }, { "Standard", "3" }, { "Far", "10" } },
					SlicerConfigName = SettingsKey.skirt_distance,
					PresentationName = "Distance From Object",
					HelpText = "The distance from the model at which the first skirt loop is drawn.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					ShowIfSet = "!sla_printer",
					EnableIfSet = SettingsKey.create_skirt,
					Units = "mm",
					DefaultValue = "6"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.skirt_height,
					PresentationName = "Skirt Height",
					HelpText = "The number of layers to draw the skirt.",
					DataEditType = DataEditTypes.INT,
					Units = "layers",
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.skirts,
					PresentationName = "Distance or Loops",
					HelpText = "The number of loops to draw around all the parts on the bed before starting on the parts. Used mostly to prime the nozzle so the flow is even when the actual print begins.",
					DataEditType = DataEditTypes.INT_OR_MM,
					ShowIfSet = "!sla_printer",
					EnableIfSet = SettingsKey.create_skirt,
					Units = "count or mm",
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.brims,
					PresentationName = "Distance or Loops",
					HelpText = "The number of loops to draw around parts. Used to provide additional bed adhesion",
					DataEditType = DataEditTypes.INT_OR_MM,
					EnableIfSet = SettingsKey.create_brim,
					Units = "count or mm",
					DefaultValue = "8mm"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.slowdown_below_layer_time,
					PresentationName = "Slow Down If Layer Print Time Is Below",
					HelpText = "The minimum amount of time a layer must take to print. If a layer will take less than this amount of time, the movement speed is reduced so the layer print time will match this value, down to the minimum print speed at the slowest.",
					DataEditType = DataEditTypes.INT,
					Units = "seconds",
					ShowIfSet = "!sla_printer",
					DefaultValue = "30"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.small_perimeter_speed,
					PresentationName = "Small Perimeters",
					HelpText = "Used for small perimeters (usually holes). This can be set explicitly or as a percentage of the Perimeters' speed.",
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm/s or %",
					DefaultValue = "30"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.solid_fill_pattern,
					PresentationName = "Top/Bottom Fill Pattern",
					HelpText = "The pattern used on the bottom and top layers of the print.",
					DataEditType = DataEditTypes.LIST,
					ListValues = "rectilinear,concentric,hilbertcurve,achimedeancords,octagramspiral",
					DefaultValue = "rectilinear"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.solid_infill_extrusion_width,
					PresentationName = "Solid Infill",
					HelpText = "Leave this as 0 to allow automatic calculation of extrusion width.",
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm or %",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.solid_infill_speed,
					PresentationName = "Solid Infill",
					HelpText = "The speed to print infill when completely solid. This can be set explicitly or as a percentage of the Infill speed.",
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm/s or %",
					DefaultValue = "60"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.spiral_vase,
					PresentationName = "Spiral Vase",
					HelpText = "Forces the print to have only one extrusion and gradually increase the Z height during the print. Only one part will print at a time with this feature.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowIfSet = "!sla_printer",
					ResetAtEndOfPrint = true,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.standby_temperature_delta,
					PresentationName = "Temp Lower Amount",
					HelpText = "The number of degrees Centigrade to lower the temperature of a nozzle while it is not active.",
					DataEditType = DataEditTypes.DOUBLE,
					Units = "°C",
					DefaultValue = "-5"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.start_gcode,
					PresentationName = "Start G-Code",
					HelpText = "G-Code to be run immediately following the temperature setting commands. Including commands to set temperature in this section will cause them not be generated outside of this section. Will accept Custom G-Code variables.",
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = "G28 ; home all axes\\nG1 Z5 F5000 ; lift nozzle"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.write_regex,
					PresentationName = "Write Filter",
					HelpText = "This is a set of regular expressions to apply to lines prior to sending to a printer. They will be applied in the order listed before sending. To return more than one instruction separate them with comma.",
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					Units = "",
					ShowAsOverride = true,
					ShowIfSet = null,
					ResetAtEndOfPrint = false,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.read_regex,
					PresentationName = "Read Filter",
					HelpText = "This is a set of regular expressions to apply to lines after they are received from the printer. They will be applied in order to each line received.",
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					Units = "",
					ShowAsOverride = true,
					ShowIfSet = null,
					ResetAtEndOfPrint = false,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.start_perimeters_at_concave_points,
					PresentationName = "Start At Concave Points",
					HelpText = "Make sure the first point on a perimeter is a concave point.",
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.start_perimeters_at_non_overhang,
					PresentationName = "Start At Non Overhang",
					HelpText = "Make sure the first point on a perimeter is not an overhang.",
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_air_gap,
					PresentationName = "Air Gap",
					HelpText = "The distance between the top of the support and the bottom of the model. A good value depends on the type of material. For ABS and PLA a value between 0.4 and 0.6 works well, respectively.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					ShowIfSet = "!sla_printer",
					Units = "mm",
					DefaultValue = ".3"
				},
				new SliceSettingData()
				{
					QuickMenuSettings = { { "Little", "10" }, { "Standard","50" }, { "Lots", "90" } },
					SlicerConfigName = SettingsKey.support_material_percent,
					PresentationName = "Support Percent",
					HelpText = "The percent of the extrusion width that can be overlapped and still generate.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					ShowIfSet = "!sla_printer",
					Units = "%",
					DefaultValue = "50"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_material_infill_angle,
					PresentationName = "Infill Angle",
					HelpText = "The angle at which the support material lines will be drawn.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					ShowIfSet = "!sla_printer",
					Units = "°",
					DefaultValue = "45"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_material_create_perimeter,
					PresentationName = "Create Perimeter",
					HelpText = "Generates an outline around the support material to improve strength and hold up interface layers.",
					ShowIfSet = "!sla_printer",
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_material_extruder,
					PresentationName = "Support Material Extruder",
					ShowIfSet = "!sla_printer&extruder_count>1",
					HelpText = "The index of the extruder to use for printing support material. Applicable only when Extruder Count is set to a value more than 1.",
					DataEditType = DataEditTypes.INT,
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.raft_extruder,
					PresentationName = "Raft Extruder",
					HelpText = "The index of the extruder to use to print the raft. Set to 0 to use the support extruder index.",
					ShowIfSet = "!sla_printer&extruder_count>1",
					EnableIfSet = "create_raft",
					DataEditType = DataEditTypes.INT,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_material_interface_extruder,
					PresentationName = "Support Interface Extruder",
					HelpText = "The index of the extruder to use for support material interface layer(s).",
					ShowIfSet = "!sla_printer&extruder_count>1",
					DataEditType = DataEditTypes.INT,
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_material_interface_layers,
					PresentationName = "Interface Layers",
					HelpText = "The number of layers or the distance to print solid material between the supports and the part. Add mm to the end of the number to specify distance.",
					DataEditType = DataEditTypes.INT_OR_MM,
					ShowIfSet = "!sla_printer",
					Units = "layers or mm",
					DefaultValue = ".9mm"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_material_spacing,
					PresentationName = "Pattern Spacing",
					HelpText = "The distance between support material lines.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm",
					ShowIfSet = "!sla_printer",
					DefaultValue = "2.5"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_material_speed,
					PresentationName = "Support Material",
					HelpText = "The speed at which support material structures will print.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					ShowIfSet = "!sla_printer",
					Units = "mm/s",
					DefaultValue = "60"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_material_xy_distance,
					PresentationName = "X and Y Distance",
					HelpText = "The distance the support material will be from the object in the X and Y directions.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					ShowIfSet = "!sla_printer",
					Units = "mm",
					DefaultValue = "0.7"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_type,
					PresentationName = "Support Type",
					HelpText = "The pattern to draw for the generation of support material.",
					DataEditType = DataEditTypes.LIST,
					ShowIfSet = "!sla_printer",
					ListValues = "GRID,LINES",
					DefaultValue = "LINES"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.temperature,
					PresentationName = "Extruder Temperature",
					HelpText = "The target temperature the extruder will attempt to reach during the print.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C",
					ShowIfSet = "!sla_printer",
					DefaultValue = "200"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.temperature1,
					PresentationName = "Extruder 2 Temperature",
					HelpText = "The target temperature the extruder will attempt to reach during the print.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C",
					ShowIfSet = "!sla_printer",
					DefaultValue = "200"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.temperature2,
					PresentationName = "Extruder 3 Temperature",
					HelpText = "The target temperature the extruder will attempt to reach during the print.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C",
					ShowIfSet = "!sla_printer",
					DefaultValue = "200"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.temperature3,
					PresentationName = "Extruder 4 Temperature",
					HelpText = "The target temperature the extruder will attempt to reach during the print.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C",
					ShowIfSet = "!sla_printer",
					DefaultValue = "200"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.extruder_wipe_temperature,
					PresentationName = "Extruder Wipe Temperature",
					HelpText = "The temperature at which the extruder will wipe the nozzle, as specified by Custom G-Code.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					ShowIfSet = "!sla_printer",
					Units = "°C",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bed_remove_part_temperature,
					PresentationName = "Bed Remove Part Temperature",
					HelpText = "The temperature to which the bed will heat (or cool) in order to remove the part, as specified in Custom G-Code.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C",
					ShowIfSet = "has_heated_bed",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.thin_walls,
					PresentationName = "Thin Walls",
					HelpText = "Detect when walls are too close together and need to be extruded as just one wall.",
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.threads,
					PresentationName = "Threads",
					HelpText = "The number of CPU cores to use while doing slicing. Increasing this can slow down your machine.",
					DataEditType = DataEditTypes.INT,
					DefaultValue = "2",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.before_toolchange_gcode,
					PresentationName = "Before Tool Change G-Code",
					HelpText = "G-Code to be run before every tool change. You can use [wipe_tower_x] [wipe_tower_y] & [wipe_tower_z] to set the extruder position if needed. You can also use '; WRITE_RAW' to skip checksums or '; NO_PROCESSING' to skip position offseting.",
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					ShowIfSet = "!sla_printer&extruder_count>1",
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.toolchange_gcode,
					PresentationName = "After Tool Change G-Code",
					HelpText = "G-Code to be run after every tool change. You can use [wipe_tower_x] [wipe_tower_y] & [wipe_tower_z]  to set the extruder position if needed. You can also use '; WRITE_RAW' to skip checksums or '; NO_PROCESSING' to skip position offseting.",
					ShowIfSet = "!sla_printer&extruder_count>1",
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.before_toolchange_gcode_1,
					PresentationName = "Before Tool Change G-Code 2",
					HelpText = "G-Code to be run before switching to extruder 2. Will use standard before G-Code if not set. You can use [wipe_tower_x] [wipe_tower_y] & [wipe_tower_z]  to set the extruder position if needed. You can also use '; WRITE_RAW' to skip checksums or '; NO_PROCESSING' to skip position offseting.",
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					ShowIfSet = "!sla_printer&extruder_count>1",
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.toolchange_gcode_1,
					PresentationName = "After Tool Change G-Code 2",
					HelpText = "G-Code to be run after switching to extruder 2. Will use standard after G-Code if not set. You can use [wipe_tower_x] [wipe_tower_y] & [wipe_tower_z]  to set the extruder position if needed. You can also use '; WRITE_RAW' to skip checksums or '; NO_PROCESSING' to skip position offseting.",
					ShowIfSet = "!sla_printer&extruder_count>1",
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.top_infill_extrusion_width,
					PresentationName = "Top Solid Infill",
					HelpText = "Leave this as 0 to allow automatic calculation of extrusion width.",
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm or %",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.top_solid_infill_speed,
					PresentationName = "Top Solid Infill",
					HelpText = "The speed at which the top solid layers will print. Can be set explicitly or as a percentage of the Infill speed.",
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm/s or %",
					ShowIfSet = "!sla_printer",
					DefaultValue = "50"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.top_solid_layers,
					PresentationName = "Top Solid Layers",
					HelpText = "The number of layers, or the distance in millimeters, to solid fill on the top surface(s) of the object. Add mm to the end of the number to specify distance in millimeters.",
					DataEditType = DataEditTypes.INT_OR_MM,
					Units = "count or mm",
					DefaultValue = "1mm"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.travel_speed,
					PresentationName = "Travel",
					HelpText = "The speed at which the nozzle will move when not extruding material.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm/s",
					DefaultValue = "130"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.use_firmware_retraction,
					PresentationName = "Use Firmware Retraction",
					HelpText = "Request the firmware to do retractions rather than specify the extruder movements directly.",
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bridge_over_infill,
					PresentationName = "Bridge Over Infill",
					HelpText = "Make the first layer on top of partial infill use the speed and fan for bridging.",
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.use_relative_e_distances,
					PresentationName = "Use Relative E Distances",
					HelpText = "Normally you will want to use absolute e distances. Only check this if you know your printer needs relative e distances.",
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					QuickMenuSettings = { { "115200", "115200" }, { "250000", "250000" } },
					SlicerConfigName = SettingsKey.baud_rate,
					PresentationName = "Baud Rate",
					HelpText = "The serial port communication speed of the printers firmware.",
					DataEditType = DataEditTypes.INT,
					ShowAsOverride = false,
					ShowIfSet = null,
					ResetAtEndOfPrint = false,
					DefaultValue = "250000",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.printer_name,
					PresentationName = "Printer Name",
					HelpText = "This is the name of your printer that will be displayed in the choose printer menu.",
					DataEditType = DataEditTypes.STRING,
					ShowAsOverride = false,
					DefaultValue = "",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.auto_connect,
					PresentationName = "Auto Connect",
					HelpText = "If set, the printer will automatically attempt to connect when selected.",
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowAsOverride = false,
					DefaultValue = "1",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.com_port,
					PresentationName = "Serial Port",
					HelpText = "The serial port to use while connecting to this printer.",
					DataEditType = DataEditTypes.COM_PORT,
					ShowAsOverride = false,
					ShowIfSet = "!enable_network_printing",
					DefaultValue = "",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.vibration_limit,
					PresentationName = "Vibration Limit",
					HelpText = "This is to help reduce vibrations during printing. If your printer has a resonance frequency that is causing trouble you can set this to try and reduce printing at that frequency.",
					DataEditType = DataEditTypes.INT,
					Units = "Hz",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.wipe_shield_distance,
					PresentationName = "Wipe Shield Distance",
					HelpText = "Creates a perimeter around the part on which to wipe the other nozzle when printing using dual extrusion. Set to 0 to disable.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					ShowIfSet = "!sla_printer&extruder_count>1",
					Units = "mm",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.wipe_tower_size,
					PresentationName = "Wipe Tower Size",
					HelpText = "The length and width of a tower created at the back left of the print used for wiping the next nozzle when changing between multiple extruders. Set to 0 to disable.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm",
					ShowIfSet = "!sla_printer&extruder_count>1",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.driver_type,
					PresentationName = "The serial driver to use",
					DefaultValue = "RepRap",
					HelpText = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.enable_network_printing,
					PresentationName = "Networked Printing",
					HelpText = "Sets MatterControl to attempt to connect to a printer over the network. (You must disconnect and reconnect for this to take effect)",
					ShowIfSet = "!sla_printer",
					DataEditType = DataEditTypes.CHECK_BOX,
					SetSettingsOnChange = new List<Dictionary<string, string>>
				{
				new  Dictionary<string, string>
				{
				{ "TargetSetting", "driver_type" },
				{ "OnValue", "TCPIP" },
				{ "OffValue", "RepRap" },
				}
				},
					DefaultValue = "0",
					RebuildGCodeOnChange = false,
					ReloadUiWhenChanged = true
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.enable_line_splitting,
					PresentationName = "Enable Line Splitting",
					HelpText = "Allow MatterControl to split long lines to improve leveling and print canceling. Critical for printers that are significantly out of level.",
					ShowIfSet = "!sla_printer",
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.enable_sailfish_communication,
					PresentationName = "Sailfish Communication",
					HelpText = "Sets MatterControl to use s3g communication method. (You must disconnect and reconnect for this to take effect)",
					ShowIfSet = "!sla_printer",
					DataEditType = DataEditTypes.CHECK_BOX,
					SetSettingsOnChange = new List<Dictionary<string, string>>
				{
				new Dictionary<string, string>
				{
				{ "TargetSetting", "driver_type" },
				{ "OnValue", "X3G" },
				{ "OffValue", "RepRap" }
				}
				},
					DefaultValue = "0",
					RebuildGCodeOnChange = false,
					ReloadUiWhenChanged = true
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.selector_ip_address,
					PresentationName = "IP Finder",
					HelpText = "List of IP's discovered on the network",
					DataEditType = DataEditTypes.IP_LIST,
					ShowAsOverride = false,
					ShowIfSet = "enable_network_printing",
					DefaultValue = "Manual",
					RebuildGCodeOnChange = false,
					ReloadUiWhenChanged = true
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.ip_address,
					PresentationName = "IP Address",
					HelpText = "IP Address of printer/printer controller",
					DataEditType = DataEditTypes.STRING,
					ShowAsOverride = false,
					ShowIfSet = "enable_network_printing",
					EnableIfSet = "selector_ip_address=Manual",
					DefaultValue = "127.0.0.1",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.ip_port,
					PresentationName = "Port",
					HelpText = "Port number to be used with IP Address to connect to printer over the network",
					DataEditType = DataEditTypes.INT,
					ShowAsOverride = false,
					ShowIfSet = "enable_network_printing",
					EnableIfSet = "selector_ip_address=Manual",
					DefaultValue = "23",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.z_offset,
					PresentationName = "Z Offset",
					HelpText = "The distance to move the nozzle along the Z axis to ensure that it is the correct distance from the print bed. A positive number will raise the nozzle, and a negative number will lower it.",
					DataEditType = DataEditTypes.OFFSET,
					Units = "mm",
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.feedrate_ratio,
					PresentationName = "Feedrate Ratio",
					HelpText = "Controls the speed of printer moves",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					DefaultValue = "1",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.extrusion_ratio,
					PresentationName = "Extrusion Ratio",
					HelpText = "Controls the amount of extrusion",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					DefaultValue = "1",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.max_acceleration,
					PresentationName = "Max Acceleration",
					HelpText = "The maximum amount the printer can accelerate on a G-Code move.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					DefaultValue = "1000",
					Units = "mm/s²"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.max_velocity,
					PresentationName = "Max Velocity",
					HelpText = "The maximum speed the printer can move.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					DefaultValue = "500",
					Units = "mm/s"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.jerk_velocity,
					PresentationName = "Jerk Velocity",
					HelpText = "The maximum speed that the printer treats as 0 and changes direction instantly.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					DefaultValue = "8",
					Units = "mm/s"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.print_time_estimate_multiplier,
					PresentationName = "Time Multiplier",
					HelpText = "Adjust this to correct differences between expected printing speeds and actual printing speeds.",
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "%",
					DefaultValue = "100"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.manual_movement_speeds,
					PresentationName = "Manual Movement Speeds",
					HelpText = "Axis movement speeds",
					DataEditType = DataEditTypes.STRING,
					DefaultValue = "x,3000,y,3000,z,315,e0,150",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.progress_reporting,
					PresentationName = "Progress Reporting",
					HelpText = "Choose the command for showing the print progress on the printer's LCD screen, if it has one.",
					DataEditType = DataEditTypes.LIST,
					ListValues = "None,M73,M117",
					DefaultValue = "M117",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.create_brim,
					PresentationName = "Create Brim",
					HelpText = "Creates a brim attached to the base of the print. Useful to prevent warping when printing ABS (and other warping-prone plastics) as it helps parts adhere to the bed.",
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0",
					ReloadUiWhenChanged = true
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.create_skirt,
					PresentationName = "Create Skirt",
					HelpText = "Creates an outline around the print, but not attached to it. This is useful for priming the nozzle to ensure the plastic is flowing when the print starts.",
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1",
					ReloadUiWhenChanged = true
				}
			};
		}

		private static string GetSettingsName(string settingsKey)
		{
			return PrinterSettings.SettingsData[settingsKey].PresentationName.Localize();
		}
	}
}