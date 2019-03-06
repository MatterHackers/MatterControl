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

using MatterControl.Printing;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class HotendTemperatureStream : GCodeStreamProxy
	{
		private bool haveSeenMultipleExtruders;
		private int extruderIndex;
		private QueuedCommandsStream queuedCommandsStream;
		int extruderCount = 0;

		public HotendTemperatureStream(PrinterConfig printer, GCodeStream internalStream, QueuedCommandsStream queuedCommandsStream)
			: base(printer, internalStream)
		{
			this.queuedCommandsStream = queuedCommandsStream;
			extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);
			extruderIndex = printer.Connection.ActiveExtruderIndex;
		}

		public override string DebugInfo
		{
			get
			{
				return $"";
			}
		}

		public override string ReadLine()
		{
			string lineToSend = base.ReadLine();

			if (lineToSend != null
				&& lineToSend.EndsWith("; NO_PROCESSING"))
			{
				return lineToSend;
			}

			TrackExtruderState(lineToSend);

			// when we are actively printing manage the extruder temperature
			if (haveSeenMultipleExtruders
				&& printer.Connection.Printing)
			{
				// get the time to the next extruder switch
				var toolChange = printer.Connection.NextToolChange();

				// if we do not switch again
				if (toolChange.time == double.PositiveInfinity)
				{
					// we do not switch extruders again, turn off any that are not currently printing
					for (int i = 0; i < extruderCount; i++)
					{
						if(i != extruderIndex)
						{
							printer.Connection.SetTargetHotendTemperature(i, 0, true);
						}
					}
				}

				// don't keep checking if need to turn off extruders
				haveSeenMultipleExtruders = false;
			}

			return lineToSend;
		}

		private void TrackExtruderState(string line)
		{
			if (line == null)
			{
				return;
			}

			if (line.StartsWith("G28)"))
			{
				extruderIndex = 0;
			}

			if (line.StartsWith("T"))
			{
				var newExtruder = extruderIndex;
				GCodeFile.GetFirstNumberAfter("T", line, ref newExtruder);
				if(newExtruder != extruderIndex)
				{
					haveSeenMultipleExtruders = true;
					extruderIndex = newExtruder;
				}
			}
		}
	}
}