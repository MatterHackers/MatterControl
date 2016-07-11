using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SettingsManagement;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using System.Linq;

namespace MatterHackers.MatterControl
{
	public static class ApplicationSettingsKey
	{
		public const string SuppressAuthPanel = nameof(SuppressAuthPanel);
		public const string HardwareHasCamera = nameof(HardwareHasCamera);
		public const string HideGCodeWarning = nameof(HideGCodeWarning);
		public const string DesktopPosition = nameof(DesktopPosition);
		public const string WindowSize = nameof(WindowSize);
	}

	public class ApplicationSettings
	{
		public static string LibraryFilterFileExtensions { get { return ".stl,.amf,.gcode,.mcx"; } }

		public static string OpenPrintableFileParams { get { return "STL, AMF, ZIP, GCODE, MCX|*.stl;*.amf;*.zip;*.gcode;*.mcx"; } }

		public static string OpenDesignFileParams { get { return "STL, AMF, ZIP, GCODE, MCX|*.stl;*.amf;*.zip;*.gcode;*.mcx" ; } }

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

			} while (!string.IsNullOrEmpty(clientToken));
			return allocatedClientTokens;
		}

		private HashSet<string> GetRunningClientTokens()
		{
			var  runningClientTokens = new HashSet<string>();

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

			} while (!firstEmptySlot);
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