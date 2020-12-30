/*
Copyright (c) 2019, Kevin Pope, John Lewin
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

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SettingsLayout
	{
		public SettingsSection[] SlicingSections { get; } =
			new[]
			{
				new SettingsSection("Slice Simple"),
				new SettingsSection("Slice Intermediate"),
				new SettingsSection("Slice Advanced")
			};

		public SettingsSection[] PrinterSections { get; } =
			new[]
			{
				new SettingsSection("Printer Simple"),
				new SettingsSection("Printer Intermediate"),
				new SettingsSection("Printer Advanced")
			};
		
		public SettingsSection AllSliceSettings => SlicingSections[2];
		
		public SettingsSection AllPrinterSettings => PrinterSections[2];

		internal SettingsLayout()
		{
			// slice settings
			CreateLayout(SlicingSections[0], SliceSettingsLayouts.SliceSettings(), (setting) =>
			{
				if (PrinterSettings.SettingsData.TryGetValue(setting, out SliceSettingData data))
				{
					return data.ReqiredDisplayDetail == SliceSettingData.DisplayDetailRequired.Simple;
				}

				return false;
			});
			CreateLayout(SlicingSections[1], SliceSettingsLayouts.SliceSettings(), (setting) =>
			{
				if (PrinterSettings.SettingsData.TryGetValue(setting, out SliceSettingData data))
				{
					return data.ReqiredDisplayDetail != SliceSettingData.DisplayDetailRequired.Advanced;
				}

				return false;
			});
			CreateLayout(SlicingSections[2], SliceSettingsLayouts.SliceSettings());

			// printer settings
			CreateLayout(PrinterSections[0], SliceSettingsLayouts.PrinterSettings(), (setting) =>
			{
				if (PrinterSettings.SettingsData.TryGetValue(setting, out SliceSettingData data))
				{
					return data.ReqiredDisplayDetail == SliceSettingData.DisplayDetailRequired.Simple;
				}

				return false;
			});
			CreateLayout(PrinterSections[1], SliceSettingsLayouts.PrinterSettings(), (setting) =>
			{
				if (PrinterSettings.SettingsData.TryGetValue(setting, out SliceSettingData data))
				{
					return data.ReqiredDisplayDetail != SliceSettingData.DisplayDetailRequired.Advanced;
				}

				return false;
			});

			CreateLayout(PrinterSections[2], SliceSettingsLayouts.PrinterSettings());
		}

		private void CreateLayout(SettingsSection section, (string categoryName, (string groupName, string[] settings)[] groups)[] layout, Func<string, bool> includeSetting = null)
		{
			foreach (var (categoryName, groups) in layout)
			{
				var categoryToAddTo = new Category(categoryName, section);
				section.Categories.Add(categoryToAddTo);

				foreach (var (groupName, settings) in groups)
				{
					var groupToAddTo = new Group(groupName, categoryToAddTo);
					categoryToAddTo.Groups.Add(groupToAddTo);

					foreach (var setting in settings)
					{
						if (PrinterSettings.SettingsData.TryGetValue(setting, out SliceSettingData data)
							&& includeSetting?.Invoke(setting) != false)
						{
							groupToAddTo.Settings.Add(data);
							data.OrganizerGroup = groupToAddTo;
							section.AddSetting(data.SlicerConfigName, groupToAddTo);
						}
					}
				}
			}
		}

		/// <summary>
		/// This is the 'Slice Settings' or the 'Printer' settings sections
		/// </summary>
		public class SettingsSection
		{
			private Dictionary<string, Group> subgroups = new Dictionary<string, Group>();

			public SettingsSection(string settingsSectionName)
			{
				this.Name = settingsSectionName;
			}

			public string Name { get; set; }

			public List<Category> Categories { get; private set; } = new List<Category>();

			internal void AddSetting(string slicerConfigName, Group organizerGroup)
			{
				subgroups.Add(slicerConfigName, organizerGroup);
			}

			public bool ContainsKey(string settingsKey) => subgroups.ContainsKey(settingsKey);
		}

		public class Category
		{
			public Category(string categoryName, SettingsSection settingsSection)
			{
				this.Name = categoryName;
				this.SettingsSection = settingsSection;
			}

			public string Name { get; set; }

			public List<Group> Groups { get; private set; } = new List<Group>();

			public SettingsSection SettingsSection { get; }
		}

		public class Group
		{
			public Group(string displayName, Category organizerCategory)
			{
				this.Name = displayName;
				this.Category = organizerCategory;
			}

			public string Name { get; }

			public List<SliceSettingData> Settings { get; private set; } = new List<SliceSettingData>();

			public Category Category { get; }
		}
	}
}