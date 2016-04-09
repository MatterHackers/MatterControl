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

using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.PolygonMesh.Processors;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MatterHackers.MatterControl
{
	internal class ManifestItem
	{
		public int ItemQuantity { get; set; }

		public string Name { get; set; }

		public string FileName { get; set; }
	}

	internal class Project
	{
		private List<ManifestItem> projectFiles;
		private string projectName = "Test Project";
		private string projectDateCreated;

		public Project()
		{
			DateTime now = DateTime.Now;
			projectDateCreated = now.ToString("s");
		}

		public List<ManifestItem> ProjectFiles
		{
			get
			{
				return projectFiles;
			}
			set
			{
				projectFiles = value;
			}
		}

		public string ProjectName
		{
			get
			{
				return projectName;
			}
			set
			{
				projectName = value;
			}
		}

		public string ProjectDateCreated
		{
			get
			{
				return projectDateCreated;
			}
			set
			{
				projectDateCreated = value;
			}
		}
	}

	internal class ProjectFileHandler
	{
		private Project project;
		private Dictionary<string, ManifestItem> sourceFiles = new Dictionary<string, ManifestItem>();
		private HashSet<string> addedFileNames = new HashSet<string>();

		public ProjectFileHandler(List<PrintItem> projectFiles)
		{
			if (projectFiles != null)
			{
				project = new Project();

				foreach (PrintItem item in projectFiles)
				{
					if (sourceFiles.ContainsKey(item.FileLocation))
					{
						sourceFiles[item.FileLocation].ItemQuantity = sourceFiles[item.FileLocation].ItemQuantity + 1;
					}
					else
					{
						string fileNameOnly = Path.GetFileName(item.FileLocation);
						if (addedFileNames.Contains(fileNameOnly))
						{
							StyledMessageBox.ShowMessageBox(null, string.Format("Duplicate file name found but in a different folder '{0}'. This part will not be added to the collection.\n\n{1}", fileNameOnly, item.FileLocation), "Duplicate File");
							continue;
						}

						addedFileNames.Add(fileNameOnly);

						ManifestItem manifestItem = new ManifestItem();
						manifestItem.ItemQuantity = 1;
						manifestItem.Name = item.Name;
						manifestItem.FileName = Path.GetFileName(item.FileLocation);

						sourceFiles.Add(item.FileLocation, manifestItem);
					}
				}
				List<ManifestItem> manifestFiles = sourceFiles.Values.ToList();
				project.ProjectFiles = manifestFiles;
			}
		}

		//Opens Save file dialog and outputs current queue as a project
		public void SaveAs()
		{
			SaveFileDialogParams saveParams = new SaveFileDialogParams("Save Project|*.zip");

			FileDialog.SaveFileDialog(saveParams, onSaveFileSelected);
		}

		private void onSaveFileSelected(SaveFileDialogParams saveParams)
		{
			if (!string.IsNullOrEmpty(saveParams.FileName))
			{
				ExportToProjectArchive(saveParams.FileName);
			}
		}

		private static string applicationDataPath = ApplicationDataStorage.ApplicationUserDataPath;
		private static string archiveStagingFolder = Path.Combine(applicationDataPath, "data", "temp", "project-assembly");
		private static string defaultManifestPathAndFileName = Path.Combine(archiveStagingFolder, "manifest.json");
		private static string defaultProjectPathAndFileName = Path.Combine(applicationDataPath, "data", "default.zip");

		public static void EmptyFolder(System.IO.DirectoryInfo directory)
		{
			foreach (System.IO.FileInfo file in directory.GetFiles()) file.Delete();
			foreach (System.IO.DirectoryInfo subDirectory in directory.GetDirectories()) subDirectory.Delete(true);
		}

		public void ExportToProjectArchive(string savedFileName = null)
		{
			if (savedFileName == null)
			{
				savedFileName = defaultProjectPathAndFileName;
			}

			//If the temp folder doesn't exist - create it, otherwise clear it
			if (!Directory.Exists(archiveStagingFolder))
			{
				Directory.CreateDirectory(archiveStagingFolder);
			}
			else
			{
				System.IO.DirectoryInfo directory = new System.IO.DirectoryInfo(@archiveStagingFolder);
				EmptyFolder(directory);
			}

			//Create and save the project manifest file into the temp directory
			File.WriteAllText(defaultManifestPathAndFileName, JsonConvert.SerializeObject(this.project, Newtonsoft.Json.Formatting.Indented));

			foreach (KeyValuePair<string, ManifestItem> item in this.sourceFiles)
			{
				CopyFileToTempFolder(item.Key, item.Value.FileName);
			}

			// Delete or move existing file out of the way as CreateFromDirectory will not overwrite and throws an exception
			if (File.Exists(savedFileName))
			{
				try
				{
					File.Delete(savedFileName);
				}
				catch (Exception ex)
				{
					string directory = Path.GetDirectoryName(savedFileName);
					string fileName = Path.GetFileNameWithoutExtension(savedFileName);
					string extension = Path.GetExtension(savedFileName);
					string candidatePath;

					for (int i = 1; i < 20; i++)
					{
						candidatePath = Path.Combine(directory, string.Format("{0}({1}){2}", fileName, i, extension));
						if (!File.Exists(candidatePath))
						{
							File.Move(savedFileName, candidatePath);
							break;
						}
					}
				}
			}

			ZipFile.CreateFromDirectory(archiveStagingFolder, savedFileName, CompressionLevel.Optimal, true);
		}

		private static void CopyFileToTempFolder(string sourceFile, string fileName)
		{
			if (File.Exists(sourceFile))
			{
				try
				{
					// Will not overwrite if the destination file already exists.
					File.Copy(sourceFile, Path.Combine(archiveStagingFolder, fileName));
				}

				// Catch exception if the file was already copied.
				catch (IOException copyError)
				{
					Console.WriteLine(copyError.Message);
				}
			}
		}

		public static List<PrintItem> ImportFromProjectArchive(string loadedFileName = null)
		{
			if (loadedFileName == null)
			{
				loadedFileName = defaultProjectPathAndFileName;
			}

			if (!System.IO.File.Exists(loadedFileName))
			{
				return null;
			}

			try
			{
				using (FileStream fs = File.OpenRead(loadedFileName))
				using (ZipArchive zip = new ZipArchive(fs))
				{
					int projectHashCode = zip.GetHashCode();

					//If the temp folder doesn't exist - create it, otherwise clear it
					string stagingFolder = Path.Combine(applicationDataPath, "data", "temp", "project-extract", projectHashCode.ToString());
					if (!Directory.Exists(stagingFolder))
					{
						Directory.CreateDirectory(stagingFolder);
					}
					else
					{
						System.IO.DirectoryInfo directory = new System.IO.DirectoryInfo(@stagingFolder);
						EmptyFolder(directory);
					}

					List<PrintItem> printItemList = new List<PrintItem>();
					Project projectManifest = null;

					foreach (ZipArchiveEntry zipEntry in zip.Entries)
					{
						string sourceExtension = Path.GetExtension(zipEntry.Name).ToUpper();

						// Note: directories have empty Name properties
						//
						// Only process ZipEntries that are:
						//    - not directories and
						//     - are in the ValidFileExtension list or
						//     - have a .GCODE extension or
						//     - are named manifest.json
						if (!string.IsNullOrWhiteSpace(zipEntry.Name) &&
							(zipEntry.Name == "manifest.json"
							|| MeshFileIo.ValidFileExtensions().Contains(sourceExtension)
							|| sourceExtension == ".GCODE"))
						{
							string extractedFileName = Path.Combine(stagingFolder, zipEntry.Name);

							string neededPathForZip = Path.GetDirectoryName(extractedFileName);
							if (!Directory.Exists(neededPathForZip))
							{
								Directory.CreateDirectory(neededPathForZip);
							}

							using (Stream zipStream = zipEntry.Open())
							using (FileStream streamWriter = File.Create(extractedFileName))
							{
								zipStream.CopyTo(streamWriter);
							}

							if (zipEntry.Name == "manifest.json")
							{
								using (StreamReader sr = new System.IO.StreamReader(extractedFileName))
								{
									projectManifest = (Project)Newtonsoft.Json.JsonConvert.DeserializeObject(sr.ReadToEnd(), typeof(Project));
								}
							}
						}
					}

					if (projectManifest != null)
					{
						foreach (ManifestItem item in projectManifest.ProjectFiles)
						{
							for (int i = 1; i <= item.ItemQuantity; i++)
							{
								printItemList.Add(new PrintItem()
								{
									FileLocation = Path.Combine(stagingFolder, item.FileName),
									Name = item.Name
								});
							}
						}
					}
					else
					{
						string[] files = Directory.GetFiles(stagingFolder, "*.*", SearchOption.AllDirectories);
						foreach (string fileName in files)
						{
							printItemList.Add(new PrintItem()
							{
								FileLocation = fileName,
								Name = Path.GetFileNameWithoutExtension(fileName)
							});
						}
					}

					return printItemList;
				}
			}
			catch
			{
				return null;
			}
		}
	}
}