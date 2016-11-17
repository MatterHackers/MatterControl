/*
Copyright (c) 2015, Lars Brubaker
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
using MatterHackers.Agg;
using MatterHackers.GCodeVisualizer;
using MatterHackers.VectorMath;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public abstract class GCodeStream : IDisposable
	{
		#region Abstract Functions
		/// <summary>
		/// returns null when there are no more lines
		/// </summary>
		/// <returns></returns>
		public abstract string ReadLine();
		public abstract void SetPrinterPosition(PrinterMove position);
		#endregion

		public abstract void Dispose();

		bool useG0ForMovement = false;

		public GCodeStream()
		{
			useG0ForMovement = ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.g0);
		}

		public string CreateMovementLine(PrinterMove currentDestination)
		{
			return CreateMovementLine(currentDestination, PrinterMove.Nowhere);
		}

		public string CreateMovementLine(PrinterMove destination, PrinterMove start)
		{
			string lineBeingSent;
			StringBuilder newLine = new StringBuilder("G1 ");

			bool moveHasExtrusion = destination.extrusion != start.extrusion;
			if (useG0ForMovement && !moveHasExtrusion)
			{
				newLine = new StringBuilder("G0 ");
			}

			if (destination.position.x != start.position.x)
			{
				newLine = newLine.Append(String.Format("X{0:0.##} ", destination.position.x));
			}
			if (destination.position.y != start.position.y)
			{
				newLine = newLine.Append(String.Format("Y{0:0.##} ", destination.position.y));
			}
			if (destination.position.z != start.position.z)
			{
				newLine = newLine.Append(String.Format("Z{0:0.###} ", destination.position.z));
			}

			if (moveHasExtrusion)
			{
				newLine = newLine.Append(String.Format("E{0:0.###} ", destination.extrusion));
			}

			if (destination.feedRate != start.feedRate)
			{
				newLine = newLine.Append(String.Format("F{0:0.##}", destination.feedRate));
			}

			lineBeingSent = newLine.ToString();
			return lineBeingSent.Trim();
		}

		public static PrinterMove GetPosition(string lineBeingSent, PrinterMove startPositionPosition)
		{
			PrinterMove currentDestination = startPositionPosition;
			GCodeFile.GetFirstNumberAfter("X", lineBeingSent, ref currentDestination.position.x);
			GCodeFile.GetFirstNumberAfter("Y", lineBeingSent, ref currentDestination.position.y);
			GCodeFile.GetFirstNumberAfter("Z", lineBeingSent, ref currentDestination.position.z);
			GCodeFile.GetFirstNumberAfter("E", lineBeingSent, ref currentDestination.extrusion);
			GCodeFile.GetFirstNumberAfter("F", lineBeingSent, ref currentDestination.feedRate);
			return currentDestination;
		}

		public static bool LineIsMovement(string lineBeingSent)
		{
			if (lineBeingSent.StartsWith("G0 ")
				|| lineBeingSent.StartsWith("G1 "))
			{
				return true;
			}

			return false;
		}
	}
}