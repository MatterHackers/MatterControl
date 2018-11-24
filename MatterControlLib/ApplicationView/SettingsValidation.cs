/*
Copyright (c) 2018, Lars Brubaker
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
using System.Diagnostics;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public static class SettingsValidation
	{
		public static bool SettingsValid(PrinterConfig printer)
		{
			var settings = printer.Settings;

			try
			{
				if (settings.GetValue<bool>(SettingsKey.validate_layer_height))
				{
					if (settings.GetValue<double>(SettingsKey.layer_height) > settings.GetValue<double>(SettingsKey.nozzle_diameter))
					{
						var error = "{0} must be less than or equal to the {1}.".Localize().FormatWith(
							GetSettingsName(SettingsKey.layer_height), GetSettingsName(SettingsKey.nozzle_diameter));
						var details = "{0} = {1}\n{2} = {3}".FormatWith(GetSettingsName(SettingsKey.layer_height),
							settings.GetValue<double>(SettingsKey.layer_height),
							GetSettingsName(SettingsKey.nozzle_diameter),
							settings.GetValue<double>(SettingsKey.nozzle_diameter));
						var location = GetSettingsLocation(SettingsKey.layer_height);
						StyledMessageBox.ShowMessageBox("{0}\n\n{1}\n\n{2}"
							.FormatWith(error, details, location), "Slice Error".Localize());
						return false;
					}
					else if (settings.GetValue<double>(SettingsKey.layer_height) <= 0)
					{
						var error = "{0} must be greater than 0.".Localize().FormatWith(
							GetSettingsName(SettingsKey.layer_height));
						var location = GetSettingsLocation(SettingsKey.layer_height);
						StyledMessageBox.ShowMessageBox($"{error}\n\n{location}", "Slice Error".Localize());
						return false;
					}
					else if (settings.GetValue<double>(SettingsKey.first_layer_height) > settings.GetValue<double>(SettingsKey.nozzle_diameter))
					{
						var error = "{0} must be less than or equal to the {1}.".Localize().FormatWith(
							GetSettingsName(SettingsKey.layer_height),
							GetSettingsName(SettingsKey.nozzle_diameter));
						var details = "{0} = {1}\n{2} = {3}".FormatWith(
							GetSettingsName(SettingsKey.first_layer_height),
							settings.GetValue<double>(SettingsKey.first_layer_height),
							GetSettingsName(SettingsKey.nozzle_diameter),
							settings.GetValue<double>(SettingsKey.nozzle_diameter));
						var location = GetSettingsLocation(SettingsKey.first_layer_height);
						StyledMessageBox.ShowMessageBox("{0}\n\n{1}\n\n{2}".FormatWith(error, details, location), "Slice Error".Localize());
						return false;
					}
				}

				// Print recovery can only work with a manually leveled or software leveled bed. Hardware leveling does not work.
				if (settings.GetValue<bool>(SettingsKey.recover_is_enabled))
				{
					string[] startGCode = settings.GetValue(SettingsKey.start_gcode).Replace("\\n", "\n").Split('\n');
					foreach (string startGCodeLine in startGCode)
					{
						if (startGCodeLine.StartsWith("G29"))
						{
							var location = GetSettingsLocation(SettingsKey.start_gcode);
							var error = "Start G-Code cannot contain G29 if Print Recovery is enabled.".Localize();
							var details = "Your Start G-Code should not contain a G29 if you are planning on using Print Recovery. Change your start G-Code or turn off Print Recovery.".Localize();
							StyledMessageBox.ShowMessageBox("{0}\n\n{1}\n\n{2}".FormatWith(error, details, location), "Slice Error".Localize());
							return false;
						}

						if (startGCodeLine.StartsWith("G30"))
						{
							var location = GetSettingsLocation(SettingsKey.start_gcode);
							var error = "Start G-Code cannot contain G30 if Print Leveling is enabled.".Localize();
							var details = "Your Start G-Code should not contain a G30 if you are planning on using Print Recovery. Change your start G-Code or turn off Print Recovery.".Localize();
							StyledMessageBox.ShowMessageBox("{0}\n\n{1}\n\n{2}".FormatWith(error, details, location), "Slice Error".Localize());
							return false;
						}
					}
				}

				// If we have print leveling turned on then make sure we don't have any leveling commands in the start gcode.
				if (settings.GetValue<bool>(SettingsKey.print_leveling_enabled))
				{
					string[] startGCode = settings.GetValue(SettingsKey.start_gcode).Replace("\\n", "\n").Split('\n');
					foreach (string startGCodeLine in startGCode)
					{
						if (startGCodeLine.StartsWith("G29"))
						{
							var location = GetSettingsLocation(SettingsKey.start_gcode);
							var error = "Start G-Code cannot contain G29 if Print Leveling is enabled.".Localize();
							var details = "Your Start G-Code should not contain a G29 if you are planning on using print leveling. Change your start G-Code or turn off print leveling.".Localize();
							StyledMessageBox.ShowMessageBox("{0}\n\n{1}\n\n{2}".FormatWith(error, details, location), "Slice Error".Localize());
							return false;
						}

						if (startGCodeLine.StartsWith("G30"))
						{
							var location = GetSettingsLocation(SettingsKey.start_gcode);
							var error = "Start G-Code cannot contain G30 if Print Leveling is enabled.".Localize();
							var details = "Your Start G-Code should not contain a G30 if you are planning on using print leveling. Change your start G-Code or turn off print leveling.".Localize();
							StyledMessageBox.ShowMessageBox("{0}\n\n{1}\n\n{2}".FormatWith(error, details, location), "Slice Error".Localize());
							return false;
						}
					}
				}

				// If we have print leveling turned on then make sure we don't have any leveling commands in the start gcode.
				if (Math.Abs(settings.GetValue<double>(SettingsKey.baby_step_z_offset)) > 2)
				{
					var location = "Location".Localize() + ":";
					location += "\n" + "Controls".Localize();
					location += "\n  • " + "Movement".Localize();
					location += "\n    • " + "Z Offset".Localize();
					var error = "Z Offset is too large.".Localize();
					var details = "The Z Offset for your printer, sometimes called Baby Stepping, is greater than 2mm and invalid. Clear the value and re-level the bed.".Localize();
					StyledMessageBox.ShowMessageBox("{0}\n\n{1}\n\n{2}".FormatWith(error, details, location), "Calibration Error".Localize());
					return false;
				}

				if (settings.GetValue<double>(SettingsKey.first_layer_extrusion_width) > settings.GetValue<double>(SettingsKey.nozzle_diameter) * 4)
				{
					var error = "{0} must be less than or equal to the {1} * 4.".Localize().FormatWith(
						GetSettingsName(SettingsKey.first_layer_extrusion_width),
						GetSettingsName(SettingsKey.nozzle_diameter));
					var details = "{0} = {1}\n{2} = {3}".FormatWith(
						GetSettingsName(SettingsKey.first_layer_extrusion_width),
						settings.GetValue<double>(SettingsKey.first_layer_extrusion_width),
						GetSettingsName(SettingsKey.nozzle_diameter),
						settings.GetValue<double>(SettingsKey.nozzle_diameter));
					string location = GetSettingsLocation(SettingsKey.first_layer_extrusion_width);
					StyledMessageBox.ShowMessageBox("{0}\n\n{1}\n\n{2}".FormatWith(error, details, location), "Slice Error".Localize());
					return false;
				}

				if (settings.GetValue<double>(SettingsKey.first_layer_extrusion_width) <= 0)
				{
					var error = "{0} must be greater than 0.".Localize().FormatWith(
						GetSettingsName(SettingsKey.first_layer_extrusion_width));
					var details = "{0} = {1}".FormatWith(
							GetSettingsName(SettingsKey.first_layer_extrusion_width),
							settings.GetValue<double>(SettingsKey.first_layer_extrusion_width));
					string location = GetSettingsLocation(SettingsKey.first_layer_extrusion_width);
					StyledMessageBox.ShowMessageBox("{0}\n\n{1}\n\n{2}".FormatWith(error, details, location), "Slice Error".Localize());
					return false;
				}

				if (settings.GetValue<double>(SettingsKey.external_perimeter_extrusion_width) > settings.GetValue<double>(SettingsKey.nozzle_diameter) * 4)
				{
					var error = "{0} must be less than or equal to the {1} * 4.".Localize().FormatWith(
						GetSettingsName(SettingsKey.external_perimeter_extrusion_width),
						GetSettingsName(SettingsKey.nozzle_diameter));
					var details = "{0} = {1}\n{2} = {3}".FormatWith(
							GetSettingsName(SettingsKey.external_perimeter_extrusion_width),
							settings.GetValue<double>(SettingsKey.external_perimeter_extrusion_width),
							GetSettingsName(SettingsKey.nozzle_diameter),
							settings.GetValue<double>(SettingsKey.nozzle_diameter));
					string location = GetSettingsLocation(SettingsKey.external_perimeter_extrusion_width);
					StyledMessageBox.ShowMessageBox("{0}\n\n{1}\n\n{2}".FormatWith(error, details, location), "Slice Error".Localize());
					return false;
				}

				if (settings.GetValue<double>(SettingsKey.external_perimeter_extrusion_width) <= 0)
				{
					var error = "{0} must be greater than 0.".Localize().FormatWith(
						GetSettingsName(SettingsKey.external_perimeter_extrusion_width));
					var details = "{0} = {1}".FormatWith(
							GetSettingsName(SettingsKey.external_perimeter_extrusion_width),
							settings.GetValue<double>(SettingsKey.external_perimeter_extrusion_width));
					var location = GetSettingsLocation(SettingsKey.external_perimeter_extrusion_width);
					StyledMessageBox.ShowMessageBox("{0}\n\n{1}\n\n{2}".FormatWith(error, details, location), "Slice Error".Localize());
					return false;
				}

				if (settings.GetValue<double>(SettingsKey.min_fan_speed) > 100)
				{
					var error = "The {0} can only go as high as 100%.".Localize().FormatWith(
						GetSettingsName(SettingsKey.min_fan_speed));
					var details = "It is currently set to {0}.".Localize().FormatWith(
						settings.GetValue<double>(SettingsKey.min_fan_speed));
					var location = GetSettingsLocation(SettingsKey.min_fan_speed);
					StyledMessageBox.ShowMessageBox("{0}\n\n{1}\n\n{2}".FormatWith(error, details, location), "Slice Error".Localize());
					return false;
				}

				if (settings.GetValue<double>(SettingsKey.max_fan_speed) > 100)
				{
					var error = "The {0} can only go as high as 100%.".Localize().FormatWith(
						GetSettingsName(SettingsKey.max_fan_speed));
					var details = "It is currently set to {0}.".Localize().FormatWith(
						settings.GetValue<double>(SettingsKey.max_fan_speed));
					var location = GetSettingsLocation(SettingsKey.max_fan_speed);
					StyledMessageBox.ShowMessageBox("{0}\n\n{1}\n\n{2}".FormatWith(error, details, location), "Slice Error".Localize());
					return false;
				}

				if (settings.GetValue<int>(SettingsKey.extruder_count) < 1)
				{
					var error = "The {0} must be at least 1.".Localize().FormatWith(
						GetSettingsName(SettingsKey.extruder_count));
					var details = "It is currently set to {0}.".Localize().FormatWith(
						settings.GetValue<int>(SettingsKey.extruder_count));
					var location = GetSettingsLocation(SettingsKey.extruder_count);
					StyledMessageBox.ShowMessageBox("{0}\n\n{1}\n\n{2}".FormatWith(error, details, location), "Slice Error".Localize());
					return false;
				}

				if (settings.GetValue<double>(SettingsKey.fill_density) < 0 || settings.GetValue<double>(SettingsKey.fill_density) > 1)
				{
					var error = "The {0} must be between 0 and 1.".Localize().FormatWith(
						GetSettingsName(SettingsKey.fill_density));
					var details = "It is currently set to {0}.".Localize().FormatWith(
						settings.GetValue<double>(SettingsKey.fill_density));
					var location = GetSettingsLocation(SettingsKey.filament_density);
					StyledMessageBox.ShowMessageBox("{0}\n\n{1}\n\n{2}".FormatWith(error, details, location), "Slice Error".Localize());
					return false;
				}

				if (settings.GetValue<double>(SettingsKey.fill_density) == 1
					&& settings.GetValue(SettingsKey.infill_type) != "LINES")
				{
					var error = "{0} works best when set to LINES.".Localize()
						.FormatWith(GetSettingsName(SettingsKey.infill_type));
					var details = "It is currently set to {0}.".Localize().FormatWith(
						settings.GetValue(SettingsKey.infill_type));
					var location = GetSettingsLocation(SettingsKey.infill_type);
					StyledMessageBox.ShowMessageBox("{0}\n\n{1}\n\n{2}".FormatWith(error, details, location), "Slice Error".Localize());
					return true;
				}

				// If the given speed is part of the current slice engine then check that it is greater than 0.
				if (!ValidateGoodSpeedSettingGreaterThan0("bridge_speed", printer)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("air_gap_speed", printer)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("external_perimeter_speed", printer)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0(SettingsKey.first_layer_speed, printer)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("infill_speed", printer)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("perimeter_speed", printer)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("small_perimeter_speed", printer)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("solid_infill_speed", printer)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("support_material_speed", printer)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0(SettingsKey.top_solid_infill_speed, printer)) return false;
				if (!ValidateGoodSpeedSettingGreaterThan0("travel_speed", printer)) return false;

				if (!ValidateGoodSpeedSettingGreaterThan0("retract_speed", printer)) return false;
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
				GuiWidget.BreakInDebugger();
				string stackTraceNoBackslashRs = e.StackTrace.Replace("\r", "");

				var widget = new ContactForm.ContactFormPage();
				widget.questionInput.Text = "Parse Error while slicing".Localize();
				widget.detailInput.Text = e.Message + stackTraceNoBackslashRs;

				DialogWindow.Show(widget);

				return false;
			}

			return true;
		}

		private static string GetSettingsLocation(string settingsKey)
		{
			var settingData = SettingsOrganizer.Instance.GetSettingsData(settingsKey);
			var setingsSectionName = settingData.OrganizerSubGroup.Group.Category.SettingsSection.Name;
			var rootLevel = SettingsOrganizer.Instance.UserLevels[setingsSectionName];
			var subGroup = rootLevel.GetContainerForSetting(settingsKey);
			var category = subGroup.Group.Category;

			if (setingsSectionName == "Advanced")
			{
				setingsSectionName = "Slice Settings";
			}

			var location = "Location".Localize() + ":";
			location += "\n" + setingsSectionName.Localize();
			location += "\n  • " + category.Name.Localize();
			location += "\n    • " + subGroup.Group.Name.Localize();
			location += "\n      • " + settingData.PresentationName.Localize();

			return location;
		}

		private static string GetSettingsName(string settingsKey)
		{
			var settingData = SettingsOrganizer.Instance.GetSettingsData(settingsKey);
			return settingData.PresentationName.Localize();
		}

		private static bool ValidateGoodSpeedSettingGreaterThan0(string speedSetting, PrinterConfig printer)
		{
			var actualSpeedValueString = printer.Settings.GetValue(speedSetting);
			var speedValueString = actualSpeedValueString;
			if (speedValueString.EndsWith("%"))
			{
				speedValueString = speedValueString.Substring(0, speedValueString.Length - 1);
			}
			bool valueWasNumber = true;
			double speedToCheck;
			if (!double.TryParse(speedValueString, out speedToCheck))
			{
				valueWasNumber = false;
			}

			if (!valueWasNumber
				|| (printer.EngineMappingsMatterSlice.MapContains(speedSetting)
				&& speedToCheck <= 0))
			{
				SliceSettingData data = SettingsOrganizer.Instance.GetSettingsData(speedSetting);
				if (data != null)
				{
					var location = GetSettingsLocation(speedSetting);

					var error = "The {0} must be greater than 0.".Localize().FormatWith(data.PresentationName);
					var details = "It is currently set to {0}.".Localize().FormatWith(actualSpeedValueString);
					StyledMessageBox.ShowMessageBox("{0}\n\n{1}\n\n{2}".FormatWith(error, details, location), "Slice Error".Localize());
				}
				return false;
			}
			return true;
		}

	}
}