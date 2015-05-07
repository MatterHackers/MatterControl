using MatterHackers.Agg;
using System;
using System.Collections.Generic;
using System.Text;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public static class GCodeProcessing
	{
		private static Dictionary<string, string> replaceWithSettingsStrings = new Dictionary<string, string>()
        {
			// Have a mapping so that MatterSlice while always use a setting that can be set. (the user cannot set first_layer_bedTemperature in MatterSlice)
			{"first_layer_temperature", "temperature"},
			{"temperature","temperature"},
			{"first_layer_bed_temperature","bed_temperature"},
			{"bed_temperature","bed_temperature"},
			{"bed_remove_part_temperature","bed_remove_part_temperature"},
			{"extruder_wipe_temperature","extruder_wipe_temperature"},
			{"z_offset","z_offset"},
			{"retract_length","retract_length"},
			{"filament_diameter","filament_diameter"},
			{"first_layer_speed","first_layer_speed"},
			{"infill_speed","infill_speed"},
			{"max_fan_speed","max_fan_speed"},
			{"min_fan_speed","min_fan_speed"},
			{"min_print_speed","min_print_speed"},
			{"perimeter_speed","perimeter_speed"},
			{"retract_speed","retract_speed"},
			{"support_material_speed","support_material_speed"},
			{"travel_speed","travel_speed"},
			{"bridge_fan_speed","bridge_fan_speed"},
			{"bridge_speed","bridge_speed"},
			{"raft_print_speed","raft_print_speed"},
			{"external_perimeter_speed","external_perimeter_speed"},
		};

		public static string ReplaceMacroValues(string gcodeWithMacros)
		{
			foreach (KeyValuePair<string, string> keyValue in replaceWithSettingsStrings)
			{
				// do the replacement with {} (curly brackets)
				{
					string thingToReplace = "{" + "{0}".FormatWith(keyValue.Key) + "}";
					gcodeWithMacros = gcodeWithMacros.Replace(thingToReplace, ActiveSliceSettings.Instance.GetActiveValue(keyValue.Value));
				}
				// do the replacement with [] (square brackets) Slic3r uses only square brackets
				{
					string thingToReplace = "[" + "{0}".FormatWith(keyValue.Key) + "]";
					gcodeWithMacros = gcodeWithMacros.Replace(thingToReplace, ActiveSliceSettings.Instance.GetActiveValue(keyValue.Value));
				}
			}

			return gcodeWithMacros;
		}
	}

	public class MapItem
	{
		private string mappedKey;
		private string originalKey;

		public MapItem(string mappedKey, string originalKey)
		{
			this.mappedKey = mappedKey;
			this.originalKey = originalKey;
		}

		protected static double ParseValueString(string valueString, double valueOnError = 0)
		{
			double value = valueOnError;

			if (!double.TryParse(valueString, out value))
			{
#if DEBUG
				throw new Exception("Slicing value is not a double.");
#endif
			}

			return value;
		}

		public static double GetValueForKey(string originalKey, double valueOnError = 0)
		{
			return ParseValueString(ActiveSliceSettings.Instance.GetActiveValue(originalKey), valueOnError);
		}

		public string MappedKey { get { return mappedKey; } }

		public string OriginalKey { get { return originalKey; } }

		public string OriginalValue { get { return ActiveSliceSettings.Instance.GetActiveValue(originalKey); } }

		public virtual string MappedValue { get { return OriginalValue; } }
	}

	public class VisibleButNotMappedToEngine : MapItem
	{
		public override string MappedValue
		{
			get
			{
				return null;
			}
		}

		public VisibleButNotMappedToEngine(string originalKey)
			: base("", originalKey)
		{
		}
	}

	public class MapStartGCode : InjectGCodeCommands
	{
		private bool replaceCRs;

		public override string MappedValue
		{
			get
			{
				StringBuilder newStartGCode = new StringBuilder();
				foreach (string line in PreStartGCode(SlicingQueue.extrudersUsed))
				{
					newStartGCode.Append(line + "\n");
				}

				newStartGCode.Append(GCodeProcessing.ReplaceMacroValues(base.MappedValue));

				foreach (string line in PostStartGCode(SlicingQueue.extrudersUsed))
				{
					newStartGCode.Append("\n");
					newStartGCode.Append(line);
				}

				if (replaceCRs)
				{
					return newStartGCode.ToString().Replace("\n", "\\n");
				}

				return newStartGCode.ToString();
			}
		}

		public MapStartGCode(string mappedKey, string originalKey, bool replaceCRs)
			: base(mappedKey, originalKey)
		{
			this.replaceCRs = replaceCRs;
		}

		public List<string> PreStartGCode(List<bool> extrudersUsed)
		{
			string startGCode = ActiveSliceSettings.Instance.GetActiveValue("start_gcode");
			string[] preStartGCodeLines = startGCode.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);

			List<string> preStartGCode = new List<string>();
			preStartGCode.Add("; automatic settings before start_gcode");
			AddDefaultIfNotPresent(preStartGCode, "G21", preStartGCodeLines, "set units to millimeters");
			AddDefaultIfNotPresent(preStartGCode, "M107", preStartGCodeLines, "fan off");
			double bed_temperature = ActiveSliceSettings.Instance.BedTemperature;
			if (bed_temperature > 0)
			{
				string setBedTempString = string.Format("M190 S{0}", bed_temperature);
				AddDefaultIfNotPresent(preStartGCode, setBedTempString, preStartGCodeLines, "wait for bed temperature to be reached");
			}

			int numberOfHeatedExtruders = 1;
			if (!ActiveSliceSettings.Instance.ExtrudersShareTemperature)
			{
				numberOfHeatedExtruders = ActiveSliceSettings.Instance.ExtruderCount;
			}

			// Start heating all the extruder that we are going to use.
			for (int extruderIndex = 0; extruderIndex < numberOfHeatedExtruders; extruderIndex++)
			{
				if (extrudersUsed.Count > extruderIndex
					&& extrudersUsed[extruderIndex])
				{
					string materialTemperature = ActiveSliceSettings.Instance.GetMaterialValue("temperature", extruderIndex);
					if (materialTemperature != "0")
					{
						string setTempString = "M104 T{0} S{1}".FormatWith(extruderIndex, materialTemperature);
						AddDefaultIfNotPresent(preStartGCode, setTempString, preStartGCodeLines, string.Format("start heating extruder {0}", extruderIndex));
					}
				}
			}

			// If we need to wait for the heaters to heat up before homing then set them to M109 (heat and wait).
			if (ActiveSliceSettings.Instance.GetActiveValue("heat_extruder_before_homing") == "1")
			{
				for (int extruderIndex = 0; extruderIndex < numberOfHeatedExtruders; extruderIndex++)
				{
					if (extrudersUsed.Count > extruderIndex
						&& extrudersUsed[extruderIndex])
					{
						string materialTemperature = ActiveSliceSettings.Instance.GetMaterialValue("temperature", extruderIndex);
						if (materialTemperature != "0")
						{
							string setTempString = "M109 T{0} S{1}".FormatWith(extruderIndex, materialTemperature);
							AddDefaultIfNotPresent(preStartGCode, setTempString, preStartGCodeLines, string.Format("wait for extruder {0}", extruderIndex));
						}
					}
				}
			}

			SwitchToFirstActiveExtruder(extrudersUsed, preStartGCodeLines, preStartGCode);
			preStartGCode.Add("; settings from start_gcode");

			return preStartGCode;
		}

		private void SwitchToFirstActiveExtruder(List<bool> extrudersUsed, string[] preStartGCodeLines, List<string> preStartGCode)
		{
			// make sure we are on the first active extruder
			for (int extruderIndex = 0; extruderIndex < extrudersUsed.Count; extruderIndex++)
			{
				if (extrudersUsed[extruderIndex])
				{
					// set the active extruder to the first one that will be printing
					AddDefaultIfNotPresent(preStartGCode, "T{0}".FormatWith(extruderIndex), preStartGCodeLines, "set the active extruder to {0}".FormatWith(extruderIndex));
					break; // then break so we don't set it to a different ones
				}
			}
		}

		public List<string> PostStartGCode(List<bool> extrudersUsed)
		{
			string startGCode = ActiveSliceSettings.Instance.GetActiveValue("start_gcode");
			string[] postStartGCodeLines = startGCode.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);

			List<string> postStartGCode = new List<string>();
			postStartGCode.Add("; automatic settings after start_gcode");

			int numberOfHeatedExtruders = 1;
			if (!ActiveSliceSettings.Instance.ExtrudersShareTemperature)
			{
				numberOfHeatedExtruders = ActiveSliceSettings.Instance.ExtruderCount;
			}

			// don't set the extrudes to heating if we alread waited for them to reach temp
			if (ActiveSliceSettings.Instance.GetActiveValue("heat_extruder_before_homing") != "1")
			{
				for (int extruderIndex = 0; extruderIndex < numberOfHeatedExtruders; extruderIndex++)
				{
					if (extrudersUsed.Count > extruderIndex
						&& extrudersUsed[extruderIndex])
					{
						string materialTemperature = ActiveSliceSettings.Instance.GetMaterialValue("temperature", extruderIndex + 1);
						if (materialTemperature != "0")
						{
							string setTempString = "M109 T{0} S{1}".FormatWith(extruderIndex, materialTemperature);
							AddDefaultIfNotPresent(postStartGCode, setTempString, postStartGCodeLines, string.Format("wait for extruder {0} to reach temperature", extruderIndex));
						}
					}
				}
			}

			SwitchToFirstActiveExtruder(extrudersUsed, postStartGCodeLines, postStartGCode);
			AddDefaultIfNotPresent(postStartGCode, "G90", postStartGCodeLines, "use absolute coordinates");
			postStartGCode.Add(string.Format("{0} ; {1}", "G92 E0", "reset the expected extruder position"));
			AddDefaultIfNotPresent(postStartGCode, "M82", postStartGCodeLines, "use absolute distance for extrusion");

			return postStartGCode;
		}
	}

	public class MapItemToBool : MapItem
	{
		public override string MappedValue
		{
			get
			{
				if (base.MappedValue == "1")
				{
					return "True";
				}

				return "False";
			}
		}

		public MapItemToBool(string mappedKey, string originalKey)
			: base(mappedKey, originalKey)
		{
		}
	}

	public class ScaledSingleNumber : MapItem
	{
		internal double scale;

		public override string MappedValue
		{
			get
			{
				double ratio = 0;
				if (OriginalValue.Contains("%"))
				{
					string withoutPercent = OriginalValue.Replace("%", "");
					ratio = MapItem.ParseValueString(withoutPercent) / 100.0;
				}
				else
				{
					ratio = MapItem.ParseValueString(base.MappedValue);
				}

				return (ratio * scale).ToString();
			}
		}

		internal ScaledSingleNumber(string mappedKey, string originalKey, double scale = 1)
			: base(mappedKey, originalKey)
		{
			this.scale = scale;
		}
	}

	public class InjectGCodeCommands : ConvertCRs
	{
		public InjectGCodeCommands(string mappedKey, string originalKey)
			: base(mappedKey, originalKey)
		{
		}

		protected void AddDefaultIfNotPresent(List<string> linesAdded, string commandToAdd, string[] linesToCheckIfAlreadyPresent, string comment)
		{
			string command = commandToAdd.Split(' ')[0].Trim();
			bool foundCommand = false;
			foreach (string line in linesToCheckIfAlreadyPresent)
			{
				if (line.StartsWith(command))
				{
					foundCommand = true;
					break;
				}
			}

			if (!foundCommand)
			{
				linesAdded.Add(string.Format("{0} ; {1}", commandToAdd, comment));
			}
		}
	}

	public class ConvertCRs : MapItem
	{
		public override string MappedValue
		{
			get
			{
				string actualCRs = base.MappedValue.Replace("\\n", "\n");
				return actualCRs;
			}
		}

		public ConvertCRs(string mappedKey, string originalKey)
			: base(mappedKey, originalKey)
		{
		}
	}

	public class AsCountOrDistance : MapItem
	{
		private string keyToUseAsDenominatorForCount;

		public AsCountOrDistance(string mappedKey, string originalKey, string keyToUseAsDenominatorForCount)
			: base(mappedKey, originalKey)
		{
			this.keyToUseAsDenominatorForCount = keyToUseAsDenominatorForCount;
		}

		public override string MappedValue
		{
			get
			{
				if (OriginalValue.Contains("mm"))
				{
					string withoutMm = OriginalValue.Replace("mm", "");
					string distanceString = ActiveSliceSettings.Instance.GetActiveValue(keyToUseAsDenominatorForCount);
					double denominator = MapItem.ParseValueString(distanceString, 1);
					int layers = (int)(MapItem.ParseValueString(withoutMm) / denominator + .5);
					return layers.ToString();
				}

				return base.MappedValue;
			}
		}
	}

	public class AsPercentOfReferenceOrDirect : ScaledSingleNumber
	{
		internal string originalReference;

		public override string MappedValue
		{
			get
			{
				if (OriginalValue.Contains("%"))
				{
					string withoutPercent = OriginalValue.Replace("%", "");
					double ratio = MapItem.ParseValueString(withoutPercent) / 100.0;
					string originalReferenceString = ActiveSliceSettings.Instance.GetActiveValue(originalReference);
					double valueToModify = MapItem.ParseValueString(originalReferenceString);
					double finalValue = valueToModify * ratio * scale;
					return finalValue.ToString();
				}

				return base.MappedValue;
			}
		}

		public AsPercentOfReferenceOrDirect(string mappedKey, string originalKey, string originalReference, double scale = 1)
			: base(mappedKey, originalKey, scale)
		{
			this.originalReference = originalReference;
		}
	}
}