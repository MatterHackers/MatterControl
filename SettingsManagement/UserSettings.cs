using System.Collections.Generic;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl
{
	public static class UserSettingsKey
	{
		public const string UpdateFeedType = nameof(UpdateFeedType);
		public const string ApplicationDisplayMode = nameof(ApplicationDisplayMode);
		public const string ApplicationTextSize = nameof(ApplicationTextSize);
		public const string defaultRenderSetting = nameof(defaultRenderSetting);
		public const string ThumbnailRenderingMode = nameof(ThumbnailRenderingMode);
		public const string CredentialsInvalid = nameof(CredentialsInvalid);
		public const string CredentialsInvalidReason = nameof(CredentialsInvalidReason);
		public const string SliceSettingsShowHelp = nameof(SliceSettingsShowHelp);
		public const string TerminalFilterOutput = nameof(TerminalFilterOutput);
		public const string TerminalAutoUppercase = nameof(TerminalAutoUppercase);
		public const string SliceSettingsTabPinned = nameof(SliceSettingsTabPinned);
		public const string SliceSettingsTabIndex = nameof(SliceSettingsTabIndex);
		public const string SliceSettingsWidth = nameof(SliceSettingsWidth);
		public const string SliceSettingsWidget_CurrentTab = nameof(SliceSettingsWidget_CurrentTab);
		public const string OpenScadPath = nameof(OpenScadPath);
		public const string LibraryViewWidth = nameof(LibraryViewWidth);
		public const string SelectedObjectPanelWidth = nameof(SelectedObjectPanelWidth);
		public const string ConfigurePrinter_CurrentTab = nameof(ConfigurePrinter_CurrentTab);
	}

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
				// LastValueWins - allow for duplicate entries in the database with the same .Name value 
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
							ToolTipManager.AllowToolTips = !UserSettings.Instance.IsTouchScreen;
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

		public double LibraryViewWidth
		{
			get
			{
				if (!double.TryParse(this.get(UserSettingsKey.LibraryViewWidth), out double controlWidth))
				{
					// Default to 254 if no UserSetting value exists
					controlWidth = 254 * GuiWidget.DeviceScale;
				}

				return controlWidth;
			}
			set
			{
				this.set(UserSettingsKey.LibraryViewWidth, value.ToString());
			}
		}

		public bool IsTouchScreen => this.get(UserSettingsKey.ApplicationDisplayMode) == "touchscreen";
	}
}