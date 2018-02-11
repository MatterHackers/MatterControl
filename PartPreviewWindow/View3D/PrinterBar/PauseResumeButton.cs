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
using MatterHackers.Agg.Platform;
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

		private EventHandler unregisterEvents;
		private PrinterConfig printer;

		public PrintButton(PrinterTabPage printerTabPage, PrinterConfig printer, ThemeConfig theme)
		{
			this.printer = printer;

			// add the finish setup button
			finishSetupButton = new TextButton("Setup...".Localize(), theme)
			{
				Name = "Finish Setup Button",
				ToolTipText = "Run setup configuration for printer.".Localize(),
				Margin = theme.ButtonSpacing,
				BackgroundColor = theme.ButtonFactory.Options.NormalFillColor,
				HoverColor = theme.ButtonFactory.Options.HoverFillColor,
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
			startPrintButton = new PrintPopupMenu(printer, theme);
			startPrintButton.Margin = theme.ButtonSpacing;
			this.AddChild(startPrintButton);

			printer.Connection.CommunicationStateChanged.RegisterEvent((s, e) =>
			{
				UiThread.RunOnIdle(SetButtonStates);
			}, ref unregisterEvents);

			SetButtonStates();
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		protected void SetButtonStates()
		{
			PrintLevelingData levelingData = printer.Settings.Helpers.GetPrintLevelingData();

			switch (printer.Connection.CommunicationState)
			{
				case CommunicationStates.Connected:
					if (levelingData != null && printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print)
						&& !levelingData.HasBeenRunAndEnabled())
					{
						SetChildVisible(finishSetupButton, true);
					}
					else
					{
						SetChildVisible(startPrintButton, true);
					}
					break;

				case CommunicationStates.PrintingFromSd:
				case CommunicationStates.Printing:
				case CommunicationStates.Paused:
					break;

				case CommunicationStates.FinishedPrint:
					SetChildVisible(startPrintButton, true);
					break;

				default:
					if (levelingData != null && printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print)
						&& !levelingData.HasBeenRunAndEnabled())
					{
						SetChildVisible(finishSetupButton, false);
					}
					else
					{
						SetChildVisible(startPrintButton, false);
					}
					break;
			}
		}

		private void SetChildVisible(GuiWidget visibleChild, bool enabled)
		{
			foreach (var child in Children)
			{
				if (child == visibleChild)
				{
					child.Visible = true;
					child.Enabled = enabled;
				}
				else
				{
					child.Visible = false;
				}
			}
		}
	}
}