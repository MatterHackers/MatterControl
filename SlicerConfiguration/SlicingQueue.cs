using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;

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
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SlicingQueue
	{
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
					List<MeshGroup> meshGroups = new List<MeshGroup> { Object3D.Load(fileToSlice).Flatten() };
					if (meshGroups != null)
					{
						List<MeshGroup> extruderMeshGroups = new List<MeshGroup>();
						for (int extruderIndex = 0; extruderIndex < extruderCount; extruderIndex++)
						{
							extruderMeshGroups.Add(new MeshGroup());
						}
						int maxExtruderIndex = 0;
						foreach (MeshGroup meshGroup in meshGroups)
						{
							foreach (Mesh mesh in meshGroup.Meshes)
							{
								MeshMaterialData material = MeshMaterialData.Get(mesh);
								int extruderIndex = Math.Max(0, material.MaterialIndex - 1);
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
							}
						}

						int savedStlCount = 0;
						List<string> extruderFilesToSlice = new List<string>();
						for (int extruderIndex = 0; extruderIndex < extruderMeshGroups.Count; extruderIndex++)
						{
							MeshGroup meshGroup = extruderMeshGroups[extruderIndex];
							List<int> materialsToInclude = new List<int>();
							materialsToInclude.Add(extruderIndex + 1);
							if (extruderIndex == 0)
							{
								for (int j = extruderCount + 1; j < maxExtruderIndex + 2; j++)
								{
									materialsToInclude.Add(j);
								}
							}

							int meshCount = meshGroup.Meshes.Count;
							if (meshCount > 0)
							{
								for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
								{
									Mesh mesh = meshGroup.Meshes[meshIndex];
									if ((meshIndex % 2) == 0)
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
									int currentMeshMaterialIntdex = MeshMaterialData.Get(mesh).MaterialIndex;
									if (materialsToInclude.Contains(currentMeshMaterialIntdex))
									{
										extruderFilesToSlice.Add(SaveAndGetFilenameForMesh(mesh));
									}
									savedStlCount++;
								}

								for (int i = 0; i < meshCount; i++)
								{
									mergeRules += ")";
								}
							}
							else // this extruder has no meshes
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
								extruderFilesToSlice.Add(SaveAndGetFilenameForMesh(PlatonicSolids.CreateCube(.001, .001, .001)));
								savedStlCount++;
							}
						}

						return extruderFilesToSlice.ToArray();
					}
					return new string[] { "" };

				default:
					return new string[] { "" };
			}
		}

		private static string SaveAndGetFilenameForMesh(Mesh meshToSave)
		{
			string fileName = Path.ChangeExtension(Path.GetRandomFileName(), ".stl");
			string applicationUserDataPath = ApplicationDataStorage.ApplicationUserDataPath;
			string folderToSaveStlsTo = Path.Combine(applicationUserDataPath, "data", "temp", "amf_to_stl");
			if (!Directory.Exists(folderToSaveStlsTo))
			{
				Directory.CreateDirectory(folderToSaveStlsTo);
			}
			string saveStlFileName = Path.Combine(folderToSaveStlsTo, fileName);
			MeshFileIo.Save(meshToSave, saveStlFileName);
			return saveStlFileName;
		}

		private static string SaveAndGetFilenameForMaterial(MeshGroup extruderMeshGroup, List<int> materialIndexsToSaveInThisSTL)
		{
			string fileName = Path.ChangeExtension(Path.GetRandomFileName(), ".stl");
			string applicationUserDataPath = ApplicationDataStorage.ApplicationUserDataPath;
			string folderToSaveStlsTo = Path.Combine(applicationUserDataPath, "data", "temp", "amf_to_stl");
			if (!Directory.Exists(folderToSaveStlsTo))
			{
				Directory.CreateDirectory(folderToSaveStlsTo);
			}

			MeshOutputSettings settings = new MeshOutputSettings()
			{
				MaterialIndexsToSave = materialIndexsToSaveInThisSTL
			};

			string extruder1StlFileToSlice = Path.Combine(folderToSaveStlsTo, fileName);
			MeshFileIo.Save(extruderMeshGroup, extruder1StlFileToSlice, settings);
			return extruder1StlFileToSlice;
		}

		public static bool runInProcess = false;
		private static Process slicerProcess = null;

		private static void CreateSlicedPartsThread()
		{
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			while (!haltSlicingThread)
			{
				if (listOfSlicingItems.Count > 0)
				{
					PrintItemWrapper itemToSlice = listOfSlicingItems[0];
					string mergeRules = "";
					
					string[] stlFileLocations = GetStlFileLocations(itemToSlice.FileLocation, ref mergeRules);
					string fileToSlice = stlFileLocations[0];

					// check that the STL file is currently on disk
					if (File.Exists(fileToSlice))
					{
						itemToSlice.CurrentlySlicing = true;

						string configFilePath = Path.Combine(
							ApplicationDataStorage.Instance.GCodeOutputPath,
							string.Format("config_{0}.ini", ActiveSliceSettings.Instance.GetLongHashCode().ToString()));

						string gcodeFilePath = itemToSlice.GetGCodePathAndFileName();
						bool gcodeFileIsComplete = itemToSlice.IsGCodeFileComplete(gcodeFilePath);

						if (!File.Exists(gcodeFilePath) || !gcodeFileIsComplete)
						{
							string commandArgs = "";

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
											commandArgs +=  $" \"{filename}\"";
										}
#if false
							Mesh loadedMesh = StlProcessing.Load(fileToSlice);
							SliceLayers layers = new SliceLayers();
							layers.GetPerimetersForAllLayers(loadedMesh, .2, .2);
							layers.DumpSegmentsToGcode("test.gcode");
#endif

							if (OsInformation.OperatingSystem == OSType.Android 
								|| OsInformation.OperatingSystem == OSType.Mac 
								|| runInProcess)
							{
								itemCurrentlySlicing = itemToSlice;
								MatterHackers.MatterSlice.LogOutput.GetLogWrites += SendProgressToItem;
								MatterSlice.MatterSlice.ProcessArgs(commandArgs);
								MatterHackers.MatterSlice.LogOutput.GetLogWrites -= SendProgressToItem;
								itemCurrentlySlicing = null;
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

										UiThread.RunOnIdle(() =>
										{
											itemToSlice.OnSlicingOutputMessage(new StringEventArgs(message));
										});
									}
								};

								slicerProcess.Start();
								slicerProcess.BeginOutputReadLine();
								string stdError = slicerProcess.StandardError.ReadToEnd();

								slicerProcess.WaitForExit();
								lock(slicerProcess)
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

					UiThread.RunOnIdle(() =>
					{
						itemToSlice.CurrentlySlicing = false;
						itemToSlice.DoneSlicing = true;
					});

					lock(listOfSlicingItems)
					{
						listOfSlicingItems.RemoveAt(0);
					}
				}

				Thread.Sleep(100);
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