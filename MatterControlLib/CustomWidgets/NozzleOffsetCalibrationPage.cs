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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
	public class NozzleOffsetCalibrationPrintPage : WizardPage
	{
		private NozzleOffsetTemplatePrinter templatePrinter;
		private NozzleOffsetTemplateWidget xOffsetWidget;
		private NozzleOffsetTemplateWidget yOffsetWidget;

		public NozzleOffsetCalibrationPrintPage(ISetupWizard setupWizard, PrinterConfig printer)
			: base(setupWizard)
		{
			this.WindowTitle = "Nozzle Offset Calibration Wizard".Localize();
			this.HeaderText = "Nozzle Offset Calibration".Localize() + ":";
			this.Name = "Nozzle Offset Calibration Wizard";

			templatePrinter = new NozzleOffsetTemplatePrinter(printer);

			contentRow.AddChild(new TextWidget("Printing Calibration Guide".Localize(), pointSize: theme.DefaultFontSize, textColor: theme.TextColor));

			contentRow.AddChild(new TextWidget("Printing Guide...".Localize(), pointSize: theme.DefaultFontSize, textColor: theme.TextColor));

			contentRow.AddChild(xOffsetWidget = new NozzleOffsetTemplateWidget(templatePrinter.ActiveOffsets, FlowDirection.LeftToRight, theme));

			contentRow.AddChild(yOffsetWidget = new NozzleOffsetTemplateWidget(templatePrinter.ActiveOffsets, FlowDirection.TopToBottom, theme)
			{
				Margin = new BorderDouble(top: 15)
			});

			this.NextButton.Enabled = false;
		}

		public double[] ActiveOffsets => templatePrinter.ActiveOffsets;

		public async override void OnLoad(EventArgs args)
		{
			// Replace with calibration template code
			await templatePrinter.PrintTemplate(verticalLayout: true);
			await templatePrinter.PrintTemplate(verticalLayout: false);

			if (!this.HasBeenClosed)
			{
				this.NextButton.Enabled = true;
			}

			base.OnLoad(args);
		}

		public override void OnClosed(EventArgs e)
		{
			if (printer.Connection.CommunicationState == PrinterCommunication.CommunicationStates.Printing ||
				printer.Connection.CommunicationState == PrinterCommunication.CommunicationStates.Paused)
			{
				printer.CancelPrint();
			}

			base.OnClosed(e);
		}
	}
}
