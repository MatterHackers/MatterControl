using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
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

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SlicingQueue
	{
		private static Thread slicePartThread = null;
		private static List<PrintItemWrapper> listOfSlicingItems = new List<PrintItemWrapper>();
		private static bool haltSlicingThread = false;

		private static List<SliceEngineInfo> availableSliceEngines;

		static public List<SliceEngineInfo> AvailableSliceEngines
		{
			get
			{
				if (availableSliceEngines == null)
				{
					availableSliceEngines = new List<SliceEngineInfo>();
					Slic3rInfo slic3rEngineInfo = new Slic3rInfo();
					if (slic3rEngineInfo.Exists())
					{
						availableSliceEngines.Add(slic3rEngineInfo);
					}
					CuraEngineInfo curaEngineInfo = new CuraEngineInfo();
					if (curaEngineInfo.Exists())
					{
						availableSliceEngines.Add(curaEngineInfo);
					}
					MatterSliceInfo matterSliceEngineInfo = new MatterSliceInfo();
					if (matterSliceEngineInfo.Exists())
					{
						availableSliceEngines.Add(matterSliceEngineInfo);
					}
				}
				return availableSliceEngines;
			}
		}

		static private SliceEngineInfo getSliceEngineInfoByType(SlicingEngineTypes engineType)
		{
			foreach (SliceEngineInfo info in AvailableSliceEngines)
			{
				if (info.GetSliceEngineType() == engineType)
				{
					return info;
				}
			}
			return null;
		}

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
			string preparingToSliceModelTxt = LocalizedString.Get("Preparing to slice model");
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

		private static string getSlicerFullPath()
		{
			SliceEngineInfo info = getSliceEngineInfoByType(ActiveSliceSettings.Instance.Helpers.ActiveSliceEngineType());
			if (info != null)
			{
				return info.GetEnginePath();
			}
			else
			{
				//throw new Exception("Slice engine is unavailable");
				return null;
			}
		}

		public static List<bool> extrudersUsed = new List<bool>();

		public static string[] GetStlFileLocations(string fileToSlice, bool doMergeInSlicer, ref string mergeRules)
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

				case ".AMF":
					List<MeshGroup> meshGroups = MeshFileIo.Load(fileToSlice);
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

							if (doMergeInSlicer)
							{
								int meshCount = meshGroup.Meshes.Count;
                                for (int meshIndex =0; meshIndex< meshCount; meshIndex++)
								{
									Mesh mesh = meshGroup.Meshes[meshIndex];
									if ((meshIndex % 2) == 0)
									{
										mergeRules += "({0}".FormatWith(savedStlCount);
									}
									else
									{
										if(meshIndex < meshCount -1)
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
								for (int i = 0; i < meshCount-1; i++)
								{
									mergeRules += ")";
								}
							}
							else
							{
								extruderFilesToSlice.Add(SaveAndGetFilenameForMaterial(meshGroup, materialsToInclude));
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
			MeshOutputSettings settings = new MeshOutputSettings();
			settings.MaterialIndexsToSave = materialIndexsToSaveInThisSTL;
			string extruder1StlFileToSlice = Path.Combine(folderToSaveStlsTo, fileName);
			MeshFileIo.Save(extruderMeshGroup, extruder1StlFileToSlice, settings);
			return extruder1StlFileToSlice;
		}

		public static bool runInProcess = true;
		private static Process slicerProcess = null;

		private static void CreateSlicedPartsThread()
		{
			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

			while (!haltSlicingThread)
			{
				if (listOfSlicingItems.Count > 0)
				{
					PrintItemWrapper itemToSlice = listOfSlicingItems[0];
					bool doMergeInSlicer = false;
					string mergeRules = "";
					doMergeInSlicer = ActiveSliceSettings.Instance.Helpers.ActiveSliceEngineType() == SlicingEngineTypes.MatterSlice;
                    string[] stlFileLocations = GetStlFileLocations(itemToSlice.FileLocation, doMergeInSlicer, ref mergeRules);
					string fileToSlice = stlFileLocations[0];
					// check that the STL file is currently on disk
					if (File.Exists(fileToSlice))
					{
						itemToSlice.CurrentlySlicing = true;

						string currentConfigurationFileAndPath = Path.Combine(ApplicationDataStorage.Instance.GCodeOutputPath, "config_" + ActiveSliceSettings.Instance.GetLongHashCode().ToString() + ".ini");
						ActiveSliceSettings.Instance.Helpers.GenerateConfigFile(currentConfigurationFileAndPath, true);

						string gcodePathAndFileName = itemToSlice.GetGCodePathAndFileName();
						bool gcodeFileIsComplete = itemToSlice.IsGCodeFileComplete(gcodePathAndFileName);

						if (!File.Exists(gcodePathAndFileName) || !gcodeFileIsComplete)
						{
							string commandArgs = "";

							switch (ActiveSliceSettings.Instance.Helpers.ActiveSliceEngineType())
							{
								case SlicingEngineTypes.Slic3r:
									commandArgs = "--load \"" + currentConfigurationFileAndPath + "\" --output \"" + gcodePathAndFileName + "\" \"" + fileToSlice + "\"";
									break;

								case SlicingEngineTypes.CuraEngine:
									commandArgs = "-v -o \"" + gcodePathAndFileName + "\" " + EngineMappingCura.GetCuraCommandLineSettings() + " \"" + fileToSlice + "\"";
									break;

								case SlicingEngineTypes.MatterSlice:
									{
										EngineMappingsMatterSlice.WriteMatterSliceSettingsFile(currentConfigurationFileAndPath);
										if (mergeRules == "")
										{
											commandArgs = "-v -o \"" + gcodePathAndFileName + "\" -c \"" + currentConfigurationFileAndPath + "\"";
										}
										else
										{
											commandArgs = "-b {0} -v -o \"".FormatWith(mergeRules) + gcodePathAndFileName + "\" -c \"" + currentConfigurationFileAndPath + "\"";
										}
										foreach (string filename in stlFileLocations)
										{
											commandArgs = commandArgs + " \"" + filename + "\"";
										}
									}
									break;
							}

#if false
							Mesh loadedMesh = StlProcessing.Load(fileToSlice);
							SliceLayers layers = new SliceLayers();
							layers.GetPerimetersForAllLayers(loadedMesh, .2, .2);
							layers.DumpSegmentsToGcode("test.gcode");
#endif

							if (OsInformation.OperatingSystem == OSType.Android ||
								((OsInformation.OperatingSystem == OSType.Mac || runInProcess)
									&& ActiveSliceSettings.Instance.Helpers.ActiveSliceEngineType() == SlicingEngineTypes.MatterSlice))
							{
								itemCurrentlySlicing = itemToSlice;
								MatterHackers.MatterSlice.LogOutput.GetLogWrites += SendProgressToItem;
								MatterSlice.MatterSlice.ProcessArgs(commandArgs);
								MatterHackers.MatterSlice.LogOutput.GetLogWrites -= SendProgressToItem;
								itemCurrentlySlicing = null;
							}
							else
							{
								slicerProcess = new Process();
								slicerProcess.StartInfo.Arguments = commandArgs;
								string slicerFullPath = getSlicerFullPath();

								slicerProcess.StartInfo.CreateNoWindow = true;
								slicerProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
								slicerProcess.StartInfo.RedirectStandardError = true;
								slicerProcess.StartInfo.RedirectStandardOutput = true;

								slicerProcess.StartInfo.FileName = slicerFullPath;
								slicerProcess.StartInfo.UseShellExecute = false;

								slicerProcess.OutputDataReceived += (sender, args) =>
								{
									if (args.Data != null)
									{
										string message = args.Data;
										message = message.Replace("=>", "").Trim();
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
							if (File.Exists(gcodePathAndFileName)
								&& File.Exists(currentConfigurationFileAndPath))
							{
								// make sure we have not already written the settings onto this file
								bool fileHaseSettings = false;
								int bufferSize = 32000;
								using (Stream fileStream = File.OpenRead(gcodePathAndFileName))
								{
									byte[] buffer = new byte[bufferSize];
									fileStream.Seek(Math.Max(0, fileStream.Length - bufferSize), SeekOrigin.Begin);
									int numBytesRead = fileStream.Read(buffer, 0, bufferSize);
									string fileEnd = System.Text.Encoding.UTF8.GetString(buffer);
									if (fileEnd.Contains("GCode settings used"))
									{
										fileHaseSettings = true;
									}
								}

								if (!fileHaseSettings)
								{
									using (StreamWriter gcodeWirter = File.AppendText(gcodePathAndFileName))
									{
										string oemName = "MatterControl";
										if (OemSettings.Instance.WindowTitleExtra != null && OemSettings.Instance.WindowTitleExtra.Trim().Length > 0)
										{
											oemName = oemName + " - {0}".FormatWith(OemSettings.Instance.WindowTitleExtra);
										}

										gcodeWirter.WriteLine("; {0} Version {1} Build {2} : GCode settings used".FormatWith(oemName, VersionInfo.Instance.ReleaseVersion, VersionInfo.Instance.BuildVersion));
										gcodeWirter.WriteLine("; Date {0} Time {1}:{2:00}".FormatWith(DateTime.Now.Date, DateTime.Now.Hour, DateTime.Now.Minute));

										foreach (string line in File.ReadLines(currentConfigurationFileAndPath))
										{
											gcodeWirter.WriteLine("; {0}".FormatWith(line));
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