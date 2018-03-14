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

using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class SendProgressStream : GCodeStreamProxy
	{
		private double nextPercent = -1;
		PrinterConnection printerConnection;
		PrinterSettings printerSettings;
		PrintTask activePrintTask;

		public SendProgressStream(PrinterConnection printerConnection, PrinterSettings printerSettings, GCodeStream internalStream)
			: base(internalStream)
		{
			this.printerConnection = printerConnection;
			this.printerSettings = printerSettings;
		}

		public override string ReadLine()
		{
			if (printerSettings.GetValue(SettingsKey.progress_reporting) != "None"
				&& printerConnection.CommunicationState == CommunicationStates.Printing
				&& printerConnection.activePrintTask != null
				&& printerConnection.activePrintTask.PercentDone > nextPercent)
			{
				nextPercent = Math.Round(printerConnection.activePrintTask.PercentDone) + 0.5;
				if (printerSettings.GetValue(SettingsKey.progress_reporting) == "M73")
				{
					return String.Format("M73 P{0:0}", printerConnection.activePrintTask.PercentDone);
				}
				else
				{
					return String.Format("M117 Printing: {0:0}%", printerConnection.activePrintTask.PercentDone);
				}
			}

			return base.ReadLine();
		}
	}
}
