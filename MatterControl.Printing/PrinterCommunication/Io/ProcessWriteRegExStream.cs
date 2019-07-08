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

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MatterControl.Printing.Pipelines
{
	public class ProcessWriteRegexStream : GCodeStreamProxy
	{
		private PrinterMove currentMove = PrinterMove.Unknown;

		public static Regex GetQuotedParts = new Regex(@"([""'])(\\?.)*?\1", RegexOptions.Compiled);

		private QueuedCommandsStream queueStream;

		public override string DebugInfo => "";

		public ProcessWriteRegexStream(PrintHostConfig printer, GCodeStream internalStream, QueuedCommandsStream queueStream)
			: base(printer, internalStream)
		{
			this.queueStream = queueStream;
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

			// if the line has no content don't process it
			if (baseLine.Length == 0
				|| baseLine.Trim().Length == 0)
			{
				return baseLine;
			}

			var lines = ProcessWriteRegEx(baseLine, printer);
			for (int i = lines.Count - 1; i >= 1; i--)
			{
				queueStream.Add(lines[i], true);
			}

			var lineToSend = lines[0];

			if (lineToSend != null
				&& LineIsMovement(lineToSend))
			{
				currentMove = GetPosition(lineToSend, currentMove);
			}

			// is it a position set?
			if (lineToSend.StartsWith("G92"))
			{
				GCodeFile.GetFirstNumberAfter("X", lineToSend, ref this.currentMove.position.X);
				GCodeFile.GetFirstNumberAfter("Y", lineToSend, ref this.currentMove.position.Y);
				GCodeFile.GetFirstNumberAfter("Z", lineToSend, ref this.currentMove.position.Z);
				GCodeFile.GetFirstNumberAfter("E", lineToSend, ref this.currentMove.extrusion);

				// tell the stream pipeline what the actual printer position is
				this.SetPrinterPosition(this.currentMove);
			}

			return lineToSend;
		}

		public static List<string> ProcessWriteRegEx(string lineToWrite, PrintHostConfig printer)
		{
			var linesToWrite = new List<string>();
			linesToWrite.Add(lineToWrite);

			var addedLines = new List<string>();
			for (int i = 0; i < linesToWrite.Count; i++)
			{
				foreach (var item in printer.Settings.Helpers.WriteLineReplacements)
				{
					var splitReplacement = item.Replacement.Split(',');
					if (splitReplacement.Length > 0)
					{
						if (item.Regex.IsMatch(lineToWrite))
						{
							// replace on the first replacement group only
							var replacedString = item.Regex.Replace(lineToWrite, splitReplacement[0]);
							linesToWrite[i] = replacedString;
							// add in the other replacement groups
							for (int j = 1; j < splitReplacement.Length; j++)
							{
								addedLines.Add(splitReplacement[j]);
							}
							break;
						}
					}
				}
			}

			linesToWrite.AddRange(addedLines);

			return linesToWrite;
		}

		public override void SetPrinterPosition(PrinterMove position)
		{
			this.currentMove.CopyKnowSettings(position);
			internalStream.SetPrinterPosition(currentMove);
		}
	}
}