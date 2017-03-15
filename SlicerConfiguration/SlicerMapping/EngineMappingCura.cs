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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class EngineMappingCura : SliceEngineMapping
	{
		public static readonly EngineMappingCura Instance = new EngineMappingCura();

		private HashSet<string> curaSettingNames;

		private MappedSetting[] curaSettings;

		// Singleton use only - prevent external construction
		private EngineMappingCura() : base("Cura")
		{
			curaSettings = new MappedSetting[]
			{
				new ScaledSingleNumber(SettingsKey.layer_height, "layerThickness", 1000),
				new AsPercentOfReferenceOrDirect(SettingsKey.first_layer_height, "initialLayerThickness", SettingsKey.layer_height, 1000),
				new ScaledSingleNumber(SettingsKey.filament_diameter, "filamentDiameter", 1000),
				//filamentFlow
				//layer0extrusionWidth
				new ScaledSingleNumber(SettingsKey.nozzle_diameter, "extrusionWidth", 1000),
				new AsCountOrDistance("perimeters", "insetCount", SettingsKey.nozzle_diameter),
				new AsCountOrDistance("bottom_solid_layers", "downSkinCount", SettingsKey.layer_height),
				new AsCountOrDistance("top_solid_layers", "upSkinCount", SettingsKey.layer_height),
				new ScaledSingleNumber("skirt_distance", "skirtDistance", 1000),
				new AsCountOrDistance("skirts", "skirtLineCount", SettingsKey.nozzle_diameter),
				new SkirtLengthMapping("min_skirt_length", "skirtMinLength"),

				new MappedSetting("infill_speed", "printSpeed"),
				new MappedSetting("infill_speed", "infillSpeed"),
				new MappedSetting("travel_speed", "moveSpeed"),
				new AsPercentOfReferenceOrDirect(SettingsKey.first_layer_speed, "initialLayerSpeed", "infill_speed"),

				new MappedSetting("perimeter_speed", "insetXSpeed"),
				new AsPercentOfReferenceOrDirect("external_perimeter_speed", "inset0Speed", "perimeter_speed"),

				new ScaledSingleNumber(SettingsKey.bottom_clip_amount, "objectSink", 1000),

				new MappedSetting("max_fan_speed", "fanSpeedMin"),
				new MappedSetting("min_fan_speed", "fanSpeedMax"),

				new FanTranslator("disable_fan_first_layers", "fanFullOnLayerNr"),
				new MappedSetting("cool_extruder_lift", "coolHeadLift"),

				new ScaledSingleNumber("retract_length", "retractionAmount", 1000),
				new MapFirstValue("retract_speed", "retractionSpeed"),
				new ScaledSingleNumber("retract_before_travel", "retractionMinimalDistance", 1000),
				new ScaledSingleNumber("min_extrusion_before_retract", "minimalExtrusionBeforeRetraction", 1000),

				new ScaledSingleNumber("retract_lift", "retractionZHop", 1000),

				new MappedSetting(SettingsKey.spiral_vase, "spiralizeMode"),
				new PrintCenterX(SettingsKey.print_center, "posx"),
				new PrintCenterY(SettingsKey.print_center, "posy"),

				// needs testing, not working
				new ScaledSingleNumber("support_material_spacing", "supportLineDistance", 1000),
				new SupportMatterial("support_material", "supportAngle"),
				new VisibleButNotMappedToEngine("support_material_threshold"),
				new MappedSetting("support_material_create_internal_support", "supportEverywhere"),
				new ScaledSingleNumber("support_material_xy_distance", "supportXYDistance", 1000),
				new ScaledSingleNumber("support_material_z_distance", "supportZDistance", 1000),

				// This needs to be passed to cura (but not matter slice). The actual value is set in "support_material", "supportAngle" but is found in this value.
				new VisibleButNotMappedToEngine("support_material_threshold"),

				new SupportTypeMapping("support_type", "supportType"),

				new MappedSetting("slowdown_below_layer_time", "minimalLayerTime"),

				new InfillTranslator("fill_density", "sparseInfillLineDistance"),

				new MapStartGCode("start_gcode", "startCode", false),
				new MapEndGCode("end_gcode", "endCode"),
			};

			curaSettingNames = new HashSet<string>(curaSettings.Select(m => m.CanonicalSettingsName));
		}

		public override bool MapContains(string canonicalSettingsName)
		{
			return curaSettingNames.Contains(canonicalSettingsName) 
				|| base.applicationLevelSettings.Contains(canonicalSettingsName);
		}

		public static string GetCuraCommandLineSettings()
		{
			StringBuilder settings = new StringBuilder();
			foreach (MappedSetting mappedSetting in Instance.curaSettings)
			{
				if (!string.IsNullOrEmpty(mappedSetting.Value))
				{
					settings.AppendFormat("-s {0}=\"{1}\" ", mappedSetting.ExportedName, mappedSetting.Value);
				}
			}

			return settings.ToString();
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

		public class SupportTypeMapping : MappedSetting
		{
			public SupportTypeMapping(string canonicalSettingsName, string exportedName)
				: base(canonicalSettingsName, exportedName)
			{
			}

			public override string Value
			{
				get
				{
					switch (base.Value)
					{
						case "LINES":
							return "1"; // the lines setting from curaengine

						default:
							return "0"; // the grid setting from curaengine
					}
				}
			}
		}

		public class SupportMatterial : MappedSetting
		{
			public SupportMatterial(string canonicalSettingsName, string exportedName)
				: base(canonicalSettingsName, exportedName)
			{
			}

			public override string Value
			{
				get
				{
					string supportMaterial = base.Value;
					if (supportMaterial == "0")
					{
						return "-1";
					}

					return (90 - ParseDoubleFromRawValue("support_material_threshold")).ToString();
				}
			}
		}

		public class InfillTranslator : ScaledSingleNumber
		{
			public InfillTranslator(string canonicalSettingsName, string exportedName) : base(canonicalSettingsName, exportedName)
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

		public class PrintCenterX : MappedSetting
		{
			public PrintCenterX(string canonicalSettingsName, string exportedName)
				: base(canonicalSettingsName, exportedName)
			{
			}

			public override string Value
			{
				get
				{
					Vector2 PrinteCenter = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center);
					return (PrinteCenter.x * 1000).ToString();
				}
			}
		}

		public class PrintCenterY : MappedSetting
		{
			public PrintCenterY(string canonicalSettingsName, string exportedName)
				: base(canonicalSettingsName, exportedName)
			{
			}

			public override string Value
			{
				get
				{
					Vector2 PrinteCenter = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center);
					return (PrinteCenter.y * 1000).ToString();
				}
			}
		}

		public class MapEndGCode : InjectGCodeCommands
		{
			public MapEndGCode(string canonicalSettingsName, string exportedName)
				: base(canonicalSettingsName, exportedName)
			{
			}

			public override string Value
			{
				get
				{
					StringBuilder curaEndGCode = new StringBuilder();

					curaEndGCode.Append(GCodeProcessing.ReplaceMacroValues(base.Value));

					curaEndGCode.Append("\n; filament used = filament_used_replace_mm (filament_used_replace_cm3)");

					return curaEndGCode.ToString();
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

					return (lineLength * 1000).ToString();
				}
			}
		}
	}
}