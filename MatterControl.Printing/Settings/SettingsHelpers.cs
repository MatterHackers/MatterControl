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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SettingsHelpers
	{
		private readonly PrinterSettings printerSettings;
		private PrintLevelingData _printLevelingData = null;

		public SettingsHelpers(PrinterSettings printerSettings)
		{
			this.printerSettings = printerSettings;
		}

		public double ExtruderTargetTemperature(int extruderIndex)
		{
			if (extruderIndex == 0)
			{
				return printerSettings.GetValue<double>(SettingsKey.temperature);
			}
			else
			{
				// Check if there is a material override for this extruder
				// Otherwise, use the SettingsLayers that is bound to this extruder
				if (extruderIndex < printerSettings.GetValue<int>(SettingsKey.extruder_count))
				{
					return printerSettings.GetValue<double>($"{SettingsKey.temperature}{extruderIndex}");
				}

				// else return the normal settings cascade
				return printerSettings.GetValue<double>(SettingsKey.temperature);
			}
		}

		public int[] LayerToPauseOn()
		{
			string[] userValues = printerSettings.GetValue("layer_to_pause").Split(';');

			return userValues.Where(v => int.TryParse(v, out int temp)).Select(v =>
			{
				// Convert from 0 based index to 1 based index
				int val = int.Parse(v);

				// Special case for user entered zero that pushes 0 to 1, otherwise val = val - 1 for 1 based index
				return val == 0 ? 1 : val - 1;
			}).ToArray();
		}

		public void SetBaudRate(string baudRate)
		{
			printerSettings.SetValue(SettingsKey.baud_rate, baudRate);
		}

		public string ComPort()
		{
			return printerSettings.GetValue($"{Environment.MachineName}_com_port");
		}

		public void SetComPort(string port)
		{
			printerSettings.SetValue($"{Environment.MachineName}_com_port", port);
		}

		public void SetComPort(string port, PrinterSettingsLayer layer)
		{
			printerSettings.SetValue($"{Environment.MachineName}_com_port", port, layer);
		}

		public void SetDriverType(string driver)
		{
			printerSettings.SetValue(SettingsKey.driver_type, driver);
		}

		public void SetDeviceToken(string token)
		{
			if (printerSettings.GetValue(SettingsKey.device_token) != token)
			{
				printerSettings.SetValue(SettingsKey.device_token, token);
			}
		}

		public void SetName(string name)
		{
			printerSettings.SetValue(SettingsKey.printer_name, name);
		}

		public PrintLevelingData PrintLevelingData
		{
			get
			{
				if (_printLevelingData == null)
				{
					// Load from settings if missing
					var jsonData = printerSettings.GetValue(SettingsKey.print_leveling_data);
					if (!string.IsNullOrEmpty(jsonData))
					{
						_printLevelingData = JsonConvert.DeserializeObject<PrintLevelingData>(jsonData);
					}

					// TODO: When this case is hit, it's certain to produce impossible to troubleshoot behavior where leveled printers suddenly act erratically.
					// Investigate a better solution - ideally we'd mark that leveling is invalid and have a validation error preventing printing/export/general use
					if (_printLevelingData == null)
					{
						_printLevelingData = new PrintLevelingData();
					}
				}

				return _printLevelingData;
			}

			set
			{
				// Store new reference
				_printLevelingData = value;

				// Persist to settings
				printerSettings.SetValue(SettingsKey.print_leveling_data, JsonConvert.SerializeObject(value));
			}
		}

		private readonly List<(Regex Regex, string Replacement)> _writeLineReplacements = new List<(Regex Regex, string Replacement)>();

		private string writeRegexString = "";

		private static readonly Regex GetQuotedParts = new Regex(@"([""'])(\\?.)*?\1", RegexOptions.Compiled);

		public List<(Regex Regex, string Replacement)> WriteLineReplacements
		{
			get
			{
				lock (_writeLineReplacements)
				{
					if (writeRegexString != printerSettings.GetValue(SettingsKey.write_regex))
					{
						_writeLineReplacements.Clear();
						writeRegexString = printerSettings.GetValue(SettingsKey.write_regex);

						foreach (string regExLine in writeRegexString.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries))
						{
							var matches = GetQuotedParts.Matches(regExLine);
							if (matches.Count == 2)
							{
								var search = matches[0].Value.Substring(1, matches[0].Value.Length - 2);
								var replace = matches[1].Value.Substring(1, matches[1].Value.Length - 2);
								_writeLineReplacements.Add((new Regex(search, RegexOptions.Compiled), replace));
							}
						}
					}
				}

				return _writeLineReplacements;
			}
		}

		public void DoPrintLeveling(bool doLeveling)
		{
			// Early exit if already set
			if (doLeveling == printerSettings.GetValue<bool>(SettingsKey.print_leveling_enabled))
			{
				return;
			}

			printerSettings.SetValue(SettingsKey.print_leveling_enabled, doLeveling ? "1" : "0");

			printerSettings.OnPrintLevelingEnabledChanged(this, null);
		}

		public void SetExtruderZOffset(int extruderIndex, double newZOffset)
		{
			// Get the existing offset, update Z, persist
			var offset = this.ExtruderOffset(extruderIndex);
			offset.Z = newZOffset;
			this.SetExtruderOffset(extruderIndex, offset);
		}

		public void SetExtruderOffset(int extruderIndex, Vector3 newOffset)
		{
			var offsetsVector3 = new List<Vector3>();
			string currentOffsets = printerSettings.GetValue(SettingsKey.extruder_offset);
			string[] offsets = currentOffsets.Split(',');

			foreach (string offset in offsets)
			{
				string[] xyz = offset.Split('x');
				if (xyz.Length == 2)
				{
					var zOffset = printerSettings.GetValue<double>(SettingsKey.z_offset);
					offsetsVector3.Add(new Vector3(double.Parse(xyz[0]), double.Parse(xyz[1]), -zOffset));
				}
				else
				{
					offsetsVector3.Add(new Vector3(double.Parse(xyz[0]), double.Parse(xyz[1]), double.Parse(xyz[2])));
				}
			}

			while (offsetsVector3.Count < extruderIndex)
			{
				offsetsVector3.Add(Vector3.Zero);
			}

			offsetsVector3[extruderIndex] = newOffset;

			// now save it
			var first = true;
			var newValue = "";
			foreach (var offset in offsetsVector3)
			{
				if (!first)
				{
					newValue += ",";
				}

				newValue += $"{offset.X:0.###}x{offset.Y:0.###}x{offset.Z:0.###}";
				first = false;
			}

			printerSettings.SetValue(SettingsKey.extruder_offset, newValue);
		}

		public Vector3 ExtruderOffset(int extruderIndex)
		{
			string currentOffsets = printerSettings.GetValue(SettingsKey.extruder_offset);
			string[] offsets = currentOffsets.Split(',');
			int count = 0;

			foreach (string offset in offsets)
			{
				if (count == extruderIndex)
				{
					string[] xyz = offset.Split('x');

					if (xyz.Length == 2)
					{
						// Import deprecated z_offset data if missing
						var zOffset = printerSettings.GetValue<double>(SettingsKey.z_offset);
						return new Vector3(double.Parse(xyz[0]), double.Parse(xyz[1]), -zOffset);
					}
					else
					{
						return new Vector3(double.Parse(xyz[0]), double.Parse(xyz[1]), double.Parse(xyz[2]));
					}
				}

				count++;
			}

			return Vector3.Zero;
		}

		public Vector3 ManualMovementSpeeds()
		{
			var feedRate = new Vector3(3000, 3000, 315);

			string savedSettings = printerSettings.GetValue(SettingsKey.manual_movement_speeds);
			if (!string.IsNullOrEmpty(savedSettings))
			{
				var segments = savedSettings.Split(',');
				feedRate.X = double.Parse(segments[1]);
				feedRate.Y = double.Parse(segments[3]);
				feedRate.Z = double.Parse(segments[5]);
			}

			return feedRate;
		}

		public Dictionary<string, double> GetMovementSpeeds()
		{
			var speeds = new Dictionary<string, double>();
			string movementSpeedsString = GetMovementSpeedsString();
			string[] allSpeeds = movementSpeedsString.Split(',');
			for (int i = 0; i < allSpeeds.Length / 2; i++)
			{
				speeds.Add(allSpeeds[i * 2 + 0], double.Parse(allSpeeds[i * 2 + 1]));
			}

			return speeds;
		}

		public string GetMovementSpeedsString()
		{
			string presets = "x,3000,y,3000,z,315,e0,150"; // stored x,value,y,value,z,value,e1,value,e2,value,e3,value,...

			string savedSettings = printerSettings.GetValue(SettingsKey.manual_movement_speeds);
			if (!string.IsNullOrEmpty(savedSettings))
			{
				presets = savedSettings;
			}

			return presets;
		}

		public int HotendCount()
		{
			if (printerSettings.GetValue<bool>(SettingsKey.extruders_share_temperature))
			{
				return 1;
			}

			return printerSettings.GetValue<int>(SettingsKey.extruder_count);
		}

		public bool UseZProbe()
		{
			return printerSettings.GetValue<bool>(SettingsKey.has_z_probe) && printerSettings.GetValue<bool>(SettingsKey.use_z_probe);
		}
	}
}
