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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PrintPauseResumeButton : FlowLayoutWidget
	{
		private GuiWidget finishSetupButton;
		private GuiWidget pausePrintButton;
		private PrinterConfig printer;
		private GuiWidget resumePrintButton;
		private GuiWidget startPrintButton;
		private EventHandler unregisterEvents;

		public PrintPauseResumeButton(PrinterActionsBar printerActionsBar, PrinterTabPage printerTabPage, PrinterConfig printer, ThemeConfig theme)
		{
			var defaultMargin = theme.ButtonSpacing;

			this.printer = printer;

			// add the finish setup button
			finishSetupButton = theme.ButtonFactory.Generate("Setup...".Localize(), AggContext.StaticData.LoadIcon("icon_play_32x32.png", 14, 14, IconColor.Theme));
			finishSetupButton.Name = "Finish Setup Button";
			finishSetupButton.ToolTipText = "Run setup configuration for printer.".Localize();
			finishSetupButton.Margin = defaultMargin;
			finishSetupButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(async () =>
				{
					var context = printer.Bed.EditContext;
					await ApplicationController.Instance.PrintPart(
						context.PartFilePath,
						context.GCodeFilePath,
						context.SourceItem.Name,
						printer,
						null);
				});
			};
			this.AddChild(finishSetupButton);

			// add the start print button
			startPrintButton = theme.ButtonFactory.Generate("Print".Localize().ToUpper(), AggContext.StaticData.LoadIcon("icon_play_32x32.png", 14, 14, IconColor.Theme));
			startPrintButton.Name = "Start Print Button";
			startPrintButton.ToolTipText = "Begin printing the selected item.".Localize();
			startPrintButton.Margin = defaultMargin;
			startPrintButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(async () =>
				{
					// Save any pending changes before starting print operation
					await printerTabPage.view3DWidget.PersistPlateIfNeeded();

					var context = printer.Bed.EditContext;
					await ApplicationController.Instance.PrintPart(
						context.PartFilePath,
						context.GCodeFilePath,
						context.SourceItem.Name,
						printer,
						null);
				});
			};
			this.AddChild(startPrintButton);

			// add the pause / resume button
			pausePrintButton = theme.ButtonFactory.Generate("Pause".Localize().ToUpper(), AggContext.StaticData.LoadIcon("icon_pause_32x32.png", 14, 14, IconColor.Theme));
			pausePrintButton.ToolTipText = "Pause the current print".Localize();
			pausePrintButton.Margin = defaultMargin;
			pausePrintButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(printer.Connection.RequestPause);
				pausePrintButton.Enabled = false;
			};
			this.AddChild(pausePrintButton);

			resumePrintButton = theme.ButtonFactory.Generate("Resume".Localize().ToUpper(), AggContext.StaticData.LoadIcon("icon_play_32x32.png", 14, 14, IconColor.Theme));
			resumePrintButton.ToolTipText = "Resume the current print".Localize();
			resumePrintButton.Margin = defaultMargin;
			resumePrintButton.Name = "Resume Button";
			resumePrintButton.Click += (s, e) =>
			{
				if (printer.Connection.PrinterIsPaused)
				{
					printer.Connection.Resume();
				}
				pausePrintButton.Enabled = true;
			};
			this.AddChild(resumePrintButton);

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
			switch (printer.Connection.CommunicationState)
			{
				case CommunicationStates.Connected:
					PrintLevelingData levelingData = printer.Settings.Helpers.GetPrintLevelingData();
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
					SetChildVisible(pausePrintButton, true);
					break;

				case CommunicationStates.Paused:
					SetChildVisible(resumePrintButton, true);
					break;

				case CommunicationStates.FinishedPrint:
					SetChildVisible(startPrintButton, true);
					break;

				default:
					SetChildVisible(startPrintButton, false);
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