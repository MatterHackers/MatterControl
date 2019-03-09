/*
Copyright (c) 2019, John Lewin
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

using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class SettingsValidationError : ValidationError
	{
		public SettingsValidationError(string settingsName)
			: base(settingsName)
		{
			this.CanonicalSettingsName = settingsName;
			this.PresentationName = PrinterSettings.SettingsData[settingsName].PresentationName;
		}

		public string CanonicalSettingsName { get; }

		public string PresentationName { get; }

		public string Location => SettingsLocation(this.CanonicalSettingsName);

		public string ValueDetails { get; set; }

		private static string SettingsLocation(string settingsKey)
		{
			var settingData = PrinterSettings.SettingsData[settingsKey];
			var setingsSectionName = settingData.OrganizerSubGroup.Group.Category.SettingsSection.Name;

			if (setingsSectionName == "Advanced")
			{
				setingsSectionName = "Slice Settings";
			}

			return "Location".Localize() + ":"
				 + "\n" + setingsSectionName.Localize()
				 + "\n  • " + settingData.OrganizerSubGroup.Group.Category.Name.Localize()
				 + "\n    • " + settingData.OrganizerSubGroup.Group.Name.Localize()
				 + "\n      • " + settingData.PresentationName.Localize();
		}
	}
}