/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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

using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class PrinterInfo
	{
		public string ComPort { get; set; }

		[JsonProperty(PropertyName = "ID")]
		private string id;

		[JsonIgnore]
		public string ID
		{
			get => id;
			set
			{
				// Update in memory state if IDs match
				if (ApplicationController.Instance.ActivePrinters.FirstOrDefault(p => p.Settings.ID == this.ID) is PrinterConfig activePrinter)
				{
					activePrinter.Settings.ID = value;
				}

				ProfileManager.Instance.ChangeID(this.ID, value);

				// Ensure the local file with the old ID moves with the new ID change
				string existingProfilePath = ProfilePath;

				if (File.Exists(existingProfilePath))
				{
					// Profile ID change must come after existingProfilePath calculation and before ProfilePath getter
					this.id = value;
					File.Move(existingProfilePath, ProfilePath);
				}
				else
				{
					this.id = value;
				}

				// If the local file exists and the PrinterInfo has been added to ProfileManager, then it's safe to call profile.Save, otherwise...
				if (File.Exists(ProfilePath) && ProfileManager.Instance[this.id] != null)
				{
					var profile = PrinterSettings.LoadFile(ProfilePath);
					profile.ID = value;
					profile.Save(userDrivenChange: false);
				}
			}
		}

		public string Name { get; set; }

		public string Make { get; set; }

		public string Model { get; set; }

		public string DeviceToken { get; set; }

		public bool IsDirty { get; set; } = false;

		public bool MarkedForDelete { get; set; } = false;

		public string ContentSHA1 { get; set; }

		public string ServerSHA1 { get; set; }

		[OnDeserialized]
		public void OnDeserialized(StreamingContext context)
		{
			if (string.IsNullOrEmpty(this.Make))
			{
				this.Make = "Other";
			}

			if (string.IsNullOrEmpty(this.Model))
			{
				this.Model = "Other";
			}
		}

		[JsonIgnore]
		public string ProfilePath => ProfileManager.Instance.ProfilePath(this);
	}
}
