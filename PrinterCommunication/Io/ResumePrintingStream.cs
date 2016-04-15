/*
Copyright (c) 2016, Lars Brubaker
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
using MatterHackers.GCodeVisualizer;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	internal class ResumePrintingStream : GCodeStream
	{
		private GCodeFileStream gCodeFileStream0;
		private GCodeFile loadedGCode;
		private double percentDone;

		public ResumePrintingStream(GCodeFile loadedGCode, GCodeFileStream gCodeFileStream0, double percentDone)
		{
			this.loadedGCode = loadedGCode;
			this.gCodeFileStream0 = gCodeFileStream0;
			this.percentDone = percentDone;
		}

		public override void Dispose()
		{
		}

		public override string ReadLine()
		{
			// heat the extrude to remove it from the part
			// remove it from the part
			// if top homing, home the extruder

			// run through the gcode that the device expected looking for things like temp
			// and skip everything else until we get to the point we left off last time
			while (loadedGCode.PercentComplete(gCodeFileStream0.LineIndex) < percentDone)
			{
				string line = gCodeFileStream0.ReadLine();
				// check if the line is something we want to send to the printer (like a temp)
				if (line.StartsWith("M109")
					|| line.StartsWith("M104"))
                {
					return line;
				}
			}

			bool isFirstLayerOfResume = false;

			string lineToSend = gCodeFileStream0.ReadLine();

			if (isFirstLayerOfResume)
			{
				// print at resume speed
				return lineToSend;
			}

			return lineToSend;
		}

		public override void SetPrinterPosition(PrinterMove position)
		{
			gCodeFileStream0.SetPrinterPosition(position);
		}
	}
}