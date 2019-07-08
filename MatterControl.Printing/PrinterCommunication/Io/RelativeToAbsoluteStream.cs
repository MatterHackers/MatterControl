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

namespace MatterControl.Printing.Pipelines
{
	public class RelativeToAbsoluteStream : GCodeStreamProxy
	{
		protected PrinterMove lastDestination = PrinterMove.Unknown;
		public PrinterMove LastDestination { get { return lastDestination; } }

		bool xyzAbsoluteMode = true;
		bool eAbsoluteMode = true;
		private bool haveSentG90;

		public RelativeToAbsoluteStream(PrintHostConfig printer, GCodeStream internalStream)
			: base(printer, internalStream)
		{
		}

		public override string DebugInfo
		{
			get
			{
				return $"Last Destination = {lastDestination}";
			}
		}

		public override void SetPrinterPosition(PrinterMove position)
		{
			this.lastDestination.CopyKnowSettings(position);
			internalStream.SetPrinterPosition(lastDestination);
		}

		public string ProcessLine(string lineToProcess)
		{
			if (lineToProcess != null
				&& lineToProcess.StartsWith("G9"))
			{
				if (lineToProcess.StartsWith("G91"))
				{
					xyzAbsoluteMode = false;
					eAbsoluteMode = false;
					return "";
				}
				else if (lineToProcess.StartsWith("G90"))
				{
					xyzAbsoluteMode = true;
					eAbsoluteMode = true;
					if (haveSentG90)
					{
						// If we have already set the printer to absolute mode, do not send it again.
						// This will guarantee we send it once and then we don't send it again (as this ensures we never send a G91).
						return "";
					}
					haveSentG90 = true;
				}

				if (lineToProcess.StartsWith("M83"))
				{
					// extruder to relative mode
					eAbsoluteMode = false;
				}
				else if (lineToProcess.StartsWith("82"))
				{
					// extruder to absolute mode
					eAbsoluteMode = true;
				}
			}

			if (lineToProcess != null
				&& LineIsMovement(lineToProcess))
			{
				PrinterMove currentDestination;
				if (xyzAbsoluteMode && eAbsoluteMode)
				{
					currentDestination = GetPosition(lineToProcess, lastDestination);
				}
				else
				{
					PrinterMove xyzDestination = GetPosition(lineToProcess, lastDestination);
					double feedRate = xyzDestination.feedRate;
					if (!xyzAbsoluteMode)
					{
						xyzDestination = GetPosition(lineToProcess, PrinterMove.Zero);
						xyzDestination += lastDestination;
					}

					PrinterMove eDestination = GetPosition(lineToProcess, lastDestination);
					if (!eAbsoluteMode)
					{
						eDestination = GetPosition(lineToProcess, PrinterMove.Zero);
						eDestination += lastDestination;
					}

					currentDestination.extrusion = eDestination.extrusion;
					currentDestination.feedRate = feedRate;
					currentDestination.position = xyzDestination.position;

					lineToProcess = CreateMovementLine(currentDestination, lastDestination);
				}

				// send the first one
				lastDestination = currentDestination;
			}

			return lineToProcess;
		}

		public override string ReadLine()
		{
			// G91 Relative
			// G90 Absolute
			string lineToSend = base.ReadLine();
			if (lineToSend != null
				&& lineToSend.EndsWith("; NO_PROCESSING"))
			{
				return lineToSend;
			}

			return ProcessLine(lineToSend);
		}
	}
}