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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class PrintLevelWizardControl : WizardControl
	{
		protected PrinterConfig printer;
		ThemeConfig theme;
		LevelWizardBase levelWizard;

		public PrintLevelWizardControl(PrinterConfig printer, LevelWizardBase levelWizard, ThemeConfig theme)
		{
			this.levelWizard = levelWizard;
			this.theme = theme;
			this.printer = printer;
		}

		protected override IEnumerator<InstructionsPage> Pages
		{
			get
			{
				var probePositions = new List<ProbePosition>(levelWizard.ProbeCount);
				for (int j = 0; j < levelWizard.ProbeCount; j++)
				{
					probePositions.Add(new ProbePosition());
				}

				var levelingStrings = new LevelingStrings(printer.Settings);

				this.doneButton.Visible = false;

				// If no leveling data has been calculated
				bool showWelcomeScreen = printer.Settings.Helpers.GetPrintLevelingData().SampledPositions.Count == 0
					&& !ProbeCalibrationWizard.UsingZProbe(printer);

				if (showWelcomeScreen)
				{
					yield return new InstructionsPage(
						printer,
						levelingStrings.initialPrinterSetupStepText,
						string.Format(
							"{0}\n\n{1}",
							"Congratulations on connecting to your printer. Before starting your first print we need to run a simple calibration procedure.".Localize(),
							"The next few screens will walk your through calibrating your printer.".Localize()),
						theme);
				}

				// To make sure the bed is at the correct temp, put in a filament selection page.
				bool hasHeatedBed = printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed);
				bool useZProbe = printer.Settings.Helpers.UseZProbe();
				int zProbeSamples = printer.Settings.GetValue<int>(SettingsKey.z_probe_samples);

				var secondsPerManualSpot = 10 * 3;
				var secondsPerAutomaticSpot = 3 * zProbeSamples;
				var secondsToCompleteWizard = levelWizard.ProbeCount * (useZProbe ? secondsPerAutomaticSpot : secondsPerManualSpot);
				secondsToCompleteWizard += (hasHeatedBed ? 60 * 3 : 0);

				yield return new InstructionsPage(
					printer,
					"Print Leveling Overview".Localize(),
					levelingStrings.WelcomeText(levelWizard.ProbeCount, (int)Math.Round(secondsToCompleteWizard / 60.0)),
					theme);

				// If we need to heat the bed or the extruder, select the current material
				if (hasHeatedBed || !useZProbe)
				{
					yield return new SelectMaterialPage(
						printer,
						"Select Material".Localize(),
						"Please select the material you will be printing with, so we can accurately calibrate the printer.".Localize(),
						theme);
				}

				yield return new HomePrinterPage(
					printer,
					this,
					"Homing The Printer".Localize(),
					levelingStrings.HomingPageInstructions(useZProbe, hasHeatedBed),
					useZProbe,
					theme);

				// figure out the heating requirements
				double targetBedTemp = 0;
				double targetHotendTemp = 0;
				if (hasHeatedBed)
				{
					targetBedTemp = printer.Settings.GetValue<double>(SettingsKey.bed_temperature);
				}

				if (!useZProbe)
				{
					targetHotendTemp = printer.Settings.Helpers.ExtruderTemperature(0);
				}

				if (targetBedTemp > 0 || targetHotendTemp > 0)
				{
					string heatingInstructions = "";
					if (targetBedTemp > 0 && targetHotendTemp > 0)
					{
						// heating both the bed and the hotend
						heatingInstructions = $"Waiting for the bed to heat to {targetBedTemp}".Localize() + "\n"
							+ $"and the hotend to heat to {targetHotendTemp}.".Localize() + "\n"
							+ "\n"
							+ "This will improve the accuracy of print leveling".Localize()
							+ "and ensure no filament is stuck to the tip of the extruder.".Localize() + "\n"
							+ "\n"
							+ "Warning! The tip of the nozzle will be HOT!".Localize() + "\n"
							+ "Avoid contact with your skin.".Localize();
					}
					else if (targetBedTemp > 0)
					{
						// only heating the bed
						heatingInstructions = $"Waiting for the bed to heat to {targetBedTemp}.".Localize() + "\n"
							+ "This will improve the accuracy of print leveling.".Localize();
					}
					else // targetHotendTemp > 0
					{
						// only heating the hotend
						heatingInstructions += $"Waiting for the hotend to heat to {targetHotendTemp}.".Localize() + "\n"
							+ "This will ensure no filament is stuck to the tip.".Localize() + "\n"
							+ "\n"
							+ "Warning! The tip of the nozzle will be HOT!".Localize() + "\n"
							+ "Avoid contact with your skin.".Localize();
					}

					yield return new WaitForTempPage(printer, this,
						"Waiting For Printer To Heat".Localize(), heatingInstructions,
						targetBedTemp, targetHotendTemp,
						theme);
				}

				string positionLabel = "Position".Localize();
				string autoCalibrateLabel = "Auto Calibrate".Localize();
				string lowPrecisionLabel = "Low Precision".Localize();
				string medPrecisionLabel = "Medium Precision".Localize();
				string highPrecisionLabel = "High Precision".Localize();

				double bedRadius = Math.Min(printer.Settings.GetValue<Vector2>(SettingsKey.bed_size).X, printer.Settings.GetValue<Vector2>(SettingsKey.bed_size).Y) / 2;

				double startProbeHeight = printer.Settings.GetValue<double>(SettingsKey.print_leveling_probe_start);
				int i = 0;
				foreach (var goalProbePosition in levelWizard.GetPrintLevelPositionToSample())
				{
					var validProbePosition = EnsureInPrintBounds(printer.Settings, goalProbePosition);

					if (printer.Settings.Helpers.UseZProbe())
					{
						yield return new AutoProbeFeedback(
							printer,
							this,
							new Vector3(validProbePosition, startProbeHeight),
							string.Format(
								"{0} {1} {2} - {3}",
								$"{"Step".Localize()} {i + 1} {"of".Localize()} {levelWizard.ProbeCount}:",
								positionLabel,
								i + 1,
								autoCalibrateLabel),
							probePositions,
							i,
							theme);
					}
					else
					{
						yield return new GetCoarseBedHeight(
							printer,
							this,
							new Vector3(validProbePosition, startProbeHeight),
							string.Format(
								"{0} {1} {2} - {3}",
								levelingStrings.GetStepString(levelWizard.TotalSteps),
								positionLabel,
								i + 1,
								lowPrecisionLabel),
							probePositions,
							i,
							levelingStrings,
							theme);

						yield return new GetFineBedHeight(
							printer,
							this,
							string.Format(
								"{0} {1} {2} - {3}",
								levelingStrings.GetStepString(levelWizard.TotalSteps),
								positionLabel,
								i + 1,
								medPrecisionLabel),
							probePositions,
							i,
							levelingStrings,
							theme);

						yield return new GetUltraFineBedHeight(
							printer,
							this,
							string.Format(
								"{0} {1} {2} - {3}",
								levelingStrings.GetStepString(levelWizard.TotalSteps),
								positionLabel,
								i + 1,
								highPrecisionLabel),
							probePositions,
							i,
							levelingStrings,
							theme);
					}
					i++;
				}

				this.nextButton.Enabled = false;
				this.cancelButton.Visible = false;
				this.doneButton.Visible = true;

				var done1 = "Print Leveling is now configured and enabled.".Localize();
				string done2 = "If you need to recalibrate the printer in the future, the print leveling controls can be found under: Controls, Calibration";
				string done3 = "Click 'Done' to close this window.".Localize();

				var doneString = "";
				if (useZProbe)
				{
					doneString = $"{"Congratulations!".Localize()} {done1}\n"
						+ "\n"
						+ $"{done2}\n"
						+ "\n"
						+ $"{done3}";
				}
				else
				{
					doneString = $"{"Congratulations!".Localize()} {done1}\n"
						+ "\n"
						+ $"\t• {"Remove the paper".Localize()}\n"
						+ "\n"
						+ $"{done2}\n"
						+ "\n"
						+ $"{done3}";
				}

				yield return new LastPagelInstructions(
					printer,
					this,
					"Done".Localize(),
					doneString,
					probePositions,
					theme);
			}
		}

		private Vector2 EnsureInPrintBounds(PrinterSettings printerSettings, Vector2 probePosition)
		{
			// check that the position is within the printing arrea and if not move it back in
			if (printerSettings.Helpers.UseZProbe())
			{
				var probeOffset = printer.Settings.GetValue<Vector2>(SettingsKey.z_probe_xy_offset);
				var actualNozzlePosition = probePosition - probeOffset;

				// clamp this to the bed bounds
				Vector2 bedSize = printer.Settings.GetValue<Vector2>(SettingsKey.bed_size);
				Vector2 printCenter = printer.Settings.GetValue<Vector2>(SettingsKey.print_center);
				RectangleDouble bedBounds = new RectangleDouble(printCenter - bedSize / 2, printCenter + bedSize / 2);
				Vector2 adjustedPosition = bedBounds.Clamp(actualNozzlePosition);

				// and push it back into the probePosition
				probePosition = adjustedPosition + probeOffset;
			}

			return probePosition;
		}
	}

	public abstract class LevelWizardBase : SystemWindow
	{
		protected PrintLevelWizardControl printLevelWizard;
		private static SystemWindow printLevelWizardWindow;
		protected PrinterConfig printer;
		private ThemeConfig theme;

		public LevelWizardBase(PrinterConfig printer, ThemeConfig theme)
			: base(500, 370)
		{
			AlwaysOnTopOfMain = true;

			this.Title = string.Format("{0} - {1}", ApplicationController.Instance.ProductName, "Print Leveling Wizard".Localize());

			this.theme = theme;
			this.printer = printer;
		}

		public override void OnLoad(EventArgs args)
		{
			printLevelWizard = new PrintLevelWizardControl(printer, this, theme);
			AddChild(printLevelWizard);
			base.OnLoad(args);
		}

		public abstract int ProbeCount { get; }
		public int TotalSteps => ProbeCount * 3;

		public static void ShowPrintLevelWizard(PrinterConfig printer, ThemeConfig theme)
		{
			if (printLevelWizardWindow == null)
			{
				// turn off print leveling
				PrintLevelingStream.AllowLeveling = false;

				printLevelWizardWindow = LevelWizardBase.CreateAndShowWizard(printer, theme);

				printLevelWizardWindow.Closed += (sender, e) =>
				{
					// If leveling was on when we started, make sure it is on when we are done.
					PrintLevelingStream.AllowLeveling = true;

					printLevelWizardWindow = null;

					// make sure we raise the probe on close
					if (printer.Settings.GetValue<bool>(SettingsKey.has_z_probe)
						&& printer.Settings.GetValue<bool>(SettingsKey.use_z_probe)
						&& printer.Settings.GetValue<bool>(SettingsKey.has_z_servo))
					{
						// make sure the servo is retracted
						var servoRetract = printer.Settings.GetValue<double>(SettingsKey.z_servo_retracted_angle);
						printer.Connection.QueueLine($"M280 P0 S{servoRetract}");
					}
				};
			}
			else
			{
				printLevelWizardWindow.BringToFront();
			}
		}

		public IEnumerable<Vector2> GetSampleRing(int numberOfSamples, double ratio, double phase)
		{
			double bedRadius = Math.Min(printer.Settings.GetValue<Vector2>(SettingsKey.bed_size).X, printer.Settings.GetValue<Vector2>(SettingsKey.bed_size).Y) / 2;
			Vector2 bedCenter = printer.Settings.GetValue<Vector2>(SettingsKey.print_center);

			for (int i = 0; i < numberOfSamples; i++)
			{
				Vector2 position = new Vector2(bedRadius * ratio, 0);
				position.Rotate(MathHelper.Tau / numberOfSamples * i + phase);
				position += bedCenter;
				yield return position;
			}
		}

		public abstract IEnumerable<Vector2> GetPrintLevelPositionToSample();

		private static LevelWizardBase CreateAndShowWizard(PrinterConfig printer, ThemeConfig theme)
		{
			// clear any data that we are going to be acquiring (sampled positions, after z home offset)
			PrintLevelingData levelingData = new PrintLevelingData()
			{
				LevelingSystem = printer.Settings.GetValue<LevelingSystem>(SettingsKey.print_leveling_solution)
			};

			printer.Settings.SetValue(SettingsKey.baby_step_z_offset, "0");

			LevelWizardBase printLevelWizardWindow;
			switch (levelingData.LevelingSystem)
			{
				case LevelingSystem.Probe3Points:
					printLevelWizardWindow = new LevelWizard3Point(printer, theme);
					break;

				case LevelingSystem.Probe7PointRadial:
					printLevelWizardWindow = new LevelWizard7PointRadial(printer, theme);
					break;

				case LevelingSystem.Probe13PointRadial:
					printLevelWizardWindow = new LevelWizard13PointRadial(printer, theme);
					break;

				case LevelingSystem.Probe100PointRadial:
					printLevelWizardWindow = new LevelWizard100PointRadial(printer, theme);
					break;

				case LevelingSystem.Probe3x3Mesh:
					printLevelWizardWindow = new LevelWizardMesh(printer, 3, 3, theme);
					break;

				case LevelingSystem.Probe5x5Mesh:
					printLevelWizardWindow = new LevelWizardMesh(printer, 5, 5, theme);
					break;

				case LevelingSystem.Probe10x10Mesh:
					printLevelWizardWindow = new LevelWizardMesh(printer, 10, 10, theme);
					break;

				default:
					throw new NotImplementedException();
			}

			printLevelWizardWindow.ShowAsSystemWindow();
			return printLevelWizardWindow;
		}
	}

	// this class is so that it is not passed by value
	public class ProbePosition
	{
		public Vector3 position;
	}
}