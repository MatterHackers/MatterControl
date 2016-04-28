/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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

using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterControl.DataStorage.ClassicDB
{
	public class ClassicSqlitePrinterProfiles
	{
		public class ClassicSettingsLayer
		{
			//Container class representing a collection of setting along with the meta info for that collection
			public Dictionary<string, SliceSetting> settingsDictionary;

			public SliceSettingsCollection settingsCollectionData;

			public ClassicSettingsLayer(SliceSettingsCollection settingsCollection, Dictionary<string, SliceSetting> settingsDictionary)
			{
				this.settingsCollectionData = settingsCollection;
				this.settingsDictionary = settingsDictionary;
			}
		}

		public static void ImportPrinters(ProfileData profileData, string profilePath)
		{
			foreach (Printer printer in Datastore.Instance.dbSQLite.Query<Printer>("SELECT * FROM Printer;"))
			{
				ImportPrinter(printer, profileData, profilePath);
			}
		}

		public static void ImportPrinter(Printer printer, ProfileData profileData, string profilePath)
		{
			var printerInfo = new PrinterInfo()
			{
				Name = printer.Name,
				Id = printer.Id.ToString()
			};
			profileData.Profiles.Add(printerInfo);

			var layeredProfile = ActiveSliceSettings.LoadEmptyProfile();
			layeredProfile.OemProfile = new OemProfile(LoadOemLayer(printer));

			LoadQualitySettings(layeredProfile, printer);
			LoadMaterialSettings(layeredProfile, printer);

			layeredProfile.UserLayer["MatterControl.Make"] = printer.Make ?? "";
			layeredProfile.UserLayer["MatterControl.Model"] = printer.Model ?? "";
			layeredProfile.UserLayer["MatterControl.BaudRate"] = printer.BaudRate ?? "";
			layeredProfile.UserLayer["MatterControl.ComPort"] = printer.ComPort ?? "";
			layeredProfile.UserLayer["MatterControl.DefaultMaterialPresets"] = printer.MaterialCollectionIds ?? "";
			layeredProfile.UserLayer["MatterControl.WindowsDriver"] = printer.DriverType ?? "";
			layeredProfile.UserLayer["MatterControl.DeviceToken"] = printer.DeviceToken ?? "";
			layeredProfile.UserLayer["MatterControl.DeviceType"] = printer.DeviceType ?? "";

			// Print leveling
			var printLevelingData = PrintLevelingData.Create(
				new SettingsProfile(layeredProfile), 
				printer.PrintLevelingJsonData, 
				printer.PrintLevelingProbePositions);

			layeredProfile.UserLayer["MatterControl.PrintLevelingData"] = JsonConvert.SerializeObject(printLevelingData);
			layeredProfile.UserLayer["MatterControl.PrintLevelingEnabled"] = printer.DoPrintLeveling ? "true" : "false";

			layeredProfile.UserLayer["MatterControl.ManualMovementSpeeds"] = printer.ManualMovementSpeeds;

			// TODO: Where can we find CalibrationFiiles in the current model?
			//layeredProfile.SetActiveValue("MatterControl.CalibrationFiles", printer.Make);

			string fullProfilePath = Path.Combine(profilePath, printer.Id + ".json");
			File.WriteAllText(fullProfilePath, JsonConvert.SerializeObject(layeredProfile, Formatting.Indented));
		}

		private static void LoadMaterialSettings(LayeredProfile layeredProfile, Printer printer)
		{
			var materialAssignments = printer.MaterialCollectionIds?.Split(',');
			if(materialAssignments == null)
			{
				return;
			}

			var collections = Datastore.Instance.dbSQLite.Table<SliceSettingsCollection>().Where(v => v.PrinterId == printer.Id && v.Tag == "material");
			foreach (var collection in collections)
			{
				var settingsDictionary = LoadSettings(collection);
				layeredProfile.MaterialLayers[collection.Name] = new SettingsLayer(settingsDictionary);
			}
		}

		public static void LoadQualitySettings(LayeredProfile layeredProfile, Printer printer)
		{
			var collections = Datastore.Instance.dbSQLite.Table<SliceSettingsCollection>().Where(v => v.PrinterId == printer.Id && v.Tag == "quality");
			foreach (var collection in collections)
			{
				var settingsDictionary = LoadSettings(collection);
				layeredProfile.QualityLayers[collection.Name] = new SettingsLayer(settingsDictionary);
			}
		}

		public static Dictionary<string, string> LoadOemLayer(Printer printer)
		{
			SliceSettingsCollection collection;
			if (printer.DefaultSettingsCollectionId != 0)
			{
				int activePrinterSettingsID = printer.DefaultSettingsCollectionId;
				collection = Datastore.Instance.dbSQLite.Table<SliceSettingsCollection>().Where(v => v.Id == activePrinterSettingsID).Take(1).FirstOrDefault();
			}
			else
			{
				collection = new SliceSettingsCollection();
				collection.Name = printer.Name;
				collection.Commit();

				printer.DefaultSettingsCollectionId = collection.Id;
			}

			return LoadSettings(collection);
		}

		private static Dictionary<string, string> LoadSettings(SliceSettingsCollection collection)
		{
			var settings = Datastore.Instance.dbSQLite.Query<SliceSetting>(
				string.Format("SELECT * FROM SliceSetting WHERE SettingsCollectionID = " + collection.Id));

			//return settings.ToDictionary(s => s.Name, s => s.Value);

			var dictionary = new Dictionary<string, string>();
			foreach(var setting in settings)
			{
				// Not distinct on .Name; last value wins
				dictionary[setting.Name] = setting.Value;
			}

			return dictionary;
		}

		private static void LoadDefaultConfigrationSettings(List<ClassicSettingsLayer> settings)
		{
			SliceSettingsCollection defaultCollection = new SliceSettingsCollection();
			defaultCollection.Name = "__default__";
			ClassicSettingsLayer defaultSettingsLayer = LoadConfigurationSettingsFromFile(Path.Combine("PrinterSettings", "config.ini"), defaultCollection);

			settings.Add(defaultSettingsLayer);
		}

		private static ClassicSettingsLayer LoadConfigurationSettingsFromFile(string pathAndFileName, SliceSettingsCollection collection)
		{
			Dictionary<string, SliceSetting> settingsDictionary = new Dictionary<string, SliceSetting>();
			ClassicSettingsLayer activeCollection;
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

							SliceSetting sliceSetting = new SliceSetting();
							sliceSetting.Name = keyName;
							sliceSetting.Value = settingDefaultValue;

							settingsDictionary.Add(keyName, sliceSetting);
						}
					}
					activeCollection = new ClassicSettingsLayer(collection, settingsDictionary);
					return activeCollection;
				}
				return null;
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
				GuiWidget.BreakInDebugger();
				Debug.WriteLine(string.Format("Error loading configuration: {0}", e));
				return null;
			}
		} 
	}
}