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
using System;
using System.Collections.Generic;
using System.IO;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
    public class OrganizerSettingsData
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum DataEditTypes { STRING, INT, DOUBLE, POSITIVE_DOUBLE, OFFSET, DOUBLE_OR_PERCENT, VECTOR2, OFFSET2, CHECK_BOX, LIST, MULTI_LINE_TEXT };

        public string SlicerConfigName { get; set; }

        public string PresentationName { get; set; }

        public string HelpText { get; set; }

        public DataEditTypes DataEditType { get; set; }

        public string ExtraSettings { get; set; }

        static public OrganizerSettingsData NewOrganizerSettingData(string slicerConfigName, string presentationName, OrganizerSettingsData.DataEditTypes dataEditType, string extraSettings = "", string helpText = "")
        {
            return new OrganizerSettingsData(slicerConfigName, presentationName, dataEditType, extraSettings, helpText);
        }

        static public OrganizerSettingsData NewOrganizerSettingData(string lineFromSettingsFile)
        {
            string[] parameters = lineFromSettingsFile.Split('|');
            OrganizerSettingsData.DataEditTypes valueType = (OrganizerSettingsData.DataEditTypes)Enum.Parse(typeof(OrganizerSettingsData.DataEditTypes), parameters[2].Trim());
            switch (parameters.Length)
            {
                case 3:
                    return NewOrganizerSettingData(parameters[0].Trim(), parameters[1].Trim(), valueType);

                case 4:
                    return NewOrganizerSettingData(parameters[0].Trim(), parameters[1].Trim(), valueType, parameters[3].Trim());

                case 5:
                    return NewOrganizerSettingData(parameters[0].Trim(), parameters[1].Trim(), valueType, parameters[3].Trim(), parameters[4].Trim());

                default:
                    throw new Exception("Bad number of paramenters.");
            }
        }

        public OrganizerSettingsData(string slicerConfigName, string presentationName, DataEditTypes dataEditType, string extraSettings = "", string helpText = "")
        {
			this.ExtraSettings = extraSettings;
            this.SlicerConfigName = slicerConfigName;
            this.PresentationName = presentationName;
            this.DataEditType = dataEditType;
			this.HelpText = LocalizedString.Get(helpText);
        }
    }

    public class OrganizerSubGroup
    {
        string name;

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        List<OrganizerSettingsData> settingDataList = new List<OrganizerSettingsData>();
        public List<OrganizerSettingsData> SettingDataList
        {
            get { return settingDataList; }
            set { settingDataList = value; }
        }

        public OrganizerSubGroup(string groupName)
        {
			this.name = groupName;
        }
    }

    public class OrganizerGroup
    {
        private string groupName;
        public string Name
        {
            get { return groupName; }
        }

        List<OrganizerSubGroup> subGroupsList = new List<OrganizerSubGroup>();
        public List<OrganizerSubGroup> SubGroupsList
        {
            get { return subGroupsList; }
            set { subGroupsList = value; }
        }

        public OrganizerGroup(string displayName)
        {
            this.groupName = displayName;
        }

        internal OrganizerSubGroup NewAndAddSettingsSubGroup(string subGroupName)
        {
            OrganizerSubGroup newSettingsSubGroup = new OrganizerSubGroup(subGroupName);
            SubGroupsList.Add(newSettingsSubGroup);
            return newSettingsSubGroup;
        }
    }

    public class OrganizerCategory
    {
        public string Name { get; set; }
        List<OrganizerGroup> groupsList = new List<OrganizerGroup>();
        public List<OrganizerGroup> GroupsList
        {
            get { return groupsList; }
            set { groupsList = value; }
        }

        public OrganizerCategory(string categoryName)
        {
            Name = categoryName;
        }

        public OrganizerGroup NewAndAddSettingsGroup(string settingsGroupName)
        {
            OrganizerGroup newSettingsGroup = new OrganizerGroup(settingsGroupName);
            GroupsList.Add(newSettingsGroup);
            return newSettingsGroup;
        }
    }

    public class OrganizerUserLevel
    {
        public string Name { get; set; }
        List<OrganizerCategory> categoriesList = new List<OrganizerCategory>();
        public List<OrganizerCategory> CategoriesList
        {
            get { return categoriesList; }
            set { categoriesList = value; }
        }

        public OrganizerUserLevel(string userLevelName)
        {
            Name = userLevelName;
        }

        public OrganizerCategory NewAndAddSettingsGroup(string settingsGroupName)
        {
            OrganizerCategory newCategoriesGroup = new OrganizerCategory(settingsGroupName);
            CategoriesList.Add(newCategoriesGroup);
            return newCategoriesGroup;
        }
    }

    public class SliceSettingsOrganizer
    {
        Dictionary<string, OrganizerUserLevel> userLevels = new Dictionary<string, OrganizerUserLevel>();
        public Dictionary<string, OrganizerUserLevel> UserLevels
        {
            get { return userLevels; }
            set { userLevels = value; }
        }

        List<OrganizerSettingsData> settingsData = new List<OrganizerSettingsData>();
        public List<OrganizerSettingsData> SettingsData
        {
            get { return settingsData; }
            set { settingsData = value; }
        }

        static SliceSettingsOrganizer instance = null;
        public static SliceSettingsOrganizer Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new SliceSettingsOrganizer();
                }

                return instance;
            }
        }
		   
        SliceSettingsOrganizer()
        {
			string layouts = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "SliceSettings", "Layouts.txt");
            string properties = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "SliceSettings", "Properties.txt");

            LoadAndParseSettingsFiles(properties, layouts);
