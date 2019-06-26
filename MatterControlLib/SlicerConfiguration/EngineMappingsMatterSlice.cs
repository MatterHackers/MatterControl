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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MatterHackers.Agg;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class EngineMappingsMatterSlice
	{
		private readonly HashSet<string> matterSliceSettingNames;

		public Dictionary<string, ExportField> Exports { get; }

		// Singleton use only - prevent external construction
		public EngineMappingsMatterSlice(PrinterConfig printer2)
		{
			Exports = new Dictionary<string, ExportField>()
			{
				[SettingsKey.bottom_solid_layers] = new ExportField("numberOfBottomLayers"),
				[SettingsKey.perimeters] = new ExportField("numberOfPerimeters"),
				[SettingsKey.raft_extra_distance_around_part] = new ExportField("raftExtraDistanceAroundPart"),
				[SettingsKey.support_material_interface_layers] = new ExportField("supportInterfaceLayers"),
				[SettingsKey.top_solid_layers] = new ExportField("numberOfTopLayers"),
				[SettingsKey.external_perimeter_extrusion_width] = new ExportField("outsidePerimeterExtrusionWidth"),
				[SettingsKey.external_perimeter_speed] = new ExportField("outsidePerimeterSpeed"),
				[SettingsKey.first_layer_speed] = new ExportField("firstLayerSpeed"),
				[SettingsKey.number_of_first_layers] = new ExportField("numberOfFirstLayers"),
				[SettingsKey.raft_print_speed] = new ExportField("raftPrintSpeed"),
				[SettingsKey.top_solid_infill_speed] = new ExportField("topInfillSpeed"),
				[SettingsKey.first_layer_extrusion_width] = new ExportField("firstLayerExtrusionWidth"),
				[SettingsKey.first_layer_height] = new ExportField("firstLayerThickness"),
				[SettingsKey.end_gcode] = new ExportField("endCode"),
				[SettingsKey.retract_before_travel] = new ExportField("minimumTravelToCauseRetraction"),
				[SettingsKey.retract_length] = new ExportField("retractionOnTravel"),
				[SettingsKey.retract_lift] = new ExportField("retractionZHop"),
				[SettingsKey.retract_restart_extra] = new ExportField("unretractExtraExtrusion"),
				[SettingsKey.retract_restart_extra_time_to_apply] = new ExportField("retractRestartExtraTimeToApply"),
				[SettingsKey.retract_speed] = new ExportField("retractionSpeed"),
				[SettingsKey.bridge_speed] = new ExportField("bridgeSpeed"),
				[SettingsKey.air_gap_speed] = new ExportField("airGapSpeed"),
				[SettingsKey.bottom_infill_speed] = new ExportField("bottomInfillSpeed"),
				[SettingsKey.bridge_over_infill] = new ExportField("bridgeOverInfill"),
				[SettingsKey.extrusion_multiplier] = new ExportField("extrusionMultiplier"),
				[SettingsKey.fill_angle] = new ExportField("infillStartingAngle"),
				[SettingsKey.infill_overlap_perimeter] = new ExportField("infillExtendIntoPerimeter"),
				[SettingsKey.infill_speed] = new ExportField("infillSpeed"),
				[SettingsKey.infill_type] = new ExportField("infillType"),
				[SettingsKey.min_extrusion_before_retract] = new ExportField("minimumExtrusionBeforeRetraction"),
				[SettingsKey.min_print_speed] = new ExportField("minimumPrintingSpeed"),
				[SettingsKey.perimeter_speed] = new ExportField("insidePerimetersSpeed"),
				[SettingsKey.raft_air_gap] = new ExportField("raftAirGap"),
				[SettingsKey.max_acceleration] = new ExportField("maxAcceleration"),
				[SettingsKey.max_velocity] = new ExportField("maxVelocity"),
				[SettingsKey.jerk_velocity] = new ExportField("jerkVelocity"),
				[SettingsKey.print_time_estimate_multiplier] = new ExportField(
					"printTimeEstimateMultiplier",
					(value, settings) =>
					{
						if (double.TryParse(value, out double infillRatio))
						{
							return $"{infillRatio * .01}";
						}

						return "0";
					}),
				// fan settings
				[SettingsKey.min_fan_speed] = new ExportField("fanSpeedMinPercent"),
				[SettingsKey.coast_at_end_distance] = new ExportField("coastAtEndDistance"),
				[SettingsKey.min_fan_speed_layer_time] = new ExportField("minFanSpeedLayerTime"),
				[SettingsKey.max_fan_speed] = new ExportField("fanSpeedMaxPercent"),
				[SettingsKey.max_fan_speed_layer_time] = new ExportField("maxFanSpeedLayerTime"),
				[SettingsKey.bridge_fan_speed] = new ExportField("bridgeFanSpeedPercent"),
				[SettingsKey.disable_fan_first_layers] = new ExportField("firstLayerToAllowFan"),
				// end fan
				[SettingsKey.retract_length_tool_change] = new ExportField("retractionOnExtruderSwitch"),
				[SettingsKey.retract_restart_extra_toolchange] = new ExportField("unretractExtraOnExtruderSwitch"),
				[SettingsKey.reset_long_extrusion] = new ExportField("resetLongExtrusion"),
				[SettingsKey.slowdown_below_layer_time] = new ExportField("minimumLayerTimeSeconds"),
				[SettingsKey.support_air_gap] = new ExportField("supportAirGap"),
				[SettingsKey.support_material_infill_angle] = new ExportField("supportInfillStartingAngle"),
				[SettingsKey.support_material_spacing] = new ExportField("supportLineSpacing"),
				[SettingsKey.support_material_speed] = new ExportField("supportMaterialSpeed"),
				[SettingsKey.interface_layer_speed] = new ExportField("interfaceLayerSpeed"),
				[SettingsKey.support_material_xy_distance] = new ExportField("supportXYDistanceFromObject"),
				[SettingsKey.support_type] = new ExportField("supportType"),
				[SettingsKey.travel_speed] = new ExportField("travelSpeed"),
				[SettingsKey.wipe_shield_distance] = new ExportField("wipeShieldDistanceFromObject"),
				[SettingsKey.wipe_tower_size] = new ExportField("wipeTowerSize"),
				[SettingsKey.filament_diameter] = new ExportField("filamentDiameter"),
				[SettingsKey.layer_height] = new ExportField("layerThickness"),
				[SettingsKey.nozzle_diameter] = new ExportField("extrusionWidth"),
				[SettingsKey.extruder_count] = new ExportField("extruderCount"),
				[SettingsKey.avoid_crossing_perimeters] = new ExportField("avoidCrossingPerimeters"),
				[SettingsKey.create_raft] = new ExportField("enableRaft"),
				[SettingsKey.external_perimeters_first] = new ExportField("outsidePerimetersFirst"),
				[SettingsKey.output_only_first_layer] = new ExportField("outputOnlyFirstLayer"),
				[SettingsKey.retract_when_changing_islands] = new ExportField("retractWhenChangingIslands"),
				[SettingsKey.support_material_create_perimeter] = new ExportField("generateSupportPerimeter"),
				[SettingsKey.expand_thin_walls] = new ExportField("expandThinWalls"),
				[SettingsKey.merge_overlapping_lines] = new ExportField("MergeOverlappingLines"),
				[SettingsKey.fill_thin_gaps] = new ExportField("fillThinGaps"),
				[SettingsKey.spiral_vase] = new ExportField("continuousSpiralOuterPerimeter"),
				[SettingsKey.start_gcode] = new ExportField(
					"startCode",
					(value, settings) =>
					{
						return StartGCodeGenerator.BuildStartGCode(settings, value);
					}),
				[SettingsKey.layer_gcode] = new ExportField("layerChangeCode"),
				[SettingsKey.fill_density] = new ExportField(
					"infillPercent",
					(value, settings) =>
					{
						if (double.TryParse(value, out double infillRatio))
						{
							return $"{infillRatio * 100}";
						}

						return "0";
					}),
				[SettingsKey.perimeter_start_end_overlap] = new ExportField(
					"perimeterStartEndOverlapRatio",
					(value, settings) =>
					{
						if (double.TryParse(value, out double infillRatio))
						{
							return $"{infillRatio * .01}";
						}

						return "0";
					}),
				[SettingsKey.raft_extruder] = new ExportField("raftExtruder"),
				[SettingsKey.support_material_extruder] = new ExportField("supportExtruder"),
				[SettingsKey.support_material_interface_extruder] = new ExportField("supportInterfaceExtruder"),
				// Skirt settings
				[SettingsKey.skirts] = new ExportField("numberOfSkirtLoops"),
				[SettingsKey.skirt_distance] = new ExportField("skirtDistanceFromObject"),
				[SettingsKey.min_skirt_length] = new ExportField("skirtMinLength"),
				// Brim settings
				[SettingsKey.brims] = new ExportField("numberOfBrimLoops")
			};

			matterSliceSettingNames = new HashSet<string>(this.Exports.Select(m => m.Key));
		}

		public string Name => "MatterSlice";

		public void WriteSliceSettingsFile(string outputFilename, IEnumerable<string> rawLines, PrinterSettings settings)
		{
			using (var sliceSettingsFile = new StreamWriter(outputFilename))
			{
				foreach (var (key, exportField) in this.Exports.Select(kvp => (kvp.Key, kvp.Value)))
				{
					string result = settings.ResolveValue(key);

					// Run custom converter if defined on the field
					if (exportField.Converter != null)
					{
						result = exportField.Converter(result, settings);
					}

					if (result != null)
					{
						sliceSettingsFile.WriteLine("{0} = {1}", exportField.OuputName, result);
					}
				}

				foreach (var line in rawLines)
				{
					sliceSettingsFile.WriteLine(line);
				}
			}
		}

		public bool MapContains(string canonicalSettingsName)
		{
			return matterSliceSettingNames.Contains(canonicalSettingsName)
				|| PrinterSettings.ApplicationLevelSettings.Contains(canonicalSettingsName);
		}

		public static class StartGCodeGenerator
		{
			public static string BuildStartGCode(PrinterSettings settings, string userGCode)
			{
				var newStartGCode = new StringBuilder();

				foreach (string line in PreStartGCode(settings, Slicer.ExtrudersUsed))
				{
					newStartGCode.Append(line + "\n");
				}

				newStartGCode.Append(userGCode);

				foreach (string line in PostStartGCode(settings, Slicer.ExtrudersUsed))
				{
					newStartGCode.Append("\n");
					newStartGCode.Append(line);
				}

				var result = newStartGCode.ToString();
				return result.Replace("\n", "\\n");
			}

			private static List<string> PreStartGCode(PrinterSettings settings, List<bool> extrudersUsed)
			{
				string startGCode = settings.GetValue(SettingsKey.start_gcode);
				string[] startGCodeLines = startGCode.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);

				var preStartGCode = new List<string>
			{
				"; automatic settings before start_gcode"
			};
				AddDefaultIfNotPresent(preStartGCode, "G21", startGCodeLines, "set units to millimeters");
				AddDefaultIfNotPresent(preStartGCode, "M107", startGCodeLines, "fan off");
				double bed_temperature = settings.GetValue<double>(SettingsKey.bed_temperature);
				if (bed_temperature > 0)
				{
					string setBedTempString = string.Format("M140 S{0}", bed_temperature);
					AddDefaultIfNotPresent(preStartGCode, setBedTempString, startGCodeLines, "start heating the bed");
				}

				int numberOfHeatedExtruders = settings.Helpers.HotendCount();

				// Start heating all the extruder that we are going to use.
				for (int hotendIndex = 0; hotendIndex < numberOfHeatedExtruders; hotendIndex++)
				{
					if (extrudersUsed.Count > hotendIndex
						&& extrudersUsed[hotendIndex])
					{
						double materialTemperature = settings.Helpers.ExtruderTargetTemperature(hotendIndex);
						if (materialTemperature != 0)
						{
							string setTempString = "M104 T{0} S{1}".FormatWith(hotendIndex, materialTemperature);
							AddDefaultIfNotPresent(preStartGCode, setTempString, startGCodeLines, $"start heating T{hotendIndex}");
						}
					}
				}

				// If we need to wait for the heaters to heat up before homing then set them to M109 (heat and wait).
				if (settings.GetValue<bool>(SettingsKey.heat_extruder_before_homing))
				{
					for (int hotendIndex = 0; hotendIndex < numberOfHeatedExtruders; hotendIndex++)
					{
						if (extrudersUsed.Count > hotendIndex
							&& extrudersUsed[hotendIndex])
						{
							double materialTemperature = settings.Helpers.ExtruderTargetTemperature(hotendIndex);
							if (materialTemperature != 0)
							{
								string setTempString = "M109 T{0} S{1}".FormatWith(hotendIndex, materialTemperature);
								AddDefaultIfNotPresent(preStartGCode, setTempString, startGCodeLines, $"wait for T{hotendIndex }");
							}
						}
					}
				}

				// If we have bed temp and the start gcode specifies to finish heating the extruders,
				// make sure we also finish heating the bed. This preserves legacy expectation.
				if (bed_temperature > 0
					&& startGCode.Contains("M109"))
				{
					string setBedTempString = string.Format("M190 S{0}", bed_temperature);
					AddDefaultIfNotPresent(preStartGCode, setBedTempString, startGCodeLines, "wait for bed temperature to be reached");
				}

				SwitchToFirstActiveExtruder(extrudersUsed, preStartGCode);
				preStartGCode.Add("; settings from start_gcode");

				return preStartGCode;
			}

			private static List<string> PostStartGCode(PrinterSettings settings, List<bool> extrudersUsed)
			{
				string startGCode = settings.GetValue(SettingsKey.start_gcode);
				string[] startGCodeLines = startGCode.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);

				var postStartGCode = new List<string>
			{
				"; automatic settings after start_gcode"
			};

				double bed_temperature = settings.GetValue<double>(SettingsKey.bed_temperature);
				if (bed_temperature > 0
					&& !startGCode.Contains("M109"))
				{
					string setBedTempString = string.Format("M190 S{0}", bed_temperature);
					AddDefaultIfNotPresent(postStartGCode, setBedTempString, startGCodeLines, "wait for bed temperature to be reached");
				}

				int numberOfHeatedExtruders = settings.GetValue<int>(SettingsKey.extruder_count);
				// wait for them to finish
				for (int hotendIndex = 0; hotendIndex < numberOfHeatedExtruders; hotendIndex++)
				{
					if (hotendIndex < extrudersUsed.Count
						&& extrudersUsed[hotendIndex])
					{
						double materialTemperature = settings.Helpers.ExtruderTargetTemperature(hotendIndex);
						if (materialTemperature != 0)
						{
							if (!(hotendIndex == 0 && LineStartsWith(startGCodeLines, "M109 S"))
								&& !LineStartsWith(startGCodeLines, $"M109 T{hotendIndex} S"))
							{
								// always heat the extruders that are used beyond extruder 0
								postStartGCode.Add($"M109 T{hotendIndex} S{materialTemperature} ; Finish heating T{hotendIndex}");
							}
						}
					}
				}

				SwitchToFirstActiveExtruder(extrudersUsed, postStartGCode);
				AddDefaultIfNotPresent(postStartGCode, "G90", startGCodeLines, "use absolute coordinates");
				postStartGCode.Add(string.Format("{0} ; {1}", "G92 E0", "reset the expected extruder position"));
				AddDefaultIfNotPresent(postStartGCode, "M82", startGCodeLines, "use absolute distance for extrusion");

				return postStartGCode;
			}

			private static void AddDefaultIfNotPresent(List<string> linesAdded, string commandToAdd, string[] lines, string comment)
			{
				string command = commandToAdd.Split(' ')[0].Trim();

				if (!LineStartsWith(lines, command))
				{
					linesAdded.Add(string.Format("{0} ; {1}", commandToAdd, comment));
				}
			}

			private static void SwitchToFirstActiveExtruder(List<bool> extrudersUsed, List<string> preStartGCode)
			{
				// make sure we are on the first active extruder
				for (int extruderIndex = 0; extruderIndex < extrudersUsed.Count; extruderIndex++)
				{
					if (extrudersUsed[extruderIndex])
					{
						// set the active extruder to the first one that will be printing
						preStartGCode.Add("T{0} ; {1}".FormatWith(extruderIndex, "set the active extruder to {0}".FormatWith(extruderIndex)));
						// we have set the active extruder so don't set it to any other extruder
						break;
					}
				}
			}

			private static bool LineStartsWith(string[] lines, string command)
			{
				foreach (string line in lines)
				{
					if (line.StartsWith(command))
					{
						return true;
					}
				}

				return false;
			}
		}



	}
}