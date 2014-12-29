﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.PrintQueue;

namespace MatterHackers.MatterControl
{
	//Wraps the printer record. Includes temporary information that we don't need in the DB.
	public class PrinterSetupStatus
	{
		public Printer ActivePrinter;
		public List<string> DriversToInstall = new List<string>();
		public Type PreviousSetupWidget;
		public Type NextSetupWidget;
		List<CustomCommands> printerCustomCommands;
		string defaultMaterialPreset;
		string defaultQualityPreset;
		string defaultMovementSpeeds;

		public PrinterSetupStatus(Printer printer = null)
		{
			if (printer == null)
			{
				this.ActivePrinter = new Printer();
				this.ActivePrinter.Make = null;
				this.ActivePrinter.Model = null;
				this.ActivePrinter.Name = "Default Printer ({0})".FormatWith(ExistingPrinterCount() + 1);
				this.ActivePrinter.BaudRate = null;
				this.ActivePrinter.ComPort = null;
			}
			else
			{
				this.ActivePrinter = printer;
			}
		}

		public int ExistingPrinterCount()
		{
			string query = string.Format("SELECT COUNT(*) FROM Printer;");
			string result = Datastore.Instance.dbSQLite.ExecuteScalar<string>(query);
			return Convert.ToInt32(result);
		}

		public void LoadCalibrationPrints()
		{
			if (this.ActivePrinter.Make != null && this.ActivePrinter.Model != null)    
			{
				// Load the calibration file names
				List<string> calibrationPrintFileNames = LoadCalibrationPartNamesForPrinter(this.ActivePrinter.Make, this.ActivePrinter.Model);

				string[] itemsToAdd = LibraryData.SyncCalibrationFilesToDisk(calibrationPrintFileNames);
				if (itemsToAdd.Length > 0)
				{
					// Import any files sync'd to disk into the library, then add them to the queue
					LibraryData.Instance.LoadFilesIntoLibrary(itemsToAdd, null, (sender, e) =>
						{
							AddItemsToQueue(calibrationPrintFileNames, QueueData.Instance.GetItemNames());
						});
				}
				else
				{
					// Otherwise, just ensure the item gets into the queue
					AddItemsToQueue(calibrationPrintFileNames, QueueData.Instance.GetItemNames());
				}
			}
		}

		private static void AddItemsToQueue(List<string> calibrationPrintFileNames, string[] queueItems)
		{
			// After the import has completed, add each of the calibration items into the print queue
			foreach (string fileName in calibrationPrintFileNames)
			{
				string nameOnly = Path.GetFileNameWithoutExtension(fileName);
				if (queueItems.Contains(nameOnly))
				{
					continue;
				}

				// If the library item does not exist in the queue, add it
				var libraryItem = LibraryData.Instance.GetLibraryItems(nameOnly).FirstOrDefault();
				if (libraryItem != null)
				{
					QueueData.Instance.AddItem(new PrintItemWrapper(libraryItem));
				}
			}
		}

		private List<string> LoadCalibrationPartNamesForPrinter(string make, string model)
		{
			List<string> calibrationFiles = new List<string>();
			string setupSettingsPathAndFile = Path.Combine("PrinterSettings", make, model, "calibration.ini");
			if (StaticData.Instance.FileExists(setupSettingsPathAndFile))
			{
				try
				{
					foreach (string line in StaticData.Instance.ReadAllLines(setupSettingsPathAndFile))
					{
						//Ignore commented lines
						if (!line.StartsWith("#"))
						{
							string settingLine = line.Trim();
							calibrationFiles.Add(settingLine);
						}
					}
				}
				catch
				{

				}
			}
			return calibrationFiles;
		}

