﻿/*
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
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public enum NamedSettingsLayers { MHBaseSettings, OEMSettings, Quality, Material, User, All }

	public class ActiveSliceSettings
	{
		public static RootedObjectEventHandler ActivePrinterChanged = new RootedObjectEventHandler();
		public static RootedObjectEventHandler ActiveProfileModified = new RootedObjectEventHandler();

		private static PrinterSettings activeInstance = null;
		public static PrinterSettings Instance
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
						BedSettings.SetMakeAndModel(activeInstance.GetValue(SettingsKey.make), activeInstance.GetValue(SettingsKey.model));
					}

					SwitchToPrinterTheme(MatterControlApplication.IsLoading);
					if (!MatterControlApplication.IsLoading)
					{
						OnActivePrinterChanged(null);

						string[] comportNames = FrostedSerialPort.GetPortNames();

						if (ActiveSliceSettings.Instance.PrinterSelected)
						{
							if (Instance.GetValue<bool>(SettingsKey.auto_connect))
							{
								UiThread.RunOnIdle(() =>
								{
								//PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
								PrinterConnectionAndCommunication.Instance.ConnectToActivePrinter();
								}, 2);
							}
						}
					}
				}
			}
		}

		public static void RefreshActiveInstance(PrinterSettings updatedProfile)
		{
			bool themeChanged = activeInstance.GetValue(SettingsKey.active_theme_index) != updatedProfile.GetValue(SettingsKey.active_theme_index);

			SliceSettingsWidget.SettingChanged.CallEvents(null, new StringEventArgs(SettingsKey.printer_name));

			activeInstance = updatedProfile;

			if (themeChanged)
			{
				UiThread.RunOnIdle(() => SwitchToPrinterTheme(true));
			}
			else
			{
				UiThread.RunOnIdle(ApplicationController.Instance.ReloadAdvancedControlsPanel);
			}
		}

		/// <summary>
		/// Switches to the ActivePrinter theme without firing the ThemeChanged event. This is useful when changing printers and
		/// allows the theme state to be updated before the ActivePrinterChanged event fires, resulting in a single ReloadAll
		/// occurring rather than two
		/// </summary>
		public static void SwitchToPrinterTheme(bool doReloadEvent)
		{
			int defaultThemeIndex = 1;

			int themeIndex;
			if (ActiveSliceSettings.Instance != null)
			{
				string activeThemeIndex = ActiveSliceSettings.Instance.GetValue(SettingsKey.active_theme_index);
				if (string.IsNullOrEmpty(activeThemeIndex) || !int.TryParse(activeThemeIndex, out themeIndex))
				{
					themeIndex = defaultThemeIndex;
				}

				if (!doReloadEvent)
				{
					ActiveTheme.SuspendEvents();
				}
				ActiveTheme.Instance = ActiveTheme.AvailableThemes[themeIndex];
				ActiveTheme.ResumeEvents();
			}
		}

		static ActiveSliceSettings()
		{
			// Load Startup Profile
			var startupProfile = ProfileManager.Instance.LoadLastProfile();
			if (startupProfile != null)
			{
				Instance = startupProfile;
			}

			if (Instance == null)
			{
				// Load an empty profile with just the MatterHackers base settings from config.json
				Instance = ProfileManager.LoadEmptyProfile();
			}
		}

		internal static async Task SwitchToProfile(string printerID)
		{
			var profile = ProfileManager.LoadProfile(printerID);

			ProfileManager.Instance.SetLastProfile(printerID);

			if (profile == null)
			{
				var printerInfo = ProfileManager.Instance[printerID];

				profile = await ApplicationController.GetPrinterProfileAsync(printerInfo, null);
			}

			Instance = profile ?? ProfileManager.LoadEmptyProfile();
		}

		private static void OnActivePrinterChanged(EventArgs e)
		{
			ActivePrinterChanged.CallEvents(null, e);
		}
	}

	public enum SlicingEngineTypes { Slic3r, CuraEngine, MatterSlice };
}