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
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.Agg.PlatformAbstract;

namespace MatterHackers.MatterControl.SlicerConfiguration
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
        static string configFileExtension = "slice";
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
			
        // private so that it can only be gotten through the Instance
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
                    globalInstance.LoadAllSettings();
                }

                return globalInstance;
            }
        }

        public void LoadAllSettings()
        {
            this.activeSettingsLayers = new List<SettingsLayer>();
            globalInstance.LoadSettingsForPrinter();
            
            //Ordering matters - Material presets trump Quality
            globalInstance.LoadSettingsForQuality();
            globalInstance.LoadSettingsForMaterial(1);

            if (ActivePrinterProfile.Instance.ActivePrinter != null)
            {
                PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(ActivePrinterProfile.Instance.ActivePrinter);

                PrintLevelingPlane.Instance.SetPrintLevelingEquation(
                    levelingData.sampledPosition0,
                    levelingData.sampledPosition1,
                    levelingData.sampledPosition2,
                    ActiveSliceSettings.Instance.PrintCenter);
            }
            OnSettingsChanged();

            this.HasUncommittedChanges = false;
        }

        public void LoadSettingsForMaterial(int extruderIndex)
        {
            if (ActivePrinterProfile.Instance.ActivePrinter != null)
            {
                SettingsLayer printerSettingsLayer;
                DataStorage.SliceSettingsCollection collection;
                if (ActivePrinterProfile.Instance.GetMaterialSetting(extruderIndex) != 0)
                {
                    int materialOneSettingsID = ActivePrinterProfile.Instance.GetMaterialSetting(extruderIndex);
                    collection = DataStorage.Datastore.Instance.dbSQLite.Table<DataStorage.SliceSettingsCollection>().Where(v => v.Id == materialOneSettingsID).Take(1).FirstOrDefault();
                    printerSettingsLayer = LoadConfigurationSettingsFromDatastore(collection);
                }
                else
                {
                    printerSettingsLayer = new SettingsLayer(new SliceSettingsCollection(),new Dictionary<string, SliceSetting>());
                }
                this.activeSettingsLayers.Add(printerSettingsLayer);
            }
        }

        public string GetMaterialValue(string sliceSetting, int extruderIndex)
        {
            int numberOfActiveLayers = activeSettingsLayers.Count;
            string settingValue = null;
            if (ActivePrinterProfile.Instance.GetMaterialSetting(extruderIndex) != 0)
            {
                int materialOneSettingsID = ActivePrinterProfile.Instance.GetMaterialSetting(extruderIndex);
                DataStorage.SliceSettingsCollection collection = DataStorage.Datastore.Instance.dbSQLite.Table<DataStorage.SliceSettingsCollection>().Where(v => v.Id == materialOneSettingsID).Take(1).FirstOrDefault();
                SettingsLayer printerSettingsLayer = LoadConfigurationSettingsFromDatastore(collection);
                if (printerSettingsLayer.settingsDictionary.ContainsKey(sliceSetting))
                {
                    settingValue = printerSettingsLayer.settingsDictionary[sliceSetting].Value;
                }
            }

            if (settingValue == null)
            {
                //Go through settings layers one-by-one, starting with quality (index = 2), in reverse order, until we find a layer that contains the value
                int startingLayer = Math.Min(numberOfActiveLayers - 1, 2);
                for (int i = startingLayer; i >= 0; i--)
                {
                    if (activeSettingsLayers[i].settingsDictionary.ContainsKey(sliceSetting))
                    {
                        settingValue = activeSettingsLayers[i].settingsDictionary[sliceSetting].Value;
                        return settingValue;
                    }
                }
            }

            if (settingValue == null)
            {
                settingValue = "Unknown";
            }

            return settingValue;
        }

        public void LoadSettingsForQuality()
        {
            if (ActivePrinterProfile.Instance.ActivePrinter != null)
            {
                SettingsLayer printerSettingsLayer;
                DataStorage.SliceSettingsCollection collection;
                if (ActivePrinterProfile.Instance.ActiveQualitySettingsID != 0)
                {
                    int materialOneSettingsID = ActivePrinterProfile.Instance.ActiveQualitySettingsID;
                    collection = DataStorage.Datastore.Instance.dbSQLite.Table<DataStorage.SliceSettingsCollection>().Where(v => v.Id == materialOneSettingsID).Take(1).FirstOrDefault();
                    printerSettingsLayer = LoadConfigurationSettingsFromDatastore(collection);                    
                }
                else
                {
                    printerSettingsLayer = new SettingsLayer(new SliceSettingsCollection(), new Dictionary<string, SliceSetting>());
                }
                this.activeSettingsLayers.Add(printerSettingsLayer);
            }           
        }

        public void LoadSettingsForPrinter()
        {
            //Load default settings from the .ini file as first layer
            LoadDefaultConfigrationSettings();

            //Load printer settings from database as second layer
            LoadPrinterConfigurationSettings();

        }

        public bool HasFan()
        {
            return GetActiveValue("has_fan") == "1";
        }

        public bool HasSdCardReader()
        {
            return GetActiveValue("has_sd_card_reader") == "1";
        }

		public bool IsMakerbotGCodeFlavor()
		{
			return GetActiveValue("gcode_flavor") == "makerbot";
		}

        public bool HasHeatedBed()
        {
            return GetActiveValue("has_heated_bed") == "1";
        }

        public bool SupportEnabled
        {
            get
            {
                return GetActiveValue("support_material") == "1";
            }
        }

        public int SupportExtruder
        {
            get
            {
                return int.Parse(GetActiveValue("support_material_extruder"));
            }
        }

        public bool RaftEnabled
        {
            get
            {
                return GetActiveValue("create_raft") == "1";
            }
        }

        public int RaftExtruder
        {
            get
            {
                return int.Parse(GetActiveValue("raft_extruder"));
            }
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

        public double FillDensity
        {
            get
            {
                string fillDensityValueString = GetActiveValue("fill_density");
                if (fillDensityValueString.Contains("%"))
                {
                    string onlyNumber = fillDensityValueString.Replace("%", "");
                    double ratio = ParseDouble(onlyNumber) / 100;
                    return ratio;
                }
                else
                {
                    return ParseDouble(GetActiveValue("fill_density"));
                }
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

        public double FirstLayerExtrusionWidth
        {
            get
            {
                string firstLayerValueString = GetActiveValue("first_layer_extrusion_width");
                if (firstLayerValueString.Contains("%"))
                {
                    string onlyNumber = firstLayerValueString.Replace("%", "");
                    double ratio = ParseDouble(onlyNumber) / 100;
                    return NozzleDiameter * ratio;
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

        public MeshVisualizer.MeshViewerWidget.BedShape BedShape
        {
            get
            {
                switch(GetActiveValue("bed_shape"))
                {
                    case "rectangular":
                        return MeshVisualizer.MeshViewerWidget.BedShape.Rectangular;

                    case "circular":
                        return MeshVisualizer.MeshViewerWidget.BedShape.Circular;

                    default:
#if DEBUG
                        throw new NotImplementedException(string.Format("'{0}' is not a known bed_shape.", GetActiveValue("bed_shape")));
#else
                        return MeshVisualizer.MeshViewerWidget.BedShape.Rectangular;
#endif
                }
            }
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

        public int ExtruderCount
        {
            get
            {
                return int.Parse(ActiveSliceSettings.Instance.GetActiveValue("extruder_count"));
            }
        }

        public bool ExtrudersShareTemperature 
        { 
            get
            {
                return (int.Parse(ActiveSliceSettings.Instance.GetActiveValue("extruders_share_temperature")) == 1);
            }
        }

        public Vector2 GetOffset(int extruderIndex)
        {
            string currentOffsets = ActiveSliceSettings.Instance.GetActiveValue("extruder_offset");
            string[] offsets = currentOffsets.Split(',');
            int count = 0;
            foreach (string offset in offsets)
            {
                if (count == extruderIndex)
                {
                    string[] xy = offset.Split('x');
                    return new Vector2(double.Parse(xy[0]), double.Parse(xy[1]));
                }
                count++;
            }

            return Vector2.Zero;
        }

        public double NozzleDiameter
        {
            get { return ParseDouble(GetActiveValue("nozzle_diameter")); }
        }

        public double FilamentDiameter
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

        /// <summary>
        /// Returns whether or not the setting is overridden by the active layer
        /// </summary>
        /// AbsoluteBaseSettings = 0, EditableBaseSettings = 1, PresetOverrides... 2-n (examples: QualitySettings = 2, MaterialSettings = 3) 
        /// <param name="sliceSetting"></param>
        /// <returns></returns>
        public bool SettingExistsInLayer(string sliceSetting, int layer)
        {
            bool settingExistsInLayer;
            int layerIndex = (int)layer;
            if (layerIndex < activeSettingsLayers.Count)
            {
                settingExistsInLayer = (activeSettingsLayers[layerIndex].settingsDictionary.ContainsKey(sliceSetting));
            }
            else
            {
                settingExistsInLayer = false;
            }
            return settingExistsInLayer;
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
            if (ActivePrinterProfile.Instance.ActivePrinter != null)
            {
                DataStorage.SliceSettingsCollection collection;
                if (ActivePrinterProfile.Instance.ActivePrinter.DefaultSettingsCollectionId != 0)
                {
                    int activePrinterSettingsID = ActivePrinterProfile.Instance.ActivePrinter.DefaultSettingsCollectionId;
                    collection = DataStorage.Datastore.Instance.dbSQLite.Table<DataStorage.SliceSettingsCollection>().Where(v => v.Id == activePrinterSettingsID).Take(1).FirstOrDefault();                    
                }
                else
                {
                    collection = new DataStorage.SliceSettingsCollection();
                    collection.Name = ActivePrinterProfile.Instance.ActivePrinter.Name;
                    collection.Commit();

                    ActivePrinterProfile.Instance.ActivePrinter.DefaultSettingsCollectionId = collection.Id;
                    ActivePrinterProfile.Instance.ActivePrinter.Commit();
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
            DataStorage.SliceSettingsCollection defaultCollection = new DataStorage.SliceSettingsCollection();
            defaultCollection.Name = "__default__";
            SettingsLayer defaultSettingsLayer = LoadConfigurationSettingsFromFile(Path.Combine("PrinterSettings", "config.ini"), defaultCollection);
            this.activeSettingsLayers.Add(defaultSettingsLayer);
        }

		public void LoadSettingsFromIni(object state)
        {
            OpenFileDialogParams openParams = new OpenFileDialogParams("Load Slice Configuration|*.slice;*.ini");
			openParams.ActionButtonLabel = "Load Configuration";
			openParams.Title = "MatterControl: Select A File";

			FileDialog.OpenFileDialog(openParams, onSettingsFileSelected);

        }

		void onSettingsFileSelected(OpenFileDialogParams openParams)
		{
			if (openParams.FileNames != null)
			{
				LoadConfigurationSettingsFromFileAsUnsaved(openParams.FileName);
				ApplicationController.Instance.ReloadAdvancedControlsPanel();
			}
		}

        public SettingsLayer LoadConfigurationSettingsFromFile(string pathAndFileName, DataStorage.SliceSettingsCollection collection)
        {
            Dictionary<string, DataStorage.SliceSetting> settingsDictionary = new Dictionary<string, DataStorage.SliceSetting>();
            SettingsLayer activeCollection; 
            try
            {
                if (StaticData.Instance.FileExists(pathAndFileName))
                {
                    foreach (string line in StaticData.Instance.ReadAllLines(pathAndFileName))
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
                        if (line.Trim() != "" && !line.StartsWith("#"))
                        {
                            string[] settingLine = line.Split('=');
                            if (settingLine.Length > 1)
                            {
                                string keyName = settingLine[0].Trim();
                                string settingDefaultValue = settingLine[1].Trim();

                                //Add the setting to the active layer
                                SaveValue(keyName, settingDefaultValue);
                            }
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
            SaveFileDialogParams saveParams = new SaveFileDialogParams("Save Slice Configuration|*." + configFileExtension);
			saveParams.FileName = "default_settings.ini";
			FileDialog.SaveFileDialog(saveParams, onExportFileSelected);
        }

		void onExportFileSelected(SaveFileDialogParams saveParams)
		{
			if (saveParams.FileName != null)
			{
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

			//To do - add file extension if it doesn't exist

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
                    string error = LocalizedString.Get("'Layer Height' must be less than or equal to the 'Nozzle Diameter'.");
                    string details = string.Format("Layer Height = {0}\nNozzle Diameter = {1}", LayerHeight, NozzleDiameter);
                    string location = LocalizedString.Get("Location: 'Advanced Controls' -> 'Slice Settings' -> 'Print' -> 'Layers/Perimeters'");
                    StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error");
                    return false;
                }
                else if (FirstLayerHeight > NozzleDiameter)
                {
                    string error = LocalizedString.Get("First Layer Height' must be less than or equal to the 'Nozzle Diameter'.");
                    string details = string.Format("First Layer Height = {0}\nNozzle Diameter = {1}", FirstLayerHeight, NozzleDiameter);
                    string location = "Location: 'Advanced Controls' -> 'Slice Settings' -> 'Print' -> 'Layers/Perimeters'";
                    StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error");
                    return false;
                }

                if (FirstLayerExtrusionWidth > NozzleDiameter * 4)
                {
                    string error = LocalizedString.Get("First Layer Extrusion Width' must be less than or equal to the 'Nozzle Diameter' * 4.");
                    string details = string.Format("First Layer Extrusion Width = {0}\nNozzle Diameter = {1}", GetActiveValue("first_layer_extrusion_width"), NozzleDiameter);
                    string location = "Location: 'Advanced Controls' -> 'Slice Settings' -> 'Print' -> 'Advanced' -> 'Frist Layer'";
                    StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error");
                    return false;
                }

                if (MinFanSpeed > 100)
                {
                    string error = "The Min Fan Speed can only go as high as 100%.";
                    string details = string.Format("It is currently set to {0}.", MinFanSpeed);
                    string location = "Location: 'Advanced Controls' -> 'Slice Settings' -> 'Filament' -> 'Cooling' (show all settings)";
                    StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error");
                    return false;
                }

                if (MaxFanSpeed > 100)
                {
                    string error = "The Max Fan Speed can only go as high as 100%.";
                    string details = string.Format("It is currently set to {0}.", MaxFanSpeed);
                    string location = "Location: 'Advanced Controls' -> 'Slice Settings' -> 'Filament' -> 'Cooling' (show all settings)";
                    StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error");
                    return false;
                }
                if (FillDensity < 0 || FillDensity > 1)
                {
                    string error = "The Fill Density must be between 0 and 1 inclusive.";
                    string details = string.Format("It is currently set to {0}.", FillDensity);
                    string location = "Location: 'Advanced Controls' -> 'Slice Settings' -> 'Print' -> 'Infill'";
                    StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error");
                    return false;
                }

                // If the given speed is part of the current slice engine then check that it is greater than 0.
                if (!ValidateGoodSpeedSettingGreaterThan0("bridge_speed")) return false;
                if (!ValidateGoodSpeedSettingGreaterThan0("external_perimeter_speed")) return false;
                if (!ValidateGoodSpeedSettingGreaterThan0("first_layer_speed")) return false;
                if (!ValidateGoodSpeedSettingGreaterThan0("gap_fill_speed")) return false;
                if (!ValidateGoodSpeedSettingGreaterThan0("infill_speed")) return false;
                if (!ValidateGoodSpeedSettingGreaterThan0("perimeter_speed")) return false;
                if (!ValidateGoodSpeedSettingGreaterThan0("retract_speed")) return false;
                if (!ValidateGoodSpeedSettingGreaterThan0("small_perimeter_speed")) return false;
                if (!ValidateGoodSpeedSettingGreaterThan0("solid_infill_speed")) return false;
                if (!ValidateGoodSpeedSettingGreaterThan0("support_material_speed")) return false;
                if (!ValidateGoodSpeedSettingGreaterThan0("top_solid_infill_speed")) return false;
                if (!ValidateGoodSpeedSettingGreaterThan0("travel_speed")) return false;                
            }
            catch(Exception e)
            {
                string stackTraceNoBackslashRs = e.StackTrace.Replace("\r", "");
                ContactFormWindow.Open("Parse Error while slicing", e.Message + stackTraceNoBackslashRs);
                return false;
            }

            return true;
        }

        private bool ValidateGoodSpeedSettingGreaterThan0(string speedSetting)
        {
            string actualSpeedValueString = GetActiveValue(speedSetting);
            string speedValueString = actualSpeedValueString;
            if (speedValueString.EndsWith("%"))
            {
                speedValueString = speedValueString.Substring(0, speedValueString.Length - 1);
            }
            bool valueWasNumber = true;
            double speedToCheck;
            if (!double.TryParse(speedValueString, out speedToCheck))
            {
                valueWasNumber = false;
            }

            if (!valueWasNumber 
                || (ActivePrinterProfile.Instance.ActiveSliceEngine.MapContains(speedSetting)
                && speedToCheck <= 0))
            {
                string error = string.Format("The '{0}' must be greater than 0.", SliceSettingsOrganizer.Instance.GetSettingsData(speedSetting).PresentationName);
                string details = string.Format("It is currently set to {0}.", actualSpeedValueString);
                string location = "Location: 'Advanced Controls' -> 'Slice Settings' -> 'Print' -> 'Speed'";
                StyledMessageBox.ShowMessageBox(null, string.Format("{0}\n\n{1}\n\n{2}", error, details, location), "Slice Error");
                return false;
            }

            return true;
        }
    }
}
