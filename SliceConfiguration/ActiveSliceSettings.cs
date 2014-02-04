﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Text;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
    public class SettingsLayer
    {
        //Container class representing a collection of setting along with the meta info for that collection
        public Dictionary<string, DataStorage.SliceSetting> settingsDictionary;
        public DataStorage.SliceSettingsCollection settingsCollectionData;

        public SettingsLayer(DataStorage.SliceSettingsCollection settingsCollection, Dictionary<string, DataStorage.SliceSetting> settingsDictionary)
        {
            this.settingsCollectionData = settingsCollection;
            this.settingsDictionary = settingsDictionary;
        }
    }
    
    public class ActiveSliceSettings
    {
        static ActiveSliceSettings globalInstance = null;
        static string configFileExtension = "ini";
        private List<SettingsLayer> activeSettingsLayers;
        public RootedObjectEventHandler CommitStatusChanged = new RootedObjectEventHandler();
        public RootedObjectEventHandler SettingsChanged = new RootedObjectEventHandler();
        private int settingsHashCode;

        bool hasUncommittedChanges = false;

        public bool HasUncommittedChanges
        {
            get
            {
                return hasUncommittedChanges; 
            }
            set
            {
                if (this.hasUncommittedChanges != value)
                {
                    this.hasUncommittedChanges = value;
                    OnCommitStatusChanged();
                }
            }
        }

        void OnCommitStatusChanged()
        {
            CommitStatusChanged.CallEvents(this, null);
        }

        void OnSettingsChanged()
        {
            //Set hash code back to 0
            this.settingsHashCode = 0;

            SettingsChanged.CallEvents(this, null);
        }

        ActiveSliceSettings()
        {
        }

        public static ActiveSliceSettings Instance
        {
            get
            {
                if (globalInstance == null)
                {
                    globalInstance = new ActiveSliceSettings();
                    globalInstance.LoadSettingsForPrinter();
                }

                return globalInstance;
            }
        }

        public Vector2 GetPrintLevelSamplePosition(int index)
        {
            Vector2 bedSize = ActiveSliceSettings.Instance.BedSize;
            Vector2 printCenter = ActiveSliceSettings.Instance.PrintCenter;
            switch (index)
            {
                case 0:
                    return new Vector2(printCenter.x, printCenter.y + (bedSize.y / 2) * .8);
                case 1:
                    return new Vector2(printCenter.x - (bedSize.x / 2) * .8, printCenter.y - (bedSize.y / 2) * .8);
                case 2:
                    return new Vector2(printCenter.x + (bedSize.x / 2) * .8, printCenter.y - (bedSize.y / 2) * .8);
                default:
                    throw new IndexOutOfRangeException();
            }
        }

        public void LoadSettingsForPrinter()
        {
            this.activeSettingsLayers = new List<SettingsLayer>();

            //Load default settings from the .ini file as first layer
            LoadDefaultConfigrationSettings();

            //Load printer settings from database as second layer
            LoadPrinterConfigurationSettings();
#if false
            SetBedLevelEquation(0, 0, 0);
#else
            if (PrinterCommunication.Instance.ActivePrinter != null)
            {
                PrintLeveling.Instance.SetPrintLevelingEquation(
                    PrinterCommunication.Instance.GetPrintLevelingProbePosition(0),
                    PrinterCommunication.Instance.GetPrintLevelingProbePosition(1),
                    PrinterCommunication.Instance.GetPrintLevelingProbePosition(2),
                    ActiveSliceSettings.Instance.PrintCenter);
            }
#endif
            OnSettingsChanged();

            this.HasUncommittedChanges = false;
        }

        public Dictionary<string, DataStorage.SliceSetting> DefaultSettings
        {
            get
            {
                return activeSettingsLayers[0].settingsDictionary;
            }
        }

        public bool Contains(string sliceSetting)
        {
            //Check whether the default settings (layer 0) contain a settings definition
            return DefaultSettings.ContainsKey(sliceSetting);
        }

        public double MaxFanSpeed
        {
            get
            {
                return ParseDouble(GetActiveValue("max_fan_speed"));
            }
        }

        public double MinFanSpeed
        {
            get
            {
                return ParseDouble(GetActiveValue("min_fan_speed"));
            }
        }

        public double FirstLayerHeight
        {
            get 
            {
                string firstLayerValueString = GetActiveValue("first_layer_height");
                if (firstLayerValueString.Contains("%"))
                {
                    string onlyNumber = firstLayerValueString.Replace("%", "");
                    double ratio = ParseDouble(onlyNumber) / 100;
                    return LayerHeight * ratio;
                }
                double firstLayerValue;
                firstLayerValue = ParseDouble(firstLayerValueString);

                return firstLayerValue;
            }
        }

        private static double ParseDouble(string firstLayerValueString)
        {
            double firstLayerValue;
            if (!double.TryParse(firstLayerValueString, out firstLayerValue))
            {
                throw new Exception(string.Format("Format cannot be parsed. FirstLayerHeight '{0}'", firstLayerValueString));
            }
            return firstLayerValue;
        }

        public double LayerHeight
        {
            get { return ParseDouble(GetActiveValue("layer_height")); }
        }

        public Vector2 GetBedSize()
        {
            return BedSize;
        }
        public Vector2 BedSize
        {
            get
            {
                return GetActiveVector2("bed_size");
            }
        }

        public Vector2 GetBedCenter()
        {
            return BedCenter;
        }
        public Vector2 BedCenter
        {
            get
            {
                return GetActiveVector2("print_center");
            }
        }

        public double BuildHeight
        {
            get
            {
                return ParseDouble(GetActiveValue("build_height"));
            }
        }

        public Vector2 PrintCenter
        {
            get
            {
                return GetActiveVector2("print_center");
            }
        }

        public double NozzleDiameter
        {
            get { return ParseDouble(GetActiveValue("nozzle_diameter")); }
        }

        public double FillamentDiameter
        {
            get { return ParseDouble(GetActiveValue("filament_diameter")); }
        }

        ///<summary>
        ///Returns the settings value at the 'top' of the stack
        ///</summary>
        public string GetActiveValue(string sliceSetting)
        {   
            int numberOfActiveLayers = activeSettingsLayers.Count;

            //Go through settings layers one-by-one, in reverse order, until we find a layer that contains the value
            for (int i = numberOfActiveLayers - 1; i >= 0; i--)
            {
                if (activeSettingsLayers[i].settingsDictionary.ContainsKey(sliceSetting))
                {
                    return activeSettingsLayers[i].settingsDictionary[sliceSetting].Value;
                }
            }

            return "Unknown";
        }

        public Vector2 GetActiveVector2(string sliceSetting)
        {
            string[] twoValues = GetActiveValue(sliceSetting).Split(',');
            if (twoValues.Length != 2)
            {
                throw new Exception(string.Format("Not parsing {0} as a Vector2", sliceSetting));
            }
            Vector2 valueAsVector2 = new Vector2();
            valueAsVector2.x = ParseDouble(twoValues[0]);
            valueAsVector2.y = ParseDouble(twoValues[1]);
            return valueAsVector2;
        }

        public void LoadPrinterConfigurationSettings()
        {
            if (PrinterCommunication.Instance.ActivePrinter != null)
            {
                DataStorage.SliceSettingsCollection collection;
                if (PrinterCommunication.Instance.ActivePrinter.DefaultSettingsCollectionId != 0)
                {
                    int activePrinterSettingsID = PrinterCommunication.Instance.ActivePrinter.DefaultSettingsCollectionId;
                    collection = DataStorage.Datastore.Instance.dbSQLite.Table<DataStorage.SliceSettingsCollection>().Where(v => v.Id == activePrinterSettingsID).Take(1).FirstOrDefault();                    
                }
                else
                {
                    collection = new DataStorage.SliceSettingsCollection();
                    collection.Name = PrinterCommunication.Instance.ActivePrinter.Name;
                    collection.Commit();

                    PrinterCommunication.Instance.ActivePrinter.DefaultSettingsCollectionId = collection.Id;
                    PrinterCommunication.Instance.ActivePrinter.Commit();
                }
                SettingsLayer printerSettingsLayer = LoadConfigurationSettingsFromDatastore(collection);
                this.activeSettingsLayers.Add(printerSettingsLayer);
            }
        }

        private SettingsLayer LoadConfigurationSettingsFromDatastore(DataStorage.SliceSettingsCollection collection)
        {
            Dictionary<string, DataStorage.SliceSetting> settingsDictionary = new Dictionary<string, DataStorage.SliceSetting>();

            IEnumerable<DataStorage.SliceSetting> settingsList = GetCollectionSettings(collection.Id);
            foreach (DataStorage.SliceSetting s in settingsList)
            {
                settingsDictionary[s.Name] = s;
            }

            SettingsLayer settingsLayer = new SettingsLayer(collection, settingsDictionary);
            return settingsLayer;
        }

        IEnumerable<DataStorage.SliceSetting> GetCollectionSettings(int collectionId)
        {
            //Retrieve a list of saved printers from the Datastore
			string query = string.Format("SELECT * FROM SliceSetting WHERE SettingsCollectionID = {0};", collectionId);
			IEnumerable<DataStorage.SliceSetting> result = (IEnumerable<DataStorage.SliceSetting>)DataStorage.Datastore.Instance.dbSQLite.Query<DataStorage.SliceSetting>(query);
			return result;
        }

        private void LoadDefaultConfigrationSettings()
        {
            string slic3rDefaultConfigurationPathAndFile = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "PrinterSettings", "config.ini");
            DataStorage.SliceSettingsCollection defaultCollection = new DataStorage.SliceSettingsCollection();
            defaultCollection.Name = "__default__";
            SettingsLayer defaultSettingsLayer = LoadConfigurationSettingsFromFile(slic3rDefaultConfigurationPathAndFile, defaultCollection);
            this.activeSettingsLayers.Add(defaultSettingsLayer);
        }

        public bool LoadSettingsFromIni()
        {
            OpenFileDialogParams openParams = new OpenFileDialogParams("Load Slice Configuration|*." + configFileExtension);
			openParams.ActionButtonLabel = "Load Configuration";
			openParams.Title = "MatterControl: Select A File";

            FileDialog.OpenFileDialog(ref openParams);
			if (openParams.FileNames != null)
            {
                LoadConfigurationSettingsFromFileAsUnsaved(openParams.FileName);
                return true;
            }

            return false;
        }

        public SettingsLayer LoadConfigurationSettingsFromFile(string pathAndFileName, DataStorage.SliceSettingsCollection collection)
        {
            Dictionary<string, DataStorage.SliceSetting> settingsDictionary = new Dictionary<string, DataStorage.SliceSetting>();
            SettingsLayer activeCollection; 
            try
            {
                if (File.Exists(pathAndFileName))
                {
                    string[] lines = System.IO.File.ReadAllLines(pathAndFileName);
                    foreach (string line in lines)
                    {
                        //Ignore commented lines
                        if (!line.StartsWith("#"))
                        {
                            string[] settingLine = line.Split('=');
                            string keyName = settingLine[0].Trim();
                            string settingDefaultValue = settingLine[1].Trim();

                            DataStorage.SliceSetting sliceSetting = new DataStorage.SliceSetting();
                            sliceSetting.Name = keyName;
                            sliceSetting.Value = settingDefaultValue;

                            settingsDictionary.Add(keyName, sliceSetting);
                        }
                    }
                    activeCollection = new SettingsLayer(collection, settingsDictionary);
                    return activeCollection;
                }
                return null;
            }
            catch (Exception e)
            {
                Debug.WriteLine(string.Format("Error loading configuration: {0}", e));
                return null;
            }
        }

        public void LoadConfigurationSettingsFromFileAsUnsaved(string pathAndFileName)
        {
            try
            {
                if (File.Exists(pathAndFileName))
                {
                    string[] lines = System.IO.File.ReadAllLines(pathAndFileName);
                    foreach (string line in lines)
                    {
                        //Ignore commented lines
                        if (!line.StartsWith("#"))
                        {
                            string[] settingLine = line.Split('=');
                            string keyName = settingLine[0].Trim();
                            string settingDefaultValue = settingLine[1].Trim();

                            //Add the setting to the active layer
                            SaveValue(keyName, settingDefaultValue);

                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(string.Format("Error loading configuration: {0}", e));                
            }
        }


        public void SaveValue(string keyName, string keyValue, int layerIndex=1)
        {
            SettingsLayer layer = this.activeSettingsLayers[layerIndex];

            if (layer.settingsDictionary.ContainsKey(keyName)
                && layer.settingsDictionary[keyName].Value != keyValue)
            {
                layer.settingsDictionary[keyName].Value = keyValue;

                OnSettingsChanged();
                HasUncommittedChanges = true;
            }
            else
            {
                DataStorage.SliceSetting sliceSetting = new DataStorage.SliceSetting();
                sliceSetting.Name = keyName;
                sliceSetting.Value = keyValue;
                sliceSetting.SettingsCollectionId = layer.settingsCollectionData.Id;

                layer.settingsDictionary[keyName] = sliceSetting;

                OnSettingsChanged();
                HasUncommittedChanges = true;
            }
        }

        public void CommitChanges()
        {
            for (int i = 1; i < this.activeSettingsLayers.Count; i++)
            {
                CommitLayerChanges(i);
            }
            HasUncommittedChanges = false;
        }

        public void CommitLayerChanges(int layerIndex)
        {
            SettingsLayer layer = this.activeSettingsLayers[layerIndex];
            foreach (KeyValuePair<String, DataStorage.SliceSetting> item in layer.settingsDictionary)
            {
                item.Value.Commit();
            }
        }

        public void SaveAs()
        {
			string documentsPath = System.Environment.GetFolderPath (System.Environment.SpecialFolder.Personal);
			SaveFileDialogParams saveParams = new SaveFileDialogParams("Save Slice Configuration|*." + configFileExtension,documentsPath);

			System.IO.Stream streamToSaveTo = FileDialog.SaveFileDialog (ref saveParams);
            if (streamToSaveTo != null)
            {
                streamToSaveTo.Close();
                GenerateConfigFile(saveParams.FileName);
            }
        }

        public override int GetHashCode()
        {
            if (this.settingsHashCode == 0)
            {
                // make a new dictionary so we only hash on the current values and keys.
                StringBuilder bigStringForHashCode = new StringBuilder();
                foreach (KeyValuePair<String, DataStorage.SliceSetting> setting in this.DefaultSettings)
                {
                    string activeValue = GetActiveValue(setting.Key);
                    bigStringForHashCode.Append(setting.Key);
                    bigStringForHashCode.Append(activeValue);
                }
                this.settingsHashCode = bigStringForHashCode.ToString().GetHashCode();
            }
            return this.settingsHashCode;
        }

        public void GenerateConfigFile(string fileName)
        {
            List<string> configFileAsList = new List<string>();

            foreach (KeyValuePair<String, DataStorage.SliceSetting> setting in this.DefaultSettings)
            {
                string activeValue = GetActiveValue(setting.Key);
                string settingString = string.Format("{0} = {1}", setting.Key, activeValue);
                configFileAsList.Add(settingString);
            }
            string configFileAsString = string.Join("\n", configFileAsList.ToArray());

            FileStream fs = new FileStream(fileName, FileMode.Create);
            StreamWriter sw = new System.IO.StreamWriter(fs);
            sw.Write(configFileAsString);
            sw.Close();
        }

        public bool IsValid()
        {
            try
            {
                if (LayerHeight > NozzleDiameter)
                {
                    string error = "'Layer Height' must be less than or equal to the 'Nozzle Diameter'.";
                    string details = string.Format("Layer Height = {0}\nNozzle Diameter = {1}", LayerHeight, NozzleDiameter);
					string location = "Location: 'Advanced Controls' -> 'Slice Settings' -> 'Print' -> 'Layers/Perimeters'";
                    StyledMessageBox.ShowMessageBox(string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error");
                    return false;
                }
                else if (FirstLayerHeight > NozzleDiameter)
                {
                    string error = "First Layer Height' must be less than or equal to the 'Nozzle Diameter'.";
                    string details = string.Format("First Layer Height = {0}\nNozzle Diameter = {1}", FirstLayerHeight, NozzleDiameter);
                    string location = "Location: 'Advanced Controls' -> 'Slice Settings' -> 'Print' -> 'Layers/Perimeters'";
                    StyledMessageBox.ShowMessageBox(string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error");
                    return false;
                }

                if (MinFanSpeed > 100)
                {
                    string error = "The Min Fan Speed can only go as high as 100%.";
                    string details = string.Format("It is currently set to {0}.", MinFanSpeed);
                    string location = "Location: 'Advanced Controls' -> 'Slice Settings' -> 'Fillament' -> 'Cooling' (show all settings)";
                    StyledMessageBox.ShowMessageBox(string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error");
                    return false;
                }

                if (MaxFanSpeed > 100)
                {
                    string error = "The Max Fan Speed can only go as high as 100%.";
                    string details = string.Format("It is currently set to {0}.", MaxFanSpeed);
                    string location = "Location: 'Advanced Controls' -> 'Slice Settings' -> 'Fillament' -> 'Cooling' (show all settings)";
                    StyledMessageBox.ShowMessageBox(string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error");
                    return false;
                }
            }
            catch(Exception e)
            {
                string stackTraceNoBackslashRs = e.StackTrace.Replace("\r", "");
                ContactFormWindow.Open("Parse Error while slicing", e.Message + stackTraceNoBackslashRs);
                return false;
            }

            return true;
        }
    }
}
