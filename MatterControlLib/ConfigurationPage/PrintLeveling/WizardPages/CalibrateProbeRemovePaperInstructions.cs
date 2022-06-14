﻿/*
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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class CalibrateProbeRemovePaperInstructions : WizardPage
	{
		public CalibrateProbeRemovePaperInstructions(ISetupWizard setupWizard, string headerText, bool lastPage = true)
			: base(setupWizard, headerText, "")
		{
			contentRow.AddChild(
				this.CreateTextField(
					"Manual Z Calibration complete.".Localize() +
					"\n    • " +
					"Remove the paper".Localize()));

			contentRow.BackgroundColor = theme.MinimalShade;

			if (lastPage)
			{
				this.ShowWizardFinished();
			}
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
				printer.Connection.MoveRelative(PrinterConnection.Axis.Z, 30, printer.Settings.Helpers.ManualMovementSpeeds().Z);
			}

			base.OnLoad(args);
		}
	}

	public class ConductiveProbeCalibrateComplete : WizardPage
	{
		public ConductiveProbeCalibrateComplete(PrinterConfig printer, ISetupWizard setupWizard, string headerText)
			: base(setupWizard, headerText, "")
		{
			var completedText = "Conductive Z Calibration complete.".Localize();
			if (printer.Settings.GetBool(SettingsKey.has_swappable_bed))
			{
				completedText += "\n\n    • " + "Place the bed back on the printer".Localize();
			}

			completedText += "\n\n" + "Click 'Done'".Localize();

			contentRow.AddChild(this.CreateTextField(completedText));

			contentRow.BackgroundColor = theme.MinimalShade;

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
				printer.Connection.MoveRelative(PrinterConnection.Axis.Z, 30, printer.Settings.Helpers.ManualMovementSpeeds().Z);
			}

			base.OnLoad(args);
		}
	}

	public class CleanNozzleBeforeConductiveProbe : WizardPage
	{
		public CleanNozzleBeforeConductiveProbe(PrinterConfig printer, ISetupWizard setupWizard, string headerText)
			: base(setupWizard, headerText, "")
		{
			var completedText = "Before Z-Calibration can begin you need to:".Localize();
			if (printer.Settings.GetBool(SettingsKey.has_swappable_bed))
			{
				completedText += "\n\n    • " + "Remove the bed from the printer so the nozzle can get low enough to touch the pad".Localize();
			}

			completedText += "\n    • " + "Ensure the nozzle is clear of any debris or filament".Localize();
			completedText += "\n\n" + "Click 'Next' to continue.".Localize();

			contentRow.AddChild(this.CreateTextField(completedText));

			contentRow.BackgroundColor = theme.MinimalShade;
		}
	}
}