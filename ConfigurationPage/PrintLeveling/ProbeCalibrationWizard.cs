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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class ProbeCalibrationRootPage : DialogPage
	{
		private PrinterConfig printer;

		public ProbeCalibrationRootPage(PrinterConfig printer)
		{
			this.printer = printer;
		}

		public override void OnLoad(EventArgs args)
		{
			var wizard = new ProbeCalibrationWizard(printer);
			wizard.ShowNextPage(WizardWindow);

			base.OnLoad(args);
		}
	}


	public class ProbeCalibrationWizard : LevelingWizardContext
	{
		public ProbeCalibrationWizard(PrinterConfig printer)
			: base(printer)
		{
		}

		protected override IEnumerator<LevelingWizardPage> GetWizardSteps()
		{
			var levelingStrings = new LevelingStrings(printer.Settings);
			var autoProbePositions = new List<ProbePosition>(3);
			var manualProbePositions = new List<ProbePosition>(3);

			autoProbePositions.Add(new ProbePosition());
			manualProbePositions.Add(new ProbePosition());

			int totalSteps = 3;

			// make a welcome page if this is the first time calibrating the probe
			if (!printer.Settings.GetValue<bool>(SettingsKey.probe_has_been_calibrated))
			{
				yield return new LevelingWizardPage(
					this,
					levelingStrings.initialPrinterSetupStepText,
					string.Format(
						"{0}\n\n{1}",
						"Congratulations on connecting to your printer. Before starting your first print we need to run a simple calibration procedure.".Localize(),
						"The next few screens will walk your through calibrating your printer.".Localize()));
			}

			// show what steps will be taken
			yield return new LevelingWizardPage(
				this,
				"Probe Calibration Overview".Localize(),
				string.Format(
					"{0}\n\n\t• {1}\n\t• {2}\n\t• {3}\n\n{4}\n\n{5}",
					"Welcome to the probe calibration wizard. Here is a quick overview on what we are going to do.".Localize(),
					"Home the printer".Localize(),
					"Probe the bed at the center".Localize(),
					"Manually measure the extruder at the center".Localize(),
					"We should be done in less than 1 minute.".Localize(),
					levelingStrings.ClickNext));

			// add in the material select page

			yield return new SelectMaterialPage(
				this,
				"Select Material".Localize(),
				string.Format(
					"{0}\n\n{1}",
					"The hot end needs to be heated to ensure it is clean.".Localize(),
					"Please select the material you will be printing, so we can heat the printer before calibrating.".Localize()));

			// add in the homing printer page
			yield return new HomePrinterPage(
				this,
				"Homing The this.Printer".Localize(),
				levelingStrings.HomingPageInstructions(true, false),
				false);

			double targetHotendTemp = 0;

			targetHotendTemp = printer.Settings.Helpers.ExtruderTemperature(0);

			yield return new WaitForTempPage(
				this,
				"Waiting For this.Printer To Heat".Localize(),
				$"Waiting for the hotend to heat to {targetHotendTemp}.".Localize() + "\n"
					+ "This will ensure no filament is stuck to the tip.".Localize() + "\n"
					+ "\n"
					+ "Warning! The tip of the nozzle will be HOT!".Localize() + "\n"
					+ "Avoid contact with your skin.".Localize(),
				0,
				targetHotendTemp);

			double startProbeHeight = printer.Settings.GetValue<double>(SettingsKey.print_leveling_probe_start);
			Vector2 probePosition = printer.Settings.GetValue<Vector2>(SettingsKey.print_center);

			int i = 0;

			// do the automatic probing of the center position
			yield return new AutoProbeFeedback(
				this,
				new Vector3(probePosition, startProbeHeight),
				$"{"Step".Localize()} {i + 1} {"of".Localize()} 3: {"Position".Localize()} {i + 1} - {"Auto Calibrate".Localize()}",
				autoProbePositions,
				i);

			// do the manual prob of the same position
			yield return new GetCoarseBedHeight(
				this,
				new Vector3(probePosition, startProbeHeight),
				string.Format(
					"{0} {1} {2} - {3}",
					levelingStrings.GetStepString(totalSteps),
					"Position".Localize(),
					i + 1,
					"Low Precision".Localize()),
				manualProbePositions,
				i,
				levelingStrings);

			yield return new GetFineBedHeight(
				this,
				string.Format(
					"{0} {1} {2} - {3}",
					levelingStrings.GetStepString(totalSteps),
					"Position".Localize(),
					i + 1,
					"Medium Precision".Localize()),
				manualProbePositions,
				i,
				levelingStrings);

			yield return new GetUltraFineBedHeight(
				this,
				string.Format(
					"{0} {1} {2} - {3}",
					levelingStrings.GetStepString(totalSteps),
					"Position".Localize(),
					i + 1,
					"High Precision".Localize()),
				manualProbePositions,
				i,
				levelingStrings);

			yield return new CalibrateProbeLastPagelInstructions(
				this,
				"Done".Localize(),
				"Your Probe is now calibrated.".Localize() + "\n\n\t• " + "Remove the paper".Localize() + "\n\n" + "Click 'Done' to close this window.".Localize(),
				autoProbePositions,
				manualProbePositions);
		}
	}
}