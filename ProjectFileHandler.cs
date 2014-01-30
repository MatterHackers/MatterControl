using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Xml;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

using Ionic.Zip;

using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;

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
            SaveFileDialogParams saveParams = new SaveFileDialogParams("Save Project|*.zip");

            System.IO.Stream streamToSaveTo = FileDialog.SaveFileDialog(ref saveParams);
            if (streamToSaveTo != null)
            {
                streamToSaveTo.Close();
                ExportToProjectArchive(saveParams.FileName);
            }
        }

        static string applicationDataPath = ApplicationDataStorage.Instance.ApplicationUserDataPath;
        static string defaultManifestPathAndFileName = System.IO.Path.Combine(applicationDataPath,"data", "temp", "project-assembly", "manifest.json");
        static string defaultProjectPathAndFileName = System.IO.Path.Combine(applicationDataPath,"data", "default.zip");

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
            
            ZipFile zip = new ZipFile();
            zip.AddFile(defaultManifestPathAndFileName).FileName = Path.GetFileName(defaultManifestPathAndFileName);
            {
                foreach (KeyValuePair<string, ManifestItem> item in this.sourceFiles)
                {
                    zip.AddFile(item.Key).FileName = Path.GetFileName(item.Key);
                }
            }
            zip.Save(savedFileName);            
        }

        public List<PrintItem> OpenFromDialog()
        {
            OpenFileDialogParams openParams = new OpenFileDialogParams("Zip file|*.zip");

            System.IO.Stream streamToLoadFrom = FileDialog.OpenFileDialog(ref openParams);
            if (streamToLoadFrom != null)
            {
                string loadedFileName = openParams.FileName;
                return ImportFromProjectArchive(loadedFileName);
            }
            else
            {
                return null;
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

                ZipFile zip = ZipFile.Read(loadedFileName);
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

                foreach (ZipEntry e in zip)
                {   
                    e.Extract(stagingFolder, ExtractExistingFileAction.OverwriteSilently);
                    if (e.FileName == "manifest.json")
                    {
                        e.Extract(stagingFolder, ExtractExistingFileAction.OverwriteSilently);
                        string extractedFileName = Path.Combine(stagingFolder, e.FileName);
                        StreamReader sr = new System.IO.StreamReader(extractedFileName);
                        projectManifest = (Project)Newtonsoft.Json.JsonConvert.DeserializeObject(sr.ReadToEnd(), typeof(Project));
                        sr.Close();
                    }
                    else if (System.IO.Path.GetExtension(e.FileName).ToUpper() == ".STL" || System.IO.Path.GetExtension(e.FileName).ToUpper() == ".GCODE")
                    {
                        e.Extract(stagingFolder, ExtractExistingFileAction.OverwriteSilently);
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
