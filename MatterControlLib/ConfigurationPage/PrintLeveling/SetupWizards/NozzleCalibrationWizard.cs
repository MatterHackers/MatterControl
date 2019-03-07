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

using System.Collections.Generic;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class NozzleCalibrationWizard : PrinterSetupWizard
	{
		public NozzleCalibrationWizard(PrinterConfig printer)
			: base(printer)
		{
			this.WindowTitle = $"{ApplicationController.Instance.ProductName} - " + "Nozzle Calibration Wizard".Localize();

			pages = this.GetPages();
			pages.MoveNext();
		}

		public static bool NeedsToBeRun(PrinterConfig printer)
		{
			// we have a probe that we are using and we have not done leveling yet
			return UsingZProbe(printer) && !printer.Settings.GetValue<bool>(SettingsKey.probe_has_been_calibrated);
		}

		public override void Dispose()
		{
		}

		public static bool UsingZProbe(PrinterConfig printer)
		{
			var required = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print);

			// we have a probe that we are using and we have not done leveling yet
			return (required || printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled))
				&& printer.Settings.GetValue<bool>(SettingsKey.has_z_probe)
				&& printer.Settings.GetValue<bool>(SettingsKey.use_z_probe);
		}

		private IEnumerator<WizardPage> GetPages()
		{
			yield return new WizardPage(
				this,
				"Nozzle Offset Calibration".Localize(),
				"Offset Calibration required. We'll now print a calibration guide on the printer to tune your nozzle offsets".Localize())
			{
				WindowTitle = WindowTitle
			};

			var extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);

			var temps = new double[4];
			for (int i = 0; i < extruderCount; i++)
			{
				temps[i] = printer.Settings.Helpers.ExtruderTargetTemperature(i);
			}

			bool hasHeatedBed = printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed);
			double targetBedTemp = 0;
			if (hasHeatedBed)
			{
				targetBedTemp = printer.Settings.GetValue<double>(SettingsKey.bed_temperature);
			}

			yield return new WaitForTempPage(
				this,
				"Waiting For Printer To Heat".Localize(),
				((extruderCount == 1) ? "Waiting for the hotend to heat to ".Localize() + temps[0] + "°C.\n" : "Waiting for the hotends to heat up.".Localize())
					+ "This will ensure that no filament is stuck to your nozzle.".Localize() + "\n"
					+ "\n"
					+ "Warning! The tip of the nozzle will be HOT!".Localize() + "\n"
					+ "Avoid contact with your skin.".Localize(),
				targetBedTemp,
				temps);

			// add in the homing printer page
			yield return new HomePrinterPage(
				this,
				"Homing The Printer".Localize(),
				"Homing the printer, please wait".Localize() + "...",
				false);


			var calibrationPage = new NozzleOffsetCalibrationPrintPage(this, printer);
			yield return calibrationPage;

			yield return new NozzleOffsetCalibrationResultsPage(this, printer, calibrationPage.XOffset, calibrationPage.YOffset);
		}
	}
}