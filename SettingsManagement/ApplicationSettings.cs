using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SettingsManagement;
using System.Collections.Generic;
using System;

namespace MatterHackers.MatterControl
{
	public class ApplicationSettings
	{
		public static string LibraryFilterFileExtensions { get { return ".stl,.amf,.gcode"; } }

		public static string OpenPrintableFileParams { get { return "STL, AMF, ZIP, GCODE|*.stl;*.amf;*.zip;*.gcode"; } }

		public static string OpenDesignFileParams { get { return "STL, AMF, ZIP, GCODE|*.stl;*.amf;*.zip;*.gcode"; } }

		private static ApplicationSettings globalInstance = null;
		public Dictionary<string, SystemSetting> settingsDictionary;

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
				if (printerWhiteListStrings.Length == 0
					|| printerWhiteListStrings.Length > 1)
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

		private string GetClientTokenKeyName()
		{
			string keyName = "ClientToken";
#if DEBUG
			keyName += "_Test";
#else
			if(OemSettings.Instance.ForceTestEnvironment)
			{
				keyName += "_Test";
			}
#endif
			if(ApplicationController.ApplicationInstanceCount > 1)
			{
				keyName += "_" + ApplicationController.ApplicationInstanceCount.ToString();
			}

			return keyName;
		}

		public string GetClientToken()
		{
			return get(GetClientTokenKeyName());
		}

		public void SetClientToken(string clientToken)
		{
			set(GetClientTokenKeyName(), clientToken);
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
			SystemSetting setting;
			if (settingsDictionary.ContainsKey(key))
			{
				setting = settingsDictionary[key];
			}
			else
			{
				setting = new SystemSetting();
				setting.Name = key;

				settingsDictionary[key] = setting;
			}

			setting.Value = value;
			setting.Commit();
		}

		private void LoadData()
		{
			settingsDictionary = new Dictionary<string, SystemSetting>();
			foreach (SystemSetting s in GetApplicationSettings())
			{
				settingsDictionary[s.Name] = s;
			}
		}

		private IEnumerable<SystemSetting> GetApplicationSettings()
		{
			//Retrieve SystemSettings from the Datastore
			return Datastore.Instance.dbSQLite.Query<SystemSetting>("SELECT * FROM SystemSetting;");
		}
	}
}