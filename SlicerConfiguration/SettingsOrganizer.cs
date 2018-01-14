/*
Copyright (c) 2016, Kevin Pope, John Lewin
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
using MatterHackers.Agg.Platform;
using MatterHackers.Localizations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class QuickMenuNameValue
	{
		public string MenuName;
		public string Value;
	}

	public class SliceSettingData
	{
		[JsonConverter(typeof(StringEnumConverter))]
		public enum DataEditTypes { STRING, INT, INT_OR_MM, DOUBLE, POSITIVE_DOUBLE, OFFSET, DOUBLE_OR_PERCENT, VECTOR2, OFFSET2, CHECK_BOX, LIST, MULTI_LINE_TEXT, HARDWARE_PRESENT, COM_PORT, IP_LIST };

		public string SlicerConfigName { get; set; }

		public string PresentationName { get; set; }

		public string ShowIfSet { get; set; }

		public string EnableIfSet { get; set; }

		public string DefaultValue { get; set; }

		public DataEditTypes DataEditType { get; set; }

		public string HelpText { get; set; } = "";

		public string Units { get; set; } = "";

		public string ListValues { get; set; } = "";

		public bool ShowAsOverride { get; set; } = true;

		public List<QuickMenuNameValue> QuickMenuSettings = new List<QuickMenuNameValue>();

		public List<Dictionary<string, string>> SetSettingsOnChange = new List<Dictionary<string,string>>();

		public bool ResetAtEndOfPrint { get; set; } = false;

		public bool RebuildGCodeOnChange { get; set; } = true;

		public bool ReloadUiWhenChanged { get; set; } = false;

		public SettingsOrganizer.SubGroup OrganizerSubGroup { get; set; }

		public SliceSettingData(string slicerConfigName, string presentationName, DataEditTypes dataEditType, string helpText = "")
		{
			// During deserialization Json.net has to call this constructor but may fail to find the optional ExtraSettings
			// value. When this occurs, it passes null overriding the default empty string. To ensure empty string instead
			// of null, we conditionally reassign "" if null
			this.SlicerConfigName = slicerConfigName;
			this.PresentationName = presentationName;
			this.DataEditType = dataEditType;
			this.HelpText = helpText.Localize();
		}
	}

	public class SettingsOrganizer
	{
		public Dictionary<string, UserLevel> UserLevels { get; set; } = new Dictionary<string, UserLevel>();

		private static SettingsOrganizer instance = null;

		public static Dictionary<string, SliceSettingData> SettingsData { get; }

		static SettingsOrganizer()
		{
			string propertiesFileContents = AggContext.StaticData.ReadAllText(Path.Combine("SliceSettings", "Properties.json"));
			var propertiesJsonData = JsonConvert.DeserializeObject<List<SliceSettingData>>(propertiesFileContents);

			SettingsData = new Dictionary<string, SliceSettingData>();
			foreach (var settingsData in propertiesJsonData)
			{
				SettingsData.Add(settingsData.SlicerConfigName, settingsData);
			}
		}

		public static SettingsOrganizer Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new SettingsOrganizer();
				}

				return instance;
			}
		}

		private SettingsOrganizer()
		{
			LoadAndParseSettingsFiles();

#if false
            Categories.Add(CreatePrintSettings());

            SettingsCategory filamentSettingsCategory = new SettingsCategory("Filament Settings");
            Categories.Add(filamentSettingsCategory);

            SettingsCategory printerSettingsCategory = new SettingsCategory("Printer Settings");
            Categories.Add(printerSettingsCategory);
#endif
		}

		public bool Contains(string userLevelKey, string slicerConfigName)
		{
			if (this.UserLevels.TryGetValue(userLevelKey, out UserLevel userLevel))
			{
				return userLevel.ContainsKey(slicerConfigName);
			}

			return false;
		}

		public SliceSettingData GetSettingsData(string slicerConfigName)
		{
			if (SettingsOrganizer.SettingsData.TryGetValue(slicerConfigName, out SliceSettingData settingsData))
			{
				return settingsData;
			}

			return null;
		}

		private void LoadAndParseSettingsFiles()
		{
			UserLevel userLevelToAddTo = null;
			Category categoryToAddTo = null;
			Group groupToAddTo = null;
			SubGroup subGroupToAddTo = null;

			foreach (string line in AggContext.StaticData.ReadAllLines(Path.Combine("SliceSettings", "Layouts.txt")))
			{
				if (line.Length > 0)
				{
					string sanitizedLine = line.Replace('"', ' ').Trim();
					switch (CountLeadingSpaces(line))
					{
						case 0:
							userLevelToAddTo = new UserLevel(sanitizedLine);
							UserLevels.Add(sanitizedLine, userLevelToAddTo);
							break;

						case 2:
							categoryToAddTo = new Category(sanitizedLine, userLevelToAddTo);
							userLevelToAddTo.Categories.Add(categoryToAddTo);
							break;

						case 4:
							groupToAddTo = new Group(sanitizedLine, categoryToAddTo);
							categoryToAddTo.Groups.Add(groupToAddTo);
							break;

						case 6:
							subGroupToAddTo = new SubGroup(sanitizedLine, groupToAddTo);
							groupToAddTo.SubGroups.Add(subGroupToAddTo);
							break;

						case 8:
							SliceSettingData data = GetSettingsData(sanitizedLine);
							if (data != null)
							{
								subGroupToAddTo.Settings.Add(data);
								data.OrganizerSubGroup = subGroupToAddTo;
								userLevelToAddTo.AddSetting(data.SlicerConfigName, subGroupToAddTo);
							}

							break;

						default:
							throw new Exception("Bad file, too many spaces (must be 0, 2, 4 or 6).");
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

		public class UserLevel
		{
			private Dictionary<string, SubGroup> mappedSettings = new Dictionary<string, SubGroup>();

			public UserLevel(string userLevelName)
			{
				this.Name = userLevelName;
			}

			public string Name { get; set; }

			public List<Category> Categories = new List<Category>();

			internal void AddSetting(string slicerConfigName, SubGroup organizerSubGroup)
			{
				mappedSettings.Add(slicerConfigName, organizerSubGroup);
			}

			public bool ContainsKey(string settingsKey) => mappedSettings.ContainsKey(settingsKey);

			public SubGroup GetContainerForSetting(string slicerConfigName)
			{
				return mappedSettings[slicerConfigName];
			}
		}

		public class Category
		{
			public Category(string categoryName, UserLevel userLevel)
			{
				this.Name = categoryName;
				this.UserLevel = userLevel;
			}

			public string Name { get; set; }

			public List<Group> Groups { get; set; } = new List<Group>();

			private UserLevel UserLevel { get; }
		}

		public class Group
		{
			public Group(string displayName, Category organizerCategory)
			{
				this.Name = displayName;
				this.Category = organizerCategory;
			}

			public string Name { get; }

			public List<SubGroup> SubGroups { get; set; } = new List<SubGroup>();


			public Category Category { get; }
		}

		public class SubGroup
		{
			public SubGroup(string groupName, Group group)
			{
				this.Name = groupName;
				this.Group = group;
			}

			public string Name { get; }

			public List<SliceSettingData> Settings { get; private set; } = new List<SliceSettingData>();

			public Group Group { get; }
		}

	}
}