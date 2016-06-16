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
	public class PrinterSettings
	{
		public int DocumentVersion { get; set; }

		public string ID { get; set; }

		// Latest version should be 2016|06|08|1
		// Year|month|day|versionForDay (to support multiple revisions on a given day)
		public static int LatestVersion { get; } = 201606161;

		[JsonIgnore]
		internal PrinterSettingsLayer QualityLayer { get; private set; }

		[JsonIgnore]
		internal PrinterSettingsLayer MaterialLayer { get; private set; }

		public PrinterSettings(OemProfile printerProfile, PrinterSettingsLayer baseConfig)
		{
			this.OemProfile = printerProfile;
			this.BaseLayer = baseConfig;
		}

		public List<GCodeMacro> Macros { get; set; } = new List<GCodeMacro>();

		[OnDeserialized]
		internal void OnDeserializedMethod(StreamingContext context)
		{
			QualityLayer = GetQualityLayer(ActiveQualityKey);
		}

		public OemProfile OemProfile { get; set; }

		//public SettingsLayer OemLayer { get; set; }
		
		internal PrinterSettingsLayer GetMaterialLayer(string layerID)
		{
			if (string.IsNullOrEmpty(layerID))
			{
				return null;
			}

			return MaterialLayers.Where(layer => layer.LayerID == layerID).FirstOrDefault();
		}

		internal PrinterSettingsLayer GetQualityLayer(string layerID)
		{
			return QualityLayers.Where(layer => layer.LayerID == layerID).FirstOrDefault();
		}

		public string ActiveQualityKey
		{
			get
			{
				return GetValue("MatterControl.ActiveQualityKey");
			}
			internal set
			{
				SetValue("MatterControl.ActiveQualityKey", value);
				QualityLayer = GetQualityLayer(value);
				Save();
			}
		}

		public string GetMaterialPresetKey(int extruderIndex)
		{
			if (extruderIndex >= MaterialSettingsKeys.Count)
			{
				return null;
			}

			return MaterialSettingsKeys[extruderIndex];
		}

		public void SetMaterialPreset(int extruderIndex, string materialKey)
		{
			if (extruderIndex >= PrinterCommunication.PrinterConnectionAndCommunication.MAX_EXTRUDERS)
			{
				throw new ArgumentOutOfRangeException("Requested extruder index is outside of bounds: " + extruderIndex);
			}

			// TODO: This should really be in SettingsProfile and should be run when the extruder count changes
			if (MaterialSettingsKeys.Count <= extruderIndex)
			{
				var resizedArray = new string[extruderIndex + 1];
				MaterialSettingsKeys.CopyTo(resizedArray);
				MaterialSettingsKeys = new List<string>(resizedArray);
			}

			MaterialSettingsKeys[extruderIndex] = materialKey;

			if (extruderIndex == 0)
			{
				ApplicationController.Instance.ReloadAdvancedControlsPanel();
			}

			Save();
		}

		public List<string> MaterialSettingsKeys { get; set; } = new List<string>();

		private string GenerateSha1()
		{
			// Maybe be UTF8 encoded, may not...
			using (var fileStream = new FileStream(DocumentPath, FileMode.Open))
			using (var bufferedStream = new BufferedStream(fileStream, 1200000))
			{
				return GenerateSha1(bufferedStream);
			}
		}

		private string GenerateSha1(Stream stream)
		{
			// var timer = Stopwatch.StartNew();
			using (var sha1 = System.Security.Cryptography.SHA1.Create())
			{
				byte[] hash = sha1.ComputeHash(stream);
				string SHA1 = BitConverter.ToString(hash).Replace("-", String.Empty);

				// Console.WriteLine("{0} {1} {2}", SHA1, timer.ElapsedMilliseconds, filePath);
				return SHA1;
			}
		}
		
		private string DocumentPath => Path.Combine(ProfileManager.ProfilesPath, this.ID + ".json");

		internal void Save()
		{
			string json = JsonConvert.SerializeObject(this, Formatting.Indented);

			// SHA1 value is based on UTF8 encoded file contents
			using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
			{
				string sha1 = GenerateSha1(memoryStream);
				this.UserLayer["profile_sha1"] = sha1;

				var printerInfo = ProfileManager.Instance[this.ID];
				if (printerInfo != null)
				{
					printerInfo.SHA1 = sha1;
				}
			}

			File.WriteAllText(DocumentPath, json);
		}

		/// <summary>
		/// User settings overrides
		/// </summary>
		public PrinterSettingsLayer UserLayer { get; } = new PrinterSettingsLayer();

		internal static PrinterSettings LoadFile(string printerProfilePath)
		{
			var jObject = JObject.Parse(File.ReadAllText(printerProfilePath));

			int documentVersion = jObject?.GetValue("DocumentVersion")?.Value<int>() ?? 0;

			if (documentVersion < PrinterSettings.LatestVersion)
			{
				printerProfilePath = ProfileMigrations.MigrateDocument(printerProfilePath, documentVersion);
			}

			// Reload the document with the new schema
			return JsonConvert.DeserializeObject<PrinterSettings>(File.ReadAllText(printerProfilePath));
		}

		// TODO: Hookup OEM layers
		/// <summary>
		/// Should contain both user created and oem specified material layers
		/// </summary>
		public List<PrinterSettingsLayer> MaterialLayers { get; } = new List<PrinterSettingsLayer>();

		// TODO: Hookup OEM layers
		/// <summary>
		/// Should contain both user created and oem specified quality layers
		/// </summary>
		public List<PrinterSettingsLayer> QualityLayers { get; } = new List<PrinterSettingsLayer>();

		///<summary>
		///Returns the settings value at the 'top' of the stack
		///</summary>
		public string GetValue(string sliceSetting, IEnumerable<PrinterSettingsLayer> layerCascade = null)
		{
			if (layerCascade == null)
			{
				layerCascade = defaultLayerCascade;
			}

			foreach (PrinterSettingsLayer layer in layerCascade)
			{
				string value;
				if (layer.TryGetValue(sliceSetting, out value))
				{
					return value;
				}
			}

			return "";
		}

		public PrinterSettingsLayer BaseLayer { get; set; }

		private IEnumerable<PrinterSettingsLayer> defaultLayerCascade
		{
			get
			{
				if (this.UserLayer != null)
				{
					yield return this.UserLayer;
				}

				if (this.MaterialLayer != null)
				{
					yield return this.MaterialLayer;
				}

				if (this.QualityLayer != null)
				{
					yield return this.QualityLayer;
				}

				if (this.OemProfile.OemLayer != null)
				{
					yield return this.OemProfile.OemLayer;
				}

				yield return this.BaseLayer;
			}
		}

		internal void SetValue(string sliceSetting, string sliceValue)
		{
			SetValue(sliceSetting, sliceValue, UserLayer);
		}

		internal void SetValue(string sliceSetting, string sliceValue, PrinterSettingsLayer layer)
		{
			layer[sliceSetting] = sliceValue;
			Save();
		}

		internal void ClearValue(string sliceSetting)
		{
			ClearValue(sliceSetting, UserLayer);
		}

		internal void ClearValue(string sliceSetting, PrinterSettingsLayer layer)
		{
			if(layer.ContainsKey(sliceSetting))
			{
				layer.Remove(sliceSetting);
			}

			// TODO: Reconsider this frequency
			Save();
		}
	}
}