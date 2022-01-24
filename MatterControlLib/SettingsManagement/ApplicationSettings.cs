/*
Copyright (c) 2020, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System.Collections.Generic;
using System.Linq;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SettingsManagement;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl
{
	public static class ApplicationSettingsKey
	{
		public const string ApplicationDisplayMode = nameof(ApplicationDisplayMode);
		public const string DesktopPosition = nameof(DesktopPosition);
		public const string HardwareHasCamera = nameof(HardwareHasCamera);
		public const string HideGCodeWarning = nameof(HideGCodeWarning);
		public const string MainWindowMaximized = nameof(MainWindowMaximized);
		public const string SuppressAuthPanel = nameof(SuppressAuthPanel);
		public const string WindowSize = nameof(WindowSize);
	}

	public class ApplicationSettings
	{
		public static string ValidFileExtensions { get; } = ".stl,.obj,.3mf,.amf,.mcx";

		public static string LibraryFilterFileExtensions { get; } = ValidFileExtensions + ",.gcode";

		public static string OpenDesignFileParams { get; } = "STL, AMF, OBJ, 3MF, GCODE, MCX|*.stl;*.amf;*.obj;*.gcode;*.mcx";

		private static ApplicationSettings globalInstance = null;

		public Dictionary<string, SystemSetting> SettingsDictionary { get; set; }

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

		public bool IsTouchScreen
		{
			get
			{
				return this.get(ApplicationSettingsKey.ApplicationDisplayMode) == "touchscreen";
			}
		}

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

		private string runningTokensKeyName = $"{ApplicationController.EnvironmentName}ClientToken_RunningTokens";

		private string claimedClientToken = null;

		public string GetClientToken()
		{
			if (!string.IsNullOrEmpty(claimedClientToken))
			{
				return claimedClientToken;
			}

			// This code should only run once per application and get cached in a local property (claimedClientToken)
			List<string> allocatedClientTokens = GetAllocatedClientTokens();
			HashSet<string> runningClientTokens = GetRunningClientTokens();

			if (ApplicationController.ApplicationInstanceCount == 1
				&& !string.IsNullOrEmpty(AuthenticationData.Instance.ActiveClientToken))
			{
				claimedClientToken = AuthenticationData.Instance.ActiveClientToken;
			}
			else
			{
				var availableTokens = allocatedClientTokens.Except(runningClientTokens);
				claimedClientToken = availableTokens.FirstOrDefault();
			}

			// Claim ClientToken
			if (!string.IsNullOrEmpty(claimedClientToken))
			{
				runningClientTokens.Add(claimedClientToken);
			}

			SetRunningClientTokens(runningClientTokens);

			return claimedClientToken;
		}

		public void ReleaseClientToken()
		{
			// Release ClientToken
			HashSet<string> runningClientTokens = GetRunningClientTokens();
			runningClientTokens.Remove(claimedClientToken);

			SetRunningClientTokens(runningClientTokens);
		}

		private List<string> GetAllocatedClientTokens()
		{
			List<string> allocatedClientTokens = new List<string>();
			string clientToken;
			int allocatedCount = 0;
			do
			{
				string keyName = $"{ApplicationController.EnvironmentName}ClientToken";

				if (allocatedCount > 0)
				{
					keyName += "_" + allocatedCount;
				}

				clientToken = get(keyName);
				if (!string.IsNullOrEmpty(clientToken))
				{
					allocatedClientTokens.Add(clientToken);
				}

				allocatedCount++;
			}
			while (!string.IsNullOrEmpty(clientToken));

			return allocatedClientTokens;
		}

		private HashSet<string> GetRunningClientTokens()
		{
			var runningClientTokens = new HashSet<string>();

			// Only deserialize if greater than one
			if (ApplicationController.ApplicationInstanceCount > 1)
			{
				try
				{
					string json = get(runningTokensKeyName);
					if (!string.IsNullOrEmpty(json))
					{
						runningClientTokens = JsonConvert.DeserializeObject<HashSet<string>>(json);
					}
				}
				catch { }
			}

			return runningClientTokens;
		}

		private void SetRunningClientTokens(HashSet<string> runningClientTokens)
		{
			set(runningTokensKeyName, JsonConvert.SerializeObject(runningClientTokens));
		}

		public void SetClientToken(string clientToken)
		{
			// Clear credentials anytime we are allocated a new client token
			AuthenticationData.Instance.ClearActiveSession();

			int allocatedCount = 0;

			bool firstEmptySlot = false;

			do
			{
				string keyName = $"{ApplicationController.EnvironmentName}ClientToken";

				if (allocatedCount > 0)
				{
					keyName += "_" + allocatedCount;
				}

				firstEmptySlot = string.IsNullOrEmpty(get(keyName));
				if (firstEmptySlot)
				{
					set(keyName, clientToken);
				}

				allocatedCount++;

			}
			while (!firstEmptySlot);
		}

		public string get(string key)
		{
			string result;
			if (SettingsDictionary == null)
			{
				globalInstance.LoadData();
			}

			if (SettingsDictionary.ContainsKey(key))
			{
				result = SettingsDictionary[key].Value;
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
			if (SettingsDictionary.ContainsKey(key))
			{
				setting = SettingsDictionary[key];
			}
			else
			{
				setting = new SystemSetting();
				setting.Name = key;

				SettingsDictionary[key] = setting;
			}

			setting.Value = value;
			setting.Commit();
		}

		private void LoadData()
		{
			SettingsDictionary = new Dictionary<string, SystemSetting>();
			foreach (SystemSetting s in GetApplicationSettings())
			{
				SettingsDictionary[s.Name] = s;
			}
		}

		private IEnumerable<SystemSetting> GetApplicationSettings()
		{
			// Retrieve SystemSettings from the Datastore
			return Datastore.Instance.dbSQLite.Query<SystemSetting>("SELECT * FROM SystemSetting;");
		}
	}
}