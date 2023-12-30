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
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
	public class TerminalLog
	{
		private static readonly bool Is32Bit = IntPtr.Size == 4;

		private int maxLinesToBuffer = int.MaxValue - 1;


		public TerminalLog()
		{
			if (Is32Bit)
			{
				// About 10 megs worth. Average line length in gcode file is about 14 and we store strings as chars (16 bit) so 450,000 lines.
				maxLinesToBuffer = 450000;
			}
		}		

		private void WriteToFile(string filePath)
		{
			System.IO.File.WriteAllLines(filePath, AllLines());
		}

		public event EventHandler<TerminalLine> LineAdded;

		public event EventHandler LogCleared;

		private List<TerminalLine> printerLines = new List<TerminalLine>();

		public string[] AllLines()
		{
			lock (printerLines)
			{
				return printerLines.Select(ld => ld.Line).ToArray();
			}
		}

		public TerminalLine[] AllTerminalLines()
		{
			lock (printerLines)
			{
				return printerLines.ToArray();
			}
		}

		private void AddLine(TerminalLine terminalLine)
		{
			lock (printerLines)
			{
				printerLines.Add(terminalLine);

				LineAdded?.Invoke(this, terminalLine);

				if (printerLines.Count > maxLinesToBuffer)
				{
					this.Clear();
				}
			}
		}

		private void Printer_LineReceived(object sender, string line)
		{
			this.AddLine(
				new TerminalLine(
					line,
					TerminalLine.MessageDirection.FromPrinter));
		}

		private void Printer_LineSent(object sender, string line)
		{
			this.AddLine(
				new TerminalLine(
					line,
					TerminalLine.MessageDirection.ToPrinter));
		}

		public void WriteLine(string line)
		{
			if (line.Contains("\n"))
			{
				foreach (var segment in line.Replace("\r\n", "\n").Split('\n'))
				{
					this.AddLine(
						new TerminalLine(
							segment,
							TerminalLine.MessageDirection.ToTerminal));
				}
			}
			else
			{
				this.AddLine(
					new TerminalLine(
						line,
						TerminalLine.MessageDirection.ToTerminal));
			}
		}

		public void Clear()
		{
			lock (printerLines)
			{
				printerLines.Clear();
			}

			this.LogCleared?.Invoke(this, null);
		}
	}
}