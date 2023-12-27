﻿using System;
using System.Collections.Generic;
using System.Globalization;
using g3;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
    public static class UserSettingsKey
	{
		public const string ActiveThemeName = nameof(ActiveThemeName);
		public const string AfterPrintFinishedPlaySound = nameof(AfterPrintFinishedPlaySound);
		public const string AfterPrintFinishedSendEmail = nameof(AfterPrintFinishedSendEmail);
		public const string AfterPrintFinishedSendTextMessage = nameof(AfterPrintFinishedSendTextMessage);
		public const string ApplicationUseHeigResDisplays = nameof(ApplicationUseHeigResDisplays);
		public const string ApplicationUseSocketeer = nameof(ApplicationUseSocketeer);
		public const string ApplicationTextSize = nameof(ApplicationTextSize);
		public const string ApplyLevelingDurringExport = nameof(ApplyLevelingDurringExport);
		public const string CollectPrintHistoryData = nameof(CollectPrintHistoryData);
		public const string ColorPanelExpanded = nameof(ColorPanelExpanded);
		public const string ConfigurePrinter_CurrentTab = nameof(ConfigurePrinter_CurrentTab);
		public const string ConfigurePrinterTabVisible = nameof(ConfigurePrinterTabVisible);
		public const string ControlsTabVisible = nameof(ControlsTabVisible);
		public const string CredentialsInvalid = nameof(CredentialsInvalid);
		public const string CredentialsInvalidReason = nameof(CredentialsInvalidReason);
		public const string defaultRenderSetting = nameof(defaultRenderSetting);
		public const string DisplayedTip_LoadFilament = nameof(DisplayedTip_LoadFilament);
		public const string EditorPanelExpanded = nameof(EditorPanelExpanded);
		public const string FavoritesBarExpansion= nameof(FavoritesBarExpansion);
		public const string GCodeLineColorStyle = nameof(GCodeLineColorStyle);
		public const string GcodeModelView = nameof(GcodeModelView);
		public const string GcodeViewerRenderGrid = nameof(GcodeViewerRenderGrid);
		public const string GcodeViewerRenderMoves = nameof(GcodeViewerRenderMoves);
		public const string GcodeViewerRenderRetractions = nameof(GcodeViewerRenderRetractions);
		public const string GcodeViewerSimulateExtrusion = nameof(GcodeViewerSimulateExtrusion);
		public const string GcodeViewerTransparentExtrusion = nameof(GcodeViewerTransparentExtrusion);
		public const string TerminalShowInputOutputMarks = nameof(TerminalShowInputOutputMarks);
		public const string TerminalShowChecksum = nameof(TerminalShowChecksum);
		public const string TerminalShowOks = nameof(TerminalShowOks);
		public const string TerminalShowMovementRequests = nameof(TerminalShowMovementRequests);
		public const string TerminalShowTempRequests = nameof(TerminalShowTempRequests);
		public const string TerminalShowTempResponse = nameof(TerminalShowTempResponse);
		public const string TerminalShowWaitResponse = nameof(TerminalShowWaitResponse);
		public const string Language = nameof(Language);
		public const string LayerViewDefault = nameof(LayerViewDefault);
		public const string LayerViewSyncToPrint = nameof(LayerViewSyncToPrint);
		public const string LibraryViewWidth = nameof(LibraryViewWidth);
		public const string MaterialsPanelExpanded = nameof(MaterialsPanelExpanded);
		public const string MirrorPanelExpanded = nameof(MirrorPanelExpanded);
		public const string NotificationEmailAddress = nameof(NotificationEmailAddress);
		public const string NotificationPhoneNumber = nameof(NotificationPhoneNumber);
		public const string OpenScadPath = nameof(OpenScadPath);
		public const string PerspectiveMode = nameof(PerspectiveMode);
		public const string SupportGenerationType = nameof(SupportGenerationType);
		public const string SupportMaxOverHangAngle = nameof(SupportMaxOverHangAngle);
		public const string SupportPillarSize = nameof(SupportPillarSize);
		public const string PopupLibraryWidth = nameof(PopupLibraryWidth);
		public const string PrintHistoryFilterShowCompleted = nameof(PrintHistoryFilterShowCompleted);
		public const string PrintNotificationsEnabled = nameof(PrintNotificationsEnabled);
		public const string PrintNotificationsIncludeImage = nameof(PrintNotificationsIncludeImage);
		public const string PublicProfilesSha = nameof(PublicProfilesSha);
		public const string ScalePanelExpanded = nameof(ScalePanelExpanded);
		public const string SceneTreeRatio = nameof(SceneTreeRatio);
		public const string SelectedObjectEditorHeight = nameof(SelectedObjectEditorHeight);
		public const string SelectedObjectPanelWidth = nameof(SelectedObjectPanelWidth);
		public const string NodeEditorPanelHeight = nameof(NodeEditorPanelHeight);
        public const string SelectionTreeViewPanelExpanded = nameof(SelectionTreeViewPanelExpanded);
		public const string ShownWelcomeMessage = nameof(ShownWelcomeMessage);
		public const string SliceSettingsTabIndex = nameof(SliceSettingsTabIndex);
		public const string SliceSettingsTabPinned = nameof(SliceSettingsTabPinned);
		public const string SliceSettingsWidget_CurrentTab = nameof(SliceSettingsWidget_CurrentTab);
		public const string SliceSettingsWidth = nameof(SliceSettingsWidth);
		public const string SliceSettingsViewDetail = nameof(SliceSettingsViewDetail);
		public const string SliceSettingsMoreClicked = nameof(SliceSettingsMoreClicked);
		public const string SnapGridDistance = nameof(SnapGridDistance);
		public const string SoftwareLicenseAccepted = nameof(SoftwareLicenseAccepted);
		public const string TerminalAutoUppercase = nameof(TerminalAutoUppercase);
		public const string TerminalTabVisible = nameof(TerminalTabVisible);
		public const string ThemeName = nameof(ThemeName);
		public const string ThumbnailRenderingMode = nameof(ThumbnailRenderingMode);
		public const string TurntableMode = nameof(TurntableMode);
		public const string UpdateFeedType = nameof(UpdateFeedType);
		public const string MainTabKey = nameof(MainTabKey);
	}

	public class UserSettings
	{
		private static UserSettings globalInstance = null;

		private static readonly object SyncRoot = new object();

		private Dictionary<string, UserSetting> settingsDictionary;

		private UserSettings()
		{
			// Load the UserSettings from the database
			settingsDictionary = new Dictionary<string, UserSetting>();
			foreach (var userSetting in Datastore.Instance.dbSQLite.Query<UserSetting>("SELECT * FROM UserSetting;"))
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

		public event EventHandler<StringEventArgs> SettingChanged;

		public static UserSettings Instance
		{
			get
			{
				if (globalInstance == null)
				{
					lock (SyncRoot)
					{
						if (globalInstance == null)
						{
							globalInstance = new UserSettings();
							ToolTipManager.AllowToolTips = !GuiWidget.TouchScreenMode;
						}
					}
				}

				return globalInstance;
			}
		}

		public string Language { get; private set; }

		public UserSettingsFields Fields { get; private set; } = new UserSettingsFields();

		/// <summary>
		/// Returns the first matching value discovered while enumerating the settings layers
		/// </summary>
		/// <typeparam name="T">The type of value being gotten</typeparam>
		/// <param name="settingsKey">The name of the value to get</param>
		/// <returns>The typed value</returns>
		public T GetValue<T>(string settingsKey, T defaultValue)
		{
			var settingValue = this.get(settingsKey);

			if (settingValue == null)
			{
				return defaultValue;
			}

			if (typeof(T) == typeof(string))
			{
				// this way we can use the common pattern without error
				return (T)(object)settingValue;
			}
			else if (typeof(T) == typeof(bool))
			{
				if (settingValue == "True")
				{
                    settingValue = "1";
                }
				return (T)(object)(settingValue == "1");
			}
			else if (typeof(T) == typeof(int))
			{
				int result;
				int.TryParse(settingValue, out result);
				return (T)(object)result;
			}
			else if (typeof(T) == typeof(double))
			{
				double.TryParse(settingValue, out double result);
				return (T)(object)result;
			}
            else if (typeof(T) == typeof(Vector2))
            {
                var parts = settingValue.Split(new[] { 'X', ':', ',', 'Y', ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && double.TryParse(parts[0].Trim(), out double x) && double.TryParse(parts[1].Trim(), out double y))
                {
                    Vector2 result = new Vector2(x, y);
                    return (T)(object)result;
                }
                // Else, handle the case when the parsing fails...
            }

            return (T)default(T);
		}

        public void SetValue<T>(string key, T value)
        {
            if (typeof(T) == typeof(bool))
            {
                set(key, ((bool)(object)value) ? "1" : "0");
            }
            else if (value is Vector2 vector)
            {
                set(key, $"X:{vector.X},Y:{vector.Y}");
            }
            else if (value is IConvertible convertible)
            {
                set(key, convertible.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                set(key, value.ToString());
            }
        }

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

			if (!settingsDictionary.TryGetValue(key, out setting))
			{
				// If the setting for the given key doesn't exist, create it
				setting = new UserSetting()
				{
					Name = key
				};
				settingsDictionary[key] = setting;
			}

			// Special case to propagate Language to local property on assignment
			if (key == "Language")
			{
				this.Language = value;
			}

			if (setting.Value != value)
			{
				setting.Value = value;
				setting.Commit();

				SettingChanged?.Invoke(this, new StringEventArgs(key));
			}
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

		public string ThumbnailRenderingMode
		{
			get
			{
				string renderingMode = this.get(UserSettingsKey.ThumbnailRenderingMode);
				if (string.IsNullOrWhiteSpace(renderingMode))
				{
					// If the current value is unset or invalid, use platform defaults
					return GuiWidget.TouchScreenMode ? "orthographic" : "raytraced";
				}

				return renderingMode;
			}

			set
			{
				this.set(UserSettingsKey.ThumbnailRenderingMode, value);
			}
		}

        public double NodeEditorPanelHeight
        {
            get
            {
                if (double.TryParse(UserSettings.Instance.get(UserSettingsKey.NodeEditorPanelHeight), out double controlWidth))
                {
                    return Math.Max(controlWidth, 150);
                }

                return 200;
            }

            set
            {
                var minimumValue = Math.Max(value, 150);
                UserSettings.Instance.set(UserSettingsKey.NodeEditorPanelHeight, minimumValue.ToString());
            }
        }

        public double SelectedObjectPanelWidth
		{
			get
			{
				if (double.TryParse(UserSettings.Instance.get(UserSettingsKey.SelectedObjectPanelWidth), out double controlWidth))
				{
					return Math.Max(controlWidth, 150);
				}

				return 200;
			}

			set
			{
				var minimumValue = Math.Max(value, 150);
				UserSettings.Instance.set(UserSettingsKey.SelectedObjectPanelWidth, minimumValue.ToString());
			}
		}
	}
}