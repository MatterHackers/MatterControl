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
            : base()
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
        public string Tag { get; set; } //ex. 'material' or 'quality'
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
        public string PrintName { get; set; }
        public DateTime PrintStart { get; set; }
        public DateTime PrintEnd { get; set; }
        public int PrintTimeSeconds { get; set; }
        public bool PrintComplete { get; set; }

        public PrintTask()
            : base()
        {
            PrintStart = DateTime.Now;
        }

        public int PrintTimeMinutes
        {
            get
            {
                TimeSpan printTimeSpan = PrintEnd.Subtract(PrintStart);

                return (int)(printTimeSpan.TotalMinutes + .5);
            }
        }

        public override void Commit()
        {
            if (this.PrintEnd != DateTime.MinValue)
            {
                TimeSpan printTimeSpan = PrintEnd.Subtract(PrintStart);
                PrintTimeSeconds = (int)printTimeSpan.TotalSeconds;
            }
            base.Commit();
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

    public class PrinterFeatures
    {
        Dictionary<string, string> features = new Dictionary<string, string>();
        public PrinterFeatures(string features)
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
            foreach (KeyValuePair<string, string> feature in features)
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
            if (features.ContainsKey("HasFan"))
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

        public string MaterialCollectionIds { get; set; } // store id1,id2... (for N extruders)
        public int QualityCollectionId { get; set; }

        /// <summary>
        /// This stores the 3 bed probed positions as a string. 3 * (x, y, z) = 9 values.
        /// </summary>
        public string PrintLevelingProbePositions { get; set; }

        protected PrinterFeatures printerFeatures;
        public PrinterFeatures GetFeatures()
        {
            if (printerFeatures == null)
            {
                printerFeatures = new PrinterFeatures(_features);
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
        public double[] GetPrintLevelingMeasuredPositions()
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

        public void SetPrintLevelingMeasuredPositions(double[] printLevelingPositions3_xyz)
        {
            StringBuilder allValues = new StringBuilder();
            bool first = true;
            foreach (double position in printLevelingPositions3_xyz)
            {
                if (!first)
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
