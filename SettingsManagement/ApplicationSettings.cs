using MatterHackers.MatterControl.SettingsManagement;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public class ApplicationSettings
	{
		public static string OpenPrintableFileParams { get { return "STL, AMF, ZIP, GCODE|*.stl;*.amf;*.zip;*.gcode"; } }

		public static string OpenDesignFileParams { get { return "STL, AMF, ZIP|*.stl;*.amf;*.zip"; } }

		private static ApplicationSettings globalInstance = null;
		public Dictionary<string, DataStorage.SystemSetting> settingsDictionary;

		public static ApplicationSettings Instance
		{
			get
			{
				if (globalInstance == null)
				{
					globalInstance = new ApplicationSettings();
					globalInstance.LoadData();
				}

				return globalInstance;
			}
		}

		private string oemName = null;

		public string GetOEMName()
		{
			if (oemName == null)
			{
				string[] printerWhiteListStrings = OemSettings.Instance.PrinterWhiteList.ToArray();
				if (printerWhiteListStrings.Length > 1)
				{
					oemName = "MatterHackers";
				}
				else
				{
					oemName = printerWhiteListStrings[0];
				}
			}

			return oemName;
		}

		public string get(string key)
		{
			string result;
			if (settingsDictionary == null)
			{
				globalInstance.LoadData();
			}

			if (settingsDictionary.ContainsKey(key))
			{
				result = settingsDictionary[key].Value;
			}
			else
			{
				result = null;
			}
			return result;
		}

		public void set(string key, string value)
		{
			DataStorage.SystemSetting setting;
			if (settingsDictionary.ContainsKey(key))
			{
				setting = settingsDictionary[key];
			}
			else
			{
				setting = new DataStorage.SystemSetting();
				setting.Name = key;

				settingsDictionary[key] = setting;
			}

			setting.Value = value;
			setting.Commit();
		}

		private void LoadData()
		{
			IEnumerable<DataStorage.SystemSetting> settingsList = GetApplicationSettings();
			settingsDictionary = new Dictionary<string, DataStorage.SystemSetting>();
			foreach (DataStorage.SystemSetting s in settingsList)
			{
				settingsDictionary[s.Name] = s;
			}
		}

		private IEnumerable<DataStorage.SystemSetting> GetApplicationSettings()
		{
			//Retrieve a list of saved printers from the Datastore
			string query = string.Format("SELECT * FROM SystemSetting;");
			IEnumerable<DataStorage.SystemSetting> result = (IEnumerable<DataStorage.SystemSetting>)DataStorage.Datastore.Instance.dbSQLite.Query<DataStorage.SystemSetting>(query);
			return result;
		}
	}
}