#if false
            Categories.Add(CreatePrintSettings());

            SettingsCategory filamentSettingsCategory = new SettingsCategory("Filament Settings");
            Categories.Add(filamentSettingsCategory);

            SettingsCategory printerSettingsCategory = new SettingsCategory("Printer Settings");
            Categories.Add(printerSettingsCategory);
#endif
        }

        public bool Contains(string userLevel, string slicerConfigName)
        {
            foreach (OrganizerCategory category in UserLevels[userLevel].CategoriesList)
            {
                foreach (OrganizerGroup group in category.GroupsList)
                {
                    foreach (OrganizerSubGroup subGroup in group.SubGroupsList)
                    {
                        foreach (OrganizerSettingsData settingData in subGroup.SettingDataList)
                        {
                            if (settingData.SlicerConfigName == slicerConfigName)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public OrganizerSettingsData GetSettingsData(string slicerConfigName)
        {
            foreach (OrganizerSettingsData settingData in SettingsData)
            {
                if (settingData.SlicerConfigName == slicerConfigName)
                {
                    return settingData;
                }
            }

            throw new Exception("You must not have a layout for a setting that is not in the Properties.txt");
        }

        public void ExportToJson(string savedFileName = null)
        {
            if (savedFileName == null)
            {
                savedFileName = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "ConfigSettingsMapping.json");
            }
            string jsonString = JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);

            FileStream fs = new FileStream(savedFileName, FileMode.Create);
            StreamWriter sw = new System.IO.StreamWriter(fs);
            sw.Write(jsonString);
            sw.Close();
        }

		void LoadAndParseSettingsFiles(string properties, string layout)
        {
            {
                string propertiesFileContents = "";
                using (FileStream fileStream = new FileStream(properties, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (StreamReader propertiesReader = new StreamReader(fileStream))
                    {
                        propertiesFileContents = propertiesReader.ReadToEnd();
                    }
                }

                string[] lines = propertiesFileContents.Split('\n');
                foreach (string line in lines)
                {
                    if (line.Trim().Length > 0)
                    {
                        settingsData.Add(OrganizerSettingsData.NewOrganizerSettingData(line));
                    }
                }
            }

            {
				string layoutFileContents = "";
				using (FileStream fileStream = new FileStream(layout, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (StreamReader layoutReader = new StreamReader(fileStream))
                    {
                        layoutFileContents = layoutReader.ReadToEnd();
                    }
				}

                OrganizerUserLevel userLevelToAddTo = null;
                OrganizerCategory categoryToAddTo = null;
                OrganizerGroup groupToAddTo = null;
                OrganizerSubGroup subGroupToAddTo = null;
                string[] lines = layoutFileContents.Split('\n');
                foreach (string line in lines)
                {
                    if (line.Length > 0)
                    {
                        switch (CountLeadingSpaces(line))
                        {
                            case 0:
                                string userLevelText = line.Replace('"', ' ').Trim();
                                userLevelToAddTo = new OrganizerUserLevel(userLevelText);
                                UserLevels.Add(userLevelText, userLevelToAddTo);
                                break;

                            case 2:
                                categoryToAddTo = new OrganizerCategory(line.Replace('"', ' ').Trim());
                                userLevelToAddTo.CategoriesList.Add(categoryToAddTo);
                                break;

                            case 4:
                                groupToAddTo = new OrganizerGroup(line.Replace('"', ' ').Trim());
                                categoryToAddTo.GroupsList.Add(groupToAddTo);
                                break;

                            case 6:
                                subGroupToAddTo = new OrganizerSubGroup(line.Replace('"', ' ').Trim());
                                groupToAddTo.SubGroupsList.Add(subGroupToAddTo);
                                break;

                            case 8:
                                subGroupToAddTo.SettingDataList.Add(GetSettingsData(line.Replace('"', ' ').Trim()));
                                break;

                            default:
                                throw new Exception("Bad file, too many spaces (must be 0, 2, 4 or 6).");
                        }
                    }
                }
            }
        }

        private static int CountLeadingSpaces(string line)
        {
            int numSpaces = 0;
            while (line[numSpaces] == ' ' && numSpaces < line.Length)
            {
                numSpaces++;
            }
            return numSpaces;
        }
    }
}
