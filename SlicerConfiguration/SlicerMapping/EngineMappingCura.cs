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
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.VectorMath;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
    public class CuraEngineMappings : SliceEngineMaping
    {
        // private so that this class is a sigleton
        CuraEngineMappings()
        {
        }

        static CuraEngineMappings instance = null;
        public static CuraEngineMappings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new CuraEngineMappings();
                }
                return instance;
            }
        }

        public override bool MapContains(string defaultKey)
        {
            foreach (MapItem mapItem in curaToDefaultMapping)
            {
                if (mapItem.DefaultKey == defaultKey)
                {
                    return true;
                }
            }

            return false;
        }

        static MapItem[] curaToDefaultMapping = 
        {
            new ScaledSingleNumber("layerThickness", "layer_height", 1000),
            new AsPercentOfReferenceOrDirect("initialLayerThickness", "first_layer_height", "layer_height", 1000),
            new ScaledSingleNumber("filamentDiameter", "filament_diameter", 1000),
            new ScaledSingleNumber("extrusionWidth", "nozzle_diameter", 1000),

            new MapItem("printSpeed", "perimeter_speed"),
            new MapItem("infillSpeed", "infill_speed"),
            new MapItem("moveSpeed", "travel_speed"),
            new AsPercentOfReferenceOrDirect("initialLayerSpeed", "first_layer_speed", "infill_speed"),

            new NotPassedItem("", "temperature"),
            new NotPassedItem("", "bed_temperature"),
            new NotPassedItem("", "bed_shape"),

            new MapItem("insetCount", "perimeters"),

            new MapItem("skirtLineCount", "skirts"),
            new SkirtLengthMaping("skirtMinLength", "min_skirt_length"),
            new ScaledSingleNumber("skirtDistance", "skirt_distance", 1000),

            new MapItem("fanSpeedMin", "max_fan_speed"),
            new MapItem("fanSpeedMax", "min_fan_speed"),

            new MapItem("downSkinCount", "bottom_solid_layers"),
            new MapItem("upSkinCount", "top_solid_layers"),

            new FanTranslator("fanFullOnLayerNr", "disable_fan_first_layers"),
            new MapItem("coolHeadLift", "cool_extruder_lift"),

            new ScaledSingleNumber("retractionAmount", "retract_length", 1000),
            new MapItem("retractionSpeed", "retract_speed"),
            new ScaledSingleNumber("retractionMinimalDistance", "retract_before_travel", 1000),
            new ScaledSingleNumber("minimalExtrusionBeforeRetraction", "min_extrusion_before_retract", 1000),

            new MapItem("spiralizeMode", "spiral_vase"),

            new NotPassedItem("", "bed_size"),

            new PrintCenterX("posx", "print_center"),
            new PrintCenterY("posy", "print_center"),

            new NotPassedItem("", "build_height"),

            // needs testing, not working
            new ScaledSingleNumber("supportLineDistance", "support_material_spacing", 1000),
            new SupportMatterial("supportAngle", "support_material"),
            new NotPassedItem("", "support_material_threshold"),
            new MapItem("supportEverywhere", "support_material_create_internal_support"),
            new ScaledSingleNumber("supportXYDistance", "support_material_xy_distance", 1000),
            new ScaledSingleNumber("supportZDistance", "support_material_z_distance", 1000),

            new MapItem("minimalLayerTime", "slowdown_below_layer_time"),

            new InfillTranslator("sparseInfillLineDistance", "fill_density"),

            new MapStartGCode("startCode", "start_gcode"),
            new MapEndGCode("endCode", "end_gcode"),
            
#if false
            SETTING(filamentFlow);
            SETTING(infillOverlap);

            SETTING(initialSpeedupLayers);

            SETTING(supportExtruder);

            SETTING(retractionAmountExtruderSwitch);
            SETTING(enableCombing);
            SETTING(multiVolumeOverlap);
            SETTING(objectSink);

            SETTING(raftMargin);
            SETTING(raftLineSpacing);
            SETTING(raftBaseThickness);
            SETTING(raftBaseLinewidth);
            SETTING(raftInterfaceThickness);
            SETTING(raftInterfaceLinewidth);

            SETTING(minimalFeedrate);

fanFullOnLayerNr = 2;

            SETTING(fixHorrible);
            SETTING(gcodeFlavor);

/*
objectPosition.X = 102500;
objectPosition.Y = 102500;
enableOozeShield = 0;
*/
#endif
        };

        public static string GetCuraCommandLineSettings()
        {
            StringBuilder settings = new StringBuilder();
            for (int i = 0; i < curaToDefaultMapping.Length; i++)
            {
                string curaValue = curaToDefaultMapping[i].CuraValue;
                if (curaValue != null && curaValue != "")
                {
                    settings.Append(string.Format("-s {0}=\"{1}\" ", curaToDefaultMapping[i].CuraKey, curaValue));
                }
            }

            return settings.ToString();
        }

        public class NotPassedItem : MapItem
        {
            public override string CuraValue
            {
                get
                {
                    return null;
                }
            }

            public NotPassedItem(string cura, string slicer)
                : base(cura, slicer)
            {
            }
        }

        public class FanTranslator : MapItem
        {
            public override string CuraValue
            {
                get
                {
                    int numLayersFanIsDisabledOn = int.Parse(base.CuraValue);
                    int layerToEnableFanOn = numLayersFanIsDisabledOn + 1;
                    return layerToEnableFanOn.ToString();
                }
            }

            public FanTranslator(string cura, string slicer)
                : base(cura, slicer)
            {
            }
        }

        public class SupportMatterial : MapItem
        {
            public override string CuraValue
            {
                get
                {
                    string supportMaterial = ActiveSliceSettings.Instance.GetActiveValue("support_material");
                    if (supportMaterial == "0")
                    {
                        return "-1";
                    }

                    return (90 - double.Parse(ActiveSliceSettings.Instance.GetActiveValue("support_material_threshold"))).ToString();
                }
            }

            public SupportMatterial(string cura, string slicer)
                : base(cura, slicer)
            {
            }
        }

        public class InfillTranslator : MapItem
        {
            public override string CuraValue
            {
                get
                {
                    double infillRatio0To1 = Double.Parse(base.CuraValue);
                    // 400 = solid (extruder width)
                    double nozzle_diameter = double.Parse(ActiveSliceSettings.Instance.GetActiveValue("nozzle_diameter"));
                    double linespacing = 1000;
                    if (infillRatio0To1 > .01)
                    {
                        linespacing = nozzle_diameter / infillRatio0To1;
                    }

                    return ((int)(linespacing * 1000)).ToString();
                }
            }

            public InfillTranslator(string cura, string slicer)
                : base(cura, slicer)
            {
            }
        }

        public class PrintCenterX : MapItem
        {
            public override string CuraValue
            {
                get
                {
                    Vector2 PrinteCenter = ActiveSliceSettings.Instance.PrintCenter;
                    return (PrinteCenter.x * 1000).ToString();
                }
            }

            public PrintCenterX(string cura, string slicer)
                : base(cura, slicer)
            {
            }
        }

        public class PrintCenterY : MapItem
        {
            public override string CuraValue
            {
                get
                {
                    Vector2 PrinteCenter = ActiveSliceSettings.Instance.PrintCenter;
                    return (PrinteCenter.y * 1000).ToString();
                }
            }

            public PrintCenterY(string cura, string slicer)
                : base(cura, slicer)
            {
            }
        }

        public class ConvertCRs : MapItem
        {
            public override string CuraValue
            {
                get
                {
                    string actualCRs = base.CuraValue.Replace("\\n", "\n");
                    return actualCRs;
                }
            }

            public ConvertCRs(string cura, string slicer)
                : base(cura, slicer)
            {
            }
        }

        public class InjectGCodeCommands : ConvertCRs
        {
            public InjectGCodeCommands(string cura, string slicer)
                : base(cura, slicer)
            {
            }

            protected void AddDefaultIfNotPresent(List<string> linesAdded, string commandToAdd, string[] linesToCheckIfAlreadyPresent, string comment)
            {
                string command = commandToAdd.Split(' ')[0].Trim();
                bool foundCommand = false;
                foreach (string line in linesToCheckIfAlreadyPresent)
                {
                    if (line.StartsWith(command))
                    {
                        foundCommand = true;
                        break;
                    }
                }

                if (!foundCommand)
                {
                    linesAdded.Add(string.Format("{0} ; {1}", commandToAdd, comment));
                }
            }
        }

        public class MapStartGCode : InjectGCodeCommands
        {
            public override string CuraValue
            {
                get
                {
                    StringBuilder curaStartGCode = new StringBuilder();
                    foreach (string line in PreStartGCode())
                    {
                        curaStartGCode.Append(line + "\n");
                    }

                    curaStartGCode.Append(base.CuraValue);

                    bool first = true;
                    foreach (string line in PostStartGCode())
                    {
                        if (!first)
                        {
                            curaStartGCode.Append("\n");
                        }
                        curaStartGCode.Append(line);
                        first = false;
                    }

                    return curaStartGCode.ToString();
                }
            }

            public MapStartGCode(string cura, string slicer)
                : base(cura, slicer)
            {
            }

            public List<string> PreStartGCode()
            {
                string startGCode = ActiveSliceSettings.Instance.GetActiveValue("start_gcode");
                string[] preStartGCodeLines = startGCode.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);

                List<string> preStartGCode = new List<string>();
                preStartGCode.Add("; automatic settings before start_gcode");
                AddDefaultIfNotPresent(preStartGCode, "G21", preStartGCodeLines, "set units to millimeters");
                AddDefaultIfNotPresent(preStartGCode, "M107", preStartGCodeLines, "fan off");
                double bed_temperature = double.Parse(ActiveSliceSettings.Instance.GetActiveValue("bed_temperature"));
                if (bed_temperature > 0)
                {
                    string setBedTempString = string.Format("M190 S{0}", bed_temperature);
                    AddDefaultIfNotPresent(preStartGCode, setBedTempString, preStartGCodeLines, "wait for bed temperature to be reached");
                }
                string setTempString = string.Format("M104 S{0}", ActiveSliceSettings.Instance.GetActiveValue("temperature"));
                AddDefaultIfNotPresent(preStartGCode, setTempString, preStartGCodeLines, "set temperature");
                preStartGCode.Add("; settings from start_gcode");

                return preStartGCode;
            }

            public List<string> PostStartGCode()
            {
                string startGCode = ActiveSliceSettings.Instance.GetActiveValue("start_gcode");
                string[] postStartGCodeLines = startGCode.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);

                List<string> postStartGCode = new List<string>();
                postStartGCode.Add("; automatic settings after start_gcode");
                string setTempString = string.Format("M109 S{0}", ActiveSliceSettings.Instance.GetActiveValue("temperature"));
                AddDefaultIfNotPresent(postStartGCode, setTempString, postStartGCodeLines, "wait for temperature");
                AddDefaultIfNotPresent(postStartGCode, "G90", postStartGCodeLines, "use absolute coordinates");
                postStartGCode.Add(string.Format("{0} ; {1}", "G92 E0", "reset the expected extruder position"));
                AddDefaultIfNotPresent(postStartGCode, "M82", postStartGCodeLines, "use absolute distance for extrusion");

                return postStartGCode;
            }
        }

        public class MapEndGCode : InjectGCodeCommands
        {
            public override string CuraValue
            {
                get
                {
                    StringBuilder curaEndGCode = new StringBuilder();

                    curaEndGCode.Append(base.CuraValue);

                    curaEndGCode.Append("\n; filament used = filament_used_replace_mm (filament_used_replace_cm3)");

                    return curaEndGCode.ToString();
                }
            }

            public MapEndGCode(string cura, string slicer)
                : base(cura, slicer)
            {
            }
        }

        public class SkirtLengthMaping : MapItem
        {
            public SkirtLengthMaping(string curaKey, string defaultKey)
                : base(curaKey, defaultKey)
            {
            }

            public override string CuraValue
            {
                get
                {
                    double lengthToExtrudeMm = double.Parse(base.CuraValue);
                    // we need to convert mm of filament to mm of extrusion path
                    double amountOfFilamentCubicMms = ActiveSliceSettings.Instance.FillamentDiameter * MathHelper.Tau * lengthToExtrudeMm;
                    double extrusionSquareSize = ActiveSliceSettings.Instance.FirstLayerHeight * ActiveSliceSettings.Instance.NozzleDiameter;
                    double lineLength = amountOfFilamentCubicMms / extrusionSquareSize;

                    return (lineLength * 1000).ToString();
                }
            }
        }

        public class ScaledSingleNumber : MapItem
        {
            internal double scale;
            public override string CuraValue
            {
                get
                {
                    if (scale != 1)
                    {
                        return (double.Parse(base.CuraValue) * scale).ToString();
                    }
                    return base.CuraValue;
                }
            }

            internal ScaledSingleNumber(string cura, string slicer, double scale = 1)
                : base(cura, slicer)
            {
                this.scale = scale;
            }
        }

        public class AsPercentOfReferenceOrDirect : ScaledSingleNumber
        {
            internal string slicerReference;
            public override string CuraValue
            {
                get
                {
                    if (SlicerValue.Contains("%"))
                    {
                        string withoutPercent = SlicerValue.Replace("%", "");
                        double ratio = double.Parse(withoutPercent) / 100.0;
                        string slicerReferenceString = ActiveSliceSettings.Instance.GetActiveValue(slicerReference);
                        double valueToModify = double.Parse(slicerReferenceString);
                        double finalValue = valueToModify * ratio * scale;
                        return finalValue.ToString();
                    }

                    return base.CuraValue;
                }
            }

            internal AsPercentOfReferenceOrDirect(string cura, string slicer, string slicerReference, double scale = 1)
                : base(cura, slicer, scale)
            {
                this.slicerReference = slicerReference;
            }
        }
    }
}
