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
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class XyCalibrationWizard : PrinterSetupWizard
	{
		public XyCalibrationWizard(PrinterConfig printer)
			: base(printer)
		{
			this.WindowTitle = $"{ApplicationController.Instance.ProductName} - " + "Nozzle Calibration Wizard".Localize();
			this.WindowSize = new Vector2(600 * GuiWidget.DeviceScale, 700 * GuiWidget.DeviceScale);

			pages = this.GetPages();
			pages.MoveNext();
		}

		public static bool NeedsToBeRun(PrinterConfig printer)
		{
			// we have a probe that we are using and we have not done leveling yet
			return UsingZProbe(printer) && !printer.Settings.GetValue<bool>(SettingsKey.xy_offsets_have_been_calibrated);
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
			var xyCalibrationData = new XyCalibrationData();

			yield return new XyCalibrationSelectPage(this, printer, xyCalibrationData);

			yield return new XyCalibrationStartPrintPage(this, printer, xyCalibrationData);
		}
	}

	public class XyCalibrationData
	{
		public enum QualityType { Coarse, Normal, Fine }
		public QualityType Quality { get; set; } = QualityType.Normal;
	}
}