/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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

using System;
using MatterHackers.MatterControl.PrinterCommunication;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.Agg;
using System.Linq;
using System.Collections.Generic;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public enum NamedSettingsLayers { MHBaseSettings, OEMSettings, Quality, Material, User, All }

	public class ActiveSliceSettings
	{
		private static readonly string userDataPath = DataStorage.ApplicationDataStorage.ApplicationUserDataPath;
		private static readonly string profilesPath = Path.Combine(userDataPath, "Profiles");
		private static readonly string profilesDBPath = Path.Combine(profilesPath, "profiles.json");

		public static RootedObjectEventHandler ActivePrinterChanged = new RootedObjectEventHandler();

		private static SettingsProfile activeInstance = null;
		public static SettingsProfile Instance
		{
			get
			{
				return activeInstance;
			}
			set
			{
				if (activeInstance != value)
				{
					// If we have an active printer, run Disable otherwise skip to prevent empty ActiveSliceSettings due to null ActivePrinter
					if (activeInstance != null)
					{
						PrinterConnectionAndCommunication.Instance.Disable();
					}

					activeInstance = value;
					if (activeInstance != null)
					{
						BedSettings.SetMakeAndModel(activeInstance.Make, activeInstance.Model);
					}

					if (!MatterControlApplication.IsLoading)
					{
						OnActivePrinterChanged(null);
					}
				}
			}
		}

		static ActiveSliceSettings()
		{
			// Ensure the profiles directory exists
			Directory.CreateDirectory(profilesPath);

			if (true)
			{
				ProfileData = new ProfileData();
				
				foreach(string filePath in Directory.GetFiles(profilesPath, "*.json"))
				{
					string fileName = Path.GetFileName(filePath);
					if (fileName == "config.json" ||  fileName == "profiles.json")
					{
						continue;
					}

					try
					{
						var profile = new SettingsProfile(LayeredProfile.LoadFile(filePath));
						ProfileData.Profiles.Add(new PrinterInfo()
						{
							AutoConnect = profile.DoAutoConnect(),
							BaudRate = profile.BaudRate(),
							ComPort = profile.ComPort(),
							DriverType = profile.DriverType(),
							Id = profile.Id(),
							Make = profile.Make,
							Model = profile.Model,
							Name = profile.Name(),
						});
					}
					catch(Exception ex)
					{
						System.Diagnostics.Debug.WriteLine("Error loading profile: {1}\r\n{2}", filePath, ex.Message);
					}
				}
			}
			else
			{
				// Load or import the profiles.json document
				if (File.Exists(profilesDBPath))
				{
					ProfileData = JsonConvert.DeserializeObject<ProfileData>(File.ReadAllText(profilesDBPath));
				}
				else
				{
					ProfileData = new ProfileData();

					// Import class profiles from the db into local json files
					DataStorage.ClassicDB.ClassicSqlitePrinterProfiles.ImportPrinters(ProfileData, profilesPath);
					File.WriteAllText(profilesDBPath, JsonConvert.SerializeObject(ProfileData, Formatting.Indented));

					// TODO: Upload new profiles to webservice
				}
			}

			ActiveSliceSettings.LoadStartupProfile();
		}

		public static void SetActiveProfileID(int id)
		{
			UserSettings.Instance.set("ActiveProfileID", id.ToString());
		}

		public static LayeredProfile LoadEmptyProfile()
		{
			return new LayeredProfile(new OemProfile(), LoadMatterHackersBaseLayer());
		}

		public static ProfileData ProfileData { get; private set; }

		public static void LoadStartupProfile()
		{
			bool portExists = false;

			string[] comportNames = FrostedSerialPort.GetPortNames();

			string lastProfileID = UserSettings.Instance.get("ActiveProfileID");

			var startupProfile = LoadProfile(lastProfileID);
			if (startupProfile != null)
			{
				portExists = comportNames.Contains(startupProfile.ComPort());

				Instance = startupProfile;

				if (portExists && startupProfile.DoAutoConnect())
				{
					UiThread.RunOnIdle(() =>
					{
						//PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
						PrinterConnectionAndCommunication.Instance.ConnectToActivePrinter();
					}, 2);
				}
			}

			if(Instance == null)
			{
				// Load an empty profile with just the MatterHackers base settings from config.json
				Instance = new SettingsProfile(LoadEmptyProfile());
			}
		}

		internal static void SwitchToProfile(int id)
		{
			var profile = LoadProfile(id);

			SetActiveProfileID(id);

			if (profile != null)
			{
				Instance = profile;
			}
		}

		internal static SettingsProfile LoadProfile(int id)
		{
			string profileID = ProfileData.Profiles.Where(p => p.Id == id.ToString()).FirstOrDefault()?.Id.ToString();
			if (!string.IsNullOrEmpty(profileID))
			{
				return LoadProfile(profileID);
			}
			
			return null;
		}

		internal static void AcquireNewProfile(string make, string model, string printerName)
		{
			string guid = Guid.NewGuid().ToString();

			OemProfile printerProfile = LoadHttpOemProfile(make, model);
			SettingsLayer baseConfig = LoadMatterHackersBaseLayer();

			var layeredProfile = new LayeredProfile(
				printerProfile, 
				baseConfig);
			layeredProfile.DocumentPath = Path.Combine(profilesPath, guid + ".json");
			layeredProfile.Save();

			ProfileData.Profiles.Add(new PrinterInfo
			{
				Name = printerName,
				Id = guid
			});

			Instance = new SettingsProfile(layeredProfile);
		}

		internal static SettingsProfile LoadProfile(string profileID)
		{
			string profilePath = Path.Combine(profilesPath, profileID + ".json");
			return File.Exists(profilePath) ? LoadProfileFromDisk(profilePath) : null;
		}

		private static SettingsProfile LoadProfileFromDisk(string profilePath)
		{
			return new SettingsProfile(LayeredProfile.LoadFile(profilePath));
		}

		private static SettingsLayer LoadMatterHackersBaseLayer()
		{
			string baseConfigPath = Path.Combine(profilesPath, "config.json");
			if(!File.Exists(baseConfigPath))
			{
				string configIniPath = StaticData.Instance.MapPath(Path.Combine("PrinterSettings", "config.ini"));

				SettingsLayer baseLayer;

				using (var sourceStream = StaticData.Instance.OpenSteam(configIniPath))
				using (var reader = new StreamReader(sourceStream))
				{
					baseLayer = SettingsLayer.LoadFromIni(reader);
				}
				File.WriteAllText(baseConfigPath, JsonConvert.SerializeObject(baseLayer));

				return baseLayer;
			}

			return JsonConvert.DeserializeObject<SettingsLayer>(File.ReadAllText(baseConfigPath));
		}

		private static OemProfile LoadHttpOemProfile(string make, string model)
		{
			var client = new WebClient();
			string profileText = client.DownloadString(string.Format("http://matterdata.azurewebsites.net/api/oemprofiles/{0}/{1}/", make, model));
			var printerProfile = JsonConvert.DeserializeObject<OemProfile>(profileText);
			return printerProfile;
		}

		private static void OnActivePrinterChanged(EventArgs e)
		{
			ActivePrinterChanged.CallEvents(null, e);
		}

		/*
		private static SettingsProfile GetAutoConnectProfile(out bool connectionAvailable)
		{
			string[] comportNames = FrostedSerialPort.GetPortNames();

			var autoConnectPrinters = ProfileData.Profiles.Where(printer => printer.AutoConnect);
			foreach (var printer in autoConnectPrinters)
			{
				bool portIsAvailable = comportNames.Contains(printer.ComPort);
				if (portIsAvailable)
				{
					// We found a printer that we can select and connect to.
					connectionAvailable = true;
					return LoadProfile(printer.Id);
				}
			}

			// If not match was found about, replicate the behavior of the old autoconnect logic, which iterated and updated the 
			// printerToSelect instance, leaving it pointing at the last object before falling into this region
			PrinterInfo printerToSelect = autoConnectPrinters.LastOrDefault();

			connectionAvailable = printerToSelect != null;

			return connectionAvailable ? LoadProfile(printerToSelect.Id) : null;
		}

		private static SettingsProfile LoadBestProfile()
		{
			// Conceptually, load settings means
			// Read from state the currently selected profile/printer token
			//   - Check for/update/load the base MatterHackers layer
			//   - Check for/update/load the OEM layer
			//   - Set the quality layer to the currently selected quality profile
			//   - Set the material layer to the currently selected material profile
			//   - Check for/update/load the customer layer

			// Load profiles document

			var activeProfile = ProfileData.Profiles.Where(p => p.ProfileToken == ProfileData.ActiveProfileID).FirstOrDefault();
			if (activeProfile != null)
			{
				printerProfilePath = Path.Combine(profilesPath, activeProfile.ProfileToken + ".json");
			}

			// or this
			return LoadProfileFromDisk(printerProfilePath);
		} */
	}

	public enum SlicingEngineTypes { Slic3r, CuraEngine, MatterSlice };
}