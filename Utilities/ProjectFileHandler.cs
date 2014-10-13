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
using System.Collections;
using System.Linq;
using System.Text;
using System.Xml;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

using ICSharpCode.SharpZipLib.Zip;

using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Utilities;

namespace MatterHackers.MatterControl
{
    class ManifestItem
    {
        public int ItemQuantity { get; set; }
        public string Name { get; set; }
        public string FileName { get; set; }
    }
    
    class Project
    {
        List<ManifestItem> projectFiles;
        string projectName = "Test Project";        
        string projectDateCreated;

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
    
    
    class ProjectFileHandler
    {
        Project project;
        Dictionary<string, ManifestItem> sourceFiles = new Dictionary<string, ManifestItem>();
        HashSet<string> addedFileNames = new HashSet<string>();

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
                            StyledMessageBox.ShowMessageBox(string.Format("Duplicate file name found but in a different folder '{0}'. This part will not be added to the collection.\n\n{1}", fileNameOnly, item.FileLocation), "Duplicate File");
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
			string documentsPath = System.Environment.GetFolderPath (System.Environment.SpecialFolder.Personal);
			SaveFileDialogParams saveParams = new SaveFileDialogParams("Save Project|*.zip", initialDirectory: documentsPath);

			FileDialog.SaveFileDialog(saveParams, onSaveFileSelected);
        }

		void onSaveFileSelected(SaveFileDialogParams saveParams)
		{
			if (saveParams.FileName != null)
			{
				ExportToProjectArchive(saveParams.FileName);
			}
		}

        static string applicationDataPath = ApplicationDataStorage.Instance.ApplicationUserDataPath;
        static string defaultManifestPathAndFileName = Path.Combine(applicationDataPath, "data", "temp", "project-assembly", "manifest.json");
        static string defaultProjectPathAndFileName = Path.Combine(applicationDataPath, "data", "default.zip");

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
            string stagingFolder = Path.Combine(applicationDataPath, "data", "temp", "project-assembly");
            if (!Directory.Exists(stagingFolder))
            {
                Directory.CreateDirectory(stagingFolder);
            }
            else
            {
                System.IO.DirectoryInfo directory = new System.IO.DirectoryInfo(@stagingFolder);
                EmptyFolder(directory);
            }

            //Create and save the project manifest file into the temp directory
            string jsonString = JsonConvert.SerializeObject(this.project, Newtonsoft.Json.Formatting.Indented);
            FileStream fs = new FileStream(defaultManifestPathAndFileName, FileMode.Create);
            StreamWriter sw = new System.IO.StreamWriter(fs);
            sw.Write(jsonString);
            sw.Close();

            FileStream outputFileStream = File.Create(savedFileName);
            ZipOutputStream zipStream = new ZipOutputStream(outputFileStream);
            zipStream.SetLevel(3);
            CopyFileToZip(zipStream, defaultManifestPathAndFileName);
            {
                foreach (KeyValuePair<string, ManifestItem> item in this.sourceFiles)
                {
                    CopyFileToZip(zipStream, item.Key);
                }
            }
            zipStream.IsStreamOwner = true; // Makes the Close also Close the underlying stream
            zipStream.Close();
        }

        private static void CopyFileToZip(ZipOutputStream zipStream, string sourceFile)
        {
            if (File.Exists(sourceFile))
            {
                ZipEntry newEntry = new ZipEntry(Path.GetFileName(sourceFile));
                FileInfo fi = new FileInfo(sourceFile);
                newEntry.DateTime = fi.LastWriteTime;
                newEntry.Size = fi.Length;
                zipStream.PutNextEntry(newEntry);
                using (FileStream streamReader = File.OpenRead(sourceFile))
                {
                    CopyStream(streamReader, zipStream);
                }
                zipStream.CloseEntry();
            }
        }

        public static void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[4096];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
            }
        }

		public void OpenFromDialog()
        {
            OpenFileDialogParams openParams = new OpenFileDialogParams("Zip file|*.zip");
			FileDialog.OpenFileDialog(openParams, onProjectArchiveLoad);
        }

		void onProjectArchiveLoad(OpenFileDialogParams openParams)
		{
			List<PrintItem> partFiles;
			if (openParams.FileNames != null)
			{
				string loadedFileName = openParams.FileName;
				partFiles =  ImportFromProjectArchive(loadedFileName);
				if (partFiles != null)
				{                
					foreach (PrintItem part in partFiles)
					{
						QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(part.Name, part.FileLocation)));
					}
				}
			}
		}

        public List<PrintItem> ImportFromProjectArchive(string loadedFileName = null)
        {
            if (loadedFileName == null)
            {
                loadedFileName = defaultProjectPathAndFileName;
            }

            if (System.IO.File.Exists(loadedFileName))
            {
                FileStream fs = File.OpenRead(loadedFileName);
                ZipFile zip = new ZipFile(fs);
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

                foreach (ZipEntry zipEntry in zip)
                {
                    if (!zipEntry.IsFile)
                    {
                        continue;           // Ignore directories
                    }

                    if (zipEntry.Name == "manifest.json"
                        || Path.GetExtension(zipEntry.Name).ToUpper() == ".STL"
                        || Path.GetExtension(zipEntry.Name).ToUpper() == ".GCODE")
                    {
                        string extractedFileName = Path.Combine(stagingFolder, zipEntry.Name);

                        string neededPathForZip = Path.GetDirectoryName(extractedFileName);
                        if (!Directory.Exists(neededPathForZip))
                        {
                            Directory.CreateDirectory(neededPathForZip);
                        }

                        Stream zipStream = zip.GetInputStream(zipEntry);
                        using (FileStream streamWriter = File.Create(extractedFileName))
                        {
                            CopyStream(zipStream, streamWriter);
                        }

                        if (zipEntry.Name == "manifest.json")
                        {
                            StreamReader sr = new System.IO.StreamReader(extractedFileName);
                            projectManifest = (Project)Newtonsoft.Json.JsonConvert.DeserializeObject(sr.ReadToEnd(), typeof(Project));
                            sr.Close();
                        }
                    }
                }

                if (projectManifest != null)
                {
                    foreach (ManifestItem item in projectManifest.ProjectFiles)
                    {
                        for (int i = 1; i <= item.ItemQuantity; i++)
                        {
                            printItemList.Add(this.GetPrintItemFromFile(Path.Combine(stagingFolder, item.FileName), item.Name));
                        }
                    }
                }
                else
                {
                    string[] files = Directory.GetFiles(stagingFolder,"*.*", SearchOption.AllDirectories);
                    foreach(string fileName in files)
                    {
                        printItemList.Add(this.GetPrintItemFromFile(fileName, Path.GetFileNameWithoutExtension(fileName)));
                    }
                }
                
                return printItemList;
            }
            else
            {
                return null;
            }
        }

        private PrintItem GetPrintItemFromFile(string fileName, string displayName)
        {
            PrintItem item = new PrintItem();
            item.FileLocation = fileName;
            item.Name = displayName;
            return item;
        }
    }
}
