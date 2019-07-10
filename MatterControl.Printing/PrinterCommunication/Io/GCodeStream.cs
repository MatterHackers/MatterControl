/*
Copyright (c) 2019, Lars Brubaker
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
using System.Text;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterControl.Printing.Pipelines
{
	public abstract class GCodeStream : IDisposable
	{
		/// <summary>
		/// Get the next gcode line from this stream.
		/// </summary>
		/// <returns>Returns the next gcode line after processing.</returns>
		public abstract string ReadLine();

		/// <summary>
		/// Sends the printer position back up the stream pipe.
		/// </summary>
		/// <param name="position">The position as seen from down stream. Effectively what this stream will output after computation.</param>
		public abstract void SetPrinterPosition(PrinterMove position);

		public abstract void Dispose();

		private readonly bool useG0ForMovement = false;

		protected PrintHostConfig printer { get; }

		public GCodeStream(PrintHostConfig printer)
		{
			this.printer = printer;
			if (printer != null)
			{
				useG0ForMovement = printer.Settings.GetValue<bool>(SettingsKey.g0);
			}
		}

		public abstract GCodeStream InternalStream { get; }

		public abstract string DebugInfo { get; }

		public string CreateMovementLine(PrinterMove currentDestination)
		{
			return CreateMovementLine(currentDestination, PrinterMove.Unknown);
		}

		public string CreateMovementLine(PrinterMove destination, PrinterMove start)
		{
			bool moveHasExtrusion = destination.extrusion != double.PositiveInfinity && destination.extrusion != start.extrusion;

			string command = (useG0ForMovement && !moveHasExtrusion) ? "G0 " : "G1 ";

			var sb = new StringBuilder(command);

			if (destination.position.X != start.position.X)
			{
				sb.AppendFormat("X{0:0.##} ", destination.position.X);
			}

			if (destination.position.Y != double.PositiveInfinity
				&& destination.position.Y != start.position.Y)
			{
				sb.AppendFormat("Y{0:0.##} ", destination.position.Y);
			}

			if (destination.position.Z != double.PositiveInfinity
				&& destination.position.Z != start.position.Z)
			{
				sb.AppendFormat("Z{0:0.###} ", destination.position.Z);
			}

			if (moveHasExtrusion)
			{
				sb.AppendFormat("E{0:0.###} ", destination.extrusion);
			}

			if (destination.feedRate != double.PositiveInfinity
				&& destination.feedRate != start.feedRate)
			{
				if (destination.feedRate > 0)
				{
					sb.AppendFormat("F{0:0.##}", destination.feedRate);
				}
			}

			return sb.ToString().Trim();
		}

		public static PrinterMove GetPosition(string lineBeingSent, PrinterMove startPositionPosition)
		{
			if (lineBeingSent.StartsWith("G28")
				|| lineBeingSent.StartsWith("G29")
				|| lineBeingSent.StartsWith("G30"))
			{
				return PrinterMove.Unknown;
			}

			PrinterMove currentDestination = startPositionPosition;
			GCodeFile.GetFirstNumberAfter("X", lineBeingSent, ref currentDestination.position.X);
			GCodeFile.GetFirstNumberAfter("Y", lineBeingSent, ref currentDestination.position.Y);
			GCodeFile.GetFirstNumberAfter("Z", lineBeingSent, ref currentDestination.position.Z);
			GCodeFile.GetFirstNumberAfter("E", lineBeingSent, ref currentDestination.extrusion);
			GCodeFile.GetFirstNumberAfter("F", lineBeingSent, ref currentDestination.feedRate);

			return currentDestination;
		}

		public static bool LineIsMovement(string lineBeingSent)
		{
			if (lineBeingSent.StartsWith("G0 ")
				|| lineBeingSent.StartsWith("G1 ")
				|| lineBeingSent.StartsWith("G28")
				|| lineBeingSent.StartsWith("G29")
				|| lineBeingSent.StartsWith("G30"))
			{
				return true;
			}

			return false;
		}
	}
}