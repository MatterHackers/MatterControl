﻿/*
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
using MatterHackers.MatterControl.SlicerConfiguration.MappingClasses;
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
			return new[]
			{
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.avoid_crossing_perimeters,
					PresentationName = "Avoid Crossing Perimeters".Localize(),
					HelpText = "Forces the slicer to attempt to avoid having the perimeter line cross over existing perimeter lines. This can help with oozing or strings.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1",
					UiUpdate = UiUpdateRequired.SliceSettings,
					Converter = new MappedToBoolString(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.monotonic_solid_infill,
					PresentationName = "Monotonic Solid Infill".Localize(),
					HelpText = "When filling bottom and top solid layers always create them so that each new print segment side is touching a previous segment on the same side.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1",
					Converter = new MappedToBoolString(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.sla_mirror_mode,
					PresentationName = "Mirror Mode".Localize(),
					HelpText = "Set how the printed output should be altered to compensate for the printers mirror.".Localize(),
					DataEditType = DataEditTypes.LIST,
					ListValues = "None,Mirror X,Mirror Y",
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "None",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bed_shape,
					PresentationName = "Bed Shape".Localize(),
					HelpText = "The shape of the physical print bed.".Localize(),
					DataEditType = DataEditTypes.LIST,
					ListValues = "rectangular,circular",
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					DefaultValue = "rectangular",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bed_size,
					PresentationName = "Bed Size".Localize(),
					HelpText = "The X and Y values of the size of the print bed, in millimeters. For printers with a circular bed, these values are the diameters on the X and Y axes.".Localize(),
					DataEditType = DataEditTypes.VECTOR2,
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					Units = "mm".Localize(),
					DefaultValue = "200,200"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.sla_resolution,
					PresentationName = "Resolution".Localize(),
					HelpText = "The X and Y resolution of the print bed in pixels.".Localize(),
					DataEditType = DataEditTypes.VECTOR2,
					Units = "px".Localize(),
					DefaultValue = "1024,800"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bed_temperature,
					PresentationName = "Bed Temperature".Localize(),
					HelpText = "The temperature to which the bed will be set for the duration of the print. Set to 0 to disable.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					Units = "°C".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.has_heated_bed),
					DefaultValue = "70"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bed_temperature_blue_tape,
					PresentationName = "Blue Tape Bed Temperature".Localize(),
					HelpText = "The temperature to print when the bed is coverd with blue tape. Set to 0 to disable.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.has_heated_bed)
						&& settings.GetBool(SettingsKey.has_swappable_bed),
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bed_temperature_buildtak,
					PresentationName = "BuildTak Bed Temperature".Localize(),
					HelpText = "The temperature to print when the bed is using BuildTak. Set to 0 to disable.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.has_heated_bed)
						&& settings.GetBool(SettingsKey.has_swappable_bed),
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bed_temperature_garolite,
					PresentationName = "Garolite Bed Temperature".Localize(),
					HelpText = "The temperature to print when the bed is using garolite. Set to 0 to disable.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.has_heated_bed)
						&& settings.GetBool(SettingsKey.has_swappable_bed),
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bed_temperature_glass,
					PresentationName = "Glass Bed Temperature".Localize(),
					HelpText = "The temperature to print when the bed is using glass. Set to 0 to disable.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.has_heated_bed)
						&& settings.GetBool(SettingsKey.has_swappable_bed),
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bed_temperature_kapton,
					PresentationName = "Kapton Bed Temperature".Localize(),
					HelpText = "The temperature to print when the bed is coverd in kapton tape. Set to 0 to disable.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.has_heated_bed)
						&& settings.GetBool(SettingsKey.has_swappable_bed),
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bed_temperature_pei,
					PresentationName = "PEI Bed Temperature".Localize(),
					HelpText = "The temperature to print when the bed is using PEI. Set to 0 to disable.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.has_heated_bed)
						&& settings.GetBool(SettingsKey.has_swappable_bed),
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bed_temperature_pp,
					PresentationName = "Polypropylene Bed Temperature".Localize(),
					HelpText = "The temperature to print when the bed is polypropylene. Set to 0 to disable.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.has_heated_bed)
						&& settings.GetBool(SettingsKey.has_swappable_bed),
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_swappable_bed,
					PresentationName = "Has Swappable Bed".Localize(),
					HelpText = "This should be checked if the printer supports multiple bed surfaces and the bed temperature for the different surfaces needs to vary.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Show = (settings) => settings.GetBool(SettingsKey.has_heated_bed),
					UiUpdate = UiUpdateRequired.Application,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bed_surface,
					PresentationName = "Bed Surface".Localize(),
					// the below text creates a bad markdown layout when the tool tip appears
					//HelpText = "The current bed surfaces that the printer is using. This is used to set the correct bed temperature for a given material.".Localize(),                  
					HelpText = "This should be set to the current bed surfaces of the printer. It is used to select the correct bed temperature for a given material and bed surface combination.".Localize(),
					DataEditType = DataEditTypes.LIST,
					Show = (settings) => settings.GetBool(SettingsKey.has_heated_bed)
						&& settings.GetBool(SettingsKey.has_swappable_bed),
					ListValues = "Default,Blue Tape,BuildTak,Garolite,Glass,Kapton,PEI,Polypropylene",
					DefaultValue = "Default"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.first_layer_bed_temperature,
					PresentationName = "First Layer Bed Temperature".Localize(),
					HelpText = "The target temperature the bed will attempt to reach during the first layer of the print.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.has_heated_bed),
					DefaultValue = "70"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.inactive_cool_down,
					PresentationName = "Inactive Cool Down".Localize(),
					HelpText = "The amount to lower the temperature when the hotend is inactive.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "°C".Localize(),
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.slice_engine,
					PresentationName = "Slice Engine".Localize(),
					HelpText = "The slicer to use.".Localize(),
					DataEditType = DataEditTypes.SLICE_ENGINE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "MatterSlice",
					UiUpdate = UiUpdateRequired.Application,
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.sla_exposure_time,
					PresentationName = "Exposure Time".Localize(),
					HelpText = "The time in seconds to expose the normal layers of the print.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "5",
					Units = "s",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.sla_base_exposure_time,
					PresentationName = "Base Exposure Time".Localize(),
					HelpText = "The number of seconds to expose the base layers. This can increase the bonding to the build plate.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "s".Localize(),
					DefaultValue = "45",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.sla_base_min_off_time,
					PresentationName = "Base Min Off Time".Localize(),
					HelpText = "The minimum amount of time the light must be off between exposures.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "s".Localize(),
					DefaultValue = "0",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.sla_min_off_time,
					PresentationName = "Base Min Off Time".Localize(),
					HelpText = "The minimum amount of time the light must be off between exposures.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "s".Localize(),
					DefaultValue = "0",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.seconds_to_reheat,
					PresentationName = "Warm up Time".Localize(),
					HelpText = "The time it takes to heat back up from a cool down.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "s".Localize(),
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.load_filament_length,
					PresentationName = "Load Filament Length".Localize(),
					HelpText = "The amount of filament to insert into the printer when loading.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					DefaultValue = "20"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.load_filament_speed,
					PresentationName = "Filament Speed".Localize(),
					HelpText = "The speed to run filament into and out of the printer.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm/s".Localize(),
					DefaultValue = "80"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.unload_filament_length,
					PresentationName = "Unload Filament Length".Localize(),
					HelpText = "The amount of filament to remove from the printer while unloading.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					DefaultValue = "70"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.trim_filament_markdown,
					PresentationName = "Trim Filament Page".Localize(),
					HelpText = "The Markdown that will be shown on the Trim Filament page.".Localize(),
					DataEditType = DataEditTypes.MARKDOWN_TEXT,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "Trim the end of the filament to ensure a good load.  \n![](https://www.matterhackers.com/r/c3zLyf)  \nMake sure you trim it at a slight angle"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.insert_filament_markdown2,
					PresentationName = "Insert Filament Page".Localize(),
					HelpText = "The Markdown that will be shown on the Insert Filament page.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.MARKDOWN_TEXT,
					DefaultValue = "* Insert filament into the extruder until you feel it start to feed\\n  * Make sure the filament is all the way into the extruder\\n  * Hold the filament for several seconds until it catches\\n  * Test that it is inserted by gently pulling down, there should be some resistance  \\n* Click 'Next'  \\n![Load Filament](https://www.matterhackers.com/r/Ipj4Bb)"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.insert_filament_1_markdown,
					PresentationName = "Insert Filament 2 Page".Localize(),
					HelpText = "The Markdown that will be shown on the second extruders Insert Filament page.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.MARKDOWN_TEXT,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					DefaultValue = "* Insert filament into extruder 2 until you feel it start to feed\\n  * Make sure the filament is all the way into the extruder\\n  * Hold the filament for several seconds until it catches\\n  * Test that it is inserted by gently pulling down, there should be some resistance  \\n* Click 'Next'  \\n![Load Filament](https://www.matterhackers.com/r/Ipj4Bb)"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.running_clean_markdown2,
					PresentationName = "Clean Filament Page".Localize(),
					HelpText = "The Markdown that will be shown on the Clean Filament page.".Localize(),
					DataEditType = DataEditTypes.MARKDOWN_TEXT,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "In a few seconds filament should be coming out of the extruder\\n* Wait for the new filament to be coming out with no trace of the previous filament\\n* Click 'Next' when the new filament is running cleanly"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.running_clean_1_markdown,
					PresentationName = "Extruder 2 Clean Page".Localize(),
					HelpText = "The Markdown that will be shown on the second extruders Clean Filament page.".Localize(),
					DataEditType = DataEditTypes.MARKDOWN_TEXT,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					DefaultValue = "In a few seconds filament should be coming out of the second extruder\\n* Wait for the new filament to be coming out with no trace of the previous filament\\n* Click 'Next' when the new filament is running cleanly"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bottom_solid_layers,
					PresentationName = "Bottom Solid Layers".Localize(),
					HelpText = "The number of layers or the distance in millimeters to solid fill on the bottom surface(s) of the object. Add mm to the end of the number to specify distance in millimeters.".Localize(),
					DataEditType = DataEditTypes.INT_OR_MM,
					Units = "count or mm".Localize(),
					DefaultValue = "1mm",
					Converter = new AsCountOrDistance(SettingsKey.layer_height),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.sla_base_layers,
					PresentationName = "Base Layers".Localize(),
					HelpText = "The number of layers or the distance in millimeters to print bottom layers. Bottom layers help adhere parts to the build plate. Add mm to the end of the number to specify distance in millimeters.".Localize(),
					DataEditType = DataEditTypes.INT_OR_MM,
					Units = "count or mm".Localize(),
					DefaultValue = ".25mm",
					Converter = new AsCountOrDistance(SettingsKey.sla_layer_height),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.sla_base_lift_distance,
					PresentationName = "Base Lift Distance".Localize(),
					HelpText = "The distance to move the build plate after each base layer prints.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm".Localize(),
					DefaultValue = "5",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.sla_lift_distance,
					PresentationName = "Lift Distance".Localize(),
					HelpText = "The distance to move the build plate after each layer prints.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					DefaultValue = "5",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.sla_base_lift_speed,
					PresentationName = "Base Lift Speed".Localize(),
					HelpText = "The speed to move the build plate after each base layer prints.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm/min".Localize(),
					DefaultValue = "90",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.sla_lift_speed,
					PresentationName = "Lift Speed".Localize(),
					HelpText = "The speed to move the build plate after each layer prints.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm/min".Localize(),
					DefaultValue = "90",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.sla_decend_speed,
					PresentationName = "Decend Speed".Localize(),
					HelpText = "The speed to move the build plate back down after lifting.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm/min".Localize(),
					DefaultValue = "150",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.layer_to_pause,
					PresentationName = "Layer(s) To Pause".Localize(),
					HelpText = "The layer(s) at which the print will pause, allowing for a change in filament. Printer is paused before starting the given layer. Leave blank to disable. To pause on multiple layers, separate the layer numbers with semicolons. For example: \"16; 37\". Rafts layers are not considered. The 1st pause layer is the first above the raft.".Localize(),
					DataEditType = DataEditTypes.STRING,
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					ResetAtEndOfPrint = true,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bridge_speed,
					PresentationName = "Bridges".Localize(),
					HelpText = "The speed at which bridging between walls will print.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm/s".Localize(),
					DefaultValue = "3",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.infill_speed),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.air_gap_speed,
					PresentationName = "Air Gapped Layer".Localize(),
					HelpText = "The speed at which the layer on top of the air gap will print.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm/s".Localize(),
					DefaultValue = "15",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.infill_speed),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bottom_infill_speed,
					PresentationName = "Bottom Solid Infill".Localize(),
					HelpText = "The speed at which the bottom solid layers will print. Can be set explicitly or as a percentage of the Infill speed. Use 0 to match infill speed.".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm/s or %".Localize(),
					DefaultValue = "0",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.infill_speed),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.build_height,
					PresentationName = "Build Height".Localize(),
					HelpText = "The height of the printer's printable volume, in millimeters. Controls the height of the visual print area displayed in 3D View.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					DefaultValue = "0",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.cancel_gcode,
					PresentationName = "Cancel G-Code".Localize(),
					HelpText = "G-Code to run when a print is canceled.".Localize(),
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = "",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.complete_objects,
					PresentationName = "Complete Individual Objects".Localize(),
					HelpText = "Each individual part is printed to completion then the nozzle is lowered back to the bed and the next part is printed.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.connect_gcode,
					PresentationName = "On Connect G-Code".Localize(),
					HelpText = "G-Code to run upon successful connection to a printer. This can be useful to set settings specific to a given printer.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.cool_extruder_lift,
					PresentationName = "Enable Extruder Lift".Localize(),
					HelpText = "Moves the nozzle up and off the part to allow cooling.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.cooling,
					PresentationName = "Enable Auto Cooling".Localize(),
					HelpText = "Turns on and off all cooling settings (all settings below this one).".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.create_raft,
					PresentationName = "Create Raft".Localize(),
					HelpText = "Creates a raft under the printed part. Useful to prevent warping when printing ABS (and other warping-prone plastics) as it helps parts adhere to the bed.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0",
					UiUpdate = UiUpdateRequired.SliceSettings,
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					Converter = new MappedToBoolString(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.sla_create_raft,
					PresentationName = "Create Raft".Localize(),
					HelpText = "Creates a raft under the printed part. Useful to help parts stick to the build plate.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0",
					UiUpdate = UiUpdateRequired.SliceSettings,
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					Converter = new MappedToBoolString(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.raft_extra_distance_around_part,
					PresentationName = "Expand Distance".Localize(),
					HelpText = "The extra distance the raft will extend around the edge of the part.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					EnableIfSet = "create_raft",
					DefaultValue = "5",
					Converter = new AsCountOrDistance(SettingsKey.nozzle_diameter),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.raft_air_gap,
					PresentationName = "Air Gap".Localize(),
					HelpText = "The distance between the top of the raft and the bottom of the model. 0.6 mm is a good starting point for PLA and 0.4 mm is a good starting point for ABS. Lower values give a smoother surface, higher values make the print easier to remove.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					EnableIfSet = "create_raft",
					DefaultValue = ".2",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.raft_print_speed,
					PresentationName = "Raft".Localize(),
					HelpText = "The speed at which the layers of the raft (other than the first layer) will print. This can be set explicitly or as a percentage of the Infill speed.".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm/s or %".Localize(),
					DefaultValue = "100%",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.infill_speed),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.end_gcode,
					PresentationName = "End G-Code".Localize(),
					HelpText = "G-Code to be run at the end of all automatic output (the very end of the G-Code commands).".Localize(),
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = "M104 S0 ; turn off temperature\\nG28 X0 ; home X axis\\nM84 ; disable motors",
					Converter = new GCodeMapping(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.clear_bed_gcode,
					PresentationName = "Clear Bed G-Code".Localize(),
					HelpText = "G-Code used by Autopilot to clear the bed after a print completes. This is only useful on a printer designed to clear the bed.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = "",
					RebuildGCodeOnChange = false,
					Converter = new GCodeMapping(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.external_perimeter_speed,
					PresentationName = "Outside Perimeter".Localize(),
					HelpText = "The speed at which outside, external, or the otherwise visible perimeters will print.".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm/s or %".Localize(),
					DefaultValue = "70%",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.perimeter_speed),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.external_perimeters_first,
					PresentationName = "External Perimeters First".Localize(),
					HelpText = "Forces external perimeters to be printed first. By default, they will print last. Using 3 or more perimeters will result in better quality.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "0",
					Converter = new MappedToBoolString(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.extruder_count,
					PresentationName = "Extruder Count".Localize(),
					HelpText = "The number of extruders the printer has.".Localize(),
					DataEditType = DataEditTypes.INT,
					DefaultValue = "1",
					UiUpdate = UiUpdateRequired.Application,
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.extruder_offset,
					PresentationName = "Nozzle Offsets".Localize(),
					HelpText = "The offset of each nozzle relative to the first nozzle. Only useful for multiple extruder machines.".Localize(),
					DataEditType = DataEditTypes.OFFSET3,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm".Localize(),
					DefaultValue = "0x0,0x0,0x0,0x0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.baby_step_z_offset,
					PresentationName = "Baby Step Offset".Localize(),
					HelpText = "The z offset to apply to improve the first layer adhesion.".Localize(),
					DataEditType = DataEditTypes.DOUBLE,
					Units = "mm".Localize(),
					DefaultValue = "0",
					RebuildGCodeOnChange = false,
					PerToolName = (toolIndex) => $"{SettingsKey.baby_step_z_offset}_{toolIndex}"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.extruders_share_temperature,
					PresentationName = "Share Temperature".Localize(),
					HelpText = "Used to specify if more than one extruder share a common heater cartridge.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "0",
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					UiUpdate = UiUpdateRequired.Application
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.heat_extruder_before_homing,
					PresentationName = "Heat Before Homing".Localize(),
					HelpText = "Forces the printer to heat the nozzle before homing.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.seam_placement,
					PresentationName = "Seam Placement".Localize(),
					HelpText = "What to do when there is not a good place to hide the seam.".Localize(),
					DataEditType = DataEditTypes.LIST,
					ListValues = "Furthest Back,Centered In Back,Always Centered In Back,Randomized,Fastest",
					DefaultValue = "Furthest Back",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.merge_overlapping_lines,
					PresentationName = "Merge Overlapping Lines".Localize(),
					HelpText = "Detect perimeters that cross over themselves and combine them.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "1",
					Converter = new MappedToBoolString(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.expand_thin_walls,
					PresentationName = "Expand Thin Walls".Localize(),
					HelpText = "Detects sections of the model that would be too thin to print and expands them to make them printable.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "1",
					Converter = new MappedToBoolString(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.extrusion_multiplier,
					PresentationName = "Extrusion Multiplier".Localize(),
					HelpText = "All extrusions are multiplied by this value. Increasing it above 1 will increase the amount of filament being extruded (1.1 is a good max value); decreasing it will decrease the amount being extruded (.9 is a good minimum value).".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "Ratio or %".Localize(),
					DefaultValue = "100%",
					Converter = new AsPercentOrDirect(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.avoid_crossing_max_ratio,
					PresentationName = "Max Ratio".Localize(),
					HelpText = "The maximum amount that an avoid crossing travel can exceed the direct distance. If the avoid travel is too long, a direct move will be executed.".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "Ratio or %".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "3",
					Converter = new AsPercentOrDirect(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.filament_cost,
					PresentationName = "Cost".Localize(),
					HelpText = "The price of one kilogram of filament. Used for estimating the cost of a print in the Layer View.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "$/kg".Localize(),
					DefaultValue = "0",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.filament_density,
					PresentationName = "Density".Localize(),
					HelpText = "Material density. Only used for estimating mass in the Layer View.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "g/cm³".Localize(),
					DefaultValue = "1.24",
					RebuildGCodeOnChange = false,
					QuickMenuSettings = { { "PLA", "1.24" }, { "PET", "1.27" }, { "ABS", "1.04" } },
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.resin_cost,
					PresentationName = "Cost".Localize(),
					HelpText = "The price of one kilogram of resin. Used for estimating the cost of a print in the Layer View.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "$/kg".Localize(),
					DefaultValue = "0",
					RebuildGCodeOnChange = false,
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.resin_density,
					PresentationName = "Density".Localize(),
					HelpText = "Resin density. Only used for estimating mass in the Layer View.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "g/cm³".Localize(),
					DefaultValue = "1.24",
					RebuildGCodeOnChange = false,
				},
				new SliceSettingData()
				{
					QuickMenuSettings = { { "Standard 1.75", "1.75" }, { "Wide 2.85", "2.85" } },
					SlicerConfigName = SettingsKey.filament_diameter,
					PresentationName = "Diameter".Localize(),
					HelpText = "The actual diameter of the filament used for printing.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm".Localize(),
					DefaultValue = "3",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.fill_angle,
					PresentationName = "Starting Angle".Localize(),
					HelpText = "The angle of the infill, measured from the X axis. Not used when bridging.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°".Localize(),
					DefaultValue = "45",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.fill_density,
					QuickMenuSettings = { { "Light", "10%" }, { "Standard", "30%" }, { "Heavy", "90%" } },
					PresentationName = "Fill Density".Localize(),
					HelpText = "The amount of infill material to generate, expressed as a ratio or a percentage.".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					DefaultValue = "0.4",
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					Converter = new AsPercentOrDirectFirst(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.fill_thin_gaps,
					PresentationName = "Fill Thin Gaps".Localize(),
					HelpText = "Detect gaps between perimeters that are too thin to fill with normal infill and attempt to fill them.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1",
					Converter = new MappedToBoolString(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.first_layer_extrusion_width,
					PresentationName = "First Layer".Localize(),
					HelpText = "A modifier of the width of the extrusion for the first layer of the print. A value greater than 100% can help with adhesion to the print bed.".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm or %".Localize(),
					DefaultValue = "100%",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.nozzle_diameter),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.first_layer_height,
					PresentationName = "First Layer Thickness".Localize(),
					HelpText = "The thickness of the first layer. A first layer taller than the default layer thickness can ensure good adhesion to the build plate.".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm or %".Localize(),
					DefaultValue = "0.3",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.layer_height),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.first_layer_speed,
					PresentationName = "Initial Layer Speed".Localize(),
					HelpText = "The speed at which the nozzle will move when printing the initial layers. If expressed as a percentage the Infill speed is modified.".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm/s or %".Localize(),
					DefaultValue = "30%",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.infill_speed),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.number_of_first_layers,
					PresentationName = "Initial Layers".Localize(),
					HelpText = "The number of layers to consider as the beginning of the print. These will print at initial layer speed.".Localize(),
					DataEditType = DataEditTypes.INT_OR_MM,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "layers or mm".Localize(),
					DefaultValue = "1",
					Converter = new AsCountOrDistance(SettingsKey.layer_height),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.recover_first_layer_speed,
					PresentationName = "Recover Layer Speed".Localize(),
					HelpText = "The speed at which the nozzle will move when recovering a failed print, for 1 layer.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm/s".Localize(),
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling)
						&& settings.GetBool(SettingsKey.recover_is_enabled),
					DefaultValue = "10",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.recover_is_enabled,
					PresentationName = "Enable Recovery".Localize(),
					HelpText = "When this is checked MatterControl will attempt to recover a print in the event of a failure, such as lost connection or lost power.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling),
					DefaultValue = "0",
					UiUpdate = UiUpdateRequired.SliceSettings,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.validate_layer_height,
					PresentationName = "Validate Layer Height".Localize(),
					HelpText = "Checks before each print that the layer height is less than the nozzle diameter (important for filament adhesion)".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					Units = "",
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					ShowAsOverride = true,
					DefaultValue = "1",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.z_homes_to_max,
					PresentationName = "Home Z Max".Localize(),
					HelpText = "Indicates that the Z axis homes the hot end away from the bed (z-max homing)".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling),
					DefaultValue = "0",
					UiUpdate = UiUpdateRequired.SliceSettings,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.recover_position_before_z_home,
					PresentationName = "XY Homing Position".Localize(),
					HelpText = "The X and Y position of the hot end that minimizes the chance of colliding with the parts on the bed.".Localize(),
					DataEditType = DataEditTypes.VECTOR2,
					Units = "mm".Localize(),
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling)
						&& settings.GetBool(SettingsKey.recover_is_enabled),
					DefaultValue = "0,0",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.first_layer_temperature,
					PresentationName = "Extrude First Layer".Localize(),
					HelpText = "The temperature to which the nozzle will be heated before printing the first layer of a part. The printer will wait until this temperature has been reached before printing.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C".Localize(),
					DefaultValue = "205"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.auto_release_motors,
					PresentationName = "Auto Release Motors".Localize(),
					HelpText = "Turn off motor current at end of print or after cancel print.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.g0,
					PresentationName = "Use G0".Localize(),
					HelpText = "Use G0 for moves rather than G1.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_z_probe,
					PresentationName = "Has Z Probe".Localize(),
					HelpText = "The printer has a z probe for measuring bed level.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowAsOverride = true,
					DefaultValue = "0",
					UiUpdate = UiUpdateRequired.SliceSettings,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_c_axis,
					PresentationName = "Has C Axis".Localize(),
					HelpText = "The printer has a c axis used by a tool changer (e3d quad extruder).".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowAsOverride = true,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "0",
					UiUpdate = UiUpdateRequired.SliceSettings,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_z_servo,
					PresentationName = "Has Z Servo".Localize(),
					HelpText = "The printer has a servo for lowering and raising the z probe.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowAsOverride = true,
					Show = (settings) => settings.GetBool(SettingsKey.has_z_probe),
					DefaultValue = "0",
					UiUpdate = UiUpdateRequired.SliceSettings,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_conductive_nozzle,
					PresentationName = "Has Conductive Nozzle".Localize(),
					HelpText = "The printer has the ability to check for continuity on the nozzle.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowAsOverride = true,
					DefaultValue = "0",
					UiUpdate = UiUpdateRequired.SliceSettings,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.measure_probe_offset_conductively,
					PresentationName = "Measure Probe Offset Conductively".Localize(),
					HelpText = "If the printer has both a conductive nozzle and a z probe this will enable automatic validation of the distance between their readings. Expected output is 'conductive: TRIGGERED' on M119.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowAsOverride = true,
					DefaultValue = "0",
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling)
						&& settings.GetBool(SettingsKey.has_z_probe)
						&& settings.GetBool(SettingsKey.has_conductive_nozzle),
					UiUpdate = UiUpdateRequired.SliceSettings,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.validate_probe_offset,
					PresentationName = "Validate Probe Offset Automatically".Localize(),
					HelpText = "If the printer has a physically touching z probe (like a BLTouch) this will enable automatic validation of the distance between the nozzle and the z probe.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowAsOverride = true,
					DefaultValue = "0",
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling)
						&& settings.GetBool(SettingsKey.has_z_probe),
					UiUpdate = UiUpdateRequired.SliceSettings,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.conductive_pad_center,
					PresentationName = "Conductive Pad Center".Localize(),
					HelpText = "The center of the conductive pad used for nozzle probing.".Localize(),
					DataEditType = DataEditTypes.VECTOR2,
					ShowAsOverride = true,
					DefaultValue = "0,0",
					Show = (settings) => ! settings.GetBool(SettingsKey.has_hardware_leveling)
						&& settings.GetBool(SettingsKey.has_z_probe)
						&& settings.GetBool(SettingsKey.has_conductive_nozzle)
						&& settings.GetBool(SettingsKey.measure_probe_offset_conductively),
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.conductive_probe_min_z,
					PresentationName = "Conductive Probe Min Z".Localize(),
					HelpText = "The minimum z to allow the probe to go to before the test is stopped and marked as failing. This is used to ensure the printer is not damaged.".Localize(),
					DataEditType = DataEditTypes.DOUBLE,
					ShowAsOverride = true,
					DefaultValue = "-1",
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling)
						&& settings.GetBool(SettingsKey.has_z_probe)
						&& settings.GetBool(SettingsKey.has_conductive_nozzle)
						&& settings.GetBool(SettingsKey.measure_probe_offset_conductively),
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_hardware_leveling,
					PresentationName = "Has Hardware Leveling".Localize(),
					HelpText = "The printer has its own auto bed leveling probe and procedure which can be called using a G29 command during Start G-Code.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0",
					UiUpdate = UiUpdateRequired.SliceSettings
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_heated_bed,
					PresentationName = "Has Heated Bed".Localize(),
					HelpText = "The printer has a heated bed.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1",
					UiUpdate = UiUpdateRequired.Application
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.include_firmware_updater,
					PresentationName = "Show Firmware Updater".Localize(),
					HelpText = "This will only work on specific hardware. Do not use unless you are sure your printer controller supports this feature".Localize(),
					DataEditType = DataEditTypes.LIST,
					ListValues = "None,Simple Arduino",
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "None",
					UiUpdate = UiUpdateRequired.SliceSettings,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.backup_firmware_before_update,
					PresentationName = "Backup Firmware Before Update".Localize(),
					HelpText = "When upgrading to new firmware, first save a backup of the current firmware.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1",
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Show = (settings) => settings.GetValue(SettingsKey.include_firmware_updater) != "None",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_power_control,
					PresentationName = "Has Power Control".Localize(),
					HelpText = "The printer has the ability to control the power supply. Enable this function to show the ATX Power Control section on the Controls pane.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0",
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					UiUpdate = UiUpdateRequired.Application,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_sd_card_reader,
					PresentationName = "Has SD Card Reader".Localize(),
					HelpText = "The printer has a SD card reader.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					Units = "",
					DefaultValue = "0",
					UiUpdate = UiUpdateRequired.Application,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.show_reset_connection,
					PresentationName = "Show Reset Connection".Localize(),
					HelpText = "Shows a button at the right side of the Printer Connection Bar used to reset the USB connection to the printer. This can be used on printers that support it as an emergency stop.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					UiUpdate = UiUpdateRequired.Application,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.infill_overlap_perimeter,
					PresentationName = "Infill Overlap".Localize(),
					HelpText = "The amount the infill edge will push into the perimeter. Helps ensure the infill is connected to the edge. This can be expressed as a percentage of the Nozzle Diameter.".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm or %".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "25%",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.nozzle_diameter, change0ToReference: false),
					QuickMenuSettings = { { "Light", "20%" }, { "Standard", "35%" }, { "Heavy", "75%" } }
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.infill_speed,
					PresentationName = "Infill".Localize(),
					HelpText = "The speed at which infill will print.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm/s".Localize(),
					DefaultValue = "60",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.infill_speed),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.infill_type,
					PresentationName = "Infill Type".Localize(),
					HelpText = "The geometric shape of the support structure for the inside of parts.".Localize(),
					DataEditType = DataEditTypes.LIST,
					ListValues = "GRID,TRIANGLES,HEXAGON,GYROID,LINES,CONCENTRIC",
					DefaultValue = "TRIANGLES",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.firmware_type,
					PresentationName = "Firmware Type".Localize(),
					HelpText = "The firmware being used by the printer. Allows for improvements based on firmware such as optimized G-Code output.".Localize(),
					DataEditType = DataEditTypes.LIST,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					ListValues = "Unknown,Marlin,Smoothie",
					DefaultValue = "Unknown",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.print_leveling_solution,
					PresentationName = "Leveling Solution".Localize(),
					HelpText = "The print leveling algorithm to use.".Localize(),
					DataEditType = DataEditTypes.LIST,
					ListValues = "3 Point Plane,3x3 Mesh,5x5 Mesh,10x10 Mesh,7 Point Disk,13 Point Disk,100 Point Disk,Custom Points",
					ShowAsOverride = true,
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling),
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					DefaultValue = "3 Point Plane",
					UiUpdate = UiUpdateRequired.SliceSettings,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.print_leveling_insets,
					PresentationName = "Leveling Insets".Localize(),
					HelpText = "The inset amount for each side of the bed.\n- As a % of the width or depth\n- Ordered: Left, Front, Right, Back\n- NOTE: The probe offset is added on top of this".Localize(),
					DataEditType = DataEditTypes.BOUNDS,
					Units = "%".Localize(),
					ShowAsOverride = true,
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling),
					RebuildGCodeOnChange = false,
					DefaultValue = "10,10,10,10"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.leveling_sample_points,
					PresentationName = "Sample Points".Localize(),
					HelpText = "A comma separated list of sample points to probe the bed at. You must specify an x and y position for each point. For example: '20,20,100,180,180,20' will sample the bad at 3 points.".Localize(),
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = "20,20,100,180,180,20",
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling)
						&& settings.GetValue(SettingsKey.print_leveling_solution) == "Custom Points",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.print_leveling_required_to_print,
					PresentationName = "Require Leveling To Print".Localize(),
					HelpText = "The printer requires print leveling to run correctly.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "0",
					ShowAsOverride = true,
					UiUpdate = UiUpdateRequired.Application,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.filament_runout_sensor,
					PresentationName = "Has Filament Runout Sensor".Localize(),
					HelpText = "Specifies that the firmware has support for ros_0 endstop reporting on M119. TRIGGERED state defines filament has runout. If runout is detected the printers pause G-Code is run.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowAsOverride = true,
					DefaultValue = "0",
					UiUpdate = UiUpdateRequired.SliceSettings,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.enable_firmware_sounds,
					PresentationName = "Enable Firmware Sounds".Localize(),
					HelpText = "Allow M300 commands (play sound) to be sent to the firmware. Disable to turn off sounds.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowAsOverride = true,
					DefaultValue = "1",
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.runout_sensor_check_distance,
					PresentationName = "Runout Check Distance".Localize(),
					HelpText = "The distance that the filament has to extrude before we check the measured distance.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					ShowAsOverride = true,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Show = (settings) => settings.GetBool(SettingsKey.filament_runout_sensor),
					RebuildGCodeOnChange = false,
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.runout_sensor_trigger_ratio,
					PresentationName = "Runout Trigger Ratio".Localize(),
					HelpText = "The ratio between the requested extrusion and the sensors measured extrusion that will trigger an error.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					ShowAsOverride = true,
					Show = (settings) => settings.GetBool(SettingsKey.filament_runout_sensor),
					RebuildGCodeOnChange = false,
					DefaultValue = "2"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.report_runout_sensor_data,
					PresentationName = "Report Filament Runout Sensor Data".Localize(),
					HelpText = "Report the data analysis of the run out sensor data each time it is read.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowAsOverride = true,
					Show = (settings) => settings.GetBool(SettingsKey.filament_runout_sensor),

					DefaultValue = "0",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.probe_has_been_calibrated,
					PresentationName = "Probe Has Been Calibrated".Localize(),
					HelpText = "Flag keeping track if probe calibration wizard has been run.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling),
					DefaultValue = "0",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.xy_offsets_have_been_calibrated,
					PresentationName = "X Y Nozzle Offsets Have Been Calibrated".Localize(),
					HelpText = "Flag keeping track if xy calibration wizard has been run.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling),
					DefaultValue = "0",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.filament_has_been_loaded,
					PresentationName = "Filament Has Been Loaded".Localize(),
					HelpText = "Flag for the state of our filament loaded.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.filament_1_has_been_loaded,
					PresentationName = "Filament 2 Has Been Loaded".Localize(),
					HelpText = "Flag for the state of our filament loaded on extruder 2.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.print_leveling_probe_start,
					PresentationName = "Start Height".Localize(),
					HelpText = "The starting height (z) of the print head before probing each print level position.".Localize(),
					DataEditType = DataEditTypes.DOUBLE,
					Units = "mm".Localize(),
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling)
						&& settings.GetBool(SettingsKey.has_z_probe),
					DefaultValue = "10",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.use_z_probe,
					PresentationName = "Use Automatic Z Probe".Localize(),
					HelpText = "Enable this if your printer has hardware support for G30 (automatic bed probing) and you want to use it rather than manually measuring the probe positions.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowAsOverride = true,
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling)
						&& settings.GetBool(SettingsKey.has_z_probe),
					RebuildGCodeOnChange = false,
					UiUpdate = UiUpdateRequired.SliceSettings,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.validate_leveling,
					PresentationName = "Validate Calibration Before Printing".Localize(),
					HelpText = "Enable this if your printer has an automatic Z Probe and you want to validate the leveling before every print. This will run immediately after M190 (print bed reaches temp).".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowAsOverride = true,
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling)
						&& settings.GetBool(SettingsKey.has_z_probe)
						&& settings.GetBool(SettingsKey.use_z_probe),
					RebuildGCodeOnChange = false,
					UiUpdate = UiUpdateRequired.SliceSettings,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.validation_threshold,
					PresentationName = "Validation Threshold".Localize(),
					HelpText = "The deviation from the last measured value allowed without re-calculating the leveling solution.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					ShowAsOverride = true,
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling)
						&& settings.GetBool(SettingsKey.has_z_probe)
						&& settings.GetBool(SettingsKey.use_z_probe)
						&& settings.GetBool(SettingsKey.validate_leveling),
					RebuildGCodeOnChange = false,
					DefaultValue = ".05"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.probe_offset,
					PresentationName = "Probe Offset".Localize(),
					HelpText = "The offset from T0 to the probe.".Localize(),
					DataEditType = DataEditTypes.OFFSET3,
					Units = "mm".Localize(),
					ShowAsOverride = true,
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling)
						&& settings.GetBool(SettingsKey.has_z_probe)
						&& settings.GetBool(SettingsKey.use_z_probe),
					RebuildGCodeOnChange = false,
					DefaultValue = "0,0,0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.z_probe_samples,
					PresentationName = "Number of Samples".Localize(),
					HelpText = "The number of times to sample each probe position (results will be averaged).".Localize(),
					DataEditType = DataEditTypes.INT,
					Units = "",
					ShowAsOverride = true,
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling)
						&& settings.GetBool(SettingsKey.has_z_probe)
						&& settings.GetBool(SettingsKey.use_z_probe),
					RebuildGCodeOnChange = false,
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.z_servo_depolyed_angle,
					PresentationName = "Lower / Deploy".Localize(),
					HelpText = "This is the angle that lowers or deploys the z probe.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°".Localize(),
					ShowAsOverride = true,
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling)
						&& settings.GetBool(SettingsKey.has_z_probe)
						&& settings.GetBool(SettingsKey.use_z_probe)
						&& settings.GetBool(SettingsKey.has_z_servo),
					RebuildGCodeOnChange = false,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.z_servo_retracted_angle,
					PresentationName = "Raise / Stow".Localize(),
					HelpText = "This is the angle that raises or stows the z probe.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°".Localize(),
					ShowAsOverride = true,
					Show = (settings) => !settings.GetBool(SettingsKey.has_hardware_leveling)
						&& settings.GetBool(SettingsKey.has_z_probe)
						&& settings.GetBool(SettingsKey.use_z_probe)
						&& settings.GetBool(SettingsKey.has_z_servo),
					RebuildGCodeOnChange = false,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.layer_gcode,
					PresentationName = "Layer Change G-Code".Localize(),
					HelpText = "G-Code to be run after the change in Z height for the next layer.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = "; LAYER:[layer_num]",
					Converter = new MapLayerChangeGCode(),
				},
				new SliceSettingData()
				{
					QuickMenuSettings = { { "Fine", "0.1" }, { "Standard", "0.2" }, { "Coarse", "0.3" } },
					SlicerConfigName = SettingsKey.layer_height,
					PresentationName = "Layer Thickness".Localize(),
					HelpText = "The thickness of each layer of the print, except the first layer. A smaller number will create more layers and more vertical accuracy but also a slower print.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					DefaultValue = "0.4",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.sla_layer_height,
					PresentationName = "Layer Thickness".Localize(),
					HelpText = "The thickness of each layer of the print, except the base layers. A smaller number will create more layers and more vertical accuracy but also a slower print.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					DefaultValue = "0.05",
					Converter = new ValueConverter(),
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
					PresentationName = "Width".Localize(),
					HelpText = "Sets the size of the outer solid surface (perimeter) for the entire print.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.min_extrusion_before_retract,
					PresentationName = "Minimum Extrusion Requiring Retraction".Localize(),
					HelpText = "The minimum length of filament that must be extruded before a retraction can occur.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					EnableIfSet = SettingsKey.enable_retractions,
					DefaultValue = ".1",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_fan,
					PresentationName = "Has Fan".Localize(),
					HelpText = "The printer has a layer-cooling fan.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1",
					UiUpdate = UiUpdateRequired.Application
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.has_fan_per_extruder,
					PresentationName = "Each Extruder Has Fan".Localize(),
					HelpText = "Each extruder has a separate part cooling fan that is controlled independently.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					Show = (settings) => settings.GetBool(SettingsKey.has_fan)
						&& settings.GetInt(SettingsKey.extruder_count) > 1,
					DefaultValue = "0",
					UiUpdate = UiUpdateRequired.SliceSettings
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.enable_fan,
					PresentationName = "Enable Fan".Localize(),
					HelpText = "Turn the fan on and off regardless of settings.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					UiUpdate = UiUpdateRequired.SliceSettings,
					Show = (settings) => settings.GetBool(SettingsKey.has_fan),
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.min_fan_speed_layer_time,
					PresentationName = "Turn on if Below".Localize(),
					HelpText = "If the time to print a layer is less than this, the fan will turn on at its minimum speed. It will then ramp up to its maximum speed as the layer time decreases.".Localize(),
					DataEditType = DataEditTypes.INT,
					Units = "seconds".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Show = (settings) => settings.GetBool(SettingsKey.has_fan),
					EnableIfSet = SettingsKey.enable_fan,
					DefaultValue = "60",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.min_fan_speed_absolute,
					PresentationName = "Minimum Speed Always".Localize(),
					HelpText = "The minimum speed at which the layer cooling fan will run, expressed as a percentage of full power, regardless of layer time.".Localize(),
					DataEditType = DataEditTypes.INT,
					Units = "%".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.has_fan),
					EnableIfSet = SettingsKey.enable_fan,
					DefaultValue = "0",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.max_fan_speed_layer_time,
					PresentationName = "Run Max if Below".Localize(),
					HelpText = "As the time to print a layer decreases to this, the fan speed will be increased up to its maximum speed.".Localize(),
					DataEditType = DataEditTypes.INT,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "seconds".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.has_fan),
					EnableIfSet = SettingsKey.enable_fan,
					DefaultValue = "30",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.min_fan_speed,
					PresentationName = "Minimum Speed".Localize(),
					HelpText = "The minimum speed at which the layer cooling fan will run, expressed as a percentage of full power.".Localize(),
					DataEditType = DataEditTypes.INT,
					Units = "%".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.has_fan),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					EnableIfSet = SettingsKey.enable_fan,
					DefaultValue = "35",
					Converter = new ConditionalField(SettingsKey.enable_fan, new ValueConverter()),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.max_fan_speed,
					PresentationName = "Maximum Speed".Localize(),
					HelpText = "The maximum speed at which the layer cooling fan will run, expressed as a percentage of full power.".Localize(),
					DataEditType = DataEditTypes.INT,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "%".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.has_fan),
					EnableIfSet = SettingsKey.enable_fan,
					DefaultValue = "100",
					Converter = new ConditionalField(SettingsKey.enable_fan, new ValueConverter()),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bridge_fan_speed,
					PresentationName = "Bridging Fan Speed".Localize(),
					HelpText = "The speed at which the layer cooling fan will run when bridging, expressed as a percentage of full power.".Localize(),
					DataEditType = DataEditTypes.INT,
					Units = "%".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Show = (settings) => settings.GetBool(SettingsKey.has_fan),
					EnableIfSet = SettingsKey.enable_fan,
					DefaultValue = "100",
					Converter = new ConditionalField(SettingsKey.enable_fan, new ValueConverter()),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.disable_fan_first_layers,
					PresentationName = "Disable Fan For The First".Localize(),
					HelpText = "The number of layers for which the layer cooling fan will be forced off at the start of the print.".Localize(),
					DataEditType = DataEditTypes.INT,
					Units = "layers".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.has_fan),
					EnableIfSet = SettingsKey.enable_fan,
					DefaultValue = "1",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.min_print_speed,
					PresentationName = "Minimum Print Speed".Localize(),
					HelpText = "The minimum speed to which the printer will reduce to in order to attempt to make the layer print time long enough to satisfy the minimum layer time.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm/s".Localize(),
					DefaultValue = "10",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.min_skirt_length,
					PresentationName = "Minimum Extrusion Length".Localize(),
					HelpText = "The minimum length of filament to use printing the skirt loops. Enough skirt loops will be drawn to use this amount of filament, overriding the value set in Loops if the value in Loops will produce a skirt shorter than this value. NOTE: This is measure as input into the extruder not mm on the bed.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					EnableIfSet = SettingsKey.create_skirt,
					Units = "mm".Localize(),
					DefaultValue = "0",
					Converter = new SkirtLengthMapping(),
				},
				new SliceSettingData()
				{
					QuickMenuSettings = { { "Standard .4", "0.4" }, { "Large .6", "0.6" }, { "Large .8", "0.8" }, { "Large 1.2", "1.2" } },
					SlicerConfigName = SettingsKey.nozzle_diameter,
					PresentationName = "Nozzle Diameter".Localize(),
					HelpText = "The diameter of the extruder's nozzle.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					DefaultValue = "0.5",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.calibration_files,
					PresentationName = "Calibration Files".Localize(),
					HelpText = "Sets the models that will be added to the queue when a new printer is created.".Localize(),
					DataEditType = DataEditTypes.STRING,
					DefaultValue = "Calibration - Box.stl"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.make,
					PresentationName = "Make".Localize(),
					HelpText = "This is the make (often the manufacturer) of printer this profile is targeting.".Localize(),
					DataEditType = DataEditTypes.READONLY_STRING,
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					DefaultValue = "Undefined"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.model,
					PresentationName = "Model".Localize(),
					HelpText = "This is the model of printer this profile is targeting.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					DataEditType = DataEditTypes.READONLY_STRING,
					DefaultValue = "Undefined"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.created_date,
					PresentationName = "Creation Data".Localize(),
					HelpText = "The date this file was originally created.".Localize(),
					DataEditType = DataEditTypes.READONLY_STRING,
					DefaultValue = "Undefined"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.output_only_first_layer,
					PresentationName = "First Layer Only".Localize(),
					HelpText = "Output only the first layer of the print. Especially useful for outputting gcode data for applications like engraving or cutting.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "0",
					Converter = new MappedToBoolString(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.pause_gcode,
					PresentationName = "Pause G-Code".Localize(),
					HelpText = "G-Code to run when the printer is paused.".Localize(),
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = "",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.perimeter_extrusion_width,
					PresentationName = "Perimeters".Localize(),
					HelpText = "Leave this as 0 to allow automatic calculation of extrusion width.".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm or %".Localize(),
					DefaultValue = "0",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.nozzle_diameter),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.external_perimeter_extrusion_width,
					PresentationName = "Outside Perimeters".Localize(),
					HelpText = "A modifier of the width of the extrusion when printing outside perimeters. Can be useful to fine-adjust actual print size when objects print larger or smaller than specified in the digital model.".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm or %".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "100%",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.nozzle_diameter),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.perimeter_speed,
					PresentationName = "Inside Perimeters".Localize(),
					HelpText = "The speed at which inside perimeters will print.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm/s".Localize(),
					DefaultValue = "30",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.infill_speed),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.perimeter_acceleration,
					PresentationName = "Perimeter Acceleration".Localize(),
					HelpText = "The acceleration that the printer will be set to for perimeters, will not be changed if set to 0. A typical perimeter acceleration is 800.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm/s^2".Localize(),
					DefaultValue = "0",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.default_acceleration,
					PresentationName = "Default Acceleration".Localize(),
					HelpText = "The acceleration that the printer will be set to by default, will not be changed if set to 0. A typical default acceleration is 1500.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm/s^2".Localize(),
					DefaultValue = "0",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.perimeter_start_end_overlap,
					PresentationName = "Start End Overlap".Localize(),
					HelpText = "The distance that a perimeter will overlap itself when it completes its loop, expressed as a percentage of the Nozzle Diameter.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "%".Localize(),
					DefaultValue = "90",
					Converter = new AsPercentOrDirect(),
					QuickMenuSettings = { { "Light", "10" }, { "Standard", "30" }, { "Heavy", "80" } }
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.perimeters,
					PresentationName = "Perimeters".Localize(),
					HelpText = "The number, or total width, of external shells to create. Add mm to the end of the number to specify width in millimeters.".Localize(),
					DataEditType = DataEditTypes.INT_OR_MM,
					Units = "count or mm".Localize(),
					DefaultValue = "3",
					Converter = new AsCountOrDistance(SettingsKey.nozzle_diameter),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.print_center,
					PresentationName = "Print Center".Localize(),
					HelpText = "The position (X and Y coordinates) of the center of the print bed, in millimeters. Normally this is 1/2 the bed size for Cartesian printers and 0, 0 for Delta printers.".Localize(),
					DataEditType = DataEditTypes.VECTOR2,
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					Units = "mm".Localize(),
					DefaultValue = "100,100"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.raft_layers,
					PresentationName = "Raft Layers".Localize(),
					HelpText = "Number of layers to print before printing any parts.".Localize(),
					DataEditType = DataEditTypes.INT,
					Units = "layers".Localize(),
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.resume_gcode,
					PresentationName = "Resume G-Code".Localize(),
					HelpText = "G-Code to be run when the print resumes after a pause.".Localize(),
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = "",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_before_travel,
					PresentationName = "Minimum Travel Requiring Retraction".Localize(),
					HelpText = "The minimum distance of a non-print move which will trigger a retraction.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm".Localize(),
					EnableIfSet = SettingsKey.enable_retractions,
					Show = (settings) => !settings.GetBool(SettingsKey.avoid_crossing_perimeters),
					DefaultValue = "5",
					Converter = new MapFirstValue(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_before_travel_avoid,
					PresentationName = "Minimum Avoid Travel Requiring Retraction".Localize(),
					HelpText = "The minimum distance with, avoid crossing perimeters turned on, of a non-print move which will trigger a retraction.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					EnableIfSet = SettingsKey.enable_retractions,
					Show = (settings) => settings.GetBool(SettingsKey.avoid_crossing_perimeters),
					DefaultValue = "20",
					Converter = new MapFirstValue(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.coast_at_end_distance,
					PresentationName = "Coast At End".Localize(),
					HelpText = "The distance to travel after completing a perimeter to improve seams.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "3",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.enable_retractions,
					PresentationName = "Enable Retractions".Localize(),
					HelpText = "Turn retractions on and off.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1",
					UiUpdate = UiUpdateRequired.SliceSettings
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_length,
					PresentationName = "Retract Length".Localize(),
					HelpText = "The distance filament will reverse before each qualifying non-print move".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					DefaultValue = "1",
					EnableIfSet = SettingsKey.enable_retractions,
					Converter = new ConditionalField(SettingsKey.enable_retractions, new ValueConverter())
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_length_tool_change,
					PresentationName = "Length on Tool Change".Localize(),
					HelpText = "When using multiple extruders, the distance filament will reverse before changing to a different extruder.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					Units = "mm".Localize(),
					EnableIfSet = SettingsKey.enable_retractions,
					DefaultValue = "10",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_when_changing_islands,
					PresentationName = "Retract When Changing Islands".Localize(),
					HelpText = "Force a retraction when moving between islands (distinct parts on the layer).".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.CHECK_BOX,
					EnableIfSet = SettingsKey.enable_retractions,
					DefaultValue = "1",
					Converter = new MappedToBoolString(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_lift,
					PresentationName = "Z Lift".Localize(),
					HelpText = "The distance the nozzle will lift after each retraction.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					EnableIfSet = SettingsKey.enable_retractions,
					DefaultValue = "0",
					Converter = new MapFirstValue(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_restart_extra_toolchange,
					PresentationName = "Extra Length After Tool Change".Localize(),
					HelpText = "Length of extra filament to extrude after a complete tool change (in addition to the re-extrusion of the tool change retraction distance).".Localize(),
					DataEditType = DataEditTypes.DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					EnableIfSet = SettingsKey.enable_retractions,
					Units = "mm zero to disable".Localize(),
					DefaultValue = "0",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.reset_long_extrusion,
					PresentationName = "Reset Long Extrusion".Localize(),
					HelpText = "If the extruder has been running for a long time, it may be reporting values that are too large, this will periodically reset it.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1",
					Converter = new MappedToBoolString(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.send_with_checksum,
					PresentationName = "Send With Checksum".Localize(),
					HelpText = "Calculate and transmit a standard rep-rap checksum for all commands.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_restart_extra,
					PresentationName = "Extra Length On Restart".Localize(),
					HelpText = "Length of filament to extrude after a complete retraction (in addition to the re-extrusion of the Length on Move distance).".Localize(),
					DataEditType = DataEditTypes.DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm".Localize(),
					EnableIfSet = SettingsKey.enable_retractions,
					DefaultValue = "0",
					Converter = new MapFirstValue(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_restart_extra_time_to_apply,
					PresentationName = "Time For Extra Length".Localize(),
					HelpText = "The time over which to increase the Extra Length On Restart to its maximum value. Below this time only a portion of the extra length will be applied. Leave 0 to apply the entire amount all the time.".Localize(),
					DataEditType = DataEditTypes.DOUBLE,
					Units = "s".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					EnableIfSet = SettingsKey.enable_retractions,
					DefaultValue = "0",
					Converter = new MapFirstValue(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.retract_speed,
					PresentationName = "Speed".Localize(),
					HelpText = "The speed at which filament will retract and re-extrude.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm/s".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					EnableIfSet = SettingsKey.enable_retractions,
					DefaultValue = "30",
					Converter = new MapFirstValue(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.repair_outlines_extensive_stitching,
					PresentationName = "Connect Bad Edges".Localize(),
					HelpText = "Try to connect mesh edges when the actual mesh data is not all the way connected.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.repair_outlines_keep_open,
					PresentationName = "Close Polygons".Localize(),
					HelpText = "Sometime a mesh will not have closed a perimeter. When this is checked these non-closed perimeters while be closed.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					QuickMenuSettings = { { "Touching", "0" }, { "Standard", "3" }, { "Far", "10" } },
					SlicerConfigName = SettingsKey.skirt_distance,
					PresentationName = "Distance From Object".Localize(),
					HelpText = "The distance from the model at which the first skirt loop is drawn.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					EnableIfSet = SettingsKey.create_skirt,
					Units = "mm".Localize(),
					DefaultValue = "6",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.skirt_height,
					PresentationName = "Skirt Height".Localize(),
					HelpText = "The number of layers to draw the skirt.".Localize(),
					DataEditType = DataEditTypes.INT,
					Units = "layers".Localize(),
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.skirts,
					PresentationName = "Distance or Loops".Localize(),
					HelpText = "The number of loops to draw around all the parts on the bed before starting on the parts. Used mostly to prime the nozzle so the flow is even when the actual print begins.".Localize(),
					DataEditType = DataEditTypes.INT_OR_MM,
					EnableIfSet = SettingsKey.create_skirt,
					Units = "count or mm".Localize(),
					DefaultValue = "1",
					Converter = new ConditionalField(SettingsKey.create_skirt, new AsCountOrDistance(SettingsKey.nozzle_diameter)),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.brims,
					PresentationName = "Distance or Loops".Localize(),
					HelpText = "The number of loops to draw around parts. Used to provide additional bed adhesion".Localize(),
					DataEditType = DataEditTypes.INT_OR_MM,
					EnableIfSet = SettingsKey.create_brim,
					Units = "count or mm".Localize(),
					DefaultValue = "8mm",
					Converter = new ConditionalField(SettingsKey.create_brim, new AsCountOrDistance(SettingsKey.nozzle_diameter))
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.brims_layers,
					PresentationName = "Layers".Localize(),
					HelpText = "The number of layers to create the brims. This can make the brim stronger when needed.".Localize(),
					DataEditType = DataEditTypes.INT,
					EnableIfSet = SettingsKey.create_brim,
					DefaultValue = "1",
					Converter = new ValueConverter(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.slowdown_below_layer_time,
					PresentationName = "Slow Down If Layer Print Time Is Below".Localize(),
					HelpText = "The minimum amount of time a layer must take to print. If a layer will take less than this amount of time, the movement speed is reduced so the layer print time will match this value, down to the minimum print speed at the slowest.".Localize(),
					DataEditType = DataEditTypes.INT,
					Units = "seconds".Localize(),
					DefaultValue = "30",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.small_perimeter_speed,
					PresentationName = "Small Perimeters".Localize(),
					HelpText = "Used for small perimeters (usually holes). This can be set explicitly or as a percentage of the Perimeters' speed.".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm/s or %".Localize(),
					DefaultValue = "30",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.perimeter_speed)
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.solid_infill_extrusion_width,
					PresentationName = "Solid Infill".Localize(),
					HelpText = "Leave this as 0 to allow automatic calculation of extrusion width.".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm or %".Localize(),
					DefaultValue = "0",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.nozzle_diameter)
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.solid_infill_speed,
					PresentationName = "Solid Infill".Localize(),
					HelpText = "The speed to print infill when completely solid. This can be set explicitly or as a percentage of the Infill speed.".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm/s or %".Localize(),
					DefaultValue = "60",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.infill_speed)
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.spiral_vase,
					PresentationName = "Spiral Vase".Localize(),
					HelpText = "Forces the print to have only one extrusion and gradually increase the Z height during the print. Only one part will print at a time with this feature.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					ResetAtEndOfPrint = true,
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					DefaultValue = "0",
					Converter = new MappedToBoolString(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.standby_temperature_delta,
					PresentationName = "Temp Lower Amount".Localize(),
					HelpText = "The number of degrees Centigrade to lower the temperature of a nozzle while it is not active.".Localize(),
					DataEditType = DataEditTypes.DOUBLE,
					Units = "°C".Localize(),
					DefaultValue = "-5"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.start_gcode,
					PresentationName = "Start G-Code".Localize(),
					HelpText = "G-Code to be run immediately following the temperature setting commands. Including commands to set temperature in this section will cause them not be generated outside of this section. Will accept Custom G-Code variables.".Localize(),
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					DefaultValue = "G28 ; home all axes\\nG1 Z5 F5000 ; lift nozzle",
					Converter = new GCodeMapping(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.write_regex,
					PresentationName = "Write Filter".Localize(),
					HelpText = "This is a set of regular expressions to apply to lines prior to sending to a printer. They will be applied in the order listed before sending. To return more than one instruction separate them with comma.".Localize(),
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "",
					ShowAsOverride = true,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.additional_printing_errors,
					PresentationName = "Additional Printing Errors".Localize(),
					HelpText = "In addition to the normal firmware errors, these comma separated strings will cause MatterControl to stop and show an error message.".Localize(),
					DataEditType = DataEditTypes.WIDE_STRING,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "",
					ShowAsOverride = true,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.read_regex,
					PresentationName = "Read Filter".Localize(),
					HelpText = "This is a set of regular expressions to apply to lines after they are received from the printer. They will be applied in order to each line received.".Localize(),
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "",
					ShowAsOverride = true,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.start_perimeters_at_concave_points,
					PresentationName = "Start At Concave Points".Localize(),
					HelpText = "Make sure the first point on a perimeter is a concave point.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.start_perimeters_at_non_overhang,
					PresentationName = "Start At Non Overhang".Localize(),
					HelpText = "Make sure the first point on a perimeter is not an overhang.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_air_gap,
					PresentationName = "Air Gap".Localize(),
					HelpText = "The distance between the top of the support and the bottom of the model. A good value depends on the type of material. For ABS and PLA a value between 0.4 and 0.6 works well, respectively.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm".Localize(),
					DefaultValue = ".3",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_material_infill_angle,
					PresentationName = "Infill Angle".Localize(),
					HelpText = "The angle at which the support material lines will be drawn.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°".Localize(),
					DefaultValue = "45",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_material_create_perimeter,
					PresentationName = "Create Perimeter".Localize(),
					HelpText = "Generates an outline around the support material to improve strength and hold up interface layers.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1",
					Converter = new MappedToBoolString(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.create_per_layer_support,
					PresentationName = "Create Supports".Localize(),
					HelpText = "Evaluate every layer for support requirements. NOTE: If there are any support columns, this setting is ignored.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0",
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					Converter = new MappedToBoolString(),
					UiUpdate = UiUpdateRequired.SliceSettings
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.sla_auto_support,
					PresentationName = "Auto Support".Localize(),
					HelpText = "Before exporting evaluate and add automatic support material. NOTE: If there is already support, no additional support will be added.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0",
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					Converter = new MappedToBoolString(),
					UiUpdate = UiUpdateRequired.SliceSettings
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.create_per_layer_internal_support,
					PresentationName = "Generate Everywhere".Localize(),
					HelpText = "When generating per layer support, evaluate all surfaces rather than only those above the bed.".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.create_per_layer_support),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1",
					Converter = new MappedToBoolString(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_percent,
					PresentationName = "Line Support Overlap".Localize(),
					HelpText = "The percentage overlap a given printed line must have over the layer below to be supported.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "%".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.create_per_layer_support),
					DefaultValue = "50",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_grab_distance,
					PresentationName = "Support Expand Distance".Localize(),
					HelpText = "The amount to expand the support so it is easy to grab.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.create_per_layer_support),
					DefaultValue = "1",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_material_extruder,
					PresentationName = "Support Material Extruder".Localize(),
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					HelpText = "The extruder to use for support material. Default will use whichever extruder active at the time.".Localize(),
					DataEditType = DataEditTypes.EXTRUDER_LIST,
					Converter = new ValuePlusConstant(-1),
					DefaultValue = "1",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.raft_extruder,
					PresentationName = "Raft Extruder".Localize(),
					HelpText = "The extruder to use to print the raft. Default will use extruder 1.".Localize(),
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					EnableIfSet = "create_raft",
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.EXTRUDER_LIST,
					Converter = new ValuePlusConstant(-1),
					DefaultValue = "1",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_material_interface_extruder,
					PresentationName = "Support Interface Extruder".Localize(),
					HelpText = "The extruder to use to for support material interface layers. Default will use whichever extruder active at the time.".Localize(),
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					DataEditType = DataEditTypes.EXTRUDER_LIST,
					Converter = new ValuePlusConstant(-1),
					DefaultValue = "1",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.brim_extruder,
					PresentationName = "Brim Extruder".Localize(),
					HelpText = "The extruder to use for the brim.  Default will use the first extruder of the print.".Localize(),
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					DataEditType = DataEditTypes.EXTRUDER_LIST,
					Converter = new ValuePlusConstant(-1),
					EnableIfSet = SettingsKey.create_brim,
					DefaultValue = "0",
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_material_interface_layers,
					PresentationName = "Interface Layers".Localize(),
					HelpText = "The number of layers or the distance to print solid material between the supports and the part. Add mm to the end of the number to specify distance.".Localize(),
					DataEditType = DataEditTypes.INT_OR_MM,
					Units = "layers or mm".Localize(),
					DefaultValue = ".9mm",
					Converter = new AsCountOrDistance(SettingsKey.layer_height),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_material_spacing,
					PresentationName = "Pattern Spacing".Localize(),
					HelpText = "The distance between support material lines.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm".Localize(),
					DefaultValue = "2.5",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_material_speed,
					PresentationName = "Support Material".Localize(),
					HelpText = "The speed at which support material structures will print.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm/s".Localize(),
					DefaultValue = "60",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.infill_speed),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.interface_layer_speed,
					PresentationName = "Interface Layer".Localize(),
					HelpText = "The speed at which interface layers will print.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm/s".Localize(),
					DefaultValue = "60",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.infill_speed),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_material_xy_distance,
					PresentationName = "X and Y Distance".Localize(),
					HelpText = "The distance the support material will be from the object in the X and Y directions.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "mm".Localize(),
					DefaultValue = "0.7",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.support_type,
					PresentationName = "Support Type".Localize(),
					HelpText = "The pattern to draw for the generation of support material.".Localize(),
					DataEditType = DataEditTypes.LIST,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					ListValues = "GRID,LINES",
					DefaultValue = "LINES",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.material_color,
					PresentationName = "Color".Localize(),
					HelpText = "The color of the first material.".Localize(),
					DataEditType = DataEditTypes.COLOR,
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					RebuildGCodeOnChange = false,
					DefaultValue = ColorF.FromHSL(0 / 10.0, .99, .49).ToColor().Html,
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.material_color_1,
					PresentationName = "Color 2".Localize(),
					HelpText = "The color of the second material (extruder 2).".Localize(),
					DataEditType = DataEditTypes.COLOR,
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					RebuildGCodeOnChange = false,
					DefaultValue = ColorF.FromHSL(1 / 10.0, .99, .49).ToColor().Html,
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.material_color_2,
					PresentationName = "Color 3".Localize(),
					HelpText = "The color of the third material (extruder 3).".Localize(),
					DataEditType = DataEditTypes.COLOR,
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 2,
					RebuildGCodeOnChange = false,
					DefaultValue = ColorF.FromHSL(2 / 10.0, .99, .49).ToColor().Html,
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.material_color_3,
					PresentationName = "Color 4".Localize(),
					HelpText = "The color of the forth material (extruder 4).".Localize(),
					DataEditType = DataEditTypes.COLOR,
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 3,
					RebuildGCodeOnChange = false,
					DefaultValue = ColorF.FromHSL(3 / 10.0, .99, .49).ToColor().Html,
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.temperature,
					PresentationName = "Extruder Temperature".Localize(),
					HelpText = "The target temperature the extruder will attempt to reach during the print.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					DefaultValue = "200"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.temperature1,
					PresentationName = "Extruder 2 Temperature".Localize(),
					HelpText = "The target temperature the extruder will attempt to reach during the print.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C".Localize(),
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					DefaultValue = "200"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.t1_extrusion_move_speed_multiplier,
					PresentationName = "Extruder 2 Speed".Localize(),
					HelpText = "Modify T1 speeds during extrusion moves by the ratio or percent.".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "Ratio or %".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					DefaultValue = "100%",
					Converter = new AsPercentOrDirect()
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.temperature2,
					PresentationName = "Extruder 3 Temperature".Localize(),
					HelpText = "The target temperature the extruder will attempt to reach during the print.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C".Localize(),
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 2,
					DefaultValue = "200"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.sla_printable_area_inset,
					PresentationName = "Printable Area Inset".Localize(),
					HelpText = "The inset amount from the edges of the bed. Defines the printable area of the bed. Leave as 0s if the entire bed can be printed to (Left, Front, Right, Back).".Localize(),
					DataEditType = DataEditTypes.BOUNDS,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.t0_inset,
					PresentationName = "Nozzle 1 Inset".Localize(),
					HelpText = "The inset amount for nozzle 1 from the bed (Left, Front, Right, Back).".Localize(),
					DataEditType = DataEditTypes.BOUNDS,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.t1_inset,
					PresentationName = "Nozzle 2 Inset".Localize(),
					HelpText = "The inset amount for nozzle 2 from the bed (Left, Front, Right, Back).".Localize(),
					DataEditType = DataEditTypes.BOUNDS,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.temperature3,
					PresentationName = "Extruder 4 Temperature".Localize(),
					HelpText = "The target temperature the extruder will attempt to reach during the print.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "°C".Localize(),
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 3,
					DefaultValue = "200"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.extruder_wipe_temperature,
					PresentationName = "Extruder Wipe Temperature".Localize(),
					HelpText = "The temperature at which the extruder will wipe the nozzle, as specified by Custom G-Code.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "°C".Localize(),
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bed_remove_part_temperature,
					PresentationName = "Bed Remove Part Temperature".Localize(),
					HelpText = "The temperature to which the bed will heat (or cool) in order to remove the part, as specified in Custom G-Code.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Units = "°C".Localize(),
					Show = (settings) => settings.GetBool(SettingsKey.has_heated_bed),
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.thin_walls,
					PresentationName = "Thin Walls".Localize(),
					HelpText = "Detect when walls are too close together and need to be extruded as just one wall.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.threads,
					PresentationName = "Threads".Localize(),
					HelpText = "The number of CPU cores to use while doing slicing. Increasing this can slow down your machine.".Localize(),
					DataEditType = DataEditTypes.INT,
					DefaultValue = "2",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.before_toolchange_gcode,
					PresentationName = "Before Tool Change G-Code".Localize(),
					HelpText = "G-Code to be run before every tool change. You can use '; WRITE_RAW' to skip checksums or '; NO_PROCESSING' to skip position offsetting.".Localize(),
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.toolchange_gcode,
					PresentationName = "After Tool Change G-Code".Localize(),
					HelpText = "G-Code to be run after every tool change. You can use '; WRITE_RAW' to skip checksums or '; NO_PROCESSING' to skip position offsetting.".Localize(),
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.before_toolchange_gcode_1,
					PresentationName = "Before Tool Change G-Code 2".Localize(),
					HelpText = "G-Code to be run before switching to extruder 2. Will use standard before G-Code if not set. You can use [wipe_tower_x] [wipe_tower_y] & [wipe_tower_z]  to set the extruder position if needed. You can also use '; WRITE_RAW' to skip checksums or '; NO_PROCESSING' to skip position offsetting.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.toolchange_gcode_1,
					PresentationName = "After Tool Change G-Code 2".Localize(),
					HelpText = "G-Code to be run after switching to extruder 2. Will use standard after G-Code if not set. You can use '; WRITE_RAW' to skip checksums or '; NO_PROCESSING' to skip position offsetting.".Localize(),
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.before_toolchange_gcode_2,
					PresentationName = "Before Tool Change G-Code 3".Localize(),
					HelpText = "G-Code to be run before switching to extruder 3. Will use standard before G-Code if not set. You can use [wipe_tower_x] [wipe_tower_y] & [wipe_tower_z]  to set the extruder position if needed. You can also use '; WRITE_RAW' to skip checksums or '; NO_PROCESSING' to skip position offsetting.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 2,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.toolchange_gcode_2,
					PresentationName = "After Tool Change G-Code 3".Localize(),
					HelpText = "G-Code to be run after switching to extruder 3. Will use standard after G-Code if not set. You can use '; WRITE_RAW' to skip checksums or '; NO_PROCESSING' to skip position offsetting.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 2,
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.before_toolchange_gcode_3,
					PresentationName = "Before Tool Change G-Code 4".Localize(),
					HelpText = "G-Code to be run before switching to extruder 3. Will use standard before G-Code if not set. You can use [wipe_tower_x] [wipe_tower_y] & [wipe_tower_z]  to set the extruder position if needed. You can also use '; WRITE_RAW' to skip checksums or '; NO_PROCESSING' to skip position offsetting.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 3,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.toolchange_gcode_3,
					PresentationName = "After Tool Change G-Code 4".Localize(),
					HelpText = "G-Code to be run after switching to extruder 2. Will use standard after G-Code if not set. You can use '; WRITE_RAW' to skip checksums or '; NO_PROCESSING' to skip position offsetting.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 3,
					DataEditType = DataEditTypes.MULTI_LINE_TEXT,
					DefaultValue = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.top_infill_extrusion_width,
					PresentationName = "Top Solid Infill".Localize(),
					HelpText = "Leave this as 0 to allow automatic calculation of extrusion width.".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm or %".Localize(),
					DefaultValue = "0",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.nozzle_diameter)
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.top_solid_infill_speed,
					PresentationName = "Top Solid Infill".Localize(),
					HelpText = "The speed at which the top solid layers will print. Can be set explicitly or as a percentage of the Infill speed.".Localize(),
					DataEditType = DataEditTypes.DOUBLE_OR_PERCENT,
					Units = "mm/s or %".Localize(),
					DefaultValue = "50",
					Converter = new AsPercentOfReferenceOrDirect(SettingsKey.infill_speed),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.top_solid_layers,
					PresentationName = "Top Solid Layers".Localize(),
					HelpText = "The number of layers, or the distance in millimeters, to solid fill on the top surface(s) of the object. Add mm to the end of the number to specify distance in millimeters.".Localize(),
					DataEditType = DataEditTypes.INT_OR_MM,
					Units = "count or mm".Localize(),
					DefaultValue = "1mm",
					Converter = new AsCountOrDistance(SettingsKey.layer_height),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.travel_speed,
					PresentationName = "Travel".Localize(),
					HelpText = "The speed at which the nozzle will move when not extruding material.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm/s".Localize(),
					DefaultValue = "130",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.use_firmware_retraction,
					PresentationName = "Use Firmware Retraction".Localize(),
					HelpText = "Request the firmware to do retractions rather than specify the extruder movements directly.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.bridge_over_infill,
					PresentationName = "Bridge Over Infill".Localize(),
					HelpText = "Make the first layer on top of partial infill use the speed and fan for bridging.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DefaultValue = "0",
					Converter = new MappedToBoolString(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.use_relative_e_distances,
					PresentationName = "Use Relative E Distances".Localize(),
					HelpText = "Normally you will want to use absolute e distances. Only check this if you know your printer needs relative e distances.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					QuickMenuSettings = { { "115200", "115200" }, { "250000", "250000" } },
					SlicerConfigName = SettingsKey.baud_rate,
					PresentationName = "Baud Rate".Localize(),
					HelpText = "The serial port communication speed of the printers firmware.".Localize(),
					DataEditType = DataEditTypes.INT,
					ShowAsOverride = false,
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					DefaultValue = "250000",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.printer_name,
					PresentationName = "Printer Name".Localize(),
					HelpText = "This is the name of your printer that will be displayed in the choose printer menu.".Localize(),
					DataEditType = DataEditTypes.WIDE_STRING,
					ShowAsOverride = false,
					DefaultValue = "",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.printer_sku,
					PresentationName = "Printer SKU".Localize(),
					HelpText = "This is the MatterHackers sku representing this printer. If the printer is not offered on MatterHackers this can be set to a collection key.".Localize(),
					DataEditType = DataEditTypes.STRING,
					ShowAsOverride = false,
					DefaultValue = "",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.auto_connect,
					PresentationName = "Auto Connect".Localize(),
					HelpText = "If set, the printer will automatically attempt to connect when selected.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					ShowAsOverride = false,
					DefaultValue = "1",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.com_port,
					PresentationName = "Serial Port".Localize(),
					HelpText = "The serial port to use while connecting to this printer.".Localize(),
					DataEditType = DataEditTypes.COM_PORT,
					ShowAsOverride = false,
					Show = (settings) => !settings.GetBool(SettingsKey.enable_network_printing),
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					DefaultValue = "",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.wipe_shield_distance,
					PresentationName = "Wipe Shield Distance".Localize(),
					HelpText = "Creates a perimeter around the part on which to wipe the other nozzle when printing using dual extrusion. Set to 0 to disable.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					Units = "mm".Localize(),
					DefaultValue = "0",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.wipe_tower_perimeters_per_extruder,
					PresentationName = "Perimeters Per Extruder".Localize(),
					HelpText = "The number of perimeters will be this number times the number of active extruders. Make this a smaller number to make the wipe more hollow or bigger to fill it.".Localize(),
					DataEditType = DataEditTypes.INT,
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					DefaultValue = "3",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.wipe_tower_size,
					PresentationName = "Wipe Tower Size".Localize(),
					HelpText = "The length and width of a tower created at the back left of the print used for wiping the next nozzle when changing between multiple extruders. Set to 0 to disable.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "mm".Localize(),
					Show = (settings) => settings.GetInt(SettingsKey.extruder_count) > 1,
					DefaultValue = "0",
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.driver_type,
					PresentationName = "The serial driver to use".Localize(),
					DefaultValue = "RepRap",
					HelpText = ""
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.enable_network_printing,
					PresentationName = "Networked Printing".Localize(),
					HelpText = "Sets MatterControl to attempt to connect to a printer over the network. (You must disconnect and reconnect for this to take effect)".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.CHECK_BOX,
					SetSettingsOnChange = new List<Dictionary<string, string>>
					{
						new Dictionary<string, string>
						{
							{ "TargetSetting", SettingsKey.driver_type },
							{ "OnValue", "TCPIP" },
							{ "OffValue", "RepRap" },
						}
					},
					DefaultValue = "0",
					RebuildGCodeOnChange = false,
					UiUpdate = UiUpdateRequired.Application
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.enable_line_splitting,
					PresentationName = "Enable Line Splitting".Localize(),
					HelpText = "Allow MatterControl to split long lines to improve leveling and print canceling. Critical for printers that are significantly out of level.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.emulate_endstops,
					PresentationName = "Emulate Endstops".Localize(),
					HelpText = "Make MatterControl emulate bed limits and endstops in software and prevent the printer from moving to invalid locations.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.enable_sailfish_communication,
					PresentationName = "Sailfish Communication".Localize(),
					HelpText = "Sets MatterControl to use s3g communication method. (You must disconnect and reconnect for this to take effect)".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.CHECK_BOX,
					SetSettingsOnChange = new List<Dictionary<string, string>>
					{
						new Dictionary<string, string>
						{
							{ "TargetSetting", SettingsKey.driver_type },
							{ "OnValue", "X3G" },
							{ "OffValue", "RepRap" }
						}
					},
					DefaultValue = "0",
					RebuildGCodeOnChange = false,
					UiUpdate = UiUpdateRequired.SliceSettings
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.selector_ip_address,
					PresentationName = "IP Finder".Localize(),
					HelpText = "List of IP's discovered on the network".Localize(),
					DataEditType = DataEditTypes.IP_LIST,
					ShowAsOverride = false,
					Show = (settings) => !settings.GetBool(SettingsKey.enable_network_printing),
					DefaultValue = "Manual",
					RebuildGCodeOnChange = false,
					UiUpdate = UiUpdateRequired.SliceSettings
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.ip_address,
					PresentationName = "IP Address".Localize(),
					HelpText = "IP Address of printer/printer controller".Localize(),
					DataEditType = DataEditTypes.STRING,
					ShowAsOverride = false,
					Show = (settings) => settings.GetBool(SettingsKey.enable_network_printing),
					EnableIfSet = "selector_ip_address=Manual",
					DefaultValue = "127.0.0.1",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.ip_port,
					PresentationName = "Port".Localize(),
					HelpText = "Port number to be used with IP Address to connect to printer over the network".Localize(),
					DataEditType = DataEditTypes.INT,
					ShowAsOverride = false,
					Show = (settings) => settings.GetBool(SettingsKey.enable_network_printing),
					EnableIfSet = "selector_ip_address=Manual",
					DefaultValue = "23",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.z_offset,
					PresentationName = "Z Offset".Localize(),
					HelpText = "DEPRECATED: replaced with extruder_offset".Localize(),
					DataEditType = DataEditTypes.OFFSET,
					Units = "mm".Localize(),
					DefaultValue = "0"
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.feedrate_ratio,
					PresentationName = "Feedrate Ratio".Localize(),
					HelpText = "Controls the speed of printer moves".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					DefaultValue = "1",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.extrusion_ratio,
					PresentationName = "Extrusion Ratio".Localize(),
					HelpText = "Controls the amount of extrusion".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					DefaultValue = "1",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.max_acceleration,
					PresentationName = "Max Acceleration".Localize(),
					HelpText = "The maximum amount the printer can accelerate on a G-Code move. Used for print time estimation.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					DefaultValue = "1000",
					RebuildGCodeOnChange = false,
					Units = "mm/s²".Localize(),
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.max_velocity,
					PresentationName = "Max Velocity".Localize(),
					HelpText = "The maximum speed the printer can move. Uused for print time estimation.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					DefaultValue = "500",
					RebuildGCodeOnChange = false,
					Units = "mm/s".Localize(),
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.jerk_velocity,
					PresentationName = "Jerk Velocity".Localize(),
					HelpText = "The maximum speed that the printer treats as 0 and changes direction instantly. Used for print time estimation.".Localize(),
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					RebuildGCodeOnChange = false,
					DefaultValue = "8",
					Units = "mm/s".Localize(),
					Converter = new ValueConverter(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.print_time_estimate_multiplier,
					PresentationName = "Time Multiplier".Localize(),
					HelpText = "Adjust this to correct differences between expected printing speeds and actual printing speeds.".Localize(),
					DataEditType = DataEditTypes.POSITIVE_DOUBLE,
					Units = "%".Localize(),
					DefaultValue = "100",
					RebuildGCodeOnChange = false,
					Converter = new AsPercentOrDirect(),
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.manual_movement_speeds,
					PresentationName = "Manual Movement Speeds".Localize(),
					HelpText = "Axis movement speeds".Localize(),
					DataEditType = DataEditTypes.STRING,
					DefaultValue = "x,3000,y,3000,z,315,e0,150",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.progress_reporting,
					PresentationName = "Progress Reporting".Localize(),
					HelpText = "Choose the command for showing the print progress on the printer's LCD screen, if it has one.".Localize(),
					DataEditType = DataEditTypes.LIST,
					RequiredDisplayDetail = DisplayDetailRequired.Advanced,
					ListValues = "None,M73,M117",
					DefaultValue = "M117",
					RebuildGCodeOnChange = false
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.create_brim,
					PresentationName = "Create Brim".Localize(),
					HelpText = "Creates a brim attached to the base of the print. Useful to prevent warping when printing ABS (and other warping-prone plastics) as it helps parts adhere to the bed.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "0",
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					UiUpdate = UiUpdateRequired.SliceSettings
				},
				new SliceSettingData()
				{
					SlicerConfigName = SettingsKey.create_skirt,
					PresentationName = "Create Skirt".Localize(),
					HelpText = "Creates an outline around the print, but not attached to it. This is useful for priming the nozzle to ensure the plastic is flowing when the print starts.".Localize(),
					DataEditType = DataEditTypes.CHECK_BOX,
					DefaultValue = "1",
					RequiredDisplayDetail = DisplayDetailRequired.Simple,
					UiUpdate = UiUpdateRequired.SliceSettings
				}
			};
		}
	}
}