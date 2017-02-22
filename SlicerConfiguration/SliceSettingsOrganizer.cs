﻿/*
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
using MatterHackers.Agg.PlatformAbstract;
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
		public enum DataEditTypes { STRING, INT, INT_OR_MM, DOUBLE, POSITIVE_DOUBLE, OFFSET, DOUBLE_OR_PERCENT, VECTOR2, OFFSET2, CHECK_BOX, LIST, MULTI_LINE_TEXT, HARDWARE_PRESENT, COM_PORT };

		public string SlicerConfigName { get; set; }

		public string PresentationName { get; set; }

		public string ShowIfSet { get; set; }

		public string DefaultValue { get; set; }

		public DataEditTypes DataEditType { get; set; }

		public string HelpText { get; set; } = "";

		public string ExtraSettings { get; set; } = "";

		public bool ShowAsOverride { get; set; } = true;

		public List<QuickMenuNameValue> QuickMenuSettings = new List<QuickMenuNameValue>();

		public List<Dictionary<string, string>> SetSettingsOnChange = new List<Dictionary<string,string>>();

		public bool ResetAtEndOfPrint { get; set; } = false;

		public bool RebuildGCodeOnChange { get; set; } = true;
		
		public bool ReloadUiWhenChanged { get; set; } = false;

		public SliceSettingData(string slicerConfigName, string presentationName, DataEditTypes dataEditType, string extraSettings = "", string helpText = "")
		{
			// During deserialization Json.net has to call this constructor but may fail to find the optional ExtraSettings
			// value. When this occurs, it passes null overriding the default empty string. To ensure empty string instead
			// of null, we conditionally reassign "" if null
			this.ExtraSettings = extraSettings ?? "";
			this.SlicerConfigName = slicerConfigName;
			this.PresentationName = presentationName;
			this.DataEditType = dataEditType;
			this.HelpText = LocalizedString.Get(helpText);
		}
	}

	public class OrganizerSubGroup
	{
		public string Name { get; }

		public List<SliceSettingData> SettingDataList { get; private set; } = new List<SliceSettingData>();

		public OrganizerSubGroup(string groupName)
		{
			this.Name = groupName;
		}
	}

	public class OrganizerGroup
	{
		public string Name { get; }

		public List<OrganizerSubGroup> SubGroupsList { get; set; } = new List<OrganizerSubGroup>();
		
		public OrganizerGroup(string displayName)
		{
			this.Name = displayName;
		}
	}

	public class OrganizerCategory
	{
		public string Name { get; set; }

		public List<OrganizerGroup> GroupsList { get; set; } = new List<OrganizerGroup>();

		public OrganizerCategory(string categoryName)
		{
			this.Name = categoryName;
		}
	}

	public class OrganizerUserLevel
	{
		public string Name { get; set; }

		public List<OrganizerCategory> CategoriesList = new List<OrganizerCategory>();

		public OrganizerUserLevel(string userLevelName)
		{
			this.Name = userLevelName;
		}
	}

	public class SliceSettingsOrganizer
	{
		private static Dictionary<string, string> defaultSettings = null;

		public Dictionary<string, OrganizerUserLevel> UserLevels { get; set; } = new Dictionary<string, OrganizerUserLevel>();

		private static SliceSettingsOrganizer instance = null;

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

		private SliceSettingsOrganizer()
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

		public bool Contains(string userLevel, string slicerConfigName)
		{
			foreach (OrganizerCategory category in UserLevels[userLevel].CategoriesList)
			{
				foreach (OrganizerGroup group in category.GroupsList)
				{
					foreach (OrganizerSubGroup subGroup in group.SubGroupsList)
					{
						foreach (SliceSettingData settingData in subGroup.SettingDataList)
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

		public SliceSettingData GetSettingsData(string slicerConfigName)
		{
			foreach (SliceSettingData settingData in ActiveSliceSettings.SettingsData)
			{
				if (settingData.SlicerConfigName == slicerConfigName)
				{
					return settingData;
				}
			}
			return null;
			//GD-Turning into non-fatal exception 12/12/14
			//throw new Exception("You must not have a layout for a setting that is not in the Properties.txt");
		}

		private void LoadAndParseSettingsFiles()
		{
			OrganizerUserLevel userLevelToAddTo = null;
			OrganizerCategory categoryToAddTo = null;
			OrganizerGroup groupToAddTo = null;
			OrganizerSubGroup subGroupToAddTo = null;

			foreach (string line in StaticData.Instance.ReadAllLines(Path.Combine("SliceSettings", "Layouts.txt")))
			{
				if (line.Length > 0)
				{
					string sanitizedLine = line.Replace('"', ' ').Trim();
					switch (CountLeadingSpaces(line))
					{
						case 0:
							userLevelToAddTo = new OrganizerUserLevel(sanitizedLine);
							UserLevels.Add(sanitizedLine, userLevelToAddTo);
							break;

						case 2:
							categoryToAddTo = new OrganizerCategory(sanitizedLine);
							userLevelToAddTo.CategoriesList.Add(categoryToAddTo);
							break;

						case 4:
							groupToAddTo = new OrganizerGroup(sanitizedLine);
							categoryToAddTo.GroupsList.Add(groupToAddTo);
							break;

						case 6:
							subGroupToAddTo = new OrganizerSubGroup(sanitizedLine);
							groupToAddTo.SubGroupsList.Add(subGroupToAddTo);
							break;

						case 8:
							SliceSettingData data = GetSettingsData(sanitizedLine);
							if (data != null)
							{
								subGroupToAddTo.SettingDataList.Add(data);
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

		public PrinterSettingsLayer GetDefaultSettings()
		{
			if (defaultSettings == null)
			{
				var settingsDictionary = new Dictionary<string, string>();
				foreach (var sliceSettingsData in ActiveSliceSettings.SettingsData)
				{
					settingsDictionary[sliceSettingsData.SlicerConfigName] = sliceSettingsData.DefaultValue;
				}

				defaultSettings = settingsDictionary;
			}

			return new PrinterSettingsLayer(defaultSettings);
		}
	}
}