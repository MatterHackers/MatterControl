using System.IO;
using System.Text.RegularExpressions;
using MatterHackers.Agg;
using MatterHackers.Localizations;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl
{
	public class AuthenticationData
	{
		public RootedObjectEventHandler AuthSessionChanged = new RootedObjectEventHandler();
		private static int failedRequestCount = int.MaxValue;

		public bool IsConnected
		{
			get
			{
				if (failedRequestCount > 5)
				{
					return false;
				}

				return true;
			}
		}

		/// <summary>
		/// Immediately push application into offline mode
		/// </summary>
		public void SetOffline()
		{
			failedRequestCount = 6;
		}

		public void WebRequestFailed()
		{
			failedRequestCount++;
		}

		public void WebRequestSucceeded()
		{
			failedRequestCount = 0;
		}

		public static AuthenticationData Instance { get; } = new AuthenticationData();

		public AuthenticationData()
		{
			activeSessionKey = ApplicationSettings.Instance.get($"{ApplicationController.EnvironmentName}ActiveSessionKey");
			activeSessionUsername = ApplicationSettings.Instance.get($"{ApplicationController.EnvironmentName}ActiveSessionUsername");
			activeSessionEmail = ApplicationSettings.Instance.get($"{ApplicationController.EnvironmentName}ActiveSessionEmail");
			lastSessionUsername = ApplicationSettings.Instance.get($"{ApplicationController.EnvironmentName}LastSessionUsername");
		}

		public void SessionRefresh()
		{
			// Called after completing a purchase (for example)
			AuthSessionChanged.CallEvents(null, null);
		}

		public void ClearActiveSession()
		{
			this.ActiveSessionKey = null;
			this.ActiveSessionUsername = null;
			this.ActiveSessionEmail = null;
			this.ActiveClientToken = null;

			ApplicationController.Instance.ChangeCloudSyncStatus(userAuthenticated: false, reason: "Session Cleared".Localize());
			AuthSessionChanged.CallEvents(null, null);
		}

		public void SetActiveSession(string userName, string userEmail, string sessionKey, string clientToken)
		{
			this.ActiveSessionKey = sessionKey;
			this.ActiveSessionUsername = userName;
			this.ActiveSessionEmail = userEmail;
			this.ActiveClientToken = clientToken;

			ApplicationController.Instance.ChangeCloudSyncStatus(userAuthenticated: true);
			AuthSessionChanged.CallEvents(null, null);
		}

		public bool ClientAuthenticatedSessionValid
		{
			get
			{
				return !string.IsNullOrEmpty(this.ActiveSessionKey)
					&& UserSettings.Instance.get(UserSettingsKey.CredentialsInvalid) != "true";
			}
		}

		private string activeSessionKey;

		public string ActiveSessionKey
		{
			get => activeSessionKey;
			private set
			{
				activeSessionKey = value;
				ApplicationSettings.Instance.set($"{ApplicationController.EnvironmentName}ActiveSessionKey", value);
			}
		}

		private string activeSessionUsername;

		public string ActiveSessionUsername
		{
			// Only return the ActiveSessionUserName if the ActiveSessionKey field is not empty
			get => string.IsNullOrEmpty(ActiveSessionKey) ? null : activeSessionUsername;
			private set
			{
				activeSessionUsername = value;
				ApplicationSettings.Instance.set($"{ApplicationController.EnvironmentName}ActiveSessionUsername", value);
			}
		}

		private string activeSessionEmail;

		public string ActiveSessionEmail
		{
			get => activeSessionEmail;
			private set
			{
				activeSessionEmail = value;
				ApplicationSettings.Instance.set($"{ApplicationController.EnvironmentName}ActiveSessionEmail", value);
			}
		}

		public string ActiveClientToken
		{
			get => ApplicationSettings.Instance.get($"{ApplicationController.EnvironmentName}ActiveClientToken");
			private set => ApplicationSettings.Instance.set($"{ApplicationController.EnvironmentName}ActiveClientToken", value);
		}

		private string lastSessionUsername;

		public string LastSessionUsername
		{
			get => lastSessionUsername;
			set
			{
				lastSessionUsername = value;
				ApplicationSettings.Instance.set($"{ApplicationController.EnvironmentName}LastSessionUsername", value);
			}
		}

		[JsonIgnore]
		public string FileSystemSafeUserName => ApplicationController.Instance.SanitizeFileName(this.ActiveSessionUsername);
	}
}