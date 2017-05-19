using MatterHackers.Agg;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SettingsManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using MatterHackers.Localizations;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl
{
	public class AuthenticationData
	{
		public RootedObjectEventHandler SessionUpdateTrigger = new RootedObjectEventHandler();
		static int failedRequestCount = int.MaxValue;

		public bool IsConnected
		{
			get
			{
				if(failedRequestCount > 5)
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
			//Called after completing a purchase (for example)
			SessionUpdateTrigger.CallEvents(null, null);
		}

		public void ClearActiveSession()
		{
			this.ActiveSessionKey = null;
			this.ActiveSessionUsername = null;
			this.ActiveSessionEmail = null;
			this.ActiveClientToken = null;

			ApplicationController.Instance.ChangeCloudSyncStatus(userAuthenticated: false, reason: "Session Cleared".Localize());
			SessionUpdateTrigger.CallEvents(null, null);
		}

		public void SetActiveSession(string userName, string userEmail, string sessionKey, string clientToken)
		{
			this.ActiveSessionKey = sessionKey;
			this.ActiveSessionUsername = userName;
			this.ActiveSessionEmail = userEmail;
			this.ActiveClientToken = clientToken;

			ApplicationController.Instance.ChangeCloudSyncStatus(userAuthenticated: true);
			SessionUpdateTrigger.CallEvents(null, null);
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
			get
			{
				return activeSessionKey;
			}
			private set
			{
				activeSessionKey = value;
				ApplicationSettings.Instance.set($"{ApplicationController.EnvironmentName}ActiveSessionKey", value);
			}
		}

		private string activeSessionUsername;
		public string ActiveSessionUsername
		{
			get
			{
				// Only return the ActiveSessionUserName if the ActiveSessionKey field is not empty
				return string.IsNullOrEmpty(ActiveSessionKey) ? null : activeSessionUsername;
			}
			private set
			{
				activeSessionUsername = value;
				ApplicationSettings.Instance.set($"{ApplicationController.EnvironmentName}ActiveSessionUsername", value);
			}
		}

		private string activeSessionEmail;
		public string ActiveSessionEmail
		{
			get
			{
				return activeSessionEmail;
			}
			private set
			{
				activeSessionEmail = value;
				ApplicationSettings.Instance.set($"{ApplicationController.EnvironmentName}ActiveSessionEmail", value);
			}
		}

		public string ActiveClientToken
		{
			get
			{
				return ApplicationSettings.Instance.get($"{ApplicationController.EnvironmentName}ActiveClientToken");
			}
			private set
			{
				ApplicationSettings.Instance.set($"{ApplicationController.EnvironmentName}ActiveClientToken", value);
			}
		}

		private string lastSessionUsername;
		public string LastSessionUsername
		{
			get
			{
				return lastSessionUsername;
			}
			set
			{
				lastSessionUsername = value;
				ApplicationSettings.Instance.set($"{ApplicationController.EnvironmentName}LastSessionUsername", value);
			}
		}

		[JsonIgnore]
		public string FileSystemSafeUserName => MakeValidFileName(this.ActiveSessionUsername);

		private static string MakeValidFileName(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return name;
			}

			string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
			string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

			return Regex.Replace(name, invalidRegStr, "_");
		}
	}
}