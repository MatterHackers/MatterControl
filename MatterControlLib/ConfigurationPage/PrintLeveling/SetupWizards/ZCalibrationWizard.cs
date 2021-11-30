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
using MatterHackers.Agg;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class ZCalibrationWizard : PrinterSetupWizard
	{
		private readonly double[] babySteppingValue = new double[4];

		public ZCalibrationWizard(PrinterConfig printer)
			: base(printer)
		{
			this.Title = "Z Calibration".Localize();
		}

		public override bool SetupRequired => NeedsToBeRun(printer);

		public override bool Visible
		{
			get
			{
				return (printer.Settings.Helpers.ProbeBeingUsed
						&& !printer.Settings.GetValue<bool>(SettingsKey.has_hardware_leveling))
						|| printer.Settings.GetValue<int>(SettingsKey.extruder_count) > 1;
			}
		}

		public override bool Enabled => printer.Connection.IsConnected && !printer.Connection.Printing && !printer.Connection.Paused;

		private string PageTitle => this.Title + " " + "Wizard".Localize();

		private Vector2 ProbePosition => LevelingPlan.ProbeOffsetSamplePosition(printer);

		private double StartProbeHeight => printer.Settings.GetValue<double>(SettingsKey.print_leveling_probe_start);

		private Vector3 ProbeStartPosition => new Vector3(ProbePosition, StartProbeHeight);

        public static bool NeedsToBeRun(PrinterConfig printer)
		{
			// we have a probe that we are using and we have not done leveling yet
			return UsingZProbe(printer) && !printer.Settings.GetValue<bool>(SettingsKey.probe_has_been_calibrated);
		}

		public override void Dispose()
		{
			// If leveling was on when we started, make sure it is on when we are done.
			printer.Connection.AllowLeveling = true;

			// set the baby stepping back to the last known good value
			printer.Settings.ForTools<double>(SettingsKey.baby_step_z_offset, (key, value, i) =>
			{
				printer.Settings.SetValue(key, value.ToString());
			});

			// make sure we raise the probe on close
			if (printer.Settings.Helpers.ProbeBeingUsed
				&& printer.Settings.GetValue<bool>(SettingsKey.has_z_servo))
			{
				// make sure the servo is retracted
				var servoRetract = printer.Settings.GetValue<double>(SettingsKey.z_servo_retracted_angle);
				printer.Connection.QueueLine($"M280 P0 S{servoRetract}");
			}
		}

		public static bool UsingZProbe(PrinterConfig printer)
		{
			var required = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print);

			// we have a probe that we are using and we have not done leveling yet
			return (required || printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled))
				&& printer.Settings.Helpers.ProbeBeingUsed;
		}

		protected override IEnumerator<WizardPage> GetPages()
		{
			var levelingStrings = new LevelingStrings();
			var autoProbePositions = new List<PrintLevelingWizard.ProbePosition>(1);
			int hotendCount = Math.Min(2, printer.Settings.Helpers.HotendCount());
			var manualProbePositions = new List<List<PrintLevelingWizard.ProbePosition>>(hotendCount);
			for (int i = 0; i < hotendCount; i++)
			{
				manualProbePositions.Add(new List<PrintLevelingWizard.ProbePosition>());
				manualProbePositions[i] = new List<PrintLevelingWizard.ProbePosition>(1)
				{
					new PrintLevelingWizard.ProbePosition()
				};
			}

			autoProbePositions.Add(new PrintLevelingWizard.ProbePosition());

			// show what steps will be taken
			yield return new WizardPage(
				this,
				string.Format("{0} {1}", this.Title, "Overview".Localize()),
				string.Format(
					"{0}\n\n{1}\n\n{2}\n\n",
					"Z Calibration measures the z position of the nozzles.".Localize(),
					"This data is required for software print leveling and ensures good first layer adhesion.".Localize(),
					"Click 'Next' to continue.".Localize()))
				{
					WindowTitle = Title,
				};

			// Initialize - turn off print leveling
			printer.Connection.AllowLeveling = false;

			printer.Settings.ForTools<double>(SettingsKey.baby_step_z_offset, (key, value, i) =>
			{
				// remember the current baby stepping values
				babySteppingValue[i] = value;
				// clear them while we measure the offsets
				printer.Settings.SetValue(key, "0");
			});

			// Require user confirmation after this point
			this.RequireCancelConfirmation = true;

			// add in the homing printer page
			yield return new HomePrinterPage(
				this,
				levelingStrings.HomingPageInstructions(true, false));

			if (LevelingPlan.NeedsToBeRun(printer))
			{
				// start heating up the bed as that will be needed next
				var bedTemperature = printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed) ?
					printer.Settings.GetValue<double>(SettingsKey.bed_temperature)
					: 0;
				if (bedTemperature > 0)
				{
					printer.Connection.TargetBedTemperature = bedTemperature;
				}
			}

			var extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);
			var temps = new double[4];
			for (int i = 0; i < extruderCount; i++)
			{
				temps[i] = printer.Settings.Helpers.ExtruderTargetTemperature(i);
			}

			yield return new WaitForTempPage(
				this,
				"Heating the printer".Localize(),
				((extruderCount == 1) ? "Waiting for the hotend to heat to ".Localize() + temps[0] + "°C.\n" : "Waiting for the hotends to heat up.".Localize())
					+ "This will ensure that no filament is stuck to your nozzle.".Localize() + "\n"
					+ "\n"
					+ "Warning! The tip of the nozzle will be HOT!".Localize() + "\n"
					+ "Avoid contact with your skin.".Localize(),
				0,
				temps);

			int extruderPriorToMeasure = printer.Connection.ActiveExtruderIndex;

			if (extruderCount > 1)
			{
				// reset the extruder that was active
				printer.Connection.QueueLine($"T0");
			}

			foreach(var page in DoManualOffsetMeasurment(levelingStrings, autoProbePositions, manualProbePositions))
			{
				yield return page;
			}


			if (extruderCount > 1)
			{
				// reset the extruder that was active
				printer.Connection.QueueLine($"T{extruderPriorToMeasure}");
			}

			for (int i = 0; i < extruderCount; i++)
			{
				// clear the baby stepping so we don't save the old values
				babySteppingValue[i] = 0;
			}

			if (hotendCount == 1 // this could be improved for dual extrusion calibration in the future. But for now it is single extrusion.
					&& printer.Settings.Helpers.ProbeBeingUsed
					&& printer.Settings.GetValue<bool>(SettingsKey.validate_probe_offset))
			{
				// tell them about the automatic part and any settings that should be changed
				yield return new ZProbePrintCalibrationPartPage(
					this,
					printer,
					"Validating Z Offset".Localize(),
					"We will now measure the probe offset from the top of a printed calibration object.".Localize());
				// measure the top of the part we just printed 
				yield return new ZProbeCalibrateRetrieveTopProbeData(this, PageTitle);
				// tell the user we are done and everything should be working
				yield return new ZCalibrationValidateComplete(this, PageTitle);
			}
			else
			{
				yield return new CalibrateProbeRemovePaperInstructions(this, PageTitle);
			}
		}

		private IEnumerable<WizardPage> DoManualOffsetMeasurment(LevelingStrings levelingStrings,
			List<PrintLevelingWizard.ProbePosition> autoProbePositions,
			List<List<PrintLevelingWizard.ProbePosition>> manualProbePositions)
        {
			int hotendCount = Math.Min(2, printer.Settings.Helpers.HotendCount());

			if (printer.Settings.Helpers.ProbeBeingUsed)
			{
				// do the automatic probing of the center position
				yield return new AutoProbeFeedback(
					this,
					ProbeStartPosition,
					"Probe at bed center".Localize(),
					"Sample the bed center position to determine the probe distance to the bed".Localize(),
					autoProbePositions,
					0);
			}

			if (hotendCount == 1
				&& printer.Settings.Helpers.ProbeBeingUsed
				&& printer.Settings.GetValue<bool>(SettingsKey.has_conductive_nozzle)
				&& printer.Settings.GetValue<bool>(SettingsKey.measure_probe_offset_conductively))
			{
				var conductiveProbeFeedback = new ConductiveProbeFeedback(
					this,
					ProbeStartPosition,
					"Conductive Probing".Localize(),
					"Measure the nozzle to probe offset using the conductive pad.".Localize(),
					manualProbePositions[0]);
				yield return conductiveProbeFeedback;

				if (conductiveProbeFeedback.MovedBelowMinZ)
				{
					// show an error message
					yield return new WizardPage(
						this,
						"Error: Below Conductive Probe Min Z".Localize(),
						"The printer moved below the minimum height set for conductive probing. Check that the nozzle is clean and there is continuity with the pad.".Localize());
				}
				else // found a good probe height
				{
					SetExtruderOffset(autoProbePositions, manualProbePositions, 0);
				}
			}
			else // collect the probe information manually
			{
				// show what steps will be taken
				yield return new WizardPage(
					this,
					"Measure the nozzle offset".Localize(),
					"{0}:\n\n\t• {1}\n\n{2}\n\n{3}".FormatWith(
						"To complete the next few steps you will need".Localize(),
						"A sheet of paper".Localize(),
						"We will use this paper to measure the distance between the nozzle and the bed.".Localize(),
						"Click 'Next' to continue.".Localize()));

				var extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);
				int totalSteps = 3 * hotendCount;

				for (int extruderIndex = 0; extruderIndex < hotendCount; extruderIndex++)
				{
					if (extruderCount > 1)
					{
						// reset the extruder that was active
						printer.Connection.QueueLine($"T{extruderIndex}");
					}

					// do the manual probe of the same position
					yield return new GetCoarseBedHeight(
						this,
						new Vector3(ProbePosition, StartProbeHeight),
						string.Format(
							"{0} {1} {2} - {3}",
							levelingStrings.GetStepString(totalSteps),
							"Position".Localize(),
							1,
							"Low Precision".Localize()),
						manualProbePositions[extruderIndex],
						0,
						levelingStrings);

					yield return new GetFineBedHeight(
						this,
						string.Format(
							"{0} {1} {2} - {3}",
							levelingStrings.GetStepString(totalSteps),
							"Position".Localize(),
							1,
							"Medium Precision".Localize()),
						manualProbePositions[extruderIndex],
						0,
						levelingStrings);

					yield return new GetUltraFineBedHeight(
						this,
						string.Format(
							"{0} {1} {2} - {3}",
							levelingStrings.GetStepString(totalSteps),
							"Position".Localize(),
							1,
							"High Precision".Localize()),
						manualProbePositions[extruderIndex],
						0,
						levelingStrings);

					SetExtruderOffset(autoProbePositions, manualProbePositions, extruderIndex);
				}
			}

			// let the user know we are done with the manual part
			yield return new CalibrateProbeRemovePaperInstructions(this, PageTitle, false);
		}

		private void SetExtruderOffset(List<PrintLevelingWizard.ProbePosition> autoProbePositions, List<List<PrintLevelingWizard.ProbePosition>> manualProbePositions, int extruderIndex)
		{
			if (extruderIndex == 0)
			{
				// set the probe z offset
				double newProbeOffset = autoProbePositions[0].Position.Z - manualProbePositions[0][0].Position.Z;
				var probe_offset = printer.Settings.GetValue<Vector3>(SettingsKey.probe_offset);
				probe_offset.Z = -newProbeOffset;
				printer.Settings.SetValue(SettingsKey.probe_offset, $"{probe_offset.X},{probe_offset.Y},{probe_offset.Z}");

				printer.Settings.SetValue(SettingsKey.probe_has_been_calibrated, "1");
			}
			else if (extruderIndex > 0)
			{
				// store the offset into the extruder offset z position
				double newZOffset;

				if (printer.Settings.Helpers.ProbeBeingUsed)
				{
					var extruderOffset = autoProbePositions[0].Position.Z - manualProbePositions[extruderIndex][0].Position.Z;
					var hotend0Offset = printer.Settings.GetValue<Vector3>(SettingsKey.probe_offset);
					newZOffset = extruderOffset + hotend0Offset.Z;
				}
				else
				{
					newZOffset = manualProbePositions[0][0].Position.Z - manualProbePositions[extruderIndex][0].Position.Z;
				}

				printer.Settings.Helpers.SetExtruderZOffset(1, newZOffset);
			}
		}
	}
}