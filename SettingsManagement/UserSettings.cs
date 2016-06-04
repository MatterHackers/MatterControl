using MatterHackers.MatterControl.DataStorage;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.MatterControl
{
	public enum ApplicationDisplayType { Responsive, Touchscreen };

	public class UserSettings
	{
		private static UserSettings globalInstance = null;

		private static readonly object syncRoot = new object();

		private Dictionary<string, UserSetting> settingsDictionary;

		private UserSettings()
		{
			// Load the UserSettings from the database
			settingsDictionary = new Dictionary<string, UserSetting>();
			foreach(var userSetting in Datastore.Instance.dbSQLite.Query<UserSetting>("SELECT * FROM UserSetting;"))
			{
				// Allow for duplicate entries in the database with the same .Name value
				settingsDictionary[userSetting.Name] = userSetting;
			}

			// Set English as default language if unset
			if (string.IsNullOrEmpty(this.get("Language")))
			{
				this.set("Language", "en");
			}

			// Propagate Language to local property
			this.Language = this.get("Language");
		}

		public static UserSettings Instance
		{
			get
			{
				if (globalInstance == null)
				{
					lock(syncRoot)
					{
						if (globalInstance == null)
						{
							globalInstance = new UserSettings();
						}
					}
				}

				return globalInstance;
			}
		}

		public string Language { get; private set; } 

		public UserSettingsFields Fields { get; private set; } = new UserSettingsFields();

		public string get(string key)
		{
			UserSetting userSetting;
			if (settingsDictionary.TryGetValue(key, out userSetting))
			{ 
				return userSetting.Value;
			}

			return null;
		}

		public void set(string key, string value)
		{
			UserSetting setting;

			if(!settingsDictionary.TryGetValue(key, out setting))
			{
				// If the setting for the given key doesn't exist, create it
				setting = new UserSetting()
				{
					Name = key
				};
				settingsDictionary[key] = setting;
			}

			// Special case to propagate Language to local property on assignment
			if(key == "Language")
			{
				this.Language = value;
			}

			setting.Value = value;
			setting.Commit();
		}

		public ApplicationDisplayType DisplayMode
		{
			get
			{
				if (this.get("ApplicationDisplayMode") == "touchscreen")
				{
					return ApplicationDisplayType.Touchscreen;
				}
				else
				{
					return ApplicationDisplayType.Responsive;
				}
			}
		}

		public bool IsTouchScreen => this.get("ApplicationDisplayMode") == "touchscreen";
	}
}