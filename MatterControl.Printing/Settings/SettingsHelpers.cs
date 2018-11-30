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

using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SettingsHelpers
	{
		private PrinterSettings printerSettings;

		public SettingsHelpers(PrinterSettings printerSettings)
		{
			this.printerSettings = printerSettings;
		}

		public double ExtruderTemperature(int extruderIndex)
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

			int temp;
			return userValues.Where(v => int.TryParse(v, out temp)).Select(v =>
			{
				//Convert from 0 based index to 1 based index
				int val = int.Parse(v);

				// Special case for user entered zero that pushes 0 to 1, otherwise val = val - 1 for 1 based index
				return val == 0 ? 1 : val - 1;
			}).ToArray();
		}

		internal double ParseDouble(string firstLayerValueString)
		{
			double firstLayerValue;
			if (!double.TryParse(firstLayerValueString, out firstLayerValue))
			{
				throw new Exception(string.Format("Format cannot be parsed. FirstLayerHeight '{0}'", firstLayerValueString));
			}
			return firstLayerValue;
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
			printerSettings.SetValue("driver_type", driver);
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

		public PrintLevelingData GetPrintLevelingData()
		{
			PrintLevelingData printLevelingData = null;
			var jsonData = printerSettings.GetValue(SettingsKey.print_leveling_data);
			if (!string.IsNullOrEmpty(jsonData))
			{
				printLevelingData = JsonConvert.DeserializeObject<PrintLevelingData>(jsonData);
			}

			// if it is still null
			if (printLevelingData == null)
			{
				printLevelingData = new PrintLevelingData();
			}

			return printLevelingData;
		}

		public void SetPrintLevelingData(PrintLevelingData data, bool clearUserZOffset)
		{
			if (clearUserZOffset)
			{
				printerSettings.SetValue(SettingsKey.baby_step_z_offset, "0");
			}

			printerSettings.SetValue(SettingsKey.print_leveling_data, JsonConvert.SerializeObject(data));
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

		public Vector2 ExtruderOffset(int extruderIndex)
		{
			string currentOffsets = printerSettings.GetValue("extruder_offset");
			string[] offsets = currentOffsets.Split(',');
			int count = 0;
			foreach (string offset in offsets)
			{
				if (count == extruderIndex)
				{
					string[] xy = offset.Split('x');
					return new Vector2(double.Parse(xy[0]), double.Parse(xy[1]));
				}
				count++;
			}

			return Vector2.Zero;
		}

		public void ExportAsCuraConfig()
		{
			throw new NotImplementedException();
		}

		public Vector3 ManualMovementSpeeds()
		{
			Vector3 feedRate = new Vector3(3000, 3000, 315);

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
			Dictionary<string, double> speeds = new Dictionary<string, double>();
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

		public int NumberOfHotends()
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
