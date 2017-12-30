/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public class TerminalLog
	{
		private static readonly bool Is32Bit = IntPtr.Size == 4;

		public List<string> PrinterLines = new List<string>();

		public RootedObjectEventHandler HasChanged = new RootedObjectEventHandler();
		private int maxLinesToBuffer = int.MaxValue - 1;

		private EventHandler unregisterEvents;

		public TerminalLog(PrinterConnection printerConnection)
		{
			printerConnection.ConnectionFailed.RegisterEvent(Instance_ConnectionFailed, ref unregisterEvents);
			printerConnection.CommunicationUnconditionalFromPrinter.RegisterEvent(FromPrinter, ref unregisterEvents);
			printerConnection.CommunicationUnconditionalToPrinter.RegisterEvent(ToPrinter, ref unregisterEvents);
			if (Is32Bit)
			{
				// About 10 megs worth. Average line length in gcode file is about 14 and we store strings as chars (16 bit) so 450,000 lines.
				maxLinesToBuffer = 450000;
			}
		}

		private void OnHasChanged(EventArgs e)
		{
			HasChanged.CallEvents(this, e);
			if (PrinterLines.Count > maxLinesToBuffer)
			{
				Clear();
			}
		}

		private void FromPrinter(Object sender, EventArgs e)
		{
			StringEventArgs lineString = e as StringEventArgs;
			StringEventArgs eventArgs = new StringEventArgs("<-" + lineString.Data);
			PrinterLines.Add(eventArgs.Data);
			OnHasChanged(eventArgs);
		}

		private void ToPrinter(Object sender, EventArgs e)
		{
			StringEventArgs lineString = e as StringEventArgs;
			StringEventArgs eventArgs = new StringEventArgs("->" + lineString.Data);
			PrinterLines.Add(eventArgs.Data);
			OnHasChanged(eventArgs);
		}

		public void WriteLine(string line)
		{
			StringEventArgs eventArgs = new StringEventArgs(line);
			PrinterLines.Add(eventArgs.Data);
			OnHasChanged(eventArgs);
		}

		private void Instance_ConnectionFailed(object sender, EventArgs e)
		{
			OnHasChanged(null);

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

				PrinterLines.Add("Connection Failed".Localize() + ": " + message);
			}

			StringEventArgs eventArgs = new StringEventArgs("Lost connection to printer.");
			PrinterLines.Add(eventArgs.Data);
			OnHasChanged(eventArgs);
		}

		public void Clear()
		{
			lock(PrinterLines)
			{
				PrinterLines.Clear();
			}
			OnHasChanged(null);
		}
	}
}