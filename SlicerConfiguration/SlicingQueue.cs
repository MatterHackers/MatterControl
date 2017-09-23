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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SlicingQueue
	{
		static Dictionary<Mesh, MeshPrintOutputSettings> meshPrintOutputSettings = new Dictionary<Mesh, MeshPrintOutputSettings>();
		private static Thread slicePartThread = null;
		private static List<PrintItemWrapper> listOfSlicingItems = new List<PrintItemWrapper>();
		private static bool haltSlicingThread = false;

		private SlicingQueue()
		{
			if (slicePartThread == null)
			{
				slicePartThread = new Thread(CreateSlicedPartsThread);
				slicePartThread.Name = "slicePartThread";
				slicePartThread.IsBackground = true;
				slicePartThread.Start();
			}
		}

		private static SlicingQueue instance;

		static public SlicingQueue Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new SlicingQueue();
				}
				return instance;
			}
		}

		public void QueuePartForSlicing(PrintItemWrapper itemToQueue)
		{
			itemToQueue.DoneSlicing = false;
			string preparingToSliceModelTxt = "Preparing to slice model".Localize();
			string peparingToSliceModelFull = string.Format("{0}...", preparingToSliceModelTxt);
			itemToQueue.OnSlicingOutputMessage(new StringEventArgs(peparingToSliceModelFull));
			lock(listOfSlicingItems)
			{
				//Add to thumbnail generation queue
				listOfSlicingItems.Add(itemToQueue);
			}
		}

		public void ShutDownSlicingThread()
		{
			haltSlicingThread = true;
		}

		private static string macQuotes(string textLine)
		{
			if (textLine.StartsWith("\"") && textLine.EndsWith("\""))
			{
				return textLine;
			}
			else
			{
				return "\"" + textLine.Replace("\"", "\\\"") + "\"";
			}
		}

		public static List<bool> extrudersUsed = new List<bool>();

		public static string[] GetStlFileLocations(string fileToSlice, ref string mergeRules)
		{
			extrudersUsed.Clear();

			int extruderCount = ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count);
			for (int extruderIndex = 0; extruderIndex < extruderCount; extruderIndex++)
			{
				extrudersUsed.Add(false);
			}

			// If we have support enabled and are using an extruder other than 0 for it
			if (ActiveSliceSettings.Instance.GetValue<bool>("support_material"))
			{
				if (ActiveSliceSettings.Instance.GetValue<int>("support_material_extruder") != 0)
				{
					int supportExtruder = Math.Max(0, Math.Min(ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count) - 1, ActiveSliceSettings.Instance.GetValue<int>("support_material_extruder") - 1));
					extrudersUsed[supportExtruder] = true;
				}
			}

			// If we have raft enabled and are using an extruder other than 0 for it
			if (ActiveSliceSettings.Instance.GetValue<bool>("create_raft"))
			{
				if (ActiveSliceSettings.Instance.GetValue<int>("raft_extruder") != 0)
				{
					int raftExtruder = Math.Max(0, Math.Min(ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count) - 1, ActiveSliceSettings.Instance.GetValue<int>("raft_extruder") - 1));
					extrudersUsed[raftExtruder] = true;
				}
			}

			switch (Path.GetExtension(fileToSlice).ToUpper())
			{
				case ".STL":
				case ".GCODE":
					extrudersUsed[0] = true;
					return new string[] { fileToSlice };

				case ".MCX":
				case ".AMF":
				case ".OBJ":
					// TODO: Once graph parsing is added to MatterSlice we can remove and avoid this flattening
					meshPrintOutputSettings.Clear();
					List<MeshGroup> meshGroups = new List<MeshGroup> { Object3D.Load(fileToSlice, CancellationToken.None).Flatten(meshPrintOutputSettings) };
					if (meshGroups != null)
					{
						List<MeshGroup> extruderMeshGroups = new List<MeshGroup>();
						for (int extruderIndex = 0; extruderIndex < extruderCount; extruderIndex++)
						{
							extruderMeshGroups.Add(new MeshGroup());
						}

						// and add one more extruder mesh group for user generated support (if exists)
						extruderMeshGroups.Add(new MeshGroup());

						int maxExtruderIndex = 0;
						foreach (MeshGroup meshGroup in meshGroups)
						{
							foreach (Mesh mesh in meshGroup.Meshes)
							{
								MeshPrintOutputSettings material = meshPrintOutputSettings[mesh];
								switch(material.PrintOutputTypes)
								{
									case PrintOutputTypes.Solid:
										int extruderIndex = Math.Max(0, material.ExtruderIndex);
										maxExtruderIndex = Math.Max(maxExtruderIndex, extruderIndex);
										if (extruderIndex >= extruderCount)
										{
											extrudersUsed[0] = true;
											extruderMeshGroups[0].Meshes.Add(mesh);
										}
										else
										{
											extrudersUsed[extruderIndex] = true;
											extruderMeshGroups[extruderIndex].Meshes.Add(mesh);
										}
										break;

									case PrintOutputTypes.Support:
										// add it to the group reserved for user support
										extruderMeshGroups[extruderCount].Meshes.Add(mesh);
										break;
								}
							}
						}

						int savedStlCount = 0;
						List<string> extruderFilesToSlice = new List<string>();
						for (int extruderIndex = 0; extruderIndex < extruderMeshGroups.Count; extruderIndex++)
						{
							MeshGroup meshGroup = extruderMeshGroups[extruderIndex];

							int meshCount = meshGroup.Meshes.Count;
							if (meshCount > 0)
							{
								for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
								{
									Mesh mesh = meshGroup.Meshes[meshIndex];
									if (meshIndex == 0)
									{
										mergeRules += "({0}".FormatWith(savedStlCount);
									}
									else
									{
										if (meshIndex < meshCount - 1)
										{
											mergeRules += ",({0}".FormatWith(savedStlCount);
										}
										else
										{
											mergeRules += ",{0}".FormatWith(savedStlCount);
										}
									}
									int meshExtruderIndex = meshPrintOutputSettings[mesh].ExtruderIndex;

									extruderFilesToSlice.Add(SaveAndGetFilePathForMesh(mesh));

									savedStlCount++;
								}

								int closeParentsCount = (meshCount == 1 || meshCount == 2) ? 1 : meshCount - 1;
								for (int i = 0; i < closeParentsCount; i++)
								{
									mergeRules += ")";
								}
							}
							else if(extruderIndex <= maxExtruderIndex) // this extruder has no meshes
							{
								// check if there are any more meshes after this extruder that will be added
								int otherMeshCounts = 0;
								for (int otherExtruderIndex = extruderIndex + 1; otherExtruderIndex < extruderMeshGroups.Count; otherExtruderIndex++)
								{
									otherMeshCounts += extruderMeshGroups[otherExtruderIndex].Meshes.Count;
								}
								if (otherMeshCounts > 0) // there are more extrudes to use after this not used one
								{
									// add in a blank for this extruder
									mergeRules += $"({savedStlCount})";
								}
								// save an empty mesh
								extruderFilesToSlice.Add(SaveAndGetFilePathForMesh(PlatonicSolids.CreateCube(.001, .001, .001)));
								savedStlCount++;
							}
						}

						// if we added user generated support 
						if(extruderMeshGroups[extruderCount].Meshes.Count > 0)
						{
							// add a flag to the merge rules to let us know there was support
							mergeRules += "S";
						}

						return extruderFilesToSlice.ToArray();
					}
					return new string[] { "" };

				default:
					return new string[] { "" };
			}
		}

		private static string SaveAndGetFilePathForMesh(Mesh meshToSave)
		{
			string folderToSaveStlsTo = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "temp", "amf_to_stl");

			// Create directory if needed
			Directory.CreateDirectory(folderToSaveStlsTo);

			string filePath = Path.Combine(folderToSaveStlsTo, Path.ChangeExtension(Path.GetRandomFileName(), ".stl"));

			MeshFileIo.Save(meshToSave, filePath);

			return filePath;
		}

		private static string SaveAndGetFilePathForMaterial(MeshGroup extruderMeshGroup, List<int> materialIndexsToSaveInThisSTL)
		{
			string folderToSaveStlsTo = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "temp", "amf_to_stl");

			// Create directory if needed
			Directory.CreateDirectory(folderToSaveStlsTo);

			string filePath = Path.Combine(folderToSaveStlsTo, Path.ChangeExtension(Path.GetRandomFileName(), ".stl"));

			MeshFileIo.Save(
				extruderMeshGroup, 
				filePath, 
				new MeshOutputSettings());

			return filePath;
		}

		public static bool runInProcess = false;
		private static Process slicerProcess = null;

		private class SliceMessageReporter : IProgress<string>
		{
			private PrintItemWrapper printItem;
			public SliceMessageReporter(PrintItemWrapper printItem)
			{
				this.printItem = printItem;
			}

			public void Report(string message)
			{
				UiThread.RunOnIdle(() =>
				{
					printItem.OnSlicingOutputMessage(new StringEventArgs(message));
				});
			}
		}

		private static void CreateSlicedPartsThread()
		{
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			while (!haltSlicingThread)
			{
				if (listOfSlicingItems.Count > 0)
				{
					PrintItemWrapper itemToSlice = listOfSlicingItems[0];

					/* 
					 * 
			#if false
									Mesh loadedMesh = StlProcessing.Load(fileToSlice);
									SliceLayers layers = new SliceLayers();
									layers.GetPerimetersForAllLayers(loadedMesh, .2, .2);
									layers.DumpSegmentsToGcode("test.gcode");
			#endif
					 * */

					if (File.Exists(itemToSlice.FileLocation))
					{
						itemToSlice.CurrentlySlicing = true;

						string gcodeFilePath = itemToSlice.GetGCodePathAndFileName();

						var reporter = new SliceMessageReporter(itemToSlice);

						if (AggContext.OperatingSystem == OSType.Android
							|| AggContext.OperatingSystem == OSType.Mac
							|| runInProcess)
						{

							itemCurrentlySlicing = itemToSlice;
							MatterHackers.MatterSlice.LogOutput.GetLogWrites += SendProgressToItem;

							SliceFile(itemToSlice.FileLocation, gcodeFilePath, reporter);

							MatterHackers.MatterSlice.LogOutput.GetLogWrites -= SendProgressToItem;
							itemCurrentlySlicing = null;
						}
						else
						{
							SliceFile(itemToSlice.FileLocation, gcodeFilePath, reporter);
						}
					}

					UiThread.RunOnIdle(() =>
					{
						itemToSlice.CurrentlySlicing = false;
						itemToSlice.DoneSlicing = true;
					});

					lock (listOfSlicingItems)
					{
						listOfSlicingItems.RemoveAt(0);
					}
				}

				Thread.Sleep(100);
			}
		}

		public static async Task SliceFileAsync(PrintItemWrapper printItem, IProgress<string> progressReporter)
		{
			string gcodeFilePath = printItem.GetGCodePathAndFileName();

			await Task.Run(() => SliceFile(
				printItem.FileLocation, 
				gcodeFilePath, 
				progressReporter));
		}

		public static async Task SliceFileAsync(string sourceFile, string gcodeFilePath, IProgress<string> progressReporter)
		{
			await Task.Run(() => SliceFile(sourceFile, gcodeFilePath, progressReporter));
		}

		private static void SliceFile(string sourceFile, string gcodeFilePath, IProgress<string> progressReporter)
		{
			string mergeRules = "";

			string[] stlFileLocations = GetStlFileLocations(sourceFile, ref mergeRules);
			string fileToSlice = stlFileLocations[0];

			{
				string configFilePath = Path.Combine(
					ApplicationDataStorage.Instance.GCodeOutputPath,
					string.Format("config_{0}.ini", ActiveSliceSettings.Instance.GetLongHashCode().ToString()));

				if (!File.Exists(gcodeFilePath))
				{
					string commandArgs;

					EngineMappingsMatterSlice.WriteSliceSettingsFile(configFilePath);

					if (mergeRules == "")
					{
						commandArgs = $"-v -o \"{gcodeFilePath}\" -c \"{configFilePath}\"";
					}
					else
					{
						commandArgs = $"-b {mergeRules} -v -o \"{gcodeFilePath}\" -c \"{configFilePath}\"";
					}

					foreach (string filename in stlFileLocations)
					{
						commandArgs += $" \"{filename}\"";
					}

					if (AggContext.OperatingSystem == OSType.Android
						|| AggContext.OperatingSystem == OSType.Mac
						|| runInProcess)
					{
						MatterSlice.MatterSlice.ProcessArgs(commandArgs);
					}
					else
					{
						slicerProcess = new Process()
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

						slicerProcess.OutputDataReceived += (sender, args) =>
						{
							if (args.Data != null)
							{
								string message = args.Data.Replace("=>", "").Trim();
								if (message.Contains(".gcode"))
								{
									message = "Saving intermediate file";
								}
								message += "...";

								progressReporter?.Report(message);
							}
						};

						slicerProcess.Start();
						slicerProcess.BeginOutputReadLine();

						string stdError = slicerProcess.StandardError.ReadToEnd();

						slicerProcess.WaitForExit();
						lock (slicerProcess)
						{
							slicerProcess = null;
						}
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
		}

		private static PrintItemWrapper itemCurrentlySlicing;

		private static void SendProgressToItem(object sender, EventArgs args)
		{
			string message = sender as string;
			if (message != null)
			{
				message = message.Replace("=>", "").Trim();
				if (message.Contains(".gcode"))
				{
					message = "Saving intermediate file";
				}
				message += "...";
				UiThread.RunOnIdle(() =>
				{
					if (itemCurrentlySlicing != null)
					{
						itemCurrentlySlicing.OnSlicingOutputMessage(new StringEventArgs(message));
					}
				});
			}
		}

		internal void CancelCurrentSlicing()
		{
			if (slicerProcess != null)
			{
				lock(slicerProcess)
				{
					if (slicerProcess != null && !slicerProcess.HasExited)
					{
						slicerProcess.Kill();
					}
				}
			}
			else
			{
				MatterHackers.MatterSlice.MatterSlice.Stop();
			}
		}
	}
}
