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
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

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
		private static string defaultPathAndFileName = applicationDataPath + "/data/default.mcp";

		public void ExportToJson(string savedFileName = null)
		{
			if (savedFileName == null)
			{
				savedFileName = defaultPathAndFileName;
			}

			//Modify PrintItem list for export
			//project.ProjectFiles = NewPrintItemListToExport(project.ProjectFiles); 


			string jsonString = JsonConvert.SerializeObject(this.project, Newtonsoft.Json.Formatting.Indented);
			if (!Directory.Exists(applicationDataPath + "/data/"))
			{
				Directory.CreateDirectory(applicationDataPath + "/data/");
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

			//newProject.ProjectFiles = NewPrintItemListForImport(newProject.ProjectFiles);

			return newProject.ProjectFiles;
		}

		public List<PrintItem> NewPrintItemListToExport(List<PrintItem> printItemList)
		{

			List<PrintItem> newPrintItemList = new List<PrintItem>();

			foreach (var printItem in printItemList)
			{

				string pathToRenameForExport = printItem.FileLocation;
				string partName = Path.GetFileName(pathToRenameForExport);
				string exportedFilePath = "[QueueItems]\\" + partName;

				PrintItem newPrintItem = new PrintItem();
				newPrintItem.DateAdded = printItem.DateAdded;
				newPrintItem.Name = printItem.Name;
				newPrintItem.PrintCount = printItem.PrintCount;
				newPrintItem.PrintItemCollectionID = printItem.PrintItemCollectionID;
				newPrintItem.ReadOnly = printItem.ReadOnly;
				newPrintItem.Protected = printItem.Protected;
				
				if(pathToRenameForExport.Contains("C:\\"))
				{

					newPrintItem.FileLocation = exportedFilePath;

				}
				else
				{

					newPrintItem.FileLocation = printItem.FileLocation;

				}
				newPrintItemList.Add(newPrintItem);
			}

			return newPrintItemList;

		}

		public List<PrintItem> NewPrintItemListForImport(List<PrintItem> printItemList)
		{

			List<PrintItem> newPrintItemList = new List<PrintItem>();
			
			foreach (var printItem in printItemList)
			{

				string userDataPath = MatterHackers.MatterControl.DataStorage.ApplicationDataStorage.ApplicationUserDataPath;
				string partName = Path.GetFileName(printItem.FileLocation);
				string pathToRenameForImport = Path.Combine(userDataPath, "data", "QueueItems");

				PrintItem newPrintItem = new PrintItem();
				newPrintItem.DateAdded = printItem.DateAdded;
				newPrintItem.Name = printItem.Name;
				newPrintItem.PrintCount = printItem.PrintCount;
				newPrintItem.PrintItemCollectionID = printItem.PrintItemCollectionID;
				newPrintItem.ReadOnly = printItem.ReadOnly;
				newPrintItem.Protected = printItem.Protected;

				if(printItem.FileLocation.Contains("[QueueItems]"))
				{
					newPrintItem.FileLocation = Path.Combine(pathToRenameForImport, partName);
					
				}
				else
				{
					newPrintItem.FileLocation = printItem.FileLocation;
				}

				newPrintItemList.Add(newPrintItem);
			}

			return newPrintItemList;

		}
	}
}