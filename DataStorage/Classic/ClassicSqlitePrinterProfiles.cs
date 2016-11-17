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

		public static void ImportPrinters(ProfileManager profileData, string profilePath)
		{
			foreach (Printer printer in Datastore.Instance.dbSQLite.Query<Printer>("SELECT * FROM Printer;"))
			{
				ImportPrinter(printer, profileData, profilePath);
			}
		}

		public static void ImportPrinter(Printer printer, ProfileManager profileData, string profilePath)
		{
			var printerInfo = new PrinterInfo()
			{
				Name = printer.Name,
				ID = printer.Id.ToString(),
				Make = printer.Make ?? "",
				Model = printer.Model ?? "",
			};
			profileData.Profiles.Add(printerInfo);

			var printerSettings = new PrinterSettings()
			{
				OemLayer = LoadOemLayer(printer)
			};

			printerSettings.OemLayer[SettingsKey.make] = printerInfo.Make;
			printerSettings.OemLayer[SettingsKey.model] = printer.Model;

			LoadQualitySettings(printerSettings, printer);
			LoadMaterialSettings(printerSettings, printer);

			printerSettings.ID = printer.Id.ToString();

			printerSettings.UserLayer[SettingsKey.printer_name] = printer.Name ?? "";
			printerSettings.UserLayer[SettingsKey.baud_rate] = printer.BaudRate ?? "";
			printerSettings.UserLayer[SettingsKey.auto_connect] = printer.AutoConnect ? "1" : "0";
			printerSettings.UserLayer[SettingsKey.default_material_presets] = printer.MaterialCollectionIds ?? "";
			printerSettings.UserLayer[SettingsKey.windows_driver] = printer.DriverType ?? "";
			printerSettings.UserLayer[SettingsKey.device_token] = printer.DeviceToken ?? "";
			printerSettings.UserLayer[SettingsKey.device_type] = printer.DeviceType ?? "";

			if (string.IsNullOrEmpty(ProfileManager.Instance.LastProfileID))
			{
				ProfileManager.Instance.LastProfileID = printer.Id.ToString();
			}

			printerSettings.UserLayer[SettingsKey.active_theme_name] = UserSettings.Instance.get(UserSettingsKey.ActiveThemeName);

			// Import macros from the database
			var allMacros =  Datastore.Instance.dbSQLite.Query<CustomCommands>("SELECT * FROM CustomCommands WHERE PrinterId = " + printer.Id);
			printerSettings.Macros = allMacros.Select(macro => new GCodeMacro()
			{
				GCode = macro.Value.Trim(),
				Name = macro.Name,
				LastModified = macro.DateLastModified
			}).ToList();

			string query = string.Format("SELECT * FROM PrinterSetting WHERE Name = 'PublishBedImage' and PrinterId = {0};", printer.Id);
			var publishBedImage = Datastore.Instance.dbSQLite.Query<PrinterSetting>(query).FirstOrDefault();

			printerSettings.UserLayer[SettingsKey.publish_bed_image] = publishBedImage?.Value == "true" ? "1" : "0";

			// Print leveling
			var printLevelingData = PrintLevelingData.Create(
				printerSettings, 
				printer.PrintLevelingJsonData, 
				printer.PrintLevelingProbePositions);

			printerSettings.UserLayer[SettingsKey.print_leveling_data] = JsonConvert.SerializeObject(printLevelingData);
			printerSettings.UserLayer[SettingsKey.print_leveling_enabled] = printer.DoPrintLeveling ? "true" : "false";

			printerSettings.UserLayer["manual_movement_speeds"] = printer.ManualMovementSpeeds;

			// make sure we clear the one time settings
			printerSettings.OemLayer[SettingsKey.spiral_vase] = "";
			printerSettings.OemLayer[SettingsKey.bottom_clip_amount] = "";
			printerSettings.OemLayer[SettingsKey.layer_to_pause] = "";

			// TODO: Where can we find CalibrationFiiles in the current model?
			//layeredProfile.SetActiveValue(""calibration_files"", ???);

			printerSettings.ID = printer.Id.ToString();

			printerSettings.DocumentVersion = PrinterSettings.LatestVersion;

			printerSettings.Helpers.SetComPort(printer.ComPort);

			printerSettings.Save();
		}

		private static void LoadMaterialSettings(PrinterSettings layeredProfile, Printer printer)
		{
			var materialAssignments = printer.MaterialCollectionIds?.Split(',');
			if(materialAssignments == null)
			{
				return;
			}

			var collections = Datastore.Instance.dbSQLite.Table<SliceSettingsCollection>().Where(v => v.PrinterId == printer.Id && v.Tag == "material");
			foreach (var collection in collections)
			{
				layeredProfile.MaterialLayers.Add(new PrinterSettingsLayer(LoadSettings(collection))
				{
					LayerID = Guid.NewGuid().ToString(),
					Name = collection.Name
				});
			}
		}

		public static void LoadQualitySettings(PrinterSettings layeredProfile, Printer printer)
		{
			var collections = Datastore.Instance.dbSQLite.Table<SliceSettingsCollection>().Where(v => v.PrinterId == printer.Id && v.Tag == "quality");
			foreach (var collection in collections)
			{
				layeredProfile.QualityLayers.Add(new PrinterSettingsLayer(LoadSettings(collection))
				{
					LayerID = Guid.NewGuid().ToString(),
					Name = collection.Name
				});
			}
		}

		public static PrinterSettingsLayer LoadOemLayer(Printer printer)
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

			return new PrinterSettingsLayer(LoadSettings(collection));
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
	}
}