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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class CalibrateProbeLastPageInstructions : WizardPage
	{
		public CalibrateProbeLastPageInstructions(ISetupWizard setupWizard, string headerText)
			: base(setupWizard, headerText, "")
		{
			var calibrated = "Your Probe is now calibrated.".Localize() + "\n"
				+ "    • " + "Remove the paper".Localize() + "\n"
				+ "\n"
				+ "If you wish to re-calibrate your probe in the future:".Localize() + "\n"
				+ "    1. Select the 'Controls' tab on the right".Localize() + "\n"
				+ "    2. Look for the calibration section (pictured below)".Localize() + "\n";
			contentRow.AddChild(this.CreateTextField(calibrated));
			contentRow.BackgroundColor = theme.MinimalShade;

			// Create and display a widget for reference
			var exampleWidget = CalibrationControls.CreateSection(printer, theme);
			exampleWidget.Width = 500;
			exampleWidget.Selectable = false;
			exampleWidget.HAnchor = HAnchor.Center | HAnchor.Absolute;

			// Disable borders on all SettingsRow children in control panels
			foreach (var settingsRow in exampleWidget.ContentPanel.Descendants<SettingsRow>())
			{
				settingsRow.BorderColor = Color.Transparent;
			}

			theme.ApplyBoxStyle(exampleWidget);

			contentRow.AddChild(exampleWidget);

			contentRow.AddChild(this.CreateTextField("Click 'Done' to close this window.".Localize()));

			this.ShowWizardFinished();
		}

		public override void OnLoad(EventArgs args)
		{
			printer.Connection.QueueLine("T0");
			printer.Connection.MoveRelative(PrinterConnection.Axis.X, .1, printer.Settings.Helpers.ManualMovementSpeeds().X);

			if (printer.Settings.GetValue<bool>(SettingsKey.z_homes_to_max))
			{
				printer.Connection.HomeAxis(PrinterConnection.Axis.XYZ);
			}
			else if (!printer.Settings.GetValue<bool>(SettingsKey.has_z_probe))
			{
				// Lift the hotend off the bed - at the conclusion of the wizard, make sure we lift the heated nozzle off the bed
				printer.Connection.MoveRelative(PrinterConnection.Axis.Z, 2, printer.Settings.Helpers.ManualMovementSpeeds().Z);
			}

			base.OnLoad(args);
		}
	}
}