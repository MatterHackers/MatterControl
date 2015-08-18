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

using MatterHackers.Agg.PlatformAbstract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace MatterHackers.MatterControl.DataStorage
{
	public class ApplicationDataStorage
	{
		public bool FirstRun = false;

		//Describes the location for storing all local application data
		private static ApplicationDataStorage globalInstance;
		private readonly string applicationDataFolderName = "MatterControl";
		private readonly string datastoreName = "MatterControl.db";
		private string applicationPath;
		public ApplicationDataStorage()
		//Constructor - validates that local storage folder exists, creates if necessary
		{
			DirectoryInfo dir = new DirectoryInfo(ApplicationUserDataPath);
			if (!dir.Exists)
			{
				dir.Create();
			}
		}

		/// <summary>
		/// Creates a global instance of ApplicationDataStorage
		/// </summary>
		public static ApplicationDataStorage Instance
		{
			get
			{
				if (globalInstance == null)
				{
					globalInstance = new ApplicationDataStorage();
				}
				return globalInstance;
			}
		}

		public string ApplicationLibraryDataPath
		{
			get
			{
				string libraryPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationUserDataPath, "Library");

				//Create library path if it doesn't exist
				DirectoryInfo dir = new DirectoryInfo(libraryPath);
				if (!dir.Exists)
				{
					dir.Create();
				}
				return libraryPath;
			}
		}

		public string ApplicationPath
		{
			get
			{
				if (this.applicationPath == null)
				{
					applicationPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
				}
				return applicationPath;
			}
		}

		/// <summary>
		/// Returns the application temp data folder
		/// </summary>
		/// <returns></returns>
		public string ApplicationTempDataPath
		{
			get
			{
				return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), applicationDataFolderName, "data", "temp");
			}
		}

		/// <summary>
		/// Returns the application user data folder
		/// </summary>
		/// <returns></returns>
		public string ApplicationUserDataPath
		{
			get
			{
				return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), applicationDataFolderName);
			}
		}
		/// <summary>
		/// Returns the path to the sqlite database
		/// </summary>
		/// <returns></returns>
		public string DatastorePath
		{
			get { return Path.Combine(ApplicationUserDataPath, datastoreName); }
		}

		/// <summary>
		/// Returns the gcode output folder
		/// </summary>
		/// <returns></returns>
		public string GCodeOutputPath
		{
			get
			{
				string gcodeOutputPath = Path.Combine(ApplicationUserDataPath, "data", "gcode");
				if (!Directory.Exists(gcodeOutputPath))
				{
					Directory.CreateDirectory(gcodeOutputPath);
				}
				return gcodeOutputPath;
			}
		}

#if __ANDROID__
		/// <summary>
		/// Returns the public storage folder (ex. download folder on Android)
		/// </summary>
		/// <returns></returns>
		public string PublicDataStoragePath
		{
			get
			{
				return Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).ToString();
			}
		}
#endif

		public override string ToString()
		{
			return base.ToString();
		}
	}

	public class Datastore
	{
		public bool ConnectionError = false;
		public ISQLite dbSQLite;
		private static string datastoreLocation = ApplicationDataStorage.Instance.DatastorePath;
		private static Datastore globalInstance;
		private ApplicationSession activeSession;
		private List<Type> dataStoreTables = new List<Type> { typeof(PrintItemCollection), typeof(PrinterSetting), typeof(CustomCommands), typeof(SystemSetting), typeof(UserSetting), typeof(ApplicationSession), typeof(PrintItem), typeof(PrintTask), typeof(Printer), typeof(SliceSetting), typeof(SliceSettingsCollection) };
		private bool TEST_FLAG = false;
		public Datastore()
		{
			if (!File.Exists(datastoreLocation))
			{
				ApplicationDataStorage.Instance.FirstRun = true;
			}

			OSType osType = OsInformation.OperatingSystem;
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

			if (TEST_FLAG)
			{
				//In test mode - attempt to drop all tables (in case db was locked when we tried to delete it)
				foreach (Type table in dataStoreTables)
				{
					try
					{
						this.dbSQLite.DropTable(table);
					}
					catch
					{
					}
				}
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
		}
		public void Exit()
		{
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
				// we faild to close so lets wait a bit and try again
				Thread.Sleep(1000);
				try
				{
					dbSQLite.Close();
				}
				catch (Exception)
				{
				}
			}
		}

		public void Initialize()
		//Run initial checks and operations on sqlite datastore
		{
			if (TEST_FLAG)
			{
				ValidateSchema();
				GenerateSampleData sampleData = new GenerateSampleData();
			}
			else
			{
				ValidateSchema();
			}
			StartSession();
		}

		public int RecordCount(string tableName)
		{
			string query = string.Format("SELECT COUNT(*) FROM {0};", tableName);
			string result = Datastore.Instance.dbSQLite.ExecuteScalar<string>(query);
			return Convert.ToInt32(result);
		}
		private void StartSession()
		//Begins new application session record
		{
			activeSession = new ApplicationSession();
			dbSQLite.Insert(activeSession);
		}

		private void ValidateSchema()
		// Checks if the datastore contains the appropriate tables - adds them if necessary
		{
			foreach (Type table in dataStoreTables)
			{
				dbSQLite.CreateTable(table);

				//string query = string.Format("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{0}';", table.Name);
				////SQLiteCommand command = dbSQLite.CreateCommand(query);

				//int RowCount = 0;
				//string result = dbSQLite.ExecuteScalar<string>(query);
				//RowCount = Convert.ToInt32(result);
				//if (RowCount == 0)
				//{
				//    dbSQLite.CreateTable(table);
				//}
			}
		}
	}
}