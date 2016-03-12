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

		public HashSet<string> matterSliceSettingNames;

		MappedSetting[] matterSliceSettings;

		// Singleton use only - prevent external construction
		private EngineMappingsMatterSlice() : base("MatterSlice")
		{
			matterSliceSettings = new MappedSetting[]
			{
				//avoidCrossingPerimeters=True # Avoid crossing any of the perimeters of a shape while printing its parts.
				new MappedToBoolString("avoid_crossing_perimeters", "avoidCrossingPerimeters"),

				new MappedToBoolString("external_perimeters_first", "outsidePerimetersFirst"),

				//bottomClipAmount=0 # The amount to clip off the bottom of the part, in millimeters.
				new MappedSetting("bottom_clip_amount", "bottomClipAmount"),

				//centerObjectInXy=True # Describes if 'positionToPlaceObjectCenter' should be used.
				new MappedToBoolString("center_part_on_bed", "centerObjectInXy"),

				//continuousSpiralOuterPerimeter=False # This will cause the z height to raise continuously while on the outer perimeter.
				new MappedToBoolString("spiral_vase", "continuousSpiralOuterPerimeter"),

				//endCode=M104 S0
				new GCodeForSlicer("end_gcode", "endCode"),

				new MappedSetting("z_offset", "zOffset"),

				//extruderOffsets=[[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0]]
				new ExtruderOffsets("extruder_offset", "extruderOffsets"),

				//extrusionWidth=0.4 # The width of the line to extrude.
				new MappedSetting("nozzle_diameter", "extrusionWidth"),

				//fanSpeedMaxPercent=100
				new MappedSetting("max_fan_speed", "fanSpeedMaxPercent"),

				//fanSpeedMinPercent=100
				new MappedSetting("min_fan_speed", "fanSpeedMinPercent"),

				//filamentDiameter=2.89 # The width of the filament being fed into the extruder, in millimeters.
				new MappedSetting("filament_diameter", "filamentDiameter"),

				//extrusionMultiplier=1 # Lets you adjust how much material to extrude.
				new MappedSetting("extrusion_multiplier", "extrusionMultiplier"),

				//firstLayerExtrusionWidth=0.8 # The width of the line to extrude for the first layer.
				new AsPercentOfReferenceOrDirect("first_layer_extrusion_width", "firstLayerExtrusionWidth", "nozzle_diameter"),

				//firstLayerSpeed=20 # mm/s.
				new AsPercentOfReferenceOrDirect("first_layer_speed", "firstLayerSpeed", "infill_speed"),

				//firstLayerThickness=0.3 # The height of the first layer to print, in millimeters.
				new AsPercentOfReferenceOrDirect("first_layer_height", "firstLayerThickness", "layer_height"),

				//firstLayerToAllowFan=2 # The fan will be force to stay off below this layer.
				new MappedSetting("disable_fan_first_layers", "firstLayerToAllowFan"),

				//outputType=REPRAP # Available Values: REPRAP, ULTIGCODE, MAKERBOT, MACH3
				new MappedSetting("gcode_output_type", "outputType"),

				//generateInternalSupport=True # If True, support will be generated within the part as well as from the bed.
				new MappedToBoolString("support_material_create_internal_support", "generateInternalSupport"),
				new MappedToBoolString("support_material_create_perimeter", "generateSupportPerimeter"),

				new MappedToBoolString("output_only_first_layer", "outputOnlyFirstLayer"),

				new MappedToBoolString("support_material", "generateSupport"),

				new MappedSetting("support_material_percent", "supportPercent"),

				//infillExtendIntoPerimeter=0.06 # The amount the infill extends into the perimeter in millimeters.
				new MappedSetting("infill_overlap_perimeter", "infillExtendIntoPerimeter"),

				//infillPercent=20 # The percent of filled space to open space while infilling.
				new ScaledSingleNumber("fill_density", "infillPercent", 100),
				//infillType=GRID # Available Values: GRID, LINES
				new MappedSetting("infill_type", "infillType"),

				//infillSpeed=50 # mm/s.
				new MappedSetting("infill_speed", "infillSpeed"),

				new MappedSetting("bridge_speed", "bridgeSpeed"),

				new MappedSetting("bridge_fan_speed", "bridgeFanSpeedPercent"),

				new MappedToBoolString("retract_when_changing_islands", "retractWhenChangingIslands"),

				new MappedSetting("raft_fan_speed_percent", "raftFanSpeedPercent"),

				new AsPercentOfReferenceOrDirect("raft_print_speed", "raftPrintSpeed", "infill_speed"),

				//infillStartingAngle=45
				new MappedSetting("fill_angle", "infillStartingAngle"),

				new MappedToBoolString("wipe", "wipeAfterRetraction"),

				//insidePerimetersSpeed=50 # The speed of all perimeters but the outside one. mm/s.
				new MappedSetting("perimeter_speed", "insidePerimetersSpeed"),

				//layerThickness=0.1
				new MappedSetting("layer_height", "layerThickness"),

				//minimumExtrusionBeforeRetraction=0 # mm.
				new MappedSetting("min_extrusion_before_retract", "minimumExtrusionBeforeRetraction"),

				//minimumPrintingSpeed=10 # The minimum speed that the extruder is allowed to move while printing. mm/s.
				new MappedSetting("min_print_speed", "minimumPrintingSpeed"),

				//minimumLayerTimeSeconds=5
				new MappedSetting("slowdown_below_layer_time", "minimumLayerTimeSeconds"),

				new MapFirstValue("retract_restart_extra", "unretractExtraExtrusion"),

				//minimumTravelToCauseRetraction=1.5 # The minimum travel distance that will require a retraction
				new MapFirstValue("retract_before_travel", "minimumTravelToCauseRetraction"),

				//modelRotationMatrix=[[1,0,0],[0,1,0],[0,0,1]]
				//multiVolumeOverlapPercent=0

				//numberOfBottomLayers=6
				new AsCountOrDistance("bottom_solid_layers", "numberOfBottomLayers", "layer_height"),

				new VisibleButNotMappedToEngine("solid_shell"),

				//numberOfSkirtLoops=1 # The number of loops to draw around the convex hull.
				new MappedSetting("skirts", "numberOfSkirtLoops"),

				//numberOfBrimLoops=0 # The number of loops to draw around islands.
				new MappedSetting("brims", "numberOfBrimLoops"),

				//numberOfTopLayers=6
				new AsCountOrDistance("top_solid_layers", "numberOfTopLayers", "layer_height"),

				new AsPercentOfReferenceOrDirect("top_solid_infill_speed", "topInfillSpeed", "infill_speed"),

				//outsidePerimeterSpeed=50 # The speed of the first perimeter. mm/s.
				new AsPercentOfReferenceOrDirect("external_perimeter_speed", "outsidePerimeterSpeed", "perimeter_speed"),

				//outsidePerimeterExtrusionWidth=extrusionWidth=nozzleDiameter
				new AsPercentOfReferenceOrDirect("external_perimeter_extrusion_width", "outsidePerimeterExtrusionWidth", "nozzle_diameter"),

				//numberOfPerimeters=2
				new AsCountOrDistance("perimeters", "numberOfPerimeters", "nozzle_diameter"),

				//positionToPlaceObjectCenter=[102.5,102.5]
				new MapPositionToPlaceObjectCenter("print_center", "positionToPlaceObjectCenter"),

				// TODO: The raft currently does not handle brim correctly. So it needs to be fixed before it is enabled.
				new MappedToBoolString("create_raft", "enableRaft"),
				new MappedSetting("raft_extra_distance_around_part", "raftExtraDistanceAroundPart"),
				new MappedSetting("raft_air_gap", "raftAirGap"),
				new MappedSetting("support_air_gap", "supportAirGap"),
			
				//retractionOnExtruderSwitch=14.5
				new MappedSetting("retract_length_tool_change", "retractionOnExtruderSwitch"),

				new MapFirstValue("retract_length", "retractionOnTravel"),
				//retractionOnTravel=4.5
				//new MapItem("retractionOnTravel", "retract_before_travel"),

				//retractionSpeed=45 # mm/s.
				new MapFirstValue("retract_speed", "retractionSpeed"),

				//retractionZHop=0 # The amount to move the extruder up in z after retracting (before a move). mm.
				new MapFirstValue("retract_lift", "retractionZHop"),

				//skirtDistanceFromObject=6 # How far from objects the first skirt loop should be, in millimeters.
				new MappedSetting("skirt_distance", "skirtDistanceFromObject"),

				//skirtMinLength=0 # The minimum length of the skirt line, in millimeters.
				new SkirtLengthMapping("min_skirt_length", "skirtMinLength"),

				//startCode=M109 S210
				new MapStartGCode("start_gcode", "startCode", true),

				new GCodeForSlicer("toolchange_gcode", "toolChangeCode"),

				//supportExtruder=1
				new ValuePlusConstant("support_material_extruder", "supportExtruder", -1),

				new ValuePlusConstant("support_material_interface_extruder", "supportInterfaceExtruder", -1),

				new ValuePlusConstant("raft_extruder", "raftExtruder", -1),

				//supportLineSpacing=2
				new MappedSetting("support_material_spacing", "supportLineSpacing"),

				new SupportExtrusionWidth("support_material_extrusion_width","supportExtrusionPercent"),

				new MappedSetting("support_material_infill_angle", "supportInfillStartingAngle"),

				//supportMaterialSpeed=50 # mm/s.
				new MappedSetting("support_material_speed", "supportMaterialSpeed"),

				//supportType=NONE # Available Values: NONE, GRID, LINES
				new MappedSetting("support_type", "supportType"),

				//supportXYDistanceFromObject=0.7 # The closest xy distance that support will be to the object. mm/s.
				new MappedSetting("support_material_xy_distance", "supportXYDistanceFromObject"),

				new AsCountOrDistance("support_material_interface_layers", "supportInterfaceLayers", "layer_height"),

				//travelSpeed=200 # The speed to move when not extruding material. mm/s.
				new MappedSetting("travel_speed", "travelSpeed"),

				//wipeShieldDistanceFromObject=0 # If greater than 0 this creates an outline around shapes so the extrude will be wiped when entering.
				new MappedSetting("wipe_shield_distance", "wipeShieldDistanceFromObject"),

				// TODO: We don't need this yet as it is only for dual extrusion
				//wipeTowerSize=0 # Unlike the wipe shield this is a square of size*size in the lower left corner for wiping during extruder changing.
				new MappedSetting("wipe_tower_size", "wipeTowerSize"),
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
					double nozzleDiameter = ActiveSliceSettings.Instance.NozzleDiameter;

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
					double nozzle_diameter = ParseDoubleFromRawValue("nozzle_diameter");
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
					Vector2 PrinteCenter = ActiveSliceSettings.Instance.PrintCenter;

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
					double amountOfFilamentCubicMms = ActiveSliceSettings.Instance.FilamentDiameter * MathHelper.Tau * lengthToExtrudeMm;
					double extrusionSquareSize = ActiveSliceSettings.Instance.FirstLayerHeight * ActiveSliceSettings.Instance.NozzleDiameter;
					double lineLength = amountOfFilamentCubicMms / extrusionSquareSize;

					return lineLength.ToString();
				}
			}
		}
	}
}