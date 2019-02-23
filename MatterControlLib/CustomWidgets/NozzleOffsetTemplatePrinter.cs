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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class NozzleOffsetTemplatePrinter
	{
		private PrinterConfig printer;
		private double[] activeOffsets;
		private double nozzleWidth;
		private int firstLayerSpeed;

		public NozzleOffsetTemplatePrinter(PrinterConfig printer)
		{
			this.printer = printer;

			// Build offsets
			activeOffsets = new double[41];
			activeOffsets[20] = 0;

			var leftStep = 1.5d / 20;
			var rightStep = 1.5d / 20;

			for (var i = 1; i <= 20; i++)
			{
				activeOffsets[20 - i] = i * leftStep * -1;
				activeOffsets[20 + i] = i * rightStep;
			}

			nozzleWidth = printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter);
			firstLayerSpeed = (int)(printer.Settings.GetValue<double>(SettingsKey.first_layer_speed) * 60);
		}

		public double[] ActiveOffsets => activeOffsets;

		public bool DebugMode { get; private set; } = false;

		public Task PrintTemplate(bool verticalLayout)
		{
			return Task.Run(async ()=>
			{
				string gcode = this.BuildTemplate(verticalLayout);

				string outputPath = Path.Combine(
					ApplicationDataStorage.Instance.GCodeOutputPath,
					$"nozzle-offset-template{ (verticalLayout ? 1 : 2) }.gcode");

				File.WriteAllText(outputPath, gcode);

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
			});
		}

		public void BuildTemplate(GCodeSketch gcodeSketch, bool verticalLayout)
		{
			//gcodeSketch.WriteRaw("G92 E0");
			gcodeSketch.WriteRaw("T0");
			gcodeSketch.WriteRaw($"G1 Z0.2 F{firstLayerSpeed}");

			if (verticalLayout)
			{
				gcodeSketch.Transform = Affine.NewRotation(MathHelper.DegreesToRadians(90)) * Affine.NewTranslation(105, 45);
			}
			else
			{
				gcodeSketch.Transform = Affine.NewTranslation(75, 175);
			}

			var rect = new RectangleDouble(0, 0, 123, 30);

			var originalRect = rect;

			int towerSize = 10;

			gcodeSketch.Speed = firstLayerSpeed;

			double y1 = rect.Bottom;
			gcodeSketch.MoveTo(rect.Left, y1);

			var towerRect = new RectangleDouble(0, 0, towerSize, towerSize);
			towerRect.Offset(originalRect.Left - towerSize, originalRect.Bottom);

			// Prime
			this.PrimeHotend(gcodeSketch, towerRect);

			// Perimeters
			rect = this.CreatePerimeters(gcodeSketch, rect);

			double x, y2, y3;
			double sectionHeight = rect.Height / 2;
			bool up = true;
			var step = (rect.Width - 3) / 40;

			if (!this.DebugMode)
			{
				y1 = rect.YCenter + (nozzleWidth / 2);

				// Draw centerline
				gcodeSketch.MoveTo(rect.Left, y1);
				gcodeSketch.LineTo(rect.Right, y1);
				y1 += nozzleWidth;
				gcodeSketch.MoveTo(rect.Right, y1);
				gcodeSketch.LineTo(rect.Left, y1);

				y1 -= nozzleWidth / 2;

				x = rect.Left + 1.5;

				y2 = y1 - sectionHeight - (nozzleWidth * 1.5);
				y3 = y2 - 5;

				bool drawGlpyphs = false;

				// Draw calibration lines
				for (var i = 0; i <= 40; i++)
				{
					gcodeSketch.MoveTo(x, up ? y1 : y2);

					if ((i % 5 == 0))
					{
						gcodeSketch.LineTo(x, y3);

						var currentPos = gcodeSketch.CurrentPosition;

						gcodeSketch.Speed = 500;

						PrintLineEnd(gcodeSketch, drawGlpyphs, i, currentPos);

						gcodeSketch.Speed = 1800;

						gcodeSketch.MoveTo(x, y3);
						gcodeSketch.MoveTo(x, y2);
					}

					gcodeSketch.LineTo(x, up ? y2 : y1);

					x = x + step;

					up = !up;
				}
			}

			x = rect.Left + 1.5;
			y1 = rect.Top + (nozzleWidth * .5);
			y2 = y1 - sectionHeight + (nozzleWidth * .5);

			gcodeSketch.WriteRaw("T1");
			gcodeSketch.ResetE();

			gcodeSketch.MoveTo(rect.Left, rect.Top);

			gcodeSketch.PenDown();

			towerRect = new RectangleDouble(0, 0, towerSize, towerSize);
			towerRect.Offset(originalRect.Left - towerSize, originalRect.Top - towerSize);

			// Prime
			this.PrimeHotend(gcodeSketch, towerRect);

			if (this.DebugMode)
			{
				// Perimeters
				rect = this.CreatePerimeters(gcodeSketch, rect);
			}
			else
			{
				up = true;

				// Draw calibration lines
				for (var i = 0; i <= 40; i++)
				{
					gcodeSketch.MoveTo(x + activeOffsets[i], up ? y1 : y2, retract: true);
					gcodeSketch.LineTo(x + activeOffsets[i], up ? y2 : y1);

					x = x + step;

					up = !up;
				}
			}

			gcodeSketch.PenUp();
		}

		private RectangleDouble CreatePerimeters(GCodeSketch gcodeSketch, RectangleDouble rect)
		{
			gcodeSketch.WriteRaw("; CreatePerimeters");
			for (var i = 0; i < 3; i++)
			{
				rect.Inflate(-nozzleWidth);
				gcodeSketch.DrawRectangle(rect);
			}

			return rect;
		}

		private void PrimeHotend(GCodeSketch gcodeSketch, RectangleDouble towerRect)
		{
			gcodeSketch.WriteRaw("; Priming");

			while (towerRect.Width > 4)
			{
				towerRect.Inflate(-nozzleWidth);
				gcodeSketch.DrawRectangle(towerRect);
			}
		}

		private static void PrintLineEnd(GCodeSketch turtle, bool drawGlpyphs, int i, Vector2 currentPos)
		{
			if (drawGlpyphs && CalibrationLine.Glyphs.TryGetValue(i, out IVertexSource vertexSource))
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
					switch (item.command)
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
		}
	}
}
