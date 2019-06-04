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
	public static class Slicer
	{
		private static Dictionary<Mesh, MeshPrintOutputSettings> meshPrintOutputSettings = new Dictionary<Mesh, MeshPrintOutputSettings>();

		public static List<bool> ExtrudersUsed = new List<bool>();

		public static bool RunInProcess { get; set; } = false;

		public static void GetExtrudersUsed(List<bool> extrudersUsed, IObject3D object3D, PrinterConfig printer, bool checkForMeshFile)
		{
			extrudersUsed.Clear();

			var meshItemsOnBuildPlate = printer.PrintableItems(object3D);
			if (!meshItemsOnBuildPlate.Any())
			{
				return;
			}

			int extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);
			// Make sure we only consider 1 extruder if in spiral vase mode
			if (printer.Settings.GetValue<bool>(SettingsKey.spiral_vase)
				&& extrudersUsed.Count(used => used == true) > 1)
			{
				extruderCount = 1;
			}

			for (int extruderIndex = 0; extruderIndex < extruderCount; extruderIndex++)
			{
				extrudersUsed.Add(false);
			}

			// If we have support enabled and are using an extruder other than 0 for it
			if (object3D.VisibleMeshes().Any(i => i.WorldOutputType() == PrintOutputTypes.Support))
			{
				if (printer.Settings.GetValue<int>(SettingsKey.support_material_extruder) != 0)
				{
					int supportExtruder = Math.Max(0, Math.Min(extruderCount - 1, printer.Settings.GetValue<int>(SettingsKey.support_material_extruder) - 1));
					extrudersUsed[supportExtruder] = true;
				}
			}

			// If we have raft enabled and are using an extruder other than 0 for it
			if (printer.Settings.GetValue<bool>(SettingsKey.create_raft))
			{
				if (printer.Settings.GetValue<int>(SettingsKey.raft_extruder) != 0)
				{
					int raftExtruder = Math.Max(0, Math.Min(extruderCount - 1, printer.Settings.GetValue<int>(SettingsKey.raft_extruder) - 1));
					extrudersUsed[raftExtruder] = true;
				}
			}

			for (int extruderIndex = 0; extruderIndex < extruderCount; extruderIndex++)
			{
				IEnumerable<IObject3D> itemsThisExtruder = GetItemsForExtruder(meshItemsOnBuildPlate, extruderCount, extruderIndex, checkForMeshFile);
				extrudersUsed[extruderIndex] |= itemsThisExtruder.Any();
			}
		}

		public static bool T1OrGreaterUsed(PrinterConfig printer)
		{
			var extrudersUsed = new List<bool>();
			Slicer.GetExtrudersUsed(extrudersUsed, printer.Bed.Scene, printer, false);
			for (int i = 1; i < extrudersUsed.Count; i++)
			{
				if (extrudersUsed[i])
				{
					return true;
				}
			}

			return false;
		}

		public static List<(Matrix4X4 matrix, string fileName)> GetStlFileLocations(IObject3D object3D, ref string mergeRules, PrinterConfig printer, IProgress<ProgressStatus> progressReporter, CancellationToken cancellationToken)
		{
			var progressStatus = new ProgressStatus();

			GetExtrudersUsed(ExtrudersUsed, object3D, printer, true);
			// TODO: Once graph parsing is added to MatterSlice we can remove and avoid this flattening
			meshPrintOutputSettings.Clear();

			// Flatten the scene, filtering out items outside of the build volume
			var meshItemsOnBuildPlate = printer.PrintableItems(object3D);

			if (meshItemsOnBuildPlate.Any())
			{
				int maxExtruderIndex = 0;

				var itemsByExtruder = new List<IEnumerable<IObject3D>>();
				int extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);
				// Make sure we only consider 1 extruder if in spiral vase mode
				if (printer.Settings.GetValue<bool>(SettingsKey.spiral_vase))
				{
					extruderCount = 1;
				}

				for (int extruderIndexIn = 0; extruderIndexIn < extruderCount; extruderIndexIn++)
				{
					var extruderIndex = extruderIndexIn;
					IEnumerable<IObject3D> itemsThisExtruder = GetItemsForExtruder(meshItemsOnBuildPlate, extruderCount, extruderIndex, true);

					itemsByExtruder.Add(itemsThisExtruder);
					if (ExtrudersUsed[extruderIndex])
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

		private static IEnumerable<IObject3D> GetItemsForExtruder(IEnumerable<IObject3D> meshItemsOnBuildPlate, int extruderCount, int extruderIndex, bool checkForMeshFile)
		{
			var itemsThisExtruder = meshItemsOnBuildPlate.Where((item) =>
				(!checkForMeshFile || (File.Exists(item.MeshPath) // Drop missing files
					|| File.Exists(Path.Combine(Object3D.AssetsPath, item.MeshPath))))
				&& (item.WorldMaterialIndex() == extruderIndex
					|| (extruderIndex == 0
						&& (item.WorldMaterialIndex() >= extruderCount || item.WorldMaterialIndex() == -1)))
				&& (item.WorldOutputType() == PrintOutputTypes.Solid || item.WorldOutputType() == PrintOutputTypes.Default));
			return itemsThisExtruder;
		}

		private static string AddObjectsForExtruder(IEnumerable<IObject3D> items,
			List<(Matrix4X4 matrix, string fileName)> outputItems,
			ref int savedStlCount)
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

		public static Task<bool> SliceItem(IObject3D object3D, string gcodeFilePath, PrinterConfig printer, IProgress<ProgressStatus> progressReporter, CancellationToken cancellationToken)
		{
			string mergeRules = "";

			var stlFileLocations = GetStlFileLocations(object3D, ref mergeRules, printer, progressReporter, cancellationToken);

			if (stlFileLocations.Count > 0)
			{
				return SliceItem(stlFileLocations, mergeRules, gcodeFilePath, printer, progressReporter, cancellationToken);
			}

			return Task.FromResult(false);
		}

		public static Task<bool> SliceItem(List<(Matrix4X4 matrix, string fileName)> stlFileLocations, string mergeRules, string gcodeFilePath, PrinterConfig printer, IProgress<ProgressStatus> reporter, CancellationToken cancellationToken)
		{
			// Wrap the reporter with a specialized MatterSlice string parser for percent from string results
			var sliceProgressReporter = new SliceProgressReporter(reporter, printer);

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
					string.Format("config_{0}.ini", printer.Settings.GetGCodeCacheKey().ToString()));

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

					printer.EngineMappingsMatterSlice.WriteSliceSettingsFile(configFilePath, rawLines: new[]
					{
						$"booleanOperations = {mergeRules}",
						$"additionalArgsToProcess ={matrixAndMeshArgs}"
					});

					commandArgs = $"-v -o \"{gcodeFilePath}\" -c \"{configFilePath}\"";

					bool forcedExit = false;

					if (AggContext.OperatingSystem == OSType.Android
						|| AggContext.OperatingSystem == OSType.Mac
						|| RunInProcess)
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
	}
}
