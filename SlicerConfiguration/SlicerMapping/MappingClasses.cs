/*
Copyright (c) 2016, Lars Brubaker
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
using System.Text;
using MatterHackers.Agg;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public static class GCodeProcessing
	{
		private static MappedSetting[] replaceWithSettingsStrings = new MappedSetting[]
		{
			// Have a mapping so that MatterSlice while always use a setting that can be set. (the user cannot set first_layer_bedTemperature in MatterSlice)
			new AsPercentOfReferenceOrDirect(SettingsKey.first_layer_speed, "first_layer_speed", "infill_speed", 60),
			new AsPercentOfReferenceOrDirect("external_perimeter_speed","external_perimeter_speed", "perimeter_speed", 60),
			new AsPercentOfReferenceOrDirect("raft_print_speed", "raft_print_speed", "infill_speed", 60),
			new MappedSetting(SettingsKey.bed_remove_part_temperature,SettingsKey.bed_remove_part_temperature),
			new MappedSetting("bridge_fan_speed","bridge_fan_speed"),
			new MappedSetting("bridge_speed","bridge_speed"),
			new MappedSetting("extruder_wipe_temperature","extruder_wipe_temperature"),
			new MappedSetting(SettingsKey.filament_diameter,SettingsKey.filament_diameter),
			new MappedSetting("first_layer_bed_temperature", SettingsKey.bed_temperature),
			new MappedSetting("first_layer_temperature", SettingsKey.temperature),
			new MappedSetting("max_fan_speed","max_fan_speed"),
			new MappedSetting("min_fan_speed","min_fan_speed"),
			new MappedSetting("retract_length","retract_length"),
			new MappedSetting(SettingsKey.temperature,SettingsKey.temperature),
			new MappedSetting("z_offset","z_offset"),
			new MappedSetting(SettingsKey.bed_temperature,SettingsKey.bed_temperature),
			new ScaledSingleNumber("infill_speed", "infill_speed", 60),
			new ScaledSingleNumber("min_print_speed", "min_print_speed", 60),
			new ScaledSingleNumber("perimeter_speed","perimeter_speed", 60),
			new ScaledSingleNumber("retract_speed","retract_speed", 60),
			new ScaledSingleNumber("support_material_speed","support_material_speed", 60),
			new ScaledSingleNumber("travel_speed", "travel_speed", 60),
			new AsPercentOfReferenceOrDirect("load_filament_length_over_six", "", "load_filament_length", 1.0/6.0, false),
			new AsPercentOfReferenceOrDirect("unload_filament_length_over_six", "", "unload_filament_length", 1.0/6.0, false),
			new ScaledSingleNumber("load_filament_speed", "load_filament_speed", 60),
			new MappedSetting("trim_image", "trim_image"),
			new MappedSetting("insert_image", "insert_image"),
			new MappedSetting("running_clean_image", "running_clean_image"),
		};

		public static string ReplaceMacroValues(string gcodeWithMacros)
		{
			foreach (MappedSetting mappedSetting in replaceWithSettingsStrings)
			{
				// do the replacement with {} (curly brackets)
				{
					string thingToReplace = "{" + "{0}".FormatWith(mappedSetting.CanonicalSettingsName) + "}";
					gcodeWithMacros = gcodeWithMacros.Replace(thingToReplace, mappedSetting.Value);
				}
				// do the replacement with [] (square brackets) Slic3r uses only square brackets
				{
					string thingToReplace = "[" + "{0}".FormatWith(mappedSetting.CanonicalSettingsName) + "]";
					gcodeWithMacros = gcodeWithMacros.Replace(thingToReplace, mappedSetting.Value);
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
				return valueOnError;
			}

			return value;
		}

		public double ParseDoubleFromRawValue(string canonicalSettingsName, double valueOnError = 0)
		{
			return ParseDouble(ActiveSliceSettings.Instance.GetValue(canonicalSettingsName), valueOnError);
		}

		public string ExportedName { get; }

		public string CanonicalSettingsName { get; }

		public virtual string Value => ActiveSliceSettings.Instance.GetValue(CanonicalSettingsName);
	}

	public class Slice3rBedShape : MappedSetting
	{
		public Slice3rBedShape(string canonicalSettingsName)
			: base(canonicalSettingsName, canonicalSettingsName)
		{
		}

		public override string Value
		{
			get
			{
				Vector2 printCenter = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.print_center);
				Vector2 bedSize = ActiveSliceSettings.Instance.GetValue<Vector2>(SettingsKey.bed_size);
				switch (ActiveSliceSettings.Instance.GetValue<BedShape>(SettingsKey.bed_shape))
				{
					case BedShape.Circular:
						{
							int numPoints = 10;
							double angle = MathHelper.Tau / numPoints;
							string bedString = "";
							bool first = true;
							for (int i = 0; i < numPoints; i++)
							{
								if(!first)
								{
									bedString += ",";
								}
								double x = Math.Cos(angle*i);
								double y = Math.Sin(angle*i);
								bedString += $"{printCenter.X + x * bedSize.X / 2:0.####}x{printCenter.Y + y * bedSize.Y / 2:0.####}";
								first = false;
							}
							return bedString;
						}
//bed_shape = 99.4522x10.4528,97.8148x20.7912,95.1057x30.9017,91.3545x40.6737,86.6025x50,80.9017x58.7785,74.3145x66.9131,66.9131x74.3145,58.7785x80.9017,50x86.6025,40.6737x91.3545,30.9017x95.1057,20.7912x97.8148,10.4528x99.4522,0x100,-10.4528x99.4522,-20.7912x97.8148,-30.9017x95.1057,-40.6737x91.3545,-50x86.6025,-58.7785x80.9017,-66.9131x74.3145,-74.3145x66.9131,-80.9017x58.7785,-86.6025x50,-91.3545x40.6737,-95.1057x30.9017,-97.8148x20.7912,-99.4522x10.4528,-100x0,-99.4522x - 10.4528,-97.8148x - 20.7912,-95.1057x - 30.9017,-91.3545x - 40.6737,-86.6025x - 50,-80.9017x - 58.7785,-74.3145x - 66.9131,-66.9131x - 74.3145,-58.7785x - 80.9017,-50x - 86.6025,-40.6737x - 91.3545,-30.9017x - 95.1057,-20.7912x - 97.8148,-10.4528x - 99.4522,0x - 100,10.4528x - 99.4522,20.7912x - 97.8148,30.9017x - 95.1057,40.6737x - 91.3545,50x - 86.6025,58.7785x - 80.9017,66.9131x - 74.3145,74.3145x - 66.9131,80.9017x - 58.7785,86.6025x - 50,91.3545x - 40.6737,95.1057x - 30.9017,97.8148x - 20.7912,99.4522x - 10.4528,100x0

					case BedShape.Rectangular:
					default:
						{
							//bed_shape = 0x0,200x0,200x200,0x200
							string bedString = $"{printCenter.X - bedSize.X / 2}x{printCenter.Y - bedSize.Y / 2}";
							bedString += $",{printCenter.X + bedSize.X / 2}x{printCenter.Y - bedSize.Y / 2}";
							bedString += $",{printCenter.X + bedSize.X / 2}x{printCenter.Y + bedSize.Y / 2}";
							bedString += $",{printCenter.X - bedSize.X / 2}x{printCenter.Y + bedSize.Y / 2}";
							return bedString;
						}
				}
			}
		}
	}

	public class MapFirstValue : MappedSetting
	{
		public MapFirstValue(string canonicalSettingsName, string exportedName)
			: base(canonicalSettingsName, exportedName)
		{
		}

		public override string Value => base.Value.Contains(",") ? base.Value.Split(',')[0] : base.Value;
	}

	// Replaces escaped newline characters with unescaped newline characters
	public class UnescapeNewlineCharacters : MappedSetting
	{
		public UnescapeNewlineCharacters(string canonicalSettingsName, string exportedName)
			: base(canonicalSettingsName, exportedName)
		{
		}

		public override string Value => base.Value.Replace("\\n", "\n");
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

	public class MapLayerChangeGCode : InjectGCodeCommands
	{
		public MapLayerChangeGCode(string canonicalSettingsName, string exportedName)
			: base(canonicalSettingsName, exportedName)
		{
		}

		public override string Value
		{
			get
			{
				string macroReplaced = base.Value;
				if (!macroReplaced.Contains("; LAYER:") 
					&& !macroReplaced.Contains(";LAYER:"))
				{
					macroReplaced += "; LAYER:[layer_num]\n";
				}

				macroReplaced = GCodeProcessing.ReplaceMacroValues(macroReplaced.Replace("\n", "\\n"));

				return macroReplaced;
			}
		}
	}

	public class MapStartGCode : InjectGCodeCommands
	{
		private bool escapeNewlineCharacters;

		public MapStartGCode(string canonicalSettingsName, string exportedName, bool escapeNewlineCharacters)
			: base(canonicalSettingsName, exportedName)
		{
			this.escapeNewlineCharacters = escapeNewlineCharacters;
		}

		public override string Value
		{
			get
			{
				StringBuilder newStartGCode = new StringBuilder();
				foreach (string line in PreStartGCode(Slicer.extrudersUsed))
				{
					newStartGCode.Append(line + "\n");
				}

				newStartGCode.Append(GCodeProcessing.ReplaceMacroValues(base.Value));

				foreach (string line in PostStartGCode(Slicer.extrudersUsed))
				{
					newStartGCode.Append("\n");
					newStartGCode.Append(line);
				}

				if (escapeNewlineCharacters)
				{
					return newStartGCode.ToString().Replace("\n", "\\n");
				}

				return newStartGCode.ToString();
			}
		}

		public List<string> PreStartGCode(List<bool> extrudersUsed)
		{
			string startGCode = ActiveSliceSettings.Instance.GetValue(SettingsKey.start_gcode);
			string[] preStartGCodeLines = startGCode.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);

			List<string> preStartGCode = new List<string>();
			preStartGCode.Add("; automatic settings before start_gcode");
			AddDefaultIfNotPresent(preStartGCode, "G21", preStartGCodeLines, "set units to millimeters");
			AddDefaultIfNotPresent(preStartGCode, "M107", preStartGCodeLines, "fan off");
			double bed_temperature = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.bed_temperature);
			if (bed_temperature > 0)
			{
				string setBedTempString = string.Format("M190 S{0}", bed_temperature);
				AddDefaultIfNotPresent(preStartGCode, setBedTempString, preStartGCodeLines, "wait for bed temperature to be reached");
			}

			int numberOfHeatedExtruders = ActiveSliceSettings.Instance.Helpers.NumberOfHotEnds();

			// Start heating all the extruder that we are going to use.
			for (int extruderIndex0Based = 0; extruderIndex0Based < numberOfHeatedExtruders; extruderIndex0Based++)
			{
				if (extrudersUsed.Count > extruderIndex0Based
					&& extrudersUsed[extruderIndex0Based])
				{
					double materialTemperature = ActiveSliceSettings.Instance.Helpers.ExtruderTemperature(extruderIndex0Based);
					if (materialTemperature != 0)
					{
						string setTempString = "M104 T{0} S{1}".FormatWith(extruderIndex0Based, materialTemperature);
						AddDefaultIfNotPresent(preStartGCode, setTempString, preStartGCodeLines, string.Format("start heating extruder {0}", extruderIndex0Based + 1));
					}
				}
			}

			// If we need to wait for the heaters to heat up before homing then set them to M109 (heat and wait).
			if (ActiveSliceSettings.Instance.GetValue(SettingsKey.heat_extruder_before_homing) == "1")
			{
				for (int extruderIndex0Based = 0; extruderIndex0Based < numberOfHeatedExtruders; extruderIndex0Based++)
				{
					if (extrudersUsed.Count > extruderIndex0Based
						&& extrudersUsed[extruderIndex0Based])
					{
						double materialTemperature = ActiveSliceSettings.Instance.Helpers.ExtruderTemperature(extruderIndex0Based);
						if (materialTemperature != 0)
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
			string startGCode = ActiveSliceSettings.Instance.GetValue(SettingsKey.start_gcode);
			string[] postStartGCodeLines = startGCode.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);

			List<string> postStartGCode = new List<string>();
			postStartGCode.Add("; automatic settings after start_gcode");

			int numberOfHeatedExtruders = ActiveSliceSettings.Instance.GetValue<int>(SettingsKey.extruder_count);

			// don't set extruder 0 to heating if we already waited for it to reach temp
			if (ActiveSliceSettings.Instance.GetValue(SettingsKey.heat_extruder_before_homing) != "1")
			{
				if (extrudersUsed[0])
				{
					double materialTemperature = ActiveSliceSettings.Instance.Helpers.ExtruderTemperature(0);
					if (materialTemperature != 0)
					{
						string setTempString = $"M109 T0 S{materialTemperature}";
						AddDefaultIfNotPresent(postStartGCode, setTempString, postStartGCodeLines, string.Format("wait for extruder {0} to reach temperature", 1));
					}
				}
			}

			if (extrudersUsed.Count > 1)
			{
				// start all the extruders heating
				for (int extruderIndex0Based = 1; extruderIndex0Based < numberOfHeatedExtruders; extruderIndex0Based++)
				{
					if (extruderIndex0Based < extrudersUsed.Count
						&& extrudersUsed[extruderIndex0Based])
					{
						double materialTemperature = ActiveSliceSettings.Instance.Helpers.ExtruderTemperature(extruderIndex0Based);
						if (materialTemperature != 0)
						{
							// always heat the extruders that are used beyond extruder 0
							postStartGCode.Add($"M104 T{extruderIndex0Based} S{materialTemperature} ; Start heating extruder{extruderIndex0Based+1}");
						}
					}
				}

				// wait for them to finish
				for (int extruderIndex0Based = 1; extruderIndex0Based < numberOfHeatedExtruders; extruderIndex0Based++)
				{
					if (extruderIndex0Based < extrudersUsed.Count
						&& extrudersUsed[extruderIndex0Based])
					{
						double materialTemperature = ActiveSliceSettings.Instance.Helpers.ExtruderTemperature(extruderIndex0Based);
						if (materialTemperature != 0)
						{
							// always heat the extruders that are used beyond extruder 0
							postStartGCode.Add($"M109 T{extruderIndex0Based} S{materialTemperature} ; Finish heating extruder{extruderIndex0Based + 1}");
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

	public class InjectGCodeCommands : UnescapeNewlineCharacters
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
					string distanceString = ActiveSliceSettings.Instance.GetValue(keyToUseAsDenominatorForCount);
					double denominator = ParseDouble(distanceString, 1);
					int layers = (int)(ParseDouble(withoutMm) / denominator + .5);
					return layers.ToString();
				}

				return base.Value;
			}
		}
	}

	public class RetractionLength : MappedSetting
	{
		public RetractionLength(string canonicalSettingsName, string exportedName)
			: base(canonicalSettingsName, exportedName)
		{
		}

		public override string Value
		{
			get
			{
				if(ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.enable_retractions))
				{
					return base.Value;
				}
				else
				{
					return 0.ToString();
				}
			}
		}
	}

	public class OverrideSpeedOnSlaPrinters : AsPercentOfReferenceOrDirect
	{
		public OverrideSpeedOnSlaPrinters(string canonicalSettingsName, string exportedName, string originalReference, double scale = 1)
			: base(canonicalSettingsName, exportedName, originalReference, scale)
		{
		}

		public override string Value
		{
			get
			{
				if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.sla_printer))
				{
					// return the speed based on the layer height
					var speedAt025 = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.laser_speed_025);
					var speedAt100 = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.laser_speed_100);
					var deltaSpeed = speedAt100 - speedAt025;

					var layerHeight = ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.layer_height);
					var deltaHeight = .1 - .025;
					var heightRatio = (layerHeight - .025) / deltaHeight;
					var ajustedSpeed = speedAt025 + deltaSpeed * heightRatio;
					return ajustedSpeed.ToString();
				}
				else
				{
					return base.Value;
				}
			}
		}
	}

	public class AsPercentOfReferenceOrDirect : MappedSetting
	{
		bool change0ToReference;
		string originalReference;
		double scale;

		public AsPercentOfReferenceOrDirect(string canonicalSettingsName, string exportedName, string originalReference, double scale = 1, bool change0ToReference = true)
			: base(canonicalSettingsName, exportedName)
		{
			this.change0ToReference = change0ToReference;
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
					string originalReferenceString = ActiveSliceSettings.Instance.GetValue(originalReference);
					double valueToModify = ParseDouble(originalReferenceString);
					finalValue = valueToModify * ratio;
				}
				else
				{
					finalValue = ParseDouble(base.Value);
				}

				if (change0ToReference
					&& finalValue == 0)
				{
					finalValue = ParseDouble(ActiveSliceSettings.Instance.GetValue(originalReference));
				}

				finalValue *= scale;

				return finalValue.ToString();
			}
		}
	}
}