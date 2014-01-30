using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.Common;
using System.Diagnostics;
using System.ComponentModel;
using System.Reflection;
using System.Threading;

using MatterHackers.VectorMath;

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
					applicationPath = System.IO.Path.GetDirectoryName (System.Reflection.Assembly.GetExecutingAssembly ().Location);
				}
				return applicationPath;
			}

		}

        public string ApplicationStaticDataPath
        {
            get
            {
                switch (MatterHackers.Agg.UI.WindowsFormsAbstract.GetOSType())
                {
                    case Agg.UI.WindowsFormsAbstract.OSType.Windows:
                        if (Directory.Exists("StaticData"))
                        {
                            return "StaticData";
                        }
                        else
                        {
                            return Path.Combine("..", "..", "StaticData");
                        }

                    case Agg.UI.WindowsFormsAbstract.OSType.Mac:
                        if (Directory.Exists("StaticData"))
                        {
                            return "StaticData";
                        }
                        else
                        {
                            return Path.Combine(ApplicationPath, "StaticData");
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
            get { return System.IO.Path.Combine(ApplicationUserDataPath, datastoreName); }
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
            
            switch (MatterHackers.Agg.UI.WindowsFormsAbstract.GetOSType())
            {
                case Agg.UI.WindowsFormsAbstract.OSType.Windows:
                    dbSQLite = new SQLiteWin32.SQLiteConnection(datastoreLocation);
                    break;

                case Agg.UI.WindowsFormsAbstract.OSType.Mac:
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

    public class Entity 
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        protected bool isSaved;
        protected int hashCode = 0;
        public event PropertyChangedEventHandler PropertyChanged;
        IEnumerable<PropertyInfo> properties;


        public Entity()
        {
            isSaved = false;
        }

        private void TryHandleInsert()
        {
            try
            {
                Datastore.Instance.dbSQLite.Insert(this);
            }
            catch (Exception)
            {                
                this.TryHandleInsert();
            }
        }

        private void TryHandleUpdate()
        {
            try
            {
                Datastore.Instance.dbSQLite.Update(this);
            }
            catch (Exception)
            {
                this.TryHandleUpdate();
            }
        }
        
        public virtual void Commit()
        {   
            //Assumes that autoincremented ids start with 1
            if (this.Id == 0)
            {
                TryHandleInsert();
            }
            else
            {
                TryHandleUpdate();
            }
        }

        public void Delete()
        {
            Datastore.Instance.dbSQLite.Delete(this);
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            this.hashCode = 0;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        public override int GetHashCode()
        {
            StringBuilder bigStringForHashCode = new StringBuilder();

            if (this.hashCode == 0)
            {
                properties = this.GetType()
                        .GetProperties(BindingFlags.Instance |
                                      BindingFlags.Public)
                    // Ignore non-string properties
                        .Where(prop => prop.PropertyType == typeof(string) || prop.PropertyType == typeof(int))
                    // Ignore indexers
                        .Where(prop => prop.GetIndexParameters().Length == 0)
                    // Must be both readable and writable
                        .Where(prop => prop.CanWrite && prop.CanRead);

                foreach (PropertyInfo prop in properties)
                {
                    object objectHoldingValue = prop.GetValue(this, null);
                    if (objectHoldingValue != null)
                    {
                        string value = objectHoldingValue.ToString();
                        if (value != null)
                        {
                            bigStringForHashCode.Append(prop.Name);
                            bigStringForHashCode.Append(value);
                        }
                    }
                }
                this.hashCode = bigStringForHashCode.ToString().GetHashCode();
            }

            return this.hashCode;
        }
    }

    public class ApplicationSession : Entity
    {        
        public DateTime SessionStart { get; set; }
        public DateTime SessionEnd { get; set; }
        public int PrintCount { get; set; }

        public ApplicationSession()
            :base ()
        {
            SessionStart = DateTime.Now;
            PrintCount = 0;
        }
    }

    public class PrintItemCollection : Entity
    {
        public string Name { get; set; }
    }

    public class PrintItem : Entity
    {
        [Indexed]
        public int PrintItemCollectionID { get; set; }
        public string Name { get; set; }
        public string FileLocation { get; set; }
        public DateTime DateAdded { get; set; }
        public int PrintCount { get; set; }

        public PrintItem()
            : this("", "")
        {
        }

        public PrintItem(string name, string fileLocation)
        {
            this.Name = name;
            this.FileLocation = fileLocation;

            DateAdded = DateTime.Now;
            PrintCount = 0;
        }
    }

    public class SliceSettingsCollection : Entity
    {
        public string Name { get; set; }

        // and the list of IDs
    }

    public class SliceSetting : Entity
    {
        [Indexed]
        public int SettingsCollectionId { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }

        public SliceSetting()
            : base()
        {
        }
    }

    public class PrintTask : Entity
    {
        [Indexed]
        public int PrinterId { get; set; }
        [Indexed]
        public int PrintItemId { get; set; }
        public DateTime PrintStart { get; set; }
        public DateTime PrintEnd { get; set; }
        public bool PrintComplete { get; set; }

        public PrintTask()
            : base()
        {
            PrintStart = DateTime.Now;
        }
    }

    public class SystemSetting : Entity
    {
        [Indexed]
        public string Name { get; set; }
        public string Value { get; set; }
        public DateTime DateLastModified { get; set; }

        public override void Commit()
        {
            DateLastModified = DateTime.Now;
            base.Commit();
        }
    }

    public class UserSetting : Entity
    {
        [Indexed]
        public string Name { get; set; }
        public string Value { get; set; }
        public DateTime DateLastModified { get; set; }

        public override void Commit()
        {
            DateLastModified = DateTime.Now;
            base.Commit();
        }
    }

    public class CustomCommands : Entity
    {
        [Indexed]
        public int PrinterId { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public DateTime DateLastModified { get; set; }

        public override void Commit()
        {
            DateLastModified = DateTime.Now;
            base.Commit();
        }
    }

    public class PrinterFeaturs
    {
        Dictionary<string, string> features = new Dictionary<string, string>();
        public PrinterFeaturs(string features)
        {
            if (features != null)
            {
                string[] featuresArray = features.Split(',');
                for (int i = 0; i < features.Length / 2; i++)
                {
                    this.features.Add(featuresArray[i * 2], featuresArray[i * 2 + 1]);
                }
            }
        }

        public string GetFeatuersString()
        {
            StringBuilder output = new StringBuilder();

            bool first = true;
            foreach(KeyValuePair<string, string> feature in features)
            {
                if (!first)
                {
                    output.Append(",");
                }
                output.Append(feature.Key + "," + feature.Value);
                first = false;
            }

            return output.ToString();
        }

        public bool HasFan()
        {
            if(features.ContainsKey("HasFan"))
            {
                return features["HasFan"] == "true";
            }

            return true;
        }

        public bool HasSdCard()
        {
            if (features.ContainsKey("HasSdCard"))
            {
                return features["HasSdCard"] == "true";
            }

            return true;
        }

        public bool HasHeatedBed()
        {
            if (features.ContainsKey("HasHeatedBed"))
            {
                return features["HasHeatedBed"] == "true";
            }

            return true;
        }

        public int ExtruderCount()
        {
            if (features.ContainsKey("ExtruderCount"))
            {
                return int.Parse(features["ExtruderCount"]);
            }

            return 1;
        }
    }

    public class Printer : Entity
    {
        public int DefaultSettingsCollectionId { get; set; }
        public string Name { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public string ComPort { get; set; }
        public string BaudRate { get; set; }
        public bool AutoConnectFlag { get; set; } //Auto connect to printer (if available)
        public bool DoPrintLeveling { get; set; }
        
        // features
        public string _features { get; set; }

        public string ManualMovementSpeeds { get; set; } // stored x,y,z,e1,e2,e3,...

        public string CurrentSlicingEngine { get; set; }

        /// <summary>
        /// This stores the 3 bed probed positions as a string. 3 * (x, y, z) = 9 values.
        /// </summary>
        public string PrintLevelingProbePositions { get; set; }

        protected PrinterFeaturs printerFeatures;
        public PrinterFeaturs GetFeatures()
        {
            if (printerFeatures == null)
            {
                printerFeatures = new PrinterFeaturs(_features);
            }

            return printerFeatures;
        }

        public override void Commit()
        {
            if (printerFeatures != null)
            {
                _features = printerFeatures.GetFeatuersString();
            }

            base.Commit();
        }

        /// <summary>
        /// Gets the 9 {3 * (x, y, z)} positions that were probed during the print leveling setup.
        /// </summary>
        /// <returns></returns>
        public double[] GetPrintLevelingPositions()
        {
            double[] positions = new double[9];

            if (PrintLevelingProbePositions != null)
            {
                string[] lines = PrintLevelingProbePositions.Split(',');
                if (lines.Length == 9)
                {
                    for (int i = 0; i < 9; i++)
                    {
                        positions[i] = double.Parse(lines[i]);
                    }
                }
            }

            return positions;
        }

        public void SetPrintLevelingPositions(double[] printLevelingPositions3_xyz)
        {
            StringBuilder allValues = new StringBuilder();
            bool first = true;
            foreach(double position in printLevelingPositions3_xyz)
            {
                if(!first)
                {
                    allValues.Append(",");
                }
                allValues.Append(position);
                first = false;
            }

            PrintLevelingProbePositions = allValues.ToString();
        }

        public Printer()
            : base()
        { 
            this.Make = "Unknown";
            this.Model = "Unknown";
        }
    }
}


