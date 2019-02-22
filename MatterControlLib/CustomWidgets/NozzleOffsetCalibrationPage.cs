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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class NozzleOffsetCalibrationPrintPage : WizardPage
	{
		private NozzleOffsetTemplatePrinter templatePrinter;
		private NozzleOffsetTemplateWidget xOffsetWidget;
		private NozzleOffsetTemplateWidget yOffsetWidget;
		private TextWidget xOffsetText;
		private TextWidget yOffsetText;

		public NozzleOffsetCalibrationPrintPage(ISetupWizard setupWizard, PrinterConfig printer)
			: base(setupWizard)
		{
			this.WindowTitle = "Nozzle Offset Calibration Wizard".Localize();
			this.HeaderText = "Nozzle Offset Calibration".Localize() + ":";
			this.Name = "Nozzle Offset Calibration Wizard";

			templatePrinter = new NozzleOffsetTemplatePrinter(printer);

			contentRow.AddChild(new TextWidget("Printing Calibration Guide".Localize(), pointSize: theme.DefaultFontSize, textColor: theme.TextColor));

			contentRow.AddChild(xOffsetWidget = new NozzleOffsetTemplateWidget(templatePrinter.ActiveOffsets, FlowDirection.LeftToRight, theme)
			{
				Padding = new BorderDouble(left: 4)
			});

			xOffsetWidget.OffsetChanged += (s, e) =>
			{
				this.XOffset = xOffsetWidget.ActiveOffset;
				xOffsetText.Text = string.Format("{0}: {1:0.###}", "X Offset".Localize(), this.XOffset);

				this.NextButton.Enabled = this.XOffset != double.MinValue && this.YOffset != double.MinValue;
			};

			var container = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			contentRow.AddChild(container);

			container.AddChild(yOffsetWidget = new NozzleOffsetTemplateWidget(templatePrinter.ActiveOffsets, FlowDirection.TopToBottom, theme)
			{
				Margin = new BorderDouble(top: 15),
				Padding = new BorderDouble(top: 4),
				Width = 300
			});

			var verticalColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			container.AddChild(verticalColumn);

			verticalColumn.AddChild(xOffsetText = new TextWidget("".Localize(), pointSize: theme.DefaultFontSize, textColor: theme.TextColor));
			verticalColumn.AddChild(yOffsetText = new TextWidget("".Localize(), pointSize: theme.DefaultFontSize, textColor: theme.TextColor));

			yOffsetWidget.OffsetChanged += (s, e) =>
			{
				this.YOffset = yOffsetWidget.ActiveOffset;
				yOffsetText.Text = string.Format("{0}: {1:0.###}", "Y Offset".Localize(), this.YOffset);

				this.NextButton.Enabled = this.XOffset != double.MinValue && this.YOffset != double.MinValue;
			};

			this.NextButton.Enabled = false;
		}

		public double YOffset { get; set; } = double.MinValue;

		public double XOffset { get; set; } = double.MinValue;

		public override void OnLoad(EventArgs args)
		{
			if (!this.HasBeenClosed)
			{
				this.NextButton.Enabled = true;
			}

			base.OnLoad(args);

			// Replace with calibration template code
			//await templatePrinter.PrintTemplate(verticalLayout: true);
			//await templatePrinter.PrintTemplate(verticalLayout: false);

			Task.Run(async () =>
			{
				string gcode1 = templatePrinter.BuildTemplate(verticalLayout: true);
				string gcode2 = templatePrinter.BuildTemplate(verticalLayout: false);

				string outputPath = Path.Combine(
					ApplicationDataStorage.Instance.GCodeOutputPath,
					$"nozzle-offset-template-combined.gcode");

				File.WriteAllText(outputPath, gcode1 + "\n" + gcode2);

				// HACK: update state needed to be set before calling StartPrint
				printer.Connection.CommunicationState = CommunicationStates.PreparingToPrint;

				await printer.Connection.StartPrint(outputPath);

				// Wait for print start
				while (!printer.Connection.PrintIsActive)
				{
					Thread.Sleep(500);
				}

				// Wait for print finished
				while (printer.Connection.PrintIsActive)
				{
					Thread.Sleep(500);
				}

				if (printer.Settings.GetValue<bool>(SettingsKey.z_homes_to_max))
				{
					printer.Connection.HomeAxis(PrinterConnection.Axis.Z);
				}
				else
				{
					printer.Connection.MoveRelative(PrinterConnection.Axis.Z, 20, printer.Settings.Helpers.ManualMovementSpeeds().Z);

					printer.Connection.MoveAbsolute(PrinterConnection.Axis.Y, 
						printer.Bed.Bounds.Top, 
						printer.Settings.Helpers.ManualMovementSpeeds().Y);
				}
			});

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
