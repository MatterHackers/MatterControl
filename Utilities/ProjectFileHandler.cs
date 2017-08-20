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
using System.IO;
using System.IO.Compression;
using System.Linq;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Platform;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.Library;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl
{
	internal class ManifestItem
	{
		public int ItemQuantity { get; set; }

		public string Name { get; set; }

		public string FileName { get; set; }

		internal ILibraryContentStream PrintItem { get; set; }
	}

	internal class Project
	{
		public List<ManifestItem> ProjectFiles { get; set; }

		public string ProjectName { get; set; } = "Test Project";

		public string ProjectDateCreated { get; set; } = DateTime.Now.ToString("s");
	}

	internal class ProjectFileHandler
	{
		private Project project;
		private Dictionary<string, ManifestItem> sourceFiles = new Dictionary<string, ManifestItem>();
		private HashSet<string> addedFileNames = new HashSet<string>();

		public ProjectFileHandler(IEnumerable<ILibraryContentStream> sourceItems)
		{
			if (sourceItems != null)
			{
				project = new Project();

				foreach (var item in sourceItems)
				{
					if (sourceFiles.ContainsKey(item.ID))
					{
						sourceFiles[item.ID].ItemQuantity += 1;
					}
					else
					{
						if (addedFileNames.Contains(item.ID))
						{
							StyledMessageBox.ShowMessageBox(
								null, 
								string.Format("Duplicate file name found but in a different folder '{0}'. This part will not be added to the collection.\n\n{1}", item.Name, item.ID), 
								"Duplicate File");
							continue;
						}

						addedFileNames.Add(item.ID);

						var manifestItem = new ManifestItem()
						{
							ItemQuantity = 1,
							Name = item.Name,
							FileName = item.Name,
							PrintItem = item
						};
						sourceFiles.Add(item.ID, manifestItem);
					}
				}

				project.ProjectFiles = sourceFiles.Values.ToList();
			}
		}

		//Opens Save file dialog and outputs current queue as a project
		public void SaveAs()
		{
			AggContext.FileDialogs.SaveFileDialog(
				new SaveFileDialogParams("Save Project|*.zip"), 
				(saveParams) =>
				{
					if (!string.IsNullOrEmpty(saveParams.FileName))
					{
						ExportToProjectArchive(saveParams.FileName);
					}
				});
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

			foreach (var manifestItem in this.sourceFiles.Values)
			{
				CopyFileToTempFolder(manifestItem);
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

					for (int i = 1; i < 20; i++)
					{
						string candidatePath = Path.Combine(directory, $"{fileName}({i}){extension}");
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

		private static async void CopyFileToTempFolder(ManifestItem item)
		{
			try
			{
				var streamInterface = item.PrintItem as ILibraryContentStream;
				using (var streamAndLength = await streamInterface.GetContentStream(null))
				{
					using (var outputStream = File.OpenWrite(Path.Combine(archiveStagingFolder, item.FileName)))
					{
						streamAndLength.Stream.CopyTo(outputStream);
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
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
