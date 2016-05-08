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

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class GCodeMacro
	{
		public string Name { get; set; }
		public string GCode { get; set; }
		public DateTime LastModified { get; set; }
	}

	public class LayeredProfile
	{
		[JsonIgnore]
		internal SettingsLayer QualityLayer { get; private set; }

		[JsonIgnore]
		internal SettingsLayer MaterialLayer { get; private set; }

		public LayeredProfile(OemProfile printerProfile, SettingsLayer baseConfig)
		{
			this.OemProfile = printerProfile;
			this.BaseLayer = baseConfig;
		}

		public List<GCodeMacro> Macros { get; set; }

		[OnDeserialized]
		internal void OnDeserializedMethod(StreamingContext context)
		{
			QualityLayer = GetQualityLayer(ActiveQualityKey);
			MaterialLayer = GetMaterialLayer(ActiveMaterialKey); ;
		}

		public OemProfile OemProfile { get; set; }
		
		internal SettingsLayer GetMaterialLayer(string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				return null;
			}

			// Find the first matching layer in either the user or the OEM layers
			SettingsLayer layer = null;
			if (!MaterialLayers.TryGetValue(key, out layer))
			{
				OemProfile.MaterialLayers.TryGetValue(key, out layer);
			}

			return layer;
		}

		internal SettingsLayer GetQualityLayer(string key)
		{
			// Find the first matching layer in either the user or the OEM layers
			SettingsLayer layer = null;
			if (key != null && !QualityLayers.TryGetValue(key, out layer))
			{
				OemProfile.QualityLayers.TryGetValue(key, out layer);
			}
			
			return layer;
		}

		public string ActiveMaterialKey
		{
			get
			{
				return GetValue("MatterControl.ActiveMaterialKey");
			}
			internal set
			{
				SetActiveValue("MatterControl.ActiveMaterialKey", value);
				MaterialLayer = GetMaterialLayer(value);
				Save();
			}
		}

		public string ActiveQualityKey
		{
			get
			{
				return GetValue("MatterControl.ActiveQualityKey");
			}
			internal set
			{
				SetActiveValue("MatterControl.ActiveQualityKey", value);
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
				ActiveMaterialKey = materialKey;
				ApplicationController.Instance.ReloadAdvancedControlsPanel();
			}

			Save();
		}

		public List<string> MaterialSettingsKeys { get; set; } = new List<string>();

		public string DocumentPath { get; set; }

		internal void Save()
		{
			File.WriteAllText(DocumentPath, JsonConvert.SerializeObject(this));
		}

		/// <summary>
		/// User settings overrides
		/// </summary>
		public SettingsLayer UserLayer { get; } = new SettingsLayer();

		public IEnumerable<string> AllMaterialKeys()
		{
			return MaterialLayers.Keys.Union(this.OemProfile.MaterialLayers.Keys);
		}

		public IEnumerable<string> AllQualityKeys()
		{
			return QualityLayers.Keys.Union(this.OemProfile.QualityLayers.Keys);
		}

		internal static LayeredProfile LoadFile(string printerProfilePath)
		{
			var layeredProfile = JsonConvert.DeserializeObject<LayeredProfile>(File.ReadAllText(printerProfilePath));
			layeredProfile.DocumentPath = printerProfilePath;

			return layeredProfile;
		}

		// TODO: Hookup OEM layers
		/// <summary>
		/// Should contain both user created and oem specified material layers
		/// </summary>
		public Dictionary<string, SettingsLayer> MaterialLayers { get; } = new Dictionary<string, SettingsLayer>();

		// TODO: Hookup OEM layers
		/// <summary>
		/// Should contain both user created and oem specified quality layers
		/// </summary>
		public Dictionary<string, SettingsLayer> QualityLayers { get; } = new Dictionary<string, SettingsLayer>();


		///<summary>
		///Returns the settings value at the 'top' of the stack
		///</summary>
		public string GetValue(string sliceSetting)
		{
			return GetValue(sliceSetting, settingsLayers);
		}

		public string GetValue(string sliceSetting, IEnumerable<SettingsLayer> layers)
		{
			foreach (SettingsLayer layer in layers)
			{
				string value;
				if (layer.TryGetValue(sliceSetting, out value))
				{
					return value;
				}
			}

			return "";
		}

		public SettingsLayer BaseLayer { get; set; }

		private IEnumerable<SettingsLayer> settingsLayers
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

		internal void SetActiveValue(string sliceSetting, string sliceValue)
		{
			SetActiveValue(sliceSetting, sliceValue, UserLayer);
		}

		internal void SetActiveValue(string sliceSetting, string sliceValue, SettingsLayer layer)
		{
			layer[sliceSetting] = sliceValue;
			Save();
		}

		internal void ClearValue(string sliceSetting)
		{
			ClearValue(sliceSetting, UserLayer);
		}

		internal void ClearValue(string sliceSetting, SettingsLayer layer)
		{
			if(layer.ContainsKey(sliceSetting))
			{
				layer.Remove(sliceSetting);
			}

			// TODO: Reconsider this frequency
			Save();
		}
	}

	public class OemProfile
	{
		public OemProfile() { }

		public OemProfile(Dictionary<string, string> settingsDictionary)
		{
			OemLayer = new SettingsLayer(settingsDictionary);
		}

		/// <summary>
		/// Printer settings from OEM
		/// </summary>
		public SettingsLayer OemLayer { get; } = new SettingsLayer();

		/// <summary>
		/// List of Material presets from OEM
		/// </summary>
		public Dictionary<string, SettingsLayer> MaterialLayers { get; } = new Dictionary<string, SettingsLayer>();

		/// <summary>
		/// List of Quality presets from OEM
		/// </summary>
		public Dictionary<string, SettingsLayer> QualityLayers { get; } = new Dictionary<string, SettingsLayer>();
	}
}