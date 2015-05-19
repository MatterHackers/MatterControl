using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public class PrinterSettings
	{
		private static PrinterSettings globalInstance = null;
		public Dictionary<string, DataStorage.PrinterSetting> settingsDictionary;
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
			//Lazy load the data (rather than hook to printer change event)
			if (ActivePrinterProfile.Instance.ActivePrinter.Id != ActiveSettingsPrinterId)
			{
				LoadData();
			}
		}

		public string get(string key)
		{
			string result = null;
			if (ActivePrinterProfile.Instance.ActivePrinter == null)
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
			if (ActivePrinterProfile.Instance.ActivePrinter == null)
			{
				//No printer selected
			}
			else
			{
				LoadDataIfNeeded();

				DataStorage.PrinterSetting setting;
				if (settingsDictionary.ContainsKey(key))
				{
					setting = settingsDictionary[key];
				}
				else
				{
					setting = new DataStorage.PrinterSetting();
					setting.Name = key;
					setting.PrinterId = ActivePrinterProfile.Instance.ActivePrinter.Id;

					settingsDictionary[key] = setting;
				}

				setting.Value = value;
				setting.Commit();
			}
		}

		private void LoadData()
		{
			if (ActivePrinterProfile.Instance.ActivePrinter != null)
			{
				IEnumerable<DataStorage.PrinterSetting> settingsList = GetPrinterSettings();
				settingsDictionary = new Dictionary<string, DataStorage.PrinterSetting>();
				foreach (DataStorage.PrinterSetting s in settingsList)
				{
					settingsDictionary[s.Name] = s;
				}
				ActiveSettingsPrinterId = ActivePrinterProfile.Instance.ActivePrinter.Id;
			}
		}

		private IEnumerable<DataStorage.PrinterSetting> GetPrinterSettings()
		{
			//Retrieve a list of settings from the Datastore
			string query = string.Format("SELECT * FROM PrinterSetting WHERE PrinterId = {0};", ActivePrinterProfile.Instance.ActivePrinter.Id);
			IEnumerable<DataStorage.PrinterSetting> result = (IEnumerable<DataStorage.PrinterSetting>)DataStorage.Datastore.Instance.dbSQLite.Query<DataStorage.PrinterSetting>(query);
			return result;
		}
	}
}