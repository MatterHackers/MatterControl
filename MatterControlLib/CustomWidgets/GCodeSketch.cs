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
		private double currentE = 0;
		private int _speed = 1500;
		private double retractAmount = 1.2;
		private bool retracted = false;

		public GCodeSketch()
		{
			sb = new StringBuilder();
			writer = new StringWriter(sb);
		}

		public double RetractLength { get; set; } = 1.2;

		public int RetractSpeed { get; set; }

		public int TravelSpeed { get; set; }
		
		public Vector2 CurrentPosition { get; private set; }

		public Affine Transform { get; set; } = Affine.NewIdentity();

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

			writer.WriteLine("G1 X{0:0.###} Y{1:0.###}", position.X, position.Y);
			this.CurrentPosition = position;
		}

		private void Retract()
		{
			currentE -= this.RetractLength;
			retracted = true;
			writer.WriteLine("G1 E{0:0.###} F{1:0.###}", currentE, this.RetractSpeed);
		}

		private void Unretract()
		{
			// Unretract
			currentE += RetractLength;
			retracted = false;
			writer.WriteLine("G1 E{0:0.###} F{1:0.###}", currentE, this.RetractSpeed);
		}

		public void PenUp()
		{
			writer.WriteLine("G1 Z0.8 E{0:0.###}", currentE - 1.2);
			this.Retract();
		}

		public void PenDown()
		{
			writer.WriteLine("G1 Z0.2 E{0:0.###}", currentE);
			this.Unretract();
		}

		public void LineTo(double x, double y)
		{
			this.LineTo(new Vector2(x, y));
		}

		public void LineTo(Vector2 position)
		{
			bool hadRetract = retracted;
			if (retracted)
			{
				this.Unretract();
			}

			position = Transform.Transform(position);

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
			currentE = 0;
			writer.WriteLine("G92 E0");
		}
	}
}
