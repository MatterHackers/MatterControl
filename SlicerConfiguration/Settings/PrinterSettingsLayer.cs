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
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
    using SettingsDictionary = Dictionary<string, string>;

    public class PrinterSettingsLayer : SettingsDictionary
	{
		public PrinterSettingsLayer() { }

		public PrinterSettingsLayer(Dictionary<string, string> settingsDictionary)
		{
			foreach(var keyValue in settingsDictionary)
			{
				this[keyValue.Key] = keyValue.Value;
			}
		}

		public string LayerID
		{
			get
			{
				string layerKey = ValueOrDefault("layer_id");
				if (string.IsNullOrEmpty(layerKey))
				{
					// Generate a new GUID when missing or empty. We can't do this in the constructor as the dictionary deserialization will fail if
					// an existing key exists for layer_id and on new empty layers, we still need to construct an initial identifier. 
					layerKey = Guid.NewGuid().ToString();
					LayerID = layerKey;
				}

				return layerKey;
			}
			set
			{
				this["layer_id"] = value;
			}
		}

		public string Name
		{
			get
			{
				return ValueOrDefault(SettingsKey.layer_name);
			}
			set
			{
				this[SettingsKey.layer_name] = value;
			}
		}

		public string Source
		{
			get
			{
				return ValueOrDefault("layer_source");
			}
			set
			{
				this["layer_source"] = value;
			}
		}

		public string ETag
		{
			get
			{
				return ValueOrDefault("layer_etag");
			}
			set
			{
				this["layer_etag"] = value;
			}
		}

		public string ValueOrDefault(string key, string defaultValue = "")
		{
			string foundValue;
			this.TryGetValue(key, out foundValue);

			return foundValue ?? defaultValue;
		}

		public static PrinterSettingsLayer LoadFromIni(TextReader reader)
		{
			var layer = new PrinterSettingsLayer();

			string line;
			while ((line = reader.ReadLine()) != null)
			{
				var segments = line.Split('=');
				if (!line.StartsWith("#") && !string.IsNullOrEmpty(line))
				{
					string key = segments[0].Trim();
					layer[key] = segments[1].Trim();
				}
			}

			return layer;
		}

		public static PrinterSettingsLayer LoadFromIni(string filePath)
		{
			var settings = from line in File.ReadAllLines(filePath)
						   let segments = line.Split('=')
						   where !line.StartsWith("#") && !string.IsNullOrEmpty(line) && segments.Length == 2
						   select new
						   {
							   Key = segments[0].Trim(),
							   Value = segments[1].Trim()
						   };

			var layer = new PrinterSettingsLayer();
			foreach (var setting in settings)
			{
				layer[setting.Key] = setting.Value;
			}

			return layer;
		}

		public PrinterSettingsLayer Clone()
		{
			string id = Guid.NewGuid().ToString();
			return new PrinterSettingsLayer(this as Dictionary<string, string>)
			{
				LayerID = id,
				Name = this.Name,
				ETag = this.ETag,
				Source = this.Source
			};
		}
	}

}