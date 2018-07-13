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
using MatterHackers.VectorMath;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class EngineMappingsMatterSlice
	{
		public static readonly EngineMappingsMatterSlice Instance = new EngineMappingsMatterSlice();

		/// <summary>
		/// Application level settings control MatterControl behaviors but aren't used or passed through to the slice engine. Putting settings
		/// in this list ensures they show up for all slice engines and the lack of a MappedSetting for the engine guarantees that it won't pass
		/// through into the slicer config file
		/// </summary>
		protected HashSet<string> applicationLevelSettings = new HashSet<string>()
		{
			SettingsKey.bed_shape,
			SettingsKey.bed_size,
			SettingsKey.print_center,
			SettingsKey.send_with_checksum,
			SettingsKey.bed_temperature,
			SettingsKey.build_height,
			SettingsKey.cancel_gcode,
			SettingsKey.connect_gcode,
			SettingsKey.write_regex,
			SettingsKey.read_regex,
			SettingsKey.has_fan,
			SettingsKey.has_hardware_leveling,
			SettingsKey.has_heated_bed,
			SettingsKey.has_power_control,
			SettingsKey.has_sd_card_reader,
			SettingsKey.printer_name,
			SettingsKey.auto_connect,
			SettingsKey.backup_firmware_before_update,
			SettingsKey.baud_rate,
			SettingsKey.com_port,
			SettingsKey.filament_cost,
			SettingsKey.filament_density,
			SettingsKey.filament_runout_sensor,
			SettingsKey.z_probe_z_offset,
			SettingsKey.use_z_probe,
			SettingsKey.z_probe_samples,
			SettingsKey.has_z_probe,
			SettingsKey.has_z_servo,
			SettingsKey.z_probe_xy_offset,
			SettingsKey.z_servo_depolyed_angle,
			SettingsKey.z_servo_retracted_angle,
			SettingsKey.pause_gcode,
			SettingsKey.print_leveling_probe_start,
			SettingsKey.probe_has_been_calibrated,
			SettingsKey.print_leveling_required_to_print,
			SettingsKey.print_leveling_solution,
			SettingsKey.recover_first_layer_speed,
			SettingsKey.number_of_first_layers,
			SettingsKey.recover_is_enabled,
			SettingsKey.recover_position_before_z_home,
			SettingsKey.auto_release_motors,
			SettingsKey.resume_gcode,
			SettingsKey.temperature,
			SettingsKey.enable_retractions,
			"z_homes_to_max",

			// TODO: merge the items below into the list above after some validation - setting that weren't previously mapped to Cura but probably should be.
			SettingsKey.bed_remove_part_temperature,
			"extruder_wipe_temperature",
			SettingsKey.heat_extruder_before_homing,
			SettingsKey.include_firmware_updater,
			SettingsKey.sla_printer,
			"layer_to_pause",
			SettingsKey.show_reset_connection,
			SettingsKey.validate_layer_height,
			SettingsKey.make,
			SettingsKey.model,
			SettingsKey.enable_network_printing,
			SettingsKey.enable_sailfish_communication,
			SettingsKey.max_velocity,
			SettingsKey.jerk_velocity,
			SettingsKey.print_time_estimate_multiplier,
			SettingsKey.max_acceleration,
			SettingsKey.ip_address,
			SettingsKey.ip_port,
			SettingsKey.progress_reporting,
			"load_filament_length",
			"trim_image",
			"clean_nozzle_image",
			"insert_image",
			"running_clean_image",
			"unload_filament_length",
			"load_filament_speed",
		};

		private MappedSetting[] mappedSettings;
		private HashSet<string> matterSliceSettingNames;

		// Singleton use only - prevent external construction
		private EngineMappingsMatterSlice()
		{
			mappedSettings = new MappedSetting[]
			{
				new AsCountOrDistance("bottom_solid_layers", "numberOfBottomLayers", SettingsKey.layer_height),
				new MappedSetting("boolean_operations", "booleanOperations"),
				new MappedSetting("additional_args_to_process", "AdditionalArgsToProcess"),
				new AsCountOrDistance("perimeters", "numberOfPerimeters", SettingsKey.nozzle_diameter),
				new AsCountOrDistance("raft_extra_distance_around_part", "raftExtraDistanceAroundPart", SettingsKey.nozzle_diameter),
				new AsCountOrDistance("support_material_interface_layers", "supportInterfaceLayers", SettingsKey.layer_height),
				new AsCountOrDistance("top_solid_layers", "numberOfTopLayers", SettingsKey.layer_height),
				new AsPercentOfReferenceOrDirect(SettingsKey.external_perimeter_extrusion_width, "outsidePerimeterExtrusionWidth", SettingsKey.nozzle_diameter),
				new OverrideSpeedOnSlaPrinters("external_perimeter_speed", "outsidePerimeterSpeed", "perimeter_speed"),
				new AsPercentOfReferenceOrDirect(SettingsKey.first_layer_speed, "firstLayerSpeed", "infill_speed"),
				new AsCountOrDistance(SettingsKey.number_of_first_layers, "numberOfFirstLayers", SettingsKey.layer_height),
				new AsPercentOfReferenceOrDirect("raft_print_speed", "raftPrintSpeed", "infill_speed"),
				new OverrideSpeedOnSlaPrinters(SettingsKey.top_solid_infill_speed, "topInfillSpeed", "infill_speed"),
				new AsPercentOfReferenceOrDirect(SettingsKey.first_layer_extrusion_width, "firstLayerExtrusionWidth", SettingsKey.nozzle_diameter),
				new AsPercentOfReferenceOrDirect(SettingsKey.first_layer_height, "firstLayerThickness", SettingsKey.layer_height),
				new ExtruderOffsets("extruder_offset", "extruderOffsets"),
				new GCodeForSlicer("before_toolchange_gcode", "beforeToolchangeCode"),
				new GCodeForSlicer(SettingsKey.end_gcode, "endCode"),
				new GCodeForSlicer("toolchange_gcode", "toolChangeCode"),
				new MapFirstValue("retract_before_travel", "minimumTravelToCauseRetraction"),
				new RetractionLength("retract_length", "retractionOnTravel"),
				new MapFirstValue("retract_lift", "retractionZHop"),
				new MapFirstValue("retract_restart_extra", "unretractExtraExtrusion"),
				new MapFirstValue("retract_restart_extra_time_to_apply", "retractRestartExtraTimeToApply"),
				new MapFirstValue("retract_speed", "retractionSpeed"),
				new OverrideSpeedOnSlaPrinters("bridge_speed", "bridgeSpeed", "infill_speed"),
				new MappedSetting("extrusion_multiplier", "extrusionMultiplier"),
				new MappedSetting("fill_angle", "infillStartingAngle"),
				new AsPercentOfReferenceOrDirect(SettingsKey.infill_overlap_perimeter, "infillExtendIntoPerimeter", SettingsKey.nozzle_diameter, change0ToReference: false),
				new OverrideSpeedOnSlaPrinters("infill_speed", "infillSpeed", "infill_speed"),
				new MappedSetting("infill_type", "infillType"),
				new MappedSetting("min_extrusion_before_retract", "minimumExtrusionBeforeRetraction"),
				new MappedSetting("min_print_speed", "minimumPrintingSpeed"),
				new OverrideSpeedOnSlaPrinters("perimeter_speed", "insidePerimetersSpeed", "infill_speed"),
				new MappedSetting("raft_air_gap", "raftAirGap"),
				// fan settings
				new VisibleButNotMappedToEngine("enable_fan"), // this is considered when sending fan speeds to slicing
				new MappedFanSpeedSetting("min_fan_speed", "fanSpeedMinPercent"),
				new MappedSetting("min_fan_speed_layer_time", "minFanSpeedLayerTime"),
				new MappedFanSpeedSetting("max_fan_speed", "fanSpeedMaxPercent"),
				new MappedSetting("max_fan_speed_layer_time", "maxFanSpeedLayerTime"),
				new MappedFanSpeedSetting("bridge_fan_speed", "bridgeFanSpeedPercent"),
				new MappedSetting("disable_fan_first_layers", "firstLayerToAllowFan"),
				// end fan
				new MappedSetting("retract_length_tool_change", "retractionOnExtruderSwitch"),
				new MappedSetting("retract_restart_extra_toolchange", "unretractExtraOnExtruderSwitch"),
				new MappedToBoolString("reset_long_extrusion", "resetLongExtrusion"),
				new MappedSetting("slowdown_below_layer_time", "minimumLayerTimeSeconds"),
				new MappedSetting("support_air_gap", "supportAirGap"),
				new MappedSetting("support_material_infill_angle", "supportInfillStartingAngle"),
				new MappedSetting("support_material_percent", "supportPercent"),
				new MappedSetting("support_material_spacing", "supportLineSpacing"),
				new OverrideSpeedOnSlaPrinters("support_material_speed", "supportMaterialSpeed", "infill_speed"),
				new MappedSetting("support_material_xy_distance", "supportXYDistanceFromObject"),
				new MappedSetting("support_type", "supportType"),
				new MappedSetting("travel_speed", "travelSpeed"),
				new MappedSetting("wipe_shield_distance", "wipeShieldDistanceFromObject"),
				new MappedSetting("wipe_tower_size", "wipeTowerSize"),
				new MappedSetting("z_offset", "zOffset"),
				new MappedSetting(SettingsKey.filament_diameter, "filamentDiameter"),
				new MappedSetting(SettingsKey.layer_height, "layerThickness"),
				new MappedSetting(SettingsKey.nozzle_diameter, "extrusionWidth"),
				new MappedToBoolString("avoid_crossing_perimeters", "avoidCrossingPerimeters"),
				new MappedToBoolString("create_raft", "enableRaft"),
				new MappedToBoolString("external_perimeters_first", "outsidePerimetersFirst"),
				new MappedToBoolString("output_only_first_layer", "outputOnlyFirstLayer"),
				new MappedToBoolString("retract_when_changing_islands", "retractWhenChangingIslands"),
				new MappedToBoolString("support_material", "generateSupport"),
				new MappedToBoolString("support_material_create_internal_support", "generateInternalSupport"),
				new MappedToBoolString("support_material_create_perimeter", "generateSupportPerimeter"),
				new MappedToBoolString("wipe", "wipeAfterRetraction"),
				new MappedToBoolString(SettingsKey.expand_thin_walls, "expandThinWalls"),
				new MappedToBoolString(SettingsKey.merge_overlapping_lines, "MergeOverlappingLines"),
				new MappedToBoolString(SettingsKey.fill_thin_gaps, "fillThinGaps"),
				new MappedToBoolString(SettingsKey.spiral_vase, "continuousSpiralOuterPerimeter"),
				new MapStartGCode(SettingsKey.start_gcode, "startCode", true),
				new MapLayerChangeGCode("layer_gcode", "layerChangeCode"),
				new ScaledSingleNumber("fill_density", "infillPercent", 100),
				new ScaledSingleNumber(SettingsKey.perimeter_start_end_overlap, "perimeterStartEndOverlapRatio", .01),
				new SupportExtrusionWidth("support_material_extrusion_width","supportExtrusionPercent"),
				new ValuePlusConstant("raft_extruder", "raftExtruder", -1),
				new ValuePlusConstant("support_material_extruder", "supportExtruder", -1),
				new ValuePlusConstant("support_material_interface_extruder", "supportInterfaceExtruder", -1),
				new VisibleButNotMappedToEngine("extruder_count"),
				new VisibleButNotMappedToEngine("extruders_share_temperature"),
				new VisibleButNotMappedToEngine("g0"),
				new VisibleButNotMappedToEngine("solid_shell"),
				new VisibleButNotMappedToEngine(SettingsKey.laser_speed_025),
				new VisibleButNotMappedToEngine(SettingsKey.laser_speed_100),
				new VisibleButNotMappedToEngine("selector_ip_address"),
				new VisibleButNotMappedToEngine("selector_ip_address"),
				// Skirt settings
				new MappedSkirtLoopsSetting("skirts", "numberOfSkirtLoops", SettingsKey.nozzle_diameter),
				new MappedSetting("skirt_distance", "skirtDistanceFromObject"),
				new SkirtLengthMapping("min_skirt_length", "skirtMinLength"),
				// Brim settings
				new MappedBrimLoopsSetting("brims", "numberOfBrimLoops", SettingsKey.nozzle_diameter),
			};

			matterSliceSettingNames = new HashSet<string>(mappedSettings.Select(m => m.CanonicalSettingsName));
		}

		public string Name => "MatterSlice";

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

		public bool MapContains(string canonicalSettingsName)
		{
			return matterSliceSettingNames.Contains(canonicalSettingsName)
				|| applicationLevelSettings.Contains(canonicalSettingsName);
		}

		public class ExtruderOffsets : MappedSetting
		{
			public ExtruderOffsets(string canonicalSettingsName, string exportedName)
				: base(canonicalSettingsName, exportedName)
			{
			}

			public override string Value
			{
				get
				{
					// map from 0x0,0x0,0x0
					// to [[0,0],[0,0]]
					StringBuilder final = new StringBuilder("[");
					string[] offsets = base.Value.Split(',');
					bool first = true;
					int count = 0;
					foreach (string offset in offsets)
					{
						if (!first)
						{
							final.Append(",");
						}
						string[] xy = offset.Split('x');
						if (xy.Length == 2)
						{
							double x = 0;
							double.TryParse(xy[0], out x);
							double y = 0;
							double.TryParse(xy[1], out y);
							final.Append($"[{x},{y}]");
							first = false;
							count++;
						}
						else
						{
							final.Append("[0,0]");
						}
					}
					while (count < 16)
					{
						final.Append(",[0,0]");
						count++;
					}
					final.Append("]");

					return final.ToString();
				}
			}
		}

		public class FanTranslator : MappedSetting
		{
			public FanTranslator(string canonicalSettingsName, string exportedName)
				: base(canonicalSettingsName, exportedName)
			{
			}

			public override string Value
			{
				get
				{
					int numLayersFanIsDisabledOn = int.Parse(base.Value);
					int layerToEnableFanOn = numLayersFanIsDisabledOn + 1;
					return layerToEnableFanOn.ToString();
				}
			}
		}

		public class GCodeForSlicer : InjectGCodeCommands
		{
			public GCodeForSlicer(string canonicalSettingsName, string exportedName)
				: base(canonicalSettingsName, exportedName)
			{
			}

			public override string Value => GCodeProcessing.ReplaceMacroValues(base.Value.Replace("\n", "\\n"));
		}

		public class InfillTranslator : MappedSetting
		{
			public InfillTranslator(string canonicalSettingsName, string exportedName)
				: base(canonicalSettingsName, exportedName)
			{
			}

			public override string Value
			{
				get
				{
					double infillRatio0To1 = ParseDouble(base.Value);
					// 400 = solid (extruder width)

					double nozzle_diameter = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.nozzle_diameter);
					double linespacing = 1000;
					if (infillRatio0To1 > .01)
					{
						linespacing = nozzle_diameter / infillRatio0To1;
					}

					return ((int)(linespacing * 1000)).ToString();
				}
			}
		}

		public class SkirtLengthMapping : MappedSetting
		{
			public SkirtLengthMapping(string canonicalSettingsName, string exportedName)
				: base(canonicalSettingsName, exportedName)
			{
			}

			public override string Value
			{
				get
				{
					double lengthToExtrudeMm = ParseDouble(base.Value);
					// we need to convert mm of filament to mm of extrusion path
					double amountOfFilamentCubicMms = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.filament_diameter) * MathHelper.Tau * lengthToExtrudeMm;
					double extrusionSquareSize = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.first_layer_height) * ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.nozzle_diameter);
					double lineLength = amountOfFilamentCubicMms / extrusionSquareSize;

					return lineLength.ToString();
				}
			}
		}

		public class SupportExtrusionWidth : MappedSetting
		{
			public SupportExtrusionWidth(string canonicalSettingsName, string exportedName)
				: base(canonicalSettingsName, exportedName)
			{
			}

			public override string Value
			{
				get
				{
					double nozzleDiameter = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.nozzle_diameter);

					string extrusionWidth = base.Value;

					if (extrusionWidth == "0")
					{
						return "100";
					}

					if (extrusionWidth.Contains("%"))
					{
						string withoutPercent = extrusionWidth.Replace("%", "");
						return withoutPercent;
					}

					double originalValue;
					if (!double.TryParse(extrusionWidth, out originalValue))
					{
						originalValue = nozzleDiameter;
					}

					return (originalValue / nozzleDiameter * 100).ToString();
				}
			}
		}

		public class ValuePlusConstant : MappedSetting
		{
			private double constant;

			public ValuePlusConstant(string canonicalSettingsName, string exportedName, double constant)
				: base(canonicalSettingsName, exportedName)
			{
				this.constant = constant;
			}

			public override string Value => (ParseDouble(base.Value) + constant).ToString();
		}
	}
}