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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class EngineMappingsMatterSlice : IObjectSlicer
	{
		private static Dictionary<Mesh, MeshPrintOutputSettings> meshPrintOutputSettings = new Dictionary<Mesh, MeshPrintOutputSettings>();
		private readonly HashSet<string> matterSliceSettingNames;

		public Dictionary<string, ExportField> Exports { get; }

		// Singleton use only - prevent external construction
		public EngineMappingsMatterSlice()
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

		public bool ValidateFile(string filePath)
		{
			// read the last few k of the file and see if it says "filament used". We use this marker to tell if the file finished writing
			int bufferSize = 32000;

			int padding = 100;

			using (Stream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				int i = 1;
				bool readToStart = false;
				do
				{
					var buffer = new byte[bufferSize + 100];

					// fileStream.Seek(Math.Max(0, fileStream.Length - bufferSize), SeekOrigin.Begin);
					fileStream.Position = Math.Max(0, fileStream.Length - (bufferSize * i++) - padding);
					readToStart = fileStream.Position == 0;

					int numBytesRead = fileStream.Read(buffer, 0, bufferSize + padding);

					string fileEnd = System.Text.Encoding.UTF8.GetString(buffer);
					if (fileEnd.Contains("filament used"))
					{
						return true;
					}
				} while (!readToStart);

				return false;
			}
		}

		public static List<(Matrix4X4 matrix, string fileName)> GetStlFileLocations(ref string mergeRules, IEnumerable<IObject3D> printableItems, PrinterSettings settings, IProgress<ProgressStatus> reporter, CancellationToken cancellationToken)
		{
			var progressStatus = new ProgressStatus();

			Slicer.GetExtrudersUsed(Slicer.ExtrudersUsed, printableItems, settings, true);

			// TODO: Once graph parsing is added to MatterSlice we can remove and avoid this flattening
			meshPrintOutputSettings.Clear();

			// Flatten the scene, filtering out items outside of the build volume
			var meshItemsOnBuildPlate = printableItems;

			if (meshItemsOnBuildPlate.Any())
			{
				int maxExtruderIndex = 0;

				var itemsByExtruder = new List<IEnumerable<IObject3D>>();
				int extruderCount = settings.GetValue<int>(SettingsKey.extruder_count);
				// Make sure we only consider 1 extruder if in spiral vase mode
				if (settings.GetValue<bool>(SettingsKey.spiral_vase))
				{
					extruderCount = 1;
				}

				for (int extruderIndexIn = 0; extruderIndexIn < extruderCount; extruderIndexIn++)
				{
					var extruderIndex = extruderIndexIn;
					IEnumerable<IObject3D> itemsThisExtruder = Slicer.GetItemsForExtruder(meshItemsOnBuildPlate, extruderCount, extruderIndex, true);

					itemsByExtruder.Add(itemsThisExtruder);
					if (Slicer.ExtrudersUsed[extruderIndex])
					{
						maxExtruderIndex = extruderIndex;
					}
				}

				var outputOptions = new List<(Matrix4X4 matrix, string fileName)>();

				int savedStlCount = 0;
				bool first = true;
				for (int extruderIndex = 0; extruderIndex < itemsByExtruder.Count; extruderIndex++)
				{
					if (!first)
					{
						mergeRules += ",";
						first = false;
					}

					mergeRules += AddObjectsForExtruder(itemsByExtruder[extruderIndex], outputOptions, ref savedStlCount);
				}

				var supportObjects = meshItemsOnBuildPlate.Where((item) => item.WorldOutputType() == PrintOutputTypes.Support);
				// if we added user generated support
				if (supportObjects.Any())
				{
					// add a flag to the merge rules to let us know there was support
					mergeRules += ",S" + AddObjectsForExtruder(supportObjects, outputOptions, ref savedStlCount);
				}

				var wipeTowerObjects = meshItemsOnBuildPlate.Where((item) => item.WorldOutputType() == PrintOutputTypes.WipeTower);
				// if we added user generated wipe tower
				if (wipeTowerObjects.Any())
				{
					// add a flag to the merge rules to let us know there was a wipe tower
					mergeRules += ",W" + AddObjectsForExtruder(wipeTowerObjects, outputOptions, ref savedStlCount);
				}

				mergeRules += " ";

				return outputOptions;
			}

			return new List<(Matrix4X4 matrix, string fileName)>();
		}

		public Task<bool> Slice(IEnumerable<IObject3D> printableItems, PrinterSettings settings, string gcodeFilePath, IProgress<ProgressStatus> reporter, CancellationToken cancellationToken)
		{
			string mergeRules = "";

			var stlFileLocations = GetStlFileLocations(ref mergeRules, printableItems, settings, reporter, cancellationToken);

			if (stlFileLocations.Count <= 0)
			{
				return Task.FromResult(false);
			}

			// Wrap the reporter with a specialized MatterSlice string parser for percent from string results
			var sliceProgressReporter = new SliceProgressReporter(reporter);

			bool slicingSucceeded = true;

			if (stlFileLocations.Count > 0)
			{
				var progressStatus = new ProgressStatus()
				{
					Status = "Generating Config"
				};
				sliceProgressReporter.Report(progressStatus);

				string configFilePath = Path.Combine(
					ApplicationDataStorage.Instance.GCodeOutputPath,
					string.Format("config_{0}.ini", settings.GetGCodeCacheKey().ToString()));

				progressStatus.Status = "Starting slicer";
				sliceProgressReporter.Report(progressStatus);

				if (!File.Exists(gcodeFilePath)
					|| !HasCompletedSuccessfully(gcodeFilePath))
				{
					string commandArgs;

					var matrixAndMeshArgs = new StringBuilder();
					foreach (var (matrix, fileName) in stlFileLocations)
					{
						var matrixString = "";
						bool first = true;
						for (int i = 0; i < 4; i++)
						{
							for (int j = 0; j < 4; j++)
							{
								if (!first)
								{
									matrixString += ",";
								}

								matrixString += matrix[i, j].ToString("0.######");
								first = false;
							}
						}

						matrixAndMeshArgs.Append($" -m \"{matrixString}\"");
						matrixAndMeshArgs.Append($" \"{fileName}\" ");
					}

					this.WriteSliceSettingsFile(
						configFilePath,
						new[]
						{
							$"booleanOperations = {mergeRules}",
							$"additionalArgsToProcess ={matrixAndMeshArgs}"
						},
						settings);

					commandArgs = $"-v -o \"{gcodeFilePath}\" -c \"{configFilePath}\"";

					bool forcedExit = false;

					if (AggContext.OperatingSystem == OSType.Android
						|| AggContext.OperatingSystem == OSType.Mac
						|| Slicer.RunInProcess)
					{
						void WriteOutput(object s, EventArgs e)
						{
							if (cancellationToken.IsCancellationRequested)
							{
								MatterHackers.MatterSlice.MatterSlice.Stop();
								forcedExit = true;
							}

							if (s is string stringValue)
							{
								sliceProgressReporter?.Report(new ProgressStatus()
								{
									Status = stringValue
								});
							}
						}

						MatterSlice.LogOutput.GetLogWrites += WriteOutput;

						MatterSlice.MatterSlice.ProcessArgs(commandArgs);

						MatterSlice.LogOutput.GetLogWrites -= WriteOutput;

						slicingSucceeded = !forcedExit;
					}
					else
					{
						var slicerProcess = new Process()
						{
							StartInfo = new ProcessStartInfo()
							{
								Arguments = commandArgs,
								CreateNoWindow = true,
								WindowStyle = ProcessWindowStyle.Hidden,
								RedirectStandardError = true,
								RedirectStandardOutput = true,
								FileName = MatterSliceInfo.GetEnginePath(),
								UseShellExecute = false
							}
						};

						slicerProcess.OutputDataReceived += (s, e) =>
						{
							if (e.Data is string stringValue)
							{
								if (cancellationToken.IsCancellationRequested)
								{
									slicerProcess?.Kill();
									slicerProcess?.Dispose();
									forcedExit = true;
								}

								string message = stringValue.Replace("=>", "").Trim();
								if (message.Contains(".gcode"))
								{
									message = "Saving intermediate file";
								}

								message += "...";

								sliceProgressReporter?.Report(new ProgressStatus()
								{
									Status = message
								});
							}
						};

						slicerProcess.Start();
						slicerProcess.BeginOutputReadLine();

						string stdError = slicerProcess.StandardError.ReadToEnd();

						if (!forcedExit)
						{
							slicerProcess.WaitForExit();
						}

						slicingSucceeded = !forcedExit;
					}
				}

				try
				{
					if (slicingSucceeded
						&& File.Exists(gcodeFilePath)
						&& File.Exists(configFilePath))
					{
						// make sure we have not already written the settings onto this file
						bool fileHasSettings = false;
						int bufferSize = 32000;
						using (Stream fileStream = File.OpenRead(gcodeFilePath))
						{
							// Read the tail of the file to determine if the given token exists
							byte[] buffer = new byte[bufferSize];
							fileStream.Seek(Math.Max(0, fileStream.Length - bufferSize), SeekOrigin.Begin);
							int numBytesRead = fileStream.Read(buffer, 0, bufferSize);
							string fileEnd = System.Text.Encoding.UTF8.GetString(buffer);
							if (fileEnd.Contains("GCode settings used"))
							{
								fileHasSettings = true;
							}
						}

						if (!fileHasSettings)
						{
							using (StreamWriter gcodeWriter = File.AppendText(gcodeFilePath))
							{
								string oemName = "MatterControl";
								if (OemSettings.Instance.WindowTitleExtra != null && OemSettings.Instance.WindowTitleExtra.Trim().Length > 0)
								{
									oemName += $" - {OemSettings.Instance.WindowTitleExtra}";
								}

								gcodeWriter.WriteLine("; {0} Version {1} Build {2} : GCode settings used", oemName, VersionInfo.Instance.ReleaseVersion, VersionInfo.Instance.BuildVersion);
								gcodeWriter.WriteLine("; Date {0} Time {1}:{2:00}", DateTime.Now.Date, DateTime.Now.Hour, DateTime.Now.Minute);

								var settingsToSkip = new string[] { "booleanOperations", "additionalArgsToProcess" };
								foreach (string line in File.ReadLines(configFilePath))
								{
									if (!settingsToSkip.Any(setting => line.StartsWith(setting)))
									{
										gcodeWriter.WriteLine("; {0}", line);
									}
								}
							}
						}
					}
				}
				catch (Exception)
				{
				}
			}

			return Task.FromResult(slicingSucceeded);
		}

		private static bool HasCompletedSuccessfully(string gcodeFilePath)
		{
			using (var reader = new StreamReader(gcodeFilePath))
			{
				int pageSize = 10000;
				var fileStream = reader.BaseStream;

				long position = reader.BaseStream.Length - pageSize;

				// Process through the stream until we find the slicing success token or we pass the start
				while (position > 0)
				{
					fileStream.Position = position;

					string tail = reader.ReadToEnd();

					// Read from current position to the end
					if (tail.Contains("; MatterSlice Completed Successfully"))
					{
						return true;
					}

					// Page further back in the stream and retry
					position -= pageSize;
				}

				return false;
			}
		}

		private static string AddObjectsForExtruder(IEnumerable<IObject3D> items, List<(Matrix4X4 matrix, string fileName)> outputItems, ref int savedStlCount)
		{
			string mergeString = "";
			if (items.Any())
			{
				bool first = true;
				foreach (var item in items)
				{
					if (!first)
					{
						mergeString += ",";
					}

					// TODO: Use existing AssetsPath property
					string assetsDirectory = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, "Assets");
					var itemWorldMatrix = item.WorldMatrix();
					if (item is GeneratedSupportObject3D generatedSupportObject3D
						&& item.Mesh != null)
					{
						// grow the support columns by the amount they are reduced by
						var aabbForCenter = item.Mesh.GetAxisAlignedBoundingBox();
						var aabbForSize = item.Mesh.GetAxisAlignedBoundingBox(item.Matrix);
						var xyScale = (aabbForSize.XSize + 2 * SupportGenerator.ColumnReduceAmount) / aabbForSize.XSize;
						itemWorldMatrix = itemWorldMatrix.ApplyAtPosition(aabbForCenter.Center.Transform(itemWorldMatrix), Matrix4X4.CreateScale(xyScale, xyScale, 1));
					}

					outputItems.Add((itemWorldMatrix, Path.Combine(assetsDirectory, item.MeshPath)));
					mergeString += $"({savedStlCount++}";
					first = false;
				}

				mergeString += new string(')', items.Count());
			}
			else
			{
				// TODO: consider dropping the custom path and using the AssetPath as above
				string folderToSaveStlsTo = Path.Combine(ApplicationDataStorage.Instance.ApplicationTempDataPath, "amf_to_stl");

				// Create directory if needed
				Directory.CreateDirectory(folderToSaveStlsTo);

				Mesh tinyMesh = PlatonicSolids.CreateCube(.001, .001, .001);

				string tinyObjectFileName = Path.Combine(folderToSaveStlsTo, Path.ChangeExtension("non_printing_extruder_change_mesh", ".stl"));

				StlProcessing.Save(tinyMesh, tinyObjectFileName, CancellationToken.None);

				outputItems.Add((Matrix4X4.Identity, tinyObjectFileName));
				mergeString += $"({savedStlCount++})";
			}

			return mergeString;
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