/*
Copyright (c) 2014, Kevin Pope
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

using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using System.Collections.Generic;
using System.IO;

namespace MatterHackers.MatterControl.PrintHistory
{
	public class PrintHistoryData
	{
		private static PrintHistoryData instance;
		public bool ShowTimestamp;

		public RootedObjectEventHandler HistoryCleared = new RootedObjectEventHandler();

		public static PrintHistoryData Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new PrintHistoryData();
				}
				return instance;
			}
		}

		private List<string> GetLibraryParts()
		{
			List<string> libraryFilesToPreload = new List<string>();
			string setupSettingsPathAndFile = Path.Combine("OEMSettings", "PreloadedLibraryFiles.txt");
			if (StaticData.Instance.FileExists(setupSettingsPathAndFile))
			{
				try
				{
					foreach (string line in StaticData.Instance.ReadAllLines(setupSettingsPathAndFile))
					{
						//Ignore commented lines
						if (!line.StartsWith("#"))
						{
							string settingLine = line.Trim();
							libraryFilesToPreload.Add(settingLine);
						}
					}
				}
				catch
				{
				}
			}
			return libraryFilesToPreload;
		}

		public static readonly int RecordLimit = 20;

		public IEnumerable<DataStorage.PrintTask> GetHistoryItems(int recordCount)
		{
			string query;
			if (UserSettings.Instance.get("PrintHistoryFilterShowCompleted") == "true")
			{
				query = string.Format("SELECT * FROM PrintTask WHERE PrintComplete = 1 ORDER BY PrintStart DESC LIMIT {0};", recordCount);
			}
			else
			{
				query = string.Format("SELECT * FROM PrintTask ORDER BY PrintStart DESC LIMIT {0};", recordCount);
			}
			IEnumerable<DataStorage.PrintTask> result = (IEnumerable<DataStorage.PrintTask>)DataStorage.Datastore.Instance.dbSQLite.Query<DataStorage.PrintTask>(query);
			return result;
		}

		internal void ClearHistory()
		{
			string query = string.Format("DELETE FROM PrintTask;");
			DataStorage.Datastore.Instance.dbSQLite.ExecuteScalar<DataStorage.PrintTask>(query);
			HistoryCleared.CallEvents(this, null);
		}
	}
}