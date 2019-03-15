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
using System.Text;
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	/// <summary>
	/// Build GCode instructions from simple commands like MoveTo/LineTo/DrawRectangle
	/// </summary>
	public class GCodeSketch : IDisposable
	{
		private StringBuilder sb;
		private StringWriter writer;
		private PrinterConfig printer;
		private double nozzleDiameter;
		private double filamentDiameterMm;
		private double printerExtrusionMultiplier;
		private double currentE = 0;
		private bool retracted = false;
		private bool penUp = false;
		private double currentSpeed = 0;
		private double layerHeight = 0.2;

		public GCodeSketch(PrinterConfig printer)
		{
			sb = new StringBuilder();
			writer = new StringWriter(sb);

			this.printer = printer;
			nozzleDiameter = printer.Settings.GetValue<double>(SettingsKey.nozzle_diameter);
			filamentDiameterMm = printer.Settings.GetValue<double>(SettingsKey.filament_diameter);
			printerExtrusionMultiplier = printer.Settings.GetValue<double>(SettingsKey.extrusion_multiplier);
		}

		public double RetractLength { get; set; } = 1.2;

		public double RetractSpeed { get; set; }

		public double TravelSpeed { get; set; }
		
		public Vector2 CurrentPosition { get; private set; }

		public Affine Transform { get; set; } = Affine.NewIdentity();

		public double Speed { get; set; } = 1500;

		public double RetractLift { get; internal set; }

		public void SetTool(string toolChange)
		{
			this.WriteRaw(toolChange);
			this.ResetE();
		}

		public void MoveTo(double x, double y, bool retract = false)
		{
			this.MoveTo(new Vector2(x, y), retract);
		}

		public void MoveTo(Vector2 position, bool retract = false)
		{
			if (retract)
			{
				this.Retract();
			}

			position = Transform.Transform(position);

			this.WriteSpeedLine(
				string.Format(
					"G1 X{0:0.###} Y{1:0.###}",
					position.X,
					position.Y),
				this.TravelSpeed);

			this.CurrentPosition = position;
		}

		private void Retract()
		{
			if (currentE > 0)
			{
				this.WriteRaw("; Retract");
				currentE -= this.RetractLength;
				retracted = true;

				this.WriteSpeedLine(
					string.Format("G1 E{0:0.###}", currentE),
					this.RetractSpeed);
			}
		}

		public void Unretract()
		{
			if (retracted)
			{
				// Unretract
				this.WriteRaw("; Unretract");
				currentE += RetractLength;
				retracted = false;

				this.WriteSpeedLine(
					string.Format("G1 E{0:0.###}", currentE),
					this.RetractSpeed);
			}
		}

		public void PenUp()
		{
			if (!penUp)
			{
				penUp = true;

				this.WriteRaw("; PenUp");
				this.Retract();
				this.WriteSpeedLine(
					string.Format("G1 Z{0:0.###}", layerHeight + this.RetractLift),
					this.TravelSpeed);
			}
		}

		public void PenDown()
		{
			if (penUp)
			{
				penUp = false;

				this.WriteRaw("; PenDown");
				this.WriteSpeedLine(
					string.Format("G1 Z{0:0.###}", layerHeight),
					this.TravelSpeed);

				this.Unretract();
			}
		}

		public static double ExtrudeAmount(PrinterConfig printer, double widthMm, double heightMm, double lengthMm)
		{
			var filamentDiameterMm = printer.Settings.GetValue<double>(SettingsKey.filament_diameter);

			var volumeMm3 = widthMm * heightMm * lengthMm;
			var areaMm2 = Math.PI * Math.Pow(filamentDiameterMm / 2, 2);
			var filamentLengthMm = volumeMm3 / areaMm2;

			return filamentLengthMm;
		}

		public double ExtrudeAmount(double widthMm, double heightMm, double lengthMm)
		{
			var volumeMm3 = widthMm * heightMm * lengthMm;
			var areaMm2 = Math.PI * Math.Pow(filamentDiameterMm / 2, 2);
			var filamentLengthMm = volumeMm3 / areaMm2;

			return filamentLengthMm;
		}

		public void LineTo(double x, double y)
		{
			this.LineTo(new Vector2(x, y), printerExtrusionMultiplier);
		}

		public void LineTo(double x, double y, double extrusionMultiplier)
		{
			this.LineTo(new Vector2(x, y), extrusionMultiplier);
		}

		public void LineTo(Vector2 position)
		{
			this.LineTo(position, printerExtrusionMultiplier);
		}

		public void LineTo(Vector2 position, double extrusionMultiplier)
		{
			if (retracted)
			{
				this.Unretract();
			}

			position = Transform.Transform(position);

			var delta = this.CurrentPosition - position;
			currentE += this.ExtrudeAmount(nozzleDiameter, layerHeight, delta.Length) * extrusionMultiplier;

			this.WriteSpeedLine(
				string.Format(
					"G1 X{0} Y{1} E{2:0.###}", 
					position.X, 
					position.Y, 
					currentE),
				this.Speed);

			this.CurrentPosition = position;
		}

		/// <summary>
		/// Write the given line, optionally pushing speeds if needed
		/// </summary>
		/// <param name="line">The line to write</param>
		/// <param name="targetSpeed">The target movement speed</param>
		public void WriteSpeedLine(string line, double targetSpeed)
		{
			if (currentSpeed == targetSpeed)
			{
				writer.WriteLine(line);
			}
			else
			{
				currentSpeed = targetSpeed;
				writer.WriteLine("{0} F{1:0.###}", line, targetSpeed);
			}
		}

		public string ToGCode()
		{
			return sb.ToString();
		}

		public void Dispose()
		{
			writer.Dispose();
		}

		public void DrawRectangle(RectangleDouble rect)
		{
			this.MoveTo(rect.Left, rect.Bottom);

			this.LineTo(rect.Left, rect.Top);
			this.LineTo(rect.Right, rect.Top);
			this.LineTo(rect.Right, rect.Bottom);
			this.LineTo(rect.Left, rect.Bottom);
		}

		public void WriteRaw(string gcode)
		{
			writer.WriteLine(gcode);
		}

		internal void ResetE()
		{
			this.WriteRaw("; Reset E");
			currentE = 0;
			writer.WriteLine("G92 E0");
		}
	}
}
