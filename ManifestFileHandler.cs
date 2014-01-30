using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Xml;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Utilities;

namespace MatterHackers.MatterControl
{
    class ManifestFile
    {
        List<PrintItem> projectFiles;
        string projectName = "Test Project";
        string projectDateCreated;

        public ManifestFile()
        {
            DateTime now = DateTime.Now;
            projectDateCreated = now.ToString("s");
        }

        public List<PrintItem> ProjectFiles
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


    class ManifestFileHandler
    {
        ManifestFile project;

        public ManifestFileHandler(List<PrintItem> projectFiles)
        {
            if (projectFiles != null)
            {
                project = new ManifestFile();
                project.ProjectFiles = projectFiles;
            }
        }

        public void SaveAs()
        //Opens Save file dialog and outputs current queue as a project
        {
            SaveFileDialogParams saveParams = new SaveFileDialogParams("Save Project|*.mcp");

            System.IO.Stream streamToSaveTo = FileDialog.SaveFileDialog(ref saveParams);
            if (streamToSaveTo != null)
            {
                streamToSaveTo.Close();
                ExportToJson(saveParams.FileName);
            }
        }

        static string applicationDataPath = ApplicationDataStorage.Instance.ApplicationUserDataPath;
        static string defaultPathAndFileName = applicationDataPath + "/data/default.mcp";
        public void ExportToJson(string savedFileName = null)
        {
            if (savedFileName == null)
            {
                savedFileName = defaultPathAndFileName;
            }
            string jsonString = JsonConvert.SerializeObject(this.project, Newtonsoft.Json.Formatting.Indented);
            if (!Directory.Exists(applicationDataPath + "/data/"))
            {
                Directory.CreateDirectory(applicationDataPath + "/data/");
            }
            FileStream fs = new FileStream(savedFileName, FileMode.Create);
            StreamWriter sw = new System.IO.StreamWriter(fs);
            sw.Write(jsonString);
            sw.Close();
        }

        public List<PrintItem> OpenFromDialog()
        {
            OpenFileDialogParams openParams = new OpenFileDialogParams("Select a Project file|*.mcp");

            System.IO.Stream streamToLoadFrom = FileDialog.OpenFileDialog(ref openParams);
            if (streamToLoadFrom != null)
            {
                string loadedFileName = openParams.FileName;
                return ImportFromJson(loadedFileName);
            }
            else
            {
                return null;
            }
        }

        public List<PrintItem> ImportFromJson(string loadedFileName = null)
        {
            if (loadedFileName == null)
            {
                loadedFileName = defaultPathAndFileName;
            }

            if (System.IO.File.Exists(loadedFileName))
            {
                StreamReader sr = new System.IO.StreamReader(loadedFileName);
                ManifestFile newProject = (ManifestFile)Newtonsoft.Json.JsonConvert.DeserializeObject(sr.ReadToEnd(), typeof(ManifestFile));
                sr.Close();
                if (newProject == null)
                {
                    return new List<PrintItem>();
                }
                return newProject.ProjectFiles;
            }
            else
            {
                return null;
            }
        }
    }
}
