/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;

namespace MatterHackers.MatterControl
{
	public static class CacheDirectory
	{
		public static void DeleteCacheData()
		{
			var basePath = ApplicationDataStorage.ApplicationUserDataPath;
			CleanDirectory(Path.Combine(basePath, "updates"), 30, new List<string>() { ".EXE" });

			CleanDirectory(Path.Combine(basePath, "data", "temp", "gcode"), 30, new List<string>() { ".GCODE", ".INI" });
			CleanDirectory(Path.Combine(basePath, "data", "gcode"), 30, new List<string>() { ".GCODE" });

			HashSet<string> filesToKeep = new HashSet<string>();

			// Get a list of all the stl and amf files referenced in the queue.
			foreach (PrintItemWrapper printItem in QueueData.Instance.PrintItems)
			{
				string fileLocation = printItem.FileLocation;
				if (!filesToKeep.Contains(fileLocation))
				{
					filesToKeep.Add(fileLocation);
				}
			}

			var allPrintItems = Datastore.Instance.dbSQLite.Query<PrintItem>("SELECT * FROM PrintItem;");

			// Add in all the stl and amf files referenced in the library.
			foreach (PrintItem printItem in allPrintItems)
			{
				if (!filesToKeep.Contains(printItem.FileLocation))
				{
					filesToKeep.Add(printItem.FileLocation);
				}
			}

			// If the count is less than 0 then we have never run and we need to populate the library and queue still. So don't delete anything yet.
			CleanDirectory(Path.Combine(basePath, "data", "temp", "amf_to_stl"), 1, new List<string>() { ".STL" }, filesToKeep);
		}

		private static HashSet<string> folderNamesToPreserve = new HashSet<string>()
		{
			"profiles",
		};

		private static int CleanDirectory(string path, int daysOldToDelete, List<string> extensionsToDelete, HashSet<string> filesToKeep = null)
		{
			int contentCount = 0;

			if (!Directory.Exists(path))
			{
				return 0;
			}

			foreach (string directory in Directory.EnumerateDirectories(path).ToArray())
			{
				int directoryContentCount = CleanDirectory(directory, daysOldToDelete, extensionsToDelete, filesToKeep);
				if (directoryContentCount == 0
					&& !folderNamesToPreserve.Contains(Path.GetFileName(directory)))
				{
					try
					{
						Directory.Delete(directory);
					}
					catch (Exception)
					{
						GuiWidget.BreakInDebugger();
					}
				}
				else
				{
					// it has a directory that has content
					contentCount++;
				}
			}

			foreach (string file in Directory.EnumerateFiles(path, "*.*"))
			{
				bool fileIsNew = new FileInfo(file).LastAccessTime > DateTime.Now.AddDays(-daysOldToDelete);
				bool forceKeep = filesToKeep != null && filesToKeep.Contains(file);

				if (fileIsNew
					|| forceKeep
					|| !extensionsToDelete.Contains(Path.GetExtension(file).ToUpper()))
				{
					contentCount++;
				}
				else
				{
					try
					{
						File.Delete(file);
					}
					catch (Exception)
					{
						GuiWidget.BreakInDebugger();
					}
				}
			}

			return contentCount;
		}
	}
}