		public void LoadSetupSettings(string make, string model)
		{
			Dictionary<string, string> settingsDict = LoadPrinterSetupFromFile(make, model);
			Dictionary<string, string> macroDict = new Dictionary<string, string>();
			macroDict["Lights On"] = "M42 P6 S255";
			macroDict["Lights Off"] = "M42 P6 S0";

			//Determine if baud rate is needed and show controls if required
			string baudRate;
			if (settingsDict.TryGetValue("baud_rate", out baudRate))
			{
				ActivePrinter.BaudRate = baudRate;
			}

			// Check if we need to run the print level wizard before printing
			PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(ActivePrinter);
			string needsPrintLeveling;
			if (settingsDict.TryGetValue("needs_print_leveling", out needsPrintLeveling))
			{
				levelingData.needsPrintLeveling = true;
			}

			string printLevelingType;
			if (settingsDict.TryGetValue("print_leveling_type", out printLevelingType))
			{
				levelingData.levelingSystem = PrintLevelingData.LevelingSystem.Probe2Points;
			}

			string defaultSliceEngine;
			if (settingsDict.TryGetValue("default_slice_engine", out defaultSliceEngine))
			{
				if (Enum.IsDefined(typeof(ActivePrinterProfile.SlicingEngineTypes), defaultSliceEngine))
				{
					ActivePrinter.CurrentSlicingEngine = defaultSliceEngine;
				}
			}

			settingsDict.TryGetValue("default_material_presets", out defaultMaterialPreset);
			settingsDict.TryGetValue("default_quality_preset", out defaultQualityPreset);
			settingsDict.TryGetValue("default_movement_speeds", out defaultMovementSpeeds);

			string defaultMacros;
			printerCustomCommands = new List<CustomCommands>();
			if (settingsDict.TryGetValue("default_macros", out defaultMacros))
			{
				string[] macroList = defaultMacros.Split(',');
				foreach (string macroName in macroList)
				{
					string macroValue;
					if (macroDict.TryGetValue(macroName.Trim(), out macroValue))
					{
						CustomCommands customMacro = new CustomCommands();
						customMacro.Name = macroName.Trim();
						customMacro.Value = macroValue;

						printerCustomCommands.Add(customMacro);

					}
				}
			}

			//Determine what if any drivers are needed
			string infFileNames;
			if (settingsDict.TryGetValue("windows_driver", out infFileNames))
			{
				string[] fileNames = infFileNames.Split(',');
				foreach (string fileName in fileNames)
				{
					switch (OsInformation.OperatingSystem)
					{
						case OSType.Windows:

							string pathForInf = Path.GetFileNameWithoutExtension(fileName);

							// TODO: It's really unexpected that the driver gets copied to the temp folder everytime a printer is setup. I'd think this only needs
							// to happen when the infinstaller is run (More specifically - move this to *after* the user clicks Install Driver)

							string infPath = Path.Combine("Drivers", pathForInf);
							string infPathAndFileToInstall =  Path.Combine(infPath, fileName);

							if (StaticData.Instance.FileExists(infPathAndFileToInstall))
							{
								// Ensure the output directory exists
								string destTempPath = Path.GetFullPath(Path.Combine(ApplicationDataStorage.Instance.ApplicationUserDataPath, "data", "temp", "inf", pathForInf));
								if (!Directory.Exists(destTempPath))
								{
									Directory.CreateDirectory(destTempPath);
								}

								string destTempInf = Path.GetFullPath(Path.Combine(destTempPath, fileName));

								// Sync each file from StaticData to the location on disk for serial drivers
								foreach (string file in StaticData.Instance.GetFiles(infPath))
								{
									using(Stream outstream = File.OpenWrite(Path.Combine(destTempPath, Path.GetFileName(file))))
									using (Stream instream = StaticData.Instance.OpenSteam(file))
									{
										instream.CopyTo(outstream);
									}
								}

								DriversToInstall.Add(destTempInf);
							}
							break;

						default:
							break;
					}
				}
			}
		}

		private Dictionary<string, string> LoadPrinterSetupFromFile(string make, string model)
		{
			string setupSettingsPathAndFile = Path.Combine("PrinterSettings", make, model, "setup.ini");
			Dictionary<string, string> settingsDict = new Dictionary<string, string>();

			if (StaticData.Instance.FileExists(setupSettingsPathAndFile))
			{
				try
				{
					foreach (string line in StaticData.Instance.ReadAllLines(setupSettingsPathAndFile))
					{
						//Ignore commented lines
						if (!line.StartsWith("#"))
						{
							string[] settingLine = line.Split('=');
							string keyName = settingLine[0].Trim();
							string settingDefaultValue = settingLine[1].Trim();

							settingsDict.Add(keyName, settingDefaultValue);
						}
					}
				}
				catch
				{

				}
			}
			return settingsDict;
		}

