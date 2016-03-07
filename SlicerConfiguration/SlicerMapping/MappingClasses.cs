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

	public class MappedSetting
	{
		public MappedSetting(string canonicalSettingsName, string exportedName)
		{
			this.CanonicalSettingsName = canonicalSettingsName;
			this.ExportedName = exportedName;
		}

		public double ParseDouble(string textValue, double valueOnError = 0)
		{
			double value;
			if (!double.TryParse(textValue, out value))
			{
				MatterControlApplication.BreakInDebugger("Slicing value is not a double.");
				return valueOnError;
			}

			return value;
		}

		public double ParseDoubleFromRawValue(string canonicalSettingsName, double valueOnError = 0)
		{
			return ParseDouble(ActiveSliceSettings.Instance.GetActiveValue(canonicalSettingsName), valueOnError);
		}

		public string ExportedName { get; }

		public string CanonicalSettingsName { get; }

		public virtual string Value => ActiveSliceSettings.Instance.GetActiveValue(CanonicalSettingsName);
	}

	public class MapFirstValue : MappedSetting
	{
		public MapFirstValue(string canonicalSettingsName, string exportedName)
			: base(canonicalSettingsName, exportedName)
		{
		}

		public override string Value => base.Value.Contains(",") ? base.Value.Split(',')[0] : base.Value;
	}
	/// <summary>
	/// Setting will appear in the editor, but it will not be passed to the slicing engine.
	/// These values are used in other parts of MatterControl, not slicing, but are held in the slicing data.
	/// </summary>
	/// <seealso cref="MatterHackers.MatterControl.SlicerConfiguration.MappedSetting" />
	public class VisibleButNotMappedToEngine : MappedSetting
	{
		public VisibleButNotMappedToEngine(string canonicalSettingsName)
			: base(canonicalSettingsName, "")
		{
		}

		public override string Value => null;
	}

	public class MapStartGCode : InjectGCodeCommands
	{
		private bool escapeCarriageReturns;

		public MapStartGCode(string canonicalSettingsName, string exportedName, bool escapeCarriageReturns)
			: base(canonicalSettingsName, exportedName)
		{
			this.escapeCarriageReturns = escapeCarriageReturns;
		}

		public override string Value
		{
			get
			{
				StringBuilder newStartGCode = new StringBuilder();
				foreach (string line in PreStartGCode(SlicingQueue.extrudersUsed))
				{
					newStartGCode.Append(line + "\n");
				}

				newStartGCode.Append(GCodeProcessing.ReplaceMacroValues(base.Value));

				foreach (string line in PostStartGCode(SlicingQueue.extrudersUsed))
				{
					newStartGCode.Append("\n");
					newStartGCode.Append(line);
				}

				if (escapeCarriageReturns)
				{
					return newStartGCode.ToString().Replace("\n", "\\n");
				}

				return newStartGCode.ToString();
			}
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
			for (int extruderIndex0Based = 0; extruderIndex0Based < numberOfHeatedExtruders; extruderIndex0Based++)
			{
				if (extrudersUsed.Count > extruderIndex0Based
					&& extrudersUsed[extruderIndex0Based])
				{
					string materialTemperature = ActiveSliceSettings.Instance.GetMaterialValue("temperature", extruderIndex0Based + 1);
					if (materialTemperature != "0")
					{
						string setTempString = "M104 T{0} S{1}".FormatWith(extruderIndex0Based, materialTemperature);
						AddDefaultIfNotPresent(preStartGCode, setTempString, preStartGCodeLines, string.Format("start heating extruder {0}", extruderIndex0Based + 1));
					}
				}
			}

			// If we need to wait for the heaters to heat up before homing then set them to M109 (heat and wait).
			if (ActiveSliceSettings.Instance.GetActiveValue("heat_extruder_before_homing") == "1")
			{
				for (int extruderIndex0Based = 0; extruderIndex0Based < numberOfHeatedExtruders; extruderIndex0Based++)
				{
					if (extrudersUsed.Count > extruderIndex0Based
						&& extrudersUsed[extruderIndex0Based])
					{
						string materialTemperature = ActiveSliceSettings.Instance.GetMaterialValue("temperature", extruderIndex0Based + 1);
						if (materialTemperature != "0")
						{
							string setTempString = "M109 T{0} S{1}".FormatWith(extruderIndex0Based, materialTemperature);
							AddDefaultIfNotPresent(preStartGCode, setTempString, preStartGCodeLines, string.Format("wait for extruder {0}", extruderIndex0Based + 1));
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

			// don't set the extruders to heating if we already waited for them to reach temp
			if (ActiveSliceSettings.Instance.GetActiveValue("heat_extruder_before_homing") != "1")
			{
				for (int extruderIndex0Based = 0; extruderIndex0Based < numberOfHeatedExtruders; extruderIndex0Based++)
				{
					if (extrudersUsed.Count > extruderIndex0Based
						&& extrudersUsed[extruderIndex0Based])
					{
						string materialTemperature = ActiveSliceSettings.Instance.GetMaterialValue("temperature", extruderIndex0Based + 1);
						if (materialTemperature != "0")
						{
							string setTempString = "M109 T{0} S{1}".FormatWith(extruderIndex0Based, materialTemperature);
							AddDefaultIfNotPresent(postStartGCode, setTempString, postStartGCodeLines, string.Format("wait for extruder {0} to reach temperature", extruderIndex0Based + 1));
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

	public class MappedToBoolString : MappedSetting
	{
		public MappedToBoolString(string canonicalSettingsName, string exportedName) : base(canonicalSettingsName, exportedName)
		{
		}

		public override string Value => (base.Value == "1") ?  "True" : "False";
	}

	public class ScaledSingleNumber : MapFirstValue
	{
		internal double scale;

		internal ScaledSingleNumber(string matterControlName, string exportedName, double scale = 1) : base(matterControlName, exportedName)
		{
			this.scale = scale;
		}

		public override string Value
		{
			get
			{
				double ratio = 0;
				if (base.Value.Contains("%"))
				{
					string withoutPercent = base.Value.Replace("%", "");
					ratio = ParseDouble(withoutPercent) / 100.0;
				}
				else
				{
					ratio = ParseDouble(base.Value);
				}

				return (ratio * scale).ToString();
			}
		}
	}

	public class InjectGCodeCommands : MappedSetting
	{
		public InjectGCodeCommands(string canonicalSettingsName, string exportedName)
			: base(canonicalSettingsName, exportedName)
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

	public class AsCountOrDistance : MappedSetting
	{
		private string keyToUseAsDenominatorForCount;

		public AsCountOrDistance(string canonicalSettingsName, string exportedName, string keyToUseAsDenominatorForCount)
			: base(canonicalSettingsName, exportedName)
		{
			this.keyToUseAsDenominatorForCount = keyToUseAsDenominatorForCount;
		}

		public override string Value
		{
			get
			{

				if (base.Value.Contains("mm"))
				{
					string withoutMm = base.Value.Replace("mm", "");
					string distanceString = ActiveSliceSettings.Instance.GetActiveValue(keyToUseAsDenominatorForCount);
					double denominator = ParseDouble(distanceString, 1);
					int layers = (int)(ParseDouble(withoutMm) / denominator + .5);
					return layers.ToString();
				}

				return base.Value;
			}
		}
	}

	public class AsPercentOfReferenceOrDirect : MappedSetting
	{
		string originalReference;
		double scale;

		public AsPercentOfReferenceOrDirect(string canonicalSettingsName, string exportedName, string originalReference, double scale = 1)
			: base(canonicalSettingsName, exportedName)
		{
			this.scale = scale;
			this.originalReference = originalReference;
		}

		public override string Value
		{
			get
			{
				double finalValue = 0;
				if (base.Value.Contains("%"))
				{
					string withoutPercent = base.Value.Replace("%", "");
					double ratio = ParseDouble(withoutPercent) / 100.0;
					string originalReferenceString = ActiveSliceSettings.Instance.GetActiveValue(originalReference);
					double valueToModify = ParseDouble(originalReferenceString);
					finalValue = valueToModify * ratio;
				}
				else
				{
					finalValue = ParseDouble(base.Value);
				}

				if (finalValue == 0)
				{
					finalValue = ParseDouble(ActiveSliceSettings.Instance.GetActiveValue(originalReference));
				}

				finalValue *= scale;

				return finalValue.ToString();
			}
		}
	}
}