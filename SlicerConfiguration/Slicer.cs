﻿/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public static class Slicer
	{
		private static Dictionary<Mesh, MeshPrintOutputSettings> meshPrintOutputSettings = new Dictionary<Mesh, MeshPrintOutputSettings>();

		public static List<bool> extrudersUsed = new List<bool>();
		public static bool runInProcess = false;

		public static List<(Matrix4X4 matrix, string fileName)> GetStlFileLocations(IObject3D object3D, ref string mergeRules, PrinterConfig printer, IProgress<ProgressStatus> progressReporter, CancellationToken cancellationToken)
		{
			var progressStatus = new ProgressStatus();

			extrudersUsed.Clear();

			int extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);
			for (int extruderIndex = 0; extruderIndex < extruderCount; extruderIndex++)
			{
				extrudersUsed.Add(false);
			}

			// If we have support enabled and are using an extruder other than 0 for it
			if (printer.Settings.GetValue<bool>("support_material"))
			{
				if (printer.Settings.GetValue<int>("support_material_extruder") != 0)
				{
					int supportExtruder = Math.Max(0, Math.Min(printer.Settings.GetValue<int>(SettingsKey.extruder_count) - 1, printer.Settings.GetValue<int>("support_material_extruder") - 1));
					extrudersUsed[supportExtruder] = true;
				}
			}

			// If we have raft enabled and are using an extruder other than 0 for it
			if (printer.Settings.GetValue<bool>("create_raft"))
			{
				if (printer.Settings.GetValue<int>("raft_extruder") != 0)
				{
					int raftExtruder = Math.Max(0, Math.Min(printer.Settings.GetValue<int>(SettingsKey.extruder_count) - 1, printer.Settings.GetValue<int>("raft_extruder") - 1));
					extrudersUsed[raftExtruder] = true;
				}
			}

			{
				// TODO: Once graph parsing is added to MatterSlice we can remove and avoid this flattening
				meshPrintOutputSettings.Clear();

				// Flatten the scene, filtering out items outside of the build volume
				var meshItemsOnBuildPlate = printer.PrintableItems(object3D);
				if (meshItemsOnBuildPlate.Any())
				{
					int maxExtruderIndex = 0;

					var itemsByExtruder = new List<IEnumerable<IObject3D>>();
					for (int extruderIndexIn = 0; extruderIndexIn < extruderCount; extruderIndexIn++)
					{
						var extruderIndex = extruderIndexIn;
						var itemsThisExtruder = meshItemsOnBuildPlate.Where((item) =>
							(File.Exists(item.MeshPath) // Drop missing files
								|| File.Exists(Path.Combine(Object3D.AssetsPath, item.MeshPath)))
							&& (item.WorldMaterialIndex() == extruderIndex
								|| (extruderIndex == 0
									&& (item.WorldMaterialIndex() >= extruderCount || item.WorldMaterialIndex() == -1)))
							&& (item.WorldOutputType() == PrintOutputTypes.Solid || item.WorldOutputType() == PrintOutputTypes.Default));

						itemsByExtruder.Add(itemsThisExtruder);
						extrudersUsed[extruderIndex] |= itemsThisExtruder.Any();
						if (extrudersUsed[extruderIndex])
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
						mergeRules += "," + AddObjectsForExtruder(supportObjects, outputOptions, ref savedStlCount) + "S";
					}

					mergeRules += " ";

					return outputOptions;
				}
			}

			return new List<(Matrix4X4 matrix, string fileName)>();
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
					outputItems.Add((item.WorldMatrix(), Path.Combine(assetsDirectory, item.MeshPath)));
					mergeString += $"({savedStlCount++}";
					first = false;
				}

				mergeString += new String(')', items.Count());
			}
			else
			{
				// TODO: consider dropping the custom path and using the AssetPath as above
				string folderToSaveStlsTo = Path.Combine(ApplicationDataStorage.Instance.ApplicationTempDataPath, "amf_to_stl");

				// Create directory if needed
				Directory.CreateDirectory(folderToSaveStlsTo);

				Mesh tinyMesh = PlatonicSolids.CreateCube(.001, .001, .001);

				string tinyObjectFileName = Path.Combine(folderToSaveStlsTo, Path.ChangeExtension(tinyMesh.GetLongHashCode().ToString(), ".stl"));

				StlProcessing.Save(tinyMesh, tinyObjectFileName, CancellationToken.None);

				outputItems.Add((Matrix4X4.Identity, tinyObjectFileName));
				mergeString += $"({savedStlCount++})";
			}

			return mergeString;
		}

		public static Task<bool> SliceFile(string sourceFile, string gcodeFilePath, PrinterConfig printer, IProgress<ProgressStatus> progressReporter, CancellationToken cancellationToken)
		{
			var progressStatus = new ProgressStatus()
			{
				Status = "Loading"
			};
			progressReporter.Report(progressStatus);

			IObject3D object3D;

			if (string.Equals(Path.GetExtension(sourceFile), ".mcx", StringComparison.OrdinalIgnoreCase))
			{
				object3D = Object3D.Load(sourceFile, cancellationToken, null, (ratio, status) =>
				{
					progressStatus.Progress0To1 = ratio;
					progressStatus.Status = status;
				});
			}
			else
			{
				object3D = new Object3D()
				{
					MeshPath = sourceFile
				};
			}

			return SliceItem(object3D, gcodeFilePath, printer, progressReporter, cancellationToken);
		}

		public static Task<bool> SliceItem(IObject3D object3D, string gcodeFilePath, PrinterConfig printer, IProgress<ProgressStatus> progressReporter, CancellationToken cancellationToken)
		{
			string mergeRules = "";

			var stlFileLocations = GetStlFileLocations(object3D, ref mergeRules, printer, progressReporter, cancellationToken);

			return SliceItem(stlFileLocations, mergeRules, gcodeFilePath, printer, progressReporter, cancellationToken);
		}

		public static Task<bool> SliceItem(List<(Matrix4X4 matrix, string fileName)> stlFileLocations, string mergeRules, string gcodeFilePath, PrinterConfig printer, IProgress<ProgressStatus> reporter, CancellationToken cancellationToken)
		{
			// Wrap the reporter with a specialized MatterSlice string parser for percent from string results
			var sliceProgressReporter = new SliceProgressReporter(reporter, printer);

			bool slicingSucceeded = true;

			if(stlFileLocations.Count > 0)
			{
				var progressStatus = new ProgressStatus()
				{
					Status = "Generating Config"
				};
				sliceProgressReporter.Report(progressStatus);

				string configFilePath = Path.Combine(
					ApplicationDataStorage.Instance.GCodeOutputPath,
					string.Format("config_{0}.ini", printer.Settings.GetLongHashCode().ToString()));

				progressStatus.Status = "Starting slicer";
				sliceProgressReporter.Report(progressStatus);

				if (!File.Exists(gcodeFilePath)
					|| !HasCompletedSuccessfully(gcodeFilePath))
				{
					string commandArgs;

					var matrixAndMeshArgs = new StringBuilder();
					foreach (var matrixAndFile in stlFileLocations)
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
								matrixString += matrixAndFile.matrix[i, j].ToString("0.######");
								first = false;
							}
						}

						matrixAndMeshArgs.Append($" -m \"{matrixString}\"");
						matrixAndMeshArgs.Append($" \"{matrixAndFile.fileName}\" ");
					}

					EngineMappingsMatterSlice.WriteSliceSettingsFile(configFilePath, rawLines: new[]
					{
						$"booleanOperations = {mergeRules}",
						$"additionalArgsToProcess ={matrixAndMeshArgs}"
					});

					commandArgs = $"-v -o \"{gcodeFilePath}\" -c \"{configFilePath}\"";

					if (AggContext.OperatingSystem == OSType.Android
						|| AggContext.OperatingSystem == OSType.Mac
						|| runInProcess)
					{
						EventHandler WriteOutput = (s, e) =>
						{
							if(cancellationToken.IsCancellationRequested)
							{
								MatterSlice.MatterSlice.Stop();
							}
							if (s is string stringValue)
							{



								sliceProgressReporter?.Report(new ProgressStatus()
								{
									Status = stringValue
								});
							}
						};

						MatterSlice.LogOutput.GetLogWrites += WriteOutput;

						MatterSlice.MatterSlice.ProcessArgs(commandArgs);

						MatterSlice.LogOutput.GetLogWrites -= WriteOutput;
					}
					else
					{
						bool forcedExit = false;

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
					if (File.Exists(gcodeFilePath)
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

								foreach (string line in File.ReadLines(configFilePath))
								{
									gcodeWriter.WriteLine("; {0}", line);
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
				if (reader.BaseStream.Length > 10000)
				{
					reader.BaseStream.Seek(-10000, SeekOrigin.End);
				}

				string endText = reader.ReadToEnd();

				return endText.Contains("; MatterSlice Completed Successfully");
			}
		}
	}
}
