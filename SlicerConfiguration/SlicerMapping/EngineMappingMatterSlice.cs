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

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.VectorMath;
using MatterHackers.Agg;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
    public class EngineMappingsMatterSlice : SliceEngineMaping
    {
        // private so that this class is a sigleton
        EngineMappingsMatterSlice()
        {
        }

        static EngineMappingsMatterSlice instance = null;
        public static EngineMappingsMatterSlice Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new EngineMappingsMatterSlice();
                }
                return instance;
            }
        }

        public override bool MapContains(string originalKey)
        {
            foreach (MapItem mapItem in matterSliceToDefaultMapping)
            {
                if (mapItem.OriginalKey == originalKey)
                {
                    return true;
                }
            }

            return false;
        }

        static MapItem[] matterSliceToDefaultMapping = 
        {
            //avoidCrossingPerimeters=True # Avoid crossing any of the perimeters of a shape while printing its parts.
            new MapItemToBool("avoidCrossingPerimeters", "avoid_crossing_perimeters"),
             
            //bottomClipAmount=0 # The amount to clip off the bottom of the part, in millimeters.
            new MapItem("bottomClipAmount", "bottom_clip_amount"),
            
            //centerObjectInXy=True # Describes if 'positionToPlaceObjectCenter' should be used.
            new MapItemToBool("centerObjectInXy", "center_part_on_bed"),
            
            //continuousSpiralOuterPerimeter=False # This will cause the z height to raise continuously while on the outer perimeter.
            new MapItemToBool("continuousSpiralOuterPerimeter", "spiral_vase"),
            
            //doCoolHeadLift=False # Will cause the head to be raised in z until the min layer time is reached.
            new MapItemToBool("doCoolHeadLift", "cool_extruder_lift"),
            
            //endCode=M104 S0
            new MapEndGCode("endCode", "end_gcode"),

            //extruderOffsets=[[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0],[0,0]]

            //extrusionWidth=0.4 # The width of the line to extrude.
            new MapItem("extrusionWidth", "nozzle_diameter"),
            
            //fanSpeedMaxPercent=100
            new MapItem("fanSpeedMaxPercent", "max_fan_speed"),

            //fanSpeedMinPercent=100
            new MapItem("fanSpeedMinPercent", "min_fan_speed"),

            //filamentDiameter=2.89 # The width of the filament being fed into the extruder, in millimeters.
            new MapItem("filamentDiameter", "filament_diameter"),
            
            //extrusionMultiplier=1 # Lets you adjust how much material to extrude.
            new MapItem("extrusionMultiplier", "extrusion_multiplier"),

            //firstLayerExtrusionWidth=0.8 # The width of the line to extrude for the first layer.
            new FirstLayerHeight("firstLayerExtrusionWidth", "first_layer_extrusion_width", "nozzle_diameter"),

            //firstLayerSpeed=20 # mm/s.
            new AsPercentOfReferenceOrDirect("firstLayerSpeed", "first_layer_speed", "infill_speed"),

            //firstLayerThickness=0.3 # The height of the first layer to print, in millimeters.
            new AsPercentOfReferenceOrDirect("firstLayerThickness", "first_layer_height", "layer_height", 1),

            //firstLayerToAllowFan=2 # The fan will be force to stay off below this layer.
            new MapItem("firstLayerToAllowFan", "disable_fan_first_layers"),

            //outputType=REPRAP # Available Values: REPRAP, ULTIGCODE, MAKERBOT, BFB, MACH3
            new MapItem("outputType", "gcode_output_type"),
            
            //generateInternalSupport=True # If True, support will be generated within the part as well as from the bed.
            new MapItemToBool("generateInternalSupport", "support_material_create_internal_support"),
            
            //infillExtendIntoPerimeter=0.06 # The amount the infill extends into the perimeter in millimeters.
            new MapItem("infillExtendIntoPerimeter", "infill_overlap_perimeter"),
            
            //infillPercent=20 # The percent of filled space to open space while infilling.
            new ScaledSingleNumber("infillPercent", "fill_density", 100),
            //infillType=GRID # Available Values: GRID, LINES
            new MapItem("infillType", "infill_type"),

            //infillSpeed=50 # mm/s.
            new MapItem("infillSpeed", "infill_speed"),

            new MapItem("bridgeSpeed", "bridge_speed"),

            new MapItem("bridgeFanSpeedPercent", "bridge_fan_speed"),
            
            //infillStartingAngle=45
            new MapItem("infillStartingAngle", "fill_angle"),

            new MapItem("supportInfillStartingAngle", "support_material_infill_angle"),            

            //insidePerimetersSpeed=50 # The speed of all perimeters but the outside one. mm/s.
            new MapItem("insidePerimetersSpeed", "perimeter_speed"),

            //layerThickness=0.1
            new MapItem("layerThickness", "layer_height"),

            //minimumExtrusionBeforeRetraction=0 # mm.
            new MapItem("minimumExtrusionBeforeRetraction", "min_extrusion_before_retract"),

            //minimumPrintingSpeed=10 # The minimum speed that the extruder is allowed to move while printing. mm/s.
            new MapItem("minimumPrintingSpeed", "min_print_speed"),            
            
            //minimumLayerTimeSeconds=5
            new MapItem("minimumLayerTimeSeconds", "slowdown_below_layer_time"),

            //minimumTravelToCauseRetraction=1.5 # The minimum travel distance that will require a retraction
            new MapItem("minimumTravelToCauseRetraction", "retract_before_travel"),            

            //modelRotationMatrix=[[1,0,0],[0,1,0],[0,0,1]]
            //multiVolumeOverlapPercent=0

            //numberOfBottomLayers=6
            new MapItem("numberOfBottomLayers", "bottom_solid_layers"),
            
            //numberOfSkirtLoops=1 # The number of loops to draw around objects. Can be used to help hold them down.
            new MapItem("numberOfSkirtLoops", "skirts"),

            //numberOfTopLayers=6
            new MapItem("numberOfTopLayers", "top_solid_layers"),

            //outsidePerimeterSpeed=50 # The speed of the first perimeter. mm/s.
            new AsPercentOfReferenceOrDirect("outsidePerimeterSpeed", "external_perimeter_speed", "perimeter_speed"),
            
            //numberOfPerimeters=2
            new MapItem("numberOfPerimeters", "perimeters"),

            //positionToPlaceObjectCenter=[102.5,102.5]
            new MapPositionToPlaceObjectCenter("positionToPlaceObjectCenter", "print_center"),

            // TODO: The raft currently does not handle brim correctly. So it needs to be fixed before it is enabled.
            new MapItemToBool("enableRaft", "create_raft"),
            new MapItem("raftExtraDistanceAroundPart", "raft_extra_distance_around_part"),
            new MapItem("raftAirGap", "raft_air_gap"),

            //repairOutlines=NONE # Available Values: NONE, EXTENSIVE_STITCHING, KEEP_OPEN # You can or them together using '|'.
            new MapRepairOutlines("repairOutlines", "repair_outlines_extensive_stitching"),
            new NotPassedItem("", "repair_outlines_keep_open"),

#if true
            new NotPassedItem("", "has_fan"),
            new NotPassedItem("", "has_heated_bed"),
            new NotPassedItem("", "has_sd_card_reader"),
#endif

            //repairOverlaps=NONE # Available Values: NONE, REVERSE_ORIENTATION, UNION_ALL_TOGETHER # You can or them together using '|'.
            new MapRepairOverlaps("repairOverlaps", "repair_overlaps_reverse_orientation"),
            new NotPassedItem("", "repair_overlaps_union_all_together"),

            //retractionOnExtruderSwitch=14.5
            new MapItem("retractionOnExtruderSwitch", "retract_length_tool_change"),
            
            new MapItem("retractionOnTravel", "retract_length"),
            //retractionOnTravel=4.5
            //new MapItem("retractionOnTravel", "retract_before_travel"),

            //retractionSpeed=45 # mm/s.
            new MapItem("retractionSpeed", "retract_speed"),
            
            //retractionZHop=0 # The amount to move the extruder up in z after retracting (before a move). mm.
            new MapItem("retractionZHop", "retract_lift"),
            
            //skirtDistanceFromObject=6 # How far from objects the first skirt loop should be, in millimeters.
            new MapItem("skirtDistanceFromObject", "skirt_distance"),

            //skirtMinLength=0 # The minimum length of the skirt line, in millimeters.
            new SkirtLengthMaping("skirtMinLength", "min_skirt_length"),

            //startCode=M109 S210
            new MapStartGCode("startCode", "start_gcode", true),

            //supportExtruder=1
            new ValuePlusConstant("supportExtruder", "support_material_extruder", -1),

            //supportLineSpacing=2
            new MapItem("supportLineSpacing", "support_material_spacing"),            

            //supportMaterialSpeed=50 # mm/s.
            new MapItem("supportMaterialSpeed", "support_material_speed"),
            
            // get the check box on the screen
            new SupportMatterial("supportEndAngle", "support_material"),
            new NotPassedItem("", "support_material_threshold"),

            //supportType=NONE # Available Values: NONE, GRID, LINES
            new MapItem("supportType", "support_type"),
            
            //supportXYDistanceFromObject=0.7 # The closest xy distance that support will be to the object. mm/s.
            new MapItem("supportXYDistanceFromObject", "support_material_xy_distance"),
            
            //supportZDistanceFromObject=1 # The number of layers to skip in z. The gap between the support and the model.
            new MapItem("supportNumberOfLayersToSkipInZ", "support_material_z_gap_layers"),

            //travelSpeed=200 # The speed to move when not extruding material. mm/s.
            new MapItem("travelSpeed", "travel_speed"),
            
            //wipeShieldDistanceFromObject=0 # If greater than 0 this creates an outline around shapes so the extrude will be wiped when entering.
            new MapItem("wipeShieldDistanceFromObject", "wipe_shield_distance"),
            
            // TODO: We don't need this yet as it is only for dual extrusion
            //wipeTowerSize=0 # Unlike the wipe shield this is a square of size*size in the lower left corner for wiping during extruder changing.

            new NotPassedItem("", "pause_gcode"),
            new NotPassedItem("", "resume_gcode"),
            new NotPassedItem("", "cancel_gcode"),

            new NotPassedItem("", "bed_size"),
            new NotPassedItem("", "build_height"),

            new NotPassedItem("", "temperature"),
            new NotPassedItem("", "bed_temperature"),
            new NotPassedItem("", "bed_shape"),
        };

        public static void WriteMatterSliceSettingsFile(string outputFilename)
        {
            using (StreamWriter sliceSettingsFile = new StreamWriter(outputFilename))
            {
                for (int i = 0; i < matterSliceToDefaultMapping.Length; i++)
                {
                    string matterSliceValue = matterSliceToDefaultMapping[i].MappedValue;
                    if (matterSliceValue != null && matterSliceValue != "")
                    {
                        sliceSettingsFile.WriteLine("{0} = {1}".FormatWith(matterSliceToDefaultMapping[i].MappedKey, matterSliceValue));
                    }
                }
            }
        }

        public class FirstLayerHeight : ScaledSingleNumber
        {
            internal string originalReference;
            public override string MappedValue
            {
                get
                {
                    string finalValueString = base.MappedValue;

                    if (OriginalValue.Contains("%"))
                    {
                        string withoutPercent = OriginalValue.Replace("%", "");
                        double ratio = MapItem.ParseValueString(withoutPercent, 100) / 100.0;
                        double valueToModify = MapItem.GetValueForKey(originalReference);
                        double finalValue = valueToModify * ratio * scale;
                        finalValueString = finalValue.ToString();
                    }

                    if (finalValueString.Trim() == "0")
                    {
                        return ActiveSliceSettings.Instance.GetActiveValue(originalReference);
                    }
                    return finalValueString;
                }
            }

            public FirstLayerHeight(string mappedKey, string originalKey, string originalReference, double scale = 1)
                : base(mappedKey, originalKey, scale)
            {
                this.originalReference = originalReference;
            }
        }

        //repairOutlines=NONE # Available Values: NONE, EXTENSIVE_STITCHING, KEEP_OPEN # You can or them together using '|'.
        public class MapRepairOutlines : MapItem
        {
            public override string MappedValue
            {
                get
                {
                    if(ActiveSliceSettings.Instance.GetActiveValue("repair_outlines_extensive_stitching") == "1")
                    {
                        if (ActiveSliceSettings.Instance.GetActiveValue("repair_outlines_keep_open") == "1")
                        {
                            return "EXTENSIVE_STITCHING|KEEP_OPEN";
                        }
                        else
                        {
                            return "EXTENSIVE_STITCHING";
                        }
                    }
                    else if(ActiveSliceSettings.Instance.GetActiveValue("repair_outlines_keep_open") == "1")
                    {
                        return "KEEP_OPEN";
                    }

                    return "NONE";
                }
            }

            public MapRepairOutlines(string mappedKey, string originalKey)
                : base(mappedKey, originalKey)
            {
            }
        }

        //repairOverlaps=NONE # Available Values: NONE, REVERSE_ORIENTATION, UNION_ALL_TOGETHER # You can or them together using '|'.
        public class MapRepairOverlaps : MapItem
        {
            public override string MappedValue
            {
                get
                {
                    if(ActiveSliceSettings.Instance.GetActiveValue("repair_overlaps_reverse_orientation") == "1")
                    {
                        if (ActiveSliceSettings.Instance.GetActiveValue("repair_overlaps_union_all_together") == "1")
                        {
                            return "REVERSE_ORIENTATION|UNION_ALL_TOGETHER";
                        }
                        else
                        {
                            return "REVERSE_ORIENTATION";
                        }
                    }
                    else if(ActiveSliceSettings.Instance.GetActiveValue("repair_overlaps_union_all_together") == "1")
                    {
                        return "UNION_ALL_TOGETHER";
                    }

                    return "NONE";
                }
            }

            public MapRepairOverlaps(string mappedKey, string originalKey)
                : base(mappedKey, originalKey)
            {
            }
        }

        public class FanTranslator : MapItem
        {
            public override string MappedValue
            {
                get
                {
                    int numLayersFanIsDisabledOn = int.Parse(base.MappedValue);
                    int layerToEnableFanOn = numLayersFanIsDisabledOn + 1;
                    return layerToEnableFanOn.ToString();
                }
            }

            public FanTranslator(string mappedKey, string originalKey)
                : base(mappedKey, originalKey)
            {
            }
        }

        public class SupportMatterial : MapItem
        {
            public SupportMatterial(string mappedKey, string originalKey)
                : base(mappedKey, originalKey)
            {
            }

            public override string MappedValue
            {
                get
                {
                    string supportMaterial = ActiveSliceSettings.Instance.GetActiveValue("support_material");
                    if (supportMaterial == "0")
                    {
                        return "-1";
                    }

                    return (MapItem.GetValueForKey("support_material_threshold")).ToString();
                }
            }
        }

        public class ConstantMinusValue : MapItem
        {
            double constant;

            public ConstantMinusValue(string mappedKey, string originalKey, double constant)
                : base(mappedKey, originalKey)
            {
                this.constant = constant;
            }

            public override string MappedValue
            {
                get
                {
                    return (90 - MapItem.ParseValueString(OriginalValue)).ToString();
                }
            }
        }

        public class ValuePlusConstant : MapItem
        {
            double constant;

            public ValuePlusConstant(string mappedKey, string originalKey, double constant)
                : base(mappedKey, originalKey)
            {
                this.constant = constant;
            }

            public override string MappedValue
            {
                get
                {
                    return (MapItem.ParseValueString(OriginalValue) + constant).ToString();
                }
            }
        }

        public class InfillTranslator : MapItem
        {
            public override string MappedValue
            {
                get
                {
                    double infillRatio0To1 = MapItem.ParseValueString(base.MappedValue);
                    // 400 = solid (extruder width)
                    double nozzle_diameter = MapItem.GetValueForKey("nozzle_diameter");
                    double linespacing = 1000;
                    if (infillRatio0To1 > .01)
                    {
                        linespacing = nozzle_diameter / infillRatio0To1;
                    }

                    return ((int)(linespacing * 1000)).ToString();
                }
            }

            public InfillTranslator(string mappedKey, string originalKey)
                : base(mappedKey, originalKey)
            {
            }
        }

        public class MapEndGCode : InjectGCodeCommands
        {
            public override string MappedValue
            {
                get
                {
                    return base.MappedValue.Replace("\n", "\\n");
                }
            }

            public MapEndGCode(string mappedKey, string originalKey)
                : base(mappedKey, originalKey)
            {
            }
        }

        public class MapPositionToPlaceObjectCenter : MapItem
        {
            public MapPositionToPlaceObjectCenter(string mappedKey, string originalKey)
                : base(mappedKey, originalKey)
            {
            }

            public override string MappedValue
            {
                get
                {
                    Vector2 PrinteCenter = ActiveSliceSettings.Instance.PrintCenter;

                    return "[{0},{1}]".FormatWith(PrinteCenter.x, PrinteCenter.y);
                }
            }
        }

        public class SkirtLengthMaping : MapItem
        {
            public SkirtLengthMaping(string mappedKey, string originalKey)
                : base(mappedKey, originalKey)
            {
            }

            public override string MappedValue
            {
                get
                {
                    double lengthToExtrudeMm = MapItem.ParseValueString(base.MappedValue);
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
