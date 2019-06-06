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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class XyCalibrationWizard : PrinterSetupWizard
	{
		private EditContext originalEditContext;

		public XyCalibrationWizard(PrinterConfig printer, int extruderToCalibrateIndex)
			: base(printer)
		{
			if (printer.Settings.GetValue<bool>(SettingsKey.xy_offsets_have_been_calibrated))
			{
				this.Quality = QualityType.Normal;
			}
			else
			{
				this.Quality = QualityType.Coarse;
			}

			this.ExtruderToCalibrateIndex = extruderToCalibrateIndex;

			this.Title = "Nozzle Calibration".Localize();
			this.WindowSize = new Vector2(600 * GuiWidget.DeviceScale, 700 * GuiWidget.DeviceScale);
		}

		public int ExtruderToCalibrateIndex { get; }

		public override bool Completed => printer.Settings.GetValue<bool>(SettingsKey.xy_offsets_have_been_calibrated);

		public override string HelpText
		{
			get
			{
				if (!printer.Settings.GetValue<bool>(SettingsKey.filament_has_been_loaded)
					&& !printer.Settings.GetValue<bool>(SettingsKey.filament_1_has_been_loaded))
				{
					return "Load Filament to continue".Localize();
				}

				return null;
			}
		}

		public QualityType Quality { get; set; }

		/// <summary>
		/// Gets or sets the index of the X calibration item that was selected by the user.
		/// </summary>
		public int XPick { get; set; } = -1;

		public int YPick { get; set; } = -1;

		public double Offset { get; set; } = .1;

		public bool PrintAgain { get; set; } = true;

		public override bool SetupRequired => NeedsToBeRun(printer);

		public override bool Visible => printer.Settings.GetValue<int>(SettingsKey.extruder_count) > 1;

		public override bool Enabled
		{
			// Wizard should be disabled until requirements are met
			get => printer.Settings.GetValue<int>(SettingsKey.extruder_count) > 1
					&& !LoadFilamentWizard.NeedsToBeRun0(printer)
					&& printer.Settings.GetValue<bool>(SettingsKey.filament_1_has_been_loaded);
		}

		public static bool NeedsToBeRun(PrinterConfig printer)
		{
			// we have a probe that we are using and we have not done leveling yet
			// and there is something on the bed that uses 2 extruders
			return UsingZProbe(printer)
				&& printer.Settings.Helpers.HotendCount() > 1
				&& !printer.Settings.GetValue<bool>(SettingsKey.xy_offsets_have_been_calibrated)
				&& Slicer.T1OrGreaterUsed(printer);
		}

		public static bool UsingZProbe(PrinterConfig printer)
		{
			var required = printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print);

			// we have a probe that we are using and we have not done leveling yet
			return (required || printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled))
				&& printer.Settings.GetValue<bool>(SettingsKey.has_z_probe)
				&& printer.Settings.GetValue<bool>(SettingsKey.use_z_probe);
		}

		public async override void Dispose()
		{
			if (originalEditContext != null
				&& printer.Bed.EditContext != originalEditContext)
			{
				await printer.Bed.LoadContent(originalEditContext);
			}

			base.Dispose();
		}

		protected override IEnumerator<WizardPage> GetPages()
		{
			yield return new WizardPage(
				this,
				string.Format("{0} {1}", this.Title, "Overview".Localize()),
				string.Format(
					"{0}\n\n{1}\n\n{2}\n\n",
					"Nozzle Calibration measures the distance between hotends.".Localize(),
					"This data improves the alignment of dual extrusion prints.".Localize(),
					"Click 'Next' to continue.".Localize()))
				{
					WindowTitle = Title,
				};

			originalEditContext = printer.Bed.EditContext;
			Task.Run(() =>
			{
				printer.Bed.SaveChanges(null, CancellationToken.None);
			});

			// loop until we are done calibrating
			while (this.PrintAgain)
			{
				yield return new XyCalibrationSelectPage(this);

				// Require user confirmation after this point
				this.RequireCancelConfirmation = true;

				yield return new XyCalibrationCollectDataPage(this);
				yield return new XyCalibrationDataRecieved(this);
			}
		}

		public enum QualityType
		{
			Coarse,
			Normal,
			Fine
		}
	}
}