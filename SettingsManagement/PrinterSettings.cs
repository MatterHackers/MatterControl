using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public class PrinterSettings
	{
		private static PrinterSettings globalInstance = null;
		public Dictionary<string, PrinterSetting> settingsDictionary;
		private int ActiveSettingsPrinterId = -1;

		public static PrinterSettings Instance
		{
			get
			{
				if (globalInstance == null)
				{
					globalInstance = new PrinterSettings();
					globalInstance.LoadData();
				}

				return globalInstance;
			}
		}

		private void LoadDataIfNeeded()
		{
			// TODO: Review int printerID
			int printerID;
			int.TryParse(ActiveSliceSettings.Instance.Id(), out printerID);

			//Lazy load the data (rather than hook to printer change event)
			if (printerID != ActiveSettingsPrinterId)
			{
				LoadData();
			}
		}

		public string get(string key)
		{
			string result = null;
			if (ActiveSliceSettings.Instance == null)
			{
				//No printer selected
			}
			else
			{
				LoadDataIfNeeded();

				if (settingsDictionary.ContainsKey(key))
				{
					result = settingsDictionary[key].Value;
				}
			}
			return result;
		}

		public void set(string key, string value)
		{
			if (ActiveSliceSettings.Instance == null)
			{
				//No printer selected
			}
			else
			{
				LoadDataIfNeeded();

				PrinterSetting setting;
				if (settingsDictionary.ContainsKey(key))
				{
					setting = settingsDictionary[key];
				}
				else
				{
					// TODO: Review int printerID
					int printerID;
					int.TryParse(ActiveSliceSettings.Instance.Id(), out printerID);

					setting = new PrinterSetting();
					setting.Name = key;
					setting.PrinterId = printerID;

					settingsDictionary[key] = setting;
				}

				setting.Value = value;
				setting.Commit();
			}
		}

		private void LoadData()
		{
			if (ActiveSliceSettings.Instance != null)
			{
				settingsDictionary = new Dictionary<string, PrinterSetting>();
				foreach (PrinterSetting s in GetPrinterSettings())
				{
					settingsDictionary[s.Name] = s;
				}
				// TODO: Review int printerID
				int printerID;
				int.TryParse(ActiveSliceSettings.Instance.Id(), out printerID);

				ActiveSettingsPrinterId = printerID;
			}
		}

		private IEnumerable<PrinterSetting> GetPrinterSettings()
		{
			//Retrieve a list of settings from the Datastore
			string query = string.Format("SELECT * FROM PrinterSetting WHERE PrinterId = {0};", ActiveSliceSettings.Instance.Id());
			return Datastore.Instance.dbSQLite.Query<PrinterSetting>(query);
		}
	}
}