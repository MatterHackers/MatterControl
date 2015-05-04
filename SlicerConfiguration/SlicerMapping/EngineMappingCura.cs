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

using MatterHackers.VectorMath;
using System.Text;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class EngineMappingCura : SliceEngineMaping
	{
		// private so that this class is a sigleton
		private EngineMappingCura()
		{
		}

		private static EngineMappingCura instance = null;

		public static EngineMappingCura Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new EngineMappingCura();
				}
				return instance;
			}
		}

		public override bool MapContains(string defaultKey)
		{
			foreach (MapItem mapItem in curaToDefaultMapping)
			{
				if (mapItem.OriginalKey == defaultKey)
				{
					return true;
				}
			}

			return false;
		}

		private static MapItem[] curaToDefaultMapping =
        {
            new ScaledSingleNumber("layerThickness", "layer_height", 1000),
            new AsPercentOfReferenceOrDirect("initialLayerThickness", "first_layer_height", "layer_height", 1000),
            new ScaledSingleNumber("filamentDiameter", "filament_diameter", 1000),
            //filamentFlow
            //layer0extrusionWidth
            new ScaledSingleNumber("extrusionWidth", "nozzle_diameter", 1000),
            new AsCountOrDistance("insetCount", "perimeters", "nozzle_diameter"),
            new AsCountOrDistance("downSkinCount", "bottom_solid_layers", "layer_height"),
            new AsCountOrDistance("upSkinCount", "top_solid_layers", "layer_height"),
            new ScaledSingleNumber("skirtDistance", "skirt_distance", 1000),
            new MapItem("skirtLineCount", "skirts"),
            new SkirtLengthMaping("skirtMinLength", "min_skirt_length"),

            new MapItem("printSpeed", "infill_speed"),
            new MapItem("infillSpeed", "infill_speed"),
            new MapItem("moveSpeed", "travel_speed"),
            new AsPercentOfReferenceOrDirect("initialLayerSpeed", "first_layer_speed", "infill_speed"),

            new MapItem("insetXSpeed", "perimeter_speed"),
            new AsPercentOfReferenceOrDirect("inset0Speed", "external_perimeter_speed", "perimeter_speed"),

            new VisibleButNotMappedToEngine("temperature"),
            new VisibleButNotMappedToEngine("bed_temperature"),
            new VisibleButNotMappedToEngine("bed_shape"),

            new VisibleButNotMappedToEngine("has_fan"),
            new VisibleButNotMappedToEngine("has_power_control"),
            new VisibleButNotMappedToEngine("has_heated_bed"),
            new VisibleButNotMappedToEngine("has_hardware_leveling"),
            new VisibleButNotMappedToEngine("has_sd_card_reader"),
            new VisibleButNotMappedToEngine("z_can_be_negative"),

            new ScaledSingleNumber("objectSink", "bottom_clip_amount", 1000),

            new MapItem("fanSpeedMin", "max_fan_speed"),
            new MapItem("fanSpeedMax", "min_fan_speed"),

            new FanTranslator("fanFullOnLayerNr", "disable_fan_first_layers"),
            new MapItem("coolHeadLift", "cool_extruder_lift"),

            new ScaledSingleNumber("retractionAmount", "retract_length", 1000),
            new MapItem("retractionSpeed", "retract_speed"),
            new ScaledSingleNumber("retractionMinimalDistance", "retract_before_travel", 1000),
            new ScaledSingleNumber("minimalExtrusionBeforeRetraction", "min_extrusion_before_retract", 1000),

            new ScaledSingleNumber("retractionZHop", "retract_lift", 1000),

            new MapItem("spiralizeMode", "spiral_vase"),

            new VisibleButNotMappedToEngine("bed_size"),

            new PrintCenterX("posx", "print_center"),
            new PrintCenterY("posy", "print_center"),

            new VisibleButNotMappedToEngine("build_height"),

            // needs testing, not working
            new ScaledSingleNumber("supportLineDistance", "support_material_spacing", 1000),
            new SupportMatterial("supportAngle", "support_material"),
            new VisibleButNotMappedToEngine("support_material_threshold"),
            new MapItem("supportEverywhere", "support_material_create_internal_support"),
            new ScaledSingleNumber("supportXYDistance", "support_material_xy_distance", 1000),
            new ScaledSingleNumber("supportZDistance", "support_material_z_distance", 1000),

            new SupportTypeMapping("supportType", "support_type"),

            new MapItem("minimalLayerTime", "slowdown_below_layer_time"),

            new InfillTranslator("sparseInfillLineDistance", "fill_density"),

            new MapStartGCode("startCode", "start_gcode", false),
            new MapEndGCode("endCode", "end_gcode"),

            new VisibleButNotMappedToEngine("pause_gcode"),
            new VisibleButNotMappedToEngine("resume_gcode"),
            new VisibleButNotMappedToEngine("cancel_gcode"),

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
				string curaValue = curaToDefaultMapping[i].MappedValue;
				if (curaValue != null && curaValue != "")
				{
					settings.Append(string.Format("-s {0}=\"{1}\" ", curaToDefaultMapping[i].MappedKey, curaValue));
				}
			}

			return settings.ToString();
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

			public FanTranslator(string cura, string originalKey)
				: base(cura, originalKey)
			{
			}
		}

		public class SupportTypeMapping : MapItem
		{
			public override string MappedValue
			{
				get
				{
					switch (base.MappedValue)
					{
						case "LINES":
							return "1"; // the lines setting from curaengine

						default:
							return "0"; // the grid setting from curaengine
					}
				}
			}

			public SupportTypeMapping(string cura, string originalKey)
				: base(cura, originalKey)
			{
			}
		}

		public class SupportMatterial : MapItem
		{
			public override string MappedValue
			{
				get
				{
					string supportMaterial = ActiveSliceSettings.Instance.GetActiveValue("support_material");
					if (supportMaterial == "0")
					{
						return "-1";
					}

					return (90 - MapItem.GetValueForKey("support_material_threshold")).ToString();
				}
			}

			public SupportMatterial(string cura, string originalKey)
				: base(cura, originalKey)
			{
			}
		}

		public class InfillTranslator : ScaledSingleNumber
		{
			public override string MappedValue
			{
				get
				{
					double infillRatio0To1 = ScaledSingleNumber.ParseValueString(base.MappedValue);

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

			public InfillTranslator(string cura, string originalKey)
				: base(cura, originalKey)
			{
			}
		}

		public class PrintCenterX : MapItem
		{
			public override string MappedValue
			{
				get
				{
					Vector2 PrinteCenter = ActiveSliceSettings.Instance.PrintCenter;
					return (PrinteCenter.x * 1000).ToString();
				}
			}

			public PrintCenterX(string cura, string originalKey)
				: base(cura, originalKey)
			{
			}
		}

		public class PrintCenterY : MapItem
		{
			public override string MappedValue
			{
				get
				{
					Vector2 PrinteCenter = ActiveSliceSettings.Instance.PrintCenter;
					return (PrinteCenter.y * 1000).ToString();
				}
			}

			public PrintCenterY(string cura, string originalKey)
				: base(cura, originalKey)
			{
			}
		}

		public class MapEndGCode : InjectGCodeCommands
		{
			public override string MappedValue
			{
				get
				{
					StringBuilder curaEndGCode = new StringBuilder();

					curaEndGCode.Append(base.MappedValue);

					curaEndGCode.Append("\n; filament used = filament_used_replace_mm (filament_used_replace_cm3)");

					return curaEndGCode.ToString();
				}
			}

			public MapEndGCode(string cura, string originalKey)
				: base(cura, originalKey)
			{
			}
		}

		public class SkirtLengthMaping : MapItem
		{
			public SkirtLengthMaping(string curaKey, string defaultKey)
				: base(curaKey, defaultKey)
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

					return (lineLength * 1000).ToString();
				}
			}
		}
	}
}