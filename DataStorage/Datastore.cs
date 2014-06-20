using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using MatterHackers.Agg.PlatformAbstract;

namespace MatterHackers.MatterControl.DataStorage
{
    public class ApplicationDataStorage
    {        
        //Describes the location for storing all local application data
        static ApplicationDataStorage globalInstance;
		string applicationPath;
        readonly string datastoreName = "MatterControl.db";
        readonly string applicationDataFolderName = "MatterControl";

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


        /// <summary>
        /// Returns the application user data folder
        /// </summary>
        /// <returns></returns>
        public string ApplicationUserDataPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), applicationDataFolderName);
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

        public string ApplicationStaticDataPath
        {
            get
            {
                switch (OsInformation.OperatingSystem)
                {
                    case OSType.Windows:
                        if (Directory.Exists("StaticData"))
                        {
                            return "StaticData";
                        }
                        else
                        {
                            return Path.Combine("..", "..", "StaticData");
                        }

                    case OSType.Mac:
                        if (Directory.Exists("StaticData"))
                        {
                            return "StaticData";
                        }
						else if(Directory.Exists(Path.Combine(ApplicationPath, "StaticData")))
						{
							return Path.Combine(ApplicationPath, "StaticData");
						}
						else
						{
							return Path.Combine("..", "..", "StaticData");
						}
                    case OSType.X11:
						if (Directory.Exists("StaticData"))
						{
							return "StaticData";
						}
						else
						{
							return Path.Combine("..", "..", "StaticData");
						}
						
                    default:
                        throw new NotImplementedException();
				}
            }
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


        /// <summary>
        /// Returns the path to the sqlite database
        /// </summary>
        /// <returns></returns>
        public string DatastorePath
        {
            get { return Path.Combine(ApplicationUserDataPath, datastoreName); }
        }

        public override string  ToString()
        {
 	         return base.ToString();
        }
    }
    
    class Datastore
    {
        bool TEST_FLAG = false;  
        static Datastore globalInstance;     
        static string datastoreLocation = ApplicationDataStorage.Instance.DatastorePath;        
        public bool ConnectionError = false;
        List<Type> dataStoreTables = new List<Type> { typeof(PrintItemCollection), typeof(CustomCommands), typeof(SystemSetting), typeof(UserSetting), typeof(ApplicationSession), typeof(PrintItem), typeof(PrintTask), typeof(Printer), typeof(SliceSetting), typeof(SliceSettingsCollection) };
        ApplicationSession activeSession;
        public ISQLite dbSQLite;

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

        public Datastore() 
        {
            if (TEST_FLAG)
            {
                //In test mode - attempt to delete the database entirely
                if (File.Exists(datastoreLocation))
                {
                    try
                    {
                        File.Delete(datastoreLocation);
                    }
                    catch (IOException)
                    {
                    }
                }
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

            Debug.WriteLine(datastoreLocation);
        }

        public int RecordCount(string tableName)
        {            
            string query = string.Format("SELECT COUNT(*) FROM {0};", tableName);
            string result = Datastore.Instance.dbSQLite.ExecuteScalar<string>(query);
            return Convert.ToInt32(result);
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

        void StartSession()
        //Begins new application session record
        {
            activeSession = new ApplicationSession();
            dbSQLite.Insert(activeSession);
        }

        void ValidateSchema()
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

        public void Exit()
        {
            this.activeSession.SessionEnd = DateTime.Now;
            this.activeSession.Commit();
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
    }


}


