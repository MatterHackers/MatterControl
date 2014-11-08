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
			string documentsPath = System.Environment.GetFolderPath (System.Environment.SpecialFolder.Personal);
			SaveFileDialogParams saveParams = new SaveFileDialogParams("Save Project|*.mcp", initialDirectory: documentsPath);

			FileDialog.SaveFileDialog(saveParams, onSaveFileSelected);
            
        }

		void onSaveFileSelected(SaveFileDialogParams saveParams)
		{
			if (saveParams.FileName != null)
			{
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

        public void OpenFromDialog()
        {
            OpenFileDialogParams openParams = new OpenFileDialogParams("Select a Project file|*.mcp");
			FileDialog.OpenFileDialog(openParams, onManifestFileLoad);
        }

		void onManifestFileLoad(OpenFileDialogParams openParams)
		{
			if (openParams.FileName != null)
			{
				string loadedFileName = openParams.FileName;
				List<PrintItem> printItems = ImportFromJson(loadedFileName);
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
