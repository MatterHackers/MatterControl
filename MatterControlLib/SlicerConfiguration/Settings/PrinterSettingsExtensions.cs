/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.IO;
using System.Linq;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public static class PrinterSettingsExtensions
	{
		private static Dictionary<string, string> blackListSettings = new Dictionary<string, string>()
		{
			[SettingsKey.spiral_vase] = "0",
			[SettingsKey.layer_to_pause] = "",
			[SettingsKey.print_leveling_data] = "",
			[SettingsKey.print_leveling_enabled] = "0",
			[SettingsKey.probe_has_been_calibrated] = "0",
			[SettingsKey.baby_step_z_offset] = "0",
			[SettingsKey.xy_offsets_have_been_calibrated] = "0",
			[SettingsKey.filament_has_been_loaded] = "0",
			[SettingsKey.filament_1_has_been_loaded] = "0"
		};

		private static object writeLock = new object();

		public static bool AutoSave { get; set; } = true;

		public static void ClearBlackList(this PrinterSettings settings)
		{
			foreach (var kvp in blackListSettings)
			{
				if (settings.UserLayer.ContainsKey(kvp.Key))
				{
					settings.UserLayer.Remove(kvp.Key);
				}

				settings.OemLayer[kvp.Key] = kvp.Value;
			}
		}

		public static void Save(this PrinterSettings settings, bool userDrivenChange = true)
		{
			// Skip save operation if on the EmptyProfile
			if (!settings.PrinterSelected || !AutoSave)
			{
				return;
			}

			settings.Save(
				ProfileManager.Instance.ProfilePath(settings.ID),
				userDrivenChange);
		}

		public static void Save(this PrinterSettings settings, string filePath, bool userDrivenChange = true)
		{
			// TODO: Rewrite to be owned by ProfileManager and simply mark as dirty and every n period persist and clear dirty flags
			lock (writeLock)
			{
				string json = settings.ToJson();

				var printerInfo = ProfileManager.Instance[settings.ID];
				if (printerInfo != null)
				{
					printerInfo.ContentSHA1 = settings.ComputeSHA1(json);

					if (printerInfo.ServerSHA1 == printerInfo.ContentSHA1)
					{
						// Any change that results in our content arriving at the last known server content fingerprint, should clear the dirty flag
						printerInfo.IsDirty = false;
					}
					else
					{
						printerInfo.IsDirty |= userDrivenChange;
					}

					ProfileManager.Instance.Save();
				}

				File.WriteAllText(filePath, json);
			}

			if (ApplicationController.Instance.ActivePrinters.FirstOrDefault(p => p.Settings.ID == settings.ID) is PrinterConfig printer)
			{
				ApplicationController.Instance.ActiveProfileModified.CallEvents(printer.Settings, null);
			}
		}
	}
}
