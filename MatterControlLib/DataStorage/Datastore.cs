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
using System.Threading;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.DataStorage
{
	public class Datastore
	{
		bool wasExited = false;
		public bool ConnectionError = false;
		public ISQLite dbSQLite;
		private string datastoreLocation = ApplicationDataStorage.Instance.DatastorePath;
		private static Datastore globalInstance;
		private ApplicationSession activeSession;

		private List<Type> dataStoreTables = new List<Type>
		{
			typeof(PrintItemCollection),
			typeof(PrinterSetting),
			typeof(CustomCommands),
			typeof(SystemSetting),
			typeof(UserSetting),
			typeof(ApplicationSession),
			typeof(PrintItem),
			typeof(PrintTask),
			typeof(Printer),
			typeof(SliceSetting),
			typeof(SliceSettingsCollection)
		};

		public Datastore()
		{
			if (!File.Exists(datastoreLocation))
			{
				ApplicationDataStorage.Instance.FirstRun = true;
			}

			OSType osType = AggContext.OperatingSystem;
			switch (osType)
			{
				case OSType.Windows:
					dbSQLite = new SQLiteWin32.SQLiteConnection(datastoreLocation);
					break;

				case OSType.Mac:
					dbSQLite = new SQLiteUnix.SQLiteConnection(datastoreLocation);
					break;

				case OSType.X11:
					dbSQLite = new SQLiteUnix.SQLiteConnection(datastoreLocation);
					break;

				case OSType.Android:
					dbSQLite = new SQLiteAndroid.SQLiteConnection(datastoreLocation);
					break;

				default:
					throw new NotImplementedException();
			}
		}

		public static Datastore Instance
		{
			get
			{
				if (globalInstance == null)
				{
					globalInstance = new Datastore();
				}
				return globalInstance;
			}

			// Special case to allow tests to set custom application paths 
			internal set
			{
				globalInstance = value;
			}
		}
		public void Exit()
		{
			if (wasExited)
			{
				return;
			}

			wasExited = true;

			if (this.activeSession != null)
			{
				this.activeSession.SessionEnd = DateTime.Now;
				this.activeSession.Commit();
			}

			// lets wait a bit to make sure the commit has resolved.
			Thread.Sleep(100);
			try
			{
				dbSQLite.Close();
			}
			catch (Exception)
			{
				GuiWidget.BreakInDebugger();
				// we failed to close so lets wait a bit and try again
				Thread.Sleep(1000);
				try
				{
					dbSQLite.Close();
				}
				catch (Exception)
				{
					GuiWidget.BreakInDebugger();
				}
			}
		}

		//Run initial checks and operations on sqlite datastore
		public void Initialize()
		{
			ValidateSchema();

			// Construct the root library collection if missing
			var rootLibraryCollection = Datastore.Instance.dbSQLite.Table<PrintItemCollection>().Where(v => v.Name == "_library").Take(1).FirstOrDefault();
			if (rootLibraryCollection == null)
			{
				rootLibraryCollection = new PrintItemCollection();
				rootLibraryCollection.Name = "_library";
				rootLibraryCollection.Commit();
			}

			StartSession();
		}

		public int RecordCount(string tableName)
		{
			string query = string.Format("SELECT COUNT(*) FROM {0};", tableName);
			string result = Datastore.Instance.dbSQLite.ExecuteScalar<string>(query);

			return Convert.ToInt32(result);
		}

		//Begins new application session record
		private void StartSession()
		{
			activeSession = new ApplicationSession();
			dbSQLite.Insert(activeSession);
		}

		private void GenerateSampleData()
		{
			for (int index = 1; index <= 5; index++)
			{
				Printer printer = new Printer();
				printer.ComPort = string.Format("COM{0}", index);
				printer.BaudRate = "250000";
				printer.Name = string.Format("Printer {0}", index);
				Datastore.Instance.dbSQLite.Insert(printer);
			}
		}

		// Checks if the datastore contains the appropriate tables - adds them if necessary
		private void ValidateSchema()
		{
			foreach (Type table in dataStoreTables)
			{
				dbSQLite.CreateTable(table);
			}
		}

		public bool WasExited()
		{
			return Instance.wasExited;
		}
	}
}