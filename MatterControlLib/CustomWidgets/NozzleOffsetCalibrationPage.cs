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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

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

				var turtle = new GCodeTurtle();

				var rect = new RectangleDouble(0, 0, 123, 30);
				rect.Offset(50, 100);

				double nozzleWidth = 0.4;

				double y1 = rect.Bottom;
				turtle.MoveTo(rect.Left, y1);

				// Purge line
				for (var i = 0; i < 2; i++)
				{
					turtle.LineTo(rect.Left - 30, y1);
					y1 += nozzleWidth;
					turtle.LineTo(rect.Left - 30, y1);

					turtle.LineTo(rect.Left, y1);
					y1 += nozzleWidth;
					turtle.LineTo(rect.Left, y1);
				}

				// Draw box
				for (var i = 0; i < 2; i++)
				{
					rect.Inflate(-nozzleWidth * 2);
					turtle.Draw(rect);
				}

				y1 = rect.YCenter + nozzleWidth / 2;

				// Draw centerline
				turtle.MoveTo(rect.Left, y1);
				turtle.LineTo(rect.Right, y1);
				y1 += nozzleWidth;
				turtle.MoveTo(rect.Right, y1);
				turtle.LineTo(rect.Left, y1);

				var x = rect.Left + 1.5;

				double sectionHeight = rect.Height / 2;

				var step = (rect.Width - 3) / 40;
				double y2 = y1 - sectionHeight - nozzleWidth * 3;

				var up = true;

				// Draw calibration lines
				for(var i = 0; i <= 40; i++)
				{
					turtle.MoveTo(x, up ? y1 : y2);

					if ((i % 5 == 0))
					{
						turtle.LineTo(x, y2 - 5);

						var currentPos = turtle.CurrentPosition;

						turtle.Speed = 500;

						if (CalibrationLine.Glyphs.TryGetValue(i, out IVertexSource vertexSource))
						{
							var flattened = new FlattenCurves(vertexSource);

							var verticies = flattened.Vertices();
							var firstItem = verticies.First();
							var position = turtle.CurrentPosition;

							var scale = 0.32;

							if (firstItem.command != ShapePath.FlagsAndCommand.MoveTo)
							{
								turtle.MoveTo((firstItem.position * scale) + currentPos);
							}

							bool closed = false;

							foreach (var item in verticies)
							{
								switch(item.command)
								{
									case ShapePath.FlagsAndCommand.MoveTo:
										turtle.MoveTo((item.position * scale) + currentPos);
										break;

									case ShapePath.FlagsAndCommand.LineTo:
										turtle.LineTo((item.position * scale) + currentPos);
										break;

									case ShapePath.FlagsAndCommand.FlagClose:
										turtle.LineTo((firstItem.position * scale) + currentPos);
										closed = true;
										break;
								}
							}

							if (!closed)
							{
								turtle.LineTo((firstItem.position * scale) + currentPos);
							}
						}

						turtle.PenUp();
						turtle.MoveTo(x, y2);
						turtle.PenDown();
						turtle.Speed = 1800;
					}

					turtle.LineTo(x, up ? y2 : y1);

					x = x + step;

					up = !up;
				}

				string gcode = turtle.ToGCode();

				Console.WriteLine("--------------------------------------------------");
				Console.WriteLine(gcode);

				File.WriteAllText(@"c:\temp\calibration.gcode", gcode);

				printer.Connection.QueueLine(gcode);
				printer.Connection.QueueLine("G1 Z20");
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

	public class GCodeTurtle : IDisposable
	{
		private StringBuilder sb;
		private StringWriter writer;
		private double currentE = 0;

		public GCodeTurtle()
		{
			sb = new StringBuilder();
			writer = new StringWriter(sb);
			writer.WriteLine("G92 E0");
			writer.WriteLine("G1 X50 Y50 Z0.2 F{0}", this.Speed);
		}

		public Vector2 CurrentPosition { get; private set; }

		private int _speed = 1800;

		public int Speed
		{
			get => _speed;
			set
			{
				if (value != _speed)
				{
					_speed = value;
					writer.WriteLine("G1 F{0}", _speed);
				}
			}
		}

		public void MoveTo(double x, double y)
		{
			this.MoveTo(new Vector2(x, y));
		}

		public void MoveTo(Vector2 position)
		{
			writer.WriteLine("G1 X{0} Y{1}", position.X, position.Y);
			this.CurrentPosition = position;
		}

		public void PenUp()
		{
			writer.WriteLine("G1 Z0.8 E{0:0.###}", currentE - .8);
		}

		public void PenDown()
		{
			writer.WriteLine("G1 Z0.2 E{0:0.###}", currentE);
		}

		public void LineTo(double x, double y)
		{
			this.LineTo(new Vector2(x, y));
		}

		public void LineTo(Vector2 position)
		{
			var delta = this.CurrentPosition - position;
			currentE += delta.Length * 0.06;

			writer.WriteLine("G1 X{0} Y{1} E{2:0.###}", position.X, position.Y, currentE);

			this.CurrentPosition = position;
		}

		public string ToGCode()
		{
			return sb.ToString();
		}

		public void Dispose()
		{
			writer.Dispose();
		}

		internal void Draw(RectangleDouble rect)
		{
			this.MoveTo(rect.Left, rect.Bottom);

			this.LineTo(rect.Left, rect.Top);
			this.LineTo(rect.Right, rect.Top);
			this.LineTo(rect.Right, rect.Bottom);
			this.LineTo(rect.Left, rect.Bottom);
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
