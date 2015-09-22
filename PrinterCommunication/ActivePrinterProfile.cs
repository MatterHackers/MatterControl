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

using MatterHackers.Agg;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.MatterControl
{
	public class ActivePrinterProfile
	{
		public enum SlicingEngineTypes { Slic3r, CuraEngine, MatterSlice };

		private static readonly SlicingEngineTypes defaultEngineType = SlicingEngineTypes.MatterSlice;
		private static ActivePrinterProfile globalInstance = null;

		public RootedObjectEventHandler ActivePrinterChanged = new RootedObjectEventHandler();
		public RootedObjectEventHandler DoPrintLevelingChanged = new RootedObjectEventHandler();

		// private so that it can only be gotten through the Instance
		private ActivePrinterProfile()
		{
		}

		private Printer activePrinter = null;

		public Printer ActivePrinter
		{
			get { return activePrinter; }
			set
			{
				if (activePrinter != value)
				{
					PrinterConnectionAndCommunication.Instance.Disable();

					activePrinter = value;
					ValidateMaterialSettings();
					ValidateQualitySettings();

					if (ActivePrinter != null)
					{
						BedSettings.SetMakeAndModel(ActivePrinter.Make, ActivePrinter.Model);
					}
					globalInstance.OnActivePrinterChanged(null);
				}
			}
		}

		public static ActivePrinterProfile Instance
		{
			get
			{
				if (globalInstance == null)
				{
					globalInstance = new ActivePrinterProfile();
				}

				return globalInstance;
			}
		}

		private void ValidateQualitySettings()
		{
			if (activePrinter != null)
			{
				int index = activePrinter.QualityCollectionId;
				SliceSettingsCollection collection = DataStorage.Datastore.Instance.dbSQLite.Table<DataStorage.SliceSettingsCollection>().Where(v => v.Id == index).Take(1).FirstOrDefault();
				if (collection == null)
				{
					ActivePrinterProfile.Instance.ActiveQualitySettingsID = 0;
				}
			}
		}

		private void ValidateMaterialSettings()
		{
			if (activePrinter != null && activePrinter.MaterialCollectionIds != null)
			{
				string[] activeMaterialPresets = activePrinter.MaterialCollectionIds.Split(',');
				for (int i = 0; i < activeMaterialPresets.Count(); i++)
				{
					int index = 0;
					Int32.TryParse(activeMaterialPresets[i], out index);
					if (index != 0)
					{
						SliceSettingsCollection collection = DataStorage.Datastore.Instance.dbSQLite.Table<DataStorage.SliceSettingsCollection>().Where(v => v.Id == index).Take(1).FirstOrDefault();
						if (collection == null)
						{
							ActivePrinterProfile.Instance.SetMaterialSetting(i + 1, 0);
						}
					}
				}
			}
		}

		public int GetMaterialSetting(int extruderNumber1Based)
		{
			int i = 0;
			if (extruderNumber1Based > 0
				&& ActivePrinter != null)
			{
				string materialSettings = ActivePrinter.MaterialCollectionIds;
				string[] materialSettingsList;
				if (materialSettings != null)
				{
					materialSettingsList = materialSettings.Split(',');
					if (materialSettingsList.Count() >= extruderNumber1Based)
					{
						Int32.TryParse(materialSettingsList[extruderNumber1Based - 1], out i);
					}
				}
			}
			return i;
		}

		public void SetMaterialSetting(int extruderPosition, int settingId)
		{
			string[] newMaterialSettingsArray;
			string[] currentMaterialSettingsArray;

			string materialSettings = ActivePrinter.MaterialCollectionIds;

			if (materialSettings != null)
			{
				currentMaterialSettingsArray = materialSettings.Split(',');
			}
			else
			{
				currentMaterialSettingsArray = new string[extruderPosition];
			}

			//Resize the array of material settings if necessary
			if (currentMaterialSettingsArray.Count() < extruderPosition)
			{
				newMaterialSettingsArray = new string[extruderPosition];
				for (int i = 0; i < currentMaterialSettingsArray.Length; i++)
				{
					newMaterialSettingsArray[i] = currentMaterialSettingsArray[i];
				}
			}
			else
			{
				newMaterialSettingsArray = currentMaterialSettingsArray;
			}
			newMaterialSettingsArray[extruderPosition - 1] = settingId.ToString();

			ActivePrinter.MaterialCollectionIds = String.Join(",", newMaterialSettingsArray);
			ActivePrinter.Commit();
		}

		public int ActiveQualitySettingsID
		{
			get
			{
				if (ActivePrinter != null)
				{
					return ActivePrinter.QualityCollectionId;
				}
				return 0;
			}

			set
			{
				if (ActiveQualitySettingsID != value)
				{
					ActivePrinter.QualityCollectionId = value;
					ActivePrinter.Commit();
				}
			}
		}

		public SlicingEngineTypes ActiveSliceEngineType
		{
			get
			{
				if (ActivePrinter != null)
				{
					foreach (SlicingEngineTypes engine in SlicingEngineTypes.GetValues(typeof(SlicingEngineTypes)))
					{
						if (ActivePrinter.CurrentSlicingEngine == engine.ToString())
						{
							return engine;
						}
					}

					// It is not set in the slice settings, so set it and save it.
					ActivePrinter.CurrentSlicingEngine = defaultEngineType.ToString();
					ActivePrinter.Commit();
				}
				return defaultEngineType;
			}

			set
			{
				if (ActiveSliceEngineType != value)
				{
					ActivePrinter.CurrentSlicingEngine = value.ToString();
					ActivePrinter.Commit();
				}
			}
		}

		public SliceEngineMaping ActiveSliceEngine
		{
			get
			{
				switch (ActiveSliceEngineType)
				{
					case SlicingEngineTypes.CuraEngine:
						return EngineMappingCura.Instance;

					case SlicingEngineTypes.MatterSlice:
						return EngineMappingsMatterSlice.Instance;

					case SlicingEngineTypes.Slic3r:
						return Slic3rEngineMappings.Instance;

					default:
						return null;
				}
			}
		}

		public void OnActivePrinterChanged(EventArgs e)
		{
			ActivePrinterChanged.CallEvents(this, e);
		}

		public bool DoPrintLeveling
		{
			get
			{
				if (ActivePrinter != null)
				{
					return ActivePrinter.DoPrintLeveling;
				}
				return false;
			}

			set
			{
				if (ActivePrinter != null && ActivePrinter.DoPrintLeveling != value)
				{
					ActivePrinter.DoPrintLeveling = value;
					DoPrintLevelingChanged.CallEvents(this, null);
					ActivePrinter.Commit();

					if (DoPrintLeveling)
					{
						PrintLevelingData levelingData = PrintLevelingData.GetForPrinter(ActivePrinterProfile.Instance.ActivePrinter);
						PrintLevelingPlane.Instance.SetPrintLevelingEquation(
							levelingData.SampledPosition0,
							levelingData.SampledPosition1,
							levelingData.SampledPosition2,
							ActiveSliceSettings.Instance.PrintCenter);
					}
				}
			}
		}

		public static void CheckForAndDoAutoConnect()
		{
			DataStorage.Printer autoConnectProfile = ActivePrinterProfile.GetAutoConnectProfile();
			if (autoConnectProfile != null)
			{
				ActivePrinterProfile.Instance.ActivePrinter = autoConnectProfile;
				PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
				PrinterConnectionAndCommunication.Instance.ConnectToActivePrinter();
			}
		}

		public static DataStorage.Printer GetAutoConnectProfile()
		{
			string query = string.Format("SELECT * FROM Printer;");
			IEnumerable<Printer> printer_profiles = (IEnumerable<Printer>)Datastore.Instance.dbSQLite.Query<Printer>(query);
			string[] comportNames = FrostedSerialPort.GetPortNames();

			foreach (DataStorage.Printer printer in printer_profiles)
			{
				if (printer.AutoConnectFlag)
				{
					bool portIsAvailable = comportNames.Contains(printer.ComPort);
					if (portIsAvailable)
					{
						return printer;
					}
				}
			}
			return null;
		}
	}
}