		public SliceSettingsCollection LoadDefaultSliceSettings(string make, string model)
		{
			SliceSettingsCollection collection = null;
			Dictionary<string, string> settingsDict = LoadSliceSettingsFromFile(Path.Combine("PrinterSettings", make, model, "config.ini"));

			if (settingsDict.Count > 0)
			{
				collection = new DataStorage.SliceSettingsCollection();
				collection.Name = this.ActivePrinter.Name;
				collection.Commit();

				this.ActivePrinter.DefaultSettingsCollectionId = collection.Id;

				CommitSliceSettings(settingsDict, collection.Id);
			}
			return collection;
		}

		public void LoadSlicePresets(string make, string model, string tag)
		{
			foreach (string filePath in GetSlicePresets(make, model, tag))
			{
				SliceSettingsCollection collection = null;
				Dictionary<string, string> settingsDict = LoadSliceSettingsFromFile(filePath);

				if (settingsDict.Count > 0)
				{
					collection = new DataStorage.SliceSettingsCollection();
					collection.Name = Path.GetFileNameWithoutExtension(filePath);
					collection.PrinterId = ActivePrinter.Id;
					collection.Tag = tag;
					collection.Commit();

					if (tag == "material" && defaultMaterialPreset != null && collection.Name == defaultMaterialPreset)
					{
						ActivePrinter.MaterialCollectionIds = collection.Id.ToString();
						ActivePrinter.Commit();
					}
					else if (tag == "quality"  && defaultQualityPreset != null && collection.Name == defaultQualityPreset)
					{
						ActivePrinter.QualityCollectionId = collection.Id;
						ActivePrinter.Commit();
					}
					CommitSliceSettings(settingsDict, collection.Id);
				}
			}
		}

		private void CommitSliceSettings(Dictionary<string, string> settingsDict, int collectionId)
		{
			foreach (KeyValuePair<string, string> item in settingsDict)
			{
				DataStorage.SliceSetting sliceSetting = new DataStorage.SliceSetting();
				sliceSetting.Name = item.Key;
				sliceSetting.Value = item.Value;
				sliceSetting.SettingsCollectionId = collectionId;
				sliceSetting.Commit();
			}
		}

		private string[] GetSlicePresets(string make, string model, string tag)
		{
			string[] presetPaths = new string[]{};
			string folderPath = Path.Combine("PrinterSettings", make, model, tag);
			if (StaticData.Instance.DirectoryExists(folderPath))
			{
				presetPaths = StaticData.Instance.GetFiles(folderPath).ToArray();
			}
			return presetPaths;
		}

		private Dictionary<string, string> LoadSliceSettingsFromFile(string setupSettingsPathAndFile)
		{            
			Dictionary<string, string> settingsDict = new Dictionary<string, string>();
			if (StaticData.Instance.FileExists(setupSettingsPathAndFile))
			{
				try
				{
					foreach (string line in StaticData.Instance.ReadAllLines(setupSettingsPathAndFile))
					{
						//Ignore commented lines
						if (!line.StartsWith("#") && line.Length > 0)
						{
							string[] settingLine = line.Split('=');
							if (settingLine.Length == 2)
							{
								string keyName = settingLine[0].Trim();
								string settingDefaultValue = settingLine[1].Trim();

								settingsDict.Add(keyName, settingDefaultValue);
							}
						}
					}
				}
				catch
				{

				}
			}
			return settingsDict;
		}

		public void Save()
		{
			//Load the default slice settings for the make and model combination - if they exist
			SliceSettingsCollection collection = LoadDefaultSliceSettings(this.ActivePrinter.Make, this.ActivePrinter.Model);

			if (defaultMovementSpeeds != null)
			{
				this.ActivePrinter.ManualMovementSpeeds = defaultMovementSpeeds;
			}

			//Ordering matters - need to get Id for printer prior to loading slice presets
			this.ActivePrinter.AutoConnectFlag = true;
			this.ActivePrinter.Commit();

			LoadSlicePresets(this.ActivePrinter.Make, this.ActivePrinter.Model, "material");
			LoadSlicePresets(this.ActivePrinter.Make, this.ActivePrinter.Model, "quality");



			foreach (CustomCommands customCommand in printerCustomCommands)
			{
				customCommand.PrinterId = ActivePrinter.Id;
				customCommand.Commit();
			}
		}
	}
}

