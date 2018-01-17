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

using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using System;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class SliceButton : TextButton
	{
		private PrinterConfig printer;
		private PrinterTabPage printerTabPage;
		private EventHandler unregisterEvents;
		private bool activelySlicing { get; set; }

		public SliceButton(PrinterConfig printer, PrinterTabPage printerTabPage, ThemeConfig theme)
			: base("Slice".Localize(), theme)
		{
			this.printer = printer;
			this.printerTabPage = printerTabPage;

			this.BackgroundColor = theme.ToolbarButtonBackground;
			this.HoverColor = theme.ToolbarButtonHover;
			this.MouseDownColor = theme.ToolbarButtonDown;

			printer.Connection.CommunicationStateChanged.RegisterEvent((s, e) =>
			{
				UiThread.RunOnIdle(SetButtonStates);
			}, ref unregisterEvents);

			SetButtonStates();
		}

		public override async void OnClick(MouseEventArgs mouseEvent)
		{
			base.OnClick(mouseEvent);
			await this.SliceFileTask();
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private void SetButtonStates()
		{
			switch (printer.Connection.CommunicationState)
			{
				case CommunicationStates.PreparingToPrint:
				case CommunicationStates.PrintingFromSd:
				case CommunicationStates.Printing:
				case CommunicationStates.Paused:
					this.Enabled = false;
					break;

				default:
					this.Enabled = !activelySlicing;
					break;
			}
		}

		private async Task SliceFileTask()
		{
			if (printer.Settings.PrinterSelected)
			{
				if (!activelySlicing
					&& printer.Settings.IsValid()
					&& printer.Bed.EditContext.SourceItem != null)
				{
					activelySlicing = true;
					this.SetButtonStates();

					try
					{
						// Switch to the 3D layer view if on Model view
						if (printer.ViewState.ViewMode == PartViewMode.Model)
						{
							printer.ViewState.ViewMode = PartViewMode.Layers3D;
						}

						await ApplicationController.Instance.Tasks.Execute(printerTabPage.view3DWidget.SaveChanges);

						await ApplicationController.Instance.SliceFileLoadOutput(
							printer,
							printer.Bed.EditContext.PartFilePath,
							printer.Bed.EditContext.GCodeFilePath);
					}
					catch (Exception ex)
					{
						Console.WriteLine("Error slicing file: " + ex.Message);
					}

					activelySlicing = false;
				};

				this.SetButtonStates();
			}
			else
			{
				UiThread.RunOnIdle(() =>
				{
					StyledMessageBox.ShowMessageBox("Oops! Please select a printer in order to continue slicing.", "Select Printer", StyledMessageBox.MessageType.OK);
				});
			}
		}
	}
}