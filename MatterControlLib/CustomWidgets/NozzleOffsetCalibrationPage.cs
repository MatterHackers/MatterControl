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
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl
{
	public class NozzleOffsetCalibrationIntroPage : DialogPage
	{
		public NozzleOffsetCalibrationIntroPage(PrinterConfig printer)
		{
			this.WindowTitle = "Nozzle Offset Calibration Wizard".Localize();
			this.HeaderText = "Nozzle Offset Calibration".Localize() + ":";
			this.Name = "Nozzle Offset Calibration Wizard";

			this.ContentRow.AddChild(
				new WrappedTextWidget(
					"Offset Calibration required. We'll now print a calibration guide on the printer to tune your nozzel offsets".Localize(), 
					textColor: theme.TextColor, 
					pointSize: theme.DefaultFontSize));

			var startCalibrationButton = theme.CreateDialogButton("Next".Localize());
			startCalibrationButton.Name = "Begin calibration print";
			startCalibrationButton.Click += (s, e) =>
			{
				this.DialogWindow.ChangeToPage(new NozzleOffsetCalibrationPrintPage(printer));
			};
			theme.ApplyPrimaryActionStyle(startCalibrationButton);

			this.AddPageAction(startCalibrationButton);
		}
	}

	public class NozzleOffsetCalibrationPrintPage: DialogPage
	{
		private PrinterConfig printer;
		private TextButton nextButton;

		public NozzleOffsetCalibrationPrintPage(PrinterConfig printer)
		{
			this.WindowTitle = "Nozzle Offset Calibration Wizard".Localize();
			this.HeaderText = "Nozzle Offset Calibration".Localize() + ":";
			this.Name = "Nozzle Offset Calibration Wizard";
			this.printer = printer;

			this.ContentRow.AddChild(new TextWidget("Printing Calibration Guide".Localize()));

			this.ContentRow.AddChild(new TextWidget("Heating printer...".Localize()));

			this.ContentRow.AddChild(new TextWidget("Printing Guide...".Localize()));

			nextButton = theme.CreateDialogButton("Next".Localize());
			nextButton.Name = "Configure Calibration";
			nextButton.Enabled = false;
			nextButton.Click += (s, e) =>
			{
				this.DialogWindow.ChangeToPage(new NozzleOffsetCalibrationResultsPage(printer));
			};
			theme.ApplyPrimaryActionStyle(nextButton);

			this.AddPageAction(nextButton);

		}

		public override void OnLoad(EventArgs args)
		{
			// Replace with calibration template code
			Task.Run(() =>
			{
				//printer.Connection.QueueLine("T0\nG1 Y180 X150 Z0.8 F1800\nG92 E0\nG1 X100 Z0.3 E25 F900");
			});

			// TODO: At conclusion of calibration template print, enable next button
			Task.Run(() =>
			{
				// TODO: Silly hack to replicate expected behavior
				Thread.Sleep(10 * 1000);
				nextButton.Enabled = true;
			});

			base.OnLoad(args);
		}
	}

	public class NozzleOffsetCalibrationResultsPage : DialogPage
	{
		public NozzleOffsetCalibrationResultsPage(PrinterConfig printer)
		{
			this.WindowTitle = "Nozzle Offset Calibration Wizard".Localize();
			this.HeaderText = "Nozzle Offset Calibration".Localize() + ":";
			this.Name = "Nozzle Offset Calibration Wizard";

			var commonMargin = new BorderDouble(4, 2);

			var row = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Absolute,
				Padding = new BorderDouble(6, 0),
				Height = 125
			};
			contentRow.AddChild(row);

			for(var i = 0; i <= 40; i++)
			{
				row.AddChild(new CalibrationLine(theme)
				{
					Width =  8,
					Margin = 1,
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Stretch,
					GlyphIndex = (i % 5 == 0) ? i : -1,
					IsNegative = i < 20
				});

				// Add spacers to stretch to size
				if (i < 40)
				{
					row.AddChild(new HorizontalSpacer());
				}
			}

			row.AfterDraw += (s, e) =>
			{
				int strokeWidth = 3;

				var rect = new RectangleDouble(0, 20, row.LocalBounds.Width, row.LocalBounds.Height);
				rect.Inflate(-2);

				var center = rect.Center;

				e.Graphics2D.Rectangle(rect, theme.TextColor, strokeWidth);
				e.Graphics2D.Line(rect.Left, center.Y, rect.Right, center.Y, theme.TextColor, strokeWidth);
			};
		}
	}
}
