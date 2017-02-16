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

using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Text;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public static class ProfileMigrations
	{
		public static string MigrateDocument(string filePath, int fromVersion = -1)
		{
			var jObject = JObject.Parse(File.ReadAllText(filePath));

			/*
			if (fromVersion < 201605131)
			{
				var materialLayers = jObject["MaterialLayers"] as JObject;
				if (materialLayers != null)
				{
					foreach (JProperty layer in materialLayers.Properties().ToList())
					{
						layer.Remove();

						string layerID = Guid.NewGuid().ToString();

						var body = layer.Value as JObject;
						body["MatterControl.LayerID"] = layerID;
						body["MatterControl.LayerName"] = layer.Name;

						materialLayers[layerID] = layer.Value;
					}
				}

				var qualityLayers = jObject["QualityLayers"] as JObject;
				if (qualityLayers != null)
				{
					foreach (JProperty layer in qualityLayers.Properties().ToList())
					{
						layer.Remove();

						string layerID = Guid.NewGuid().ToString();

						var body = layer.Value as JObject;
						body["MatterControl.LayerID"] = layerID;
						body["MatterControl.LayerName"] = layer.Name;

						qualityLayers[layerID] = layer.Value;
					}
				}


				jObject["DocumentVersion"] = 201605131;
			}

			if (fromVersion < 201605132)
			{
				string printerID = Guid.NewGuid().ToString();
				jObject.Remove("DocumentPath");
				jObject["ID"] = printerID;
				jObject["DocumentVersion"] = 201605132;

				File.Delete(filePath);
				filePath = Path.Combine(Path.GetDirectoryName(filePath), printerID + ProfileManager.ProfileExtension);
			}
			*/

			if (fromVersion < 201606271)
			{
				JObject oemProfile = jObject["OemProfile"] as JObject;
				if (oemProfile != null)
				{
					jObject.Property("OemProfile").Remove();
					jObject["OemLayer"] = oemProfile["OemLayer"];

				}
				jObject["DocumentVersion"] = 201606271;
			}

			File.WriteAllText(
						filePath,
						JsonConvert.SerializeObject(jObject, Formatting.Indented));

			return filePath;
		}
	}
}