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
using MatterHackers.MatterControl.DataStorage;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl
{
	internal class ManifestFile
	{
		private List<PrintItem> projectFiles;
		private string projectName = "Test Project";
		private string projectDateCreated;

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

	internal class ManifestFileHandler
	{
		private ManifestFile project;

		public ManifestFileHandler(List<PrintItem> projectFiles)
		{
			if (projectFiles != null)
			{
				project = new ManifestFile();
				project.ProjectFiles = projectFiles;
			}
		}

		private static string applicationDataPath = ApplicationDataStorage.ApplicationUserDataPath;
		private static string defaultPathAndFileName = Path.Combine(applicationDataPath , "data", "default.mcp");

		public void ExportToJson(string savedFileName = null)
		{
			if (savedFileName == null)
			{
				savedFileName = defaultPathAndFileName;
			}


			string jsonString = JsonConvert.SerializeObject(this.project, Newtonsoft.Json.Formatting.Indented);
            string pathToDataFolder = Path.Combine(applicationDataPath, "data");
            if (!Directory.Exists(pathToDataFolder))
			{
                Directory.CreateDirectory(pathToDataFolder);
			}

			File.WriteAllText(savedFileName, jsonString);
		}

		public List<PrintItem> ImportFromJson(string filePath = null)
		{
			if (filePath == null)
			{
				filePath = defaultPathAndFileName;
			}

			if (!System.IO.File.Exists(filePath))
			{
				return null;
			}

			string json = File.ReadAllText(filePath);

			ManifestFile newProject = JsonConvert.DeserializeObject<ManifestFile>(json);
			if (newProject == null)
			{
				return new List<PrintItem>();
			}

			return newProject.ProjectFiles;
		}

	}
}