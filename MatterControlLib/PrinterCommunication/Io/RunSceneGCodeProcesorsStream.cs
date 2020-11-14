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

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MatterControl.Printing;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{

	public class RunSceneGCodeProcesorsStream : GCodeStreamProxy
	{
		private QueuedCommandsStream queueStream;
		private List<IGCodeTransformer> gcodeTransformers;

		public override string DebugInfo => "";

		public RunSceneGCodeProcesorsStream(PrinterConfig printer, GCodeStream internalStream, QueuedCommandsStream queueStream)
			: base(printer, internalStream)
		{
			this.queueStream = queueStream;

			// get all the gcode processors that are in the scene
			gcodeTransformers = printer.Bed.Scene.Descendants().Where(i => i is IGCodeTransformer).Select(i => (IGCodeTransformer)i).ToList();

			foreach (var gcodeTransformer in gcodeTransformers)
			{
				gcodeTransformer.Reset();
			}
		}

		public override string ReadLine()
		{
			var baseLine = base.ReadLine();

			if (baseLine == null)
			{
				return null;
			}

			if (baseLine.EndsWith("; NO_PROCESSING"))
			{
				return baseLine;
			}

			// if we are not printing or the line has no content don't process it
			if (baseLine.Length == 0
				|| baseLine.Trim().Length == 0)
			{
				return baseLine;
			}

			var lines = ProcessGCodeLine(baseLine, printer);
			for (int i = lines.Count - 1; i >= 1; i--)
			{
				queueStream.Add(lines[i], true);
			}

			var lineToSend = lines[0];

			return lineToSend;
		}

		private List<string> ProcessGCodeLine(string lineToWrite, PrinterConfig printer)
		{
			var linesToWrite = new List<string>
			{
				lineToWrite
			};

			var addedLines = new List<string>();
			foreach (var gcodeTransformer in gcodeTransformers)
			{
				addedLines.AddRange(gcodeTransformer.ProcessCGcode(lineToWrite, printer));
			}

			linesToWrite.AddRange(addedLines);

			return linesToWrite;
		}
	}
}