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
using System.Linq;
using MatterHackers.MatterControl.DataStorage;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl
{
	public class QueueData
    {
		private static QueueData instance;
		public static QueueData Instance
		{
			get
			{
				if (instance == null)
                {
					instance = new QueueData();
                }

				return instance;
			}
		}

		public int ItemCount
        {
			get
            {
				var queueDirectory = LegacyQueueFiles.QueueDirectory;
				try
				{
					return Directory.EnumerateFiles(queueDirectory).Count();
				}
				catch (DirectoryNotFoundException)
				{
					return 0;
				}
			}
		}

        public void AddItem(string filePath)
        {
			var queueDirectory = LegacyQueueFiles.QueueDirectory;
			Directory.CreateDirectory(queueDirectory);
			var destFile = Path.Combine(queueDirectory, Path.GetFileName(filePath));
			File.Copy(filePath, destFile, true);
		}

		public string GetFirstItem()
        {
			return new DirectoryInfo(LegacyQueueFiles.QueueDirectory).GetFiles().OrderBy(f => f.LastWriteTime).Select(f => f.FullName).FirstOrDefault();
        }

        public IEnumerable<string> GetItemNames()
        {
			return new DirectoryInfo(LegacyQueueFiles.QueueDirectory).GetFiles().Select(f => f.FullName);
		}
	}

	public class LegacyQueueFiles
	{
		public static string QueueDirectory => Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, "Queue");

		public List<PrintItem> ProjectFiles { get; set; }

		public static void ImportFromLegacy()
		{
			var legacyQueuePath = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "default.mcp");

			if (!File.Exists(legacyQueuePath))
			{
				// nothing to do
				return;
			}

			string json = File.ReadAllText(legacyQueuePath);

			LegacyQueueFiles newProject = JsonConvert.DeserializeObject<LegacyQueueFiles>(json);
			if (newProject.ProjectFiles.Count == 0)
            {
				return;
            }

			var queueDirectory = QueueDirectory;
			Directory.CreateDirectory(queueDirectory);
			foreach (var printItem in newProject.ProjectFiles)
            {
				var destFile = Path.Combine(queueDirectory, Path.ChangeExtension(printItem.Name, Path.GetExtension(printItem.FileLocation)));
				if (!File.Exists(destFile)
					&& File.Exists(printItem.FileLocation))
				{
					// copy the print item to the destination directory
					File.Copy(printItem.FileLocation, destFile, true);
				}
            }

			// and rename the .mcp file no that we have migrated it
			File.Move(legacyQueuePath, Path.ChangeExtension(legacyQueuePath, ".bak"));
		}
	}
}