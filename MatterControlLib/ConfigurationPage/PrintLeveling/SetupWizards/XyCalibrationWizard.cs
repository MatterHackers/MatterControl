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

		private bool allowChildClose = true;

		private bool childRequestedClose = false;

		public XyCalibrationWizard(PrinterConfig printer, int extruderToCalibrateIndex)
			: base(printer)
		{
			this.ExtruderToCalibrateIndex = extruderToCalibrateIndex;

			this.Title = "Nozzle Calibration".Localize();
			this.WindowSize = new Vector2(600 * GuiWidget.DeviceScale, 700 * GuiWidget.DeviceScale);
		}

		public int ExtruderToCalibrateIndex { get; }

		public QualityType Quality { get; set; } = QualityType.Normal;

		/// <summary>
		/// Gets or sets the index of the X calibration item that was selected by the user.
		/// </summary>
		public int XPick { get; set; } = -1;

		public int YPick { get; set; } = -1;

		public double Offset { get; set; } = .1;

		public bool PrintAgain { get; set; }

		public override bool SetupRequired => NeedsToBeRun(printer);

		public override bool Visible => printer.Settings.GetValue<int>(SettingsKey.extruder_count) > 1;

		public override bool Enabled => printer.Settings.GetValue<int>(SettingsKey.extruder_count) > 1;

		public static bool NeedsToBeRun(PrinterConfig printer)
		{
			// we have a probe that we are using and we have not done leveling yet
			return UsingZProbe(printer)
				&& printer.Settings.Helpers.HotendCount() > 1
				&& !printer.Settings.GetValue<bool>(SettingsKey.xy_offsets_have_been_calibrated);
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

		public override bool ClosePage()
		{
			childRequestedClose = true;
			return allowChildClose;
		}

		protected override IEnumerator<WizardPage> GetPages()
		{
			yield return new WizardPage(
				this,
				"Overview".Localize(),
				string.Format(
					"{0}\n\n{1}\n\n{2}\n\n",
					"Nozzle Calibration measures the distance between hotends.".Localize(),
					"This data improves the alignment of dual extrusion prints.".Localize(),
					"Click 'Next' to continue.".Localize()))
						{
							WindowTitle = Title
						};

			yield return new XyCalibrationSelectPage(this);

			// Require user confirmation after this point
			this.RequireCancelConfirmation = true;

			// run load filament if we need to
			if (LoadFilamentWizard.NeedsToBeRun0(printer))
			{
				using (var tool0FilamentWizard = new LoadFilamentWizard(printer, extruderIndex: 0, showAlreadyLoadedButton: true))
				{
					allowChildClose = false;
					childRequestedClose = false;

					var pages = tool0FilamentWizard.GetWizardPages();

					while (!childRequestedClose
						&& pages.MoveNext())
					{
						yield return pages.Current;
					}

					allowChildClose = true;
				}
			}

			if (LoadFilamentWizard.NeedsToBeRun1(printer))
			{
				using (var tool1FilamentWizard = new LoadFilamentWizard(printer, extruderIndex: 1, showAlreadyLoadedButton: true))
				{
					allowChildClose = false;
					childRequestedClose = false;

					var pages = tool1FilamentWizard.GetWizardPages();

					while (!childRequestedClose
						&& pages.MoveNext())
					{
						yield return pages.Current;
					}

					allowChildClose = true;
				}
			}

			yield return new XyCalibrationStartPrintPage(this);

			originalEditContext = printer.Bed.EditContext;

			Task.Run(() =>
			{
				printer.Bed.SaveChanges(null, CancellationToken.None);
			});

			yield return new XyCalibrationCollectDataPage(this);
			yield return new XyCalibrationDataRecieved(this);

			// loop until we are done calibrating
			while (this.PrintAgain)
			{
				yield return new XyCalibrationStartPrintPage(this);
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