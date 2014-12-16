using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.MatterControl;

namespace MatterHackers.MatterControl
{
    public class UserSettings
    {
        static UserSettings globalInstance = null;
        public Dictionary<string, DataStorage.UserSetting> settingsDictionary;

        UserSettingsFields fields = new UserSettingsFields();
        public UserSettingsFields Fields
        {
            get { return fields; }
        }

        public static UserSettings Instance
        {
            get
            {
                if (globalInstance == null)
                {
                    globalInstance = new UserSettings();
                    globalInstance.LoadData();
                }

                return globalInstance;
            }
        }

        public string get(string key)
        {
            string result;
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
            DataStorage.UserSetting setting;
            if (settingsDictionary.ContainsKey(key))
            {
                setting = settingsDictionary[key];
            }
            else
            {
                setting = new DataStorage.UserSetting();
                setting.Name = key;

                settingsDictionary[key] = setting;
            }

            setting.Value = value;
            setting.Commit();
        }

        private void LoadData()
        {
            IEnumerable<DataStorage.UserSetting> settingsList = GetApplicationSettings();
            settingsDictionary = new Dictionary<string, DataStorage.UserSetting>();
            foreach (DataStorage.UserSetting s in settingsList)
            {
                settingsDictionary[s.Name] = s;
            }

        }

        IEnumerable<DataStorage.UserSetting> GetApplicationSettings()
        {
            //Retrieve a list of settings from the Datastore
            string query = string.Format("SELECT * FROM UserSetting;");
            IEnumerable<DataStorage.UserSetting> result = (IEnumerable<DataStorage.UserSetting>)DataStorage.Datastore.Instance.dbSQLite.Query<DataStorage.UserSetting>(query);
            return result;
        }
    }
}
