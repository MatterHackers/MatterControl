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
	public class EngineMappingsMatterSlice : SliceEngineMapping
	{
		public static readonly EngineMappingsMatterSlice Instance = new EngineMappingsMatterSlice();

		private HashSet<string> matterSliceSettingNames;

		private MappedSetting[] matterSliceSettings;

		// Singleton use only - prevent external construction
		private EngineMappingsMatterSlice() : base("MatterSlice")
		{
			matterSliceSettings = new MappedSetting[]
			{
				new MappedToBoolString("avoid_crossing_perimeters", "avoidCrossingPerimeters"),
				new MappedToBoolString("external_perimeters_first", "outsidePerimetersFirst"),
				new MappedSetting(SettingsKey.bottom_clip_amount, "bottomClipAmount"),
				new MappedToBoolString(SettingsKey.center_part_on_bed, "centerObjectInXy"),
				new MappedToBoolString(SettingsKey.spiral_vase, "continuousSpiralOuterPerimeter"),
				new GCodeForSlicer("end_gcode", "endCode"),
				new MappedSetting("z_offset", "zOffset"),
				new ExtruderOffsets("extruder_offset", "extruderOffsets"),
				new MappedSetting(SettingsKey.nozzle_diameter, "extrusionWidth"),
				new MappedSetting("max_fan_speed", "fanSpeedMaxPercent"),
				new MappedSetting("min_fan_speed", "fanSpeedMinPercent"),
				new MappedSetting(SettingsKey.filament_diameter, "filamentDiameter"),
				new ScaledSingleNumber(SettingsKey.perimeter_start_end_overlap, "perimeterStartEndOverlapRatio", .01),
				new MappedSetting("extrusion_multiplier", "extrusionMultiplier"),
				new AsPercentOfReferenceOrDirect(SettingsKey.first_layer_extrusion_width, "firstLayerExtrusionWidth", SettingsKey.nozzle_diameter),
				new AsPercentOfReferenceOrDirect("first_layer_speed", "firstLayerSpeed", "infill_speed"),
				new AsPercentOfReferenceOrDirect(SettingsKey.first_layer_height, "firstLayerThickness", SettingsKey.layer_height),
				new MappedSetting("disable_fan_first_layers", "firstLayerToAllowFan"),
				new MappedToBoolString("support_material_create_internal_support", "generateInternalSupport"),
				new MappedToBoolString("support_material_create_perimeter", "generateSupportPerimeter"),
				new MappedToBoolString("output_only_first_layer", "outputOnlyFirstLayer"),
				new MappedToBoolString("support_material", "generateSupport"),
				new MappedSetting("support_material_percent", "supportPercent"),
				new MappedSetting("infill_overlap_perimeter", "infillExtendIntoPerimeter"),
				new ScaledSingleNumber("fill_density", "infillPercent", 100),
				new MappedSetting("infill_type", "infillType"),
				new MappedSetting("infill_speed", "infillSpeed"),
				new MappedSetting("bridge_speed", "bridgeSpeed"),
				new MappedSetting("bridge_fan_speed", "bridgeFanSpeedPercent"),
				new MappedToBoolString("retract_when_changing_islands", "retractWhenChangingIslands"),
				new MappedSetting("raft_fan_speed_percent", "raftFanSpeedPercent"),
				new AsPercentOfReferenceOrDirect("raft_print_speed", "raftPrintSpeed", "infill_speed"),
				new MappedSetting("fill_angle", "infillStartingAngle"),
				new MappedToBoolString("wipe", "wipeAfterRetraction"),
				new MappedSetting("perimeter_speed", "insidePerimetersSpeed"),
				new MappedSetting(SettingsKey.layer_height, "layerThickness"),
				new MappedSetting("min_extrusion_before_retract", "minimumExtrusionBeforeRetraction"),
				new MappedSetting("min_print_speed", "minimumPrintingSpeed"),
				new MappedSetting("slowdown_below_layer_time", "minimumLayerTimeSeconds"),
				new MapFirstValue("retract_restart_extra", "unretractExtraExtrusion"),
				new MapFirstValue("retract_before_travel", "minimumTravelToCauseRetraction"),
				new AsCountOrDistance("bottom_solid_layers", "numberOfBottomLayers", SettingsKey.layer_height),
				new MappedSetting("skirts", "numberOfSkirtLoops"),
				new MappedSetting("brims", "numberOfBrimLoops"),
				new AsCountOrDistance("top_solid_layers", "numberOfTopLayers", SettingsKey.layer_height),
				new AsPercentOfReferenceOrDirect("top_solid_infill_speed", "topInfillSpeed", "infill_speed"),
				new AsPercentOfReferenceOrDirect("external_perimeter_speed", "outsidePerimeterSpeed", "perimeter_speed"),
				new AsPercentOfReferenceOrDirect("external_perimeter_extrusion_width", "outsidePerimeterExtrusionWidth", SettingsKey.nozzle_diameter),
				new AsCountOrDistance("perimeters", "numberOfPerimeters", SettingsKey.nozzle_diameter),
				new MapPositionToPlaceObjectCenter(SettingsKey.print_center, "positionToPlaceObjectCenter"),
				// TODO: The raft currently does not handle brim correctly. So it needs to be fixed before it is enabled.
				new MappedToBoolString("create_raft", "enableRaft"),
				new MappedSetting("raft_extra_distance_around_part", "raftExtraDistanceAroundPart"),
				new MappedSetting("raft_air_gap", "raftAirGap"),
				new MappedSetting("support_air_gap", "supportAirGap"),
				new MappedSetting("retract_length_tool_change", "retractionOnExtruderSwitch"),
				new MappedSetting("retract_restart_extra_toolchange", "unretractExtraOnExtruderSwitch"),
				new MapFirstValue("retract_length", "retractionOnTravel"),
				new MapFirstValue("retract_speed", "retractionSpeed"),
				new MapFirstValue("retract_lift", "retractionZHop"),
				new MappedSetting("skirt_distance", "skirtDistanceFromObject"),
				new SkirtLengthMapping("min_skirt_length", "skirtMinLength"),
				new MapStartGCode("start_gcode", "startCode", true),
				new GCodeForSlicer("toolchange_gcode", "toolChangeCode"),
				new GCodeForSlicer("before_toolchange_gcode", "beforeToolchangeCode"),
				new ValuePlusConstant("support_material_extruder", "supportExtruder", -1),
				new ValuePlusConstant("support_material_interface_extruder", "supportInterfaceExtruder", -1),
				new ValuePlusConstant("raft_extruder", "raftExtruder", -1),
				new MappedSetting("support_material_spacing", "supportLineSpacing"),
				new SupportExtrusionWidth("support_material_extrusion_width","supportExtrusionPercent"),
				new MappedSetting("support_material_infill_angle", "supportInfillStartingAngle"),
				new MappedSetting("support_material_speed", "supportMaterialSpeed"),
				new MappedSetting("support_type", "supportType"),
				new MappedSetting("support_material_xy_distance", "supportXYDistanceFromObject"),
				new AsCountOrDistance("support_material_interface_layers", "supportInterfaceLayers", SettingsKey.layer_height),
				new MappedSetting("travel_speed", "travelSpeed"),
				new MappedSetting("wipe_shield_distance", "wipeShieldDistanceFromObject"),
				new MappedSetting("wipe_tower_size", "wipeTowerSize"),

				// Enable MatterControl behaviors that are unique to MatterSlice only
				new VisibleButNotMappedToEngine("solid_shell"),
				new VisibleButNotMappedToEngine("extruder_count"),
				new VisibleButNotMappedToEngine("extruders_share_temperature"),
			};

			matterSliceSettingNames = new HashSet<string>(matterSliceSettings.Select(m => m.CanonicalSettingsName));
		}

		public override bool MapContains(string canonicalSettingsName)
		{
			return matterSliceSettingNames.Contains(canonicalSettingsName)
				|| base.applicationLevelSettings.Contains(canonicalSettingsName);
		}

		public static void WriteMatterSliceSettingsFile(string outputFilename)
		{
			using (StreamWriter sliceSettingsFile = new StreamWriter(outputFilename))
			{
				foreach (MappedSetting mappedSetting in Instance.matterSliceSettings)
				{
					if (mappedSetting.Value != null)
					{
						sliceSettingsFile.WriteLine("{0} = {1}".FormatWith(mappedSetting.ExportedName, mappedSetting.Value));
					}
				}
			}
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
						final.Append("[{0},{1}]".FormatWith(double.Parse(xy[0]), double.Parse(xy[1])));
						first = false;
						count++;
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
					double nozzle_diameter = ParseDoubleFromRawValue(SettingsKey.nozzle_diameter);
					double linespacing = 1000;
					if (infillRatio0To1 > .01)
					{
						linespacing = nozzle_diameter / infillRatio0To1;
					}

					return ((int)(linespacing * 1000)).ToString();
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

		public class MapPositionToPlaceObjectCenter : MappedSetting
		{
			public MapPositionToPlaceObjectCenter(string canonicalSettingsName, string exportedName)
				: base(canonicalSettingsName, exportedName)
			{
			}

			public override string Value
			{
				get
				{
					Vector2 PrinteCenter = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center);

					return "[{0},{1}]".FormatWith(PrinteCenter.x, PrinteCenter.y);
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
	}
}