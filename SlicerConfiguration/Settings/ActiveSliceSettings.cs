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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public enum NamedSettingsLayers { MHBaseSettings, OEMSettings, Quality, Material, User, All }

	public static class ActiveSliceSettings
	{
		public static RootedObjectEventHandler ActivePrinterChanged = new RootedObjectEventHandler();
		public static RootedObjectEventHandler ActiveProfileModified = new RootedObjectEventHandler();
		public static RootedObjectEventHandler SettingChanged = new RootedObjectEventHandler();

		public static event EventHandler MaterialPresetChanged;

		public static PrinterSettings Instance => ApplicationController.Instance.ActivePrinter.Settings;

		public static void OnSettingChanged(string slicerConfigName)
		{
			SettingChanged.CallEvents(null, new StringEventArgs(slicerConfigName));
		}

		/// <summary>
		/// Switches to the ActivePrinter theme without firing the ThemeChanged event. This is useful when changing printers and
		/// allows the theme state to be updated before the ActivePrinterChanged event fires, resulting in a single ReloadAll
		/// occurring rather than two
		/// </summary>
		public static void SwitchToPrinterTheme()
		{
			if (ActiveSliceSettings.Instance.PrinterSelected)
			{
				//Attempt to load userSetting theme as default
				string activeThemeName = ActiveSliceSettings.Instance.GetValue(SettingsKey.active_theme_name);
				if (!string.IsNullOrEmpty(activeThemeName))
				{
					ActiveTheme.Instance = ActiveTheme.GetThemeColors(activeThemeName);
				}
			}
		}

		public static void OnActivePrinterChanged(EventArgs e)
		{
			ActivePrinterChanged.CallEvents(null, e);
		}

		internal static void OnMaterialPresetChanged()
		{
			MaterialPresetChanged?.Invoke(null, null);
		}
	}
}