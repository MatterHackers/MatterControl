using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl
{
	public static class UserSettingsKey
	{
		public const string ActiveThemeName = nameof(ActiveThemeName);
		public const string AfterPrintFinishedPlaySound = nameof(AfterPrintFinishedPlaySound);
		public const string AfterPrintFinishedSendEmail = nameof(AfterPrintFinishedSendEmail);
		public const string AfterPrintFinishedSendTextMessage = nameof(AfterPrintFinishedSendTextMessage);
		public const string ApplicationDisplayMode = nameof(ApplicationDisplayMode);
		public const string ApplicationTextSize = nameof(ApplicationTextSize);
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
		public const string SelectionTreeViewPanelExpanded = nameof(SelectionTreeViewPanelExpanded);
		public const string ShowContainers = nameof(ShowContainers);
		public const string ShownPrintCanceledMessage = nameof(ShownPrintCanceledMessage);
		public const string ShownPrintCompleteMessage = nameof(ShownPrintCompleteMessage);
		public const string ShownWelcomeMessage = nameof(ShownWelcomeMessage);
		public const string SliceSettingsTabIndex = nameof(SliceSettingsTabIndex);
		public const string SliceSettingsTabPinned = nameof(SliceSettingsTabPinned);
		public const string SliceSettingsWidget_CurrentTab = nameof(SliceSettingsWidget_CurrentTab);
		public const string SliceSettingsWidth = nameof(SliceSettingsWidth);
		public const string SnapGridDistance = nameof(SnapGridDistance);
		public const string SoftwareLicenseAccepted = nameof(SoftwareLicenseAccepted);
		public const string TerminalAutoUppercase = nameof(TerminalAutoUppercase);
		public const string TerminalTabVisible = nameof(TerminalTabVisible);
		public const string ThemeName = nameof(ThemeName);
		public const string ThumbnailRenderingMode = nameof(ThumbnailRenderingMode);
		public const string UpdateFeedType = nameof(UpdateFeedType);
		public const string MainTabKey = nameof(MainTabKey);
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

		public event EventHandler<StringEventArgs> SettingChanged;

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

		///<summary>
		///Returns the first matching value discovered while enumerating the settings layers
		///</summary>
		public T GetValue<T>(string settingsKey) where T : IConvertible
		{
			if (typeof(T) == typeof(string))
			{
				// this way we can use the common pattern without error
				return (T)(object)this.get(settingsKey);
			}
			else if (typeof(T) == typeof(bool))
			{
				return (T)(object)(this.get(settingsKey) == "1");
			}
			else if (typeof(T) == typeof(int))
			{
				int result;
				int.TryParse(this.get(settingsKey), out result);
				return (T)(object)(result);
			}
			else if (typeof(T) == typeof(double))
			{
				double.TryParse(this.get(settingsKey), out double result);
				return (T)(object)(result);
			}

			return (T)default(T);
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

			SettingChanged?.Invoke(this, new StringEventArgs(key));
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

		public bool IsTouchScreen
		{
			get
			{
#if __ANDROID__
				return true;
#else
				return this.get(UserSettingsKey.ApplicationDisplayMode) == "touchscreen";
#endif
			}
		}

		public string ThumbnailRenderingMode
		{
			get
			{
#if __ANDROID__
				// Always use flat thumbnails on Android - at least until alpha glitch is resolve and compute cost for thumbnails is reduced
				return "orthographic";
#else
				string renderingMode = this.get(UserSettingsKey.ThumbnailRenderingMode);
				if (string.IsNullOrWhiteSpace(renderingMode))
				{
					// If the current value is unset or invalid, use platform defaults
					return UserSettings.Instance.IsTouchScreen ? "orthographic" : "raytraced";
				}

				return renderingMode;
#endif
			}
			set
			{
				this.set(UserSettingsKey.ThumbnailRenderingMode, value);
			}
		}

		public bool ShowContainers
		{
			get
			{
				var showContainers = this.get(UserSettingsKey.ShowContainers);
				return string.IsNullOrWhiteSpace(showContainers) || showContainers == "1";
			}
		}
	}
}