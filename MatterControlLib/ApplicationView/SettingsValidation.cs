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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public static class SettingsValidation
	{
		/// <summary>
		/// Validates the printer settings satisfy all requirements.
		/// </summary>
		/// <param name="printer">The printer to validate.</param>
		/// <returns>A list of all warnings and errors.</returns>
		public static List<ValidationError> ValidateSettings(this PrinterConfig printer, SettingsContext settings = null, bool validatePrintBed = true)
		{
			var fffPrinter = printer.Settings.Slicer.PrinterType == PrinterType.FFF;

			if (settings == null)
			{
				settings = new SettingsContext(printer, null, NamedSettingsLayers.All);
			}

			var errors = new List<ValidationError>();

			var extruderCount = settings.GetValue<int>(SettingsKey.extruder_count);

			// Check to see if supports are required
			if (!settings.GetValue<bool>(SettingsKey.create_per_layer_support))
			{
				var supportGenerator = new SupportGenerator(printer.Bed.Scene, .05);
				if (supportGenerator.RequiresSupport())
				{
					errors.Add(new ValidationError(ValidationErrors.UnsupportedParts)
					{
						Error = "Possible Unsupported Parts Detected".Localize(),
						Details = "Some parts may require support structures to print correctly".Localize(),
						ErrorLevel = ValidationErrorLevel.Warning,
						FixAction = new NamedAction()
						{
							Title = "Generate Supports".Localize(),
							Action = () =>
							{
								// Find and InvokeClick on the Generate Supports toolbar button
								var sharedParent = ApplicationController.Instance.DragDropData.View3DWidget.Parents<GuiWidget>().FirstOrDefault(w => w.Name == "View3DContainerParent");
								if (sharedParent != null)
								{
									var supportsPopup = sharedParent.FindDescendant("Support SplitButton");
									supportsPopup.InvokeClick();
								}
							}
						}
					});
				}
			}

			if (!settings.GetValue<bool>(SettingsKey.extruder_offset))
			{
				var t0Offset = printer.Settings.Helpers.ExtruderOffset(0);
				if (t0Offset != Vector3.Zero)
				{
					errors.Add(
						new SettingsValidationError(SettingsKey.extruder_offset)
						{
							Error = "Nozzle 1 should have offsets set to 0.".Localize(),
							ValueDetails = "{0} = {1}\n{2} = {3}".FormatWith(
								GetSettingsName(SettingsKey.extruder_offset),
								settings.GetValue<double>(SettingsKey.extruder_offset),
								GetSettingsName(SettingsKey.extruder_offset),
								settings.GetValue<double>(SettingsKey.extruder_offset)),
							ErrorLevel = ValidationErrorLevel.Warning,
						});
				}
			}

			// Check to see if current OEM layer matches downloaded OEM layer
			{
				if (printer.Settings.GetValue(SettingsKey.make) != "Other"
					&& ProfileManager.GetOemSettingsNeedingUpdate(printer).Any())
				{
					errors.Add(new ValidationError(ValidationErrors.SettingsUpdateAvailable)
					{
						Error = "Settings Update Available".Localize(),
						Details = "The default settings for this printer have changed and can be updated".Localize(),
						ErrorLevel = ValidationErrorLevel.Warning,
						FixAction = new NamedAction()
						{
							Title = "Update Settings...".Localize(),
							Action = () =>
							{
								DialogWindow.Show(new UpdateSettingsPage(printer));
							}
						}
					});
				}
			}

			if (printer.Connection.IsConnected
				&& !PrinterSetupRequired(printer)
				&& validatePrintBed
				&& errors.Count(e => e.ErrorLevel == ValidationErrorLevel.Error) == 0
				&& !printer.PrintableItems(printer.Bed.Scene).Any())
			{
				errors.Add(new ValidationError(ValidationErrors.NoPrintableParts)
				{
					Error = "Empty Bed".Localize(),
					Details = "No printable parts exists within the bounds of the printer bed. Add content to continue".Localize(),
					ErrorLevel = ValidationErrorLevel.Error,
				});
			}

			try
			{
				if (settings.GetValue<bool>(SettingsKey.validate_layer_height))
				{
					if (settings.GetValue<double>(SettingsKey.layer_height) > settings.GetValue<double>(SettingsKey.nozzle_diameter))
					{
						errors.Add(
							new SettingsValidationError(SettingsKey.layer_height)
							{
								Error = "{0} must be less than or equal to the {1}.".Localize().FormatWith(
									GetSettingsName(SettingsKey.layer_height),
									GetSettingsName(SettingsKey.nozzle_diameter)),
								ValueDetails = "{0} = {1}\n{2} = {3}".FormatWith(
									GetSettingsName(SettingsKey.layer_height),
									settings.GetValue<double>(SettingsKey.layer_height),
									GetSettingsName(SettingsKey.nozzle_diameter),
									settings.GetValue<double>(SettingsKey.nozzle_diameter)),
							});
					}
					else if (settings.GetValue<double>(SettingsKey.layer_height) <= 0)
					{
						errors.Add(
							new SettingsValidationError(SettingsKey.layer_height)
							{
								Error = "{0} must be greater than 0.".Localize().FormatWith(GetSettingsName(SettingsKey.layer_height)),
							});
					}

					// make sure the first layer height is not too big
					if (settings.GetValue<double>(SettingsKey.first_layer_height) > settings.GetValue<double>(SettingsKey.nozzle_diameter))
					{
						errors.Add(
							new SettingsValidationError(SettingsKey.first_layer_height)
							{
								Error = "{0} must be less than or equal to the {1}.".Localize().FormatWith(
									GetSettingsName(SettingsKey.first_layer_height),
									GetSettingsName(SettingsKey.nozzle_diameter)),
								ValueDetails = "{0} = {1}\n{2} = {3}".FormatWith(
									GetSettingsName(SettingsKey.first_layer_height),
									settings.GetValue<double>(SettingsKey.first_layer_height),
									GetSettingsName(SettingsKey.nozzle_diameter),
									settings.GetValue<double>(SettingsKey.nozzle_diameter)),
							});
					}
					// make sure the first layer height is not too small
					else if (settings.GetValue<double>(SettingsKey.first_layer_height) < settings.GetValue<double>(SettingsKey.nozzle_diameter) / 2)
					{
						errors.Add(
							new SettingsValidationError(SettingsKey.first_layer_height)
							{
								Error = "{0} should be greater than or equal to 1/2 the {1}.".Localize().FormatWith(
									GetSettingsName(SettingsKey.first_layer_height),
									GetSettingsName(SettingsKey.nozzle_diameter)),
								ValueDetails = "{0} = {1}\n1/2 {2} = {3}".FormatWith(
									GetSettingsName(SettingsKey.first_layer_height),
									settings.GetValue<double>(SettingsKey.first_layer_height),
									GetSettingsName(SettingsKey.nozzle_diameter),
									settings.GetValue<double>(SettingsKey.nozzle_diameter) / 2),
								ErrorLevel = ValidationErrorLevel.Warning,
							});
					}

				}

				string[] startGCode = settings.GetValue(SettingsKey.start_gcode).Replace("\\n", "\n").Split('\n');

				// Print recovery is incompatible with firmware leveling - ensure not enabled in startGCode
				if (settings.GetValue<bool>(SettingsKey.recover_is_enabled)
					&& !settings.GetValue<bool>(SettingsKey.has_hardware_leveling))
				{
					// Ensure we don't have hardware leveling commands in the start gcode.
					foreach (string startGCodeLine in startGCode)
					{
						if (startGCodeLine.StartsWith("G29"))
						{
							errors.Add(
								new SettingsValidationError(SettingsKey.start_gcode)
								{
									Error = "Start G-Code cannot contain G29 if Print Recovery is enabled.".Localize(),
									Details = "Your Start G-Code should not contain a G29 if you are planning on using Print Recovery. Change your start G-Code or turn off Print Recovery.".Localize(),
								});
						}

						if (startGCodeLine.StartsWith("G30"))
						{
							errors.Add(
								new SettingsValidationError(SettingsKey.start_gcode)
								{
									Error = "Start G-Code cannot contain G30 if Print Leveling is enabled.".Localize(),
									Details = "Your Start G-Code should not contain a G30 if you are planning on using Print Recovery. Change your start G-Code or turn off Print Recovery.".Localize(),
								});
						}
					}
				}

				var levelingEnabled = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled) & !settings.GetValue<bool>(SettingsKey.has_hardware_leveling);
				var levelingRequired = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print);

				if (levelingEnabled || levelingRequired)
				{
					// Ensure we don't have hardware leveling commands in the start gcode.
					foreach (string startGCodeLine in startGCode)
					{
						if (startGCodeLine.StartsWith("G29"))
						{
							errors.Add(
								new SettingsValidationError(SettingsKey.start_gcode)
								{
									Error = "Start G-Code cannot contain G29 if Print Leveling is enabled.".Localize(),
									Details = "Your Start G-Code should not contain a G29 if you are planning on using print leveling. Change your start G-Code or turn off print leveling.".Localize(),
								});
						}

						if (startGCodeLine.StartsWith("G30"))
						{
							errors.Add(
								new SettingsValidationError(SettingsKey.start_gcode)
								{
									Error = "Start G-Code cannot contain G30 if Print Leveling is enabled.".Localize(),
									Details = "Your Start G-Code should not contain a G30 if you are planning on using print leveling. Change your start G-Code or turn off print leveling.".Localize(),
								});
						}
					}

					bool heatedBed = printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed);

					double bedTemperature = printer.Settings.Helpers.ActiveBedTemperature;

					if (heatedBed
						&& printer.Connection.IsConnected
						&& !PrinterSetupRequired(printer)
						&& printer.Settings.Helpers.PrintLevelingData is PrintLevelingData levelingData
						&& !levelingData.IssuedLevelingTempWarning
						&& Math.Abs(bedTemperature - levelingData.BedTemperature) > 10
						&& !printer.Settings.Helpers.ValidateLevelingWithProbe)
					{
						errors.Add(
							new ValidationError(ValidationErrors.BedLevelingTemperature)
							{
								Error = "Bed Leveling Temperature".Localize(),
								Details = string.Format(
									"Bed Leveling data created at {0}°C versus current {1}°C".Localize(),
									levelingData.BedTemperature,
									bedTemperature),
								ErrorLevel = ValidationErrorLevel.Warning,
								FixAction = new NamedAction()
								{
									Title = "Recalibrate",
									Action = () =>
									{
										UiThread.RunOnIdle(() =>
										{
											DialogWindow.Show(new PrintLevelingWizard(printer));
										});
									},
									IsEnabled = () => printer.Connection.IsConnected
								}
							});
					}

					if (levelingEnabled
						&& !settings.GetValue<bool>(SettingsKey.has_hardware_leveling)
						&& settings.GetValue<bool>(SettingsKey.has_z_probe)
						&& settings.GetValue<bool>(SettingsKey.use_z_probe)
						&& settings.GetValue<bool>(SettingsKey.validate_leveling)
						&& (settings.GetValue<double>(SettingsKey.validation_threshold) < .001
						|| settings.GetValue<double>(SettingsKey.validation_threshold) > .5))
					{
						var threshold = settings.GetValue<double>(SettingsKey.validation_threshold);
						errors.Add(
							new SettingsValidationError(SettingsKey.validation_threshold)
							{
								Error = "The Validation Threshold mush be greater than 0 and less than .5mm.".Localize().FormatWith(threshold),
								ValueDetails = "{0} = {1}".FormatWith(GetSettingsName(SettingsKey.validation_threshold), threshold),
							});
					}

					// check if the leveling data has too large a range
					if (printer.Settings.Helpers.PrintLevelingData.SampledPositions.Count > 3)
					{
						var minLevelZ = double.MaxValue;
						var maxLevelZ = double.MinValue;
						foreach (var levelPosition in printer.Settings.Helpers.PrintLevelingData.SampledPositions)
						{
							minLevelZ = Math.Min(minLevelZ, levelPosition.Z);
							maxLevelZ = Math.Max(maxLevelZ, levelPosition.Z);
						}

						var delta = maxLevelZ - minLevelZ;
						var maxDelta = printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter) * 10;
						if (delta > maxDelta)
						{
							errors.Add(
								new ValidationError(ValidationErrors.BedLevelingMesh)
								{
									Error = "Leveling Data Warning".Localize(),
									Details = "The leveling data might be invalid. It changes by as much as {0:0.##}mm. Leveling calibration should be re-run".Localize().FormatWith(delta),
									ErrorLevel = ValidationErrorLevel.Warning,
									FixAction = new NamedAction()
									{
										Title = "Recalibrate",
										Action = () =>
										{
											UiThread.RunOnIdle(() =>
											{
												DialogWindow.Show(new PrintLevelingWizard(printer));
											});
										},
										IsEnabled = () => printer.Connection.IsConnected
									}
								});
						}
					}
				}

				printer.Settings.ForTools<double>(SettingsKey.baby_step_z_offset, (key, value, i) =>
				{
					// Make sure the z offsets are not too big
					if (Math.Abs(value) > 2)
					{
						// Static path generation for non-SliceSettings value
						var location = "Location".Localize() + ":"
							+ "\n" + "Controls".Localize()
							+ "\n  • " + "Movement".Localize()
							+ "\n    • " + "Z Offset".Localize();

						errors.Add(
							new ValidationError(ValidationErrors.ZOffset)
							{
								Error = "Z Offset is too large.".Localize(),
								Details = string.Format(
									"{0}\n\n{1}",
									"The Z Offset for your printer, sometimes called Baby Stepping, is greater than 2mm and invalid. Clear the value and re-level the bed.".Localize(),
									location)
							});
					}
				});

				if (settings.GetValue<double>(SettingsKey.first_layer_extrusion_width) > settings.GetValue<double>(SettingsKey.nozzle_diameter) * 4)
				{
					errors.Add(
						new SettingsValidationError(SettingsKey.first_layer_extrusion_width)
						{
							Error = "{0} must be less than or equal to the {1} * 4.".Localize().FormatWith(
								GetSettingsName(SettingsKey.first_layer_extrusion_width),
								GetSettingsName(SettingsKey.nozzle_diameter)),
							ValueDetails = "{0} = {1}\n{2} * 4 = {3}".FormatWith(
								GetSettingsName(SettingsKey.first_layer_extrusion_width),
								settings.GetValue<double>(SettingsKey.first_layer_extrusion_width),
								GetSettingsName(SettingsKey.nozzle_diameter),
								settings.GetValue<double>(SettingsKey.nozzle_diameter) * 4)
						});
				}

				if (settings.GetValue<double>(SettingsKey.first_layer_extrusion_width) <= 0)
				{
					errors.Add(
						new SettingsValidationError(SettingsKey.first_layer_extrusion_width)
						{
							Error = "{0} must be greater than 0.".Localize().FormatWith(
								GetSettingsName(SettingsKey.first_layer_extrusion_width)),
							ValueDetails = "{0} = {1}".FormatWith(
								GetSettingsName(SettingsKey.first_layer_extrusion_width),
								settings.GetValue<double>(SettingsKey.first_layer_extrusion_width)),
						});
				}

				if (settings.GetValue<double>(SettingsKey.fill_density) <= 0)
				{
					errors.Add(
						new SettingsValidationError(SettingsKey.fill_density)
						{
							Error = "{0} should be greater than 0.".Localize().FormatWith(
								GetSettingsName(SettingsKey.fill_density)),
							ErrorLevel = ValidationErrorLevel.Warning,
							ValueDetails = "{0} = {1}".FormatWith(
								GetSettingsName(SettingsKey.fill_density),
								settings.GetValue<double>(SettingsKey.fill_density)),
						});
				}

				if (settings.GetValue<double>(SettingsKey.perimeters) <= 0)
				{
					errors.Add(
						new SettingsValidationError(SettingsKey.perimeters)
						{
							Error = "{0} should be greater than 0.".Localize().FormatWith(
								GetSettingsName(SettingsKey.perimeters)),
							ErrorLevel = ValidationErrorLevel.Warning,
							ValueDetails = "{0} = {1}".FormatWith(
								GetSettingsName(SettingsKey.perimeters),
								settings.GetValue<double>(SettingsKey.perimeters)),
						});
				}

				if (settings.GetValue<int>(SettingsKey.extruder_count) > 1
					&& settings.GetValue<double>(SettingsKey.wipe_tower_perimeters_per_extruder) < 3)
				{
					errors.Add(
						new SettingsValidationError(SettingsKey.wipe_tower_perimeters_per_extruder)
						{
							Error = "{0} should be greater than 2.".Localize().FormatWith(
								GetSettingsName(SettingsKey.wipe_tower_perimeters_per_extruder)),
							ErrorLevel = ValidationErrorLevel.Warning,
							ValueDetails = "{0} = {1}".FormatWith(
								GetSettingsName(SettingsKey.wipe_tower_perimeters_per_extruder),
								settings.GetValue<double>(SettingsKey.wipe_tower_perimeters_per_extruder)),
						});
				}

				if (settings.GetValue<double>(SettingsKey.infill_overlap_perimeter) < -settings.GetValue<double>(SettingsKey.nozzle_diameter)
					|| settings.GetValue<double>(SettingsKey.infill_overlap_perimeter) > settings.GetValue<double>(SettingsKey.nozzle_diameter))
				{
					errors.Add(
						new SettingsValidationError(SettingsKey.infill_overlap_perimeter)
						{
							Error = "{0} must be greater than 0 and less than your nozzle diameter. You may be missing a '%'.".Localize().FormatWith(
								GetSettingsName(SettingsKey.infill_overlap_perimeter)),
							ValueDetails = "{0} = {1}, {2} = {3}".FormatWith(
								GetSettingsName(SettingsKey.infill_overlap_perimeter),
								settings.GetValue<double>(SettingsKey.infill_overlap_perimeter),
								GetSettingsName(SettingsKey.nozzle_diameter),
								settings.GetValue<double>(SettingsKey.nozzle_diameter)),
						});
				}

				if (printer.Connection.IsConnected
					&& printer.Settings?.Helpers.ComPort() == "Emulator"
					&& fffPrinter)
				{
					errors.Add(
						new SettingsValidationError(SettingsKey.com_port, "Connected to Emulator".Localize())
						{
							Error = "You are connected to the Emulator not an actual printer.".Localize(),
							ErrorLevel = ValidationErrorLevel.Warning,
							FixAction = new NamedAction()
							{
								Title = "Switch".Localize(),
								IsEnabled = () => !printer.Connection.Printing && !printer.Connection.Paused,
								Action = () => UiThread.RunOnIdle(() =>
								{
									// make sure we are not connected or we can't change the port
									printer.Connection.Disable();

									// User initiated connect attempt failed, show port selection dialog
									DialogWindow.Show(new SetupStepComPortOne(printer));
								})
							}
						});
				}

				if (settings.GetValue<double>(SettingsKey.external_perimeter_extrusion_width) <= 0)
				{
					errors.Add(
						new SettingsValidationError(SettingsKey.external_perimeter_extrusion_width)
						{
							Error = "{0} must be greater than 0.".Localize().FormatWith(
								GetSettingsName(SettingsKey.external_perimeter_extrusion_width)),
							ValueDetails = "{0} = {1}".FormatWith(
								GetSettingsName(SettingsKey.external_perimeter_extrusion_width),
								settings.GetValue<double>(SettingsKey.external_perimeter_extrusion_width)),
						});
				}

				if (settings.GetValue<double>(SettingsKey.min_fan_speed) > 100)
				{
					errors.Add(
						new SettingsValidationError(SettingsKey.min_fan_speed)
						{
							Error = "The {0} can only go as high as 100%.".Localize().FormatWith(
								GetSettingsName(SettingsKey.min_fan_speed)),
							ValueDetails = "It is currently set to {0}.".Localize().FormatWith(
								settings.GetValue<double>(SettingsKey.min_fan_speed)),
						});
				}

				if (settings.GetValue<double>(SettingsKey.max_fan_speed) > 100)
				{
					errors.Add(
						new SettingsValidationError(SettingsKey.max_fan_speed)
						{
							Error = "The {0} can only go as high as 100%.".Localize().FormatWith(
								GetSettingsName(SettingsKey.max_fan_speed)),
							ValueDetails = "It is currently set to {0}.".Localize().FormatWith(
								settings.GetValue<double>(SettingsKey.max_fan_speed)),
						});
				}

				if (extruderCount < 1)
				{
					errors.Add(
						new SettingsValidationError(SettingsKey.extruder_count)
						{
							Error = "The {0} must be at least 1.".Localize().FormatWith(
								GetSettingsName(SettingsKey.extruder_count)),
							ValueDetails = "It is currently set to {0}.".Localize().FormatWith(extruderCount),
						});
				}

				if (settings.GetValue<double>(SettingsKey.fill_density) < 0 || settings.GetValue<double>(SettingsKey.fill_density) > 1)
				{
					errors.Add(
						new SettingsValidationError(SettingsKey.fill_density)
						{
							Error = "The {0} must be between 0 and 1.".Localize().FormatWith(
								GetSettingsName(SettingsKey.fill_density)),
							ValueDetails = "It is currently set to {0}.".Localize().FormatWith(
								settings.GetValue<double>(SettingsKey.fill_density)),
						});
				}

				// marlin firmware can only take a max of 128 bytes in a single instruction, make sure no lines are longer than that
				ValidateGCodeLinesShortEnough(SettingsKey.cancel_gcode, printer, errors);
				ValidateGCodeLinesShortEnough(SettingsKey.connect_gcode, printer, errors);
				ValidateGCodeLinesShortEnough(SettingsKey.end_gcode, printer, errors);
				ValidateGCodeLinesShortEnough(SettingsKey.layer_gcode, printer, errors);
				ValidateGCodeLinesShortEnough(SettingsKey.pause_gcode, printer, errors);
				ValidateGCodeLinesShortEnough(SettingsKey.resume_gcode, printer, errors);
				ValidateGCodeLinesShortEnough(SettingsKey.start_gcode, printer, errors);

				// If the given speed is part of the current slice engine then check that it is greater than 0.
				ValidateGoodSpeedSettingGreaterThan0(SettingsKey.bridge_speed, printer, errors);
				ValidateGoodSpeedSettingGreaterThan0(SettingsKey.air_gap_speed, printer, errors);
				ValidateGoodSpeedSettingGreaterThan0(SettingsKey.external_perimeter_speed, printer, errors);
				ValidateGoodSpeedSettingGreaterThan0(SettingsKey.first_layer_speed, printer, errors);
				ValidateGoodSpeedSettingGreaterThan0(SettingsKey.infill_speed, printer, errors);
				ValidateGoodSpeedSettingGreaterThan0(SettingsKey.perimeter_speed, printer, errors);
				ValidateGoodSpeedSettingGreaterThan0(SettingsKey.small_perimeter_speed, printer, errors);
				ValidateGoodSpeedSettingGreaterThan0(SettingsKey.solid_infill_speed, printer, errors);
				ValidateGoodSpeedSettingGreaterThan0(SettingsKey.support_material_speed, printer, errors);
				ValidateGoodSpeedSettingGreaterThan0(SettingsKey.top_solid_infill_speed, printer, errors);
				ValidateGoodSpeedSettingGreaterThan0(SettingsKey.travel_speed, printer, errors);
				ValidateGoodSpeedSettingGreaterThan0(SettingsKey.retract_speed, printer, errors);
			}
			catch (Exception e)
			{
				errors.Add(
					new ValidationError(ValidationErrors.ExceptionDuringSliceSettingsValidation)
					{
						Error = "Unexpected error validating settings".Localize(),
						Details = e.Message
					});
			}

			if (printer.Connection.IsConnected
				&& !PrinterSetupRequired(printer)
				&& validatePrintBed
				&& errors.Count(e => e.ErrorLevel == ValidationErrorLevel.Error) == 0
				&& printer.PrintableItems(printer.Bed.Scene).Any()
				&& settings.GetValue<bool>(SettingsKey.has_swappable_bed)
				&& settings.GetValue(SettingsKey.bed_surface) == "Default")
			{
				errors.Add(new ValidationError(ValidationErrors.BedSurfaceNotSelected)
				{
					Error = "Bed Surface Needs to be Selected".Localize(),
					Details = "You need to select your printer's 'Bed Surface' under the 'Bed Temperature' menu on the top right of your screen.".Localize(),
					ErrorLevel = ValidationErrorLevel.Error,
				});
			}

			if (printer.Connection.IsConnected
				&& !PrinterSetupRequired(printer)
				&& validatePrintBed
				&& errors.Count(e => e.ErrorLevel == ValidationErrorLevel.Error) == 0
				&& printer.PrintableItems(printer.Bed.Scene).Any()
				&& settings.GetValue<bool>(SettingsKey.has_swappable_bed)
				&& settings.GetValue(SettingsKey.bed_surface) != "Default"
				&& settings.GetValue(printer.Settings.Helpers.ActiveBedTemperatureSetting) == "NC")
			{
				errors.Add(new ValidationError(ValidationErrors.IncompatableBedSurfaceAndMaterial)
				{
					Error = "Selected Material and Bed Surface are Incompatable".Localize(),
					Details = "The 'Material' you have selected is incompatible with the 'Bed Surface' you have selected. You may get poor bed adhesion or printing results. Changing the 'Bed Surface' is recommended. You can change it in the 'Bed Temperature' menu on the top right of your screen.".Localize(),
					ErrorLevel = ValidationErrorLevel.Warning,
				});
			}

			if (printer.Connection.IsConnected
				&& !PrinterSetupRequired(printer)
				&& validatePrintBed
				&& errors.Count(e => e.ErrorLevel == ValidationErrorLevel.Error) == 0
				&& printer.PrintableItems(printer.Bed.Scene).Any()
				&& string.IsNullOrEmpty(settings.GetValue(SettingsKey.active_material_key)))
			{
				errors.Add(new ValidationError(ValidationErrors.MaterialNotSelected)
				{
					Error = "A Material Should be Selected".Localize(),
					Details = "You should select the 'Material' your are printing with under the 'Hotend Temperature' menu on the top right of your screen.".Localize(),
					ErrorLevel = ValidationErrorLevel.Warning,
				});
			}

			return errors;
		}

		/// <summary>
		/// Validates printer satisfies all requirements.
		/// </summary>
		/// <param name="printer">The printer to validate.</param>
		/// <returns>A list of all warnings and errors.</returns>
		public static List<ValidationError> Validate(this PrinterConfig printer)
		{
			var errors = new List<ValidationError>();

			var fffPrinter = printer.Settings.Slicer.PrinterType == PrinterType.FFF;

			if (!printer.Connection.IsConnected
				&& fffPrinter)
			{
				errors.Add(new ValidationError(ValidationErrors.PrinterDisconnected)
				{
					Error = "Printer Disconnected".Localize(),
					Details = "Connect to your printer to continue".Localize(),
					FixAction = new NamedAction()
					{
						Title = "Connect".Localize(),
						Action = () => ApplicationController.Instance.ConnectToPrinter(printer)
					}
				});
			}

			if (PrinterSetupRequired(printer))
			{
				errors.Add(new ValidationError(ValidationErrors.PrinterSetupRequired)
				{
					Error = "Printer Setup Required".Localize(),
					Details = "Printer Setup must be run before printing".Localize(),
					FixAction = new NamedAction()
					{
						ID = "SetupPrinter",
						Title = "Setup".Localize() + "...",
						Action = () =>
						{
							UiThread.RunOnIdle(() =>
							{
								DialogWindow.Show(
									new PrinterCalibrationWizard(printer, AppContext.Theme),
									advanceToIncompleteStage: true);
							});
						}
					}
				});
			}

			// Concatenate printer and settings errors
			errors.AddRange(printer.ValidateSettings(validatePrintBed: !printer.Bed.EditContext.IsGGCodeSource));

			return errors;
		}

		private static bool PrinterSetupRequired(PrinterConfig printer)
		{
			return printer.Connection.IsConnected
				&& PrinterCalibrationWizard.SetupRequired(printer, requiresLoadedFilament: true);
		}

		private static string GetSettingsName(string settingsKey)
		{
			var settingData = PrinterSettings.SettingsData[settingsKey];
			return settingData.PresentationName.Localize();
		}

		private static bool ValidateGCodeLinesShortEnough(string settingsKey, PrinterConfig printer, List<ValidationError> errors)
		{
			// make sure the custom gcode does not have lines too long to print
			foreach (string line in printer.Settings.GetValue(settingsKey).Replace("\\n", "\n").Split('\n'))
			{
				var trimedLine = line.Split(';')[0].Trim();
				var length = trimedLine.Length;
				if (length > 100)
				{
					var details = "Found a line that is {0} characters long.\n{1}...".Localize().FormatWith(length, trimedLine.Substring(0, 20));
					errors.Add(
						new SettingsValidationError(settingsKey)
						{
							Error = "All G-Code lines mush be shorter than 100 characters (excluding comments).".Localize().FormatWith(
								GetSettingsName(settingsKey)),
							Details = details,
						});

					return false;
				}
			}

			return true;
		}

		private static void ValidateGoodSpeedSettingGreaterThan0(string settingsKey, PrinterConfig printer, List<ValidationError> errors)
		{
			var actualSpeedValueString = printer.Settings.GetValue(settingsKey);
			var speedValueString = actualSpeedValueString;
			if (speedValueString.EndsWith("%"))
			{
				speedValueString = speedValueString.Substring(0, speedValueString.Length - 1);
			}

			bool valueWasNumber = true;

			if (!double.TryParse(speedValueString, out double speedToCheck))
			{
				valueWasNumber = false;
			}

			if (!valueWasNumber
				|| (printer.Settings.IsActive(settingsKey)
				&& speedToCheck <= 0))
			{
					errors.Add(
						new SettingsValidationError(settingsKey)
						{
							Error = "The {0} must be greater than 0.".Localize().FormatWith(GetSettingsName(settingsKey)),
							Details = "It is currently set to {0}.".Localize().FormatWith(actualSpeedValueString),
						});
			}
		}
	}
}