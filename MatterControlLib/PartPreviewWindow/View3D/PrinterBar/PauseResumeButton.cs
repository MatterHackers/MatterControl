/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PrintButton : FlowLayoutWidget
	{
		private GuiWidget finishSetupButton;
		private GuiWidget startPrintButton;

		private PrinterConfig printer;
		private ThemeConfig theme;

		public PrintButton(PrinterConfig printer, ThemeConfig theme)
		{
			this.printer = printer;
			this.theme = theme;

			// add the finish setup button
			finishSetupButton = new TextButton("Setup...".Localize(), theme)
			{
				Name = "Finish Setup Button",
				ToolTipText = "Run setup configuration for printer.".Localize(),
				Margin = theme.ButtonSpacing,
			};
			finishSetupButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(async () =>
				{
					await ApplicationController.Instance.PrintPart(
						printer.Bed.EditContext,
						printer,
						null,
						CancellationToken.None);
				});
			};
			this.AddChild(finishSetupButton);

			// add the start print button
			this.AddChild(startPrintButton = new PrintPopupMenu(printer, theme)
			{
				Margin = theme.ButtonSpacing
			});

			// Register listeners
			printer.Connection.CommunicationStateChanged += Connection_CommunicationStateChanged;
			printer.Settings.SettingChanged += Printer_SettingChanged;

			SetButtonStates();
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Connection.CommunicationStateChanged -= Connection_CommunicationStateChanged;
			printer.Settings.SettingChanged -= Printer_SettingChanged;

			base.OnClosed(e);
		}

		protected void SetButtonStates()
		{
			// If we don't have leveling data and we need it
			bool showSetupButton = LevelingValidation.NeedsToBeRun(printer)
				|| ProbeCalibrationWizard.NeedsToBeRun(printer)
				|| LoadFilamentWizard.NeedsToBeRun(printer);

			switch (printer.Connection.CommunicationState)
			{
				case CommunicationStates.FinishedPrint:
				case CommunicationStates.Connected:
					if(showSetupButton)
					{
						startPrintButton.Visible = false;
						finishSetupButton.Visible = true;
						finishSetupButton.Enabled = true;
						theme.ApplyPrimaryActionStyle(finishSetupButton);
					}
					else
					{
						startPrintButton.Visible = true;
						startPrintButton.Enabled = true;
						finishSetupButton.Visible = false;
						theme.ApplyPrimaryActionStyle(startPrintButton);
					}
					break;

				case CommunicationStates.PrintingFromSd:
				case CommunicationStates.Printing:
				case CommunicationStates.Paused:
				default:
					if (showSetupButton)
					{
						startPrintButton.Visible = false;
						finishSetupButton.Visible = true;
						finishSetupButton.Enabled = false;
						theme.RemovePrimaryActionStyle(finishSetupButton);
					}
					else
					{
						startPrintButton.Visible = true;
						startPrintButton.Enabled = false;
						finishSetupButton.Visible = false;
						theme.RemovePrimaryActionStyle(startPrintButton);
					}
					break;
			}
		}

		private void Connection_CommunicationStateChanged(object s, EventArgs e)
		{
			UiThread.RunOnIdle(SetButtonStates);
		}

		private void Printer_SettingChanged(object s, EventArgs e)
		{
			if (e is StringEventArgs stringEvent
				&& (stringEvent.Data == SettingsKey.z_probe_z_offset
					|| stringEvent.Data == SettingsKey.print_leveling_data
					|| stringEvent.Data == SettingsKey.print_leveling_solution
					|| stringEvent.Data == SettingsKey.bed_temperature
					|| stringEvent.Data == SettingsKey.print_leveling_enabled
					|| stringEvent.Data == SettingsKey.print_leveling_required_to_print
					|| stringEvent.Data == SettingsKey.filament_has_been_loaded))
			{
				SetButtonStates();
			}
		}
	}
}