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
using System.IO;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class LastPageInstructions : PrinterSetupWizardPage
	{
		private List<ProbePosition> probePositions;

		public LastPageInstructions(PrinterSetupWizard context, string pageDescription, bool useZProbe, List<ProbePosition> probePositions)
			: base(context, pageDescription, "")
		{
			this.probePositions = probePositions;

			var calibrated = "Congratulations! Print Leveling is now configured and enabled.".Localize() + "\n"
				+ (useZProbe ? "" : "    • Remove the paper".Localize()) + "\n"
				+ "\n"
				+ "If you wish to re-calibrate leveling in the future:".Localize() + "\n"
				+ "    1. Select the 'Controls' tab on the right" + "\n"
				+ "    2. Look for the calibration section (pictured below)".Localize() + "\n";
			contentRow.AddChild(this.CreateTextField(calibrated));

			contentRow.AddChild(new ImageWidget(AggContext.StaticData.LoadImage(Path.Combine("Images", "leveling.png")))
			{
				HAnchor = HAnchor.Center
			});

			contentRow.AddChild(this.CreateTextField("Click 'Done' to close this window.".Localize()));

			this.ShowWizardFinished();
		}

		public override void PageIsBecomingActive()
		{
			PrintLevelingData levelingData = printer.Settings.Helpers.GetPrintLevelingData();
			levelingData.SampledPositions.Clear();

			for (int i = 0; i < probePositions.Count; i++)
			{
				levelingData.SampledPositions.Add(probePositions[i].position);
			}

			levelingData.LevelingSystem = printer.Settings.GetValue<LevelingSystem>(SettingsKey.print_leveling_solution);
			levelingData.CreationDate = DateTime.Now;
			// record the temp the bed was when we measured it (or 0 if no heated bed)
			levelingData.BedTemperature = printer.Settings.GetValue<bool>(SettingsKey.has_heated_bed) ?
				printer.Settings.GetValue<double>(SettingsKey.bed_temperature)
				: 0;
			levelingData.IssuedLevelingTempWarning = false;

			// Invoke setter forcing persistence of leveling data
			printer.Settings.Helpers.SetPrintLevelingData(levelingData, true);
			PrintLevelingStream.AllowLeveling = true;
			printer.Settings.Helpers.DoPrintLeveling(true);

			if (printer.Settings.GetValue<bool>(SettingsKey.z_homes_to_max))
			{
				printer.Connection.HomeAxis(PrinterConnection.Axis.XYZ);
			}

			// Make sure when the wizard is done we turn off the bed heating
			printer.Connection.TurnOffBedAndExtruders(TurnOff.AfterDelay);

			Closed += (s, e) =>
			{
				// give instruction about how to load filament if the user has not gotten them
				ApplicationController.Instance.RunAnyRequiredPrinterSetup(printer, theme);
			};


			base.PageIsBecomingActive();
		}
	}
}