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
using MatterHackers.Agg;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl
{
	public class TerminalLog
	{
		private static readonly bool Is32Bit = IntPtr.Size == 4;

		private int maxLinesToBuffer = int.MaxValue - 1;

		public TerminalLog(PrinterConnection printerConnection)
		{
			printerConnection.ConnectionFailed += Instance_ConnectionFailed;
			printerConnection.Disposed += (s, e) => printerConnection.ConnectionFailed -= Instance_ConnectionFailed;

			printerConnection.LineReceived += Printer_LineReceived;
			printerConnection.LineSent += Printer_LineSent;

			if (Is32Bit)
			{
				// About 10 megs worth. Average line length in gcode file is about 14 and we store strings as chars (16 bit) so 450,000 lines.
				maxLinesToBuffer = 450000;
			}
		}

		public event EventHandler<(string line, bool output)> HasChanged;

		public List<(string line, bool output)> PrinterLines { get; } = new List<(string line, bool output)>();

		private void OnHasChanged((string line, bool output) lineData)
		{
			HasChanged?.Invoke(this, lineData);
			if (PrinterLines.Count > maxLinesToBuffer)
			{
				Clear();
			}
		}

		private void Printer_LineReceived(object sender, string line)
		{
			PrinterLines.Add((line, false));
			OnHasChanged((line, false));
		}

		private void Printer_LineSent(object sender, string line)
		{
			PrinterLines.Add((line, true));
			OnHasChanged((line, true));
		}

		public void WriteLine(string line)
		{
			this.WriteLine((line, true));
		}

		public void WriteLine((string line, bool output) lineData)
		{
			PrinterLines.Add(lineData);
			OnHasChanged(lineData);
		}

		private void Instance_ConnectionFailed(object sender, EventArgs e)
		{
			OnHasChanged((null, true));

			if (e is ConnectFailedEventArgs args)
			{
				string message;

				switch(args.Reason)
				{
					case ConnectionFailure.AlreadyConnected:
						message = "You can only connect when not currently connected.".Localize();
						break;
					case ConnectionFailure.UnsupportedBaudRate:
						message = "Unsupported Baud Rate".Localize();
						break;
					case ConnectionFailure.PortInUse:
						message = "Serial port in use".Localize();
						break;
					case ConnectionFailure.PortNotFound:
						message = "Port not found".Localize();
						break;
					default:
						message = "Unknown Reason".Localize();
						break;
				}

				PrinterLines.Add(("Connection Failed".Localize() + ": " + message, true));
			}

			StringEventArgs eventArgs = new StringEventArgs("Lost connection to printer.");
			PrinterLines.Add((eventArgs.Data, true));
			OnHasChanged((eventArgs.Data, true));
		}

		public void Clear()
		{
			lock(PrinterLines)
			{
				PrinterLines.Clear();
			}

			OnHasChanged((null, true));
		}
	}
}